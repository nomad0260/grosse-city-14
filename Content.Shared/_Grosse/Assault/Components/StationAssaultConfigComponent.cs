using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Assault.Components;

/// <summary>
/// Per-station Assault team selection. Place on a gameMap station like StationNameSetup.
/// </summary>
[RegisterComponent]
public sealed partial class StationAssaultConfigComponent : Component
{
    [DataField]
    public ProtoId<AssaultTeamPrototype> Attackers = AssaultConstants.DefaultAttackersTeam;

    [DataField]
    public ProtoId<AssaultTeamPrototype> Defenders = AssaultConstants.DefaultDefendersTeam;
}
