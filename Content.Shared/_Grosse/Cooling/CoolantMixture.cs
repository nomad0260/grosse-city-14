using System;
using System.Collections.Generic;
using Content.Shared.Chemistry.Components;

namespace Content.Shared._Grosse.Cooling;

/// <summary>
/// Усреднённые характеристики теплоносителя в баке. Считаются одним кодом
/// и для охладителя, и для смесителя, чтобы интерфейс не расходился с моделью.
/// </summary>
public sealed class CoolantMixture
{
    /// <summary>Объём известных теплоносителей.</summary>
    public float Total;

    /// <summary>Объём примесей: отработка и всё, чего нет в таблице теплоносителей.</summary>
    public float Unknown;

    public float HeatCapacity;
    public float Viscosity;
    public float Flow;
    public float HeatTransfer;
    public float RadiationResistance;
    public float Stability;
    public float MinTemperatureCelsius;
    public float MaxTemperatureCelsius;
    public float Fouling;
    public float ConsumptionMultiplier;
    public float WasteMultiplier;
    public int Components;
    public bool Hazardous;
    public string DominantReagent = "DarkCoreCoolantWater";

    /// <summary>Доля примесей в баке, 0…1. Вода без отработки — 0.</summary>
    public float Contamination => Total + Unknown > 0.01f ? Unknown / (Total + Unknown) : 1f;

    /// <summary>Поток смеси: вязкость его уменьшает, но не ниже 0.1.</summary>
    public float EffectiveFlow => DarkCoreCoolantMath.CalculateFlow(Flow, Viscosity);

    /// <summary>Множитель теплоотвода смеси с учётом штрафа за смешивание.</summary>
    public float GetCapacityFactor(float mixturePenalty)
    {
        return DarkCoreCoolantMath.CalculateCapacityFactor(
            HeatCapacity,
            HeatTransfer,
            EffectiveFlow,
            Contamination,
            Components,
            mixturePenalty);
    }

    /// <summary>Теплоотвод блока на этой смеси, единиц тепла в секунду.</summary>
    public float GetCapacity(float baseCapacity, float mixturePenalty)
    {
        return Math.Max(0f, baseCapacity * GetCapacityFactor(mixturePenalty));
    }

    /// <summary>Расход жидкости в секунду на этой смеси.</summary>
    public float GetConsumptionPerSecond(float baseUse)
    {
        return DarkCoreCoolantMath.CalculateConsumptionPerSecond(
            baseUse,
            ConsumptionMultiplier,
            EffectiveFlow,
            Contamination);
    }
}

/// <summary>
/// Расчёт смеси в баке: средневзвешенные свойства известных теплоносителей
/// плюс доля примесей.
/// </summary>
public static class CoolantMixtureMath
{
    public static CoolantMixture? Compute(IReadOnlyList<CoolantData> coolants, Solution solution)
    {
        var total = 0f;
        var heatCapacity = 0f;
        var viscosity = 0f;
        var flow = 0f;
        var heatTransfer = 0f;
        var radiationResistance = 0f;
        var stability = 0f;
        var minTemperature = 0f;
        var maxTemperature = 0f;
        var fouling = 0f;
        var consumption = 0f;
        var waste = 0f;
        var components = 0;
        var hazardous = false;
        var dominant = "DarkCoreCoolantWater";
        var dominantAmount = 0f;

        foreach (var data in coolants)
        {
            var amount = solution.GetTotalPrototypeQuantity(data.Reagent).Float();
            if (amount <= 0f)
                continue;

            total += amount;
            heatCapacity += data.HeatCapacity * amount;
            viscosity += data.Viscosity * amount;
            flow += data.Flow * amount;
            heatTransfer += data.HeatTransfer * amount;
            radiationResistance += data.RadiationResistance * amount;
            stability += data.Stability * amount;
            minTemperature += data.MinTemperatureCelsius * amount;
            maxTemperature += data.MaxTemperatureCelsius * amount;
            fouling += data.Fouling * amount;
            consumption += data.ConsumptionMultiplier * amount;
            waste += data.WasteMultiplier * amount;
            components++;
            hazardous |= data.Hazardous;

            if (amount > dominantAmount)
            {
                dominantAmount = amount;
                dominant = data.Reagent.Id;
            }
        }

        if (total <= 0f)
            return null;

        return new CoolantMixture
        {
            Total = total,
            Unknown = Math.Max(0f, solution.Volume.Float() - total),
            HeatCapacity = heatCapacity / total,
            Viscosity = viscosity / total,
            Flow = flow / total,
            HeatTransfer = heatTransfer / total,
            RadiationResistance = radiationResistance / total,
            Stability = stability / total,
            MinTemperatureCelsius = minTemperature / total,
            MaxTemperatureCelsius = maxTemperature / total,
            Fouling = fouling / total,
            ConsumptionMultiplier = consumption / total,
            WasteMultiplier = waste / total,
            Components = components,
            Hazardous = hazardous,
            DominantReagent = dominant,
        };
    }
}
