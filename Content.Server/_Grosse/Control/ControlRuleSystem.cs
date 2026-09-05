using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Clothing.Systems;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server._Grosse.Pvp;
using Content.Server._Grosse.Pvp.UI;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Control;
using Content.Shared._Grosse.Control.Components;
using Content.Shared._Grosse.Pvp;
using Content.Shared._Grosse.Pvp.UI;
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

namespace Content.Server._Grosse.Control;

public sealed partial class ControlRuleSystem : GameRuleSystem<ControlRuleComponent>
{
    [Dependency] private PvpLobbySystem _lobby = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameMapManager _gameMap = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private OutfitSystem _outfit = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedControlGateSystem _gates = default!;
    [Dependency] private SharedControlSpawnBlockerSystem _blockers = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private StationSystem _station = default!;

    private readonly Dictionary<NetUserId, PvpClassSelectEui> _classUis = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawning);
        SubscribeLocalEvent<ControlPlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    protected override void Started(EntityUid uid, ControlRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Phase = ControlPhase.Prep;
        ApplyTeamConfig(component);
        component.PrepEndsAt = Timing.CurTime + component.PrepTime;
        component.RoundEndsAt = Timing.CurTime + component.RoundTime;
        component.NextScoreTick = Timing.CurTime + component.PrepTime + component.ScoreInterval;
        component.Winner = null;
        component.AttackersScore = 0;
        component.DefendersScore = 0;
        component.AttackersComebackGiven = false;
        component.DefendersComebackGiven = false;
        component.Players.Clear();

        ResetCapturePoints();
        _blockers.SetAllActive(true);
        Announce("control-announce-prep", ("time", (int) component.PrepTime.TotalSeconds));
        BroadcastHud(component);
    }

    protected override void Ended(EntityUid uid, ControlRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Phase = ControlPhase.Ended;
        foreach (var eui in _classUis.Values.ToList())
        {
            eui.Close();
        }

        _classUis.Clear();
        RaiseNetworkEvent(new ControlHudUpdateEvent { Enabled = false });
        _lobby.BroadcastAll();
    }

    protected override void AppendRoundEndText(EntityUid uid, ControlRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        if (component.Winner == PvpTeam.Attackers)
            args.AddLine(Loc.GetString("control-roundend-attackers", ("name", TeamName(component, PvpTeam.Attackers))));
        else if (component.Winner == PvpTeam.Defenders)
            args.AddLine(Loc.GetString("control-roundend-defenders", ("name", TeamName(component, PvpTeam.Defenders))));
        else
            args.AddLine(Loc.GetString("control-roundend-draw"));

        args.AddLine(Loc.GetString("control-roundend-score",
            ("attackersName", TeamName(component, PvpTeam.Attackers)),
            ("defendersName", TeamName(component, PvpTeam.Defenders)),
            ("attackers", component.AttackersScore),
            ("defenders", component.DefendersScore),
            ("cap", component.ScoreCap)));
    }

    protected override void ActiveTick(EntityUid uid, ControlRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (component.Phase is ControlPhase.Ended or ControlPhase.Lobby)
            return;

        var now = Timing.CurTime;

        switch (component.Phase)
        {
            case ControlPhase.Prep when now >= component.PrepEndsAt:
                _gates.UnlockAll();
                component.Phase = ControlPhase.Fight;
                component.NextScoreTick = now + component.ScoreInterval;
                Announce("control-announce-fight");
                break;
            case ControlPhase.Fight:
                if (now >= component.RoundEndsAt)
                {
                    TryStartLastStand(component);
                    break;
                }

                TickScore(component);
                TryComebackCrate(component);
                UpdateWaves(component);
                if (component.AttackersScore >= component.ScoreCap || component.DefendersScore >= component.ScoreCap)
                    TryStartLastStand(component);
                break;
            case ControlPhase.LastStand:
                UpdateWaves(component);
                if (now >= component.LastStandEndsAt || !HasLivingLosers(component))
                    FinishRound(component);
                break;
        }

        if (now >= component.HudNextUpdate)
        {
            component.HudNextUpdate = now + TimeSpan.FromSeconds(1);
            BroadcastHud(component);
        }
    }

    private void OnPlayerSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<ControlRuleComponent, GameRuleComponent>();
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

    private void AssignAndSpawn(ControlRuleComponent rule, List<ICommonSession> players)
    {
        _random.Shuffle(players);
        foreach (var session in players)
        {
            AssignPlayer(rule, session);
        }

        SpawnWave(rule, PvpTeam.Attackers);
        SpawnWave(rule, PvpTeam.Defenders);

        foreach (var (user, slot) in rule.Players)
        {
            if (slot.Class != null || !slot.InWaveQueue)
                continue;

            if (_players.TryGetSessionById(user, out var session))
                OpenClassSelect(session, rule, slot);
        }

        _lobby.BroadcastAll();
    }

    private void AssignPlayer(ControlRuleComponent rule, ICommonSession session)
    {
        _lobby.TryGetChoice(session.UserId, out var choice);
        var team = ResolveTeam(rule, choice);
        var cls = ResolveClass(rule, team, choice, session.UserId);

        rule.Players[session.UserId] = new ControlPlayerSlot
        {
            Team = team,
            Class = cls,
            InWaveQueue = true,
            QueuedAt = Timing.CurTime,
        };
    }

    private PvpTeam ResolveTeam(ControlRuleComponent rule, PvpLobbyChoice? choice)
    {
        var atk = CountTeam(rule, PvpTeam.Attackers);
        var def = CountTeam(rule, PvpTeam.Defenders);
        var preferred = choice is { Random: false, Team: { } team } ? team : (PvpTeam?) null;

        if (preferred == PvpTeam.Attackers && atk <= def)
            return PvpTeam.Attackers;
        if (preferred == PvpTeam.Defenders && def <= atk)
            return PvpTeam.Defenders;

        return atk <= def ? PvpTeam.Attackers : PvpTeam.Defenders;
    }

    private ProtoId<AssaultClassPrototype>? ResolveClass(ControlRuleComponent rule, PvpTeam team, PvpLobbyChoice? choice, NetUserId user)
    {
        if (choice is { Random: false, Class: { } selected }
            && Proto.TryIndex<AssaultClassPrototype>(selected, out _)
            && TeamHasClass(rule, team, selected)
            && CanAssignClass(user, selected))
        {
            return selected;
        }

        return PickRandomClass(rule, team, user);
    }

    private ProtoId<AssaultClassPrototype>? PickRandomClass(ControlRuleComponent rule, PvpTeam team, NetUserId user)
    {
        var options = new List<AssaultClassPrototype>();
        foreach (var proto in EnumerateTeamClasses(rule, team))
        {
            if (CanAssignClass(user, proto.ID))
                options.Add(proto);
        }

        if (options.Count == 0)
            return null;

        return _random.Pick(options).ID;
    }

    private bool CanAssignClass(NetUserId user, string classId)
    {
        return _lobby.CanSelectClass(user, classId, includeUnassignedLobby: false);
    }

    private int CountTeam(ControlRuleComponent rule, PvpTeam team)
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

        if (!Proto.TryIndex<AssaultClassPrototype>(classId, out var proto)
            || !TeamHasClass(rule, slot.Team, proto.ID))
            return;

        if (!_lobby.CanSelectClass(user, proto.ID))
        {
            if (_players.TryGetSessionById(user, out var session))
                _chat.DispatchServerMessage(session, Loc.GetString("control-lobby-class-full"));
            RefreshClassUi(user, rule, slot);
            return;
        }

        slot.Class = proto.ID;
        NotifyClassOccupancy(rule);
    }

    public void OnClassSelectClosed(NetUserId user)
    {
        _classUis.Remove(user);
    }

    public void TryLateJoin(ICommonSession session)
    {
        if (!TryGetActiveRule(out var rule) || rule.Phase is ControlPhase.Ended or ControlPhase.LastStand)
            return;

        if (rule.Players.ContainsKey(session.UserId))
            return;

        if (!_lobby.HasValidChoice(session.UserId))
        {
            _chat.DispatchServerMessage(session, Loc.GetString("control-lobby-need-loadout"));
            return;
        }

        AssignPlayer(rule, session);
        if (rule.Players.TryGetValue(session.UserId, out var slot))
            OpenClassSelect(session, rule, slot);

        NotifyClassOccupancy(rule);
        BroadcastHud(rule);
        _chat.DispatchServerMessage(session, Loc.GetString("control-lobby-queued"));
    }

    private void OnMobStateChanged(Entity<ControlPlayerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryGetActiveRule(out var rule))
            return;

        if (!rule.Players.TryGetValue(ent.Comp.UserId, out var slot))
            return;

        if (rule.Phase == ControlPhase.LastStand && slot.Team != rule.Winner)
        {
            slot.InWaveQueue = false;
            BroadcastHud(rule);
            return;
        }

        slot.InWaveQueue = true;
        slot.QueuedAt = Timing.CurTime;
        slot.Class = ent.Comp.Class;

        if (_players.TryGetSessionById(ent.Comp.UserId, out var session))
            OpenClassSelect(session, rule, slot);

        BroadcastHud(rule);
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

        NotifyClassOccupancy(rule);
        BroadcastHud(rule);
    }

    private void UpdateWaves(ControlRuleComponent rule)
    {
        TrySpawnWave(rule, PvpTeam.Attackers);
        TrySpawnWave(rule, PvpTeam.Defenders);
    }

    private void TrySpawnWave(ControlRuleComponent rule, PvpTeam team)
    {
        if (rule.Phase == ControlPhase.LastStand && team != rule.Winner)
            return;

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

        SpawnWave(rule, team);
    }

    private void SpawnWave(ControlRuleComponent rule, PvpTeam team)
    {
        foreach (var (user, slot) in rule.Players.ToList())
        {
            if (slot.Team != team || !slot.InWaveQueue)
                continue;

            if (rule.Phase == ControlPhase.LastStand && team != rule.Winner)
                continue;

            if (!_players.TryGetSessionById(user, out var session))
                continue;

            if (slot.Class == null
                || !TeamHasClass(rule, team, slot.Class.Value)
                || !CanAssignClass(user, slot.Class.Value))
                slot.Class = PickRandomClass(rule, team, user);

            if (slot.Class == null || !Proto.TryIndex(slot.Class.Value, out AssaultClassPrototype? proto))
            {
                OpenClassSelect(session, rule, slot);
                continue;
            }

            if (!TryGetSpawnCoords(team, out var coords))
                continue;

            SpawnPlayer(rule, session, slot, proto, coords);
        }

        BroadcastHud(rule);
        NotifyClassOccupancy(rule);
    }

    private void SpawnPlayer(
        ControlRuleComponent rule,
        ICommonSession session,
        ControlPlayerSlot slot,
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
            slot.Team == PvpTeam.Attackers
                ? ControlConstants.AttackersFaction
                : ControlConstants.DefendersFaction);

        ApplyTeamCollisionMask(mob, slot.Team);

        var playerComp = EnsureComp<ControlPlayerComponent>(mob);
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

    private bool TryGetSpawnCoords(PvpTeam team, out EntityCoordinates coords)
    {
        var points = new List<EntityUid>();
        var query = EntityQueryEnumerator<ControlSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out _))
        {
            if (spawn.Team == team)
                points.Add(uid);
        }

        if (points.Count == 0)
        {
            coords = default;
            return false;
        }

        coords = Transform(_random.Pick(points)).Coordinates;
        return true;
    }

    private void ApplyTeamCollisionMask(EntityUid mob, PvpTeam team)
    {
        if (!TryComp<FixturesComponent>(mob, out var fixtures))
            return;

        var remove = (int) (team == PvpTeam.Attackers
            ? CollisionGroup.AssaultAttackersImpassable
            : CollisionGroup.AssaultDefendersImpassable);

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            _physics.SetCollisionMask(mob, id, fixture, fixture.CollisionMask & ~remove, fixtures);
        }
    }

    private void TickScore(ControlRuleComponent rule)
    {
        if (Timing.CurTime < rule.NextScoreTick)
            return;

        rule.NextScoreTick = Timing.CurTime + rule.ScoreInterval;
        var atk = 0;
        var def = 0;
        var query = EntityQueryEnumerator<ControlCapturePointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            if (point.Owner == PvpTeam.Attackers)
                atk++;
            else if (point.Owner == PvpTeam.Defenders)
                def++;
        }

        rule.AttackersScore += atk * rule.ScorePerHeldPoint;
        rule.DefendersScore += def * rule.ScorePerHeldPoint;
    }

    private void TryComebackCrate(ControlRuleComponent rule)
    {
        var diff = rule.AttackersScore - rule.DefendersScore;
        if (diff <= -rule.ComebackDeficit && !rule.AttackersComebackGiven)
        {
            if (SpawnComebackCrate(PvpTeam.Attackers))
            {
                rule.AttackersComebackGiven = true;
                Announce("control-announce-comeback", ("name", TeamName(rule, PvpTeam.Attackers)));
            }
        }
        else if (diff >= rule.ComebackDeficit && !rule.DefendersComebackGiven)
        {
            if (SpawnComebackCrate(PvpTeam.Defenders))
            {
                rule.DefendersComebackGiven = true;
                Announce("control-announce-comeback", ("name", TeamName(rule, PvpTeam.Defenders)));
            }
        }
    }

    private bool SpawnComebackCrate(PvpTeam team)
    {
        var markers = new List<EntityUid>();
        var query = EntityQueryEnumerator<ControlComebackCrateSpawnComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out _))
        {
            if (spawn.Team == team)
                markers.Add(uid);
        }

        if (markers.Count == 0)
            return false;

        var marker = _random.Pick(markers);
        Spawn(ControlConstants.ComebackCratePrototypeId, Transform(marker).Coordinates);
        return true;
    }

    private void TryStartLastStand(ControlRuleComponent rule)
    {
        PvpTeam? winner = null;
        if (rule.AttackersScore > rule.DefendersScore)
            winner = PvpTeam.Attackers;
        else if (rule.DefendersScore > rule.AttackersScore)
            winner = PvpTeam.Defenders;

        if (winner == null)
        {
            FinishRound(rule);
            return;
        }

        rule.Winner = winner;
        rule.Phase = ControlPhase.LastStand;
        rule.LastStandEndsAt = Timing.CurTime + rule.LastStandTime;
        _blockers.SetAllActive(false);

        var losers = winner == PvpTeam.Attackers ? PvpTeam.Defenders : PvpTeam.Attackers;
        Announce("control-announce-retreat",
            ("winner", TeamName(rule, winner.Value)),
            ("loser", TeamName(rule, losers)),
            ("time", (int) rule.LastStandTime.TotalSeconds));

        foreach (var (user, slot) in rule.Players)
        {
            if (slot.Team != losers)
                continue;

            slot.InWaveQueue = true;
            slot.QueuedAt = Timing.CurTime;
            slot.LastStandSpent = true;
        }

        SpawnWave(rule, losers);
        foreach (var slot in rule.Players.Values)
        {
            if (slot.Team == losers)
                slot.InWaveQueue = false;
        }
    }

    private bool HasLivingLosers(ControlRuleComponent rule)
    {
        if (rule.Winner is not { } winner)
            return false;

        var losers = winner == PvpTeam.Attackers ? PvpTeam.Defenders : PvpTeam.Attackers;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.Team != losers)
                continue;

            if (IsAlive(slot))
                return true;
        }

        return false;
    }

    private void FinishRound(ControlRuleComponent rule)
    {
        if (rule.Phase == ControlPhase.Ended)
            return;

        rule.Phase = ControlPhase.Ended;
        if (rule.Winner is { } winner)
        {
            Announce("control-announce-end", ("name", TeamName(rule, winner)));
        }
        else
        {
            Announce("control-announce-draw");
        }

        BroadcastHud(rule);
        _roundEnd.EndRound(rule.RestartDelay);
    }

    private void OpenClassSelect(ICommonSession session, ControlRuleComponent rule, ControlPlayerSlot slot)
    {
        var state = BuildClassState(rule, slot, session.UserId);
        if (_classUis.TryGetValue(session.UserId, out var existing))
        {
            existing.UpdateState(state);
            return;
        }

        var eui = new PvpClassSelectEui(session.UserId, state, TrySelectClass, OnClassSelectClosed);
        _classUis[session.UserId] = eui;
        _eui.OpenEui(eui, session);
        eui.StateDirty();
    }

    private void RefreshClassUi(NetUserId user, ControlRuleComponent rule, ControlPlayerSlot slot)
    {
        if (!_classUis.TryGetValue(user, out var eui))
            return;

        eui.UpdateState(BuildClassState(rule, slot, user));
    }

    private void RefreshAllClassUis(ControlRuleComponent rule)
    {
        foreach (var (user, eui) in _classUis.ToList())
        {
            if (!rule.Players.TryGetValue(user, out var slot))
                continue;

            eui.UpdateState(BuildClassState(rule, slot, user));
        }
    }

    private void NotifyClassOccupancy(ControlRuleComponent rule)
    {
        RefreshAllClassUis(rule);
        _lobby.BroadcastAll();
    }

    private PvpClassSelectEuiState BuildClassState(ControlRuleComponent rule, ControlPlayerSlot slot, NetUserId user)
    {
        var state = new PvpClassSelectEuiState
        {
            Team = slot.Team,
            ShowTickets = false,
            ShowClassCost = false,
            SelectedClass = slot.Class,
        };

        foreach (var proto in EnumerateTeamClasses(rule, slot.Team))
        {
            state.Classes.Add(new PvpClassSelectInfo
            {
                Id = proto.ID,
                Name = Loc.GetString(proto.Name),
                Description = Loc.GetString(proto.Description),
                Cost = proto.Cost,
                Affordable = true,
                Available = _lobby.CanSelectClass(user, proto.ID),
            });
        }

        return state;
    }

    private void BroadcastHud(ControlRuleComponent rule)
    {
        var (atkDead, atkTotal) = CountWave(rule, PvpTeam.Attackers);
        var (defDead, defTotal) = CountWave(rule, PvpTeam.Defenders);
        var points = new List<ControlPointHudInfo>();
        var query = EntityQueryEnumerator<ControlCapturePointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            points.Add(new ControlPointHudInfo
            {
                Name = string.IsNullOrEmpty(point.PointName)
                    ? Loc.GetString("control-capture-unnamed")
                    : Loc.GetString(point.PointName),
                Owner = point.Owner,
                VisualState = point.VisualState,
            });
        }

        var phaseEnd = rule.Phase switch
        {
            ControlPhase.Prep => rule.PrepEndsAt,
            ControlPhase.LastStand => rule.LastStandEndsAt,
            _ => rule.RoundEndsAt,
        };

        RaiseNetworkEvent(new ControlHudUpdateEvent
        {
            Enabled = rule.Phase is not ControlPhase.Ended and not ControlPhase.Lobby,
            Phase = rule.Phase,
            AttackersScore = rule.AttackersScore,
            DefendersScore = rule.DefendersScore,
            ScoreCap = rule.ScoreCap,
            PhaseEndsAt = phaseEnd,
            RoundEndsAt = rule.RoundEndsAt,
            AttackersDead = atkDead,
            AttackersTotal = atkTotal,
            DefendersDead = defDead,
            DefendersTotal = defTotal,
            WaveThreshold = rule.WaveThreshold,
            AttackersTeam = rule.AttackersTeam,
            DefendersTeam = rule.DefendersTeam,
            Winner = rule.Winner,
            Points = points,
        });
    }

    private (int Dead, int Total) CountWave(ControlRuleComponent rule, PvpTeam team)
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

    private bool IsAlive(ControlPlayerSlot slot)
    {
        return slot.Mob is { } mob
            && Exists(mob)
            && TryComp<MobStateComponent>(mob, out var state)
            && state.CurrentState == MobState.Alive;
    }

    private void ResetCapturePoints()
    {
        var query = EntityQueryEnumerator<ControlCapturePointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            point.Progress = 0f;
            point.Owner = null;
            point.CapturingTeam = null;
            point.VisualState = ControlCaptureState.Neutral;
            Dirty(uid, point);
        }
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out ControlRuleComponent? rule)
    {
        var query = EntityQueryEnumerator<ControlRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out rule, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive(uid, gameRule))
                return true;
        }

        rule = null;
        return false;
    }

    public bool TryGetRuleForStatus([NotNullWhen(true)] out ControlRuleComponent? rule)
    {
        return TryGetActiveRule(out rule);
    }

    private void ApplyTeamConfig(ControlRuleComponent rule)
    {
        StationControlConfigComponent? config = null;
        foreach (var station in _station.GetStations())
        {
            if (TryComp(station, out config))
                break;
        }

        config ??= ControlTeamConfig.FromGameMap(_gameMap.GetSelectedMap());
        rule.AttackersTeam = ControlTeamConfig.GetId(config, PvpTeam.Attackers);
        rule.DefendersTeam = ControlTeamConfig.GetId(config, PvpTeam.Defenders);
    }

    private ProtoId<ControlTeamPrototype> GetTeamId(ControlRuleComponent rule, PvpTeam team)
    {
        return team == PvpTeam.Attackers ? rule.AttackersTeam : rule.DefendersTeam;
    }

    private bool TryGetTeamPrototype(ControlRuleComponent rule, PvpTeam team, [NotNullWhen(true)] out ControlTeamPrototype? proto)
    {
        return Proto.TryIndex(GetTeamId(rule, team), out proto);
    }

    private bool TeamHasClass(ControlRuleComponent rule, PvpTeam team, ProtoId<AssaultClassPrototype> classId)
    {
        return TryGetTeamPrototype(rule, team, out var proto) && proto.ContainsClass(classId);
    }

    private IEnumerable<AssaultClassPrototype> EnumerateTeamClasses(ControlRuleComponent rule, PvpTeam team)
    {
        if (!TryGetTeamPrototype(rule, team, out var teamProto))
            yield break;

        foreach (var classId in teamProto.Classes)
        {
            if (Proto.TryIndex(classId, out AssaultClassPrototype? proto))
                yield return proto;
        }
    }

    private string TeamName(ControlRuleComponent rule, PvpTeam team)
    {
        return Loc.GetString(ControlTeamConfig.GetName(Proto, GetTeamId(rule, team), team));
    }

    private void Announce(string locId, params (string, object)[] args)
    {
        _chat.DispatchServerAnnouncement(Loc.GetString(locId, args));
    }
}
