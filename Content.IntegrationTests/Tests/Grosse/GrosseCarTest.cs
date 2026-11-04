#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Grosse.Cars;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Grosse;

[TestFixture]
[TestOf(typeof(SharedGrosseCarSystem))]
public sealed class GrosseCarTest : GameTest
{
    private const string DummyId = "GrosseCarTestDummy";

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
    public async Task PrototypeConfigIsValid()
    {
        var pair = Pair;
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || pair.IsTestPrototype(proto))
                        continue;

                    if (!proto.TryGetComponent(out GrosseCarComponent? car, factory))
                        continue;

                    Assert.That(car.Slots, Is.Not.Empty, $"{proto.ID} has no seats");
                    Assert.That(car.Slots.Select(s => s.Id).Distinct().Count(), Is.EqualTo(car.Slots.Count), $"{proto.ID} has duplicate slot ids");
                    Assert.That(car.Slots.Select(s => s.ContainerId).Distinct().Count(), Is.EqualTo(car.Slots.Count), $"{proto.ID} has duplicate container ids");
                    Assert.That(car.Slots.Count(s => s.IsDriver), Is.EqualTo(1), $"{proto.ID} must have exactly one driver slot");
                    Assert.That(car.EngineForce, Is.GreaterThan(0f), $"{proto.ID} engineForce");
                    Assert.That(car.EngineStartSound, Is.Not.Null, $"{proto.ID} engineStartSound");
                    Assert.That(car.EngineStopSound, Is.Not.Null, $"{proto.ID} engineStopSound");
                    Assert.That(car.EngineIdleSound, Is.Not.Null, $"{proto.ID} engineIdleSound");
                    Assert.That(car.EngineMoveStartSound, Is.Not.Null, $"{proto.ID} engineMoveStartSound");
                    Assert.That(car.EngineMoveStartDuration, Is.GreaterThan(0f), $"{proto.ID} engineMoveStartDuration");
                    Assert.That(car.EngineDriveSound, Is.Not.Null, $"{proto.ID} engineDriveSound");
                    Assert.That(car.EngineState, Is.EqualTo(GrosseCarEngineState.Off), $"{proto.ID} engine starts off");
                    Assert.That(car.MaxForwardSpeed, Is.GreaterThan(0f), $"{proto.ID} maxForwardSpeed");
                    Assert.That(car.RamBlacklist.Tags?.Any(tag => tag.Id == "Wall"), Is.True, $"{proto.ID} ramBlacklist must include Wall");
                    if (proto.ID == "VehicleKraz17")
                        Assert.That(car.BlacklistImpactCoefficient, Is.EqualTo(1.5f), $"{proto.ID} blacklistImpactCoefficient");
                    else
                        Assert.That(car.BlacklistImpactCoefficient, Is.Null, $"{proto.ID} blacklistImpactCoefficient");
                    Assert.That(proto.HasComponent<SkipMobMovementComponent>(factory), $"{proto.ID} missing SkipMobMovement");
                    Assert.That(proto.HasComponent<InputMoverComponent>(factory), $"{proto.ID} missing InputMover");
                    Assert.That(proto.HasComponent<AppearanceComponent>(factory), $"{proto.ID} missing Appearance");
                    Assert.That(proto.TryGetComponent(out PhysicsComponent? physics, factory), $"{proto.ID} missing Physics");
                    Assert.That(physics!.BodyType, Is.EqualTo(BodyType.Dynamic), $"{proto.ID} must be Dynamic so it collides with walking mobs");
                    Assert.That(proto.TryGetComponent(out FixturesComponent? fixtures, factory), $"{proto.ID} missing Fixtures");
                    foreach (var fixture in fixtures!.Fixtures.Values)
                    {
                        if (fixture.Shape is PhysShapeAabb aabb)
                        {
                            var maxWidth = proto.ID.StartsWith("GrosseTank") ? 3.6f : 2.2f;
                            Assert.That(aabb.LocalBounds.Width, Is.LessThan(maxWidth), $"{proto.ID} hitbox is wider than the south-facing sprite");
                        }
                    }
                    Assert.That(proto.TryGetComponent(out ContainerManagerComponent? containers, factory), $"{proto.ID} missing ContainerContainer");

