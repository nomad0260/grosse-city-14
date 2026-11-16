namespace Content.Server.PatchHTN.Components;

/// <summary>
/// A waypoint NPCs can patrol between. Mappers place these markers on the map; points of the same
/// network are linked into a graph on map init by <see cref="Content.Server.PatchHTN.Systems.PatrolNetworkSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class PatrolPointComponent : Component
{
    /// <summary>
    /// Network this point belongs to. NPCs only patrol points of their own network.
    /// </summary>
    [DataField]
    public string Network = "Default";

    /// <summary>
    /// Points of the same network within this distance are linked together. The smallest radius of
    /// the two points is used, so a point can be isolated by setting this to zero.
    /// </summary>
    [DataField]
    public float LinkRadius = 12f;

    /// <summary>
    /// Whether the patrol task may use this point.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Points this point is linked to. Populated on map init.
    /// </summary>
    [ViewVariables]
    public readonly List<EntityUid> Neighbors = new();
}
