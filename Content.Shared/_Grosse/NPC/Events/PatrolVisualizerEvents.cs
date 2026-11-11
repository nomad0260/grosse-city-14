using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Content.Shared.NPC;

namespace Content.Shared._Grosse.NPC.Events;

/// <summary>
/// Запрос на включение/выключение визуализации патрульных маршрутов.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPatrolVisualizerMessage : EntityEventArgs
{
    /// <summary>
    /// Группа патруля для визуализации. Если null - показать все группы.
    /// </summary>
    public string? PatrolGroup;

    /// <summary>
    /// Включить или выключить визуализацию.
    /// </summary>
    public bool Enabled;
}

/// <summary>
/// Данные о патрульных точках для отрисовки на клиенте.
/// </summary>
[Serializable, NetSerializable]
public sealed class PatrolVisualizerDataMessage : EntityEventArgs
{
    public List<PatrolGroupData> Groups = new();
}

[Serializable, NetSerializable]
public sealed class PatrolGroupData
{
    public string GroupName = string.Empty;
    public List<PatrolPointData> Points = new();
}

[Serializable, NetSerializable]
public sealed class PatrolPointData
{
    public NetEntity Entity;
    public NetCoordinates Coordinates;
    public int? Order;
    public float WaitTime;
    public bool LookAround;
}
