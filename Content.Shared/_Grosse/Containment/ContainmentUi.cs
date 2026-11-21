using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Containment;

[NetSerializable, Serializable]
public enum ContainmentConsoleUiKey
{
    Key,
}

[NetSerializable, Serializable]
public sealed class ReactorProjectorInfo
{
    public readonly bool Enabled;
    public readonly float Intensity;
    public readonly float Integrity;
    public readonly bool HasCartridge;
    public readonly string CartridgeName;
    public readonly float CartridgeResource;
    public readonly float CartridgeMaxResource;
    public readonly float Temperature;

    /// <summary>Название жидкости в обслуживающем охладителе или «—».</summary>
    public readonly string CoolantName;

    /// <summary>Текущий теплоотвод обслуживающего охладителя, единиц тепла в секунду.</summary>
    public readonly float CoolantCapacity;

    /// <summary>Тепло эмиттера при текущей интенсивности, единиц в секунду.</summary>
    public readonly float HeatLoad;

    public ReactorProjectorInfo(
        bool enabled,
        float intensity,
        float integrity,
        bool hasCartridge,
        string cartridgeName,
        float cartridgeResource,
        float cartridgeMaxResource,
        float temperature,
        string coolantName,
        float coolantCapacity,
        float heatLoad)
    {
        Enabled = enabled;
        Intensity = intensity;
        Integrity = integrity;
        HasCartridge = hasCartridge;
        CartridgeName = cartridgeName;
        CartridgeResource = cartridgeResource;
        CartridgeMaxResource = cartridgeMaxResource;
        Temperature = temperature;
        CoolantName = coolantName;
        CoolantCapacity = coolantCapacity;
        HeatLoad = heatLoad;
    }
}

/// <summary>
/// Полная телеметрия РТМ для центральной консоли.
/// </summary>
[NetSerializable, Serializable]
public sealed class ContainmentConsoleState : BoundUserInterfaceState
{
    public readonly bool CorePresent;
    public readonly ContainmentCoreState CoreState;
    public readonly ReactorConsoleMode Mode;
    public readonly bool Running;

    public readonly float Stability;
    public readonly float Density;
    public readonly float Temperature;
    public readonly float Expansion;
    public readonly float Compression;
    public readonly float ContainmentIntegrity;
    public readonly float CoolingEfficiency;
    public readonly float CoolingCapacity;
    public readonly float HeatInput;
    public readonly float Radiation;
    public readonly float EnergyOutput;
    public readonly float EnergyLoad;

    public readonly float PowerScale;
    public readonly float MaxEnergyOutput;
    public readonly float MaxTemperature;
    public readonly float MaxRadiation;
    public readonly float CompressionThreshold;
    public readonly float CollapseTimeRemaining;

    public readonly List<ReactorProjectorInfo> Projectors;

    /// <summary>
    /// Консоль пережила катастрофу своего ядра (взрыв или коллапс):
    /// интерфейс работает с помехами, пока к консоли не привяжется новое ядро.
    /// </summary>
    public readonly bool Aftermath;

    public ContainmentConsoleState(
        bool corePresent,
        ContainmentCoreState coreState,
        ReactorConsoleMode mode,
        bool running,
        float stability,
        float density,
        float temperature,
        float expansion,
        float compression,
        float containmentIntegrity,
        float coolingEfficiency,
        float coolingCapacity,
        float heatInput,
        float radiation,
        float energyOutput,
        float energyLoad,
        float powerScale,
        float maxEnergyOutput,
        float maxTemperature,
        float maxRadiation,
        float compressionThreshold,
        float collapseTimeRemaining,
        List<ReactorProjectorInfo> projectors,
        bool aftermath = false)
    {
        CorePresent = corePresent;
        CoreState = coreState;
        Mode = mode;
        Running = running;
        Stability = stability;
        Density = density;
        Temperature = temperature;
        Expansion = expansion;
        Compression = compression;
        ContainmentIntegrity = containmentIntegrity;
        CoolingEfficiency = coolingEfficiency;
        CoolingCapacity = coolingCapacity;
        HeatInput = heatInput;
        Radiation = radiation;
        EnergyOutput = energyOutput;
        EnergyLoad = energyLoad;
        PowerScale = powerScale;
        MaxEnergyOutput = maxEnergyOutput;
        MaxTemperature = maxTemperature;
        MaxRadiation = maxRadiation;
        CompressionThreshold = compressionThreshold;
        CollapseTimeRemaining = collapseTimeRemaining;
        Projectors = projectors;
        Aftermath = aftermath;
    }
}

[NetSerializable, Serializable]
public sealed class ContainmentToggleStabilizerMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public ContainmentToggleStabilizerMessage(int index)
    {
        Index = index;
    }
}

[NetSerializable, Serializable]
public sealed class ContainmentSetProjectorIntensityMessage : BoundUserInterfaceMessage
{
    public readonly int Index;
    public readonly float Intensity;

    public ContainmentSetProjectorIntensityMessage(int index, float intensity)
    {
        Index = index;
        Intensity = intensity;
    }
}

[NetSerializable, Serializable]
public sealed class ReactorSetPowerMessage : BoundUserInterfaceMessage
{
    public readonly float Power;

    public ReactorSetPowerMessage(float power)
    {
        Power = power;
    }
}

[NetSerializable, Serializable]
public sealed class ReactorEmergencyStopMessage : BoundUserInterfaceMessage
{
    public readonly bool Restart;

    public ReactorEmergencyStopMessage(bool restart)
    {
        Restart = restart;
    }
}

