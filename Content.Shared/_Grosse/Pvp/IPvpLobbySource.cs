using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Grosse.Pvp;

public interface IPvpLobbySource
{
    string RuleId { get; }
    bool ShowClassCost { get; }
    string HeaderLoc { get; }
    string NeedLoadoutLoc { get; }
    string TeamFullLoc { get; }
    string ClassFullLoc { get; }

    (string AttackersId, string DefendersId) GetTeamIds();
    string GetTeamName(PvpTeam team);
    IReadOnlyList<PvpClassInfo> GetClasses(PvpTeam team);
    bool ContainsClass(PvpTeam team, string classId);
    IEnumerable<(NetUserId User, string ClassId)> GetAssignedClasses();
    bool IsInWaveQueue(NetUserId user);
    void HandleLateJoin(ICommonSession session);
}
