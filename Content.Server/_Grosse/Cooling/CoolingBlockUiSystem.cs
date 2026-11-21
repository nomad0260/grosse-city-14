using System;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Интерфейс охладителя: заливка бака из баллона, слив одной отработки
/// и полный слив в тару. Сама жидкостная модель остаётся в
/// <see cref="CoolingBlockCoolantSystem"/> и не дублируется здесь.
/// </summary>
public sealed partial class CoolingBlockUiSystem : EntitySystem
{
    private const float UiRefreshInterval = 1f;

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CoolingBlockCoolantSystem _coolant = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoolingBlockCoolantComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CoolingBlockCoolantComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CoolingBlockCoolantComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CoolingBlockCoolantComponent, CoolerTransferMessage>(OnTransferMessage);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;

        if (_accumulator < UiRefreshInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<CoolingBlockCoolantComponent>();

        while (query.MoveNext(out var uid, out var coolant))
        {
            if (_ui.IsUiOpen(uid, CoolerUiKey.Key))
                UpdateUi(uid, coolant);
        }
    }

    private void OnUiOpened(Entity<CoolingBlockCoolantComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnContainerChanged(Entity<CoolingBlockCoolantComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnContainerChanged(Entity<CoolingBlockCoolantComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnTransferMessage(Entity<CoolingBlockCoolantComponent> ent, ref CoolerTransferMessage args)
    {
        switch (args.Mode)
        {
            case CoolerTransferMode.Fill:
                FillFromCanister(ent, args.Actor);
                break;
            case CoolerTransferMode.DrainWaste:
                DrainWasteIntoCanister(ent, args.Actor);
                break;
            case CoolerTransferMode.DrainAll:
                DrainAllIntoCanister(ent, args.Actor);
                break;
            default:
                return;
        }

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void FillFromCanister(Entity<CoolingBlockCoolantComponent> ent, EntityUid user)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, CoolerSlots.Coolant) is not { } canister)
        {
            _popup.PopupEntity("В ячейке нет баллона с хладагентом.", ent.Owner, user);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var tankEnt, out var tank) ||
            !_solutions.TryGetDrainableSolution(canister, out var canisterEnt, out var canisterSolution))
        {
            return;
        }

        if (tank.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Бак охладителя уже полон.", ent.Owner, user);
            return;
        }

        var amount = FixedPoint2.Min(canisterSolution.Volume, tank.AvailableVolume);

        if (amount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Баллон пуст.", ent.Owner, user);
            return;
        }

        var moved = _transfer.Transfer(new SolutionTransferData(
            user,
            canister,
            canisterEnt.Value,
            ent.Owner,
            tankEnt.Value,
            amount));

        if (moved > FixedPoint2.Zero)
            _popup.PopupEntity($"Залито {moved:0} единиц жидкости.", ent.Owner, user);
    }

    private void DrainWasteIntoCanister(Entity<CoolingBlockCoolantComponent> ent, EntityUid user)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, CoolerSlots.Waste) is not { } canister)
        {
            _popup.PopupEntity("В ячейке нет тары для отработки.", ent.Owner, user);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var tankEnt, out var tank) ||
            !_solutions.TryGetRefillableSolution(canister, out var canisterEnt, out var canisterSolution))
        {
            return;
        }

        var waste = tank.GetTotalPrototypeQuantity(ent.Comp.WasteReagent);

        if (waste <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("В баке нет отработки.", ent.Owner, user);
            return;
        }

        if (canisterSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Тара уже полна.", ent.Owner, user);
            return;
        }

        var removed = _solutions.RemoveReagent(
            tankEnt.Value,
            ent.Comp.WasteReagent.Id,
            FixedPoint2.Min(waste, canisterSolution.AvailableVolume));

        if (removed <= FixedPoint2.Zero)
            return;

        _solutions.TryAddReagent(canisterEnt.Value, ent.Comp.WasteReagent.Id, removed, out _);
        _popup.PopupEntity($"Слито {removed:0} единиц отработки. Чистый хладагент остался в баке.", ent.Owner, user);
    }

    private void DrainAllIntoCanister(Entity<CoolingBlockCoolantComponent> ent, EntityUid user)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, CoolerSlots.Waste) is not { } canister)
        {
            _popup.PopupEntity("В ячейке нет тары для отработки.", ent.Owner, user);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var tankEnt, out var tank) ||
            !_solutions.TryGetRefillableSolution(canister, out var canisterEnt, out var canisterSolution))
        {
            return;
        }

        if (tank.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Бак охладителя пуст.", ent.Owner, user);
            return;
        }

        if (canisterSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Тара уже полна.", ent.Owner, user);
            return;
        }

        var amount = FixedPoint2.Min(tank.Volume, canisterSolution.AvailableVolume);

        var moved = _transfer.Transfer(new SolutionTransferData(
            user,
            ent.Owner,
            tankEnt.Value,
            canister,
            canisterEnt.Value,
            amount));

        if (moved > FixedPoint2.Zero)
            _popup.PopupEntity($"Слито {moved:0} единиц жидкости.", ent.Owner, user);
    }

    private FluidSlotState? GetSlotState(EntityUid uid, string slotId)
    {
        if (_itemSlots.GetItemOrNull(uid, slotId) is not { } item)
            return null;

        if (!_solutions.TryGetDrainableSolution(item, out _, out var solution))
            return new FluidSlotState(0f, 0f);

        return new FluidSlotState(solution.Volume.Float(), solution.MaxVolume.Float());
    }

    private void UpdateUi(EntityUid uid, CoolingBlockCoolantComponent coolant)
    {
        if (!_ui.HasUi(uid, CoolerUiKey.Key))
            return;

        if (!_solutions.TryGetSolution(uid, coolant.Solution, out _, out var solution))
            return;

        var support = CompOrNull<ReactorSupportComponent>(uid);

        _ui.SetUiState(uid, CoolerUiKey.Key, new CoolerBoundUserInterfaceState(
            solution.Volume.Float(),
            solution.MaxVolume.Float(),
            solution.GetTotalPrototypeQuantity(coolant.WasteReagent).Float(),
            coolant.MaxWaste,
            _coolant.GetDominantCoolant(uid) ?? string.Empty,
            support?.CoolingCapacity ?? 0f,
            GetNetEntity(_itemSlots.GetItemOrNull(uid, CoolerSlots.Coolant)),
            GetNetEntity(_itemSlots.GetItemOrNull(uid, CoolerSlots.Waste)),
            GetSlotState(uid, CoolerSlots.Coolant),
            GetSlotState(uid, CoolerSlots.Waste)));
    }
}
