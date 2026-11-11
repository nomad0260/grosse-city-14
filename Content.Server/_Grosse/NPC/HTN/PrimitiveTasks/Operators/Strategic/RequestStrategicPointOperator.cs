using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Strategic;

/// <summary>
/// Ищет активную стратегическую точку через <see cref="NpcPointManagerSystem"/>,
/// резервирует слот и передаёт координаты слота (а не центра точки) в
/// blackboard для MoveToOperator, чтобы NPC расходились по округе.
/// </summary>
public sealed partial class RequestStrategicPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private NpcPointManagerSystem _pointManager = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedMapSystem _mapSystem = default!;

    // Операторы-дефинишены создаются вне IoC-контекста, поэтому сэвмилл
    // нельзя получать в инициализаторе поля — только в Initialize.
    private ISawmill _sawmill = default!;

    // Не даём диагностике спамить лог при каждом перепланировании.
    private TimeSpan _nextDiagnostics;

    /// <summary>
    /// Фракция для фильтра точек. Если у NPC есть NpcPointTracker с фракцией,
    /// значение трекера имеет приоритет.
    /// </summary>
    [DataField("faction")]
    public string? Faction;

    /// <summary>
    /// Теги для фильтра точек. Если у NPC есть NpcPointTracker с тегами,
    /// теги трекера имеют приоритет.
    /// </summary>
    [DataField("tags")]
    public HashSet<string> Tags = new();

    /// <summary>
    /// Ключ, в котором хранится EntityUid выбранной точки.
    /// </summary>
    [DataField("targetKey")]
    public string TargetKey = "StrategicTarget";

    /// <summary>
    /// Ключ координат для MoveToOperator.
    /// </summary>
    [DataField("coordinatesKey")]
    public string CoordinatesKey = "TargetCoordinates";

    /// <summary>
    /// Ключ дистанции прибытия для MoveToOperator.
    /// </summary>
    [DataField("rangeKey")]
    public string RangeKey = "MovementRange";

    /// <summary>
    /// Ключ времени удержания для WaitOperator.
    /// </summary>
    [DataField("waitTimeKey")]
    public string WaitTimeKey = "StrategicWaitTime";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _mapSystem = sysManager.GetEntitySystem<SharedMapSystem>();
        _sawmill = _logManager.GetSawmill("npc.strategic");
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Стратегические точки доступны только NPC, которые явно включили
        // это поведение через NpcPointTracker. Без компонента общие
        // compound-ветки (например, SimpleHostileCompound) заставили бы
        // любых NPC идти к точке.
        if (!_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
            return (false, null);

        var faction = string.IsNullOrEmpty(tracker.Faction) ? Faction : tracker.Faction;
        var tags = tracker.StrategicTags.Count > 0 ? tracker.StrategicTags : Tags;

        if (tags.Count == 0)
            return (false, null);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var ownerXform))
            return (false, null);

        var candidates = _pointManager.GetStrategicPointCandidates(owner, faction, tags);

        if (candidates.Count == 0)
        {
            if (_timing.CurTime >= _nextDiagnostics)
            {
                _nextDiagnostics = _timing.CurTime + TimeSpan.FromSeconds(10);
                _sawmill.Info($"NPC {owner}: нет доступных стратегических точек (faction='{faction}', tags={string.Join(",", tags)}); " +
                              _pointManager.DescribeStrategicPoints(owner, faction, tags));
            }

            return (false, null);
        }

        // Перебираем точки по убыванию приоритета: первая достижимая и будет целью.
        // Без этой проверки NPC резервировал недостижимую точку и просто стоял:
        // MoveToOperator не мог построить путь, план падал и перепланировался
        // на ту же самую точку.
        foreach (var (pointUid, pointComp) in candidates)
        {
            if (!_entManager.TryGetComponent<TransformComponent>(pointUid, out var pointXform))
                continue;

            if (!CanReachMap(ownerXform, pointXform))
            {
                _sawmill.Debug($"NPC {owner}: точка {pointUid} на другой карте/Z-уровне без перехода, пропуск");
                continue;
            }

            if (!_pointManager.TryReserveStrategicPoint(pointUid, owner))
                continue;

            // Свой слот на точке: NPC встают врозь вокруг центра, а не в одну точку.
            _pointManager.TryGetSlotOffset(pointUid, owner, out var slotOffset);
            var targetCoords = pointXform.Coordinates.Offset(slotOffset);

            PathResultEvent? path = null;

            // Проверяем путь только внутри одной карты. Переход между Z-уровнями
            // одной сети выполняет MoveToOperator через лестницы.
            if (pointXform.MapID == ownerXform.MapID)
            {
                var pathResult = await _pathfind.GetPath(
                    owner,
                    ownerXform.Coordinates,
                    targetCoords,
                    pointComp.ArrivalRange,
                    cancelToken,
                    _pathfind.GetFlags(blackboard));

                if (pathResult.Result != PathResult.Path)
                {
                    _pointManager.ReleaseStrategicPoint(pointUid, owner);
                    _sawmill.Debug($"NPC {owner}: к точке {pointUid} нет пути ({pathResult.Result}), пропуск");
                    continue;
                }

                path = pathResult;
            }

            // При перепланировании освобождаем предыдущую точку, чтобы не занимать
            // слот до истечения reservationTimeout.
            if (tracker.CurrentStrategicPoint is { } previous && previous != pointUid)
                _pointManager.ReleaseStrategicPoint(previous, owner);

            tracker.CurrentStrategicPoint = pointUid;

            var effects = new Dictionary<string, object>
            {
                { TargetKey, pointUid },
                { CoordinatesKey, targetCoords },
                { RangeKey, pointComp.ArrivalRange },
                { WaitTimeKey, pointComp.WaitTime },
            };

            // Передаём готовый путь, чтобы MoveToOperator не искал его заново
            // и сразу знал, какие препятствия на маршруте нужно сломать.
            if (path != null)
            {
                effects[NPCBlackboard.PathfindKey] = path;
                effects[NPCBlackboard.OwnerCoordinates] = targetCoords;
            }

            _sawmill.Debug($"NPC {owner}: выбрана стратегическая точка {pointUid} (tags={string.Join(",", pointComp.Tags)}, offset={slotOffset})");
            return (true, effects);
        }

        return (false, null);
    }

    /// <summary>
    /// Точка на другой карте имеет смысл только если это Z-уровень той же сети
    /// (переход через лестницу обрабатывает MoveToOperator).
    /// </summary>
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
