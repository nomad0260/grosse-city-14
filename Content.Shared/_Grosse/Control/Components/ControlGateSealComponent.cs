using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ControlGateSealComponent : Component
{
    [DataField]
    public bool Unlocked;
}
