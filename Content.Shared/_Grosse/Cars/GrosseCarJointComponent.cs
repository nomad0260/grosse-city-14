using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

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

    /// <summary>
    /// Used when the loader's seat has no <see cref="GrosseCarTurretSeat.ReloadDelay"/>.
    /// </summary>
    [DataField]
    public TimeSpan ReloadDelay = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// Leave the gunner seat. Not handed to the driver: the driver already has a vehicle exit.
    /// </summary>
    [DataField]
    public List<EntProtoId> GunnerActions = new()
    {
        "ActionGrosseCarExit",
    };

    /// <summary>
    /// Given to whichever occupied seat is loading. Moves when that seat changes.
    /// </summary>
    [DataField]
    public List<EntProtoId> TurretActions = new()
    {
        "ActionGrosseTankReload",
    };

    /// <summary>
    /// Who may aim and who loads. A later loader seat is another entry with a lower priority and no aim.
    /// </summary>
    [DataField]
    public List<GrosseCarTurretSeat> Seats = new()
    {
        new()
        {
            Id = "gunner",
            Container = "turret-gunner",
            OnTurret = true,
            Priority = 0,
            ReloadDelay = TimeSpan.FromSeconds(1.5f),
        },
        new()
        {
            Id = "driver",
            Priority = 1,
            ReloadDelay = TimeSpan.FromSeconds(2.5f),
        },
    };

    /// <summary>
    /// Whoever currently holds <see cref="TurretActions"/>. Server bookkeeping, not replicated.
    /// </summary>
    public EntityUid? TurretUser;
}

/// <summary>
/// One crew seat that can aim the turret, load it, or both.
/// Lower <see cref="Priority"/> wins among occupied seats.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class GrosseCarTurretSeat
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// Container id. Empty means a GrosseCar slot with the same <see cref="Id"/> on the hull.
    /// </summary>
    [DataField]
    public string? Container;

    /// <summary>
    /// The container is on the turret. Otherwise it is on the hull.
    /// </summary>
    [DataField]
    public bool OnTurret;

    [DataField]
    public int Priority;

    [DataField]
    public bool Aims = true;

    [DataField]
    public bool Loads = true;

    [DataField]
    public TimeSpan ReloadDelay = TimeSpan.FromSeconds(1.5f);
}
