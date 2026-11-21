using System;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Состояния реактора РТМ.
/// </summary>
public enum ContainmentCoreState : byte
{
    Offline,
    Initialization,
    Nominal,
    Overload,
    Unstable,
    Expanding,
    Compressing,
    ContainmentFailure,
    Meltdown,
    Singularity,
}

/// <summary>
/// Тип вспомогательной системы реактора.
/// </summary>
public enum ReactorSupportKind : byte
{
    ContainmentLoop,
    RadiationScreen,
    CoolingBlock,
}

/// <summary>
/// Ядро РТМ. Параметры в игровых единицах:
/// стабильность 0–100, плотность 0–200, расширение 0–200,
/// энергия 0–1000, температура 0–1000, радиация 0–1000, сдерживание 0–100.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ContainmentCoreComponent : Component
{
    #region Состояние

    [DataField, AutoNetworkedField]
    public ContainmentCoreState State = ContainmentCoreState.Offline;

    [DataField, AutoNetworkedField]
    public bool IsRunning;

    [DataField]
    public float StartupDelay = 5f;

    [ViewVariables] public float StartupTimer;

    /// <summary>
    /// Максимальная температура ядра, при которой разрешён пуск. Горячее ядро
    /// не примет стержни, поэтому после сброса нужно ждать остывания.
    /// </summary>
    [DataField] public float ColdStartTemperature = 350f;

    /// <summary>Сколько секунд стержни возвращаются в исходное положение после сброса.</summary>
    [DataField] public float RestartLockout = 20f;

    /// <summary>Термоудар сброса: доля исправности, которую теряет каждый эмиттер.</summary>
    [DataField] public float ScramIntegrityPenalty = 0.15f;

    /// <summary>
    /// Пуск шёл с консоли: пока идёт разгон, сборка обязана держать условия,
    /// иначе стержни падают автоматически.
    /// </summary>
    [ViewVariables] public bool StartConditionsEnforced;

    /// <summary>Время последнего сброса стержней. Ноль — сброса ещё не было.</summary>
    [ViewVariables] public TimeSpan ScrammedAt = TimeSpan.Zero;

    /// <summary>Скорость остывания остановленного ядра, K/с без живого контура.</summary>
    [DataField] public float ScramCoolingRate = 8f;

    /// <summary>Суммарная мощность охлаждения, удваивающая остывание после сброса.</summary>
    [DataField] public float ScramCoolingReference = 200f;

    /// <summary>
    /// Температура, выше которой расширение остановленного ядра не спадает:
    /// сброс стержней не сдувает уже раздутое ядро, его надо остудить.
    /// </summary>
    [DataField] public float ExpansionHoldTemperature = 700f;

    #endregion

    #region Параметры ядра

    [DataField, AutoNetworkedField]
    public float Stability;

    [DataField, AutoNetworkedField]
    public float Density = 100f;

    [DataField, AutoNetworkedField]
    public float Expansion;

    [DataField, AutoNetworkedField]
    public float Temperature = 293f;

    /// <summary>Полное сжатие C = Σ I·Kc.</summary>
    [DataField, AutoNetworkedField]
    public float Compression;

    /// <summary>Превышение сжатия над порогом: ΔD = max(0, C − Cпорог).</summary>
    [DataField, AutoNetworkedField]
    public float CompressionExcess;

    /// <summary>Сдерживание, 0–100.</summary>
    [DataField, AutoNetworkedField]
    public float ContainmentIntegrity = 100f;

    [DataField, AutoNetworkedField]
    public float CoolingEfficiency = 1f;

    [DataField, AutoNetworkedField]
    public float CoolingCapacity;

    [DataField, AutoNetworkedField]
    public float HeatInput;

    [DataField, AutoNetworkedField]
    public float Radiation;

    #endregion

    #region Энергия

    /// <summary>
    /// Верхняя граница выработки. Тёмная материя питает город, поэтому
    /// даже форсированный реактор остаётся в пределах этого потолка.
    /// </summary>
    [DataField]
    public float MaxEnergyOutput = 5000f;

    [DataField, AutoNetworkedField]
    public float PowerScale = 1f;

    /// <summary>Верхняя граница множителя мощности: 3 — это 300%.</summary>
    [DataField]
    public float MaxPowerScale = 3f;

    /// <summary>Прибавка к теплу эмиттеров за каждый шаг мощности выше 100%.</summary>
    [DataField]
    public float PowerHeatFactor = 1.2f;

    /// <summary>Прибавка к износу корпуса эмиттеров за каждый шаг мощности выше 100%.</summary>
    [DataField]
    public float PowerWearFactor = 1f;

    /// <summary>Прибавка к радиации за каждый шаг мощности выше 100%.</summary>
    [DataField]
    public float PowerRadiationFactor = 0.8f;

    /// <summary>Штраф к целевой стабильности за каждый шаг мощности выше 100%.</summary>
    [DataField]
    public float PowerStabilityPenalty = 0.12f;

    /// <summary>Вычет из целевого сдерживания за каждый шаг мощности выше 100%.</summary>
    [DataField]
    public float PowerContainmentPenalty = 8f;

    [DataField, AutoNetworkedField]
    public float EnergyOutput;

    [DataField, AutoNetworkedField]
    public float EnergyLoad;

    #endregion

    #region Эмиттеры и сдерживание

    [DataField] public int MaxStabilizers = 4;

    /// <summary>Суммарная стабилизация, необходимая для удержания ядра.</summary>
    [DataField] public float StabilizationThreshold = 200f;

    /// <summary>Суммарное сжатие, после которого начинает расти плотность.</summary>
    /// <remarks>
    /// Два альфа- и два гамма-патрона при номинальном поле дают около 216 единиц сжатия.
    /// Это штатная смесь, поэтому порог должен быть выше неё; критический отсчёт
    /// начинается только после <see cref="CompressionLimit"/> сверх этого значения.
    /// </remarks>
    [DataField] public float CompressionThreshold = 260f;

    /// <summary>Избыток сжатия (C − CompressionThreshold), после которого начинается отсчёт до коллапса.</summary>
    [DataField] public float CompressionLimit = 80f;

    /// <summary>Плотность, при которой сжатие уже реально влияет на ядро.</summary>
    [DataField] public float CompressionStateDensity = 100.01f;

    /// <summary>Отсчёт от критического сжатия до коллапса в сингулярность.</summary>
    [DataField] public float CollapseDelay = 20f;
    [DataField] public float ExplodeDelay = 45f;
    [DataField] public float ContainmentFailureDelay = 30f;

    [DataField] public float StabilityChangeRate = 30f;
    [DataField] public float ContainmentChangeRate = 10f;
    [DataField] public float ExpansionRate = 4f;
    [DataField] public float ExpansionRecoveryRate = 2f;

    /// <summary>
    /// Допуск к порогу стабилизации: расширение начинается, только когда
    /// стабилизация падает ниже (1 − допуск) от порога. Без него сборка,
    /// стоящая ровно на пороге, расширялась от любой сотой доли износа.
    /// </summary>
    [DataField] public float ExpansionTolerance = 0.05f;
    [DataField] public float DensityGrowthRate = 0.08f;
    [DataField] public float DensityRecoveryRate = 8f;

    #endregion

    #region Тепло и радиация

    [DataField] public float AmbientTemperature = 293f;
    [DataField] public float MaxTemperature = 1000f;
    [DataField] public float HeatRate = 0.12f;
    [DataField] public float MaxRadiation = 1000f;

    #endregion

    #region Привязка и катастрофа

    [DataField] public float LinkRange = 20f;
    [DataField] public float ExplosionIntensity = 4000f;
    [DataField] public float ExplosionSlope = 6f;
    [DataField] public string ExplosionPrototype = "Default";
    [DataField] public EntProtoId CollapseSpawn = "Singularity";
    [DataField] public byte CollapseLevel = 4;
    [DataField] public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25f);

    [ViewVariables] public TimeSpan NextUpdate;
    [ViewVariables] public TimeSpan DestabilizedSince = TimeSpan.Zero;
    [ViewVariables] public TimeSpan OverpoweredSince = TimeSpan.Zero;

    #endregion
}
