using Robust.Shared.GameObjects;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Тип патрона эмиттера. Один корпус эмиттера может использовать любой из них.
/// </summary>
public enum DarkCoreCartridgeType : byte
{
    Alpha,
    Beta,
    Gamma,
    Digamma,
}

/// <summary>
/// Сменный патрон эмиттера: коэффициенты луча и расход ресурса.
/// </summary>
[RegisterComponent]
public sealed partial class DarkCoreCartridgeComponent : Component
{
    [DataField]
    public DarkCoreCartridgeType Kind = DarkCoreCartridgeType.Alpha;

    [DataField]
    public float Resource = 10000f;

    [DataField]
    public float MaxResource = 10000f;

    [DataField]
    public float Stabilization = 1f;

    [DataField]
    public float Compression = 0.5f;

    [DataField]
    public float Energy = 1f;

    [DataField]
    public float Heat = 0.5f;

    [DataField]
    public float WearRate = 10f;
}

public static class DarkCoreCartridgeData
{
    public static string GetName(DarkCoreCartridgeType type)
    {
        return type switch
        {
            DarkCoreCartridgeType.Alpha => "Альфа",
            DarkCoreCartridgeType.Beta => "Бета",
            DarkCoreCartridgeType.Gamma => "Гамма",
            DarkCoreCartridgeType.Digamma => "Дигамма",
            _ => type.ToString(),
        };
    }

    public static string GetEntityId(DarkCoreCartridgeType type)
    {
        return type switch
        {
            DarkCoreCartridgeType.Alpha => "DarkCoreCartridgeAlpha",
            DarkCoreCartridgeType.Beta => "DarkCoreCartridgeBeta",
            DarkCoreCartridgeType.Gamma => "DarkCoreCartridgeGamma",
            DarkCoreCartridgeType.Digamma => "DarkCoreCartridgeDigamma",
            _ => "DarkCoreCartridgeAlpha",
        };
    }
}
