using Content.Server.NPC.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Mobs.Systems;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Добивание беспомощной добычи: NPC бьёт тело, пока оно не развалится
/// на куски, после чего лечится.
/// </summary>
public sealed class NpcDevourSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>
    /// Можно ли прямо сейчас добить цель.
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
    /// Подходит ли цель как добыча, без учёта дистанции. Подходит всё, что уже
    /// не живо (крит или труп): в процессе добивания тело успевает умереть,
    /// но его всё ещё нужно разорвать на гибы.
    /// </summary>
    public bool IsValidPrey(EntityUid target)
    {
        if (!Exists(target))
            return false;

        if (_mobState.IsAlive(target))
            return false;

        // Не едим иммунных к зомби.
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
    /// Разорвать тело на куски и восстановить здоровье.
    /// </summary>
    public bool TryDevour(EntityUid eater, EntityUid target)
    {
        if (!Exists(eater) || !Exists(target))
            return false;

        if (!CanDevour(eater, target) || !TryComp<NpcDevourComponent>(eater, out var comp))
            return false;

        if (comp.DevourSound != null)
            _audio.PlayPvs(comp.DevourSound, target);

        _gibbing.Gib(target, dropGiblets: true, user: eater);

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
