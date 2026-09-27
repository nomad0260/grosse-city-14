using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Cars;

/// <summary>
/// Hull-side link to a turret entity. The turret uid is saved with the map so a placed tank reloads as one piece.
/// Verbs for the gunner and the ammo rack live here; the turret itself is not clickable.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseCarJointComponent : Component
{
    [DataField]
    public EntProtoId TurretPrototype = "GrosseTankTurret";

    [DataField]
    public Vector2 Anchor = Vector2.Zero;

    [DataField]
    public string JointId = "grosse-car-turret";

    /// <summary>
    /// Saved and networked so map load reconnects the same turret instead of spawning another.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Turret;

    [DataField]
    public string GunnerContainer = "turret-gunner";

    [DataField]
    public string AmmoContainer = "turret-ammo";

    [DataField]
    public int AmmoCapacity = 8;

    [DataField]
    public string AmmoTag = "CartridgeGrosseTank";

    [DataField]
    public EntProtoId AmmoPrototype = "CartridgeGrosseTank";

    /// <summary>
    /// Shells placed in the rack when the turret is first created. Zero leaves it empty.
    /// </summary>
    [DataField]
    public int StartingAmmo = 8;

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(0.8f);

    [DataField]
    public TimeSpan ReloadDelay = TimeSpan.FromSeconds(1.5f);

    [DataField]
    public List<EntProtoId> GunnerActions = new()
    {
        "ActionGrosseCarExit",
        "ActionGrosseTankReload",
    };
}
