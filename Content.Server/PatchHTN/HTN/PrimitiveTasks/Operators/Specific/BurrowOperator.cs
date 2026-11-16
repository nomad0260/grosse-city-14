using System.Threading;
using System.Threading.Tasks;
using Content.Server.PatchHTN.Components;
using Content.Server.PatchHTN.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server.PatchHTN.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Buries the NPC (see <see cref="BurrowComponent"/>). The NPC resurfaces when the plan shuts down,
/// so this is meant to be used together with a wait task in the same plan.
/// </summary>
public sealed partial class BurrowOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;

    private BurrowSystem _burrow = default!;
    private NPCIdleTrackingSystem _idle = default!;

    /// <summary>
    /// Only run the shutdown logic when the whole plan ends: the NPC has to stay buried while the
    /// rest of the plan (e.g. the wait task) runs.
    /// </summary>
    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.PlanFinished;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _burrow = sysManager.GetEntitySystem<BurrowSystem>();
        _idle = sysManager.GetEntitySystem<NPCIdleTrackingSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var valid = _entManager.TryGetComponent<BurrowComponent>(owner, out var burrow) &&
                    !burrow.Burrowed &&
                    _burrow.CanBurrowAt(owner, burrow);

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((valid, null));
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<BurrowComponent>(owner, out var burrow) ||
            !_burrow.TryBurrow(owner, burrow))
        {
            return HTNOperatorStatus.Failed;
        }

        return HTNOperatorStatus.Finished;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<BurrowComponent>(owner, out var burrow))
            _burrow.TrySurface(owner, burrow);

        // Don't dig in again as soon as we resurface; let the NPC do something first.
        _idle.ResetIdleTime(owner);
    }
}
