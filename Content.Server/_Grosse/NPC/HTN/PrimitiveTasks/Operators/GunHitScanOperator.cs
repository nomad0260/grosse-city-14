using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Отдельный оператор стрельбы для NPC форка. Нужен, чтобы не менять поведение
/// апстримного <see cref="Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Ranged.GunOperator"/>:
/// любая правка обычного оператора действует на всех стреляющих NPC сразу.
/// <para>
/// Отличие от апстрима одно и намеренное: <see cref="UseOpaqueForLOSChecks"/> по
/// умолчанию включён, поэтому NPC видят и стреляют сквозь стекло и решётки.
/// Пользоваться им нужно только в наших прототипах вооружённых NPC.
/// </para>
/// </summary>
public sealed partial class GunHitScanOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Ключ blackboard с целью.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// Минимальное состояние цели, при котором ещё стоит стрелять.
    /// </summary>
    [DataField("targetState")]
    public MobState TargetState = MobState.Alive;

    /// <summary>
    /// Требовать ли прямую видимость перед выстрелом.
    /// </summary>
    [DataField("requireLOS")]
    public bool RequireLOS = false;

    /// <summary>
    /// Считать видимость по непрозрачным препятствиям: стекло и решётки не мешают.
    /// </summary>
    [DataField("opaqueKey")]
    public bool UseOpaqueForLOSChecks = true;

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
        {
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));
        }

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, null));
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var ranged = _entManager.EnsureComponent<NPCRangedCombatComponent>(owner);
        ranged.Target = blackboard.GetValue<EntityUid>(TargetKey);
        ranged.UseOpaqueForLOSChecks = UseOpaqueForLOSChecks;

        if (blackboard.TryGetValue<float>(NPCBlackboard.RotateSpeed, out var rotSpeed, _entManager))
            ranged.RotationSpeed = new Angle(rotSpeed);

        if (blackboard.TryGetValue<SoundSpecifier>("SoundTargetInLOS", out var losSound, _entManager))
            ranged.SoundTargetInLOS = losSound;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        _entManager.System<SharedCombatModeSystem>().SetInCombatMode(owner, false);
        _entManager.RemoveComponent<NPCRangedCombatComponent>(owner);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        HTNOperatorStatus status;

        if (_entManager.TryGetComponent<NPCRangedCombatComponent>(owner, out var combat) &&
            blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            combat.Target = target;

            if (_entManager.TryGetComponent<MobStateComponent>(combat.Target, out var mobState) &&
                mobState.CurrentState > TargetState)
            {
                status = HTNOperatorStatus.Finished;
            }
            else
            {
                status = combat.Status switch
                {
                    CombatStatus.TargetUnreachable => HTNOperatorStatus.Failed,
                    CombatStatus.NotInSight => RequireLOS ? HTNOperatorStatus.Failed : HTNOperatorStatus.Continuing,
                    CombatStatus.Normal => HTNOperatorStatus.Continuing,
                    _ => HTNOperatorStatus.Failed,
                };
            }
        }
        else
        {
            status = HTNOperatorStatus.Failed;
        }

        if (status == HTNOperatorStatus.Continuing && ShutdownState == HTNPlanState.PlanFinished)
            status = HTNOperatorStatus.Finished;

        return status;
    }
}
