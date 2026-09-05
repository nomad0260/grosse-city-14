using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class StationControlConfigComponent : Component
{
    [DataField]
    public ProtoId<ControlTeamPrototype> Attackers = ControlConstants.DefaultAttackersTeam;

    [DataField]
    public ProtoId<ControlTeamPrototype> Defenders = ControlConstants.DefaultDefendersTeam;
}
