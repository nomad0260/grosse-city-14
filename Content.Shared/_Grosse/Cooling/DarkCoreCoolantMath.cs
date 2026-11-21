using System;
using Content.Shared._Grosse.Containment;

namespace Content.Shared._Grosse.Cooling;

/// <summary>
/// Формулы теплового баланса РТМ. Вынесены в отдельный класс, чтобы серверный
/// расчёт охлаждения, отладочная команда и справочник использовали одни и те же числа.
/// </summary>
public static class DarkCoreCoolantMath
{
    /// <summary>Тепловыделение эмиттера при нулевой интенсивности, единиц тепла в секунду.</summary>
    public const float EmitterBaseHeat = 30f;

    /// <summary>Добавка тепла на единицу коэффициента патрона при 100% интенсивности.</summary>
    public const float EmitterHeatPerCoefficient = 150f;

    /// <summary>Теплоотдача одного охладителя РТМ. Совпадает с baseCoolingCapacity прототипа.</summary>
    public const float DefaultCoolingCapacity = 120f;

    /// <summary>Объём канистры хладагента.</summary>
    public const float CanisterVolume = 300f;

    /// <summary>
    /// Поток жидкости: вязкость его уменьшает, но не ниже 0.1 и не выше 1.5.
    /// </summary>
    public static float CalculateFlow(float flow, float viscosity)
    {
        return Math.Clamp(flow / Math.Max(0.1f, viscosity), 0.1f, 1.5f);
    }

    /// <summary>
    /// Множитель теплоотвода для уже усреднённой смеси. Именно он превращает
    /// базовые 100 единиц охладителя в фактическую теплоотдачу.
    /// </summary>
    public static float CalculateCapacityFactor(
        float heatCapacity,
        float heatTransfer,
        float flow,
        float contamination = 0f,
        int components = 1,
        float mixturePenalty = 1f)
    {
        var factor = heatTransfer *
                     MathF.Sqrt(Math.Max(0.1f, heatCapacity)) *
                     flow;

        factor *= 1f - contamination * 0.8f;

        if (components > 1)
            factor *= Math.Clamp(mixturePenalty, 0.5f, 1f);

        return factor;
    }

    /// <summary>Фактическая теплоотдача одного охладителя на чистом хладагенте.</summary>
    public static float CalculateCapacity(CoolantData coolant, float baseCapacity, float contamination = 0f)
    {
        var flow = CalculateFlow(coolant.Flow, coolant.Viscosity);
        return Math.Max(0f, baseCapacity * CalculateCapacityFactor(
            coolant.HeatCapacity,
            coolant.HeatTransfer,
            flow,
            contamination));
    }

    /// <summary>Расход хладагента в секунду при известном потоке.</summary>
    public static float CalculateConsumptionPerSecond(float baseUse, float consumptionMultiplier, float flow, float contamination = 0f)
    {
        return baseUse * consumptionMultiplier / Math.Max(0.25f, flow) * (1f + contamination * 0.5f);
    }

    /// <summary>Расход хладагента в секунду для чистого хладагента.</summary>
    public static float CalculateConsumptionPerSecond(CoolantData coolant, float baseUse, float contamination = 0f)
    {
        var flow = CalculateFlow(coolant.Flow, coolant.Viscosity);
        return CalculateConsumptionPerSecond(baseUse, coolant.ConsumptionMultiplier, flow, contamination);
    }

    /// <summary>
    /// Тепловыделение эмиттера: фиксированные 30 единиц плюс вклад патрона,
    /// пропорциональный интенсивности луча. Форсаж свыше 100% греет круче линейного:
    /// каждый процент перегруза добавляет половину обычного вклада.
    /// </summary>
    public static float CalculateEmitterHeat(float heatCoefficient, float intensityPercent = 100f)
    {
        var intensity = Math.Clamp(intensityPercent, 0f, 200f) / 100f;
        var overdrive = Math.Max(0f, intensity - 1f);
        return EmitterBaseHeat +
               (intensity + overdrive * 0.5f) * heatCoefficient * EmitterHeatPerCoefficient;
    }

    /// <summary>
    /// Во сколько раз быстрее накапливается отработка. Стабильность влияет именно
    /// на загрязнение, а не на теплоотвод.
    /// </summary>
    public static float CalculateStabilityFactor(float stability)
    {
        return Math.Clamp(2f - stability, 0.5f, 2f);
    }

    /// <summary>Сколько секунд проработает полная канистра при таком расходе.</summary>
    public static float CalculateCanisterRuntime(float consumptionPerSecond, float volume = CanisterVolume)
    {
        return consumptionPerSecond <= 0.01f ? float.PositiveInfinity : volume / consumptionPerSecond;
    }
}
