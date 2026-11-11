using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Освобождает текущую точку патруля
/// </summary>
public sealed partial class ReleasePatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

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
