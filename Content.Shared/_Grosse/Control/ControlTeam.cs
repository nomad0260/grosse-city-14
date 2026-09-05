using Content.Shared._Grosse.Pvp;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Control;

/// <summary>
/// Symmetric Control team slots. Shared PvP lobby still uses <see cref="PvpTeam"/>;
/// map TeamA↔Attackers and TeamB↔Defenders only at the lobby bridge.
/// </summary>
[Serializable, NetSerializable]
public enum ControlTeam : byte
{
    TeamA = 0,
    TeamB = 1,
}

public static class ControlTeamConversion
{
    public static ControlTeam ToControl(this PvpTeam team) =>
        team == PvpTeam.Defenders ? ControlTeam.TeamB : ControlTeam.TeamA;

    public static PvpTeam ToPvp(this ControlTeam team) =>
        team == ControlTeam.TeamB ? PvpTeam.Defenders : PvpTeam.Attackers;
}
