using Content.Server._Grosse.Pvp;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Assault.Components;
using Content.Shared._Grosse.Pvp;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Assault;

public sealed partial class AssaultPvpLobbySource : EntitySystem, IPvpLobbySource
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameMapManager _gameMap = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PvpLobbySystem _pvp = default!;

    public string RuleId => AssaultConstants.RulePrototypeId;
    public bool ShowClassCost => true;
    public string HeaderLoc => "assault-lobby-header";
    public string NeedLoadoutLoc => "assault-lobby-need-loadout";
    public string TeamFullLoc => "assault-lobby-team-full";
    public string ClassFullLoc => "assault-lobby-class-full";

    public override void Initialize()
    {
        base.Initialize();
        _pvp.Register(this);
    }

    public (string AttackersId, string DefendersId) GetTeamIds()
    {
        var config = GetConfig();
        return (
            AssaultTeamConfig.GetId(config, PvpTeam.Attackers),
            AssaultTeamConfig.GetId(config, PvpTeam.Defenders));
    }

    public string GetTeamName(PvpTeam team)
    {
        var id = team == PvpTeam.Attackers ? GetTeamIds().AttackersId : GetTeamIds().DefendersId;
        return Loc.GetString(AssaultTeamConfig.GetName(_proto, id, team));
    }

    public IReadOnlyList<PvpClassInfo> GetClasses(PvpTeam team)
    {
        var list = new List<PvpClassInfo>();
        if (!AssaultTeamConfig.TryGetTeam(_proto, GetConfig(), team, out var teamProto))
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
        return AssaultTeamConfig.TryGetTeam(_proto, GetConfig(), team, out var teamProto)
            && teamProto.ContainsClass(classId);
    }

    public IEnumerable<(NetUserId User, string ClassId)> GetAssignedClasses()
    {
        var query = EntityQueryEnumerator<AssaultRuleComponent, GameRuleComponent>();
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
        var query = EntityQueryEnumerator<AssaultRuleComponent, GameRuleComponent>();
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
        EntityManager.System<AssaultRuleSystem>().TryLateJoin(session);
    }

    private StationAssaultConfigComponent? GetConfig()
    {
        var query = EntityQueryEnumerator<StationAssaultConfigComponent>();
        if (query.MoveNext(out _, out var live))
            return live;

        return AssaultTeamConfig.FromGameMap(_gameMap.GetSelectedMap());
    }
}
