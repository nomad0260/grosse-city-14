using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared._Grosse.NPC;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Пожирание добычи. Живую или крит-цель NPC забивает своим оружием ближнего
/// боя до смерти, после чего запускает длинное действие разрывания тела на
/// части (см. <see cref="NpcDevourComponent.TearTime"/>), съедает organs и лечится.
/// </summary>
public sealed partial class DevourOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private MobStateSystem _mobState = default!;
    private NpcDevourSystem _devour = default!;
    private SharedMeleeWeaponSystem _melee = default!;
    private SharedCombatModeSystem _combat = default!;
    private NPCSteeringSystem _steering = default!;

    /// <summary>
    /// Ключ с целевой сущностью.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _mobState = sysManager.GetEntitySystem<MobStateSystem>();
        _devour = sysManager.GetEntitySystem<NpcDevourSystem>();
        _melee = sysManager.GetEntitySystem<SharedMeleeWeaponSystem>();
        _combat = sysManager.GetEntitySystem<SharedCombatModeSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
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

        _devour.Reserve(owner, target);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<NpcDevourComponent>(owner, out var comp) ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            return HTNOperatorStatus.Failed;
        }

        // Тело могли удалить (гибы, админ) — задача просто проваливается,
        // ничего трогать по старому EntityUid нельзя.
        if (!_entManager.EntityExists(target))
            return _devour.IsTearing(owner) ? HTNOperatorStatus.Continuing : HTNOperatorStatus.Failed;

        if (!_devour.IsValidPrey(target))
            return HTNOperatorStatus.Failed;

        // Ещё не подошли — догоняем, а не падаем и не бросаем цель.
        if (!_devour.CanDevour(owner, target))
        {
            // MoveToOperator в devour_compound завершается с shutdownState:
            // TaskFinished, то есть снимает steering ещё до этого оператора.
            // Без повторной регистрации NPC просто стоит, пока добыча не
            // подойдёт сама — именно так зомби «вставали колом» после удара.
            //
            // Регистрируем только если steering ещё не ведёт к этой цели:
            // повторный Register каждый тик сбрасывает путь, и NPC начинает
            // искать его заново 60 раз в секунду.
            if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering) ||
                steering.Coordinates.EntityId != target)
            {
                _steering.Register(owner, new EntityCoordinates(target, Vector2.Zero));
            }

            return HTNOperatorStatus.Continuing;
        }

        // Цель мертва: вместо ударов запускаем длинное разрывание тела.
        if (_mobState.IsDead(target))
        {
            if (_devour.IsTearing(owner))
                return HTNOperatorStatus.Continuing;

            // Останавливаем движение: steering ещё ведёт NPC к трупу, а
            // разрывание обрывается от первого же шага. Без этого зомби
            // кружит вокруг тела, перезапуская действие каждый тик.
            _steering.Unregister(owner);

            // Не смогли начать разрывание (тело уже утащили, DoAfter занят) —
            // задача проваливается, иначе NPC зависает в Continuing навсегда.
            return _devour.TryStartTearing(owner, target)
                ? HTNOperatorStatus.Continuing
                : HTNOperatorStatus.Failed;
        }

        // Живого или крит-человека забиваем ударами до смерти.
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

        _melee.AttemptLightAttack(owner, weaponUid, weapon, target);

        return HTNOperatorStatus.Continuing;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        _devour.CancelTearing(owner);

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            _devour.Release(owner, target);

        _combat.SetInCombatMode(owner, false);
    }
}
