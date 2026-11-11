using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Освобождает текущую точку патруля
/// </summary>
public sealed partial class ReleasePatrolPointOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Получаем трекер
        if (!_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
            return (true, null); // Продолжаем даже если нет трекера

        // Освобождаем текущую точку
        if (tracker.CurrentPatrolPoint != null)
        {
            _pointManager.ReleasePatrolPoint(tracker.CurrentPatrolPoint.Value, owner);
        }

        return (true, null);
    }
}
