using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AssaultCapturePointComponent : Component
{
    [DataField]
    public int ZoneIndex;

    [DataField]
    public float Radius = 3f;

    [DataField]
    public float CaptureTime = 20f;

    [ViewVariables]
    public float Progress;

    [ViewVariables]
    public bool Captured;
}
