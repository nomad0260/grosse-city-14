using System;
using System.Collections.Generic;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Containment;

/// <summary>
/// Единственная центральная консоль РТМ: эмиттеры, патроны, сдерживание, мощность,
/// охлаждение и аварийная остановка. Все — один интерфейс с разными правами по режиму.
/// </summary>
public sealed partial class ContainmentConsoleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ContainmentCoreSystem _core = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentConsoleComponent, ContainmentToggleStabilizerMessage>(OnToggleProjector);
        SubscribeLocalEvent<ContainmentConsoleComponent, ContainmentSetProjectorIntensityMessage>(OnSetProjectorIntensity);
        SubscribeLocalEvent<ContainmentConsoleComponent, ReactorSetPowerMessage>(OnSetPower);
        SubscribeLocalEvent<ContainmentConsoleComponent, ReactorEmergencyStopMessage>(OnEmergencyStop);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ContainmentConsoleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextRefresh)
                continue;

            comp.NextRefresh = now + comp.RefreshInterval;

            if (comp.Core is { } currentCore && TerminatingOrDeleted(currentCore))
                comp.Core = null;

            comp.Core ??= _core.FindCore(uid, comp.LinkRange);

            if (comp.Core is { } core && TryComp<ContainmentCoreComponent>(core, out _))
            {
                // Новое живое ядро снова держит связь: помехи прекращаются.
                comp.Aftermath = false;
                UpdateUi(uid, comp);
            }
            else if (comp.Aftermath)
            {
                // Ядро погибло: отправляем «нет связи» с пометкой катастрофы,
                // иначе окно навсегда застыло бы на последней телеметрии.
                UpdateUi(uid, comp);
            }
        }
    }

    public void UpdateUi(EntityUid uid, ContainmentConsoleComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (!_ui.HasUi(uid, ContainmentConsoleUiKey.Key))
            return;

        if (comp.Core is not { } core || !TryComp<ContainmentCoreComponent>(core, out var coreComp))
        {
            _ui.SetUiState(uid, ContainmentConsoleUiKey.Key, new ContainmentConsoleState(
                false,
                ContainmentCoreState.Offline,
                comp.Mode,
                false,
                0f, 100f, 293f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                1f, 1000f, 1000f, 1000f, 0f, 0f,
                new List<ReactorProjectorInfo>(),
                comp.Aftermath));
            return;
        }

        var projectors = new List<ReactorProjectorInfo>();

        foreach (var projectorUid in _core.GetSortedStabilizers(core))
        {
            if (projectors.Count >= coreComp.MaxStabilizers)
                break;

            if (!TryComp<ContainmentStabilizerComponent>(projectorUid, out var projector))
                continue;

            var temperature = TryComp<ThermalEquipmentComponent>(projectorUid, out var thermal)
                ? thermal.Temperature
                : 293f;

            var coolantName = "—";
            var coolantCapacity = 0f;

            if (_core.TryGetEmitterCooler(projectorUid, out var coolerUid) &&
                TryComp<ReactorSupportComponent>(coolerUid, out var coolerSupport))
            {
                coolantCapacity = coolerSupport.CoolingCapacity *
                                  Math.Clamp(coolerSupport.Integrity, 0f, 1f);

                if (coolerSupport.CoolantReagent is { } reagentId &&
                    _prototype.TryIndex<ReagentPrototype>(reagentId, out var reagent))
                {
                    coolantName = reagent.LocalizedName;
                }
            }

            projectors.Add(new ReactorProjectorInfo(
                projector.Enabled,
                projector.Intensity,
                projector.Integrity,
                projector.HasCartridge,
                projector.HasCartridge ? DarkCoreCartridgeData.GetName(projector.CartridgeType) : "нет",
                projector.CartridgeResource,
                projector.CartridgeMaxResource,
                temperature,
                coolantName,
                coolantCapacity,
                DarkCoreCoolantMath.CalculateEmitterHeat(projector.HeatCoefficient, projector.Intensity)));
        }

        var radiation = TryComp<RadiationSourceComponent>(core, out var radiationComp)
            ? radiationComp.Intensity
            : 0f;

        var collapseTimeRemaining = coreComp.CompressionExcess >= coreComp.CompressionLimit &&
                                    coreComp.OverpoweredSince != TimeSpan.Zero
            ? Math.Max(0f, coreComp.CollapseDelay -
                             (float) (_timing.CurTime - coreComp.OverpoweredSince).TotalSeconds)
            : 0f;

        _ui.SetUiState(uid, ContainmentConsoleUiKey.Key, new ContainmentConsoleState(
            true,
            coreComp.State,
            comp.Mode,
            coreComp.IsRunning,
            coreComp.Stability,
            coreComp.Density,
            coreComp.Temperature,
            coreComp.Expansion,
            coreComp.Compression,
            coreComp.ContainmentIntegrity,
            coreComp.CoolingEfficiency,
            coreComp.CoolingCapacity,
            coreComp.HeatInput,
            radiation,
            coreComp.EnergyOutput,
            coreComp.EnergyLoad,
            coreComp.PowerScale,
            coreComp.MaxEnergyOutput,
            coreComp.MaxTemperature,
            coreComp.MaxRadiation,
            coreComp.CompressionThreshold,
            collapseTimeRemaining,
            projectors,
            comp.Aftermath));
    }

    private static bool CanUse(ReactorConsoleMode mode, ReactorConsoleMode required)
    {
        return mode == ReactorConsoleMode.Full || mode == required;
    }

    private EntityUid? GetProjector(EntityUid console, ContainmentConsoleComponent comp, int index)
    {
        if (comp.Core is not { } core)
            return null;

        // Ядро могло быть удалено взрывом между апдейтами консоли, а связь сбрасывается
        // только раз в RefreshInterval. Обращение к Transform удалённой сущности бросает
        // исключение прямо в обработчике сообщения и роняет сервер, поэтому устаревшую
        // ссылку отбрасываем здесь, не дожидаясь следующего Update.
        if (TerminatingOrDeleted(core) || !TryComp<ContainmentCoreComponent>(core, out _))
        {
            comp.Core = null;
            return null;
        }

        var projectors = _core.GetSortedStabilizers(core);

        if (index < 0 || index >= projectors.Count)
            return null;

        return projectors[index];
    }

    private void OnToggleProjector(EntityUid uid, ContainmentConsoleComponent comp, ContainmentToggleStabilizerMessage msg)
    {
        if (!ContainmentConsoleUiKey.Key.Equals(msg.UiKey))
            return;

        if (!CanUse(comp.Mode, ReactorConsoleMode.Manual))
            return;

        if (GetProjector(uid, comp, msg.Index) is not { } projectorUid)
            return;

        if (!TryComp<ContainmentStabilizerComponent>(projectorUid, out var projector))
            return;

        projector.Enabled = !projector.Enabled;
        Dirty(projectorUid, projector);

        UpdateUi(uid, comp);
    }

    private void OnSetProjectorIntensity(EntityUid uid, ContainmentConsoleComponent comp, ContainmentSetProjectorIntensityMessage msg)
    {
        if (!ContainmentConsoleUiKey.Key.Equals(msg.UiKey))
            return;

        if (!CanUse(comp.Mode, ReactorConsoleMode.Manual))
            return;

        if (GetProjector(uid, comp, msg.Index) is not { } projectorUid)
            return;

        if (!TryComp<ContainmentStabilizerComponent>(projectorUid, out var projector))
            return;

        projector.Intensity = Math.Clamp(msg.Intensity, 0f, 200f);
        Dirty(projectorUid, projector);

        UpdateUi(uid, comp);
    }

    private void OnSetPower(EntityUid uid, ContainmentConsoleComponent comp, ReactorSetPowerMessage msg)
    {
        if (!ContainmentConsoleUiKey.Key.Equals(msg.UiKey))
            return;

        if (!CanUse(comp.Mode, ReactorConsoleMode.Power))
            return;

        if (comp.Core is not { } core ||
            TerminatingOrDeleted(core) ||
            !TryComp<ContainmentCoreComponent>(core, out var coreComp))
            return;

        coreComp.PowerScale = Math.Clamp(msg.Power, 0f, coreComp.MaxPowerScale);
        Dirty(core, coreComp);

        UpdateUi(uid, comp);
    }

    /// <summary>
    /// Сброс стержней — термоудар по эмиттерам. После аварийной остановки
    /// контур приходится ремонтировать, поэтому пользоваться ей «на всякий случай» невыгодно.
    /// </summary>
    private void ApplyScramDamage(EntityUid core, float penalty)
    {
        if (penalty <= 0f)
            return;

        foreach (var emitterUid in _core.GetSortedStabilizers(core))
        {
            if (!TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
                continue;

            emitter.Integrity = Math.Clamp(emitter.Integrity - penalty, 0f, 1f);
            Dirty(emitterUid, emitter);
        }
    }

    private void OnEmergencyStop(EntityUid uid, ContainmentConsoleComponent comp, ReactorEmergencyStopMessage msg)
    {
        if (!ContainmentConsoleUiKey.Key.Equals(msg.UiKey))
            return;

        if (comp.Mode != ReactorConsoleMode.Full)
            return;

        if (comp.Core is not { } core ||
            TerminatingOrDeleted(core) ||
            !TryComp<ContainmentCoreComponent>(core, out var coreComp))
            return;

        // Grosse: true — запуск, false — остановка.
        if (msg.Restart)
        {
            // Пуск — не кнопка, а холодный пуск: сборка должна быть собрана,
            // поле выставлено на номинал, ядро остыло, а стержни вернулись.
            if (!_core.TryGetStartBlocker(core, coreComp, out var blocker))
            {
                if (msg.Actor.Valid)
                    _popup.PopupEntity(blocker, uid, msg.Actor, PopupType.MediumCaution);
                else
                    _popup.PopupEntity(blocker, uid, PopupType.MediumCaution);

                UpdateUi(uid, comp);
                return;
            }

            coreComp.DestabilizedSince = TimeSpan.Zero;
            coreComp.OverpoweredSince = TimeSpan.Zero;

            // Плотность — единственное состояние сжатия, которое переживает остановку.
            // Без сброса повторный пуск начинался с уже «сжатого» ядра и сразу звал тревогу.
            coreComp.Density = 100f;
            coreComp.StartupTimer = 0f;
            coreComp.StartConditionsEnforced = true;
            coreComp.IsRunning = true;
        }
        else
        {
            if (!coreComp.IsRunning)
                return;

            // Стержни падают сразу, но это не бесплатно: термоудар по эмиттерам
            // и пауза на возврат стержней не дают гасить аварию по щелчку.
            coreComp.IsRunning = false;
            coreComp.StartConditionsEnforced = false;
            coreComp.ScrammedAt = _timing.CurTime;
            coreComp.EnergyOutput = 0f;
            ApplyScramDamage(core, coreComp.ScramIntegrityPenalty);
        }

        Dirty(core, coreComp);

        UpdateUi(uid, comp);
    }
}
