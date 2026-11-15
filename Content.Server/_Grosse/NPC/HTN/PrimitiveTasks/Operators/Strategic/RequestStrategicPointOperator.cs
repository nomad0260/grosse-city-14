using System.Threading;
using System.Threading.Tasks;
using Content.Server._Grosse.NPC.Components;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared._Grosse.NPC.Components;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Robust.Shared.Map;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Strategic;

/// <summary>
/// Выбирает ближайшую по приоритету достижимую стратегическую точку и резервирует
/// на ней слот для этого NPC.
/// <para>
/// Планирование синхронное: поиск пути здесь не выполняется, его делает
/// <c>MoveToOperator</c> при запуске задачи. Раньше оператор ждал pathfinding
/// внутри <c>Plan</c>, из-за чего остальные NPC ждали освобождения планировщика.
/// </para>
/// </summary>
public sealed partial class RequestStrategicPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;
    private SharedMapSystem _mapSystem = default!;

    /// <summary>
    /// Фракция для фильтра точек. Значение из <see cref="NpcPointTrackerComponent"/>
    /// имеет приоритет.
    /// </summary>
    [DataField("faction")]
    public string? Faction;

    /// <summary>
    /// Теги для фильтра точек. Теги трекера имеют приоритет.
    /// </summary>
    [DataField("tags")]
    public HashSet<string> Tags = new();

    [DataField("targetKey")]
    public string TargetKey = "StrategicTarget";

    [DataField("coordinatesKey")]
    public string CoordinatesKey = "TargetCoordinates";

    [DataField("rangeKey")]
    public string RangeKey = "MovementRange";

    [DataField("waitTimeKey")]
    public string WaitTimeKey = "StrategicWaitTime";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
        _mapSystem = sysManager.GetEntitySystem<SharedMapSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Стратегические точки доступны только NPC, которым это поведение включили
        // явно: иначе общие ветки HTN утащат к точке любого моба.
        if (!_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        var faction = string.IsNullOrEmpty(tracker.Faction) ? Faction : tracker.Faction;
        var tags = tracker.StrategicTags.Count > 0 ? tracker.StrategicTags : Tags;

        if (tags.Count == 0)
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var ownerXform))
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        var candidates = _pointManager.GetStrategicPointCandidates(owner, faction, tags);

        foreach (var (pointUid, pointComponent) in candidates)
        {
            if (!_entManager.TryGetComponent<TransformComponent>(pointUid, out var pointXform))
                continue;

            // Точка на другой карте достижима только как Z-уровень той же сети:
            // переход между уровнями выполняет MoveToOperator.
            if (!CanReachMap(ownerXform, pointXform))
                continue;

            if (!_pointManager.TryReserveStrategicPoint(pointUid, owner))
                continue;

            _pointManager.TryGetSlotOffset(pointUid, owner, out var slotOffset);
            var targetCoords = pointXform.Coordinates.Offset(slotOffset);

            if (tracker.CurrentStrategicPoint is { } previous && previous != pointUid)
                _pointManager.ReleaseStrategicPoint(previous, owner);

            tracker.CurrentStrategicPoint = pointUid;

            var effects = new Dictionary<string, object>
            {
                { TargetKey, pointUid },
                { CoordinatesKey, targetCoords },
                { RangeKey, pointComponent.ArrivalRange },
                { WaitTimeKey, pointComponent.WaitTime },
            };

            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, effects));
        }

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));
    }

    private bool CanReachMap(TransformComponent ownerXform, TransformComponent pointXform)
    {
        if (ownerXform.MapID == pointXform.MapID)
            return true;

        var ownerMapUid = _mapSystem.GetMap(ownerXform.MapID);
        var pointMapUid = _mapSystem.GetMap(pointXform.MapID);

        return _entManager.TryGetComponent<GrosseZMapComponent>(ownerMapUid, out var ownerZMap) &&
               _entManager.TryGetComponent<GrosseZMapComponent>(pointMapUid, out var pointZMap) &&
               ownerZMap.NetworkUid == pointZMap.NetworkUid;
    }
}
