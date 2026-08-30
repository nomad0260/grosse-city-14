using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Clothing.Systems;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server._Grosse.Assault.UI;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Assault.Components;
using Content.Shared._Grosse.Assault.UI;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Assault;

public sealed partial class AssaultRuleSystem : GameRuleSystem<AssaultRuleComponent>
{
    [Dependency] private AssaultLobbySystem _lobby = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private OutfitSystem _outfit = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedAssaultGateSystem _gates = default!;
    [Dependency] private SharedAssaultSpawnBlockerSystem _blockers = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private StationSystem _station = default!;

    private readonly Dictionary<NetUserId, AssaultClassSelectEui> _classUis = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawning);
        SubscribeLocalEvent<AssaultPlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<AssaultCapturePointComponent, AssaultZoneCapturedEvent>(OnZoneCapturedEvent);
        SubscribeNetworkEvent<AssaultLateJoinRequestEvent>(OnLateJoin);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    protected override void Started(EntityUid uid, AssaultRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Phase = AssaultPhase.Prep;
        component.CurrentZone = 0;
        component.TotalZones = GetMaxZone() + 1;
        if (component.TotalZones <= 0)
            component.TotalZones = 1;

        component.AttackersTickets = component.StartingTickets;
        component.DefendersTickets = component.StartingTickets;
        component.PrepEndsAt = Timing.CurTime + component.PrepTime;
        component.RoundEndsAt = Timing.CurTime + component.RoundTime;
        component.Winner = null;
        component.Players.Clear();

        ResetCapturePoints();
        _blockers.UpdateForZone(component.CurrentZone);
        Announce("assault-announce-prep", ("time", (int) component.PrepTime.TotalSeconds));
        BroadcastHud(component);
    }

    protected override void Ended(EntityUid uid, AssaultRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Phase = AssaultPhase.Ended;
        foreach (var eui in _classUis.Values.ToList())
        {
            eui.Close();
        }

        _classUis.Clear();
        RaiseNetworkEvent(new AssaultHudUpdateEvent { Enabled = false });
        _lobby.BroadcastAll();
    }

    protected override void AppendRoundEndText(EntityUid uid, AssaultRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        if (component.Winner == AssaultTeam.Attackers)
            args.AddLine(Loc.GetString("assault-roundend-attackers"));
        else if (component.Winner == AssaultTeam.Defenders)
            args.AddLine(Loc.GetString("assault-roundend-defenders"));
        else
            args.AddLine(Loc.GetString("assault-roundend-draw"));

        args.AddLine(Loc.GetString("assault-roundend-tickets",
            ("attackers", component.AttackersTickets),
            ("defenders", component.DefendersTickets)));
        args.AddLine(Loc.GetString("assault-roundend-zone",
            ("zone", component.CurrentZone + 1),
            ("total", component.TotalZones)));
    }

    protected override void ActiveTick(EntityUid uid, AssaultRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (component.Phase is AssaultPhase.Ended or AssaultPhase.Lobby)
            return;

        var now = Timing.CurTime;

        if (now >= component.RoundEndsAt)
        {
            EndAs(component, AssaultTeam.Defenders, "assault-announce-timeout");
            return;
        }

        switch (component.Phase)
        {
            case AssaultPhase.Prep when now >= component.PrepEndsAt:
                component.Phase = AssaultPhase.Attack;
                Announce("assault-announce-attack");
                break;
            case AssaultPhase.Intermission when now >= component.IntermissionEndsAt:
                _gates.UnlockForZone(component.CurrentZone);
                component.Phase = AssaultPhase.Attack;
                Announce("assault-announce-gates", ("zone", component.CurrentZone + 1));
                break;
        }

        UpdateWaves(component);
        if (component.Phase != AssaultPhase.Ended)
            TryEndIfAttackersDepleted(component);

        if (now >= component.HudNextUpdate)
        {
            component.HudNextUpdate = now + TimeSpan.FromSeconds(1);
            BroadcastHud(component);
        }
    }

    private void OnPlayerSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<AssaultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var players = ev.PlayerPool.ToList();
            ev.PlayerPool.Clear();
            AssignAndSpawn(rule, players);
            return;
        }
    }

    private void AssignAndSpawn(AssaultRuleComponent rule, List<ICommonSession> players)
    {
        _random.Shuffle(players);
        foreach (var session in players)
        {
            AssignPlayer(rule, session);
        }

        SpawnWave(rule, AssaultTeam.Attackers, chargeTickets: true);
        SpawnWave(rule, AssaultTeam.Defenders, chargeTickets: true);
        _lobby.BroadcastAll();
    }

    private void AssignPlayer(AssaultRuleComponent rule, ICommonSession session)
    {
        _lobby.TryGetChoice(session.UserId, out var choice);
        var team = ResolveTeam(rule, choice);
        var cls = ResolveClass(team, choice, GetTickets(rule, team));

        rule.Players[session.UserId] = new AssaultPlayerSlot
        {
            Team = team,
            Class = cls,
            InWaveQueue = true,
            QueuedAt = Timing.CurTime,
        };
    }

    private AssaultTeam ResolveTeam(AssaultRuleComponent rule, AssaultLobbyChoice? choice)
    {
        var atk = CountTeam(rule, AssaultTeam.Attackers);
        var def = CountTeam(rule, AssaultTeam.Defenders);
        var preferred = choice is { Random: false, Team: { } team } ? team : (AssaultTeam?) null;

        if (preferred == AssaultTeam.Attackers && atk <= def)
            return AssaultTeam.Attackers;
        if (preferred == AssaultTeam.Defenders && def <= atk)
            return AssaultTeam.Defenders;

        return atk <= def ? AssaultTeam.Attackers : AssaultTeam.Defenders;
    }

    private ProtoId<AssaultClassPrototype>? ResolveClass(AssaultTeam team, AssaultLobbyChoice? choice, int tickets)
    {
        if (choice is { Random: false, Class: { } selected }
            && Proto.TryIndex(selected, out AssaultClassPrototype? proto)
            && proto.Team == team
            && proto.Cost <= tickets)
        {
            return selected;
        }

        return PickRandomClass(team, tickets);
    }

    private ProtoId<AssaultClassPrototype>? PickRandomClass(AssaultTeam team, int tickets)
    {
        var options = Proto.EnumeratePrototypes<AssaultClassPrototype>()
            .Where(c => c.Team == team && c.Cost <= tickets)
            .ToList();

        if (options.Count == 0)
            return null;

        return _random.Pick(options).ID;
    }

    private int CountTeam(AssaultRuleComponent rule, AssaultTeam team)
    {
        var count = 0;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.Team == team)
                count++;
        }

        return count;
    }

    public void TrySelectClass(NetUserId user, string classId)
    {
        if (!TryGetActiveRule(out var rule) || !rule.Players.TryGetValue(user, out var slot))
            return;

        if (!Proto.TryIndex<AssaultClassPrototype>(classId, out var proto) || proto.Team != slot.Team)
            return;

        slot.Class = proto.ID;
        RefreshClassUi(user, rule, slot);
    }

    public void OnClassSelectClosed(NetUserId user)
    {
        _classUis.Remove(user);
    }

    private void OnLateJoin(AssaultLateJoinRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetActiveRule(out var rule))
            return;

        var session = args.SenderSession;
        if (rule.Players.ContainsKey(session.UserId))
            return;

        if (!_lobby.HasValidChoice(session.UserId))
        {
            _chat.DispatchServerMessage(session, Loc.GetString("assault-lobby-need-loadout"));
            return;
        }

        AssignPlayer(rule, session);
        if (rule.Players.TryGetValue(session.UserId, out var slot))
            OpenClassSelect(session, rule, slot);

        _lobby.BroadcastAll();
        BroadcastHud(rule);
        _chat.DispatchServerMessage(session, Loc.GetString("assault-lobby-queued"));
    }

    private void OnMobStateChanged(Entity<AssaultPlayerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryGetActiveRule(out var rule))
            return;

        if (!rule.Players.TryGetValue(ent.Comp.UserId, out var slot))
            return;

        slot.InWaveQueue = true;
        slot.QueuedAt = Timing.CurTime;
        slot.Class = ent.Comp.Class;

        if (_players.TryGetSessionById(ent.Comp.UserId, out var session))
            OpenClassSelect(session, rule, slot);

        BroadcastHud(rule);
        TryEndIfAttackersDepleted(rule);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (!TryGetActiveRule(out var rule))
            return;

        rule.Players.Remove(args.Session.UserId);
        if (_classUis.Remove(args.Session.UserId, out var eui))
            eui.Close();

        BroadcastHud(rule);
    }

    private void UpdateWaves(AssaultRuleComponent rule)
    {
        TrySpawnWave(rule, AssaultTeam.Attackers);
        TrySpawnWave(rule, AssaultTeam.Defenders);
    }

    private void TrySpawnWave(AssaultRuleComponent rule, AssaultTeam team)
    {
        var total = 0;
        var dead = 0;
        var oldest = TimeSpan.MaxValue;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.Team != team)
                continue;

            total++;
            if (slot.InWaveQueue || !IsAlive(slot))
            {
                dead++;
                if (slot.QueuedAt < oldest)
                    oldest = slot.QueuedAt;
            }
        }

        if (total == 0 || dead == 0)
            return;

        var ratio = (float) dead / total;
        var timedOut = oldest != TimeSpan.MaxValue && Timing.CurTime - oldest >= rule.WaveTimeout;
        if (ratio < rule.WaveThreshold && !timedOut)
            return;

        SpawnWave(rule, team, chargeTickets: true);
    }

    private void SpawnWave(AssaultRuleComponent rule, AssaultTeam team, bool chargeTickets)
    {
        foreach (var (user, slot) in rule.Players.ToList())
        {
            if (slot.Team != team || !slot.InWaveQueue)
                continue;

            if (!_players.TryGetSessionById(user, out var session))
                continue;

            var tickets = GetTickets(rule, team);
            slot.Class ??= PickRandomClass(team, tickets);
            if (slot.Class == null || !Proto.TryIndex(slot.Class.Value, out AssaultClassPrototype? proto))
                continue;

            if (chargeTickets && proto.Cost > tickets)
                continue;

            if (!TryGetSpawnCoords(rule, team, out var coords))
                continue;

            if (chargeTickets)
                SetTickets(rule, team, tickets - proto.Cost);

            SpawnPlayer(rule, session, slot, proto, coords);
        }

        BroadcastHud(rule);
        _lobby.BroadcastAll();
    }

    private void SpawnPlayer(
        AssaultRuleComponent rule,
        ICommonSession session,
        AssaultPlayerSlot slot,
        AssaultClassPrototype proto,
        EntityCoordinates coords)
    {
        var profile = GameTicker.GetPlayerProfile(session);
        var station = _station.GetStations().FirstOrDefault();
        var mob = _spawning.SpawnPlayerMob(coords, null, profile, station == default ? null : station);

        EntityUid mindId;
        MindComponent mind;
        if (_mind.TryGetMind(session.UserId, out var existingMind, out var existingComp) && existingMind != null && existingComp != null)
        {
            mindId = existingMind.Value;
            mind = existingComp;
            if (mind.OwnedEntity is { } old && old != mob)
                QueueDel(old);
        }
        else
        {
            var created = _mind.CreateMind(session.UserId, profile.Name);
            mindId = created.Owner;
            mind = created.Comp;
        }

        _mind.SetUserId(mindId, session.UserId, mind);
        _mind.TransferTo(mindId, mob, mind: mind);
        _outfit.SetOutfit(mob, proto.StartingGear);

        _factions.ClearFactions(mob);
        _factions.AddFaction(mob,
            slot.Team == AssaultTeam.Attackers
                ? AssaultConstants.AttackersFaction
                : AssaultConstants.DefendersFaction);

        ApplyTeamCollisionMask(mob, slot.Team);

        var playerComp = EnsureComp<AssaultPlayerComponent>(mob);
        playerComp.Team = slot.Team;
        playerComp.Class = proto.ID;
        playerComp.UserId = session.UserId;

        slot.Mob = mob;
        slot.InWaveQueue = false;
        slot.Class = proto.ID;

        if (_classUis.Remove(session.UserId, out var eui))
            eui.Close();

        GameTicker.PlayerJoinGame(session, silent: true);
    }

    private bool TryGetSpawnCoords(AssaultRuleComponent rule, AssaultTeam team, out EntityCoordinates coords)
    {
        var zone = team == AssaultTeam.Attackers
            ? Math.Max(0, rule.CurrentZone - 1)
            : rule.CurrentZone;

        var points = new List<EntityUid>();
        var query = EntityQueryEnumerator<AssaultSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out _))
        {
            if (spawn.Team == team && spawn.ZoneIndex == zone)
                points.Add(uid);
        }

        if (points.Count == 0)
        {
            query = EntityQueryEnumerator<AssaultSpawnPointComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var spawn, out _))
            {
                if (spawn.Team == team)
                    points.Add(uid);
            }
        }

        if (points.Count == 0)
        {
            coords = default;
            return false;
        }

        coords = Transform(_random.Pick(points)).Coordinates;
        return true;
    }

    private void ApplyTeamCollisionMask(EntityUid mob, AssaultTeam team)
    {
        if (!TryComp<FixturesComponent>(mob, out var fixtures))
            return;

        var remove = (int) (team == AssaultTeam.Attackers
            ? CollisionGroup.AssaultAttackersImpassable
            : CollisionGroup.AssaultDefendersImpassable);

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            _physics.SetCollisionMask(mob, id, fixture, fixture.CollisionMask & ~remove, fixtures);
        }
    }

    private void OnZoneCapturedEvent(Entity<AssaultCapturePointComponent> ent, ref AssaultZoneCapturedEvent args)
    {
        if (!TryGetActiveRule(out var rule) || args.ZoneIndex != rule.CurrentZone)
            return;

        OnZoneCaptured(rule);
    }

    private void OnZoneCaptured(AssaultRuleComponent rule)
    {
        rule.AttackersTickets += rule.AttackersCaptureReward;
        rule.DefendersTickets += rule.DefendersCaptureReward;

        if (rule.CurrentZone + 1 >= rule.TotalZones)
        {
            EndAs(rule, AssaultTeam.Attackers, "assault-announce-last-point");
            return;
        }

        rule.CurrentZone++;
        rule.Phase = AssaultPhase.Intermission;
        rule.IntermissionEndsAt = Timing.CurTime + rule.GateOpenDelay;
        _blockers.UpdateForZone(rule.CurrentZone);
        Announce("assault-announce-captured",
            ("zone", rule.CurrentZone),
            ("next", rule.CurrentZone + 1),
            ("delay", (int) rule.GateOpenDelay.TotalSeconds),
            ("atk", rule.AttackersCaptureReward),
            ("def", rule.DefendersCaptureReward));
        BroadcastHud(rule);
    }

    private void TryEndIfAttackersDepleted(AssaultRuleComponent rule)
    {
        if (rule.Phase == AssaultPhase.Ended)
            return;

        var minCost = GetMinCost(AssaultTeam.Attackers);
        var living = 0;
        var queuedCanSpawn = false;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.Team != AssaultTeam.Attackers)
                continue;

            if (IsAlive(slot))
                living++;

            if (slot.InWaveQueue)
            {
                var cost = minCost;
                if (slot.Class != null && Proto.TryIndex(slot.Class.Value, out AssaultClassPrototype? proto))
                    cost = proto.Cost;
                if (cost <= rule.AttackersTickets)
                    queuedCanSpawn = true;
            }
        }

        if (living == 0 && !queuedCanSpawn && rule.AttackersTickets < minCost)
            EndAs(rule, AssaultTeam.Defenders, "assault-announce-tickets");
    }

    private int GetMinCost(AssaultTeam team)
    {
        var min = int.MaxValue;
        foreach (var proto in Proto.EnumeratePrototypes<AssaultClassPrototype>())
        {
            if (proto.Team != team)
                continue;
            min = Math.Min(min, proto.Cost);
        }

        return min == int.MaxValue ? 1 : min;
    }

    private void EndAs(AssaultRuleComponent rule, AssaultTeam winner, string announce)
    {
        if (rule.Phase == AssaultPhase.Ended)
            return;

        rule.Phase = AssaultPhase.Ended;
        rule.Winner = winner;
        Announce(announce);
        BroadcastHud(rule);
        _roundEnd.EndRound(rule.RestartDelay);
    }

    private void OpenClassSelect(ICommonSession session, AssaultRuleComponent rule, AssaultPlayerSlot slot)
    {
        var state = BuildClassState(rule, slot);
        if (_classUis.TryGetValue(session.UserId, out var existing))
        {
            existing.UpdateState(state);
            return;
        }

        var eui = new AssaultClassSelectEui(this, session.UserId, state);
        _classUis[session.UserId] = eui;
        _eui.OpenEui(eui, session);
        eui.StateDirty();
    }

    private void RefreshClassUi(NetUserId user, AssaultRuleComponent rule, AssaultPlayerSlot slot)
    {
        if (!_classUis.TryGetValue(user, out var eui))
            return;

        eui.UpdateState(BuildClassState(rule, slot));
    }

    private AssaultClassSelectEuiState BuildClassState(AssaultRuleComponent rule, AssaultPlayerSlot slot)
    {
        var tickets = GetTickets(rule, slot.Team);
        var state = new AssaultClassSelectEuiState
        {
            Team = slot.Team,
            Tickets = tickets,
            SelectedClass = slot.Class,
        };

        foreach (var proto in Proto.EnumeratePrototypes<AssaultClassPrototype>())
        {
            if (proto.Team != slot.Team)
                continue;

            state.Classes.Add(new AssaultClassSelectInfo
            {
                Id = proto.ID,
                Name = Loc.GetString(proto.Name),
                Description = Loc.GetString(proto.Description),
                Cost = proto.Cost,
                Affordable = proto.Cost <= tickets,
            });
        }

        return state;
    }

    private void BroadcastHud(AssaultRuleComponent rule)
    {
        var (atkDead, atkTotal) = CountWave(rule, AssaultTeam.Attackers);
        var (defDead, defTotal) = CountWave(rule, AssaultTeam.Defenders);
        var progress = 0f;
        var capQuery = EntityQueryEnumerator<AssaultCapturePointComponent>();
        while (capQuery.MoveNext(out _, out var point))
        {
            if (!point.Captured && point.ZoneIndex == rule.CurrentZone)
            {
                progress = Math.Max(progress, point.Progress);
            }
        }

        var phaseEnd = rule.Phase switch
        {
            AssaultPhase.Prep => rule.PrepEndsAt,
            AssaultPhase.Intermission => rule.IntermissionEndsAt,
            _ => rule.RoundEndsAt,
        };

        RaiseNetworkEvent(new AssaultHudUpdateEvent
        {
            Enabled = rule.Phase != AssaultPhase.Ended,
            Phase = rule.Phase,
            AttackersTickets = rule.AttackersTickets,
            DefendersTickets = rule.DefendersTickets,
            CurrentZone = rule.CurrentZone + 1,
            TotalZones = rule.TotalZones,
            PhaseEndsAt = phaseEnd,
            RoundEndsAt = rule.RoundEndsAt,
            AttackersDead = atkDead,
            AttackersTotal = atkTotal,
            DefendersDead = defDead,
            DefendersTotal = defTotal,
            WaveThreshold = rule.WaveThreshold,
            CaptureProgress = progress,
        });
    }

    private (int Dead, int Total) CountWave(AssaultRuleComponent rule, AssaultTeam team)
    {
        var dead = 0;
        var total = 0;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.Team != team)
                continue;
            total++;
            if (slot.InWaveQueue || !IsAlive(slot))
                dead++;
        }

        return (dead, total);
    }

    private bool IsAlive(AssaultPlayerSlot slot)
    {
        return slot.Mob is { } mob
               && !Deleted(mob)
               && TryComp<MobStateComponent>(mob, out var state)
               && state.CurrentState == MobState.Alive;
    }

    private int GetTickets(AssaultRuleComponent rule, AssaultTeam team)
    {
        return team == AssaultTeam.Attackers ? rule.AttackersTickets : rule.DefendersTickets;
    }

    private void SetTickets(AssaultRuleComponent rule, AssaultTeam team, int value)
    {
        if (team == AssaultTeam.Attackers)
            rule.AttackersTickets = Math.Max(0, value);
        else
            rule.DefendersTickets = Math.Max(0, value);
    }

    private int GetMaxZone()
    {
        var max = -1;
        var query = AllEntityQuery<AssaultCapturePointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            max = Math.Max(max, point.ZoneIndex);
        }

        return max;
    }

    private void ResetCapturePoints()
    {
        var query = AllEntityQuery<AssaultCapturePointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            point.Progress = 0f;
            point.Captured = false;
            point.VisualState = AssaultCaptureState.Idle;
            point.Occupants.Clear();
            Dirty(uid, point);
        }

        var gates = AllEntityQuery<AssaultGateComponent>();
        while (gates.MoveNext(out _, out var gate))
        {
            gate.Opened = false;
        }
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out AssaultRuleComponent? rule)
    {
        var query = EntityQueryEnumerator<AssaultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out rule, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive(uid, gameRule))
                return true;
        }

        rule = null;
        return false;
    }

    public bool TryGetRuleForStatus([NotNullWhen(true)] out AssaultRuleComponent? rule)
    {
        return TryGetActiveRule(out rule);
    }

    private void Announce(string locId, params (string, object)[] args)
    {
        _chat.DispatchServerAnnouncement(Loc.GetString(locId, args));
    }
}
