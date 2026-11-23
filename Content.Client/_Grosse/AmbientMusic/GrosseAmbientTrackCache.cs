using System.Diagnostics.CodeAnalysis;
using System.IO;
using Content.Shared._Grosse.AmbientMusic;
using Robust.Client.Audio;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.AmbientMusic;

/// <summary>
///     Keeps the city ambience playable without shipping the tracks with the client: they are requested from the
///     server, assembled from chunks and decoded into an <see cref="AudioStream"/>.
/// </summary>
/// <remarks>
///     A decoded track is tens of megabytes, so only one stream is kept: the previous one is released as soon as the
///     next track is decoded. The compressed bytes are dropped after decoding, which keeps the memory use at about
///     one track plus the download in flight.
/// </remarks>
public sealed partial class GrosseAmbientTrackCache : EntitySystem
{
    /// <summary>
    ///     Largest track the server is allowed to send.
    /// </summary>
    private const int MaxTrackBytes = 24 * 1024 * 1024;

    /// <summary>
    ///     How long a started download may stay unfinished before it is dropped.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Pause before asking again for a track that could not be downloaded.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Pause between two requests, so the playlist does not send the whole album at once.
    /// </summary>
    private static readonly TimeSpan RequestInterval = TimeSpan.FromMilliseconds(600);

    private sealed class Transfer
    {
        public required byte[][] Chunks;
        public int Received;
        public TimeSpan StartedAt;
    }

    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;

    /// <summary>
    ///     Tracks asked for, but not sent yet.
    /// </summary>
    private readonly HashSet<string> _wanted = new();

    /// <summary>
    ///     Tracks that are already downloaded and wait to be decoded.
    /// </summary>
    private readonly Dictionary<string, byte[]> _ready = new();

    /// <summary>
    ///     Tracks that are arriving right now.
    /// </summary>
    private readonly Dictionary<string, Transfer> _transfers = new();

    /// <summary>
    ///     When each track was last requested, so a lost message is asked for again.
    /// </summary>
    private readonly Dictionary<string, TimeSpan> _sentAt = new();

    /// <summary>
    ///     Until when a track is not worth asking for: the server does not have it, or it never arrived.
    /// </summary>
    private readonly Dictionary<string, TimeSpan> _retryAt = new();

    private readonly List<string> _expired = new();

    private ISawmill _sawmill = default!;
    private AudioStream? _decoded;
    private string? _decodedName;
    private AudioStream? _stale;
    private TimeSpan _nextRequestAt;

    public override void Initialize()
    {
        base.Initialize();

        // Downloads and stream lifetime are purely local, so they have to run even when the client is not predicting.
        UpdatesOutsidePrediction = true;
        _sawmill = _logManager.GetSawmill("grosse.ambientmusic");

        SubscribeNetworkEvent<AmbientTrackChunkEvent>(OnChunk);
        SubscribeNetworkEvent<AmbientTrackMissingEvent>(OnMissing);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _decoded?.Dispose();
        _stale?.Dispose();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The audio entity of the previous track may still be waiting for deletion in the tick the next track is
        // decoded in, so its stream is released a tick later instead of immediately.
        if (_stale != null)
        {
            _stale.Dispose();
            _stale = null;
        }

        ExpireTransfers();
        SendRequests();
    }

    /// <summary>
    ///     Stream to play <paramref name="track"/> from. False while the track is still on its way.
    /// </summary>
    public bool TryTake(ResPath track, [NotNullWhen(true)] out AudioStream? stream)
    {
        stream = null;

        var name = track.Filename;

        if (_decoded != null && _decodedName == name)
        {
            stream = _decoded;
            return true;
        }

        if (!_ready.Remove(name, out var data))
            return false;

        try
        {
            using var memory = new MemoryStream(data, writable: false);
            var decoded = _audioManager.LoadAudioOggVorbis(memory, name);

            if (_decoded != null)
            {
                _stale?.Dispose();
                _stale = _decoded;
            }

            _decoded = decoded;
            _decodedName = name;
            stream = decoded;
            return true;
        }
        catch (Exception e)
        {
            // A truncated or damaged download must not take the client down with it.
            _sawmill.Error($"Could not decode the ambient track {name}: {e.Message}");
            _retryAt[name] = _timing.CurTime + RetryDelay;
            return false;
        }
    }

