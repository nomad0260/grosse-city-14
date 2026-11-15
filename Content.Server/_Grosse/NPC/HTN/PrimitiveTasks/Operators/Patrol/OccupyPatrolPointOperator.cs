using System.Threading;
using System.Threading.Tasks;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Grosse.NPC.Components;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Отмечает, что NPC дошёл до патрульной точки, и передаёт время ожидания
/// в blackboard для <c>WaitOperator</c>.
/// <para>
/// Раньше ожидание было реализовано через <c>Task.Delay</c> прямо в <c>Plan</c>:
/// планировщик HTN — общий на всех NPC, поэтому на время задержки вставали все.
/// Теперь ожидание делает обычный <c>WaitOperator</c> в ветке, а этот оператор
/// только занимает слот.
/// </para>
/// </summary>
public sealed partial class OccupyPatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    [DataField("targetKey")]
    public string TargetKey = "PatrolTarget";

    [DataField("waitTimeKey")]
    public string WaitTimeKey = "PatrolWaitTime";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        if (!_entManager.HasComponent<PatrolPointComponent>(target))
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, null));
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            _pointManager.OccupySlot(target, owner);
    }
}
