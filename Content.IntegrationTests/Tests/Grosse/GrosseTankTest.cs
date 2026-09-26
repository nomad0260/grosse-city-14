#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._Grosse.Cars;
using Content.Shared.Movement.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
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
            Assert.That(entityManager.GetComponent<GrosseCarTurretComponent>(turret).Hull, Is.EqualTo(tank));

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
}
