using Content.Server.GameTicking;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Управляет системой закапывания NPC с учётом состояний
/// </summary>
public sealed partial class BurrowableSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    /// <summary>
    /// Список природных тайлов, на которых можно закапываться
    /// </summary>
    private static readonly HashSet<string> NaturalTiles = new()
    {
        // Грязь
        "FloorDirt",
        "FloorPlanetDirt",

        // Трава
        "FloorGrass",
        "FloorGrassJungle",
        "FloorGrassDark",
        "FloorGrassLight",
        "FloorPlanetGrass",
        "FloorJungleAstroGrass",

        // Песок астероидный
        "FloorAsteroidSand",
        "FloorAsteroidSandBorderless",
        "FloorAsteroidSandDug",
        "FloorAsteroidSandDugBorderless",
        "FloorAsteroidSandRed",
        "FloorAsteroidSandRedBorderless",
        "FloorAsteroidSandUnvariantized",
        "FloorAsteroidSandUnvariantizedBorderless",

        // Железный песок
        "FloorAsteroidIronsand",
        "FloorAsteroidIronsandBorderless",
        "FloorAsteroidIronsandUnvariantized",
        "FloorAsteroidIronsandUnvariantizedBorderless",

        // Пещеры
        "FloorCave",
        "FloorCaveDrought",

        // Пустыня/снег (природные)
        "FloorDesert",
        "FloorLowDesert",
        "FloorSnow",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BurrowableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BurrowableComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<BurrowableComponent, DamageDealtEvent>(OnDamaged);
    }

    private void OnStartup(EntityUid uid, BurrowableComponent component, ComponentStartup args)
    {
        component.StateEnterTime = _gameTicker.RoundDuration();
        component.State = BurrowState.Active;

        // Генерируем случайную длительность патруля при старте
        var random = new Random();
        component.CurrentPatrolDuration = (float)(random.NextDouble() * (component.MaxPatrolTime - component.MinPatrolTime) + component.MinPatrolTime);

        Robust.Shared.Log.Logger.Info($"[BURROW-STARTUP] Entity {uid}: Initialized! State=Active, CurrentPatrolDuration={component.CurrentPatrolDuration:F1}s (min={component.MinPatrolTime:F1}s, max={component.MaxPatrolTime:F1}s)");
    }

    private void OnRefreshMovespeed(EntityUid uid, BurrowableComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.State == BurrowState.Burrowed)
        {
            args.ModifySpeed(component.BurrowedSpeedMultiplier, component.BurrowedSpeedMultiplier);
        }
    }

    private void OnDamaged(EntityUid uid, BurrowableComponent component, ref DamageDealtEvent args)
    {
        // При получении урона сбрасываем состояние и выкапываемся
        if (args.Damage.GetTotal() > 0)
        {
            component.LastDamageTime = _gameTicker.RoundDuration();
            component.LastCombatTime = _gameTicker.RoundDuration();

            if (component.State == BurrowState.Burrowed)
            {
                TransitionToActive(uid, component);
            }
        }
    }

    /// <summary>
    /// Переход в состояние наблюдения (перед закапыванием)
    /// </summary>
    public bool TryStartObserving(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            Robust.Shared.Log.Logger.Warning($"[TRY-START-OBSERVING] Entity {uid}: Failed to resolve component!");
            return false;
        }

        Robust.Shared.Log.Logger.Info($"[TRY-START-OBSERVING] Entity {uid}: Current state={component.State}");

        // Проверяем условия для безопасного закапывания
        if (!CanStartObserving(uid, component))
        {
            Robust.Shared.Log.Logger.Info($"[TRY-START-OBSERVING] Entity {uid}: CanStartObserving returned FALSE!");
            return false;
        }

        Robust.Shared.Log.Logger.Info($"[TRY-START-OBSERVING] Entity {uid}: Transitioning to Observing state!");
        component.State = BurrowState.Observing;
        component.StateEnterTime = _gameTicker.RoundDuration();
        return true;
    }

    /// <summary>
    /// Проверка: можно ли начать наблюдение?
    /// </summary>
    private bool CanStartObserving(EntityUid uid, BurrowableComponent component)
    {
        var currentTime = _gameTicker.RoundDuration();

        Robust.Shared.Log.Logger.Info($"[CAN-START-OBSERVING] Entity {uid}: Checking conditions. Current state={component.State}");

        // Нельзя если уже закопан или наблюдает
        if (component.State != BurrowState.Active)
        {
            Robust.Shared.Log.Logger.Info($"[CAN-START-OBSERVING] Entity {uid}: Wrong state! Expected Active, got {component.State}");
            return false;
        }

        // Нельзя если недавно получил урон
        var timeSinceDamage = (float)(currentTime - component.LastDamageTime).TotalSeconds;
        if (timeSinceDamage < 5f)
        {
            Robust.Shared.Log.Logger.Info($"[CAN-START-OBSERVING] Entity {uid}: Too soon after damage! {timeSinceDamage:F1}s < 5.0s");
            return false;
        }

        // Нельзя если недавно был в бою
        var timeSinceCombat = (float)(currentTime - component.LastCombatTime).TotalSeconds;
        if (timeSinceCombat < component.CombatCooldown)
        {
            Robust.Shared.Log.Logger.Info($"[CAN-START-OBSERVING] Entity {uid}: Combat cooldown! {timeSinceCombat:F1}s < {component.CombatCooldown:F1}s");
            return false;
        }

        Robust.Shared.Log.Logger.Info($"[CAN-START-OBSERVING] Entity {uid}: All checks passed!");
        return true;
    }

    /// <summary>
    /// Попытка закопаться (из состояния Observing)
    /// </summary>
    public bool TryBurrow(EntityUid uid, BurrowableComponent? component = null)
    {
        Robust.Shared.Log.Logger.Info($"TryBurrow: Called for {uid}");

        if (!Resolve(uid, ref component))
        {
            Robust.Shared.Log.Logger.Info($"TryBurrow: Failed to resolve component for {uid}");
            return false;
        }

        Robust.Shared.Log.Logger.Info($"TryBurrow: Current state for {uid} is {component.State}");

        // Закапываться можно только из состояния Observing
        if (component.State != BurrowState.Observing)
        {
            Robust.Shared.Log.Logger.Info($"TryBurrow: Wrong state! Expected Observing, got {component.State}");
            return false;
        }

        // Проверяем достаточно ли долго наблюдали
        var currentTime = _gameTicker.RoundDuration();
        var observeTime = (float)(currentTime - component.StateEnterTime).TotalSeconds;

        Robust.Shared.Log.Logger.Info($"TryBurrow: ObserveTime={observeTime:F1}s, Required={component.ObserveTimeBeforeBurrow:F1}s");

        if (observeTime < component.ObserveTimeBeforeBurrow)
        {
            Robust.Shared.Log.Logger.Info($"TryBurrow: Not enough observe time! {observeTime:F1}s < {component.ObserveTimeBeforeBurrow:F1}s");
            return false;
        }

        // Проверка типа плитки - можно закапываться только на природных поверхностях
        Robust.Shared.Log.Logger.Info($"TryBurrow: Checking tile type for {uid}");

        if (!CanBurrowOnTile(uid))
        {
            Robust.Shared.Log.Logger.Info($"TryBurrow: Wrong tile type! Cannot burrow here.");
            return false;
        }

        Robust.Shared.Log.Logger.Info($"TryBurrow: All checks passed! Burrowing {uid}");

        component.State = BurrowState.Burrowed;
        component.StateEnterTime = currentTime;

        // Проигрываем звук закапывания
        if (component.BurrowSound != null)
        {
            _audio.PlayPvs(component.BurrowSound, uid);
        }

        // Обновляем визуал через appearance
        UpdateVisuals(uid, component);

        // Скорость движения будет 0 (см. OnRefreshMovespeed)
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Robust.Shared.Log.Logger.Info($"TryBurrow: Successfully burrowed {uid}!");

        return true;
    }

    /// <summary>
    /// Выкопаться и вернуться в активное состояние
    /// </summary>
    public void TransitionToActive(EntityUid uid, BurrowableComponent? component = null, bool resetPatrolTimer = false)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.State == BurrowState.Active && !resetPatrolTimer)
            return;

        var wasBurrowed = component.State == BurrowState.Burrowed;

        component.State = BurrowState.Active;
        component.StateEnterTime = _gameTicker.RoundDuration();

        // Сбрасываем таймер патруля если нужно (например, не смогли закопаться)
        if (resetPatrolTimer)
        {
            var random = new Random();
            component.CurrentPatrolDuration = (float)(random.NextDouble() * (component.MaxPatrolTime - component.MinPatrolTime) + component.MinPatrolTime);
            Robust.Shared.Log.Logger.Info($"[TRANSITION-ACTIVE] Entity {uid}: Reset patrol timer! New duration={component.CurrentPatrolDuration:F1}s");
        }

        // Проигрываем звук выкапывания если был закопан
        if (wasBurrowed && component.UnburrowSound != null)
        {
            _audio.PlayPvs(component.UnburrowSound, uid);
        }

        // Восстанавливаем визуал
        UpdateVisuals(uid, component);

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    /// <summary>
    /// Обновить appearance для визуализации на клиенте
    /// </summary>
    private void UpdateVisuals(EntityUid uid, BurrowableComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, BurrowVisuals.Burrowed, component.State == BurrowState.Burrowed, appearance);
    }

    /// <summary>
    /// Отметить начало боя (для кулдауна)
    /// </summary>
    public void MarkCombatStart(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        component.LastCombatTime = _gameTicker.RoundDuration();

        // Если был закопан - выкапываемся
        if (component.State == BurrowState.Burrowed)
        {
            TransitionToActive(uid, component);
        }
    }

    /// <summary>
    /// Проверка: может ли NPC начать патрулирование?
    /// </summary>
    public bool CanStartPatrol(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return true; // Нет компонента - нет ограничений

        // Из состояния Observing можно начать патруль
        // Из Burrowed - нужно сначала выкопаться
        return component.State != BurrowState.Burrowed;
    }

    /// <summary>
    /// Получить текущее состояние
    /// </summary>
    public BurrowState GetState(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return BurrowState.Active;

        return component.State;
    }

    /// <summary>
    /// Проверка: можно ли закопаться на текущей плитке?
    /// </summary>
    private bool CanBurrowOnTile(EntityUid uid)
    {
        Robust.Shared.Log.Logger.Info($"CanBurrowOnTile: Checking tile for {uid}");

        // Получаем координаты entity
        var xform = Transform(uid);
        if (xform.GridUid == null)
        {
            Robust.Shared.Log.Logger.Info($"CanBurrowOnTile: GridUid is null for {uid}");
            return false;
        }

        Robust.Shared.Log.Logger.Info($"CanBurrowOnTile: GridUid={xform.GridUid.Value}");

        // Получаем grid component
        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
        {
            Robust.Shared.Log.Logger.Info($"CanBurrowOnTile: Failed to get MapGridComponent for {uid}");
            return false;
        }

        // Получаем тайл под entity
        var tileRef = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        var tileDef = _turf.GetContentTileDefinition(tileRef);

        Robust.Shared.Log.Logger.Info($"CanBurrowOnTile: TileID={tileDef.ID} for {uid}");

        // Проверяем, является ли тайл природным
        var canBurrow = NaturalTiles.Contains(tileDef.ID);

        Robust.Shared.Log.Logger.Info($"CanBurrowOnTile: TileID={tileDef.ID}, CanBurrow={canBurrow}");

        return canBurrow;
    }

    /// <summary>
    /// Проверка: пора ли закапываться? (патрулировал достаточно долго)
    /// </summary>
    public bool ShouldBurrow(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        Robust.Shared.Log.Logger.Info($"[SHOULDBURROW] Entity {uid}: Starting check. Current state={component.State}");

        // Закапываться можно только из активного состояния
        if (component.State != BurrowState.Active)
        {
            Robust.Shared.Log.Logger.Info($"[SHOULDBURROW] Entity {uid}: Wrong state! Expected Active, got {component.State}");
            return false;
        }

        // ВАЖНО: Если CurrentPatrolDuration еще не инициализирована, инициализируем сейчас
        if (component.CurrentPatrolDuration <= 0)
        {
            var random = new Random();
            component.CurrentPatrolDuration = (float)(random.NextDouble() * (component.MaxPatrolTime - component.MinPatrolTime) + component.MinPatrolTime);
            Robust.Shared.Log.Logger.Warning($"[SHOULDBURROW] Entity {uid}: CurrentPatrolDuration was 0! Initializing to {component.CurrentPatrolDuration:F1}s. StateEnterTime will be reset.");
            // Сбрасываем StateEnterTime чтобы начать отсчет заново
            component.StateEnterTime = _gameTicker.RoundDuration();
        }

        var currentTime = _gameTicker.RoundDuration();
        var timeInState = (float)(currentTime - component.StateEnterTime).TotalSeconds;

        // Патрулировал достаточно долго?
        var shouldBurrow = timeInState >= component.CurrentPatrolDuration;
        Robust.Shared.Log.Logger.Info($"[SHOULDBURROW] Entity {uid}: TimeInState={timeInState:F1}s, Required={component.CurrentPatrolDuration:F1}s, ShouldBurrow={shouldBurrow}");

        return shouldBurrow;
    }

    /// <summary>
    /// Проверка: пора ли выкапываться? (был в засаде достаточно долго)
    /// </summary>
    public bool ShouldUnburrow(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Выкапываться можно только из закопанного состояния
        if (component.State != BurrowState.Burrowed)
            return false;

        var currentTime = _gameTicker.RoundDuration();
        var timeInState = (float)(currentTime - component.StateEnterTime).TotalSeconds;

        // Был в засаде достаточно долго?
        return timeInState >= component.AmbushTime;
    }

    /// <summary>
    /// Начать новый цикл патруля (генерировать новую случайную длительность)
    /// </summary>
    public void StartNewPatrolCycle(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Генерируем новую случайную длительность патруля
        var random = new Random();
        component.CurrentPatrolDuration = (float)(random.NextDouble() * (component.MaxPatrolTime - component.MinPatrolTime) + component.MinPatrolTime);

        // Переходим в активное состояние
        TransitionToActive(uid, component);
    }
}
