using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Grosse.DarkMatter;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Content.Server.Stack;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.DarkMatter;

/// <summary>
/// The scientific terminal and the shared machines of EНК-ТМ. The terminal is only a controller:
/// analysis, synthesis, crystal formation and experiments happen on the server.
/// </summary>
public sealed partial class DarkMatterResearchSystem : EntitySystem
{
    [Dependency] private EffectGraphSystem _graphs = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ResearchArtifactSystem _artifacts = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private DarkMatterArchiveSystem _archive = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkMatterMachineComponent, ComponentInit>(OnMachineInit);
        SubscribeLocalEvent<DarkMatterMachineComponent, ComponentRemove>(OnMachineRemove);

        SubscribeLocalEvent<ResearchTerminalComponent, BoundUIOpenedEvent>(OnTerminalOpened);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchSaveFormulaMessage>(OnSaveFormula);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchLoadFormulaMessage>(OnLoadFormula);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchDeleteFormulaMessage>(OnDeleteFormula);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchRenameFormulaMessage>(OnRenameFormula);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchCopyFormulaMessage>(OnCopyFormula);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchSynthesizeMessage>(OnSynthesize);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchCrystallizeMessage>(OnCrystallize);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchReflashCrystalMessage>(OnReflashCrystal);
        SubscribeLocalEvent<ResearchTerminalComponent, ResearchTestMessage>(OnTest);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ResearchTerminalComponent>();

