using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Robust.Shared.Log;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Начинает фазу наблюдения перед закапыванием (когда пришло время)
/// </summary>
public sealed partial class StartObservingOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
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
        {
            Logger.Warning($"[START-OBSERVING] Entity {owner}: No BurrowableComponent!");
            return HTNOperatorStatus.Failed;
        }

        Logger.Info($"[START-OBSERVING] Entity {owner}: Current state={component.State}");

        // Проверяем пора ли закапываться (патрулировал достаточно долго?)
        if (!_burrowSystem.ShouldBurrow(owner, component))
        {
            Logger.Info($"[START-OBSERVING] Entity {owner}: ShouldBurrow returned FALSE. Will continue patrolling.");
            return HTNOperatorStatus.Failed; // Ещё рано, продолжаем патрулировать
        }

        // Пытаемся начать наблюдение
        Logger.Info($"[START-OBSERVING] Entity {owner}: ShouldBurrow returned TRUE! Trying to start observing...");
        if (_burrowSystem.TryStartObserving(owner, component))
        {
            Logger.Info($"[START-OBSERVING] Entity {owner}: Successfully started observing!");
            return HTNOperatorStatus.Finished;
        }

        Logger.Warning($"[START-OBSERVING] Entity {owner}: Failed to start observing!");
        return HTNOperatorStatus.Failed;
    }
}
