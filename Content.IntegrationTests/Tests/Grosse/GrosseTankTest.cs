#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Grosse.Cars;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Grosse;

[TestFixture]
[TestOf(typeof(GrosseCarJointComponent))]
public sealed class GrosseTankTest : GameTest
{
    private const string DummyId = "GrosseTankTestDummy";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {DummyId}
  name: {DummyId}
  components:
  - type: Hands
    hands:
      right:
        location: Right
      left:
        location: Left
    sortedHands:
    - right
    - left
  - type: ComplexInteraction
  - type: InputMover
  - type: Physics
    bodyType: KinematicController
  - type: Body
    prototype: Human
  - type: MobState
  - type: DoAfter
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        density: 80
        mask:
        - MobMask
        layer:
        - MobLayer
";

    [Test]
    public async Task TurretIsJointedChildAndDiesWithHull()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var joints = entityManager.System<SharedGrosseCarJointSystem>();
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            const string turretId = "GrosseTankTurret";
            var turretProto = protos.Index<EntityPrototype>(turretId);
            Assert.That(turretProto.Components.ContainsKey("Clickable"), Is.False);
            Assert.That(turretProto.Components.ContainsKey("InteractionOutline"), Is.False);

            int Count() => entityManager.EntityCount - entityManager.Count<AudioComponent>();
            var before = Count();

            var tank = entityManager.SpawnEntity("GrosseTank", coords);
            var driver = entityManager.SpawnEntity(DummyId, coords);
            var gunner = entityManager.SpawnEntity(DummyId, coords);

            Assert.That(entityManager.TryGetComponent(tank, out GrosseCarComponent? car));
            Assert.That(car!.SteerInPlace, Is.False);
            Assert.That(entityManager.TryGetComponent(tank, out GrosseCarJointComponent? joint));
            Assert.That(joint!.Turret, Is.Not.Null);

            var turret = joint.Turret!.Value;
            Assert.That(entityManager.GetComponent<TransformComponent>(turret).ParentUid, Is.EqualTo(tank));
            Assert.That(entityManager.TryGetComponent(tank, out JointComponent? physicsJoints));
            Assert.That(physicsJoints!.GetJoints.ContainsKey(joint.JointId), Is.True);
            Assert.That(physicsJoints.GetJoints[joint.JointId], Is.InstanceOf<RevoluteJoint>());
            Assert.That(entityManager.GetComponent<GrosseCarTurretComponent>(turret).Hull, Is.EqualTo((EntityUid?) tank));

            Assert.That(cars.TryEnterSlot(driver, tank, "driver", skipDelay: true), Is.True);
            Assert.That(entityManager.GetComponent<GrosseCarRiderComponent>(driver).ControlsTurret, Is.False);
            Assert.That(joints.TryEnterGunner(driver, (tank, joint), skipDelay: true), Is.False, "the driver cannot also sit in the turret");

            Assert.That(joints.TryEnterGunner(gunner, (tank, joint), skipDelay: true), Is.True);
            var rider = entityManager.GetComponent<GrosseCarRiderComponent>(gunner);
            Assert.That(rider.ControlsTurret, Is.True);
            Assert.That(rider.IsDriver, Is.False);
            Assert.That(rider.Car, Is.EqualTo(tank));
            Assert.That(entityManager.HasComponent<RelayInputMoverComponent>(gunner), Is.False);

            entityManager.DeleteEntity(tank);

