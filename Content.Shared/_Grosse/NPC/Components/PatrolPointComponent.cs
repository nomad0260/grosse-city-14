using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.NPC;

namespace Content.Shared._Grosse.NPC.Components;

/// <summary>
/// Компонент для точки патруля
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PatrolPointComponent : Component
{
    /// <summary>
    /// Группа патруля (например "security", "janitor")
    /// </summary>
    [DataField("group"), ViewVariables(VVAccess.ReadWrite)]
    public string Group = "default";

    /// <summary>
    /// Альтернативное название (используется в PatrolOperator)
    /// </summary>
    [DataField("patrolGroup"), ViewVariables(VVAccess.ReadWrite)]
    public string PatrolGroup
    {
        get => Group;
        set => Group = value;
    }

    /// <summary>
    /// Название маршрута (например "route_a", "route_b").
    /// Точки с одинаковым route образуют отдельный маршрут внутри группы.
    /// Если пусто - точка доступна для любого маршрута группы.
    /// </summary>
    [DataField("route"), ViewVariables(VVAccess.ReadWrite)]
    public string? Route = null;

    /// <summary>
    /// Порядковый номер точки в маршруте
    /// </summary>
    [DataField("order"), ViewVariables(VVAccess.ReadWrite)]
    public int? Order = null;

    /// <summary>
    /// Сколько секунд ждать на этой точке
    /// </summary>
    [DataField("waitTime"), ViewVariables(VVAccess.ReadWrite)]
    public float WaitTime = 2.0f;

    /// <summary>
    /// Дистанция прибытия к точке
    /// </summary>
    [DataField("arrivalRange"), ViewVariables(VVAccess.ReadWrite)]
    public float ArrivalRange = 0.5f;

    /// <summary>
    /// Осматриваться по сторонам
    /// </summary>
    [DataField("lookAround"), ViewVariables(VVAccess.ReadWrite)]
    public bool LookAround = false;

    /// <summary>
    /// Максимальное количество NPC, которые могут одновременно занимать эту точку
    /// </summary>
    [DataField("capacity"), ViewVariables(VVAccess.ReadWrite)]
    public int Capacity = 1;

    /// <summary>
    /// Слоты точки (управляется системой, не задаётся в YAML)
    /// НЕ синхронизируется с клиентом - только серверная логика
    /// </summary>
    [ViewVariables]
    public List<PointSlot> Slots = new();

    /// <summary>
    /// Время кулдауна после освобождения точки (секунды)
    /// </summary>
    [DataField("cooldownTime"), ViewVariables(VVAccess.ReadWrite)]
    public float CooldownTime = 1.0f;

    /// <summary>
    /// Таймаут резервации - если NPC не пришёл за это время, резервация снимается (секунды)
    /// </summary>
    [DataField("reservationTimeout"), ViewVariables(VVAccess.ReadWrite)]
    public float ReservationTimeout = 30.0f;

    /// <summary>
    /// Приоритет точки (больше = важнее)
    /// </summary>
    [DataField("priority"), ViewVariables(VVAccess.ReadWrite)]
    public int Priority = 0;
}
