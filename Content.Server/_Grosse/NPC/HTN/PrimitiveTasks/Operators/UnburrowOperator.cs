using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Выкапывает NPC когда пришло время (после AmbushTime)
/// </summary>
public sealed partial class UnburrowOperator : HTNOperator
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

        // Проверяем пора ли выкапываться
        if (!_burrowSystem.ShouldUnburrow(owner, component))
            return HTNOperatorStatus.Failed; // Ещё рано, продолжаем ждать

        // Выкапываемся и начинаем новый цикл патруля
        _burrowSystem.StartNewPatrolCycle(owner, component);
        return HTNOperatorStatus.Finished;
    }
}
