using Robust.Shared.Audio;

namespace Content.Server.PatchHTN.Components;

/// <summary>
/// Allows an NPC to dig itself in while idle and to resurface later (e.g. antlions).
/// </summary>
[RegisterComponent]
public sealed partial class BurrowComponent : Component
{
    /// <summary>
    /// Is the NPC currently underground?
    /// </summary>
    [ViewVariables]
    public bool Burrowed;

    /// <summary>
    /// Safety net so an NPC can't stay buried forever if its plan gets interrupted.
    /// </summary>
    [DataField]
    public float MaxBurrowTime = 60f;

    /// <summary>
    /// When we burrowed, used for <see cref="MaxBurrowTime"/>.
    /// </summary>
    [ViewVariables]
    public TimeSpan BurrowedAt;

    /// <summary>
    /// Whether the entity had the normal visibility layer before burrowing.
    /// </summary>
    [ViewVariables]
    public bool HadNormalVisibilityLayer;

    /// <summary>
    /// Played when digging in and when resurfacing.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Tile IDs the NPC may burrow on, e.g. natural planet floors. Empty means any tile.
    /// </summary>
    [DataField]
    public HashSet<string> AllowedTiles = new();
}
