using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Log;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Закапывает NPC (из состояния Observing)
/// </summary>
public sealed partial class BurrowOperator : HTNOperator
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private BurrowableSystem _burrowSystem = default!;
    private TimeSpan? _attemptStartTime = null;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _burrowSystem = sysManager.GetEntitySystem<BurrowableSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        _attemptStartTime = null;
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.HasComponent<BurrowableComponent>(owner))
        {
            Logger.Info($"BurrowOperator: Entity {owner} has no BurrowableComponent");
            return HTNOperatorStatus.Failed;
        }

        Logger.Info($"BurrowOperator: Attempting TryBurrow for {owner}");

        // Пытаемся закопаться
        var result = _burrowSystem.TryBurrow(owner);

        Logger.Info($"BurrowOperator: TryBurrow returned {result} for {owner}");

        if (result)
        {
            // Успешно закопались
            Logger.Info($"BurrowOperator: Successfully burrowed {owner}");
            _attemptStartTime = null;
            return HTNOperatorStatus.Finished;
        }

        // Не смогли закопаться - проверяем причину
        // Если прошло достаточно времени наблюдения, но плитка неподходящая - переходим к патрулированию
        if (_attemptStartTime == null)
        {
            _attemptStartTime = _timing.CurTime;
            Logger.Info($"BurrowOperator: Starting timer for {owner}");
        }
        else
        {
            var elapsed = (_timing.CurTime - _attemptStartTime.Value).TotalSeconds;
            Logger.Info($"BurrowOperator: Elapsed time {elapsed}s for {owner}");

            if (elapsed > 5.0) // Если за 5 секунд не получилось - не та плитка
            {
                // Возвращаемся в активное состояние и продолжаем патрулировать
                // ВАЖНО: Сбрасываем таймер патруля, чтобы антлайон снова патрулировал!
                Logger.Info($"BurrowOperator: Timeout! Transitioning {owner} back to Active state with patrol timer reset!");
                _burrowSystem.TransitionToActive(owner, resetPatrolTimer: true);
                _attemptStartTime = null;
                return HTNOperatorStatus.Failed;
            }
        }

        // Ещё пытаемся
        return HTNOperatorStatus.Continuing;
    }
}
