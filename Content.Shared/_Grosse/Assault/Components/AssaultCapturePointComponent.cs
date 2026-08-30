using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true)]
public sealed partial class AssaultCapturePointComponent : Component
{
    [DataField, AutoNetworkedField]
    public int ZoneIndex;

    [DataField]
    public float Radius = 3f;

    [DataField]
    public float CaptureTime = 20f;

    [ViewVariables, AutoNetworkedField]
    public float Progress;

    [ViewVariables, AutoNetworkedField]
    public bool Captured;

    [ViewVariables, AutoNetworkedField]
    public AssaultCaptureState VisualState = AssaultCaptureState.Idle;

    /// <summary>
    /// In-round circle/progress overlay. The capture point itself stays a mapper marker.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Visual;

    /// <summary>
    /// Players currently overlapping the capture fixture. Server occupancy only.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Occupants = new();
}
