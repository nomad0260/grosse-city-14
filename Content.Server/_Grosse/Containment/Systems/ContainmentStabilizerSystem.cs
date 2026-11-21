using System;
using System.Collections.Generic;
using Content.Server.Beam;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Beam.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Systems;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Containment;

/// <summary>
/// Привязывает эмиттеры к ближайшему ядру, держит луч и обслуживает патроны.
/// </summary>
public sealed partial class ContainmentStabilizerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BeamSystem _beam = default!;
    [Dependency] private ContainmentCoreSystem _core = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRadiationSystem _radiation = default!;

    private const string WeldingQuality = "Welding";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentStabilizerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ContainmentStabilizerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ContainmentStabilizerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ContainmentStabilizerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ContainmentStabilizerComponent, StabilizerRepairDoAfterEvent>(OnRepairDoAfter);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ContainmentStabilizerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextRelink)
                continue;

            comp.NextRelink = now + TimeSpan.FromSeconds(0.5f);

            if (comp.Core is { } currentCore && TerminatingOrDeleted(currentCore))
                comp.Core = null;

            comp.Core ??= _core.FindCore(uid, comp.LinkRange);

            if (comp.HasCartridge && comp.Enabled && comp.CartridgeResource > 0f)
            {
                // Интенсивность, коэффициент износа патрона и состояние корпуса.
                var equipmentFactor = Math.Clamp(comp.Integrity, 0.05f, 1f);
                if (TryComp<ThermalEquipmentComponent>(uid, out var thermal))
                    equipmentFactor *= Math.Clamp(thermal.Efficiency, 0.05f, 1f);

                // Форсаж сверх 100% вырабатывает патрон быстрее линейного: на 200%
                // расход двое больше номинального.
                var intensityFactor = Math.Clamp(comp.Intensity, 0f, 200f) / 100f;
                var overdrive = Math.Max(0f, intensityFactor - 1f);

                var drain = intensityFactor *
                            comp.CartridgeWearRate *
                            equipmentFactor *
                            (1f + overdrive) *
                            0.5f;
                comp.CartridgeResource = Math.Max(0f, comp.CartridgeResource - drain);

                if (comp.CartridgeResource <= 0.01f)
                {
                    comp.CartridgeResource = 0f;
                    comp.HasCartridge = false;
                    comp.Enabled = false;

                    // Отработанный патрон не удаляется: он остаётся сильно радиоактивным
                    // предметом, который нужно утилизировать или переработать. Сохраняем тип.
                    var spent = Spawn("DarkCoreCartridgeSpent", Transform(uid).Coordinates);
                    if (TryComp<DarkCoreCartridgeComponent>(spent, out var spentCartridge))
                        spentCartridge.Kind = comp.CartridgeType;

                    // Отработанная Дигамма опаснее обычного пустого патрона.
                    if (HasComp<RadiationSourceComponent>(spent))
                        _radiation.SetIntensity(spent, comp.CartridgeType == DarkCoreCartridgeType.Digamma ? 800f : 250f);

                    _popup.PopupEntity("Патрон выработан. Эмиттер остановлен, оставлен радиоактивный патрон.", uid, PopupType.MediumCaution);
                    Dirty(uid, comp);
                }
            }

            var shouldBeam = comp.Enabled &&
                             comp.HasCartridge &&
                             comp.CartridgeResource > 0.01f &&
                             comp.Intensity > 0.01f &&
                             comp.Integrity > 0.05f &&
                             comp.Core is { } core &&
                             !TerminatingOrDeleted(core);

            var hasLiveBeam = HasLiveBeam(comp);
            if (shouldBeam && !hasLiveBeam)
            {
                ClearBeams(comp);
                CreateBeam(uid, comp);
            }
            else if (!shouldBeam && hasLiveBeam)
            {
                ClearBeams(comp);
            }
        }
    }

    private bool HasLiveBeam(ContainmentStabilizerComponent comp)
    {
        comp.Beams.RemoveAll(beam => !Exists(beam));
        comp.BeamControllers.RemoveAll(controller => !Exists(controller));
        return comp.Beams.Count > 0;
    }

    private void CreateBeam(EntityUid uid, ContainmentStabilizerComponent comp)
    {
        if (comp.Core is not { } core || TerminatingOrDeleted(core))
            return;

        var before = new HashSet<EntityUid>();
        var existing = EntityQueryEnumerator<BeamComponent>();

        while (existing.MoveNext(out var beamUid, out _))
        {
            before.Add(beamUid);
        }

        _beam.TryCreateBeam(uid, core, comp.BeamPrototype);

        var created = EntityQueryEnumerator<BeamComponent>();

        while (created.MoveNext(out var beamUid, out var beamComp))
        {
            if (before.Contains(beamUid))
                continue;

            comp.Beams.Add(beamUid);

            if (beamComp.VirtualBeamController is { } controller && !comp.BeamControllers.Contains(controller))
                comp.BeamControllers.Add(controller);
        }
    }

    private void ClearBeams(ContainmentStabilizerComponent comp)
    {
        foreach (var beam in comp.Beams)
        {
            if (Exists(beam))
                QueueDel(beam);
        }

        foreach (var controller in comp.BeamControllers)
        {
            if (Exists(controller) && HasComp<BeamComponent>(controller))
                QueueDel(controller);
        }

        comp.Beams.Clear();
        comp.BeamControllers.Clear();
    }

    private void OnShutdown(EntityUid uid, ContainmentStabilizerComponent comp, ComponentShutdown args)
    {
        ClearBeams(comp);
    }

    private void OnInteractUsing(Entity<ContainmentStabilizerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<DarkCoreCartridgeComponent>(args.Used, out var cartridge))
        {
            if (ent.Comp.HasCartridge)
            {
                _popup.PopupEntity("В эмиттере уже установлен патрон. Сначала извлеките его.", ent.Owner, args.User);
                args.Handled = true;
                return;
            }

            if (cartridge.Resource <= 0.01f)
            {
                _popup.PopupEntity("Отработанный патрон не выдаёт луч. Утилизируйте его.", ent.Owner, args.User);
                args.Handled = true;
                return;
            }

            // Сбрасываем устаревшие данные о луче перед запуском эмиттера с новым патроном.
            ClearBeams(ent.Comp);
            ent.Comp.NextRelink = TimeSpan.Zero;
            LoadCartridge(ent.Comp, cartridge);
            QueueDel(args.Used);
            args.Handled = true;
            Dirty(ent.Owner, ent.Comp);
            _popup.PopupEntity($"Установлен {DarkCoreCartridgeData.GetName(cartridge.Kind)}-патрон", ent.Owner, args.User);
            return;
        }

        if (ent.Comp.Integrity >= 0.999f || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            2f,
            WeldingQuality,
            new StabilizerRepairDoAfterEvent(),
            fuel: 5f);
    }

    private void OnInteractHand(Entity<ContainmentStabilizerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.HasCartridge)
            return;

        var cartridge = Spawn(DarkCoreCartridgeData.GetEntityId(ent.Comp.CartridgeType), Transform(ent).Coordinates);

        if (TryComp<DarkCoreCartridgeComponent>(cartridge, out var item))
        {
            item.Kind = ent.Comp.CartridgeType;
            item.Resource = ent.Comp.CartridgeResource;
            item.MaxResource = ent.Comp.CartridgeMaxResource;
            item.Stabilization = ent.Comp.StabilizationCoefficient;
            item.Compression = ent.Comp.CompressionCoefficient;
            item.Energy = ent.Comp.EnergyCoefficient;
            item.Heat = ent.Comp.HeatCoefficient;
            item.WearRate = ent.Comp.CartridgeWearRate;
        }

        ClearBeams(ent.Comp);
        ent.Comp.HasCartridge = false;
        ent.Comp.Enabled = false;
        ent.Comp.CartridgeResource = 0f;
        ent.Comp.NextRelink = TimeSpan.Zero;
        ent.Comp.CartridgeMaxResource = 0f;
        args.Handled = true;
        Dirty(ent.Owner, ent.Comp);
        _popup.PopupEntity("Патрон извлечён, эмиттер остановлен.", ent.Owner, args.User);
    }

    private static void LoadCartridge(ContainmentStabilizerComponent emitter, DarkCoreCartridgeComponent cartridge)
    {
        emitter.HasCartridge = true;
        // Замена выработанного патрона должна возобновлять луч без отдельного переключения с консоли.
        emitter.Enabled = true;
        emitter.CartridgeType = cartridge.Kind;
        emitter.CartridgeResource = cartridge.Resource;
        emitter.CartridgeMaxResource = cartridge.MaxResource;
        emitter.StabilizationCoefficient = cartridge.Stabilization;
        emitter.CompressionCoefficient = cartridge.Compression;
        emitter.EnergyCoefficient = cartridge.Energy;
        emitter.HeatCoefficient = cartridge.Heat;
        emitter.CartridgeWearRate = cartridge.WearRate;
    }

    private void OnRepairDoAfter(Entity<ContainmentStabilizerComponent> ent, ref StabilizerRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        ent.Comp.Integrity = Math.Clamp(ent.Comp.Integrity + 0.25f, 0f, 1f);
        Dirty(ent.Owner, ent.Comp);

        _popup.PopupEntity($"Эмиттер отремонтирован: {ent.Comp.Integrity:P0} исправности", ent.Owner, args.User);
    }

    private void OnExamined(Entity<ContainmentStabilizerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup($"Эмиттер: интенсивность {ent.Comp.Intensity:0}%, исправность {ent.Comp.Integrity * 100f:0}%");

        if (!ent.Comp.HasCartridge)
        {
            args.PushMarkup("[color=red]Патрон не установлен.[/color]");
        }
        else
        {
            args.PushMarkup(
                $"Патрон: [color=lightblue]{DarkCoreCartridgeData.GetName(ent.Comp.CartridgeType)}[/color], " +
                $"ресурс {ent.Comp.CartridgeResource:0}/{ent.Comp.CartridgeMaxResource:0}");
        }

        if (ent.Comp.Core is not { } core || TerminatingOrDeleted(core))
        {
            args.PushMarkup("[color=red]Связь с ядром: нет[/color] (ядро не найдено в радиусе привязки)");
            return;
        }

        args.PushMarkup(!ent.Comp.Enabled
            ? "[color=orange]Луч выключен[/color]"
            : "[color=cyan]Луч активен[/color]");
    }
}
