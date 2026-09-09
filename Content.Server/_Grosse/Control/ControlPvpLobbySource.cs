using Content.Server._Grosse.Pvp;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Control;
using Content.Shared._Grosse.Control.Components;
using Content.Shared._Grosse.Pvp;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Control;

public sealed partial class ControlPvpLobbySource : EntitySystem, IPvpLobbySource
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameMapManager _gameMap = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PvpLobbySystem _pvp = default!;

    public string RuleId => ControlConstants.RulePrototypeId;
    public bool ShowClassCost => false;
    public string HeaderLoc => "control-lobby-header";
    public string NeedLoadoutLoc => "control-lobby-need-loadout";
    public string TeamFullLoc => "control-lobby-team-full";
    public string ClassFullLoc => "control-lobby-class-full";

    public override void Initialize()
    {
        base.Initialize();
        _pvp.Register(this);
    }

    public (string AttackersId, string DefendersId) GetTeamIds()
    {
        var config = GetConfig();
        // Shared PvP lobby slots map to Control TeamA / TeamB.
        return (
            ControlTeamConfig.GetId(config, ControlTeam.TeamA),
            ControlTeamConfig.GetId(config, ControlTeam.TeamB));
    }

    public string GetTeamName(PvpTeam team)
    {
        var controlTeam = team.ToControl();
        var id = controlTeam == ControlTeam.TeamA ? GetTeamIds().AttackersId : GetTeamIds().DefendersId;
        return Loc.GetString(ControlTeamConfig.GetName(_proto, id, controlTeam));
    }

    public IReadOnlyList<PvpClassInfo> GetClasses(PvpTeam team)
    {
        var list = new List<PvpClassInfo>();
        if (!ControlTeamConfig.TryGetTeam(_proto, GetConfig(), team.ToControl(), out var teamProto))
            return list;

        foreach (var classId in teamProto.Classes)
        {
            if (!_proto.TryIndex(classId, out AssaultClassPrototype? proto))
                continue;

            list.Add(new PvpClassInfo
            {
                Id = proto.ID,
                Name = Loc.GetString(proto.Name),
                Description = Loc.GetString(proto.Description),
                Cost = proto.Cost,
                MaxCount = proto.MaxCount,
            });
        }

        return list;
    }

    public bool ContainsClass(PvpTeam team, string classId)
    {
        return ControlTeamConfig.TryGetTeam(_proto, GetConfig(), team.ToControl(), out var teamProto)
            && teamProto.ContainsClass(classId);
    }

    public IEnumerable<(NetUserId User, string ClassId)> GetAssignedClasses()
    {
        var query = EntityQueryEnumerator<ControlRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!_ticker.IsGameRuleActive(uid, gameRule))
                continue;

            foreach (var (user, slot) in rule.Players)
            {
                if (slot.Class is { } assigned)
                    yield return (user, assigned.Id);
            }
        }
    }

    public bool IsInWaveQueue(NetUserId user)
    {
        var query = EntityQueryEnumerator<ControlRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!_ticker.IsGameRuleActive(uid, gameRule))
                continue;

            if (rule.Players.TryGetValue(user, out var slot))
                return slot.InWaveQueue;
        }

        return false;
    }

    public void HandleLateJoin(ICommonSession session)
    {
        EntityManager.System<ControlRuleSystem>().TryLateJoin(session);
    }

    private StationControlConfigComponent? GetConfig()
    {
        var query = EntityQueryEnumerator<StationControlConfigComponent>();
        if (query.MoveNext(out _, out var live))
            return live;

        return ControlTeamConfig.FromGameMap(_gameMap.GetSelectedMap());
    }
}
