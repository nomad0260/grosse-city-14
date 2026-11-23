using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.CommanderTerminal;

/// <summary>
/// Worn on a GEO gas mask. The wearer can see squad marks and other squads' orders.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CommanderGroupMaskComponent : Component;
