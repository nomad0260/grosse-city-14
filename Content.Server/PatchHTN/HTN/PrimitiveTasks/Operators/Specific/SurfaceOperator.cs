using Content.Server.PatchHTN.Components;
using Content.Server.PatchHTN.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server.PatchHTN.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Makes a buried NPC (see <see cref="BurrowComponent"/>) resurface.
/// </summary>
public sealed partial class SurfaceOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private BurrowSystem _burrow = default!;
    private NPCIdleTrackingSystem _idle = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _burrow = sysManager.GetEntitySystem<BurrowSystem>();
        _idle = sysManager.GetEntitySystem<NPCIdleTrackingSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<BurrowComponent>(owner, out var burrow))
            return HTNOperatorStatus.Failed;

        _burrow.TrySurface(owner, burrow);
        _idle.ResetIdleTime(owner);

        return HTNOperatorStatus.Finished;
    }
}
