using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Grosse.GhostRespawn;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.GhostRespawn;

public sealed partial class GhostRespawnSystem : EntitySystem
{
    [Dependency] private IBanManager _bans = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;

    private readonly Dictionary<NetUserId, HashSet<int>> _usedCharacters = new();
    private readonly Dictionary<NetUserId, GhostRespawnEui> _open = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnGhostAttached);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public void OpenMenu(ICommonSession player)
    {
        if (player.AttachedEntity is not { } attached || !HasComp<GhostComponent>(attached))
        {
            Send(player, Loc.GetString("ghost-respawn-not-ghost"));
            return;
        }

        if (!IsRespawnAvailable())
        {
            Send(player, Loc.GetString("ghost-respawn-unavailable"));
            return;
        }

        if (_open.TryGetValue(player.UserId, out var existing))
        {
            existing.StateDirty();
            return;
        }

        var ui = new GhostRespawnEui(this);
        _open[player.UserId] = ui;
        _eui.OpenEui(ui, player);
        ui.StateDirty();
    }

    public void OnEuiClosed(ICommonSession player)
    {
        _open.Remove(player.UserId);
    }

    public GhostRespawnEuiState BuildState(ICommonSession player)
    {
        var state = new GhostRespawnEuiState();
        var prefs = _prefs.GetPreferencesOrNull(player.UserId);
        if (prefs == null)
            return state;

        _usedCharacters.TryGetValue(player.UserId, out var used);
        foreach (var (slot, profile) in prefs.Characters.OrderBy(pair => pair.Value.Name, StringComparer.CurrentCulture))
        {
            if (used != null && used.Contains(slot))
                continue;

            state.Characters.Add(new GhostRespawnCharacter
            {
                Slot = slot,
                Name = profile.Name,
            });
        }

        var roles = CollectRoles(player);
        var multipleStations = roles.Select(role => role.Station).Distinct().Count() > 1;
        foreach (var role in roles)
        {
            var jobName = role.Job.LocalizedName;
            string label;
            if (role.Slots == 0)
                label = Loc.GetString("ghost-respawn-role-full", ("job", jobName));
            else if (role.Slots is { } count)
                label = Loc.GetString("ghost-respawn-role-slots", ("job", jobName), ("slots", count));
            else
                label = jobName;

            if (multipleStations)
                label = $"{Name(role.Station)}: {label}";

            state.Roles.Add(new GhostRespawnRole
            {
                Station = GetNetEntity(role.Station),
                JobId = role.Job.ID,
                Name = label,
                Selectable = role.Selectable,
            });
        }

        return state;
    }

    public void OnRespawnSelected(GhostRespawnEui ui, GhostRespawnSelectMessage args)
    {
        var player = ui.Player;
        if (player.AttachedEntity is not { } attached || !HasComp<GhostComponent>(attached))
        {
            Send(player, Loc.GetString("ghost-respawn-not-ghost"));
            _eui.CloseEui(ui);
            return;
        }

        var prefs = _prefs.GetPreferencesOrNull(player.UserId);
        if (prefs == null || !prefs.Characters.TryGetValue(args.CharacterSlot, out var profile))
        {
            Send(player, Loc.GetString("ghost-respawn-character-used"));
            ui.StateDirty();
            return;
        }

        if (_usedCharacters.TryGetValue(player.UserId, out var used) && used.Contains(args.CharacterSlot))
        {
            Send(player, Loc.GetString("ghost-respawn-character-used"));
            ui.StateDirty();
            return;
        }

        var station = GetEntity(args.Station);
        if (!TryComp<GhostRespawnWhitelistComponent>(station, out var whitelist) ||
            !whitelist.Jobs.Contains(args.JobId) ||
            !_prototypes.TryIndex<JobPrototype>(args.JobId, out var job) ||
            !_stationJobs.TryGetJobSlot(station, job, out var slots) ||
            slots == 0)
        {
            Send(player, Loc.GetString("ghost-respawn-role-unavailable"));
            ui.StateDirty();
            return;
        }

        if (!IsJobAllowed(player, job, profile))
        {
            Send(player, Loc.GetString("ghost-respawn-role-requirements"));
            ui.StateDirty();
            return;
        }

        MarkUsed(player.UserId, args.CharacterSlot);
        if (!_ticker.TrySpawnGhostRespawn(player, profile, station, args.JobId))
        {
            UnmarkUsed(player.UserId, args.CharacterSlot);
            Send(player, Loc.GetString("ghost-respawn-unavailable"));
            ui.StateDirty();
            return;
        }

        _eui.CloseEui(ui);
    }

    private void OnGhostAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        RaiseNetworkEvent(new GhostRespawnAvailabilityEvent(IsRespawnAvailable()), args.Player);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var prefs = _prefs.GetPreferencesOrNull(ev.Player.UserId);
        if (prefs == null)
            return;

        if (!prefs.TryIndexOfCharacter(ev.Profile, out var index))
        {
            Log.Warning($"No character slot matched spawned profile for {ev.Player}");
            return;
        }

        MarkUsed(ev.Player.UserId, index);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _usedCharacters.Clear();

        foreach (var ui in _open.Values.ToArray())
        {
            _eui.CloseEui(ui);
        }

        _open.Clear();
    }

    private List<RespawnRole> CollectRoles(ICommonSession player)
    {
        var roles = new List<RespawnRole>();
        var query = EntityQueryEnumerator<GhostRespawnWhitelistComponent, StationJobsComponent, StationSpawningComponent>();
        while (query.MoveNext(out var uid, out var whitelist, out _, out _))
        {
            if (Paused(uid) || TerminatingOrDeleted(uid))
                continue;

            foreach (var jobId in whitelist.Jobs)
            {
                if (!_prototypes.TryIndex(jobId, out var job))
                    continue;

                if (!_stationJobs.TryGetJobSlot(uid, job, out var slots))
                    continue;

                roles.Add(new RespawnRole(uid, job, slots, slots != 0 && IsJobAllowed(player, job, profile: null)));
            }
        }

        roles.Sort((a, b) => JobUIComparer.Instance.Compare(a.Job, b.Job));
        return roles;
    }

    private bool IsRespawnAvailable()
    {
        var query = EntityQueryEnumerator<GhostRespawnWhitelistComponent, StationJobsComponent, StationSpawningComponent>();
        while (query.MoveNext(out var uid, out var whitelist, out _, out _))
        {
            if (Paused(uid) || TerminatingOrDeleted(uid) || whitelist.Jobs.Count == 0)
                continue;

            return true;
        }

        return false;
    }

    private bool IsJobAllowed(ICommonSession player, JobPrototype job, HumanoidCharacterProfile? profile)
    {
        var bans = _bans.GetJobBans(player.UserId);
        if (bans != null && bans.Contains(job.ID))
            return false;

        if (!_jobWhitelist.IsAllowed(player, job.ID))
            return false;

        if (!_cfg.GetCVar(CCVars.GameRoleTimers))
            return true;

        if (!_playTime.TryGetTrackerTimes(player, out var playTimes))
            playTimes = new Dictionary<string, TimeSpan>();

        return JobRequirements.TryRequirementsMet(job, playTimes, out _, EntityManager, _prototypes, profile);
    }

    private void MarkUsed(NetUserId userId, int slot)
    {
        if (!_usedCharacters.TryGetValue(userId, out var used))
        {
            used = new HashSet<int>();
            _usedCharacters[userId] = used;
        }

        used.Add(slot);
    }

    private void UnmarkUsed(NetUserId userId, int slot)
    {
        if (_usedCharacters.TryGetValue(userId, out var used))
            used.Remove(slot);
    }

    private void Send(ICommonSession player, string message)
    {
        _chat.DispatchServerMessage(player, message);
    }

    private readonly record struct RespawnRole(EntityUid Station, JobPrototype Job, int? Slots, bool Selectable);
}
