using System;
using System.Collections.Generic;
using Content.Server.Stack;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Простые операции с жидкостями и патронами без труб и насосов:
/// заправка канистры, очистка отработанного хладагента и утилизация патрона.
/// </summary>
public sealed partial class DarkCoreProcessorSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkCoreProcessorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DarkCoreProcessorComponent, DarkCoreProcessorDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(Entity<DarkCoreProcessorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!IsValidInput(ent.Comp, args.Used, out var error))
        {
            _popup.PopupEntity(error, ent.Owner, args.User);
            args.Handled = true;
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.Delay,
            new DarkCoreProcessorDoAfterEvent(),
            ent.Owner,
            target: args.Used,
            used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private bool IsValidInput(DarkCoreProcessorComponent processor, EntityUid used, out string error)
    {
        switch (processor.Kind)
        {
            case DarkCoreProcessorKind.CoolantSynthesizer:
                if (!TryComp<DarkCoreCoolantCanisterComponent>(used, out var canister) || canister.Waste)
                {
                    error = "Нужна пустая канистра для хладагента.";
                    return false;
                }

                if (!_solutions.TryGetSolution(used, "solution", out _, out var solution) ||
                    solution.Volume > FixedPoint2.New(0.1f))
                {
                    error = "Канистра должна быть пустой.";
                    return false;
                }

                error = string.Empty;
                return true;

            case DarkCoreProcessorKind.WasteProcessor:
                if (!HasWasteCoolant(used))
                {
                    error = "Нужна канистра или бочка с отработанным хладагентом.";
                    return false;
                }

                error = string.Empty;
                return true;

            case DarkCoreProcessorKind.CartridgeRecycler:
                if (HasComp<DarkCoreSpentCartridgeBarrelComponent>(used))
                {
                    if (!TryComp<StorageComponent>(used, out var storage) || !HasSpentCartridge(storage))
                    {
                        error = "В бочке нет отработанных патронов.";
                        return false;
                    }

                    error = string.Empty;
                    return true;
                }

                if (!TryComp<DarkCoreCartridgeComponent>(used, out var cartridge))
                {
                    error = "Нужна бочка с отработанными патронами РТМ.";
                    return false;
                }

                // Полный патрон разбирается напрямую; пустой принимается только
                // в бочке, чтобы его нельзя было случайно уничтожить без сырья.
                if (cartridge.Resource <= 0.01f)
                {
                    error = "Отработанный патрон принимается только в бочке.";
                    return false;
                }

                error = string.Empty;
                return true;

            default:
                error = "Установка не настроена.";
                return false;
        }
    }

    private void OnDoAfter(Entity<DarkCoreProcessorComponent> ent, ref DarkCoreProcessorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || !Exists(used))
            return;

        args.Handled = true;

        switch (ent.Comp.Kind)
        {
            case DarkCoreProcessorKind.CoolantSynthesizer:
                SynthesizeCoolant(ent, used);
                break;

            case DarkCoreProcessorKind.WasteProcessor:
                RecycleWaste(ent, used, args.User);
                break;

            case DarkCoreProcessorKind.CartridgeRecycler:
                if (HasComp<DarkCoreSpentCartridgeBarrelComponent>(used))
                    RecycleCartridgeBarrel(ent, used);
                else
                    RecycleCartridge(ent, used);
                break;
        }
    }

    private void SynthesizeCoolant(Entity<DarkCoreProcessorComponent> ent, EntityUid canister)
    {
        if (!_solutions.TryGetSolution(canister, "solution", out var solutionEnt, out _))
            return;

        var coolant = "DarkCoreCoolant";

        if (TryComp<DarkCoreCoolantCanisterComponent>(canister, out var canisterComp))
        {
            coolant = canisterComp.CoolantReagent.Id;
            canisterComp.Waste = false;
        }

        _solutions.RemoveAllSolution(solutionEnt.Value);
        _solutions.TryAddReagent(solutionEnt.Value, coolant, FixedPoint2.New(ent.Comp.CoolantAmount), out _);

        _popup.PopupEntity("Канистра заправлена выбранным хладагентом РТМ.", ent.Owner);
    }

    private void RecycleWaste(Entity<DarkCoreProcessorComponent> ent, EntityUid canister, EntityUid user)
    {
        var coolant = "DarkCoreCoolant";

        if (TryComp<DarkCoreCoolantCanisterComponent>(canister, out var canisterComp))
            coolant = canisterComp.CoolantReagent.Id;

        // Бочка хранит хладагент в баке tank, канистра — в solution.
        if (!_solutions.TryGetSolution(canister, "solution", out var solutionEnt, out var wasteSolution) ||
            wasteSolution.GetTotalPrototypeQuantity("DarkCoreCoolantWaste") <= FixedPoint2.Zero)
        {
            if (!_solutions.TryGetSolution(canister, "tank", out var tankEnt, out var tankSolution) ||
                tankSolution.GetTotalPrototypeQuantity("DarkCoreCoolantWaste") <= FixedPoint2.Zero)
            {
                return;
            }

            _solutions.RemoveAllSolution(tankEnt.Value);
            _solutions.TryAddReagent(tankEnt.Value, coolant, FixedPoint2.New(ent.Comp.RecoveredCoolantAmount), out _);
            _popup.PopupEntity(
                $"Отработанный хладагент очищен в бочке: получено {ent.Comp.RecoveredCoolantAmount:0} единиц.",
                ent.Owner,
                user);
            return;
        }

        var coordinates = Transform(ent).Coordinates;
        var cleanCanister = Spawn("DarkCoreCoolantCanister", coordinates);

        if (_solutions.TryGetSolution(cleanCanister, "solution", out var cleanSolutionEnt, out _))
        {
            _solutions.RemoveAllSolution(cleanSolutionEnt.Value);
            _solutions.TryAddReagent(cleanSolutionEnt.Value, coolant, FixedPoint2.New(ent.Comp.RecoveredCoolantAmount), out _);
        }

        if (TryComp<DarkCoreCoolantCanisterComponent>(cleanCanister, out var cleanComp))
        {
            cleanComp.CoolantReagent = new ProtoId<ReagentPrototype>(coolant);
            cleanComp.Waste = false;
        }

        QueueDel(canister);
        _popup.PopupEntity($"Отработанный хладагент переработан: получено {ent.Comp.RecoveredCoolantAmount:0} единиц.", ent.Owner, user);
    }

    private void RecycleCartridge(Entity<DarkCoreProcessorComponent> ent, EntityUid cartridge)
    {
        if (!TryComp<DarkCoreCartridgeComponent>(cartridge, out var oldCartridge))
            return;

        var scrapAmount = GetCartridgeScrap(oldCartridge.Kind, ent.Comp.RecycledCartridgeFraction);
        SpawnScrap(Transform(ent).Coordinates, scrapAmount);

        _popup.PopupEntity(
            $"Патрон разрушен: получено {scrapAmount} единиц сырья для новых патронов.",
            ent.Owner);

        QueueDel(cartridge);
    }

    private void RecycleCartridgeBarrel(Entity<DarkCoreProcessorComponent> ent, EntityUid barrel)
    {
        if (!TryComp<StorageComponent>(barrel, out var storage))
            return;

        var coordinates = Transform(ent).Coordinates;
        var cartridges = new List<EntityUid>(storage.Container.ContainedEntities);
        var scrapAmount = 0;
        var destroyedCount = 0;

        foreach (var cartridge in cartridges)
        {
            if (!TryComp<DarkCoreCartridgeComponent>(cartridge, out var cartridgeComp))
                continue;

            scrapAmount += GetCartridgeScrap(cartridgeComp.Kind, ent.Comp.RecycledCartridgeFraction);
            QueueDel(cartridge);
            destroyedCount++;
        }

        if (scrapAmount > 0)
            SpawnScrap(coordinates, scrapAmount);

        _popup.PopupEntity(
            $"Уничтожено патронов: {destroyedCount}. Получено сырья: {scrapAmount} единиц.",
            ent.Owner);
    }

    private static int GetCartridgeScrap(DarkCoreCartridgeType kind, float fraction)
    {
        var fullValue = kind switch
        {
            DarkCoreCartridgeType.Alpha => 100,
            DarkCoreCartridgeType.Beta => 120,
            DarkCoreCartridgeType.Gamma => 120,
            DarkCoreCartridgeType.Digamma => 200,
            _ => 100,
        };

        return Math.Max(1, (int) MathF.Round(fullValue * Math.Clamp(fraction, 0f, 1f)));
    }

    private void SpawnScrap(EntityCoordinates coordinates, int amount)
    {
        if (amount <= 0)
            return;

        var scrap = Spawn("DarkCoreCartridgeScrap1", coordinates);
        if (TryComp<StackComponent>(scrap, out var stack))
            _stack.SetCount((scrap, stack), amount);
    }

    private bool HasWasteCoolant(EntityUid container)
    {
        return HasWasteCoolantIn(container, "solution") || HasWasteCoolantIn(container, "tank");
    }

    private bool HasWasteCoolantIn(EntityUid container, string solutionName)
    {
        return _solutions.TryGetSolution(container, solutionName, out _, out var solution) &&
               solution.GetTotalPrototypeQuantity("DarkCoreCoolantWaste") > FixedPoint2.Zero;
    }

    private bool HasSpentCartridge(StorageComponent storage)
    {
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (HasComp<DarkCoreCartridgeComponent>(item))
                return true;
        }

        return false;
    }
}
