using Content.Shared._Grosse.Pvp;
using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true)]
public sealed partial class ControlCapturePointComponent : Component
{
    [DataField, AutoNetworkedField]
    public string PointName = string.Empty;

    [DataField]
    public float Radius = 3f;

    [DataField]
    public float CaptureTime = 20f;

    [ViewVariables, AutoNetworkedField]
    public float Progress;

    [ViewVariables, AutoNetworkedField]
    public PvpTeam? Owner;

    [ViewVariables, AutoNetworkedField]
    public PvpTeam? CapturingTeam;

    [ViewVariables, AutoNetworkedField]
    public ControlCaptureState VisualState = ControlCaptureState.Neutral;

    [ViewVariables]
    public HashSet<EntityUid> Occupants = new();
}
