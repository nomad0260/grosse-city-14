using System.Collections.Generic;

namespace Content.Shared._Grosse.Cooling;

/// <summary>
/// Единая таблица теплоносителей РТМ. Охладитель и смеситель берут свойства
/// из одного места, чтобы прогноз смесителя не расходился с расчётом блока.
/// </summary>
public static class CoolantCatalog
{
    /// <summary>Теплоноситель по идентификатору реагента; null, если его нет в таблице.</summary>
    public static CoolantData? Find(IReadOnlyList<CoolantData> coolants, string reagent)
    {
        foreach (var data in coolants)
        {
            if (data.Reagent.Id == reagent)
                return data;
        }

        return null;
    }

    /// <summary>
    /// Строгая лестница от самой слабой жидкости к самой сильной.
    /// Теплоотвод подобран так, чтобы каждый уровень закрывал свой патрон
    /// одним охладителем: вода → база → криогель → термосил → радиогель →
    /// изотоп → плазма → уран → дигамма-флюид.
    /// </summary>
    public static List<CoolantData> CreateDefault()
    {
        return new List<CoolantData>
        {
            new()
            {
                Reagent = "DarkCoreCoolantWater", HeatCapacity = 1.0f, Viscosity = 1.0f,
                Flow = 1.0f, HeatTransfer = 0.917f, RadiationResistance = 0.3f, Stability = 1.0f,
                MinTemperatureCelsius = 0f, MaxTemperatureCelsius = 100f, Fouling = 1.0f,
                WearReduction = 0.10f,
            },
            new()
            {
                Reagent = "DarkCoreCoolant", HeatCapacity = 1.0f, Viscosity = 1.0f,
                Flow = 1.0f, HeatTransfer = 1.083f, RadiationResistance = 0.4f, Stability = 1.0f,
                MinTemperatureCelsius = 0f, MaxTemperatureCelsius = 100f, Fouling = 1.0f,
            },
            new()
            {
                Reagent = "DarkCoreCoolantCryogel", HeatCapacity = 1.4f, Viscosity = 1.0f,
                Flow = 0.85f, HeatTransfer = 1.326f, RadiationResistance = 0.7f, Stability = 0.8f,
                MinTemperatureCelsius = -100f, MaxTemperatureCelsius = 80f, Fouling = 0.7f,
                WearReduction = 0.25f,
            },
            new()
            {
                Reagent = "DarkCoreCoolantThermosil", HeatCapacity = 1.1f, Viscosity = 1.0f,
                Flow = 0.9f, HeatTransfer = 1.898f, RadiationResistance = 0.8f, Stability = 1.2f,
                MinTemperatureCelsius = 20f, MaxTemperatureCelsius = 350f, Fouling = 0.6f,
                TemperatureTolerance = 100f,
            },
            new()
            {
                Reagent = "DarkCoreCoolantRadiogel", HeatCapacity = 1.2f, Viscosity = 1.0f,
                Flow = 0.9f, HeatTransfer = 1.985f, RadiationResistance = 1.5f, Stability = 0.9f,
                MinTemperatureCelsius = -20f, MaxTemperatureCelsius = 180f, Fouling = 0.45f,
                HeatGenerationReduction = 0.10f,
                Hazardous = true,
            },
            new()
            {
                Reagent = "DarkCoreCoolantIsotope", HeatCapacity = 1.5f, Viscosity = 1.0f,
                Flow = 0.9f, HeatTransfer = 1.928f, RadiationResistance = 1.8f, Stability = 0.7f,
                MinTemperatureCelsius = -50f, MaxTemperatureCelsius = 250f, Fouling = 0.8f,
                StabilizationBonus = 0.10f,
            },
            new()
            {
                Reagent = "DarkCoreCoolantPlasma", HeatCapacity = 1.2f, Viscosity = 1.0f,
                Flow = 0.95f, HeatTransfer = 2.282f, RadiationResistance = 0.8f, Stability = 0.9f,
                MinTemperatureCelsius = -20f, MaxTemperatureCelsius = 180f, Fouling = 0.8f,
                EnergyBonus = 0.10f,
                // Плазма — экономный вариант для беты: канистра держится дольше.
                ConsumptionMultiplier = 0.85f,
            },
            new()
            {
                Reagent = "DarkCoreCoolantUranium", HeatCapacity = 1.5f, Viscosity = 1.0f,
                Flow = 0.9f, HeatTransfer = 2.381f, RadiationResistance = 1.6f, Stability = 0.8f,
                MinTemperatureCelsius = -50f, MaxTemperatureCelsius = 250f, Fouling = 0.6f,
                EnergyBonus = 0.15f,
                // Уран — запас по температуре: эмиттеры дольше живут в перегреве.
                TemperatureTolerance = 50f,
                Hazardous = true,
            },
            new()
            {
                Reagent = "DarkCoreCoolantDigamma", HeatCapacity = 1.8f, Viscosity = 1.0f,
                Flow = 0.7f, HeatTransfer = 3.106f, RadiationResistance = 2.0f, Stability = 0.4f,
                MinTemperatureCelsius = -80f, MaxTemperatureCelsius = 300f, Fouling = 1.3f,
                EnergyBonus = 0.25f,
                WearIncrease = 0.25f,
                Hazardous = true,
            },
        };
    }
}
