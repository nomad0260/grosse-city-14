using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.CommanderTerminal;

/// <summary>
/// Marks a civil protection body as a member of a commander group.
/// The state is session-specific: only the owner, the same group, and a GEO mask see it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CommanderGroupMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public string GroupId = string.Empty;

    [DataField, AutoNetworkedField]
    public string GroupName = string.Empty;

    [DataField, AutoNetworkedField]
    public int ColorIndex;

    [DataField, AutoNetworkedField]
    public bool IsLeader;

    public override bool SessionSpecific => true;
}
