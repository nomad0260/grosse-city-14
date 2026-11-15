using Content.Server._Grosse.NPC.Components;
using Content.Server._Grosse.NPC.Systems;
using Robust.Shared.Timing;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Закапывает NPC из состояния <see cref="BurrowState.Observing"/>.
/// <para>
/// Операторы HTN — общий экземпляр на прототип, а не на NPC. Поэтому таймер
/// попытки живёт в blackboard конкретного NPC: раньше он был полем оператора,
/// и один зверь перезаписывал таймер всем остальным.
/// </para>
/// </summary>
public sealed partial class BurrowOperator : HTNOperator
{
    private const string AttemptKey = "BurrowAttemptStart";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private BurrowableSystem _burrowSystem = default!;

    /// <summary>
    /// Сколько секунд пробуем закопаться, прежде чем признать поверхность неподходящей.
    /// </summary>
    private const float AttemptTimeout = 5f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _burrowSystem = sysManager.GetEntitySystem<BurrowableSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.HasComponent<BurrowableComponent>(owner))
            return HTNOperatorStatus.Failed;

        if (_burrowSystem.TryBurrow(owner))
        {
            blackboard.Remove<float>(AttemptKey);
            return HTNOperatorStatus.Finished;
        }

        var now = (float)_timing.CurTime.TotalSeconds;

        if (!blackboard.TryGetValue<float>(AttemptKey, out var startedAt, _entityManager))
        {
            blackboard.SetValue(AttemptKey, now);
            return HTNOperatorStatus.Continuing;
        }

        if (now - startedAt <= AttemptTimeout)
            return HTNOperatorStatus.Continuing;

        // Поверхность не подходит: возвращаемся к патрулю с новым таймером.
        blackboard.Remove<float>(AttemptKey);
        _burrowSystem.TransitionToActive(owner, resetPatrolTimer: true);
        return HTNOperatorStatus.Failed;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<float>(AttemptKey);
    }
}
