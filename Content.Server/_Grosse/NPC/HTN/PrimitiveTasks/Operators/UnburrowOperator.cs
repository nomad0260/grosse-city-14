using Content.Server._Grosse.NPC.Components;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Выкапывает NPC, когда закончилось время засады.
/// <para>
/// Пока время не пришло, оператор возвращает <see cref="HTNOperatorStatus.Continuing"/>.
/// Возврат <c>Failed</c> (как было раньше) обрывал план и запускал перепланирование
/// каждый тик: NPC «замирал» на все 5 минут засады, а вместе с ним простаивал
/// общий планировщик HTN.
/// </para>
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

        // Уже не в засаде (выкопался от урона) — задача выполнена.
        if (component.State != BurrowState.Burrowed)
            return HTNOperatorStatus.Finished;

        if (!_burrowSystem.ShouldUnburrow(owner, component))
            return HTNOperatorStatus.Continuing;

        _burrowSystem.StartNewPatrolCycle(owner, component);
        return HTNOperatorStatus.Finished;
    }
}
