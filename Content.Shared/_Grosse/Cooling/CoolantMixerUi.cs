using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Cooling;

[NetSerializable, Serializable]
public enum CoolantMixerUiKey
{
    Key,
}

/// <summary>
/// Имена ячеек смесителя. Совпадают с ключами ItemSlots в прототипе.
/// </summary>
public static class CoolantMixerSlots
{
    /// <summary>Откуда берём жидкость.</summary>
    public const string Input = "inputSlot";

    /// <summary>Куда сливаем готовую смесь.</summary>
    public const string Output = "outputSlot";
}

public enum CoolantMixerTransferMode : byte
{
    /// <summary>Влить содержимое баллона из ячейки в бак смесителя.</summary>
    PourIn,

    /// <summary>Слить смесь из бака в тару.</summary>
    PourOut,
}

/// <summary>Одна жидкость в баке: имя реагента и количество.</summary>
[NetSerializable, Serializable]
public sealed class CoolantMixerReagent
{
    public readonly string Reagent;
    public readonly float Amount;

    public CoolantMixerReagent(string reagent, float amount)
    {
        Reagent = reagent;
        Amount = amount;
    }
}

/// <summary>
/// Бонусы основной (доминирующей) жидкости смеси. Охладитель применяет их
/// от залитого реагента, поэтому смеситель показывает ровно эти числа.
/// </summary>
[NetSerializable, Serializable]
public sealed class CoolantMixerBonuses
{
    public readonly string Coolant;

    /// <summary>Прибавка к выработке энергии (0.1 = +10%).</summary>
    public readonly float EnergyBonus;

    /// <summary>Итоговый множитель износа эмиттеров: 0.9 — на 10% медленнее.</summary>
    public readonly float WearFactor;

    /// <summary>Прибавка к стабилизации ядра.</summary>
    public readonly float StabilizationBonus;

    /// <summary>Сдвиг порога температурного штрафа, K.</summary>
    public readonly float TemperatureTolerance;

    /// <summary>Насколько меньше тепла выделяют эмиттеры (0…1).</summary>
    public readonly float HeatGenerationReduction;

    public CoolantMixerBonuses(
        string coolant,
        float energyBonus,
        float wearFactor,
        float stabilizationBonus,
        float temperatureTolerance,
        float heatGenerationReduction)
    {
        Coolant = coolant;
        EnergyBonus = energyBonus;
        WearFactor = wearFactor;
        StabilizationBonus = stabilizationBonus;
        TemperatureTolerance = temperatureTolerance;
        HeatGenerationReduction = heatGenerationReduction;
    }
}

/// <summary>
/// Прогноз характеристик смеси — ровно то, что получит охладитель,
/// если залить её в бак.
/// </summary>
[NetSerializable, Serializable]
public sealed class CoolantMixerStats
{
    /// <summary>Теплоотвод блока, единиц тепла в секунду.</summary>
    public readonly float Capacity;

    public readonly float HeatCapacity;
    public readonly float HeatTransfer;
    public readonly float Flow;
    public readonly float Viscosity;
    public readonly float RadiationResistance;
    public readonly float Stability;
    public readonly float MinTemperatureCelsius;
    public readonly float MaxTemperatureCelsius;

    /// <summary>Расход смеси, единиц в секунду.</summary>
    public readonly float ConsumptionPerSecond;

    /// <summary>Сколько секунд проработает канистра; −1 — расход нулевой.</summary>
    public readonly float RuntimeSeconds;

    public readonly float WasteMultiplier;
    public readonly float Fouling;
    public readonly float Contamination;
    public readonly int Components;
    public readonly bool Hazardous;

    /// <summary>Бонусы основной жидкости; null — бак пуст.</summary>
    public readonly CoolantMixerBonuses? Bonuses;

    public CoolantMixerStats(
        float capacity,
        float heatCapacity,
        float heatTransfer,
        float flow,
        float viscosity,
        float radiationResistance,
        float stability,
        float minTemperatureCelsius,
        float maxTemperatureCelsius,
        float consumptionPerSecond,
        float runtimeSeconds,
        float wasteMultiplier,
        float fouling,
        float contamination,
        int components,
        bool hazardous,
        CoolantMixerBonuses? bonuses)
    {
        Capacity = capacity;
        HeatCapacity = heatCapacity;
        HeatTransfer = heatTransfer;
        Flow = flow;
        Viscosity = viscosity;
        RadiationResistance = radiationResistance;
        Stability = stability;
        MinTemperatureCelsius = minTemperatureCelsius;
        MaxTemperatureCelsius = maxTemperatureCelsius;
        ConsumptionPerSecond = consumptionPerSecond;
        RuntimeSeconds = runtimeSeconds;
        WasteMultiplier = wasteMultiplier;
        Fouling = fouling;
        Contamination = contamination;
        Components = components;
        Hazardous = hazardous;
        Bonuses = bonuses;
    }
}

[NetSerializable, Serializable]
public sealed class CoolantMixerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly float Volume;
    public readonly float MaxVolume;

    /// <summary>Характеристики смеси; null — бак пуст.</summary>
    public readonly CoolantMixerStats? Stats;

    /// <summary>Что сейчас в баке: жидкости и их количества.</summary>
    public readonly List<CoolantMixerReagent> Reagents;

    public readonly NetEntity? InputItem;
    public readonly NetEntity? OutputItem;
    public readonly FluidSlotState? InputSlot;
    public readonly FluidSlotState? OutputSlot;

    public CoolantMixerBoundUserInterfaceState(
        float volume,
        float maxVolume,
        CoolantMixerStats? stats,
        List<CoolantMixerReagent> reagents,
        NetEntity? inputItem,
        NetEntity? outputItem,
        FluidSlotState? inputSlot,
        FluidSlotState? outputSlot)
    {
        Volume = volume;
        MaxVolume = maxVolume;
        Stats = stats;
        Reagents = reagents;
        InputItem = inputItem;
        OutputItem = outputItem;
        InputSlot = inputSlot;
        OutputSlot = outputSlot;
    }
}

[NetSerializable, Serializable]
public sealed class CoolantMixerTransferMessage : BoundUserInterfaceMessage
{
    public readonly CoolantMixerTransferMode Mode;

    public CoolantMixerTransferMessage(CoolantMixerTransferMode mode)
    {
        Mode = mode;
    }
}
