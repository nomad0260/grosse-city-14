using System;
using System.Collections.Generic;
using Content.Server._Grosse.Cooling;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Singularity.EntitySystems;
using Content.Shared._Grosse.Cooling;
using Content.Shared._Grosse.Containment;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Systems;
using Content.Shared.Power;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Singularity.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Containment;

/// <summary>
/// Контроллер РТМ: эмиттеры и патроны, стабильность, плотность, расширение,
/// температура, радиация, сдерживание, энергия и катастрофы.
/// </summary>
public sealed partial class ContainmentCoreSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedRadiationSystem _radiation = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private ThermalEquipmentSystem _thermal = default!;
    [Dependency] private SingularitySystem _singularity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private const string WeldingQuality = "Welding";

    /// <summary>
    /// С какой интенсивностью эмиттеры допускаются к холодному пуску.
    /// Выше — форсаж: сборка ещё не проверена, стартовать в нём нельзя.
    /// </summary>
    private const float SafeStartIntensity = 150f;

    /// <summary>Последнее распределение: эмиттер → обслуживающий его охладитель.</summary>
    [ViewVariables]
    private readonly Dictionary<EntityUid, EntityUid> _emitterCoolers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentCoreComponent, ExaminedEvent>(OnCoreExamined);
        SubscribeLocalEvent<ReactorSupportComponent, ExaminedEvent>(OnSupportExamined);
        SubscribeLocalEvent<ReactorDistributorComponent, ExaminedEvent>(OnDistributorExamined);
        SubscribeLocalEvent<ReactorSupportComponent, InteractUsingEvent>(OnSupportInteract);
        SubscribeLocalEvent<ReactorDistributorComponent, InteractUsingEvent>(OnDistributorInteract);
        SubscribeLocalEvent<ReactorSupportComponent, ReactorSupportRepairDoAfterEvent>(OnSupportRepaired);
        SubscribeLocalEvent<ReactorDistributorComponent, ReactorSupportRepairDoAfterEvent>(OnDistributorRepaired);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ContainmentCoreComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;
            UpdateCore(uid, comp, now);
        }
    }

    private void UpdateCore(EntityUid uid, ContainmentCoreComponent comp, TimeSpan now)
    {
        var dt = (float) comp.UpdateInterval.TotalSeconds;
        var emitters = GetSortedStabilizers(uid);
        var workingEmitters = GetWorkingEmitters(emitters, comp.MaxStabilizers);

        // Охладители обслуживают только работающие эмиттеры: вместе с отводом тепла
        // собираем эффекты жидкостей именно тех блоков, которые реально привязаны
        // к такому эмиттеру на этой карте.
        var allocatedCooling = AllocateEmitterCooling(workingEmitters, out var coolantEffects);
        var (loop, screen, supportStabilization) = GetSupport(uid);

        var totalStabilization = 0f;
        var totalCompression = 0f;
        var totalEnergy = 0f;
        var totalHeat = 0f;
        // Перегрев снижает эффективность лучей, но не отменяет выделение тепла.
        var temperatureEfficiency = Math.Clamp(
            1f - Math.Max(0f, comp.Temperature - 500f - coolantEffects.TemperatureTolerance) / 500f * 0.8f,
            0.05f,
            1f);
        var activeEmitters = 0;
        var integritySum = 0f;
        var emitterCount = 0;
        var hottestEmitter = comp.AmbientTemperature;
        var hasEmitterTemperature = false;
        var alpha = 0;
        var beta = 0;
        var gamma = 0;
        var digamma = 0;

        // Мощность ядра выше 100% — форсирование: выработка выше, но эмиттеры
        // греются сильнее, изнашиваются быстрее, растёт фон, а ядро держится хуже.
        var powerOverdrive = Math.Max(0f, comp.PowerScale - 1f);

        foreach (var emitterUid in emitters)
        {
            if (emitterCount >= comp.MaxStabilizers)
                break;

            if (!TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
                continue;

            emitterCount++;
            emitter.Integrity = Math.Clamp(emitter.Integrity, 0f, 1f);
            integritySum += emitter.Integrity;

            var working = IsEmitterWorking(emitter);

            // Интенсивность в игровых процентах: формулы используют I = 0…200.
            // Всё выше 100% — форсаж: больше выработки, но резко больше тепла и износа.
            var intensity = Math.Clamp(emitter.Intensity, 0f, 200f);
            var intensityFactor = intensity / 100f;
            var overdrive = Math.Max(0f, intensityFactor - 1f);
            var equipmentEfficiency = emitter.Integrity *
                                      (TryComp<ThermalEquipmentComponent>(emitterUid, out _)
                                          ? _thermal.GetEfficiency(emitterUid)
                                          : 1f);
            var efficiency = equipmentEfficiency * temperatureEfficiency;

            if (TryComp<ThermalEquipmentComponent>(emitterUid, out var thermal))
            {
                thermal.Load = working ? 1f : 0f;
                // Тепло считает общая формула: она учитывает форсаж свыше 100%,
                // иначе перегрузка давала бы выработку без роста нагрева.
                thermal.HeatGeneration = working
                    ? DarkCoreCoolantMath.CalculateEmitterHeat(emitter.HeatCoefficient, intensity) *
                      (1f - Math.Clamp(coolantEffects.HeatGenerationReduction, 0f, 1f)) *
                      (1f + powerOverdrive * comp.PowerHeatFactor)
                    : 0f;
                thermal.HeatRemoved = 0f;

                if (working)
                {
                    hottestEmitter = Math.Max(hottestEmitter, thermal.Temperature);
                    hasEmitterTemperature = true;
                }
            }

            if (working)
            {
                activeEmitters++;
                totalStabilization += intensity * emitter.StabilizationCoefficient * efficiency;
                totalCompression += intensity * emitter.CompressionCoefficient * efficiency;
                totalEnergy += intensity * emitter.EnergyCoefficient * efficiency;
                // Тепло берём у самого оборудования: в нём уже учтены базовая составляющая
                // и снижение тепла от радиогеля. Раньше здесь терялись оба слагаемых.
                totalHeat += thermal != null
                    ? thermal.HeatGeneration
                    : DarkCoreCoolantMath.CalculateEmitterHeat(emitter.HeatCoefficient, intensityFactor * 100f);

                switch (emitter.CartridgeType)
                {
                    case DarkCoreCartridgeType.Alpha: alpha++; break;
                    case DarkCoreCartridgeType.Beta: beta++; break;
                    case DarkCoreCartridgeType.Gamma: gamma++; break;
                    case DarkCoreCartridgeType.Digamma: digamma++; break;
                }
            }

            // Износ корпуса: перекачка и тепло ускоряют разрушение; хладагент может его замедлить.
            emitter.Integrity = Math.Clamp(
                emitter.Integrity - dt *
                (0.00002f + intensityFactor * 0.00035f * (1f + emitter.HeatCoefficient * 0.1f)) *
                (1f + overdrive * 1.5f) *
                (1f + powerOverdrive * comp.PowerWearFactor) *
                coolantEffects.GetWearFactor(),
                0f,
                1f);
        }

        ApplySynergy(ref totalStabilization, ref totalCompression, ref totalEnergy, ref totalHeat,
            alpha, beta, gamma, digamma);

        // Эффекты залитых в охладители жидкостей. Одинаковые эффекты не складываются.
        totalStabilization *= 1f + coolantEffects.StabilizationBonus;
        totalEnergy *= 1f + coolantEffects.EnergyBonus;

        var coolingCapacity = 0f;

        foreach (var (emitterUid, capacity) in allocatedCooling)
        {
            coolingCapacity += capacity;

            if (!TryComp<ThermalEquipmentComponent>(emitterUid, out var thermal))
                continue;

            thermal.HeatRemoved = Math.Min(
                thermal.HeatGeneration * Math.Clamp(thermal.Load, 0f, 1f),
                capacity);
        }

        comp.CoolingCapacity = coolingCapacity;

        // Зажимы — физическая система удержания, поэтому мощность ядра не масштабирует их вклад.
        totalStabilization += supportStabilization;

        comp.HeatInput = totalHeat;
        comp.CoolingEfficiency = totalHeat > 0.01f
            ? Math.Clamp(coolingCapacity / totalHeat, 0f, 1f)
            : 1f;
        comp.Compression = totalCompression;
        comp.CompressionExcess = Math.Max(0f, totalCompression - comp.CompressionThreshold);

        var initialized = comp.StartupTimer >= comp.StartupDelay;

        if (comp.IsRunning)
            comp.StartupTimer += dt;

        // Разгон после пуска с консоли держится только на собранном контуре:
        // если эмиттер потерял патрон, охлаждение или поле ушло с номинала,
        // стержни падают сами. Секунда форы нужна, чтобы назначение
        // охладителей успело произойти в первом же апдейте.
        if (comp.StartConditionsEnforced)
        {
            if (initialized)
            {
                comp.StartConditionsEnforced = false;
            }
            else if (comp.StartupTimer > 1f)
            {
                var cooledEmitters = 0;

                foreach (var (_, capacity) in allocatedCooling)
                {
                    if (capacity > 0.01f)
                        cooledEmitters++;
                }

                if (activeEmitters == 0 || cooledEmitters < activeEmitters)
                {
                    AbortStart(uid, comp);
                    return;
                }
            }
        }

        _appearance.SetData(uid, PowerDeviceVisuals.Powered, comp.IsRunning);
        _light.SetEnabled(uid, comp.IsRunning);

        var load = GetLoad(uid, comp);
        comp.EnergyLoad = load;

        if (!comp.IsRunning)
        {
            comp.Stability = Approach(comp.Stability, 0f, dt * comp.StabilityChangeRate);
            comp.Compression = 0f;
            comp.CompressionExcess = 0f;
            comp.Density = Approach(comp.Density, 100f, dt * comp.DensityRecoveryRate);
            comp.EnergyOutput = 0f;
            comp.OverpoweredSince = TimeSpan.Zero;

            // Сброс стержней гасит реакцию, но не остужает ядро. Остывание —
            // медленное и зависит от живого контура: с целыми охладителями
            // ядро остывает вдвое быстрее.
            var coolingBonus = Math.Clamp(
                GetScramCooling(uid, comp) / Math.Max(1f, comp.ScramCoolingReference),
                0f,
                1f);

            comp.Temperature = Approach(comp.Temperature, comp.AmbientTemperature,
                dt * comp.ScramCoolingRate * (1f + coolingBonus));

            // Пока ядро горячее, оно остаётся раздутым: авария сама не заканчивается.
            var expansionRecovery = Math.Clamp(
                (comp.ExpansionHoldTemperature - comp.Temperature) /
                Math.Max(1f, comp.ExpansionHoldTemperature - comp.ColdStartTemperature),
                0f,
                1f);

            comp.Expansion = Math.Clamp(
                Approach(comp.Expansion, 0f, dt * comp.ExpansionRecoveryRate * expansionRecovery),
                0f,
                200f);

            // Сдерживание восстанавливается только вместе с остыванием и спадом расширения.
            var stoppedContainmentTarget = Math.Clamp(100f - comp.Expansion * 0.55f, 0f, 100f);
            comp.ContainmentIntegrity = Approach(comp.ContainmentIntegrity, stoppedContainmentTarget,
                dt * comp.ContainmentChangeRate);

            // Раздутое ядро фонит и без лучей.
            comp.Radiation = Math.Clamp(comp.Expansion * 4f, 0f, comp.MaxRadiation);

            if (TryComp<RadiationSourceComponent>(uid, out _))
                _radiation.SetIntensity(uid, comp.Radiation);

            // Остановленное ядро продолжает распадаться, пока его не остудили.
            comp.State = comp.Expansion >= 100f
                ? ContainmentCoreState.Expanding
                : comp.ContainmentIntegrity <= 1f
                    ? ContainmentCoreState.ContainmentFailure
                    : ContainmentCoreState.Offline;

            if (TryHandleMeltdown(uid, comp, now))
                return;

            ApplyLight(uid, comp);
            Dirty(uid, comp);
            return;
        }

        var stabilityTarget = 0f;

        if (initialized)
        {
            stabilityTarget = Math.Clamp(
                totalStabilization / Math.Max(1f, comp.StabilizationThreshold) * 100f,
                0f,
                100f);

            stabilityTarget *= 0.5f + 0.5f * Math.Clamp(comp.ContainmentIntegrity / 100f, 0f, 1f);

            // Форсирование мощности расшатывает ядро: нужен запас стабилизации.
            stabilityTarget *= Math.Clamp(1f - powerOverdrive * comp.PowerStabilityPenalty, 0.5f, 1f);

            if (load > comp.EnergyOutput)
                stabilityTarget *= 0.9f;
        }

        comp.Stability = Approach(comp.Stability, stabilityTarget, dt * comp.StabilityChangeRate);

        if (initialized)
        {
            // Допуск к порогу: сборка из четырёх альфа-патронов на 50% даёт ровно
            // порог, и сотая доля износа сразу запускала расширение при «100%»
            // на консоли. Внутри допуска ядро держится, расширение начинается ниже него.
            var threshold = Math.Max(1f, comp.StabilizationThreshold);
            var reserve = totalStabilization - threshold * (1f - comp.ExpansionTolerance);

            if (reserve < 0f)
            {
                // Нелинейность: чем сильнее расширение, тем быстрее оно ускоряется.
                comp.Expansion += -reserve / threshold * comp.ExpansionRate *
                                  (1f + comp.Expansion / 100f) * dt;
            }
            else
            {
                comp.Expansion -= reserve / threshold * comp.ExpansionRecoveryRate * dt;
            }
        }

        comp.Expansion = Math.Clamp(comp.Expansion, 0f, 200f);

        var densityPenalty = Math.Max(0f, comp.Density - 100f) * 0.15f;
        var containmentTarget = Math.Clamp(
            100f - comp.Expansion * 0.55f - densityPenalty - powerOverdrive * comp.PowerContainmentPenalty + loop,
            0f,
            100f);

        comp.ContainmentIntegrity = Approach(
            comp.ContainmentIntegrity,
            containmentTarget,
            dt * comp.ContainmentChangeRate);

        if (initialized && comp.CompressionExcess > 0.01f)
        {
            comp.Density = Math.Clamp(
                comp.Density + comp.CompressionExcess * comp.DensityGrowthRate * dt,
                0f,
                200f);
        }
        else
        {
            comp.Density = Approach(comp.Density, 100f, dt * comp.DensityRecoveryRate);
        }

        // Температура ядра — температура самого горячего работающего эмиттера.
        // Охладители влияют на неё только через температуру эмиттеров.
        comp.Temperature = hasEmitterTemperature
            ? Math.Clamp(hottestEmitter, 1f, comp.MaxTemperature)
            : Approach(comp.Temperature, comp.AmbientTemperature, dt * 40f);

        if (comp.Temperature >= 900f)
        {
            // Критическая неисправность: сдерживание начинает разрушаться само по себе.
            comp.ContainmentIntegrity = Math.Clamp(
                comp.ContainmentIntegrity - dt * 2f * ((comp.Temperature - 900f) / 100f + 1f),
                0f,
                100f);
        }

        if (comp.Temperature >= 700f)
        {
            var damage = (comp.Temperature - 700f) / 300f;

            foreach (var emitterUid in emitters)
            {
                if (TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
                {
                    emitter.Integrity = Math.Clamp(
                        emitter.Integrity - dt * 0.002f * damage,
                        0f,
                        1f);
                }
            }
        }

        comp.Radiation = Math.Clamp(
            (comp.Expansion * 4f + totalEnergy * 0.25f + comp.Compression * 0.5f) *
            (1f + powerOverdrive * comp.PowerRadiationFactor) *
            (1f - Math.Clamp(screen, 0f, 1f)),
            0f,
            comp.MaxRadiation);

        if (TryComp<RadiationSourceComponent>(uid, out _))
            _radiation.SetIntensity(uid, comp.Radiation);

        comp.EnergyOutput = Math.Clamp(
            totalEnergy *
            comp.PowerScale *
            Math.Clamp(comp.Stability / 100f, 0f, 1f) *
            Math.Clamp(comp.ContainmentIntegrity / 100f, 0f, 1f) *
            temperatureEfficiency,
            0f,
            comp.MaxEnergyOutput);

        var state = ContainmentCoreState.Offline;
        var compressing = false;

        if (comp.IsRunning)
        {
            // Сжатие считается тревожным только когда уже идёт отсчёт до коллапса
            // и плотность ядра действительно изменилась. Простое превышение порога
            // (например, от двух гамма-патронов) — это предупреждение, а не сирена.
            // До окончания пуска ядро ещё не вышло на режим: избыток сжатия в это
            // время не считается угрозой, иначе тревога включалась сразу после запуска.
            compressing = initialized &&
                          comp.CompressionExcess >= comp.CompressionLimit &&
                          comp.Density > comp.CompressionStateDensity;

            if (!initialized)
                state = ContainmentCoreState.Initialization;
            else if (comp.ContainmentIntegrity <= 1f)
                state = ContainmentCoreState.ContainmentFailure;
            else if (comp.Expansion >= 100f)
                state = ContainmentCoreState.Expanding;
            else if (compressing)
                state = ContainmentCoreState.Compressing;
            else if (comp.EnergyOutput < comp.EnergyLoad)
                state = ContainmentCoreState.Overload;
            else if (comp.Stability < 60f)
                state = ContainmentCoreState.Unstable;
            else
                state = ContainmentCoreState.Nominal;
        }

        if (state != comp.State)
        {
            comp.State = state;
            Dirty(uid, comp);
        }

        // Коллапс начинается только при том же опасном сжатии, которое
        // показывает состояние Compressing. Иначе таймер запускался бы от
        // безопасного превышения порога и срабатывал бы сразу после пересечения
        // CompressionLimit, а без роста плотности ядро исчезало бы без тревоги.
        if (compressing)
        {
            comp.OverpoweredSince = comp.OverpoweredSince == TimeSpan.Zero
                ? now
                : comp.OverpoweredSince;

            if (now - comp.OverpoweredSince >= TimeSpan.FromSeconds(comp.CollapseDelay))
            {
                Collapse(uid, comp);
                return;
            }
        }
        else
        {
            comp.OverpoweredSince = TimeSpan.Zero;
        }

        if (TryHandleMeltdown(uid, comp, now))
            return;

        ApplyLight(uid, comp);
        Dirty(uid, comp);
    }

    private static void ApplySynergy(
        ref float stabilization,
        ref float compression,
        ref float energy,
        ref float heat,
        int alpha,
        int beta,
        int gamma,
        int digamma)
    {
        if (alpha > 0 && gamma > 0)
        {
            stabilization *= 1.15f;
            compression *= 0.9f;
        }

        if (beta > 0 && digamma > 0)
        {
            energy *= 1.2f;
            heat *= 1.25f;
        }

        if (alpha > 0 && beta > 0)
        {
            stabilization *= 1.05f;
            energy *= 1.1f;
        }

        if (gamma > 0 && digamma > 0)
        {
            // Взаимные помехи: дигамма «продавливает» гамма-стабилизацию.
            stabilization *= 0.9f;
            compression *= 1.15f;
        }

        var distinct = 0;
        distinct += alpha > 0 ? 1 : 0;
        distinct += beta > 0 ? 1 : 0;
        distinct += gamma > 0 ? 1 : 0;
        distinct += digamma > 0 ? 1 : 0;

        if (distinct >= 3)
        {
            stabilization *= 0.95f;
            energy *= 1.1f;
            heat *= 1.1f;
        }
    }

    private void ApplyLight(EntityUid uid, ContainmentCoreComponent comp)
    {
        _light.SetColor(uid, GetLightColor(comp.State));
        _light.SetRadius(uid, comp.State switch
        {
            ContainmentCoreState.Initialization => 10f,
            ContainmentCoreState.Nominal => 14f,
            ContainmentCoreState.Overload or ContainmentCoreState.Unstable => 17f,
            ContainmentCoreState.Expanding => 22f,
            ContainmentCoreState.Compressing => 19f,
            ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown => 24f,
            _ => 14f,
        });
    }

    private void OnCoreExamined(Entity<ContainmentCoreComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup("[color=lightblue]Задача:[/color] удерживать реакцию в штатном режиме.");
        args.PushMarkup("Пуск и остановка — с центральной консоли РТМ.");
    }

    private void OnSupportExamined(Entity<ReactorSupportComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var task = ent.Comp.Kind switch
        {
            ReactorSupportKind.ContainmentLoop => "держать целостность камеры",
            ReactorSupportKind.RadiationScreen => "снижать радиацию от ядра",
            ReactorSupportKind.CoolingBlock => "отводить тепло от реактора",
            _ => "обслуживать реактор",
        };

        args.PushMarkup($"[color=lightblue]Задача:[/color] {task}.");
        args.PushMarkup($"Исправность: [color={IntegrityColor(ent.Comp.Integrity)}]{ent.Comp.Integrity:P0}[/color]. " +
                        "Ремонт — сварочным аппаратом.");

        if (ent.Comp.Kind == ReactorSupportKind.CoolingBlock)
        {
            args.PushMarkup($"Мощность охлаждения: {ent.Comp.CoolingCapacity * ent.Comp.Integrity:0} ед. тепла/с. Обслуживает ближайший эмиттер.");
        }
        else if (ent.Comp.Kind == ReactorSupportKind.ContainmentLoop)
        {
            args.PushMarkup($"Бонус: +{ent.Comp.StabilizationBonus * ent.Comp.Integrity:0} стабилизации, " +
                            $"+{ent.Comp.ContainmentBonus * ent.Comp.Integrity:0} сдерживания. " +
                            "Зажимы должны стоять прямо на ядре.");
        }
    }

    private void OnDistributorExamined(Entity<ReactorDistributorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup("[color=lightblue]Задача:[/color] передавать выработку реактора в сеть.");
        args.PushMarkup($"Нагрузка: {ent.Comp.Load:0} кВт. " +
                        $"Исправность: [color={IntegrityColor(ent.Comp.Integrity)}]{ent.Comp.Integrity:P0}[/color]. " +
                        "Ремонт — сварочным аппаратом.");
    }

    private void OnSupportInteract(Entity<ReactorSupportComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Integrity >= 0.999f || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = _tool.UseTool(args.Used, args.User, ent.Owner, 2f, WeldingQuality,
            new ReactorSupportRepairDoAfterEvent(), fuel: 5f);
    }

    private void OnDistributorInteract(Entity<ReactorDistributorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Integrity >= 0.999f || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = _tool.UseTool(args.Used, args.User, ent.Owner, 2f, WeldingQuality,
            new ReactorSupportRepairDoAfterEvent(), fuel: 5f);
    }

    private void OnSupportRepaired(Entity<ReactorSupportComponent> ent, ref ReactorSupportRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Integrity = Math.Clamp(ent.Comp.Integrity + 0.25f, 0f, 1f);
        Dirty(ent.Owner, ent.Comp);
        _popup.PopupEntity($"Исправность системы: {ent.Comp.Integrity:P0}", ent.Owner, args.User);
    }

    private void OnDistributorRepaired(Entity<ReactorDistributorComponent> ent, ref ReactorSupportRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Integrity = Math.Clamp(ent.Comp.Integrity + 0.25f, 0f, 1f);
        Dirty(ent.Owner, ent.Comp);
        _popup.PopupEntity($"Исправность распределителя: {ent.Comp.Integrity:P0}", ent.Owner, args.User);
    }

    /// <summary>
    /// Проверяет холодный пуск: ядро остыло, стержни вернулись, сборка собрана
    /// и поле выставлено на номинал. false — пуск запрещён, причина в blocker.
    /// </summary>
    public bool TryGetStartBlocker(EntityUid core, ContainmentCoreComponent comp, out string blocker)
    {
        blocker = string.Empty;

        if (comp.IsRunning)
        {
            blocker = "Реактор уже работает.";
            return false;
        }

        if (comp.Temperature > comp.ColdStartTemperature)
        {
            blocker = $"Ядро не остыло: {comp.Temperature:0} K. Пуск возможен ниже {comp.ColdStartTemperature:0} K.";
            return false;
        }

        if (comp.ScrammedAt != TimeSpan.Zero)
        {
            var sinceScram = (_timing.CurTime - comp.ScrammedAt).TotalSeconds;

            if (sinceScram < comp.RestartLockout)
            {
                blocker = $"Стержни возвращаются в исходное положение: осталось {comp.RestartLockout - sinceScram:0} с.";
                return false;
            }
        }

        var index = 0;

        foreach (var emitterUid in GetSortedStabilizers(core))
        {
            index++;

            if (!TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
                continue;

            if (!emitter.Enabled)
            {
                blocker = $"Эмиттер {index} выключен.";
                return false;
            }

            if (!emitter.HasCartridge || emitter.CartridgeResource <= 0.01f)
            {
                blocker = $"В эмиттере {index} нет рабочего патрона.";
                return false;
            }

            if (emitter.Integrity <= 0.05f)
            {
                blocker = $"Эмиттер {index} неисправен.";
                return false;
            }

            if (emitter.Intensity > SafeStartIntensity)
            {
                blocker = $"Эмиттер {index} в форсаже: {emitter.Intensity:0}% " +
                          $"(для пуска — не выше {SafeStartIntensity:0}%).";
                return false;
            }

            if (!TryGetEmitterCooler(emitterUid, out var coolerUid) ||
                !TryComp<ReactorSupportComponent>(coolerUid, out var cooler) ||
                cooler.Integrity <= 0.05f ||
                cooler.CoolingCapacity <= 0.01f)
            {
                blocker = $"Эмиттер {index} без рабочего охлаждения.";
                return false;
            }
        }

        if (index == 0)
        {
            blocker = "В реакторе нет эмиттеров.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Срыв разгона: условия сборки перестали выполняться, стержни падают.
    /// Повторный пуск ждёт остывания и возврата стержней.
    /// </summary>
    private void AbortStart(EntityUid uid, ContainmentCoreComponent comp)
    {
        comp.IsRunning = false;
        comp.StartConditionsEnforced = false;
        comp.ScrammedAt = _timing.CurTime;
        comp.EnergyOutput = 0f;
        Dirty(uid, comp);
    }

    private static string IntegrityColor(float value)
    {
        return value >= 0.6f ? "#7BE07B" : value >= 0.25f ? "#FFB347" : "#FF6B6B";
    }

    private static Color GetLightColor(ContainmentCoreState state)
    {
        return state switch
        {
            ContainmentCoreState.Initialization => Color.FromHex("#8FB8FF"),
            ContainmentCoreState.Nominal => Color.FromHex("#B070FF"),
            ContainmentCoreState.Overload or ContainmentCoreState.Unstable => Color.FromHex("#FFAA55"),
            ContainmentCoreState.Expanding => Color.FromHex("#FF4444"),
            ContainmentCoreState.Compressing => Color.FromHex("#FF2277"),
            ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown => Color.FromHex("#FFFFFF"),
            _ => Color.FromHex("#B070FF"),
        };
    }

    private static float Approach(float current, float target, float maxDelta)
    {
        return current + Math.Clamp(target - current, -maxDelta, maxDelta);
    }

    private float GetLoad(EntityUid core, ContainmentCoreComponent comp)
    {
        var corePosition = _transform.GetMapCoordinates(core);
        var load = 0f;

        var query = EntityQueryEnumerator<ReactorDistributorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var distributor, out var xform))
        {
            var position = _transform.GetMapCoordinates(uid, xform);

            if (position.MapId != corePosition.MapId)
                continue;

            if ((position.Position - corePosition.Position).Length() > distributor.LinkRange)
                continue;

            // Изношенный распределитель отдаёт в сеть меньше: исправность
            // чинится сваркой и не должна быть чисто декоративной.
            var integrity = Math.Clamp(distributor.Integrity, 0f, 1f);

            if (TryComp<ThermalEquipmentComponent>(uid, out var distributorThermal))
                distributorThermal.Load = comp.IsRunning
                    ? Math.Clamp(distributor.Load * integrity / 1000f, 0f, 1f)
                    : 0f;

            load += distributor.Load * integrity * _thermal.GetEfficiency(uid);
        }

        return load;
    }

    private (float Loop, float Screen, float Stabilization) GetSupport(EntityUid core)
    {
        var corePosition = _transform.GetMapCoordinates(core);
        var loop = 0f;
        var screen = 0f;
        var stabilization = 0f;
        var running = TryComp<ContainmentCoreComponent>(core, out var coreComp) && coreComp.IsRunning;

        var query = EntityQueryEnumerator<ReactorSupportComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var support, out var xform))
        {
            var position = _transform.GetMapCoordinates(uid, xform);

            if (position.MapId != corePosition.MapId)
                continue;

            if ((position.Position - corePosition.Position).Length() > support.Range)
                continue;

            var integrity = Math.Clamp(support.Integrity, 0f, 1f);
            var efficiency = _thermal.GetEfficiency(uid);

            if (TryComp<ThermalEquipmentComponent>(uid, out var supportThermal))
            {
                // Охладитель нагружается пропорционально своей текущей мощности:
                // это связывает его нагрев с температурой хладагента и тепловым КПД.
                if (support.Kind == ReactorSupportKind.CoolingBlock)
                {
                    var baseCapacity = TryComp<CoolingBlockCoolantComponent>(uid, out var coolant)
                        ? Math.Max(1f, coolant.BaseCoolingCapacity)
                        : 100f;

                    var load = running
                        ? Math.Clamp(support.CoolingCapacity / baseCapacity, 0f, 1f)
                        : 0f;

                    supportThermal.Load = load;

                    // Хладагент уносит ровно то тепло, что блок выделяет сам, и не больше:
                    // иначе корпус остывал ниже среды, а без протока теплоотвод не падал
                    // вместе с нагрузкой. При пустом баке нагрузка нулевая — блок греется.
                    supportThermal.HeatRemoved = supportThermal.HeatGeneration * load;
                }
                else
                {
                    supportThermal.Load = running ? 1f : 0f;
                }
            }

            switch (support.Kind)
            {
                case ReactorSupportKind.ContainmentLoop:
                    loop += support.ContainmentBonus * integrity * efficiency;
                    stabilization += support.StabilizationBonus * integrity * efficiency;
                    break;
                case ReactorSupportKind.RadiationScreen:
                    // Экранирование не зависит от нагрева: важна только исправность экрана.
                    screen += integrity * 0.25f;
                    break;
            }
        }

        // Четыре исправных экрана по 25% дают полную защиту от излучения ядра.
        return (loop, Math.Clamp(screen, 0f, 1f), stabilization);
    }

    /// <summary>
    /// Каждый охлаждающий блок отводит тепло одному ближайшему эмиттеру.
    /// Возвращает мощность охлаждения по эмиттерам.
    /// </summary>
    private static bool IsEmitterWorking(ContainmentStabilizerComponent emitter)
    {
        return emitter.Enabled &&
               emitter.Integrity > 0.05f &&
               emitter.HasCartridge &&
               emitter.CartridgeResource > 0.01f;
    }

    /// <summary>
    /// Работающие эмиттеры ядра в том же порядке и с тем же лимитом, что и в основном цикле.
    /// </summary>
    private List<EntityUid> GetWorkingEmitters(List<EntityUid> emitters, int maxStabilizers)
    {
        var result = new List<EntityUid>();
        var count = 0;

        foreach (var emitterUid in emitters)
        {
            if (count >= maxStabilizers)
                break;

            if (!TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
                continue;

            count++;

            if (IsEmitterWorking(emitter))
                result.Add(emitterUid);
        }

        return result;
    }

    private static CoolantData? FindCoolant(CoolingBlockCoolantComponent block, ReactorSupportComponent support)
    {
        if (support.CoolantReagent == null)
            return null;

        foreach (var data in block.Coolants)
        {
            if (data.Reagent.Id == support.CoolantReagent)
                return data;
        }

        return null;
    }

    /// <summary>
    /// Строго один эмиттер — один охладитель: каждый блок обслуживает ближайший
    /// свободный работающий эмиттер, а второй блок на того же эмиттера не ставится.
    /// Эффекты собираются только с блоков, которые реально ведут охлаждение.
    /// </summary>
    /// <summary>
    /// Потребность охладителя в прокачке: форсаж эмиттера сверх 100%
    /// заставляет расходовать больше хладагента.
    /// </summary>
    private float GetCoolantDemand(EntityUid emitterUid)
    {
        if (!TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
            return 1f;

        var factor = Math.Clamp(emitter.Intensity, 0f, 200f) / 100f;
        return 1f + Math.Max(0f, factor - 1f);
    }

    private Dictionary<EntityUid, float> AllocateEmitterCooling(List<EntityUid> emitters,
        out CoolantEffects effects)
    {
        effects = new CoolantEffects();
        var result = new Dictionary<EntityUid, float>();
        _emitterCoolers.Clear();

        if (emitters.Count == 0)
            return result;

        var emitterPositions = new Dictionary<EntityUid, MapCoordinates>();

        foreach (var emitter in emitters)
            emitterPositions[emitter] = _transform.GetMapCoordinates(emitter);

        var assigned = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<ReactorSupportComponent, TransformComponent>();

        while (query.MoveNext(out var coolerUid, out var support, out var xform))
        {
            if (support.Kind != ReactorSupportKind.CoolingBlock || support.Integrity <= 0f)
                continue;

            // Потребность в прокачке пересчитывается каждый тик: охладитель, который
            // остался без эмиттера, не должен продолжать жечь хладагент по прошлому форсажу.
            support.CoolantDemand = 1f;

            // Пустой блок не отводит тепло и не должен занимать единственный слот
            // обслуживания: иначе заправленный охладитель рядом останется без эмиттера.
            if (support.CoolingCapacity <= 0.01f)
                continue;

            var coolerPosition = _transform.GetMapCoordinates(coolerUid, xform);
            EntityUid? bestEmitter = null;
            var bestDistance = support.Range;

            foreach (var (emitter, emitterPosition) in emitterPositions)
            {
                if (assigned.Contains(emitter))
                    continue;

                if (emitterPosition.MapId != coolerPosition.MapId)
                    continue;

                var distance = (emitterPosition.Position - coolerPosition.Position).Length();

                if (distance > support.Range || distance >= bestDistance)
                    continue;

                bestEmitter = emitter;
                bestDistance = distance;
            }

            if (bestEmitter is not { } target)
                continue;

            assigned.Add(target);
            _emitterCoolers[target] = coolerUid;
            result[target] = support.CoolingCapacity *
                             Math.Clamp(support.Integrity, 0f, 1f) *
                             _thermal.GetEfficiency(coolerUid);

            // Форсаж эмиттера заставляет охладитель работать с повышенным расходом.
            support.CoolantDemand = GetCoolantDemand(target);

            // Идёт в зачёт только блок, который действительно отводит тепло
            // (есть хладагент и ненулевая мощность).
            if (support.CoolingCapacity > 0.01f &&
                TryComp<CoolingBlockCoolantComponent>(coolerUid, out var blockCoolant) &&
                FindCoolant(blockCoolant, support) is { } coolantData)
            {
                effects.Merge(coolantData);
            }
        }

        return result;
    }

    /// <summary>
    /// Охладитель, обслуживающий эмиттер в последнем распределении.
    /// Пара строго один к одному, поэтому запись одна.
    /// </summary>
    public bool TryGetEmitterCooler(EntityUid emitter, out EntityUid cooler)
    {
        if (_emitterCoolers.TryGetValue(emitter, out cooler) && !Deleted(cooler))
            return true;

        cooler = default;
        return false;
    }

    /// <summary>
    /// Короткая сводка по сборке для консоли и стартового отчёта: для каждого
    /// работающего эмиттера — жидкость, теплоотвод блока и тепло патрона.
    /// </summary>
    public string BuildAssemblySummary(EntityUid core, ContainmentCoreComponent comp)
    {
        var parts = new List<string>();
        var index = 0;

        foreach (var emitterUid in GetSortedStabilizers(core))
        {
            if (index >= comp.MaxStabilizers)
                break;

            if (!TryComp<ContainmentStabilizerComponent>(emitterUid, out var emitter))
                continue;

            index++;
            var name = DarkCoreCartridgeData.GetName(emitter.CartridgeType);

            if (!IsEmitterWorking(emitter))
            {
                parts.Add($"{index}: {name} — не работает");
                continue;
            }

            var heat = DarkCoreCoolantMath.CalculateEmitterHeat(emitter.HeatCoefficient, emitter.Intensity);

            if (!_emitterCoolers.TryGetValue(emitterUid, out var coolerUid) ||
                !TryComp<ReactorSupportComponent>(coolerUid, out var support))
            {
                parts.Add($"{index}: {name} — нет охладителя (нужно {heat:0})");
                continue;
            }

            var capacity = support.CoolingCapacity * Math.Clamp(support.Integrity, 0f, 1f);
            var coolant = "нет жидкости";

            if (support.CoolantReagent is { } reagentId &&
                _prototype.TryIndex<ReagentPrototype>(reagentId, out var reagent))
            {
                coolant = reagent.LocalizedName;
            }

            var verdict = capacity + 0.01f >= heat ? "ок" : "НЕ ХВАТАЕТ";
            parts.Add($"{index}: {name} + {coolant} {capacity:0}/{heat:0} — {verdict}");
        }

        return parts.Count == 0 ? "эмиттеры не найдены" : string.Join(" · ", parts);
    }

    /// <summary>
    /// Отсчёт до расплавления идёт и на остановленном ядре: сброс стержней
    /// прекращает реакцию, но не отменяет уже начавшееся расширение или потерю
    /// сдерживания — спасти установку можно только остыванием и ремонтом.
    /// </summary>
    private bool TryHandleMeltdown(EntityUid uid, ContainmentCoreComponent comp, TimeSpan now)
    {
        // Взрыв уже поставлен в очередь, а сущность — на удаление: второй
        // QueueExplosion до фактического удаления сложил бы две бомбы в одну точку.
        if (comp.State == ContainmentCoreState.Meltdown)
            return true;

        if (comp.Expansion < 100f && comp.ContainmentIntegrity > 1f)
        {
            comp.DestabilizedSince = TimeSpan.Zero;
            return false;
        }

        comp.DestabilizedSince = comp.DestabilizedSince == TimeSpan.Zero
            ? now
            : comp.DestabilizedSince;

        var delay = comp.ContainmentIntegrity <= 1f
            ? comp.ContainmentFailureDelay
            : comp.ExplodeDelay;

        if (now - comp.DestabilizedSince < TimeSpan.FromSeconds(delay))
            return false;

        Explode(uid, comp);
        return true;
    }

    /// <summary>
    /// Живые охладители вокруг ядра. После сброса только они решают, успеет ли
    /// ядро остыть до распада.
    /// </summary>
    private float GetScramCooling(EntityUid core, ContainmentCoreComponent comp)
    {
        var corePosition = _transform.GetMapCoordinates(core);
        var total = 0f;
        var query = EntityQueryEnumerator<ReactorSupportComponent>();

        while (query.MoveNext(out var coolerUid, out var support))
        {
            if (support.Kind != ReactorSupportKind.CoolingBlock || support.Integrity <= 0.05f)
                continue;

            // Мировые координаты не различают карты: охладитель с соседней карты
            // с теми же координатами не должен считаться охлаждением этого ядра.
            var coolerPosition = _transform.GetMapCoordinates(coolerUid);

            if (coolerPosition.MapId != corePosition.MapId)
                continue;

            if ((coolerPosition.Position - corePosition.Position).Length() > comp.LinkRange)
                continue;

            total += support.CoolingCapacity * support.Integrity;
        }

        return total;
    }

    private void Explode(EntityUid uid, ContainmentCoreComponent comp)
    {
        // Потолок интенсивности на клетку не должен быть ниже шага взрыва
        // (Slope / 2): при единице весь запас растекался по десяткам клеток с
        // уроном 1 на каждой. Размер уже задан общим запасом, поэтому потолок
        // равен ему и ничего не срезает.
        _explosion.QueueExplosion(
            _transform.GetMapCoordinates(uid),
            comp.ExplosionPrototype,
            comp.ExplosionIntensity,
            comp.ExplosionSlope,
            comp.ExplosionIntensity,
            uid);

        comp.State = ContainmentCoreState.Meltdown;
        MarkConsolesAftermath(uid);
        QueueDel(uid);
    }

    private void Collapse(EntityUid uid, ContainmentCoreComponent comp)
    {
        var coordinates = _transform.GetMapCoordinates(uid);
        var singularity = Spawn(comp.CollapseSpawn, coordinates);

        if (TryComp<SingularityComponent>(singularity, out var singularityComp))
        {
            _singularity.SetEnergy(singularity, LevelToEnergy(comp.CollapseLevel), singularityComp);
        }

        comp.State = ContainmentCoreState.Singularity;
        MarkConsolesAftermath(uid);
        QueueDel(uid);
    }

    /// <summary>
    /// Помечает консоли, которые вели это ядро: после катастрофы их интерфейс
    /// работает с помехами, пока к консоли не привяжется новое живое ядро.
    /// </summary>
    private void MarkConsolesAftermath(EntityUid core)
    {
        var query = EntityQueryEnumerator<ContainmentConsoleComponent>();

        while (query.MoveNext(out _, out var console))
        {
            if (console.Core == core)
                console.Aftermath = true;
        }
    }

    private static float LevelToEnergy(byte level)
    {
        return level switch
        {
            >= 6 => 5000f,
            5 => 2000f,
            4 => 1000f,
            3 => 500f,
            2 => 200f,
            _ => 180f,
        };
    }

    /// <summary>
    /// Эмиттеры, привязанные к ядру, по углу вокруг него.
    /// </summary>
    public List<EntityUid> GetSortedStabilizers(EntityUid core)
    {
        // Взрыв удаляет ядро в том же тике, но консоль, сирена или пуск могут
        // ещё обратиться к нему: Transform() удалённой сущности бросает
        // KeyNotFoundException и роняет сервер. Отдаём пустой список.
        if (!TryComp(core, out TransformComponent? coreXform))
            return new List<EntityUid>();

        var result = new List<(EntityUid Uid, float Angle)>();
        var corePosition = coreXform.LocalPosition;

        var query = EntityQueryEnumerator<ContainmentStabilizerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var stabilizer, out var xform))
        {
            if (stabilizer.Core != core)
                continue;

            var offset = xform.LocalPosition - corePosition;
            result.Add((uid, MathF.Atan2(offset.Y, offset.X)));
        }

        result.Sort((a, b) => a.Angle.CompareTo(b.Angle));

        var uids = new List<EntityUid>(result.Count);
        foreach (var (uid, _) in result)
        {
            uids.Add(uid);
        }

        return uids;
    }

    public EntityUid? FindCore(EntityUid uid, float range)
    {
        var ourPosition = _transform.GetMapCoordinates(uid);
        EntityUid? best = null;
        var bestDistance = range;

        var query = EntityQueryEnumerator<ContainmentCoreComponent, TransformComponent>();
        while (query.MoveNext(out var coreUid, out _, out var xform))
        {
            // Уже удаляемое ядро не должно попасть в привязку даже на один тик.
            if (TerminatingOrDeleted(coreUid))
                continue;

            var corePosition = _transform.GetMapCoordinates(coreUid, xform);

            if (corePosition.MapId != ourPosition.MapId)
                continue;

            var distance = (corePosition.Position - ourPosition.Position).Length();

            if (distance > bestDistance)
                continue;

            best = coreUid;
            bestDistance = distance;
        }

        return best;
    }
}