        while (query.MoveNext(out var uid, out var terminal))
        {
            if (now < terminal.NextRefresh)
                continue;

            terminal.NextRefresh = now + TimeSpan.FromSeconds(MathF.Max(0.1f, terminal.RefreshInterval));

            if (_ui.IsUiOpen(uid, ResearchTerminalUiKey.Key))
                UpdateUi(uid, terminal);
        }
    }

    private void OnMachineInit(EntityUid uid, DarkMatterMachineComponent machine, ComponentInit args)
    {
        if (machine.Kind != DarkMatterMachineKind.Synthesizer)
        {
            machine.Slot.Name = "образец";
            _slots.AddItemSlot(uid, DarkMatterMachineComponent.SlotId, machine.Slot);
            return;
        }

        machine.LiquidSlot.Name = "жидкость";
        machine.LiquidSlot.Whitelist = new EntityWhitelist { Components = new[] { "FitsInDispenser" } };
        machine.OreSlot.Name = "сырьё";
        machine.OreSlot.Whitelist = new EntityWhitelist { Components = new[] { "Material" } };
        machine.ClotSlot.Name = "сгусток тёмной материи";
        machine.ClotSlot.Whitelist = new EntityWhitelist { Components = new[] { "DarkMatterClot" } };
        _slots.AddItemSlot(uid, DarkMatterMachineComponent.LiquidSlotId, machine.LiquidSlot);
        _slots.AddItemSlot(uid, DarkMatterMachineComponent.OreSlotId, machine.OreSlot);
        _slots.AddItemSlot(uid, DarkMatterMachineComponent.ClotSlotId, machine.ClotSlot);
        EnsureRequirements(machine);
    }

    private void OnMachineRemove(EntityUid uid, DarkMatterMachineComponent machine, ComponentRemove args)
    {
        if (machine.Kind == DarkMatterMachineKind.Synthesizer)
        {
            _slots.RemoveItemSlot(uid, machine.LiquidSlot);
            _slots.RemoveItemSlot(uid, machine.OreSlot);
            _slots.RemoveItemSlot(uid, machine.ClotSlot);
            return;
        }

        _slots.RemoveItemSlot(uid, machine.Slot);
    }

    private void OnTerminalOpened(EntityUid uid, ResearchTerminalComponent terminal, BoundUIOpenedEvent args)
    {
        _archive.MergeInto(terminal.Archive);
        UpdateUi(uid, terminal);
    }

    private void OnSaveFormula(EntityUid uid, ResearchTerminalComponent terminal, ResearchSaveFormulaMessage args)
    {
        if (!EffectGraphValidator.Validate(args.Graph, out var errors))
        {
            terminal.LastStatus = string.Join(" ", errors);
            UpdateUi(uid, terminal);
            return;
        }

        if (terminal.Archive.Count >= 32)
        {
            terminal.LastStatus = "Архив заполнен: удалите старую формулу.";
            UpdateUi(uid, terminal);
            return;
        }

        var record = new ResearchFormulaRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = NormalizeName(args.Name, "Новая формула"),
            Author = Name(args.Actor),
            Created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
            Version = args.Graph.Version,
            MaterialName = FindMaterialSource(uid, terminal) is { } materialSource &&
                           TryComp<ResearchMaterialComponent>(materialSource, out var savedMaterial)
                ? savedMaterial.MaterialName
                : string.Empty,
            Graph = args.Graph.Clone(),
        };

        terminal.Archive.Add(record);
        _archive.Upsert(record);
        _archive.Save();
        terminal.SelectedFormulaId = record.Id;
        terminal.Draft = record.Graph.Clone();
        terminal.Revision++;
        terminal.LastStatus = $"Формула «{record.Name}» сохранена в архив.";
        UpdateUi(uid, terminal);
    }

    private void OnLoadFormula(EntityUid uid, ResearchTerminalComponent terminal, ResearchLoadFormulaMessage args)
    {
        var record = terminal.Archive.FirstOrDefault(formula => formula.Id == args.Id);
        if (record == null)
            return;

        terminal.SelectedFormulaId = record.Id;
        terminal.Draft = record.Graph.Clone();
        terminal.Revision++;
        terminal.LastStatus = $"Формула «{record.Name}» загружена в конструктор.";
        UpdateUi(uid, terminal);
    }

    private void OnDeleteFormula(EntityUid uid, ResearchTerminalComponent terminal, ResearchDeleteFormulaMessage args)
    {
        if (terminal.Archive.RemoveAll(formula => formula.Id == args.Id) == 0)
        {
            terminal.LastStatus = "Формула не найдена в архиве.";
            UpdateUi(uid, terminal);
            return;
        }

        _archive.Remove(args.Id);
        _archive.Save();

        if (terminal.SelectedFormulaId == args.Id)
            terminal.SelectedFormulaId = string.Empty;

        terminal.Revision++;
        terminal.LastStatus = "Формула удалена из архива.";
        UpdateUi(uid, terminal);
    }

    private void OnRenameFormula(EntityUid uid, ResearchTerminalComponent terminal, ResearchRenameFormulaMessage args)
    {
        var record = terminal.Archive.FirstOrDefault(formula => formula.Id == args.Id);
        if (record == null)
            return;

        record.Name = NormalizeName(args.Name, record.Name);
        _archive.Upsert(record);
        _archive.Save();
        terminal.Revision++;
        terminal.LastStatus = $"Формула переименована в «{record.Name}».";
        UpdateUi(uid, terminal);
    }

    private void OnCopyFormula(EntityUid uid, ResearchTerminalComponent terminal, ResearchCopyFormulaMessage args)
    {
        var record = terminal.Archive.FirstOrDefault(formula => formula.Id == args.Id);
        if (record == null || terminal.Archive.Count >= 32)
            return;

        var copy = record.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = NormalizeName(record.Name + " (копия)", "Копия формулы");
        copy.Author = Name(args.Actor);
        copy.Created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        terminal.Archive.Add(copy);
        _archive.Upsert(copy);
        _archive.Save();
        terminal.SelectedFormulaId = copy.Id;
        terminal.Draft = copy.Graph.Clone();
        terminal.Revision++;
        terminal.LastStatus = $"Создана копия «{copy.Name}».";
        UpdateUi(uid, terminal);
    }

    private void OnSynthesize(EntityUid uid, ResearchTerminalComponent terminal, ResearchSynthesizeMessage args)
    {
        var synthesizer = FindMachine(uid, terminal, DarkMatterMachineKind.Synthesizer);
        if (synthesizer == null ||
            !TryComp<DarkMatterMachineComponent>(synthesizer, out var synthesizerComp))
        {
            _popup.PopupEntity("В радиусе терминала нет КС-1 «Матрица».", uid, args.Actor);
            return;
        }

        if (synthesizerComp.LiquidSlot.Item is not { } liquidItem)
        {
            _popup.PopupEntity("В КС-1 не залита жидкость.", synthesizer.Value, args.Actor);
            return;
        }

        if (synthesizerComp.OreSlot.Item is not { } oreItem)
        {
            _popup.PopupEntity("В КС-1 не загружено сырьё.", synthesizer.Value, args.Actor);
            return;
        }

        if (synthesizerComp.ClotSlot.Item is not { } clotItem ||
            !TryComp<DarkMatterClotComponent>(clotItem, out var clotComp))
        {
            _popup.PopupEntity("В КС-1 не загружен сгусток тёмной материи.", synthesizer.Value, args.Actor);
            return;
        }

        EnsureRequirements(synthesizerComp);
        if (!_prototype.TryIndex(synthesizerComp.RequiredLiquidId, out DarkMatterMaterialPrototype? liquid) ||
            liquid == null ||
            !_prototype.TryIndex(synthesizerComp.RequiredOreId, out DarkMatterMaterialPrototype? ore) ||
            ore == null)
        {
            _popup.PopupEntity("КС-1 ещё не сформировала заказ на синтез.", synthesizer.Value, args.Actor);
            return;
        }

        if (!_solution.TryGetFitsInDispenser(liquidItem, out var liquidSolution, out var solution) ||
            solution.Volume.Float() < 10f)
        {
            _popup.PopupEntity("Для синтеза нужно не менее 10 единиц жидкости.", synthesizer.Value, args.Actor);
            return;
        }

        if (!TryComp<PhysicalCompositionComponent>(oreItem, out var composition))
        {
            _popup.PopupEntity("В слоте сырья должен быть материал с физическим составом.", synthesizer.Value, args.Actor);
            return;
        }

        var liquidEntry = solution.Contents.FirstOrDefault(entry =>
            entry.Quantity >= 10 && liquid.ReagentIds.Contains(entry.Reagent.Prototype));
        if (liquidEntry.Quantity < 10)
        {
            _popup.PopupEntity($"Для заказа нужно 10 единиц жидкости: {liquid.Name}.", synthesizer.Value, args.Actor);
            return;
        }

        if (!ore.MaterialIds.Any(id => composition.MaterialComposition.ContainsKey(id)))
        {
            _popup.PopupEntity($"Для заказа нужно сырьё: {ore.Name}.", synthesizer.Value, args.Actor);
            return;
        }

        var clot = clotComp.Clot;
        var liquidName = liquid.Name;
        var oreName = ore.Name;
        var sample = Spawn("DarkMatterResearchSample", Transform(synthesizer.Value).Coordinates);
        var material = EnsureComp<ResearchMaterialComponent>(sample);

        material.MaterialName = NormalizeName(args.Name, $"{oreName} + {liquidName}");
        material.Density = ClampStat(liquid.Density * 0.35f + ore.Density * 0.45f + clot.Density * 0.2f, 0.1f, 10f);
        material.Strength = ClampStat(ore.Strength * 0.55f + clot.Strength * 0.45f, 0.1f, 10f);
        material.Hardness = ClampStat(ore.Strength * 0.5f + clot.Density * 0.5f, 0.1f, 10f);
        // Conductivity is the inverse of thermal resistance: a coolant with high resistance
        // should not make the resulting crystal a better heat conductor.
        material.ThermalConductivity = ClampStat(
            1f / MathF.Max(0.1f, liquid.ThermalResistance * 0.5f + 0.5f), 0.1f, 10f);
        material.ElectricalConductivity = ClampStat(ore.Efficiency * 0.6f + liquid.Efficiency * 0.4f, 0.1f, 10f);
        material.ThermalResistance = ClampStat(ore.ThermalResistance * 0.45f + liquid.ThermalResistance * 0.25f + clot.ThermalResistance * 0.3f, 0.1f, 10f);
        material.RadiationResistance = ClampStat(1.5f - ore.RadiationActivity * 0.05f - clot.RadiationActivity * 0.02f, 0.1f, 10f);
        material.EnergyCapacity = ClampStat(60f + ore.EnergyCapacity * 0.35f + liquid.EnergyCapacity * 0.2f + clot.EnergyCapacity * 0.45f, 10f, 2000f);
        material.Stability = ClampStat(ore.Stability * 0.25f + liquid.Stability * 0.2f + clot.Stability * 0.55f - 8f, 1f, 100f);
        material.Reactivity = ClampStat(0.4f + ore.Efficiency * 0.25f + liquid.Efficiency * 0.2f + clot.Efficiency * 0.35f, 0.1f, 5f);
        material.Degradation = ClampStat(ore.Degradation * 0.4f + liquid.Degradation * 0.25f + clot.Degradation * 0.35f, 0f, 5f);
        material.Purity = ClampStat(ore.Purity * 0.35f + liquid.Purity * 0.25f + clot.Purity * 0.4f, 0f, 1f);
        material.DarkMatter = ClampStat(clot.Potency, 0f, 10f);
        material.RadiationActivity = ClampStat(ore.RadiationActivity * 0.35f + clot.RadiationActivity * 0.65f, 0f, 20f);
        material.EffectEfficiency = ClampStat(ore.Efficiency * 0.25f + liquid.Efficiency * 0.25f + clot.Efficiency * 0.5f, 0.1f, 5f);
        material.Color = ore.Color;

        // All three feeds are consumed only after validation.
        _solution.RemoveReagent(liquidSolution!.Value, liquidEntry.Reagent, FixedPoint2.New(10));
        ConsumeOne(oreItem);
        QueueDel(clotItem);
        RollRequirements(synthesizerComp);

        terminal.LastSynthesis = $"{liquidName} + {oreName} + {clot.Name}";
        _meta.SetEntityName(sample, material.MaterialName);

        if (!_hands.TryPickupAnyHand(args.Actor, sample))
            _popup.PopupEntity("Образец создан рядом с КС-1.", synthesizer.Value, args.Actor);

        terminal.LastStatus = $"Синтезирован образец «{material.MaterialName}». Израсходованы жидкость, сырьё и сгусток. Новый заказ готов.";
        UpdateUi(uid, terminal);
    }

    private void OnCrystallize(EntityUid uid, ResearchTerminalComponent terminal, ResearchCrystallizeMessage args)
    {
        if (!EffectGraphValidator.Validate(args.Graph, out var errors))
        {
            terminal.LastStatus = string.Join(" ", errors);
            UpdateUi(uid, terminal);
            return;
        }

        var analyzer = FindMachine(uid, terminal, DarkMatterMachineKind.Analyzer);
        if (analyzer == null ||
            !TryComp<DarkMatterMachineComponent>(analyzer, out var analyzerComp) ||
            analyzerComp.Slot.Item is not { } sample ||
            !TryComp<ResearchMaterialComponent>(sample, out var material))
        {
            _popup.PopupEntity("Вставьте синтезированный образец в анализатор.", uid, args.Actor);
            terminal.LastStatus = "Для создания кристалла нужен образец материала в анализаторе.";
            UpdateUi(uid, terminal);
            return;
        }

        if (HasComp<ResearchArtifactComponent>(sample))
        {
            _popup.PopupEntity("В анализаторе уже готовый кристалл. Исправьте формулу и нажмите «Перепрошить кристалл».", analyzer.Value, args.Actor);
            terminal.LastStatus = "Готовый кристалл не расходуется: перепрошейте его новой формулой или загрузите свежий образец.";
            UpdateUi(uid, terminal);
            return;
        }

        var crystal = Spawn("DarkMatterCrystal", Transform(uid).Coordinates);
        var artifact = EnsureComp<ResearchArtifactComponent>(crystal);

        var name = NormalizeName(args.Name, "Кристалл закономерности");
        _artifacts.Configure(crystal, args.Graph, args.Actor, material, artifact);
        _meta.SetEntityName(crystal, name);
        artifact.FormulaName = name;

        // The sample is the physical source of the crystal. It is consumed only after the
        // formula and the sample have both passed validation.
        if (!_slots.TryEject(analyzer.Value, analyzerComp.Slot, null, out var usedSample))
        {
            _popup.PopupEntity("Не удалось извлечь образец из анализатора.", analyzer.Value, args.Actor);
            QueueDel(crystal);
            UpdateUi(uid, terminal);
            return;
        }

        QueueDel(usedSample);

        if (!_hands.TryPickupAnyHand(args.Actor, crystal))
            _popup.PopupEntity("Кристалл создан рядом с терминалом.", uid, args.Actor);

        terminal.SelectedFormulaId = string.Empty;
        terminal.LastStatus = $"Кристалл «{name}» создан из образца «{material.MaterialName}».";
        UpdateUi(uid, terminal);
    }

    private void OnReflashCrystal(EntityUid uid, ResearchTerminalComponent terminal, ResearchReflashCrystalMessage args)
    {
        if (!EffectGraphValidator.Validate(args.Graph, out var errors))
        {
            terminal.LastStatus = string.Join(" ", errors);
            UpdateUi(uid, terminal);
            return;
        }

        var analyzer = FindMachine(uid, terminal, DarkMatterMachineKind.Analyzer);
        if (analyzer == null ||
            !TryComp<DarkMatterMachineComponent>(analyzer, out var analyzerComp) ||
            analyzerComp.Slot.Item is not { } crystal ||
            !TryComp<ResearchArtifactComponent>(crystal, out var artifact) ||
            !TryComp<ResearchMaterialComponent>(crystal, out var material))
        {
            _popup.PopupEntity("Вставьте готовый кристалл в анализатор, чтобы перепрошить его.", uid, args.Actor);
            terminal.LastStatus = "Для перепрошивки нужен готовый кристалл в анализаторе.";
            UpdateUi(uid, terminal);
            return;
        }

        if (artifact.CrystalOwner is { } crystalOwner && crystalOwner != args.Actor)
        {
            _popup.PopupEntity("Этот кристалл принадлежит другому исследователю.", crystal, args.Actor);
            return;
        }

        _artifacts.Configure(crystal, args.Graph, args.Actor, material, artifact);
        var name = NormalizeName(args.Name, "Кристалл закономерности");
        _meta.SetEntityName(crystal, name);
        artifact.FormulaName = name;

        terminal.SelectedFormulaId = string.Empty;
        terminal.LastStatus = $"Кристалл «{name}» перепрошит. Образец и материал кристалла не израсходованы.";
        UpdateUi(uid, terminal);
    }

    private void OnTest(EntityUid uid, ResearchTerminalComponent terminal, ResearchTestMessage args)
    {
        if (!EffectGraphValidator.Validate(args.Graph, out var errors))
        {
            terminal.LastStatus = string.Join(" ", errors);
            UpdateUi(uid, terminal);
            return;
        }

        var testMaterial = FindMaterialSource(uid, terminal) is { } testSource &&
                           TryComp<ResearchMaterialComponent>(testSource, out var sourceMaterial)
            ? sourceMaterial
            : null;
        var testEnergy = testMaterial?.EnergyCapacity ?? 1000f;
        // The simplified terminal performs a safe virtual test: no experimental chamber or
        // physical test crystal is needed, and no world effects are applied.
        var result = _graphs.Execute(uid, args.Graph, args.Actor, testEnergy, testEnergy, applyEffects: false);
        var summary = result.Success
            ? $"Виртуальное испытание: успех. Энергия: {result.EnergyCost:0.##}/{testEnergy:0.##}."
            : $"Ошибка: {result.Error}";

        terminal.LastStatus = summary;

        if (terminal.Archive.FirstOrDefault(formula => formula.Id == terminal.SelectedFormulaId) is { } record)
        {
            record.TestCount++;
            record.LastResult = summary;
            record.Reproducible = result.Success;
            if (FindMaterialSource(uid, terminal) is { } materialSource &&
                TryComp<ResearchMaterialComponent>(materialSource, out var material))
            {
                record.MaterialName = material.MaterialName;
            }

            _archive.Upsert(record);
            _archive.Save();
        }

        UpdateUi(uid, terminal);
    }

    private EntityUid? FindMaterialSource(EntityUid terminalUid, ResearchTerminalComponent terminal)
    {
        if (FindMachine(terminalUid, terminal, DarkMatterMachineKind.Analyzer) is { } analyzer &&
            TryComp<DarkMatterMachineComponent>(analyzer, out var analyzerComp) &&
            analyzerComp.Slot.Item is { } sample &&
            HasComp<ResearchMaterialComponent>(sample))
            return sample;

        return null;
    }

    /// <summary>
    /// Keeps one random synthesis order on the machine until it is completed. The order is
    /// visible in the terminal before the player loads anything.
    /// </summary>
    private void EnsureRequirements(DarkMatterMachineComponent machine)
    {
        if (!string.IsNullOrEmpty(machine.RequiredLiquidId) &&
            !string.IsNullOrEmpty(machine.RequiredOreId) &&
            _prototype.HasIndex<DarkMatterMaterialPrototype>(machine.RequiredLiquidId) &&
            _prototype.HasIndex<DarkMatterMaterialPrototype>(machine.RequiredOreId))
            return;

        RollRequirements(machine);
    }

    private void RollRequirements(DarkMatterMachineComponent machine)
    {
        var liquids = _prototype.EnumeratePrototypes<DarkMatterMaterialPrototype>()
            .Where(material => material.Kind == DarkMatterMaterialKind.Liquid && material.ReagentIds.Count > 0)
            .ToList();
        var solids = _prototype.EnumeratePrototypes<DarkMatterMaterialPrototype>()
            .Where(material => material.Kind != DarkMatterMaterialKind.Liquid && material.MaterialIds.Count > 0)
            .ToList();

        machine.RequiredLiquidId = PickWeighted(liquids)?.ID ?? string.Empty;
        machine.RequiredOreId = PickWeighted(solids)?.ID ?? string.Empty;
    }

    private DarkMatterMaterialPrototype? PickWeighted(List<DarkMatterMaterialPrototype> materials)
    {
        if (materials.Count == 0)
            return null;

        var total = materials.Sum(material => MathF.Max(0.01f, material.Weight));
        var roll = _random.NextFloat() * total;

        foreach (var material in materials)
        {
            roll -= MathF.Max(0.01f, material.Weight);
            if (roll <= 0f)
                return material;
        }

        return materials[^1];
    }

    private static float ClampStat(float value, float min, float max)
    {
        if (!float.IsFinite(value))
            return min;

        return Math.Clamp(value, min, max);
    }

    private void ConsumeOne(EntityUid item)
    {
        if (TryComp<StackComponent>(item, out var stack) && stack.Count > 1 &&
            _stack.TryUse((item, stack), 1))
            return;

        QueueDel(item);
    }

    private EntityUid? FindMachine(EntityUid terminalUid, ResearchTerminalComponent terminal, DarkMatterMachineKind kind)
    {
        var terminalXform = Transform(terminalUid);
        var terminalPosition = _transform.GetWorldPosition(terminalXform);
        var maxDistance = MathF.Max(0f, terminal.LinkRange);
        maxDistance *= maxDistance;
        EntityUid? best = null;
        var bestDistance = maxDistance;

        var query = EntityQueryEnumerator<DarkMatterMachineComponent>();
        while (query.MoveNext(out var uid, out var machine))
        {
            if (machine.Kind != kind || uid == terminalUid)
                continue;

            var xform = Transform(uid);
            if (xform.MapID != terminalXform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(xform) - terminalPosition).LengthSquared();
            if (distance > bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best;
    }

    public void UpdateUi(EntityUid uid, ResearchTerminalComponent? terminal = null)
    {
        if (!Resolve(uid, ref terminal))
            return;

        if (!_ui.HasUi(uid, ResearchTerminalUiKey.Key))
            return;

        var analyzer = FindMachine(uid, terminal, DarkMatterMachineKind.Analyzer);
        var synthesizer = FindMachine(uid, terminal, DarkMatterMachineKind.Synthesizer);
        var hasMaterial = false;
        var analyzerHasCrystal = false;

        var materialStats = new List<MaterialStatEntry>();
        if (analyzer is { } analyzerUid &&
            TryComp<DarkMatterMachineComponent>(analyzerUid, out var analyzerComp) &&
            analyzerComp.Slot.Item is { } sample &&
            TryComp<ResearchMaterialComponent>(sample, out var material))
        {
            hasMaterial = true;
            materialStats.Add(new MaterialStatEntry("Материал", material.MaterialName));
            materialStats.Add(new MaterialStatEntry("Плотность", $"{material.Density:0.##}"));
            materialStats.Add(new MaterialStatEntry("Прочность", $"{material.Strength:0.##}"));
            materialStats.Add(new MaterialStatEntry("Твёрдость", $"{material.Hardness:0.##}"));
            materialStats.Add(new MaterialStatEntry("Теплопроводность", $"{material.ThermalConductivity:0.##}"));
            materialStats.Add(new MaterialStatEntry("Электропроводность", $"{material.ElectricalConductivity:0.##}"));
            materialStats.Add(new MaterialStatEntry("Термостойкость", $"{material.ThermalResistance:0.##}"));
            materialStats.Add(new MaterialStatEntry("Радиационная стойкость", $"{material.RadiationResistance:0.##}"));
            materialStats.Add(new MaterialStatEntry("Энергоёмкость", $"{material.EnergyCapacity:0.##}"));
            materialStats.Add(new MaterialStatEntry("Стабильность", $"{material.Stability:0.##}"));
            materialStats.Add(new MaterialStatEntry("Реактивность", $"{material.Reactivity:0.##}"));
            materialStats.Add(new MaterialStatEntry("Деградация", $"{material.Degradation:0.##}"));
            materialStats.Add(new MaterialStatEntry("Чистота", $"{material.Purity:P0}"));
            materialStats.Add(new MaterialStatEntry("Тёмная материя", $"{material.DarkMatter:0.##}"));
            materialStats.Add(new MaterialStatEntry("Радиационная активность", $"{material.RadiationActivity:0.##}"));
            materialStats.Add(new MaterialStatEntry("Эффективность воздействий", $"{material.EffectEfficiency:0.##}"));

            if (TryComp<ResearchArtifactComponent>(sample, out var crystalArtifact))
            {
                analyzerHasCrystal = true;
                materialStats.Add(new MaterialStatEntry("Формула", crystalArtifact.FormulaName));
                materialStats.Add(new MaterialStatEntry("Состояние",
                    string.IsNullOrEmpty(crystalArtifact.LastError)
                        ? crystalArtifact.Enabled ? "активен" : "ожидание"
                        : crystalArtifact.LastError));
            }
        }

        var hasLiquid = false;
        var hasOre = false;
        var hasClot = false;
        var liquidMatches = false;
        var oreMatches = false;
        var liquidName = string.Empty;
        var oreName = string.Empty;
        var clotName = string.Empty;
        var requiredLiquidName = string.Empty;
        var requiredOreName = string.Empty;
        var liquidPrototype = string.Empty;
        var orePrototype = string.Empty;
        var liquidFeed = new ResearchFeedInfo("Жидкость", string.Empty, string.Empty, "Реагент", string.Empty,
            "#FFFFFF", "Beaker", string.Empty, string.Empty, false, false);
        var oreFeed = new ResearchFeedInfo("Сырьё", string.Empty, string.Empty, "Материал", string.Empty,
            "#FFFFFF", string.Empty, string.Empty, string.Empty, false, false);

        if (synthesizer is { } synthesizerUid &&
            TryComp<DarkMatterMachineComponent>(synthesizerUid, out var synthesizerComp))
        {
            EnsureRequirements(synthesizerComp);
            DarkMatterMaterialPrototype? requiredLiquid = null;
            DarkMatterMaterialPrototype? requiredOre = null;

            if (_prototype.TryIndex(synthesizerComp.RequiredLiquidId, out requiredLiquid))
                requiredLiquidName = requiredLiquid.Name;

            if (_prototype.TryIndex(synthesizerComp.RequiredOreId, out requiredOre))
                requiredOreName = requiredOre.Name;

            if (synthesizerComp.LiquidSlot.Item is { } liquidItem)
            {
                hasLiquid = true;
                liquidName = Name(liquidItem);
                if (TryComp(liquidItem, out MetaDataComponent? liquidMeta))
                    liquidPrototype = liquidMeta.EntityPrototype?.ID ?? string.Empty;

                liquidMatches = requiredLiquid != null &&
                                _solution.TryGetFitsInDispenser(liquidItem, out _, out var solution) &&
                                solution.Contents.Any(entry =>
                                    entry.Quantity >= 10 &&
                                    requiredLiquid.ReagentIds.Contains(entry.Reagent.Prototype));
            }

            if (synthesizerComp.OreSlot.Item is { } oreItem)
            {
                hasOre = true;
                oreName = Name(oreItem);
                if (TryComp(oreItem, out MetaDataComponent? oreMeta))
                    orePrototype = oreMeta.EntityPrototype?.ID ?? string.Empty;

                oreMatches = requiredOre != null &&
                             TryComp<PhysicalCompositionComponent>(oreItem, out var composition) &&
                             requiredOre.MaterialIds.Any(id =>
                                 composition.MaterialComposition.ContainsKey(id));
            }

            if (synthesizerComp.ClotSlot.Item is { } clotItem &&
                TryComp<DarkMatterClotComponent>(clotItem, out var clotComp))
            {
                hasClot = true;
                clotName = clotComp.Clot.Name;
            }

            var liquidContentId = requiredLiquid?.ReagentIds.FirstOrDefault() ?? string.Empty;
            var oreContentId = requiredOre?.MaterialIds.FirstOrDefault() ?? string.Empty;
            var oreIconPrototype = string.Empty;
            if (!string.IsNullOrEmpty(oreContentId) &&
                _prototype.TryIndex<MaterialPrototype>(oreContentId, out var oreMaterial) &&
                oreMaterial.StackEntity is { } oreStack)
            {
                oreIconPrototype = oreStack.Id;
            }

            liquidFeed = new ResearchFeedInfo("Жидкость", requiredLiquidName, synthesizerComp.RequiredLiquidId,
                "Реагент", liquidContentId, requiredLiquid?.Color ?? "#FFFFFF", "Beaker",
                liquidName, liquidPrototype, hasLiquid, liquidMatches);
            oreFeed = new ResearchFeedInfo("Сырьё", requiredOreName, synthesizerComp.RequiredOreId,
                "Материал", oreContentId, requiredOre?.Color ?? "#FFFFFF", oreIconPrototype,
                oreName, orePrototype, hasOre, oreMatches);
        }

        var formulas = terminal.Archive
            .Select(formula => new ResearchFormulaInfo(formula.Id, formula.Name, formula.Author, formula.Created,
                formula.Graph.Nodes.Count, formula.Version, formula.TestCount, formula.LastResult, formula.Reproducible))
            .ToList();

        var state = new ResearchTerminalState(
            terminal.Draft.Clone(),
            formulas,
            materialStats,
            terminal.LastStatus,
            analyzer != null,
            synthesizer != null,
            requiredLiquidName,
            requiredOreName,
            liquidFeed,
            oreFeed,
            hasLiquid,
            hasOre,
            hasClot,
            hasMaterial,
            analyzerHasCrystal,
            liquidMatches,
            oreMatches,
            liquidName,
            oreName,
            clotName,
            terminal.Revision,
            terminal.SelectedFormulaId,
            terminal.LastSynthesis);

        _ui.SetUiState(uid, ResearchTerminalUiKey.Key, state);
    }

    private static string NormalizeName(string? value, string fallback)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return fallback;

        return value.Length <= 64 ? value : value[..64];
    }
}
