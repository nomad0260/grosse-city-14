using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Запрашивает следующую точку патруля у менеджера
/// </summary>
public sealed partial class RequestNextPatrolPointOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private NpcPointManagerSystem _pointManager = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
        _sawmill = Logger.GetSawmill("npc.patrol");
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Проверяем наличие трекера
        if (!_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
        {
            return (false, null);
        }

        // Автоматически выбираем маршрут, если не задан
        if (string.IsNullOrEmpty(tracker.PatrolRoute))
        {
            tracker.PatrolRoute = _pointManager.AutoAssignRoute(owner, tracker.PatrolGroup);
        }

        // Запрашиваем следующую точку у менеджера
        var nextPoint = _pointManager.TryClaimNextPatrolPoint(
            owner,
            tracker.PatrolGroup,
            tracker.CurrentPatrolPoint,
            tracker.PatrolRoute);

        if (nextPoint == null)
        {
            return (false, null);
        }

        // Обновляем трекер
        tracker.CurrentPatrolPoint = nextPoint;
        tracker.LastPointRequestTime = _timing.CurTime;

        // Получаем координаты точки
        if (!_entManager.TryGetComponent<TransformComponent>(nextPoint.Value, out var xform))
        {
            return (false, null);
        }

        // Возвращаем эффекты для blackboard
        var effects = new Dictionary<string, object>
        {
            { "TargetCoordinates", xform.Coordinates },
            { "PatrolTarget", nextPoint.Value }
        };

        _sawmill.Info($"RequestNextPatrolPointOperator.Plan() returning Valid=true with effects: TargetCoordinates={xform.Coordinates}, PatrolTarget={nextPoint.Value}");
        return (true, effects);
    }
}
