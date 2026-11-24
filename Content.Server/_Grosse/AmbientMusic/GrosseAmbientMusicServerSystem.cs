using System.IO;
using Content.Shared._Grosse.AmbientMusic;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Grosse.AmbientMusic;

/// <summary>
///     Sends the city ambience tracks to the clients that ask for them.
/// </summary>
/// <remarks>
///     The tracks are not part of the repository, so nothing copyrighted is shipped with the client. They live in the
///     folder of <see cref="CCVars.GrosseAmbientMusicDirectory"/> on the server machine and are sent at the start of
///     the round, one track per request. Only the files of the <c>GrosseCityAmbient</c> playlist are served,
///     so a client cannot read arbitrary files from the server.
/// </remarks>
public sealed partial class GrosseAmbientMusicServerSystem : EntitySystem
{
    private static readonly ProtoId<GrosseAmbientPlaylistPrototype> AmbientCollection = "GrosseCityAmbient";

    /// <summary>
    ///     Size of one part of a track. A whole track would be a multi-megabyte network message, which the network
    ///     layer is not made for.
    /// </summary>
    private const int ChunkSize = 64 * 1024;

    /// <summary>
    ///     Upper bound for one track, so a wrong path cannot push an arbitrary file through the server.
    /// </summary>
    private const int MaxTrackBytes = 24 * 1024 * 1024;

    /// <summary>
    ///     Pause between two tracks sent to the same client, so a client cannot flood the server with requests.
    ///     The client sends at most one request per <c>RequestInterval</c> anyway.
    /// </summary>
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromMilliseconds(300);

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _res = default!;

    /// <summary>
    ///     File names of the collection, the only names that can be requested.
    /// </summary>
    private readonly HashSet<string> _tracks = new();

    /// <summary>
    ///     Last time a track was sent to a client, for <see cref="RequestCooldown"/>.
    /// </summary>
    private readonly Dictionary<NetUserId, TimeSpan> _lastRequest = new();

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("grosse.ambientmusic.server");

        SubscribeNetworkEvent<AmbientTrackRequestEvent>(OnTrackRequested);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);

        LoadTracks();
    }

    private void OnTrackRequested(AmbientTrackRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var now = _timing.CurTime;

        if (_lastRequest.TryGetValue(session.UserId, out var last) && now - last < RequestCooldown)
            return;

        _lastRequest[session.UserId] = now;

        if (!_tracks.Contains(ev.Name))
        {
            _sawmill.Warning($"Ignored a request for {ev.Name}, it is not in {AmbientCollection}");
            RaiseNetworkEvent(new AmbientTrackMissingEvent(ev.Name), session);
            return;
        }

        if (!TryReadTrack(ev.Name, out var data))
        {
            RaiseNetworkEvent(new AmbientTrackMissingEvent(ev.Name), session);
            return;
        }

        var total = (data.Length + ChunkSize - 1) / ChunkSize;

        for (var index = 0; index < total; index++)
        {
            var offset = index * ChunkSize;
            var length = Math.Min(ChunkSize, data.Length - offset);
            var chunk = new byte[length];
            Buffer.BlockCopy(data, offset, chunk, 0, length);
            RaiseNetworkEvent(new AmbientTrackChunkEvent(ev.Name, index, total, chunk), session);
        }
    }

    /// <summary>
    ///     Reads <paramref name="name"/> from the configured folder.
    /// </summary>
    private bool TryReadTrack(string name, out byte[] data)
    {
        data = Array.Empty<byte>();

        var directory = _config.GetCVar(CCVars.GrosseAmbientMusicDirectory);

        if (string.IsNullOrWhiteSpace(directory))
            return false;

        if (!Path.IsPathRooted(directory))
        {
            // Integration tests and virtual filesystems have no physical UserData root; treat as unavailable.
            if (_res.UserData.RootDir is not { } root)
                return false;

            directory = Path.Combine(root, directory);
        }

        var path = Path.Combine(directory, name);

        try
        {
            var file = new FileInfo(path);

            if (!file.Exists)
            {
                _sawmill.Warning($"The ambient track {path} is missing");
                return false;
            }

            if (file.Length > MaxTrackBytes)
            {
                _sawmill.Error($"The ambient track {path} is larger than {MaxTrackBytes} bytes");
                return false;
            }

            data = File.ReadAllBytes(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _sawmill.Error($"Could not read the ambient track {path}: {e.Message}");
            return false;
        }
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        _lastRequest.Remove(ev.Player.UserId);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<GrosseAmbientPlaylistPrototype>())
            LoadTracks();
    }

    private void LoadTracks()
    {
        _tracks.Clear();

        if (!_proto.TryIndex(AmbientCollection, out GrosseAmbientPlaylistPrototype? collection))
        {
            _sawmill.Error($"Ambient playlist {AmbientCollection} is missing, no ambient track can be sent");
            return;
        }

        foreach (var file in collection.Files)
            _tracks.Add(new ResPath(file).Filename);

        var directory = _config.GetCVar(CCVars.GrosseAmbientMusicDirectory);

        if (string.IsNullOrWhiteSpace(directory))
        {
            _sawmill.Info($"{CCVars.GrosseAmbientMusicDirectory.Name} is empty, the city ambience is off");
        }
        else if (!Path.IsPathRooted(directory) && _res.UserData.RootDir == null)
        {
            _sawmill.Info($"Ambient music folder {directory} is unavailable (no UserData root), the city ambience is off");
        }
        else
        {
            _sawmill.Info($"Serving {_tracks.Count} ambient tracks from {directory}");
        }
    }
}
