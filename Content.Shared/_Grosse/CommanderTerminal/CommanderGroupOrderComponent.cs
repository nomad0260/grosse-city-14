using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Grosse.CommanderTerminal;

/// <summary>
/// Latest move order for a squad member. Session-specific, same viewers as the membership.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CommanderGroupOrderComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetCoordinates Coordinates;

    [DataField, AutoNetworkedField]
    public int Depth;

    [DataField, AutoNetworkedField]
    public string Location = string.Empty;

    public override bool SessionSpecific => true;
}