            Assert.That(entityManager.EntityExists(turret), Is.False);
            Assert.That(entityManager.EntityExists(gunner), Is.True);
            Assert.That(entityManager.HasComponent<GrosseCarRiderComponent>(gunner), Is.False);
            Assert.That(Count(), Is.EqualTo(before + 1), "deleting the hull should remove the turret and leave the ejected gunner");
        });
    }

    [Test]
    public async Task LoadedTankSpawnsWithRackAndEmptyDoesNot()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var containers = entityManager.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var loaded = entityManager.SpawnEntity("GrosseTank", coords);
            var loadedTurret = entityManager.GetComponent<GrosseCarJointComponent>(loaded).Turret!.Value;
            Assert.That(containers.TryGetContainer(loadedTurret, "turret-ammo", out var rack), Is.True);
            Assert.That(rack!.ContainedEntities, Has.Count.EqualTo(8));

            var empty = entityManager.SpawnEntity("GrosseTankEmpty", coords);
            var emptyJoint = entityManager.GetComponent<GrosseCarJointComponent>(empty);
            Assert.That(emptyJoint.StartingAmmo, Is.Zero);
            var emptyTurret = emptyJoint.Turret!.Value;
            var emptyCount = containers.TryGetContainer(emptyTurret, "turret-ammo", out var emptyRack)
                ? emptyRack.ContainedEntities.Count
                : 0;
            Assert.That(emptyCount, Is.Zero);
        });
    }

    [Test]
    public async Task GunnerSitsAndShotsDoNotDamageTheTank()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var joints = entityManager.System<SharedGrosseCarJointSystem>();
        var guns = entityManager.System<SharedGunSystem>();
        var damageable = entityManager.System<DamageableSystem>();
        var tags = entityManager.System<TagSystem>();
        var xform = entityManager.System<SharedTransformSystem>();

        EntityUid tank = default;
        EntityUid turret = default;
        EntityUid driver = default;
        EntityUid gunner = default;

        await server.WaitAssertion(() =>
        {
            tank = entityManager.SpawnEntity("GrosseTank", coords);
            driver = entityManager.SpawnEntity(DummyId, coords);
            gunner = entityManager.SpawnEntity(DummyId, coords);
            turret = entityManager.GetComponent<GrosseCarJointComponent>(tank).Turret!.Value;

            Assert.That(cars.TryEnterSlot(driver, tank, "driver", skipDelay: true), Is.True);
            Assert.That(ActionIds(entityManager, driver), Does.Contain("ActionGrosseTankReload"),
                "the driver loads the turret while the gunner seat is empty");
            Assert.That(joints.TryGetLoader((tank, entityManager.GetComponent<GrosseCarJointComponent>(tank)), out var driverSeat, out var driverLoader)
                        && driverLoader == driver
                        && driverSeat.ReloadDelay == TimeSpan.FromSeconds(2.5), Is.True);
            Assert.That(joints.CanAim(driver, (tank, entityManager.GetComponent<GrosseCarJointComponent>(tank))), Is.True);
            Assert.That(guns.TryGetGun(driver, out var driverGun) && driverGun.Owner == turret, Is.True,
                "the driver should fire the turret while the gunner seat is empty");
        });

        await FireThroughHull(server, entityManager, guns, xform, damageable, driver, tank, turret);

        await server.WaitAssertion(() =>
        {
            tags.AddTag(gunner, InstantDoAfters);
            var joint = entityManager.GetComponent<GrosseCarJointComponent>(tank);
            Assert.That(joints.TryEnterGunner(gunner, (tank, joint), skipDelay: false), Is.True);
            Assert.That(entityManager.GetComponent<GrosseCarRiderComponent>(gunner).ControlsTurret, Is.True);
            Assert.That(joints.CanAim(gunner, (tank, joint)), Is.True);
            Assert.That(joints.CanAim(driver, (tank, joint)), Is.False, "the driver must not aim while a gunner is seated");
            Assert.That(ActionIds(entityManager, driver), Does.Not.Contain("ActionGrosseTankReload"));
            Assert.That(joints.TryGetLoader((tank, joint), out var gunnerSeat, out var gunnerLoader)
                        && gunnerLoader == gunner
                        && gunnerSeat.ReloadDelay == TimeSpan.FromSeconds(1.5), Is.True);
            Assert.That(guns.TryGetGun(gunner, out var gunnerGun) && gunnerGun.Owner == turret, Is.True);

            Assert.That(ActionIds(entityManager, gunner), Is.EquivalentTo(new[] { "ActionGrosseCarExit", "ActionGrosseTankReload" }));

            var reload = new GrosseTankReloadEvent { Performer = gunner };
            entityManager.EventBus.RaiseLocalEvent(tank, reload);
            Assert.That(guns.GetAmmoCount(turret), Is.EqualTo(1), "the seated gunner should be able to chamber a shell");
        });

        await FireThroughHull(server, entityManager, guns, xform, damageable, gunner, tank, turret);
    }

    private static readonly ProtoId<TagPrototype> InstantDoAfters = "InstantDoAfters";

    private static List<string> ActionIds(IEntityManager entityManager, EntityUid user)
    {
        var ids = new List<string>();
        foreach (var action in entityManager.System<SharedActionsSystem>().GetActions(user))
        {
            var id = entityManager.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID;
            if (id != null)
                ids.Add(id);
        }

        return ids;
    }

    private static async Task FireThroughHull(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        IEntityManager entityManager,
        SharedGunSystem guns,
        SharedTransformSystem xform,
        DamageableSystem damageable,
        EntityUid shooter,
        EntityUid tank,
        EntityUid turret)
    {
        await server.WaitAssertion(() =>
        {
            var breech = entityManager.GetComponent<BallisticAmmoProviderComponent>(turret);
            // Map init already leaves one unspawned round, and a reload leaves a real shell in the breech.
            // Adding another unspawned round on top of that makes the count 2 and the leftover round survives the shot.
            if (guns.GetAmmoCount(turret) == 0)
                guns.SetBallisticUnspawned((turret, breech), 1);
            Assert.That(guns.GetAmmoCount(turret), Is.EqualTo(1));

            var existing = new HashSet<EntityUid>();
            var before = entityManager.EntityQueryEnumerator<ProjectileComponent>();
            while (before.MoveNext(out var uid, out _))
                existing.Add(uid);

            var world = xform.GetWorldPosition(turret);
            var facing = xform.GetWorldRotation(turret).ToWorldVec();
            var mapUid = entityManager.GetComponent<TransformComponent>(turret).MapUid!.Value;
            var target = new EntityCoordinates(mapUid, world - facing * 40f);
            var gun = entityManager.GetComponent<GunComponent>(turret);
            Assert.That(guns.AttemptShoot(shooter, (turret, gun), target), Is.True);

            var found = false;
            var query = entityManager.EntityQueryEnumerator<ProjectileComponent>();
            while (query.MoveNext(out var uid, out var projectile))
            {
                if (existing.Contains(uid) || projectile.Weapon != turret)
                    continue;

                found = true;
                Assert.That(projectile.Shooter, Is.EqualTo(tank), "the shell must treat the hull as its shooter");
            }

            Assert.That(found, Is.True, "the turret did not spawn a shell");
            Assert.That(damageable.GetTotalDamage(tank), Is.EqualTo(FixedPoint2.Zero));
        });

        await server.WaitRunTicks(70);

        await server.WaitAssertion(() =>
        {
            Assert.That(damageable.GetTotalDamage(tank), Is.EqualTo(FixedPoint2.Zero), "the shell damaged its own tank");
            Assert.That(guns.GetAmmoCount(turret), Is.EqualTo(0));
        });
    }
}
