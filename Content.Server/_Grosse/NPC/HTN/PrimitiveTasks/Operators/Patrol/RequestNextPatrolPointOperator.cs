using System.Threading;
using System.Threading.Tasks;
using Content.Server._Grosse.NPC.Components;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared._Grosse.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Запрашивает у <see cref="NpcPointManagerSystem"/> следующую точку патруля.
/// Планирование полностью синхронное: никаких задержек и ожиданий в Plan —
/// иначе один NPC останавливает планирование всех остальных.
/// </summary>
public sealed partial class RequestNextPatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        if (string.IsNullOrEmpty(tracker.PatrolRoute))
            tracker.PatrolRoute = _pointManager.AutoAssignRoute(owner, tracker.PatrolGroup);

        var nextPoint = _pointManager.TryClaimNextPatrolPoint(
            owner,
            tracker.PatrolGroup,
            tracker.CurrentPatrolPoint,
            tracker.PatrolRoute);

        if (nextPoint is not { } point ||
            !_entManager.TryGetComponent<TransformComponent>(point, out var xform))
        {
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));
        }

        tracker.CurrentPatrolPoint = point;

        var arrivalRange = 0.5f;
        var waitTime = 1f;

        if (_entManager.TryGetComponent<PatrolPointComponent>(point, out var pointComponent))
        {
            arrivalRange = pointComponent.ArrivalRange;
            waitTime = pointComponent.WaitTime;
        }

        // Свой слот на точке: при capacity > 1 NPC расходятся по округе.
        var targetCoordinates = xform.Coordinates;

        if (_pointManager.TryGetSlotOffset(point, owner, out var slotOffset))
            targetCoordinates = targetCoordinates.Offset(slotOffset);

        var effects = new Dictionary<string, object>
        {
            { "PatrolTarget", point },
            { "TargetCoordinates", targetCoordinates },
            { "MovementRange", arrivalRange },
            { "PatrolWaitTime", waitTime },
        };

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, effects));
    }
}
