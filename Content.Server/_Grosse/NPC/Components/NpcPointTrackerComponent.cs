using Robust.Shared.GameObjects;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Components;

/// <summary>
/// Компонент для отслеживания текущих точек NPC
/// </summary>
[RegisterComponent]
public sealed partial class NpcPointTrackerComponent : Component
{
    /// <summary>
    /// Текущая патрульная точка (если есть)
    /// </summary>
    [ViewVariables]
    public EntityUid? CurrentPatrolPoint { get; set; }

    /// <summary>
    /// Текущая стратегическая точка (если есть)
    /// </summary>
    [ViewVariables]
    public EntityUid? CurrentStrategicPoint { get; set; }

    /// <summary>
    /// Группа патруля NPC
    /// </summary>
    [DataField("patrolGroup")]
    public string PatrolGroup = "default";

    /// <summary>
    /// Маршрут внутри группы (например "route_a", "route_b").
    /// Если не задан - NPC будет выбран случайный маршрут при первом запросе точки.
    /// </summary>
    [DataField("patrolRoute"), ViewVariables(VVAccess.ReadWrite)]
    public string? PatrolRoute = null;

    /// <summary>
    /// Фракция NPC для стратегических точек
    /// </summary>
    [DataField("faction")]
    public string? Faction = null;

    /// <summary>
    /// Теги для поиска стратегических точек
    /// </summary>
    [DataField("strategicTags")]
    public HashSet<string> StrategicTags = new();

    /// <summary>
    /// Время последнего запроса точки
    /// </summary>
    [ViewVariables]
    public TimeSpan LastPointRequestTime { get; set; }
}
