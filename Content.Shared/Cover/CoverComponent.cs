using Robust.Shared.GameStates;

namespace Content.Shared.Cover;

/// <summary>
/// Soft cover: blocks bullets by default, but shooters next to it can fire through,
/// and a shot aimed at a mob (cursor on them) can still hit them behind it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CoverSystem))]
public sealed partial class CoverComponent : Component
{
    /// <summary>
    /// Distance in tiles. A shooter this close can fire through the cover.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShootThroughRadius = 3f;
}
