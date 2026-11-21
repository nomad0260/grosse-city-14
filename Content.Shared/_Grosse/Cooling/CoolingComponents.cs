using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Cooling;

/// <summary>
/// Тепловые параметры оборудования реактора. Перегрев снижает эффективность конкретного устройства.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThermalEquipmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Label = "оборудование";

    [DataField, AutoNetworkedField]
    public float Temperature = 293f;

    [DataField]
    public float AmbientTemperature = 293f;

    [DataField]
    public float NominalTemperature = 330f;

    [DataField]
    public float MaxTemperature = 430f;

    /// <summary>Тепловыделение при полной нагрузке, единиц тепла в секунду.</summary>
    [DataField]
    public float HeatGeneration = 150f;

    /// <summary>Тепловая инерция: чем выше, тем медленнее меняется температура.</summary>
    [DataField]
    public float ThermalMass = 600f;

    /// <summary>Пассивная теплоотдача в среду.</summary>
    [DataField]
    public float PassiveLoss = 0.8f;

    /// <summary>Нагрузка 0..1, выставляется реакторными системами.</summary>
    [DataField, AutoNetworkedField]
    public float Load;

    /// <summary>Тепло, отведённое охладителями за последний тик.</summary>
    [DataField, AutoNetworkedField]
    public float HeatRemoved;

    [DataField, AutoNetworkedField]
    public float Efficiency = 1f;

    [DataField, AutoNetworkedField]
    public bool Overheated;
}
