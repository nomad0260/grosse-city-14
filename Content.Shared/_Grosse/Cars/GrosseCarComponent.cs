using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Decals;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Grosse.Cars;

[Serializable, NetSerializable]
public enum GrosseCarEngineState : byte
{
    Off,
    Starting,
    On,
    Stopping,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseCarComponent : Component
{
    [DataField]
    public List<GrosseCarSlot> Slots = new();

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan ExitDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public List<EntProtoId> OccupantActions = new();

    [DataField]
    public float OccupantDamageMultiplier = 0.35f;

    [DataField]
    public float MaxDriveDamage = 200f;

    [DataField]
    public float EngineForce = 100000f;

    [DataField]
    public float BrakeForce = 150000f;

    [DataField]
    public float Friction = 1.5f;

    [DataField]
    public float MaxForwardSpeed = 8f;

    [DataField]
    public float MaxReverseSpeed = 3f;

    [DataField]
    public float SteerRate = 2.2f;

    [DataField]
    public float MinSteerSpeed = 0.6f;

    /// <summary>
    /// When enabled, the driver can yaw the chassis while nearly stopped.
    /// Left off so existing cars only steer once they are rolling.
    /// </summary>
    [DataField]
    public bool SteerInPlace;

    [DataField]
    public float Grip = 4f;

    [DataField]
    public float HandbrakeGrip = 0.6f;

    [DataField]
    public float HandbrakeForce = 120000f;

    /// <summary>
    /// Extra rotation from the entity transform to the sprite's rest pose.
    /// World rotation 0 is South; the KrAZ south frame already points that way, so leave at 0.
    /// Use 90° only if the locked RSI frame faces East at identity.
    /// </summary>
    [DataField]
    public Angle VisualRotationOffset = Angle.Zero;

    [DataField]
    public float MinImpactSpeed = 4f;

    /// <summary>
    /// Minimum car speed to damage <see cref="DamageableComponent"/> / Injurable props (lights, trees, poles).
    /// </summary>
    [DataField]
    public float RamMinSpeed = 4f;

    [DataField]
    public DamageSpecifier HitDamage = new();

    [DataField]
    public DamageSpecifier SelfDamage = new();

    [DataField]
    public DamageSpecifier WallDamage = new();

    /// <summary>
    /// Entities the car can crash into without damaging or tossing them. The car and crew still take impact damage.
    /// An empty list rams every damageable target. Set tags on the vehicle prototype.
    /// </summary>
    [DataField]
    public EntityWhitelist RamBlacklist = new();

    /// <summary>
    /// Multiplies <see cref="WallDamage"/> dealt to the car and crew when the target is on <see cref="RamBlacklist"/>.
    /// Null leaves that damage unchanged.
    /// </summary>
    [DataField]
    public float? BlacklistImpactCoefficient;

    [DataField]
    public TimeSpan ImpactCooldown = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float ImpactRestitution = 0.3f;

    [DataField]
    public float PushMultiplier = 1f;

    [DataField]
    public float MinDriftSpeed = 4f;

    [DataField]
    public float DriftSlipThreshold = 0.35f;

    [DataField]
    public ProtoId<DecalPrototype> DriftDecal = "GrosseCarSkid";

    [DataField]
    public float DriftDecalSpacing = 0.35f;

    /// <summary>
    /// Shifts both tire marks in the car's local space, same axes as the physics box.
    /// Use this when the entity origin is not the visual center.
    /// </summary>
    [DataField]
    public Vector2 DriftDecalOffset = Vector2.Zero;

    [DataField]
    public Color DriftDecalColor = Color.Black;

    [DataField]
    public SoundSpecifier? DriftSound;

    [DataField]
    public SoundSpecifier? EngineStartSound;

    [DataField]
    public SoundSpecifier? EngineStopSound;

    [DataField]
    public SoundSpecifier? EngineIdleSound;

    /// <summary>
    /// Played once when the car first starts rolling. The drive loop takes over after it finishes.
    /// </summary>
    [DataField]
    public SoundSpecifier? EngineMoveStartSound;

    [DataField]
    public SoundSpecifier? EngineDriveSound;

    [DataField]
    public float IdlePitch = 1f;

    [DataField]
    public float RevPitch = 1.45f;

    [DataField]
    public float RpmRiseRate = 2.2f;

    [DataField]
    public float RpmFallRate = 1.1f;

    [DataField]
    public float IdleVolume = -4f;

    [DataField]
    public float DriveVolume = -2f;

    [DataField]
    public float DriveStartSpeed = 0.6f;

    [DataField]
    public float DriveStopSpeed = 0.25f;

    [DataField]
    public EntProtoId EngineToggleAction = "ActionGrosseCarToggleEngine";

    [DataField]
    public SoundSpecifier? ImpactSound = new SoundCollectionSpecifier("MetalThud");

    /// <summary>
    /// Played when the car rams and tosses an Injurable (mobs, props).
    /// </summary>
    [DataField]
    public SoundSpecifier? HitSound = new SoundCollectionSpecifier("GrosseCarHit");

    /// <summary>
    /// Played when the car hits a wall or other Impassable geometry.
    /// </summary>
    [DataField]
    public SoundSpecifier? WallImpactSound = new SoundCollectionSpecifier("GrosseCarWall");

    [DataField]
    public EntProtoId RadioAction = "ActionGrosseCarPlayMidi";

    [DataField, AutoNetworkedField]
    public EntityUid? RadioActionEntity;

    [AutoNetworkedField]
    public bool Handbrake;

    [AutoNetworkedField]
    public bool IsDrifting;

    [AutoNetworkedField]
    public float DriftSlip;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastImpact;

    [AutoNetworkedField]
    public GrosseCarEngineState EngineState = GrosseCarEngineState.Off;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan EngineTransitionEnd;

    /// <summary>
    /// True after the car has crossed <see cref="DriveStartSpeed"/> until it falls under <see cref="DriveStopSpeed"/>.
    /// </summary>
    [AutoNetworkedField]
    public bool WasRolling;

    public Vector2 LastSkidPosition;
    public EntityUid? IdleSoundEntity;
    public EntityUid? DriveSoundEntity;
    public EntityUid? MoveStartSoundEntity;
    public EntityUid? IdleSoundExcluded;
    public EntityUid? DriveSoundExcluded;
    public EntityUid? DriftSoundEntity;
    public TimeSpan MoveStartUntil;
    public float CurrentRpm;
    public bool VisualRunning;

    /// <summary>
    /// World rotation chosen by the driving step. Re-applied after the physics solve so a turret joint cannot yaw the hull.
    /// </summary>
    public Angle CommandedRotation;

    public bool HasCommandedRotation;
}
