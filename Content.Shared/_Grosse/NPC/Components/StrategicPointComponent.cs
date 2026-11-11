using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.NPC;

namespace Content.Shared._Grosse.NPC.Components;

/// <summary>
/// Компонент для стратегической точки (штурм/удержание)
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StrategicPointComponent : Component
{
    /// <summary>
    /// Теги точки для выбора (например "city_assault", "defense_position", "vip_location")
    /// </summary>
    [DataField("tags")]
    public HashSet<string> Tags = new();

    /// <summary>
    /// Фракция, которая может использовать эту точку
    /// </summary>
    [DataField("faction")]
    public string? Faction = null;

    /// <summary>
    /// Приоритет точки (больше = важнее)
    /// </summary>
    [DataField("priority")]
    public int Priority = 0;

    /// <summary>
    /// Максимальное количество NPC, которые могут одновременно занимать эту точку
    /// </summary>
    [DataField("capacity")]
    public int Capacity = 4;

    /// <summary>
    /// Сколько секунд NPC удерживает точку после прибытия.
    /// </summary>
    [DataField("waitTime"), ViewVariables(VVAccess.ReadWrite)]
    public float WaitTime = 5.0f;

    /// <summary>
    /// Дистанция, на которой NPC считается прибывшим к точке.
    /// </summary>
    [DataField("arrivalRange"), ViewVariables(VVAccess.ReadWrite)]
    public float ArrivalRange = 0.6f;

    /// <summary>
    /// Радиус, в котором раскладываются позиции слотов. NPC занимают разные
    /// точки вокруг центра, а не толпятся на одном тайле.
    /// </summary>
    [DataField("spreadRadius"), ViewVariables(VVAccess.ReadWrite)]
    public float SpreadRadius = 3.0f;

    /// <summary>
    /// Слоты точки (управляется системой)
    /// НЕ синхронизируется с клиентом - только серверная логика
    /// </summary>
    [ViewVariables]
    public List<PointSlot> Slots = new();

    /// <summary>
    /// Время кулдауна после освобождения (секунды)
    /// </summary>
    [DataField("cooldownTime")]
    public float CooldownTime = 2.0f;

    /// <summary>
    /// Таймаут резервации (секунды)
    /// </summary>
    [DataField("reservationTimeout")]
    public float ReservationTimeout = 60.0f;

    /// <summary>
    /// Активна ли точка (например, во время ивента).
    /// Точки с active: false не выбираются HTN-оператором.
    /// </summary>
    [DataField("active"), ViewVariables(VVAccess.ReadWrite)]
    public bool IsActive = false;

    /// <summary>
    /// Дополнительный множитель приоритета во время активности
    /// </summary>
    [DataField("activePriorityMultiplier")]
    public float ActivePriorityMultiplier = 2.0f;
}
