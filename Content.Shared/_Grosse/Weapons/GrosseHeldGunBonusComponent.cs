using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Weapons;

/// <summary>
/// Adds gun spread modifiers to every gun this entity is holding.
/// Signs match <see cref="Content.Shared.Weapons.Ranged.Components.GunWieldBonusComponent"/>:
/// a negative angle tightens spread, a negative <see cref="AngleIncrease"/> slows recoil buildup,
/// and a positive <see cref="AngleDecay"/> recovers faster.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedGrosseHeldGunBonusSystem))]
public sealed partial class GrosseHeldGunBonusComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public Angle MinAngle = Angle.Zero;

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public Angle MaxAngle = Angle.Zero;

    /// <summary>
    /// Higher decay recovers spread faster.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public Angle AngleDecay = Angle.Zero;

    /// <summary>
    /// Lower increase, including negative values, builds spread more slowly.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public Angle AngleIncrease = Angle.Zero;
}
