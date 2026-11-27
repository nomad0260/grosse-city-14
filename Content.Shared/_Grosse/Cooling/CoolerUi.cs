using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Cooling;

[NetSerializable, Serializable]
public enum CoolerUiKey
{
    Key,
}

/// <summary>
/// Имена ячеек охладителя. Совпадают с ключами ItemSlots в прототипе.
/// </summary>
public static class CoolerSlots
{
    public const string Coolant = "coolantSlot";
    public const string Waste = "wasteSlot";
}

/// <summary>
/// Что делает кнопка интерфейса охладителя.
/// </summary>
public enum CoolerTransferMode : byte
{
    /// <summary>Из баллона в ячейке хладагента в бак охладителя.</summary>
    Fill,

    /// <summary>Только отработанная жидкость из бака в тару.</summary>
    DrainWaste,

    /// <summary>Всё содержимое бака в тару.</summary>
    DrainAll,
}

/// <summary>
/// Содержимое ячейки с жидкостью: сколько сейчас и сколько влезает.
/// </summary>
[NetSerializable, Serializable]
public sealed class FluidSlotState
{
    public readonly float Volume;
    public readonly float MaxVolume;

    public FluidSlotState(float volume, float maxVolume)
    {
        Volume = volume;
        MaxVolume = maxVolume;
    }
}

/// <summary>
/// Телеметрия бака охладителя и содержимое его ячеек.
/// </summary>
[NetSerializable, Serializable]
public sealed class CoolerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly float Volume;
    public readonly float MaxVolume;
    public readonly float Waste;
    public readonly float MaxWaste;

    /// <summary>Идентификатор доминирующего теплоносителя или пустая строка, если бак пуст.</summary>
    public readonly string Coolant;

    /// <summary>Текущий теплоотвод блока, единиц тепла в секунду.</summary>
    public readonly float CoolingCapacity;

    public readonly NetEntity? CoolantItem;
    public readonly NetEntity? WasteItem;

    /// <summary>Заполнение баллона в ячейке хладагента; null — ячейка пуста.</summary>
    public readonly FluidSlotState? CoolantSlot;

    /// <summary>Заполнение тары в ячейке отработки; null — ячейка пуста.</summary>
    public readonly FluidSlotState? WasteSlot;

    public CoolerBoundUserInterfaceState(
        float volume,
        float maxVolume,
        float waste,
        float maxWaste,
        string coolant,
        float coolingCapacity,
        NetEntity? coolantItem,
        NetEntity? wasteItem,
        FluidSlotState? coolantSlot,
        FluidSlotState? wasteSlot)
    {
        Volume = volume;
        MaxVolume = maxVolume;
        Waste = waste;
        MaxWaste = maxWaste;
        Coolant = coolant;
        CoolingCapacity = coolingCapacity;
        CoolantItem = coolantItem;
        WasteItem = wasteItem;
        CoolantSlot = coolantSlot;
        WasteSlot = wasteSlot;
    }
}

[NetSerializable, Serializable]
public sealed class CoolerTransferMessage : BoundUserInterfaceMessage
{
    public readonly CoolerTransferMode Mode;

    public CoolerTransferMessage(CoolerTransferMode mode)
    {
        Mode = mode;
    }
}
