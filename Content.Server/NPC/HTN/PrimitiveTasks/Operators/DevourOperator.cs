using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.CombatMode;
using Content.Shared.NPC;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Timing;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Добивает беспомощную добычу: NPC бьёт тело своим оружием ближнего боя,
/// пока оно не развалится на гибы, после чего лечится. Никакого DoAfter
/// «поедания» здесь нет — со стороны это обычные удары.
/// </summary>
public sealed partial class DevourOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private NpcDevourSystem _devour = default!;
    private SharedMeleeWeaponSystem _melee = default!;
    private SharedCombatModeSystem _combat = default!;

    /// <summary>
    /// Ключ с целевой сущностью.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    private const string HitsKey = "DevourHits";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _devour = sysManager.GetEntitySystem<NpcDevourSystem>();
        _melee = sysManager.GetEntitySystem<SharedMeleeWeaponSystem>();
        _combat = sysManager.GetEntitySystem<SharedCombatModeSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Ветка общая для всех монстров, поэтому без компонента она просто
        // не должна выбираться.
        if (!_entManager.HasComponent<NpcDevourComponent>(owner) ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target) ||
            !_devour.IsValidPrey(target) ||
            _devour.IsReservedByOther(owner, target))
        {
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));
        }

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, null));
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        blackboard.Remove<int>(HitsKey);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target) ||
            !_entManager.HasComponent<NpcDevourComponent>(owner))
        {
            return;
        }

        // Пока NPC планировал, труп мог достаться другому.
        if (_devour.IsReservedByOther(owner, target))
            return;

        blackboard.SetValue(HitsKey, 0);
        _devour.Reserve(owner, target);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Тело могли удалить (гибы, админ) — задача просто проваливается,
        // ничего трогать по старому EntityUid нельзя.
        if (!_entManager.TryGetComponent<NpcDevourComponent>(owner, out var comp) ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target) ||
            !_devour.IsValidPrey(target))
        {
            return HTNOperatorStatus.Failed;
        }

        // Ещё не подошли — догоняем, а не падаем и не бросаем цель.
        if (!_devour.CanDevour(owner, target))
            return HTNOperatorStatus.Continuing;

        if (!_entManager.TryGetComponent<CombatModeComponent>(owner, out var combatMode) ||
            !_melee.TryGetWeapon(owner, out var weaponUid, out var weapon) ||
            weapon == null)
        {
            return HTNOperatorStatus.Failed;
        }

        _combat.SetInCombatMode(owner, true, combatMode);

        // Оружие на кулдауне: ждём следующий удар, не сбрасывая задачу.
        if (weapon.NextAttack > _timing.CurTime)
            return HTNOperatorStatus.Continuing;

        if (!_melee.AttemptLightAttack(owner, weaponUid, weapon, target))
            return HTNOperatorStatus.Continuing;

        var hits = blackboard.GetValueOrDefault<int>(HitsKey, _entManager) + 1;
        blackboard.SetValue(HitsKey, hits);

        if (hits < comp.GibHits)
            return HTNOperatorStatus.Continuing;

        return _devour.TryDevour(owner, target)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<int>(HitsKey);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            _devour.Release(owner, target);

        _combat.SetInCombatMode(owner, false);
    }
}
