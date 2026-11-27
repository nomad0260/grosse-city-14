using System;
using System.Collections.Generic;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Интерфейс смесителя теплоносителей: вливает содержимое канистры в бак,
/// сразу считает характеристики получившейся смеси и сливает её в тару.
/// Формулы общие с охладителем и живут в <see cref="CoolantMixtureMath"/>.
/// </summary>
public sealed partial class CoolantMixerSystem : EntitySystem
{
    private const float UiRefreshInterval = 1f;

    /// <summary>Дольше этого ресурс канистры не показываем — там уже «очень долго».</summary>
    private const float MaxReportedRuntime = 36000f;

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly List<CoolantData> _catalog = CoolantCatalog.CreateDefault();
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoolantMixerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CoolantMixerComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CoolantMixerComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CoolantMixerComponent, CoolantMixerTransferMessage>(OnTransferMessage);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;

        if (_accumulator < UiRefreshInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<CoolantMixerComponent>();

        while (query.MoveNext(out var uid, out var mixer))
        {
            if (_ui.IsUiOpen(uid, CoolantMixerUiKey.Key))
                UpdateUi(uid, mixer);
        }
    }

    private void OnUiOpened(Entity<CoolantMixerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnContainerChanged(Entity<CoolantMixerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnContainerChanged(Entity<CoolantMixerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnTransferMessage(Entity<CoolantMixerComponent> ent, ref CoolantMixerTransferMessage args)
    {
        switch (args.Mode)
        {
            case CoolantMixerTransferMode.PourIn:
                PourIn(ent, args.Actor);
                break;
            case CoolantMixerTransferMode.PourOut:
                PourOut(ent, args.Actor);
                break;
            default:
                return;
        }

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void PourIn(Entity<CoolantMixerComponent> ent, EntityUid user)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, CoolantMixerSlots.Input) is not { } canister)
        {
            _popup.PopupEntity("В ячейке «Откуда» нет ёмкости с жидкостью.", ent.Owner, user);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var bufferEnt, out var buffer) ||
            !_solutions.TryGetDrainableSolution(canister, out var canisterEnt, out var canisterSolution))
        {
            _popup.PopupEntity("Из этой ёмкости нельзя взять жидкость.", ent.Owner, user);
            return;
        }

        if (buffer.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Бак смесителя полон.", ent.Owner, user);
            return;
        }

        var amount = FixedPoint2.Min(canisterSolution.Volume, buffer.AvailableVolume);

        if (amount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Ёмкость пуста.", ent.Owner, user);
            return;
        }

        var transferred = _transfer.Transfer(new SolutionTransferData(
            user,
            canister,
            canisterEnt.Value,
            ent.Owner,
            bufferEnt.Value,
            amount));

        if (transferred > FixedPoint2.Zero)
            _popup.PopupEntity($"Влито {transferred:0} единиц жидкости.", ent.Owner, user);
    }

    private void PourOut(Entity<CoolantMixerComponent> ent, EntityUid user)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, CoolantMixerSlots.Output) is not { } target)
        {
            _popup.PopupEntity("В ячейке «Куда» нет тары.", ent.Owner, user);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var bufferEnt, out var buffer) ||
            !_solutions.TryGetRefillableSolution(target, out var targetEnt, out var targetSolution))
        {
            _popup.PopupEntity("В эту тару нельзя налить жидкость.", ent.Owner, user);
            return;
        }

        if (buffer.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Бак смесителя пуст.", ent.Owner, user);
            return;
        }

        var amount = FixedPoint2.Min(buffer.Volume, targetSolution.AvailableVolume);

        if (amount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Тара уже полна.", ent.Owner, user);
            return;
        }

        var transferred = _transfer.Transfer(new SolutionTransferData(
            user,
            ent.Owner,
            bufferEnt.Value,
            target,
            targetEnt.Value,
            amount));

        if (transferred > FixedPoint2.Zero)
            _popup.PopupEntity($"Слито {transferred:0} единиц смеси.", ent.Owner, user);
    }

    private void UpdateUi(EntityUid uid, CoolantMixerComponent mixer)
    {
        if (!_solutions.TryGetSolution(uid, mixer.Solution, out _, out var solution))
            return;

        var mixture = CoolantMixtureMath.Compute(_catalog, solution);
        CoolantMixerStats? stats = null;

        if (mixture != null)
        {
            var consumption = mixture.GetConsumptionPerSecond(mixer.BaseUse);
            var runtime = DarkCoreCoolantMath.CalculateCanisterRuntime(consumption);

            stats = new CoolantMixerStats(
                mixture.GetCapacity(mixer.BaseCoolingCapacity, mixer.MixturePenalty),
                mixture.HeatCapacity,
                mixture.HeatTransfer,
                mixture.EffectiveFlow,
                mixture.Viscosity,
                mixture.RadiationResistance,
                mixture.Stability,
                mixture.MinTemperatureCelsius,
                mixture.MaxTemperatureCelsius,
                consumption,
                float.IsInfinity(runtime) || runtime > MaxReportedRuntime ? -1f : runtime,
                mixture.WasteMultiplier,
                mixture.Fouling,
                mixture.Contamination,
                mixture.Components,
                mixture.Hazardous,
                BuildBonuses(mixture.DominantReagent));
        }

        var reagents = new List<CoolantMixerReagent>(solution.Contents.Count);

        foreach (var reagent in solution.Contents)
            reagents.Add(new CoolantMixerReagent(reagent.Reagent.Prototype, reagent.Quantity.Float()));

        reagents.Sort((a, b) => b.Amount.CompareTo(a.Amount));

        var state = new CoolantMixerBoundUserInterfaceState(
            solution.Volume.Float(),
            solution.MaxVolume.Float(),
            stats,
            reagents,
            GetItemNet(uid, CoolantMixerSlots.Input),
            GetItemNet(uid, CoolantMixerSlots.Output),
            GetSlotState(uid, CoolantMixerSlots.Input),
            GetSlotState(uid, CoolantMixerSlots.Output));

        _ui.SetUiState(uid, CoolantMixerUiKey.Key, state);
    }

    /// <summary>
    /// Бонусы основной жидкости: охладитель применяет эффекты того реагента,
    /// который доминирует в баке, поэтому и здесь показываем их же.
    /// </summary>
    private CoolantMixerBonuses? BuildBonuses(string reagent)
    {
        if (CoolantCatalog.Find(_catalog, reagent) is not { } data)
            return null;

        var effects = new CoolantEffects
        {
            WearReduction = data.WearReduction,
            WearIncrease = data.WearIncrease,
            HeatGenerationReduction = data.HeatGenerationReduction,
            StabilizationBonus = data.StabilizationBonus,
            EnergyBonus = data.EnergyBonus,
            TemperatureTolerance = data.TemperatureTolerance,
        };

        return new CoolantMixerBonuses(
            data.Reagent.Id,
            effects.EnergyBonus,
            effects.GetWearFactor(),
            effects.StabilizationBonus,
            effects.TemperatureTolerance,
            effects.HeatGenerationReduction);
    }

    private NetEntity? GetItemNet(EntityUid uid, string slot)
    {
        return _itemSlots.GetItemOrNull(uid, slot) is { } item ? GetNetEntity(item) : null;
    }

    private FluidSlotState? GetSlotState(EntityUid uid, string slot)
    {
        if (_itemSlots.GetItemOrNull(uid, slot) is not { } item)
            return null;

        if (!_solutions.TryGetDrainableSolution(item, out _, out var solution))
            return new FluidSlotState(0f, 0f);

        return new FluidSlotState(solution.Volume.Float(), solution.MaxVolume.Float());
    }
}
