using System.Linq;
using System.Numerics;
using Content.Shared._Grosse.Cars;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Grosse.Cars;

/// <summary>
/// Spawns the turret as a child of the hull and keeps a revolute joint on it.
/// The joint is not map data, so it is created again whenever the hull starts.
/// </summary>
public sealed partial class GrosseCarJointSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseCarJointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GrosseCarJointComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<GrosseCarJointComponent>();
        while (query.MoveNext(out var uid, out var joint))
        {
            if (joint.Turret is not { } turret || TerminatingOrDeleted(turret))
                continue;

            if (TryComp<JointComponent>(uid, out var joints) && joints.GetJoints.ContainsKey(joint.JointId))
                continue;

            EnsureJoint((uid, joint), turret);
        }
    }

    private void OnMapInit(Entity<GrosseCarJointComponent> ent, ref MapInitEvent args)
    {
        // The component test adds this to a blank entity with no physics. A joint there only logs an error.
        if (!HasComp<PhysicsComponent>(ent))
            return;

        if (ent.Comp.Turret is { } existing && !TerminatingOrDeleted(existing))
        {
            if (Transform(existing).ParentUid != ent.Owner)
                _transform.SetParent(existing, ent);

            var turretComp = EnsureComp<GrosseCarTurretComponent>(existing);
            turretComp.Hull = ent;
            Dirty(existing, turretComp);
            EnsureJoint(ent, existing);
            EntityManager.System<SharedGrosseCarJointSystem>().UpdateTurretActions(ent);
            return;
        }

        var turret = Spawn(ent.Comp.TurretPrototype, Transform(ent).Coordinates);
        _transform.SetParent(turret, ent);
        _transform.SetLocalPosition(turret, ent.Comp.Anchor);
        _transform.SetLocalRotation(turret, Angle.Zero);

        var spawned = EnsureComp<GrosseCarTurretComponent>(turret);
        spawned.Hull = ent;
        Dirty(turret, spawned);

        ent.Comp.Turret = turret;
        Dirty(ent, ent.Comp);
        EnsureJoint(ent, turret);
        FillRack(ent, turret);
        var joints = EntityManager.System<SharedGrosseCarJointSystem>();
        joints.UpdateAppearance(ent);
        joints.UpdateTurretActions(ent);
    }

    private void FillRack(Entity<GrosseCarJointComponent> hull, EntityUid turret)
    {
        var count = Math.Min(hull.Comp.StartingAmmo, hull.Comp.AmmoCapacity);
        if (count <= 0)
            return;

        var rack = _container.EnsureContainer<Container>(turret, hull.Comp.AmmoContainer);
        for (var i = 0; i < count; i++)
        {
            var shell = Spawn(hull.Comp.AmmoPrototype, Transform(turret).Coordinates);
            if (_container.Insert(shell, rack))
                continue;

            Del(shell);
            break;
        }
    }

    private void OnShutdown(Entity<GrosseCarJointComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Turret is not { } turret || Deleted(turret))
            return;

        if (MetaData(ent).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (_container.TryGetContainer(turret, ent.Comp.GunnerContainer, out var container))
        {
            foreach (var occupant in container.ContainedEntities.ToArray())
                _container.Remove(occupant, container, destination: _transform.GetMoverCoordinates(ent.Owner));
        }

        QueueDel(turret);
    }

    private void EnsureJoint(Entity<GrosseCarJointComponent> hull, EntityUid turret)
    {
        if (!HasComp<PhysicsComponent>(hull) || !HasComp<PhysicsComponent>(turret))
            return;

        if (TryComp<JointComponent>(hull, out var joints) && joints.GetJoints.ContainsKey(hull.Comp.JointId))
            return;

        var joint = _joints.CreateRevoluteJoint(hull, turret, hull.Comp.JointId);
        joint.LocalAnchorA = hull.Comp.Anchor;
        joint.LocalAnchorB = Vector2.Zero;
        joint.CollideConnected = false;
        joint.EnableLimit = false;
        var relative = _transform.GetWorldRotation(turret) - _transform.GetWorldRotation(hull);
        joint.ReferenceAngle = (float) relative.Theta;
    }
}
