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
    /// Safety net so an NPC can't stay buried forever if its plan gets interrupted. Must stay above the
    /// longest planned burrow time (see the MinimumBurrowTime/MaximumBurrowTime blackboard keys), otherwise
    /// it cuts a normal burrow short.
    /// </summary>
    [DataField]
    public float MaxBurrowTime = 330f;

    /// <summary>
    /// When we burrowed, used for <see cref="MaxBurrowTime"/>.
    /// </summary>
    [ViewVariables]
    public TimeSpan BurrowedAt;

    /// <summary>
    /// Whether the invisibility component was added by us, so surfacing only removes what burrowing added.
    /// </summary>
    [ViewVariables]
    public bool AddedStealth;

    /// <summary>
    /// Previous <see cref="Content.Shared.Stealth.Components.StealthComponent.Enabled"/> value when the entity
    /// already had stealth before burrowing.
    /// </summary>
    [ViewVariables]
    public bool PreviousStealthEnabled;

    /// <summary>
    /// Previous stealth visibility when the entity already had stealth before burrowing.
    /// </summary>
    [ViewVariables]
    public float PreviousStealthVisibility;

    /// <summary>
    /// Played when digging in.
    /// </summary>
    [DataField]
    public SoundSpecifier? DigInSound;

    /// <summary>
    /// Played when resurfacing.
    /// </summary>
    [DataField]
    public SoundSpecifier? DigOutSound;

    /// <summary>
    /// Tile IDs the NPC may burrow on, e.g. natural planet floors. Empty means any tile.
    /// </summary>
    [DataField]
    public HashSet<string> AllowedTiles = new();
}
