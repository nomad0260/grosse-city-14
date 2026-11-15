using Content.Server._Grosse.NPC.Components;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Переводит NPC в фазу наблюдения, если он уже достаточно долго патрулировал.
/// Если время ещё не пришло — ветка просто уступает место патрулю.
/// </summary>
public sealed partial class StartObservingOperator : HTNOperator
{
    [Dependency] private IEntityManager _entityManager = default!;

    private BurrowableSystem _burrowSystem = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _burrowSystem = sysManager.GetEntitySystem<BurrowableSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.TryGetComponent<BurrowableComponent>(owner, out var component))
            return HTNOperatorStatus.Failed;

        if (!_burrowSystem.ShouldBurrow(owner, component))
            return HTNOperatorStatus.Failed;

        return _burrowSystem.TryStartObserving(owner, component)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }
}
