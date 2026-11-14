using System.Threading;
using System.Threading.Tasks;
using Content.Server._Grosse.NPC.Components;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Освобождает патрульную точку, на которой стоял NPC.
/// </summary>
public sealed partial class ReleasePatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    [DataField("targetKey")]
    public string TargetKey = "PatrolTarget";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        // Освобождение — завершающий шаг: он не должен ломать ветку,
        // даже если точку уже удалили.
        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, null));
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        EntityUid? target = null;

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var targetId, _entManager))
            target = targetId;

        if (_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
        {
            target ??= tracker.CurrentPatrolPoint;
            tracker.CurrentPatrolPoint = null;
        }

        if (target is { } point)
            _pointManager.ReleasePatrolPoint(point, owner);

        blackboard.Remove<EntityUid>(TargetKey);
    }
}
