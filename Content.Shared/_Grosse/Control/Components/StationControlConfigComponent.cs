using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class StationControlConfigComponent : Component
{
    [DataField]
    public ProtoId<ControlTeamPrototype> TeamA = ControlConstants.DefaultTeamA;

    [DataField]
    public ProtoId<ControlTeamPrototype> TeamB = ControlConstants.DefaultTeamB;
}
