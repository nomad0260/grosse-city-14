using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Shared._Grosse.Cars;

public sealed partial class SharedGrosseCarSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<GrosseCarRiderComponent> _riderQuery = default!;
    [Dependency] private EntityQuery<InjurableComponent> _injurableQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    private void InitializeCollision()
    {
        SubscribeLocalEvent<GrosseCarComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<GrosseCarComponent> ent, ref StartCollideEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        if (args.OtherEntity == ent.Owner)
            return;

        if (_riderQuery.TryComp(args.OtherEntity, out var rider) && rider.Car == ent.Owner)
            return;

        var now = _timing.CurTime;
        if (now - ent.Comp.LastImpact < ent.Comp.ImpactCooldown)
            return;

        // Sprint is 4.5 and minImpactSpeed is 4. Relative speed would treat walking into a parked truck as a ram.
        var carSpeed = args.OurBody.LinearVelocity.Length();
        if (carSpeed < 0.15f)
            return;

        var relVel = args.OurBody.LinearVelocity - args.OtherBody.LinearVelocity;
        var relSpeed = relVel.Length();
        if (relSpeed < 0.15f)
            return;

        var dir = relVel / relSpeed;
        var mCar = Math.Max(args.OurBody.FixturesMass, 1f);

        if (_mapGridQuery.HasComp(args.OtherEntity))
        {
            if (carSpeed >= ent.Comp.MinImpactSpeed)
                HandleWall(ent, dir, relSpeed, mCar);
            return;
        }

        if (_injurableQuery.HasComp(args.OtherEntity) || _damageableQuery.HasComp(args.OtherEntity))
        {
            if (_whitelist.IsWhitelistPass(ent.Comp.RamBlacklist, args.OtherEntity))
            {
                if (carSpeed >= ent.Comp.MinImpactSpeed)
                    HandleBlacklist(ent, args.OtherEntity, dir, relSpeed, mCar);
            }
            else if (_mobStateQuery.HasComp(args.OtherEntity))
            {
                if (carSpeed >= ent.Comp.MinImpactSpeed)
                    HandleMob(ent, args.OtherEntity, args.OtherBody, dir, relSpeed, mCar);
            }
            else if (carSpeed >= ent.Comp.RamMinSpeed)
            {
                HandleRammable(ent, args.OtherEntity, args.OtherBody, dir, relSpeed, mCar);
            }

            return;
        }

        if ((args.OtherFixture.CollisionLayer & (int) CollisionGroup.Impassable) != 0 &&
            carSpeed >= ent.Comp.MinImpactSpeed)
            HandleWall(ent, dir, relSpeed, mCar);
    }

    private void HandleMob(
        Entity<GrosseCarComponent> ent,
        EntityUid other,
        PhysicsComponent otherBody,
        Vector2 dir,
        float relSpeed,
        float mCar)
    {
        var mOther = Math.Max(otherBody.FixturesMass, 1f);
        SlowCar(ent, dir, relSpeed, mCar, mOther);

        if (_net.IsServer)
        {
            TossTarget(other, dir, relSpeed, ent.Comp.PushMultiplier);

            if (relSpeed >= ent.Comp.MinImpactSpeed)
            {
                var scale = relSpeed / ent.Comp.MinImpactSpeed;
                ApplyCarDamage(ent, ent.Comp.SelfDamage * scale);
                _damageable.TryChangeDamage(other, ent.Comp.HitDamage * scale);
                _stun.TryKnockdown(other, ent.Comp.KnockdownTime, force: true);
            }
        }

        PlayImpact(ent, ent.Comp.HitSound);
    }

    private void HandleBlacklist(
        Entity<GrosseCarComponent> ent,
        EntityUid other,
        Vector2 dir,
        float relSpeed,
        float mCar)
    {
        SlowCar(ent, dir, relSpeed, mCar, float.PositiveInfinity);

        if (_net.IsServer && relSpeed >= ent.Comp.MinImpactSpeed)
        {
            var scale = relSpeed / ent.Comp.MinImpactSpeed;
            var damage = ent.Comp.WallDamage * scale;
            if (ent.Comp.BlacklistImpactCoefficient is { } coefficient)
                damage *= coefficient;

            ApplyCarDamage(ent, damage);
        }

        PlayImpact(ent, _injurableQuery.HasComp(other) ? ent.Comp.HitSound : ent.Comp.ImpactSound);
    }

    private void HandleWall(Entity<GrosseCarComponent> ent, Vector2 dir, float relSpeed, float mCar)
    {
        SlowCar(ent, dir, relSpeed, mCar, float.PositiveInfinity);

        if (_net.IsServer && relSpeed >= ent.Comp.MinImpactSpeed)
        {
            var scale = relSpeed / ent.Comp.MinImpactSpeed;
            ApplyCarDamage(ent, ent.Comp.WallDamage * scale);
        }

        PlayImpact(ent, ent.Comp.WallImpactSound);
    }

    private void HandleRammable(
        Entity<GrosseCarComponent> ent,
        EntityUid other,
        PhysicsComponent otherBody,
        Vector2 dir,
        float relSpeed,
        float mCar)
    {
        var mOther = Math.Max(otherBody.FixturesMass, 1f);
        SlowCar(ent, dir, relSpeed, mCar, mOther);

        if (_net.IsServer && relSpeed >= ent.Comp.RamMinSpeed)
        {
            var scale = relSpeed / Math.Max(ent.Comp.RamMinSpeed, 0.01f);
            _damageable.TryChangeDamage(other, ent.Comp.HitDamage * scale);
            ApplyCarDamage(ent, ent.Comp.SelfDamage * 0.5f * scale);

            if (_injurableQuery.HasComp(other))
                TossTarget(other, dir, relSpeed, ent.Comp.PushMultiplier);
        }

        PlayImpact(ent, _injurableQuery.HasComp(other) ? ent.Comp.HitSound : ent.Comp.ImpactSound);
    }

    private void TossTarget(EntityUid other, Vector2 dir, float relSpeed, float pushMultiplier)
    {
        if (!_physicsQuery.TryComp(other, out var physics))
            return;

        _transform.Unanchor(other);
        if ((physics.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) == 0)
            _physics.SetBodyType(other, BodyType.Dynamic, body: physics);

        var speed = Math.Max(relSpeed * pushMultiplier, 5f);
        _throwing.TryThrow(
            other,
            dir * speed,
            physics,
            Transform(other),
            speed,
            recoil: false,
            playSound: false,
            doSpin: false,
            unanchor: ThrowingUnanchorStrength.All);

        // KinematicController bodies have InvMass 0, so TryThrow's impulse does nothing.
        _physics.SetLinearVelocity(other, dir * speed, body: physics);
        _physics.SetBodyStatus(other, physics, BodyStatus.InAir);
        _physics.WakeBody(other);
    }

    private void SlowCar(Entity<GrosseCarComponent> ent, Vector2 dir, float relSpeed, float mCar, float mOther)
    {
        if (!_physicsQuery.TryComp(ent.Owner, out var physics))
            return;

        float reduce;
        if (float.IsPositiveInfinity(mOther))
            reduce = 1f;
        else
            reduce = mOther / (mCar + mOther);

        var lost = dir * (relSpeed * reduce * (1f - ent.Comp.ImpactRestitution));
        _physics.SetLinearVelocity(ent.Owner, physics.LinearVelocity - lost);
        _physics.WakeBody(ent.Owner);
        ent.Comp.LastImpact = _timing.CurTime;
        Dirty(ent.Owner, ent.Comp);
    }

    private void ApplyCarDamage(Entity<GrosseCarComponent> ent, DamageSpecifier damage)
    {
        _damageable.TryChangeDamage(ent.Owner, damage);

        if (ent.Comp.OccupantDamageMultiplier <= 0f)
            return;

        var occupantDamage = damage * ent.Comp.OccupantDamageMultiplier;
        foreach (var slot in ent.Comp.Slots)
        {
            if (!_container.TryGetContainer(ent.Owner, slot.ContainerId, out var container))
                continue;

            foreach (var occupant in container.ContainedEntities)
            {
                _damageable.TryChangeDamage(occupant, occupantDamage);
            }
        }
    }

    private void PlayImpact(Entity<GrosseCarComponent> ent, SoundSpecifier? sound)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (sound != null)
        {
            _audio.PlayPredicted(sound, ent.Owner, null,
                AudioParams.Default.WithVariation(0.125f).WithVolume(-0.125f));
        }

        _color.RaiseEffect(Color.Red, new List<EntityUid> { ent.Owner }, Filter.Pvs(ent.Owner, entityManager: EntityManager));
    }
}
