using Content.Client.Gameplay;
using Content.Shared._Grosse.AmbientMusic;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.AmbientMusic;

/// <summary>
///     Plays the ambient bed of the city: the tracks of <see cref="AmbientCollection"/> one after another,
///     shuffled into playlists, with a short fade and a pause between them.
/// </summary>
/// <remarks>
///     The bed is only heard during a round. Its volume is the music setting
///     (<see cref="CCVars.AmbientMusicVolume"/>, also adjustable on the music player tab of the options), so the
///     slider controls the ambience live, including muting it at zero.
/// </remarks>
public sealed partial class GrosseAmbientMusicSystem : EntitySystem
{
    /// <summary>
    ///     Playlist with the ambient tracks.
    /// </summary>
    private static readonly ProtoId<GrosseAmbientPlaylistPrototype> AmbientCollection = "GrosseCityAmbient";

    /// <summary>
    ///     Volume the tracks are normalised at, before the music setting is added on top of it.
    ///     Volumes are in dB with gain = 10^(dB/10), so the old -14 dB was raised to -11 dB to play twice as loud.
    /// </summary>
    private const float BaseVolume = -11f;

    /// <summary>
    ///     Volume a track starts from and fades in to <see cref="BaseVolume"/>.
    /// </summary>
    private const float SilentVolume = -32f;

    /// <summary>
    ///     Fade speed in dB per second.
    /// </summary>
    private const float FadeSpeed = 10f;

    /// <summary>
    ///     Silence between two tracks.
    /// </summary>
    private static readonly TimeSpan TrackGap = TimeSpan.FromSeconds(4);

    /// <summary>
    ///     Delay before the first track of a round, so the bed joins the round after the player
    ///     is fully spawned instead of starting on the exact frame of connecting.
    /// </summary>
    private static readonly TimeSpan FirstTrackDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Delay before trying the playlist again, e.g. when it failed to load.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     How long to wait before checking a track that is still being downloaded again.
    /// </summary>
    private static readonly TimeSpan LoadingRetryDelay = TimeSpan.FromSeconds(1);

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private GrosseAmbientTrackCache _cache = default!;

    /// <summary>
    ///     All tracks of the collection.
    /// </summary>
    private readonly List<ResPath> _tracks = new();

    /// <summary>
    ///     Tracks that were not played yet in the current playlist pass.
    /// </summary>
    private readonly List<ResPath> _queue = new();

    /// <summary>
    ///     Tracks that were played this round, oldest first, so the music player can step back.
    /// </summary>
    private readonly List<ResPath> _history = new();

    /// <summary>
    ///     How many tracks back the music player can step.
    /// </summary>
    private const int HistoryLength = 32;

    private ISawmill _sawmill = default!;

    private EntityUid? _stream;
    private ResPath? _currentTrack;
    private ResPath? _pendingTrack;
    private float _pendingPosition;
    private TimeSpan _currentLength;
    private bool _paused;
    private bool _inRound;
    private float _targetVolume = BaseVolume;
    private float _volume = SilentVolume;
    private TimeSpan _nextTrackAt;

    /// <summary>
    ///     Track that is loaded right now, or was loaded last. Stays set while paused and between tracks.
    /// </summary>
    public ResPath? CurrentTrack => _currentTrack;

    /// <summary>
    ///     True while the listener paused the bed from the music player.
    /// </summary>
    /// <remarks>
    ///     Deliberately not named <c>IsPaused</c>: that would hide <see cref="EntitySystem.IsPaused(EntityUid?, MetaDataComponent?)"/>.
    /// </remarks>
    public bool IsPausedByPlayer => _paused;

    /// <summary>
    ///     True while a track is actually audible.
    /// </summary>
    public bool IsPlaying => _stream != null && !_paused;

    /// <summary>
    ///     Length of <see cref="CurrentTrack"/>, for the music player's progress bar.
    /// </summary>
    public TimeSpan TrackLength => _currentLength;

