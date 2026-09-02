using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Cover;

/// <summary>
/// Makes cover actually stop bullets, except when firing from next to it
/// or when the shot is aimed at a mob behind it.
/// </summary>
public sealed partial class CoverSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CoverComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<CoverComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp(args.OtherEntity, out ProjectileComponent? projectile))
            return;

        var target = CompOrNull<TargetedProjectileComponent>(args.OtherEntity)?.Target;
        if (CanShootThrough(ent, projectile.Shooter, target))
            args.Cancelled = true;
    }

    /// <summary>
    /// True if this entity is cover that this shot should pass through.
    /// Non-cover entities always return false.
    /// </summary>
    public bool CanShootThrough(EntityUid cover, EntityUid? shooter, EntityUid? target, CoverComponent? comp = null)
    {
        if (!Resolve(cover, ref comp, false))
            return false;

        // Standing next to cover: always fire through, even if the cursor is on the barricade.
        if (shooter is { } shooterUid &&
            !TerminatingOrDeleted(shooterUid) &&
            _transform.InRange(shooterUid, cover, comp.ShootThroughRadius))
        {
            return true;
        }

        // Sniper / aimed fire: cursor is on a mob, so the shot can pass through cover to hit them.
        return target is { } aimed &&
               aimed != cover &&
               !TerminatingOrDeleted(aimed) &&
               HasComp<MobStateComponent>(aimed);
    }
}
