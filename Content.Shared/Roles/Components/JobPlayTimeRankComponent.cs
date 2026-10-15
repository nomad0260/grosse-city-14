using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Stores the playtime-resolved job display name (and optional icon) applied at spawn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JobPlayTimeRankComponent : Component
{
    /// <summary>
    /// Already-localized job title for ID / greetings / manifest.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string DisplayName = string.Empty;

    [DataField, AutoNetworkedField]
    public ProtoId<JobIconPrototype>? Icon;

    /// <summary>
    /// Two-digit civil-protection callsign rank copied from the resolved playtime rank.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Callsign;
}
