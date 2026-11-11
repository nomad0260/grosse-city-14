using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared._Grosse.NPC;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Systems;

/// <summary>
/// Пожирание добычи: NPC бьёт цель до смерти, затем <see cref="NpcDevourComponent.TearTime"/>
/// секунд разрывает тело на части, съедает несколько выпавших органов и лечится.
/// </summary>
public sealed partial class NpcDevourSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcDevourComponent, NpcTearDoAfterEvent>(OnTearDoAfter);
    }

    /// <summary>
    /// Можно ли прямо сейчас добраться до цели.
    /// </summary>
    public bool CanDevour(EntityUid eater, EntityUid target)
    {
        // Труп могли уже сожрать, разорвать на куски или удалить, пока NPC
        // шёл к нему: без проверки Transform упадёт на удалённой сущности.
        if (!Exists(eater) || !Exists(target))
            return false;

        if (eater == target)
            return false;

        if (!TryComp<NpcDevourComponent>(eater, out var comp))
            return false;

        if (!IsValidPrey(target))
            return false;

        if (!Transform(eater).Coordinates.TryDistance(EntityManager, Transform(target).Coordinates, out var distance))
            return false;

        return distance <= comp.Range;
    }

    /// <summary>
    /// Подходит ли цель как добыча, без учёта дистанции. Годится и живая цель
    /// (NPC забивает её до смерти), и уже лежачее тело (крит или труп).
    /// Иммунных к зомби не трогаем.
    /// </summary>
    public bool IsValidPrey(EntityUid target)
    {
        if (!Exists(target))
            return false;

        // Только мобы: стены и мебель жрём другими системами.
        if (!HasComp<MobStateComponent>(target))
            return false;

        return !HasComp<ZombieImmuneComponent>(target);
    }

    /// <summary>
    /// Цель уже занята другим живым едоком.
    /// </summary>
    public bool IsReservedByOther(EntityUid eater, EntityUid target)
    {
        if (!TryComp<NpcDevourTargetComponent>(target, out var reserve) || reserve.Eater == eater)
            return false;

        // Брошенный резерв (едок удалён или больше не умеет есть) не держит цель.
        return Exists(reserve.Eater) && HasComp<NpcDevourComponent>(reserve.Eater);
    }

    /// <summary>
    /// Отмечает цель как занятую, чтобы остальные NPC не налетали на тот же труп.
    /// </summary>
    public void Reserve(EntityUid eater, EntityUid target)
    {
        if (!Exists(target))
            return;

        var reserve = EnsureComp<NpcDevourTargetComponent>(target);
        reserve.Eater = eater;
    }

    /// <summary>
    /// Снимает отметку о занятости цели.
    /// </summary>
    public void Release(EntityUid eater, EntityUid target)
    {
        if (!Exists(target))
            return;

        if (TryComp<NpcDevourTargetComponent>(target, out var reserve) && reserve.Eater == eater)
            RemComp<NpcDevourTargetComponent>(target);
    }

    /// <summary>
    /// Начинает разрывание мёртвого тела на части. Пока действие идёт,
    /// NPC стоит на месте; движение или урон его прерывают.
    /// </summary>
    public bool TryStartTearing(EntityUid eater, EntityUid target)
    {
        if (!TryComp<NpcDevourComponent>(eater, out var comp))
            return false;

        if (!CanDevour(eater, target) || !_mobState.IsDead(target))
            return false;

        // Уже рвём именно это тело — не запускаем второе действие.
        if (comp.TearTarget == target && _doAfter.IsRunning(comp.TearId))
            return true;

        var args = new DoAfterArgs(EntityManager, eater, comp.TearTime, new NpcTearDoAfterEvent(), eater, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(args, out var id))
            return false;

        comp.TearTarget = target;
        comp.TearId = id;
        return true;
    }

    /// <summary>
    /// Разрывает ли NPC прямо сейчас чьё-то тело.
    /// </summary>
    public bool IsTearing(EntityUid eater)
    {
        return TryComp<NpcDevourComponent>(eater, out var comp) &&
               comp.TearTarget != null &&
               _doAfter.IsRunning(comp.TearId);
    }

    /// <summary>
    /// Прерывает разрывание тела (NPC сменил задачу, отошёл и т.п.).
    /// </summary>
    public void CancelTearing(EntityUid eater)
    {
        if (!TryComp<NpcDevourComponent>(eater, out var comp))
            return;

        if (comp.TearId is { } id)
            _doAfter.Cancel(id);

        comp.TearTarget = null;
        comp.TearId = null;
    }

    private void OnTearDoAfter(EntityUid uid, NpcDevourComponent comp, NpcTearDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
        {
            comp.TearTarget = null;
            comp.TearId = null;
            return;
        }

        TryDevour(uid, target, comp);

        comp.TearTarget = null;
        comp.TearId = null;
    }

    /// <summary>
    /// Разорвать тело на куски, съесть несколько органов и восстановить здоровье.
    /// </summary>
    public bool TryDevour(EntityUid eater, EntityUid target, NpcDevourComponent? comp = null)
    {
        if (!Exists(eater) || !Exists(target))
            return false;

        if (!Resolve(eater, ref comp, false))
            return false;

        if (!CanDevour(eater, target))
            return false;

        var giblets = _gibbing.Gib(target, dropGiblets: true, user: eater);
        var eaten = 0;

        foreach (var giblet in giblets)
        {
            if (eaten >= comp.OrgansToEat)
                break;

            if (!HasComp<OrganComponent>(giblet))
                continue;

            QueueDel(giblet);
            eaten++;
        }

        if (eaten > 0 && comp.DevourSound != null)
            _audio.PlayPvs(comp.DevourSound, eater);

        if (comp.HealAmount > 0)
        {
            var heal = new DamageSpecifier
            {
                DamageDict = { { "Slash", -comp.HealAmount } },
            };

            _damageable.TryChangeDamage(eater, heal, ignoreResistances: true, interruptsDoAfters: false);
        }

        return true;
    }
}