    /// <summary>
    ///     Playback position of the current track, for the music player's progress bar.
    /// </summary>
    public TimeSpan TrackPosition
    {
        get
        {
            if (_paused)
                return TimeSpan.FromSeconds(_pendingPosition);

            if (_stream != null && TryComp(_stream, out AudioComponent? component))
                return TimeSpan.FromSeconds(MathF.Max(0f, component.PlaybackPosition));

            return TimeSpan.Zero;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        // Purely local playback, so it has to run even when the client is not predicting.
        UpdatesOutsidePrediction = true;
        _sawmill = _logManager.GetSawmill("grosse.ambientmusic");

        Subs.CVar(_config, CCVars.AmbientMusicVolume, OnMusicVolumeChanged, true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        LoadTracks();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopAmbience();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Don't play anything extra while the client is re-simulating ticks.
        if (!_timing.IsFirstTimePredicted)
            return;

        // The bed is in-round only: nothing in the lobby, on the round end summary or when disconnected.
        // Without a local entity there is no one to play for (e.g. after "entities delete"), so the
        // last track must be stopped instead of lingering as a client-side audio entity.
        // The music setting at zero also mutes it, like it mutes the ambient music.
        if (_state.CurrentState is not GameplayState || !_net.IsConnected || _player.LocalEntity == null ||
            _targetVolume <= SilentVolume)
        {
            _inRound = false;
            StopAmbience();
            return;
        }

        if (!_inRound)
        {
            _inRound = true;
            _nextTrackAt = _timing.CurTime + FirstTrackDelay;

            // The tracks come from the server, so the first one is asked for while the round is settling down.
            if (_queue.Count == 0 && _tracks.Count > 0)
                FillQueue();

            PrefetchNextTrack();
        }

        // The listener pressed pause in the music player: hold the playlist where it is.
        if (_paused)
            return;

        if (_stream != null)
        {
            // The engine deletes the audio entity once the track is over.
            if (!TryComp(_stream, out AudioComponent? component) || !component.Playing)
            {
                _stream = null;
                _nextTrackAt = _timing.CurTime + TrackGap;
            }
            else
            {
                FadeTowards(_stream.Value, component, frameTime);
                return;
            }
        }

        if (_timing.CurTime < _nextTrackAt)
            return;

        // A track chosen by the player (resume after pause or "previous track") goes first.
        if (_pendingTrack is { } pending)
        {
            if (!_cache.TryTake(pending, out var pendingStream))
            {
                // Its bytes were dropped after playback, so it has to be asked for again before it can resume.
                _cache.Request(pending);
                _nextTrackAt = _timing.CurTime + LoadingRetryDelay;
                return;
            }

            var startAt = _pendingPosition;
            _pendingTrack = null;
            _pendingPosition = 0f;
            PlayTrack(pending, pendingStream, startAt);
            return;
        }

        PlayNextTrack();
    }

    /// <summary>
    ///     Moves the volume of the playing track towards the one the music setting asks for.
    /// </summary>
    private void FadeTowards(EntityUid stream, AudioComponent component, float frameTime)
    {
        var difference = _targetVolume - _volume;
        var step = FadeSpeed * frameTime;

        _volume = MathF.Abs(difference) <= step
            ? _targetVolume
            : _volume + MathF.Sign(difference) * step;

        _audio.SetVolume(stream, _volume, component);
    }

    private void PlayNextTrack()
    {
        if (_tracks.Count == 0)
        {
            LoadTracks();

            if (_tracks.Count == 0)
            {
                _nextTrackAt = _timing.CurTime + RetryDelay;
                return;
            }
        }

        if (_queue.Count == 0)
            FillQueue();

        // The tracks are not shipped with the client: play the first one that is already downloaded, ask for the
        // others and drop the ones the server does not have from this pass.
        while (_queue.Count > 0)
        {
            var track = _queue[^1];

            if (_cache.IsUnavailable(track))
            {
                _queue.RemoveAt(_queue.Count - 1);
                continue;
            }

            if (!_cache.TryTake(track, out var stream))
            {
                _cache.Request(track);
                _nextTrackAt = _timing.CurTime + LoadingRetryDelay;
                return;
            }

            _queue.RemoveAt(_queue.Count - 1);
            PlayTrack(track, stream);
            PrefetchNextTrack();
            return;
        }

        _nextTrackAt = _timing.CurTime + RetryDelay;
    }

    /// <summary>
    ///     Asks the server for the track after the current one, so the playlist rarely waits for a download.
    /// </summary>
    private void PrefetchNextTrack()
    {
        if (_queue.Count == 0)
            return;

        var next = _queue[^1];

        if (!_cache.IsUnavailable(next))
            _cache.Request(next);
    }

    /// <summary>
    ///     Starts <paramref name="stream"/> from <paramref name="startAt"/> seconds, fading in from silence.
    /// </summary>
    private void PlayTrack(ResPath track, AudioStream stream, float startAt = 0f)
    {
        _volume = SilentVolume;

        // The track is played from the stream the server sent, the path only identifies it for the music player
        // and the logs: there is no such file in the client resources.
        var played = _audio.PlayGlobal(
            stream,
            new ResolvedPathSpecifier(track),
            AudioParams.Default.WithVolume(_volume).WithPlayOffset(startAt));

        if (played == null)
        {
            _sawmill.Warning($"Could not play the ambient track {track}");
            _nextTrackAt = _timing.CurTime + RetryDelay;
            return;
        }

        _stream = played.Value.Entity;
        _currentTrack = track;
        _currentLength = stream.Length;

        _history.Add(track);

        if (_history.Count > HistoryLength)
            _history.RemoveAt(0);
    }

    /// <summary>
    ///     Pauses the bed where it is, or resumes the paused track from the same spot.
    /// </summary>
    public void TogglePause()
    {
        if (_paused)
        {
            _paused = false;
            _nextTrackAt = TimeSpan.Zero;
            return;
        }

        _paused = true;

        if (_stream != null)
        {
            _pendingTrack = _currentTrack;
            _pendingPosition = TryComp(_stream, out AudioComponent? component)
                ? MathF.Max(0f, component.PlaybackPosition)
                : 0f;

            _stream = _audio.Stop(_stream);
            return;
        }

        // Nothing is playing right now (between tracks): just hold the playlist.
        _pendingTrack = null;
        _pendingPosition = 0f;
    }

    /// <summary>
    ///     Stops the current track so the playlist picks the next one.
    /// </summary>
    public void SkipTrack()
    {
        StopAmbience();
        _paused = false;
        _pendingTrack = null;
        _pendingPosition = 0f;
    }

    /// <summary>
    ///     Steps back to the track that played before the current one.
    /// </summary>
    public void PlayPreviousTrack()
    {
        if (_history.Count < 2)
            return;

        _history.RemoveAt(_history.Count - 1);
        var previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);

        StopAmbience();
        _paused = false;
        _pendingTrack = previous;
        _pendingPosition = 0f;
    }

    /// <summary>
    ///     Shuffles the whole playlist into the queue, so a pass plays every track exactly once and only
    ///     then starts a freshly shuffled one.
    /// </summary>
    private void FillQueue()
    {
        _queue.AddRange(_tracks);
        _random.Shuffle(_queue);

        // The pass wrapped around: don't replay the track that just finished right away.
        if (_queue.Count > 1 && _currentTrack != null && _queue[^1] == _currentTrack.Value)
            (_queue[0], _queue[^1]) = (_queue[^1], _queue[0]);
    }

    private void LoadTracks()
    {
        _tracks.Clear();

        if (!_proto.TryIndex(AmbientCollection, out GrosseAmbientPlaylistPrototype? collection))
        {
            _sawmill.Error($"Ambient playlist {AmbientCollection} is missing, the city will have no ambience");
            _queue.Clear();
            return;
        }

        foreach (var file in collection.Files)
        {
            var resPath = new ResPath(file);

            // A file listed twice would play twice in the same pass, so the playlist is deduplicated.
            if (!_tracks.Contains(resPath))
                _tracks.Add(resPath);
        }

        // Keep the pass that is already running: only tracks that are gone leave it.
        _queue.RemoveAll(track => !_tracks.Contains(track));
    }

    private void OnMusicVolumeChanged(float volume)
    {
        _targetVolume = volume <= 0f
            ? SilentVolume
            : MathF.Max(SilentVolume, BaseVolume + SharedAudioSystem.GainToVolume(volume));
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<GrosseAmbientPlaylistPrototype>())
            return;

        LoadTracks();
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        StopAmbience();
        _paused = false;
        _pendingTrack = null;
        _pendingPosition = 0f;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        StopAmbience();
        _paused = false;
        _pendingTrack = null;
        _pendingPosition = 0f;
        _queue.Clear();
        _history.Clear();
    }

    private void StopAmbience()
    {
        _stream = _audio.Stop(_stream);
        _nextTrackAt = TimeSpan.Zero;
    }
}
