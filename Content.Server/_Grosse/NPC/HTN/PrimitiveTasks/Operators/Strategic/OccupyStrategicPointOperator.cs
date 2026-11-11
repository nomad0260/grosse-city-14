using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Strategic;

/// <summary>
/// Занимает стратегическую точку после прибытия и передаёт время удержания
/// в blackboard для WaitOperator.
/// </summary>
public sealed partial class OccupyStrategicPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    [DataField("targetKey")]
    public string TargetKey = "StrategicTarget";

    [DataField("waitTimeKey")]
    public string WaitTimeKey = "StrategicWaitTime";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.TryGetComponent<StrategicPointComponent>(target, out var point))
        {
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));
        }

        var effects = new Dictionary<string, object>
        {
            { WaitTimeKey, point.WaitTime },
        };

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, effects));
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            _pointManager.OccupySlot(target, owner);
    }
}
