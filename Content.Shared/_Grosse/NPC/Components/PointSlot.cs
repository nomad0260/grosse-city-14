using System.Numerics;
using Robust.Shared.Serialization;
using Content.Shared.NPC;

namespace Content.Shared._Grosse.NPC.Components;

/// <summary>
/// Состояние точки или слота
/// </summary>
[Serializable, NetSerializable]
public enum PointSlotState : byte
{
    /// <summary>
    /// Свободна, можно занимать
    /// </summary>
    Free = 0,

    /// <summary>
    /// Зарезервирована конкретным NPC, но он ещё не пришёл
    /// </summary>
    Reserved = 1,

    /// <summary>
    /// NPC стоит на точке
    /// </summary>
    Occupied = 2,

    /// <summary>
    /// Недавно освободилась, временно недоступна (анти-дёрганье)
    /// </summary>
    Cooldown = 3,

    /// <summary>
    /// Недостижима/сломана, временно исключена
    /// </summary>
    Blocked = 4
}

/// <summary>
/// Слот на точке - может быть зарезервирован или занят NPC
/// Только серверная логика, НЕ синхронизируется с клиентом!
/// </summary>
[Serializable]
public sealed class PointSlot
{
    /// <summary>
    /// Индекс слота (0, 1, 2...)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Текущее состояние слота
    /// </summary>
    public PointSlotState State { get; set; } = PointSlotState.Free;

    /// <summary>
    /// NPC, который владеет этим слотом (зарезервировал или занял)
    /// </summary>
    public EntityUid? Owner { get; set; }

    /// <summary>
    /// Время истечения резервации/кулдауна
    /// </summary>
    public TimeSpan ExpiresAt { get; set; }

    /// <summary>
    /// Время начала резервации/занятия
    /// </summary>
    public TimeSpan StartedAt { get; set; }

    /// <summary>
    /// Позиция слота относительно центра точки. NPC расходятся по округе,
    /// а не встают все в одну точку.
    /// </summary>
    public Vector2 Offset { get; set; } = Vector2.Zero;

    public PointSlot(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Свободен ли слот для занятия
    /// </summary>
    public bool IsFree() => State == PointSlotState.Free;

    /// <summary>
    /// Зарезервирован ли слот
    /// </summary>
    public bool IsReserved() => State == PointSlotState.Reserved;

    /// <summary>
    /// Занят ли слот
    /// </summary>
    public bool IsOccupied() => State == PointSlotState.Occupied;

    /// <summary>
    /// Принадлежит ли слот данному NPC
    /// </summary>
    public bool IsOwnedBy(EntityUid npc) => Owner == npc;
}
