using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Находит следующую патрульную точку из группы и строит путь к ней.
/// Если точки имеют Order - идёт по порядку, иначе выбирает случайно.
/// </summary>
public sealed partial class PatrolOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    
    private EntityLookupSystem _lookup = default!;
    private PathfindingSystem _pathfinding = default!;
    private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Название группы патруля для поиска точек.
    /// </summary>
    [DataField("patrolGroup", required: true)]
    public string PatrolGroup = "default";

    /// <summary>
    /// Максимальная дальность поиска патрульных точек от текущей позиции.
    /// </summary>
    [DataField("searchRange")]
    public float SearchRange = 100f;

    /// <summary>
    /// Ключ для хранения EntityUid текущей патрульной точки.
    /// </summary>
    [DataField("targetKey")]
    public string TargetKey = "PatrolTarget";

    /// <summary>
    /// Ключ для координат патрульной точки (используется MoveToOperator).
    /// </summary>
    [DataField("coordinatesKey")]
    public string CoordinatesKey = "TargetCoordinates";

    /// <summary>
    /// Ключ для хранения времени ожидания на точке.
    /// </summary>
    [DataField("waitTimeKey")]
    public string WaitTimeKey = "PatrolWaitTime";

    /// <summary>
    /// Ключ для хранения текущего порядкового номера (для упорядоченного патруля).
    /// </summary>
    private const string CurrentOrderKey = "CurrentPatrolOrder";

    /// <summary>
    /// Ключ где хранится результат pathfinding.
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var ownerXform))
            return (false, null);

        // 1. Поиск всех патрульных точек в радиусе
        var patrolPoints = new List<(EntityUid Uid, PatrolPointComponent Comp, float Distance)>();
        var ownerPos = _transform.GetWorldPosition(ownerXform);

        foreach (var entity in _lookup.GetEntitiesInRange(ownerXform.Coordinates, SearchRange))
        {
            if (!_entManager.TryGetComponent<PatrolPointComponent>(entity, out var point))
                continue;

            // Фильтруем по группе патруля
            if (point.PatrolGroup != PatrolGroup)
                continue;

            if (!_entManager.TryGetComponent<TransformComponent>(entity, out var pointXform))
                continue;

            var distance = (_transform.GetWorldPosition(pointXform) - ownerPos).Length();
            patrolPoints.Add((entity, point, distance));
        }

        if (patrolPoints.Count == 0)
            return (false, null); // Нет доступных патрульных точек

        // 2. Определяем режим: упорядоченный или случайный
        var hasOrders = patrolPoints.Any(p => p.Comp.Order != null);
        EntityUid nextPointUid;
        PatrolPointComponent nextPoint;

        if (hasOrders)
        {
            // Упорядоченный патруль
            var currentOrder = blackboard.GetValueOrDefault<int?>(CurrentOrderKey, _entManager);
            var currentTarget = blackboard.GetValueOrDefault<EntityUid?>(TargetKey, _entManager);

            if (currentOrder == null || currentTarget == null)
            {
                // Первый раз - идём к ближайшей точке
                var nearest = patrolPoints.OrderBy(p => p.Distance).First();
                nextPointUid = nearest.Uid;
                nextPoint = nearest.Comp;
            }
            else
            {
                // Ищем следующую точку по порядку
                var nextOrdered = patrolPoints
                    .Where(p => p.Comp.Order != null && p.Comp.Order > currentOrder)
                    .OrderBy(p => p.Comp.Order)
                    .FirstOrDefault();

                if (nextOrdered == default)
                {
                    // Цикл завершён, начинаем с начала
                    nextOrdered = patrolPoints
                        .Where(p => p.Comp.Order != null)
                        .OrderBy(p => p.Comp.Order)
                        .FirstOrDefault();
                }

                if (nextOrdered == default)
                {
                    // Что-то пошло не так, берём ближайшую
                    nextOrdered = patrolPoints.OrderBy(p => p.Distance).First();
                }

                nextPointUid = nextOrdered.Uid;
                nextPoint = nextOrdered.Comp;
            }
        }
        else
        {
            // Случайный патруль - выбираем любую точку кроме текущей
            var currentTarget = blackboard.GetValueOrDefault<EntityUid?>(TargetKey, _entManager);
            var available = patrolPoints.Where(p => p.Uid != currentTarget).ToList();

            if (available.Count == 0)
                available = patrolPoints; // Если только одна точка или текущая не найдена

            var randomPick = _random.Pick(available);
            nextPointUid = randomPick.Uid;
            nextPoint = randomPick.Comp;
        }

        // 3. Получаем координаты выбранной точки
        if (!_entManager.TryGetComponent<TransformComponent>(nextPointUid, out var targetXform))
            return (false, null);

        var targetCoords = targetXform.Coordinates;

        // 4. Строим путь к точке
        var pathResult = await _pathfinding.GetPath(
            owner,
            ownerXform.Coordinates,
            targetCoords,
            nextPoint.ArrivalRange,
            cancelToken,
            _pathfinding.GetFlags(blackboard));

        if (pathResult.Result != PathResult.Path)
            return (false, null); // Не можем построить путь к этой точке

        // 5. Формируем эффекты для blackboard
        var effects = new Dictionary<string, object>
        {
            { TargetKey, nextPointUid },
            { CoordinatesKey, targetCoords },
            { WaitTimeKey, nextPoint.WaitTime },
            { PathfindKey, pathResult },
            { NPCBlackboard.OwnerCoordinates, targetCoords }
        };

        // Сохраняем текущий порядок для следующей итерации
        if (nextPoint.Order != null)
            effects[CurrentOrderKey] = nextPoint.Order.Value;

        return (true, effects);
    }
}
