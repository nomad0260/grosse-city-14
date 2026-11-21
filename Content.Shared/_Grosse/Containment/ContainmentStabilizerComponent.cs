using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Эмиттер стабилизации РТМ. Один корпус, характер луча задаётся патроном.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ContainmentStabilizerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// Интенсивность луча: 0–200%. Выше 100% — форсаж: больше выработки,
    /// тепла, износа корпуса и расхода патрона.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity = 100f;

    /// <summary>Физическая исправность эмиттера.</summary>
    [DataField, AutoNetworkedField]
    public float Integrity = 1f;

    [DataField, AutoNetworkedField]
    public bool HasCartridge;

    [DataField, AutoNetworkedField]
    public DarkCoreCartridgeType CartridgeType = DarkCoreCartridgeType.Alpha;

    [DataField, AutoNetworkedField]
    public float CartridgeResource;

    [DataField, AutoNetworkedField]
    public float CartridgeMaxResource;

    [DataField]
    public float StabilizationCoefficient = 1f;

    [DataField]
    public float CompressionCoefficient = 0.5f;

    [DataField]
    public float EnergyCoefficient = 1f;

    [DataField]
    public float HeatCoefficient = 0.5f;

    [DataField]
    public float CartridgeWearRate = 10f;

    /// <summary>Ядро, к которому привязан эмиттер.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Core;

    [DataField]
    public float LinkRange = 20f;

    [DataField]
    public string BeamPrototype = "DarkCoreStabilizerBeam";

    [ViewVariables] public List<EntityUid> Beams = new();
    [ViewVariables] public List<EntityUid> BeamControllers = new();
    [ViewVariables] public TimeSpan NextRelink;
}
