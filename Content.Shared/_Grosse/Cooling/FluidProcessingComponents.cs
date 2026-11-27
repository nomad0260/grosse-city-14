using System;
using System.Collections.Generic;
using Content.Shared._Grosse.Containment;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Cooling;

/// <summary>
/// Один допустимый теплоноситель и его параметры для охладителя.
/// Все расчёты выполняются на сервере; клиент только отображает результат.
/// </summary>
[DataDefinition]
public sealed partial class CoolantData
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "DarkCoreCoolantWater";

    /// <summary>Теплоёмкость жидкости.</summary>
    [DataField]
    public float HeatCapacity = 1f;

    /// <summary>Вязкость. Высокая вязкость снижает поток.</summary>
    [DataField]
    public float Viscosity = 1f;

    /// <summary>Базовый коэффициент потока.</summary>
    [DataField]
    public float Flow = 1f;

    /// <summary>Эффективность теплоотвода.</summary>
    [DataField]
    public float HeatTransfer = 1f;

    /// <summary>Стойкость к радиационному загрязнению.</summary>
    [DataField]
    public float RadiationResistance = 0.3f;

    /// <summary>
    /// Устойчивость состава. Не влияет на теплоотвод: чем ниже значение,
    /// тем быстрее накапливается отработка (коэффициент загрязнения = 2 − стабильность).
    /// </summary>
    [DataField]
    public float Stability = 1f;

    [DataField]
    public float MinTemperatureCelsius;

    [DataField]
    public float MaxTemperatureCelsius = 100f;

    /// <summary>Коэффициент загрязнения: чем выше, тем быстрее растёт отработка.</summary>
    [DataField]
    public float Fouling = 1f;

    /// <summary>Опасная жидкость при сильном загрязнении.</summary>
    [DataField]
    public bool Hazardous;

    [DataField]
    public float ConsumptionMultiplier = 1f;

    [DataField]
    public float WasteMultiplier = 1f;

    /// <summary>Насколько медленнее изнашиваются эмиттеры, пока эта жидкость залита (0…1).</summary>
    [DataField]
    public float WearReduction;

    /// <summary>Насколько быстрее изнашиваются эмиттеры (0…1). Плата за экстремальную мощность.</summary>
    [DataField]
    public float WearIncrease;

    /// <summary>Насколько меньше тепла выделяют эмиттеры (0…1).</summary>
    [DataField]
    public float HeatGenerationReduction;

    /// <summary>Прибавка к стабилизации ядра (0.1 = +10%).</summary>
    [DataField]
    public float StabilizationBonus;

    /// <summary>Прибавка к выработке энергии (0.15 = +15%).</summary>
    [DataField]
    public float EnergyBonus;

    /// <summary>Сдвиг порога температурного штрафа эмиттеров, K.</summary>
    [DataField]
    public float TemperatureTolerance;
}

/// <summary>
/// Суммарные эффекты жидкостей, залитых в работающие охладители.
/// Одинаковые эффекты не складываются: берётся самый сильный.
/// </summary>
public struct CoolantEffects
{
    public float WearReduction;
    public float WearIncrease;
    public float HeatGenerationReduction;
    public float StabilizationBonus;
    public float EnergyBonus;
    public float TemperatureTolerance;

    public void Merge(CoolantData data)
    {
        WearReduction = Math.Max(WearReduction, data.WearReduction);
        WearIncrease = Math.Max(WearIncrease, data.WearIncrease);
        HeatGenerationReduction = Math.Max(HeatGenerationReduction, data.HeatGenerationReduction);
        StabilizationBonus = Math.Max(StabilizationBonus, data.StabilizationBonus);
        EnergyBonus = Math.Max(EnergyBonus, data.EnergyBonus);
        TemperatureTolerance = Math.Max(TemperatureTolerance, data.TemperatureTolerance);
    }

    /// <summary>Итоговый множитель износа эмиттеров.</summary>
    public readonly float GetWearFactor()
    {
        return (1f - Math.Clamp(WearReduction, 0f, 1f)) * (1f + Math.Max(0f, WearIncrease));
    }
}

/// <summary>
/// Внутренний контур охладителя: хладагент расходуется, отработанная жидкость накапливается.
/// Трубы и насосы для этого не нужны.
/// </summary>
[RegisterComponent]
public sealed partial class CoolingBlockCoolantComponent : Component
{
    [DataField]
    public List<CoolantData> Coolants = CoolantCatalog.CreateDefault();

    [DataField]
    public ProtoId<ReagentPrototype> WasteReagent = "DarkCoreCoolantWaste";

    /// <summary>Теплоотдача одного блока при идеальном хладагенте. Совпадает с прототипом охладителя.</summary>
    [DataField]
    public float BaseCoolingCapacity = 120f;

    [DataField]
    public float CoolantUsePerSecond = 0.5f;

    [DataField]
    public float WastePerSecond = 0.25f;

    /// <summary>После этого количества отходов охладитель перестаёт работать до промывки.</summary>
    [DataField]
    public float MaxWaste = 150f;

    /// <summary>Порог загрязнения, при котором опасная жидкость повреждает контур.</summary>
    [DataField]
    public float DangerContamination = 0.45f;

    /// <summary>Штраф к эффективности при смешивании разных жидкостей.</summary>
    [DataField]
    public float MixturePenalty = 0.9f;

    [DataField]
    public string Solution = "solution";

    [ViewVariables]
    public float Accumulator;
}

/// <summary>
/// Тип установки для производства и переработки жидкостей и патронов.
/// </summary>
public enum DarkCoreProcessorKind : byte
{
    CoolantSynthesizer,
    WasteProcessor,
    CartridgeRecycler,
}

/// <summary>
/// Установка, которая принимает предмет и выполняет одну операцию:
/// заправку канистры, очистку отработанного хладагента или утилизацию патрона.
/// </summary>
[RegisterComponent]
public sealed partial class DarkCoreProcessorComponent : Component
{
    [DataField]
    public DarkCoreProcessorKind Kind = DarkCoreProcessorKind.CoolantSynthesizer;

    [DataField]
    public float Delay = 4f;

    [DataField]
    public float CoolantAmount = 300f;

    [DataField]
    public float RecoveredCoolantAmount = 150f;

    [DataField]
    public float RecycledCartridgeFraction = 0.5f;
}

/// <summary>
/// Сырьё, полученное при разрушении патрона в утилизаторе.
/// </summary>
[RegisterComponent]
public sealed partial class DarkCoreCartridgeScrapComponent : Component
{
}

/// <summary>
/// Радиоактивная бочка, в которую складывают только отработанные патроны эмиттера.
/// </summary>
[RegisterComponent]
public sealed partial class DarkCoreSpentCartridgeBarrelComponent : Component
{
}

/// <summary>
/// Канистра с хладагентом или отработанной жидкостью.
/// </summary>
[RegisterComponent]
public sealed partial class DarkCoreCoolantCanisterComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> CoolantReagent = "DarkCoreCoolant";

    [DataField]
    public bool Waste;
}

[Serializable, NetSerializable]
public sealed partial class DarkCoreProcessorDoAfterEvent : SimpleDoAfterEvent
{
}