                    foreach (var slot in car.Slots)
                    {
                        Assert.That(loc.HasString(slot.Name), $"{proto.ID} slot {slot.Id} missing locale {slot.Name}");
                        Assert.That(containers!.Containers.ContainsKey(slot.ContainerId), $"{proto.ID} missing container {slot.ContainerId}");
                        Assert.That(containers.Containers[slot.ContainerId], Is.TypeOf<ContainerSlot>(), $"{proto.ID} {slot.ContainerId} is not a ContainerSlot");
                    }

                    foreach (var action in car.OccupantActions)
                    {
                        Assert.That(protoMan.HasIndex(action), $"{proto.ID} occupant action {action} missing");
                    }
                }
            });
        });
    }

    [Test]
    public async Task CanEnterAndExitKraz()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var containers = entityManager.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var car = entityManager.SpawnEntity("VehicleKraz17", coords);
            var driver = entityManager.SpawnEntity(DummyId, coords);
            var passenger = entityManager.SpawnEntity(DummyId, coords);
            var passenger2 = entityManager.SpawnEntity(DummyId, coords);
            var extra = entityManager.SpawnEntity(DummyId, coords);

            Assert.That(cars.TryEnterSlot(driver, car, "driver", skipDelay: true), Is.True);
            Assert.That(entityManager.TryGetComponent(driver, out GrosseCarRiderComponent? driverRider));
            Assert.That(driverRider!.IsDriver, Is.True);
            Assert.That(entityManager.HasComponent<RelayInputMoverComponent>(driver), Is.True);
            Assert.That(containers.TryGetContainer(car, "car-driver", out var driverSlot) && driverSlot.Contains(driver));

            Assert.That(cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), Is.True);
            Assert.That(entityManager.TryGetComponent(passenger, out GrosseCarRiderComponent? passRider));
            Assert.That(passRider!.IsDriver, Is.False);
            Assert.That(entityManager.HasComponent<RelayInputMoverComponent>(passenger), Is.False);

            Assert.That(cars.TryEnterSlot(passenger2, car, "passenger2", skipDelay: true), Is.True);
            Assert.That(cars.TryEnterSlot(extra, car, skipDelay: true), Is.False);

            Assert.That(cars.TryEject((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver, driver, skipDelay: true), Is.True);
            Assert.That(entityManager.HasComponent<GrosseCarRiderComponent>(driver), Is.False);
            Assert.That(entityManager.HasComponent<RelayInputMoverComponent>(driver), Is.False);
            Assert.That(containers.TryGetContainer(car, "car-driver", out var emptyDriver) && emptyDriver.ContainedEntities.Count == 0);
        });
    }

    [Test]
    public async Task DriverControlIsRelayedToCar()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var mover = entityManager.System<SharedMoverController>();

        await server.WaitAssertion(() =>
        {
            var car = entityManager.SpawnEntity("VehicleKraz17", coords);
            var driver = entityManager.SpawnEntity(DummyId, coords);
            var passenger = entityManager.SpawnEntity(DummyId, coords);

            Assert.That(cars.TryEnterSlot(driver, car, "driver", skipDelay: true), Is.True);
            Assert.That(cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entityManager.TryGetComponent(driver, out RelayInputMoverComponent? relay));
                Assert.That(relay!.RelayEntity, Is.EqualTo(car), "driver WASD should be relayed to the car");
                Assert.That(mover.GetEffectiveMover(driver), Is.EqualTo(car));
                Assert.That(entityManager.GetComponent<InputMoverComponent>(driver).CanMove, Is.True, "driver CanMove must stay true or WASD will not relay");

                Assert.That(entityManager.TryGetComponent(car, out MovementRelayTargetComponent? target));
                Assert.That(target!.Source, Is.EqualTo(driver));
                Assert.That(entityManager.HasComponent<SkipMobMovementComponent>(car), Is.True, "car must skip omni-walk");
                Assert.That(entityManager.HasComponent<InputMoverComponent>(car), Is.True);

                Assert.That(entityManager.HasComponent<RelayInputMoverComponent>(passenger), Is.False);
                Assert.That(mover.GetEffectiveMover(passenger), Is.EqualTo(passenger));
                Assert.That(entityManager.GetComponent<InputMoverComponent>(passenger).CanMove, Is.False, "passenger must not walk while seated");
            });

            Assert.That(cars.TryEject((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver, driver, skipDelay: true), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entityManager.HasComponent<RelayInputMoverComponent>(driver), Is.False);
                Assert.That(entityManager.HasComponent<MovementRelayTargetComponent>(car), Is.False);
                Assert.That(mover.GetEffectiveMover(driver), Is.EqualTo(driver));
                Assert.That(entityManager.GetComponent<InputMoverComponent>(driver).CanMove, Is.True);
            });
        });
    }

    [Test]
    public async Task ParkedCarStaysIdleWithoutDriver()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        EntityUid car = default;

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity("VehicleKraz17", coords);
            Assert.That(entityManager.HasComponent<MovementRelayTargetComponent>(car), Is.False);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var physics = entityManager.GetComponent<PhysicsComponent>(car);
            Assert.That(physics.LinearVelocity.Length(), Is.LessThan(0.01f), "empty parked car must not be stepped as if driven");
        });
    }

    [Test]
    public async Task DriverCannotDropVirtualItems()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var hands = entityManager.System<SharedHandsSystem>();

        EntityUid car = default;
        EntityUid driver = default;
        EntityUid passenger = default;

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity("VehicleKraz17", coords);
            driver = entityManager.SpawnEntity(DummyId, coords);
            passenger = entityManager.SpawnEntity(DummyId, coords);

            Assert.That(cars.TryEnterSlot(driver, car, "driver", skipDelay: true), Is.True);
            Assert.That(cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), Is.True);

            Assert.That(hands.GetHandCount(driver), Is.GreaterThan(0));
            Assert.That(CountBlockingVirtuals(entityManager, driver, car), Is.EqualTo(hands.GetHandCount(driver)),
                "every driver hand should be occupied by the car");
            Assert.That(hands.CountFreeHands(driver), Is.EqualTo(0));
            Assert.That(CountBlockingVirtuals(entityManager, passenger, car), Is.EqualTo(0),
                "passengers keep their hands free");

            foreach (var held in hands.EnumerateHeld(driver))
            {
                Assert.That(entityManager.HasComponent<UnremoveableComponent>(held), Is.True);
                var blocking = entityManager.GetComponent<VirtualItemComponent>(held).BlockingEntity;
                Assert.That(blocking, Is.EqualTo(car));
                Assert.That(hands.TryDrop(driver, held), Is.False, "driver must not be able to drop occupancy virtual items");
            }

            Assert.That(CountBlockingVirtuals(entityManager, driver, car), Is.EqualTo(hands.GetHandCount(driver)));

            var driverCrowbar = entityManager.SpawnEntity("Crowbar", coords);
            Assert.That(hands.TryPickupAnyHand(driver, driverCrowbar), Is.False, "occupied driver hands must not pick up other items");
            Assert.That(hands.CountFreeHands(passenger), Is.EqualTo(hands.GetHandCount(passenger)));

            Assert.That(cars.TryEject((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver, driver, skipDelay: true), Is.True);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountBlockingVirtuals(entityManager, driver, car), Is.EqualTo(0));
            var crowbar = entityManager.SpawnEntity("Crowbar", coords);
            Assert.That(hands.TryPickupAnyHand(driver, crowbar), Is.True, "driver should use hands after leaving the car");
        });
    }

    [Test]
    public async Task CanEjectEachPassengerAfterDelay()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var containers = entityManager.System<SharedContainerSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid car = default;
        EntityUid passenger = default;
        EntityUid passenger2 = default;
        EntityUid rescuer = default;

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity("VehicleKraz17", coords);
            passenger = entityManager.SpawnEntity(DummyId, coords);
            passenger2 = entityManager.SpawnEntity(DummyId, coords);
            // Beside the hull, not inside the fixture, so the pull is not cancelled by collision shove.
            rescuer = entityManager.SpawnEntity(DummyId, coords.Offset(new Vector2(1.3f, 0f)));
            entityManager.EnsureComponent<DoAfterComponent>(rescuer);

            Assert.That(cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), Is.True);
            Assert.That(cars.TryEnterSlot(passenger2, car, "passenger2", skipDelay: true), Is.True);

            var carEnt = (car, entityManager.GetComponent<GrosseCarComponent>(car));
            Assert.That(cars.TryEject(carEnt, passenger, rescuer, skipDelay: false), Is.True);
            Assert.That(cars.TryEject(carEnt, passenger2, rescuer, skipDelay: false), Is.True,
                "a second seat must not cancel the first pull");

            Assert.That(containers.TryGetContainer(car, "car-passenger", out var first) && first.Contains(passenger));
            Assert.That(containers.TryGetContainer(car, "car-passenger2", out var second) && second.Contains(passenger2));
        });

        var ticks = (int)Math.Ceiling(1.6 / timing.TickPeriod.TotalSeconds) + 2;
        await server.WaitRunTicks(ticks);

        await server.WaitAssertion(() =>
        {
            var pending = entityManager.GetComponent<DoAfterComponent>(rescuer).DoAfters.Values
                .Select(doAfter => $"cancelled={doAfter.Cancelled} completed={doAfter.Completed}");
            Assert.That(entityManager.HasComponent<GrosseCarRiderComponent>(passenger), Is.False, string.Join(", ", pending));
            Assert.That(entityManager.HasComponent<GrosseCarRiderComponent>(passenger2), Is.False);
            Assert.That(containers.TryGetContainer(car, "car-passenger", out var first) && first.ContainedEntities.Count == 0);
            Assert.That(containers.TryGetContainer(car, "car-passenger2", out var second) && second.ContainedEntities.Count == 0);
        });
    }

    [Test]
    public async Task EngineOffDoesNotAccelerateUntilStarted()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        EntityUid car = default;
        EntityUid driver = default;

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity("VehicleKraz17", coords);
            driver = entityManager.SpawnEntity(DummyId, coords);
            Assert.That(cars.TryEnterSlot(driver, car, "driver", skipDelay: true), Is.True);

            var component = entityManager.GetComponent<GrosseCarComponent>(car);
            Assert.That(component.EngineState, Is.EqualTo(GrosseCarEngineState.Off));
            entityManager.GetComponent<InputMoverComponent>(car).HeldMoveButtons = MoveButtons.Up;
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            var physics = entityManager.GetComponent<PhysicsComponent>(car);
            Assert.That(physics.LinearVelocity.Length(), Is.LessThan(0.2f), "throttle must not move a stopped engine");

            var driver = entityManager.System<SharedContainerSystem>().GetContainer(car, "car-driver").ContainedEntities[0];
            Assert.That(cars.TryToggleEngine((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver), Is.True);
            Assert.That(entityManager.GetComponent<GrosseCarComponent>(car).EngineState, Is.EqualTo(GrosseCarEngineState.Starting));

            entityManager.GetComponent<GrosseCarComponent>(car).EngineState = GrosseCarEngineState.On;
            entityManager.GetComponent<InputMoverComponent>(car).HeldMoveButtons = MoveButtons.Up;
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            var physics = entityManager.GetComponent<PhysicsComponent>(car);
            Assert.That(physics.LinearVelocity.Length(), Is.GreaterThan(1f), "a running engine should pull away under throttle");
        });
    }

    [Test]
    public async Task WreckedEngineDoesNotStart()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;
        var entityManager = server.EntMan;
        var cars = entityManager.System<SharedGrosseCarSystem>();
        var damageable = entityManager.System<DamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var car = entityManager.SpawnEntity("VehicleKraz17", coords);
            var driver = entityManager.SpawnEntity(DummyId, coords);
            Assert.That(cars.TryEnterSlot(driver, car, "driver", skipDelay: true), Is.True);

            var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = 250 } };
            Assert.That(damageable.TryChangeDamage(car, hit, ignoreResistances: true), Is.True);

            var component = entityManager.GetComponent<GrosseCarComponent>(car);
            Assert.That(damageable.GetTotalDamage(car).Float(), Is.GreaterThanOrEqualTo(component.MaxDriveDamage));
            Assert.That(cars.TryToggleEngine((car, component), driver), Is.False);
            Assert.That(component.EngineState, Is.EqualTo(GrosseCarEngineState.Off));
        });
    }

    private static int CountBlockingVirtuals(IEntityManager entityManager, EntityUid user, EntityUid blocking)
    {
        var hands = entityManager.System<SharedHandsSystem>();
        var count = 0;
        foreach (var held in hands.EnumerateHeld(user))
        {
            if (!entityManager.TryGetComponent(held, out VirtualItemComponent? virt))
                continue;

            var blockingEntity = virt.BlockingEntity;
            if (blockingEntity == blocking)
                count++;
        }

        return count;
    }
}
