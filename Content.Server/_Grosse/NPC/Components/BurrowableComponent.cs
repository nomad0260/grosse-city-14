using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Components;

/// <summary>
/// Состояния для системы закапывания
/// </summary>
public enum BurrowState
{
    /// <summary>Нормальное состояние, активен</summary>
    Active,

    /// <summary>Наблюдает за окружением перед закапыванием</summary>
    Observing,

    /// <summary>Закопан, в засаде</summary>
    Burrowed
}

/// <summary>
/// Позволяет NPC закапываться в землю для засады
/// </summary>
[RegisterComponent]
public sealed partial class BurrowableComponent : Component
{
    /// <summary>
    /// Текущее состояние закапывания
    /// </summary>
    [DataField("state")]
    public BurrowState State = BurrowState.Active;

    /// <summary>
    /// Время наблюдения перед закапыванием (в секундах)
    /// </summary>
    [DataField("observeTimeBeforeBurrow")]
    public float ObserveTimeBeforeBurrow = 8f;

    /// <summary>
    /// Минимальное время патрулирования в секундах (3 минуты)
    /// </summary>
    [DataField("minPatrolTime")]
    public float MinPatrolTime = 180f;

    /// <summary>
    /// Максимальное время патрулирования в секундах (7 минут)
    /// </summary>
    [DataField("maxPatrolTime")]
    public float MaxPatrolTime = 420f;

    /// <summary>
    /// Время нахождения в засаде (закопанным) в секундах (5 минут)
    /// </summary>
    [DataField("ambushTime")]
    public float AmbushTime = 300f;

    /// <summary>
    /// Запланированное время для текущего патруля (случайное между min и max)
    /// Инициализируется в OnStartup
    /// </summary>
    [DataField("currentPatrolDuration")]
    public float CurrentPatrolDuration = 0f;

    /// <summary>
    /// Кулдаун после боя перед возможностью закопаться (в секундах)
    /// </summary>
    [DataField("combatCooldown")]
    public float CombatCooldown = 15f;

    /// <summary>
    /// Время входа в текущее состояние
    /// </summary>
    public TimeSpan StateEnterTime = TimeSpan.Zero;

    /// <summary>
    /// Время последнего урона
    /// </summary>
    public TimeSpan LastDamageTime = TimeSpan.Zero;

    /// <summary>
    /// Время последнего боя
    /// </summary>
    public TimeSpan LastCombatTime = TimeSpan.Zero;

    /// <summary>
    /// ID плиток на которых можно закапываться (dirt, grass, sand и т.д.)
    /// </summary>
    [DataField("allowedTileIds")]
    public HashSet<string> AllowedTileIds = new()
    {
        "FloorPlanetDirt",
        "FloorPlanetGrass",
        "FloorDesert",
        "FloorLowDesert",
        "FloorAsteroidSand",
        "FloorAsteroidTile",
        "FloorSnow",
        "FloorSnowDug"
    };

    /// <summary>
    /// Прозрачность спрайта когда закопан (0.0 = невидим, 1.0 = полностью виден)
    /// </summary>
    [DataField("burrowedAlpha")]
    public float BurrowedAlpha = 0.0f; // Полностью невидим

    /// <summary>
    /// Скорость движения когда закопан (множитель)
    /// </summary>
    [DataField("burrowedSpeedMultiplier")]
    public float BurrowedSpeedMultiplier = 0.0f; // Не может двигаться

    /// <summary>
    /// Звук закапывания
    /// </summary>
    [DataField("burrowSound")]
    public SoundSpecifier? BurrowSound = new SoundPathSpecifier("/Audio/_Grosse/Mobs/Antlion/digdown1.ogg");

    /// <summary>
    /// Звук выкапывания
    /// </summary>
    [DataField("unburrowSound")]
    public SoundSpecifier? UnburrowSound = new SoundPathSpecifier("/Audio/_Grosse/Mobs/Antlion/digup1.ogg");
}
