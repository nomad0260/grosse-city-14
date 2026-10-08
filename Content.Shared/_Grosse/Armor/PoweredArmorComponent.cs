using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Armor;

/// <summary>
/// Extra armor while a power cell in the suit has charge. Charge is spent when the wearer takes damage.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(PoweredArmorSystem))]
public sealed partial class PoweredArmorComponent : Component
{
    /// <summary>
    /// Added to each armor coefficient, in absolute protection (0.2 is +20 percentage points).
    /// </summary>
    [DataField]
    public float CoefficientBonus = 0.2f;

    /// <summary>
    /// Charge removed per point of incoming damage, before this bonus is applied.
    /// </summary>
    [DataField]
    public float EnergyPerDamage = 10f;

    [DataField]
    public SoundSpecifier PowerOnSound = new SoundPathSpecifier("/Audio/_Grosse/Effects/Armor/powerarmor_on.ogg");

    [DataField]
    public SoundSpecifier PowerOffSound = new SoundPathSpecifier("/Audio/_Grosse/Effects/Armor/armor_gone.ogg");

    [DataField]
    public ProtoId<AlertPrototype> ChargeAlert = "PoweredArmorCharge";

    [DataField]
    public ProtoId<AlertPrototype> NoCellAlert = "PoweredArmorChargeNone";

    [DataField]
    public ProtoId<AlertCategoryPrototype> AlertCategory = "PoweredArmor";

    /// <summary>
    /// Whether the suit was worn with charge. Used so the on/off sounds play once per transition.
    /// </summary>
    public bool Active;
}
