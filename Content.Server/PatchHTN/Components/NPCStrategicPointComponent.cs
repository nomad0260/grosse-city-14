namespace Content.Server.PatchHTN.Components;

/// <summary>
/// Marks an entity as a strategic objective for NPC assault tasks (e.g. a city district that event
/// NPCs should storm).
/// </summary>
[RegisterComponent]
public sealed partial class NPCStrategicPointComponent : Component
{
    /// <summary>
    /// Higher priority points are preferred when several are reachable.
    /// </summary>
    [DataField]
    public float Priority = 1f;

    /// <summary>
    /// Whether NPCs may pick this point.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Network this point belongs to. NPCs only assault points of their own network.
    /// </summary>
    [DataField]
    public string Network = "Default";
}
