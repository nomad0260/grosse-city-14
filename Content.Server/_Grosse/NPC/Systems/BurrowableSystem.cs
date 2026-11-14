using System;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared._Grosse.NPC.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Состояния закапывания NPC: Active → Observing → Burrowed → Active.
/// <para>
/// Система только хранит состояние и переключает его; время отсчитывается по
/// <see cref="IGameTiming.CurTime"/>, поэтому паузы раунда и перемотка времени
/// не ломают таймеры. В горячем пути (его дёргают HTN-операторы каждый тик)
/// нет ни логирования, ни аллокаций.
/// </para>
/// </summary>
public sealed partial class BurrowableSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private NpcPointManagerSystem _points = default!;

    /// <summary>
    /// Природные поверхности, на которых можно закапываться. Используются как
    /// дополнение к <see cref="BurrowableComponent.AllowedTileIds"/>, чтобы
    /// прототипы без своего списка работали как раньше.
    /// </summary>
    private static readonly HashSet<string> NaturalTiles = new()
    {
        "FloorDirt",
        "FloorPlanetDirt",
        "FloorGrass",
        "FloorGrassJungle",
        "FloorGrassDark",
        "FloorGrassLight",
        "FloorPlanetGrass",
        "FloorJungleAstroGrass",
        "FloorAsteroidSand",
        "FloorAsteroidSandBorderless",
        "FloorAsteroidSandDug",
        "FloorAsteroidSandDugBorderless",
        "FloorAsteroidSandRed",
        "FloorAsteroidSandRedBorderless",
        "FloorAsteroidSandUnvariantized",
        "FloorAsteroidSandUnvariantizedBorderless",
        "FloorAsteroidIronsand",
        "FloorAsteroidIronsandBorderless",
        "FloorAsteroidIronsandUnvariantized",
        "FloorAsteroidIronsandUnvariantizedBorderless",
        "FloorCave",
        "FloorCaveDrought",
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
        component.StateEnterTime = _timing.CurTime;

        // Без этого компонента клиентский визуализатор не подписывается на
        // appearance, и закопанный моб остаётся видимым.
        EnsureComp<BurrowableVisualsComponent>(uid);

        if (component.CurrentPatrolDuration <= 0f)
            component.CurrentPatrolDuration = RollPatrolDuration(component);

        // Моб может быть заспавнен уже закопанным: визуал и скорость нужно
        // применить сразу, иначе он останется видимым и продолжит ходить.
        if (component.State == BurrowState.Burrowed)
        {
            UpdateVisuals(uid, component);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
        }
    }

    private void OnRefreshMovespeed(EntityUid uid, BurrowableComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.State == BurrowState.Burrowed)
            args.ModifySpeed(component.BurrowedSpeedMultiplier, component.BurrowedSpeedMultiplier);
    }

    private void OnDamaged(EntityUid uid, BurrowableComponent component, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() <= 0)
            return;

        var now = _timing.CurTime;
        component.LastDamageTime = now;
        component.LastCombatTime = now;

        // Получил урон в засаде — сразу выкапывается и отвечает.
        if (component.State == BurrowState.Burrowed)
            TransitionToActive(uid, component);
    }

    /// <summary>
    /// Начинает фазу наблюдения перед закапыванием.
    /// </summary>
    public bool TryStartObserving(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!CanStartObserving(uid, component))
            return false;

        component.State = BurrowState.Observing;
        component.StateEnterTime = _timing.CurTime;
        return true;
    }

    private bool CanStartObserving(EntityUid uid, BurrowableComponent component)
    {
        if (component.State != BurrowState.Active)
            return false;

        var now = _timing.CurTime;

        if ((now - component.LastDamageTime).TotalSeconds < component.DamageCooldown)
            return false;

        if ((now - component.LastCombatTime).TotalSeconds < component.CombatCooldown)
            return false;

        return true;
    }

    /// <summary>
    /// Закапывается, если NPC уже достаточно долго наблюдает и стоит на подходящей поверхности.
    /// </summary>
    public bool TryBurrow(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (component.State != BurrowState.Observing)
            return false;

        var now = _timing.CurTime;

        if ((now - component.StateEnterTime).TotalSeconds < component.ObserveTimeBeforeBurrow)
            return false;

        if (!CanBurrowOnTile(uid, component))
            return false;

        component.State = BurrowState.Burrowed;
        component.StateEnterTime = now;

        // Закапался — точка больше не нужна. После выкапывания NPC пойдёт
        // к следующей, а не вернётся в ту же самую.
        ForgetPatrolPoint(uid);

        if (component.BurrowSound != null)
            _audio.PlayPvs(component.BurrowSound, uid);

        UpdateVisuals(uid, component);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        return true;
    }

    /// <summary>
    /// Возвращает NPC в активное состояние: после урона, боя или неудачной попытки закопаться.
    /// </summary>
    public void TransitionToActive(EntityUid uid, BurrowableComponent? component = null, bool resetPatrolTimer = false)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (component.State == BurrowState.Active && !resetPatrolTimer)
            return;

        var wasBurrowed = component.State == BurrowState.Burrowed;

        component.State = BurrowState.Active;
        component.StateEnterTime = _timing.CurTime;

        if (resetPatrolTimer)
        {
            component.CurrentPatrolDuration = RollPatrolDuration(component);

            // Не удалось закопаться: следующая точка, а не та же самая.
            ForgetPatrolPoint(uid);
        }

        if (wasBurrowed && component.UnburrowSound != null)
            _audio.PlayPvs(component.UnburrowSound, uid);

        UpdateVisuals(uid, component);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void UpdateVisuals(EntityUid uid, BurrowableComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, BurrowVisuals.Burrowed, component.State == BurrowState.Burrowed, appearance);
    }

    /// <summary>
    /// Отмечает начало боя: закопанный NPC выкапывается, и закапывание блокируется на кулдаун.
    /// </summary>
    public void MarkCombatStart(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        component.LastCombatTime = _timing.CurTime;

        if (component.State == BurrowState.Burrowed)
            TransitionToActive(uid, component);
    }

    public bool CanStartPatrol(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return true;

        return component.State != BurrowState.Burrowed;
    }

    public BurrowState GetState(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return BurrowState.Active;

        return component.State;
    }

    private bool CanBurrowOnTile(EntityUid uid, BurrowableComponent component)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tileRef = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        var tileDef = _turf.GetContentTileDefinition(tileRef);

        return component.AllowedTileIds.Contains(tileDef.ID) || NaturalTiles.Contains(tileDef.ID);
    }

    /// <summary>
    /// Пора ли закапываться: NPC патрулировал достаточно долго.
    /// </summary>
    public bool ShouldBurrow(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (component.State != BurrowState.Active)
            return false;

        if (component.CurrentPatrolDuration <= 0f)
        {
            component.CurrentPatrolDuration = RollPatrolDuration(component);
            component.StateEnterTime = _timing.CurTime;
            return false;
        }

        return (_timing.CurTime - component.StateEnterTime).TotalSeconds >= component.CurrentPatrolDuration;
    }

    /// <summary>
    /// Пора ли выкапываться: NPC просидел в засаде достаточно долго.
    /// </summary>
    public bool ShouldUnburrow(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (component.State != BurrowState.Burrowed)
            return false;

        return (_timing.CurTime - component.StateEnterTime).TotalSeconds >= component.AmbushTime;
    }

    /// <summary>
    /// Начинает новый цикл патруля с новой случайной длительностью.
    /// </summary>
    public void StartNewPatrolCycle(EntityUid uid, BurrowableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        component.CurrentPatrolDuration = RollPatrolDuration(component);
        TransitionToActive(uid, component);

        // Выкопался — идём к следующей точке.
        ForgetPatrolPoint(uid);
    }

    /// <summary>
    /// Освобождает удержанные точки NPC, чтобы следующая патрульная цель
    /// была другой. Вызывается при закапывании, неудачной попытке и выкапывании.
    /// </summary>
    private void ForgetPatrolPoint(EntityUid uid)
    {
        if (!TryComp<NpcPointTrackerComponent>(uid, out var tracker))
            return;

        _points.ReleaseAll(uid, tracker);
    }

    private float RollPatrolDuration(BurrowableComponent component)
    {
        var min = MathF.Min(component.MinPatrolTime, component.MaxPatrolTime);
        var max = MathF.Max(component.MinPatrolTime, component.MaxPatrolTime);
        return min + _random.NextFloat() * (max - min);
    }
}