    /// <summary>
    ///     Asks the server for <paramref name="track"/> unless it is already here or on its way.
    /// </summary>
    public void Request(ResPath track)
    {
        _wanted.Add(track.Filename);
    }

    /// <summary>
    ///     True while the server has no such track or it never arrived, so the playlist can skip it for a while.
    /// </summary>
    public bool IsUnavailable(ResPath track)
    {
        return _retryAt.TryGetValue(track.Filename, out var retryAt) && _timing.CurTime < retryAt;
    }

    private void SendRequests()
    {
        if (_wanted.Count == 0)
            return;

        _wanted.RemoveWhere(IsSettled);

        if (_wanted.Count == 0 || _timing.CurTime < _nextRequestAt)
            return;

        string? name = null;

        foreach (var wanted in _wanted)
        {
            name = wanted;
            break;
        }

        if (name == null)
            return;

        _wanted.Remove(name);

        _sentAt[name] = _timing.CurTime;
        _nextRequestAt = _timing.CurTime + RequestInterval;

        RaiseNetworkEvent(new AmbientTrackRequestEvent(name));
    }

    /// <summary>
    ///     True when a wanted track needs no request: it is here, arriving, recently asked for or on cooldown.
    /// </summary>
    private bool IsSettled(string name)
    {
        if (_decodedName == name || _ready.ContainsKey(name) || _transfers.ContainsKey(name))
            return true;

        if (_sentAt.TryGetValue(name, out var sentAt) && _timing.CurTime - sentAt < RequestTimeout)
            return true;

        return _retryAt.TryGetValue(name, out var retryAt) && _timing.CurTime < retryAt;
    }

    private void ExpireTransfers()
    {
        if (_transfers.Count == 0)
            return;

        _expired.Clear();

        foreach (var (name, transfer) in _transfers)
        {
            if (_timing.CurTime - transfer.StartedAt > RequestTimeout)
                _expired.Add(name);
        }

        foreach (var name in _expired)
        {
            _transfers.Remove(name);
            _sentAt.Remove(name);
            _retryAt[name] = _timing.CurTime + RetryDelay;
            _sawmill.Warning($"The ambient track {name} did not arrive in time");
        }
    }

    private void OnChunk(AmbientTrackChunkEvent ev)
    {
        if (ev.Total <= 0 || ev.Index < 0 || ev.Index >= ev.Total)
            return;

        if (!_transfers.TryGetValue(ev.Name, out var transfer))
        {
            transfer = new Transfer
            {
                Chunks = new byte[ev.Total][],
                StartedAt = _timing.CurTime,
            };

            _transfers[ev.Name] = transfer;
        }
        else if (transfer.Chunks.Length != ev.Total)
        {
            // A stale piece of an older download of the same track.
            return;
        }

        if (transfer.Chunks[ev.Index] != null)
            return;

        transfer.Chunks[ev.Index] = ev.Data;
        transfer.Received++;

        if (transfer.Received < transfer.Chunks.Length)
            return;

        _transfers.Remove(ev.Name);
        Assemble(ev.Name, transfer);
    }

    private void Assemble(string name, Transfer transfer)
    {
        var total = 0;

        foreach (var chunk in transfer.Chunks)
            total += chunk.Length;

        if (total == 0 || total > MaxTrackBytes)
        {
            _sawmill.Error($"The ambient track {name} has an unexpected size of {total} bytes");
            _retryAt[name] = _timing.CurTime + RetryDelay;
            return;
        }

        var data = new byte[total];
        var offset = 0;

        foreach (var chunk in transfer.Chunks)
        {
            // Array.Copy instead of Buffer.BlockCopy: the content sandbox rejects System.Buffer at load.
            Array.Copy(chunk, 0, data, offset, chunk.Length);
            offset += chunk.Length;
        }

        // The bytes are here, so the request no longer counts as pending and the track can be asked for again at
        // once if it is dropped after decoding.
        _sentAt.Remove(name);
        _ready[name] = data;
    }

    private void OnMissing(AmbientTrackMissingEvent ev)
    {
        _wanted.Remove(ev.Name);
        _sentAt.Remove(ev.Name);
        _transfers.Remove(ev.Name);
        _retryAt[ev.Name] = _timing.CurTime + RetryDelay;
        _sawmill.Warning($"The server does not have the ambient track {ev.Name}");
    }
}
