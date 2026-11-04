#nullable enable
using System.Collections.Generic;
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
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Grosse;

[TestFixture]
[TestOf(typeof(SharedGrosseCarSystem))]
public sealed class GrosseCarTest : GameTest
{
    private const string DummyId = "GrosseCarTestDummy";
    private const string CarId = "GrosseCarTestCar";

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
- type: entity
  id: {CarId}
  name: {CarId}
  components:
  - type: Transform
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.5,-0.5,0.5,0.5""
        density: 50
        mask:
        - MobMask
        layer:
        - MachineLayer
  - type: GrosseCar
    entryDelay: 0
    exitDelay: 1.5
    occupantActions:
    - ActionGrosseCarExit
    slots:
    - id: driver
      name: grosse-car-slot-driver
      containerId: car-driver
      isDriver: true
      visuals: Driver
    - id: passenger
      name: grosse-car-slot-passenger
      containerId: car-passenger
      visuals: Passenger1
    - id: passenger2
      name: grosse-car-slot-passenger2
      containerId: car-passenger2
      visuals: Passenger2
  - type: ContainerContainer
    containers:
      car-driver: !type:ContainerSlot
      car-passenger: !type:ContainerSlot
      car-passenger2: !type:ContainerSlot
      actions: !type:Container
  - type: SkipMobMovement
  - type: InputMover
  - type: DoAfter
  - type: Appearance
  - type: ActionsContainer
";

    [Test]
    public async Task PrototypeConfigIsValid()
    {
        var pair = Pair;
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        var failures = new List<string>();
        await server.WaitAssertion(() =>
        {
            foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || pair.IsTestPrototype(proto))
                    continue;

                if (!proto.TryGetComponent(out GrosseCarComponent? car, factory))
                    continue;

                Note(failures, car.Slots.Count > 0, $"{proto.ID} has no seats");
                Note(failures, car.Slots.Select(s => s.Id).Distinct().Count() == car.Slots.Count, $"{proto.ID} has duplicate slot ids");
                Note(failures, car.Slots.Select(s => s.ContainerId).Distinct().Count() == car.Slots.Count, $"{proto.ID} has duplicate container ids");
                Note(failures, car.Slots.Count(s => s.IsDriver) == 1, $"{proto.ID} must have exactly one driver slot");
                Note(failures, car.EngineForce > 0f, $"{proto.ID} engineForce");
                Note(failures, car.EngineStartSound != null, $"{proto.ID} engineStartSound");
                Note(failures, car.EngineStopSound != null, $"{proto.ID} engineStopSound");
                Note(failures, car.EngineIdleSound != null, $"{proto.ID} engineIdleSound");
                Note(failures, car.EngineMoveStartSound != null, $"{proto.ID} engineMoveStartSound");
                Note(failures, car.EngineMoveStartDuration > 0f, $"{proto.ID} engineMoveStartDuration");
                Note(failures, car.EngineDriveSound != null, $"{proto.ID} engineDriveSound");
                Note(failures, car.EngineState == GrosseCarEngineState.Off, $"{proto.ID} engine starts off");
                Note(failures, car.MaxForwardSpeed > 0f, $"{proto.ID} maxForwardSpeed");
                Note(failures, proto.HasComponent<SkipMobMovementComponent>(factory), $"{proto.ID} missing SkipMobMovement");
                Note(failures, proto.HasComponent<InputMoverComponent>(factory), $"{proto.ID} missing InputMover");
                Note(failures, proto.HasComponent<AppearanceComponent>(factory), $"{proto.ID} missing Appearance");
                Note(failures, proto.TryGetComponent(out PhysicsComponent? physics, factory), $"{proto.ID} missing Physics");
                if (physics != null)
                    Note(failures, physics.BodyType == BodyType.Dynamic, $"{proto.ID} must be Dynamic so it collides with walking mobs");
                Note(failures, proto.TryGetComponent(out FixturesComponent? _, factory), $"{proto.ID} missing Fixtures");
                Note(failures, proto.TryGetComponent(out ContainerManagerComponent? containers, factory), $"{proto.ID} missing ContainerContainer");

                if (containers == null)
                    continue;

                foreach (var slot in car.Slots)
                {
                    Note(failures, loc.HasString(slot.Name), $"{proto.ID} slot {slot.Id} missing locale {slot.Name}");
                    Note(failures, containers.Containers.ContainsKey(slot.ContainerId), $"{proto.ID} missing container {slot.ContainerId}");
                    if (containers.Containers.TryGetValue(slot.ContainerId, out var container))
                        Note(failures, container is ContainerSlot, $"{proto.ID} {slot.ContainerId} is not a ContainerSlot");
                }

                foreach (var action in car.OccupantActions)
                    Note(failures, protoMan.HasIndex(action), $"{proto.ID} occupant action {action} missing");
            }
        });

        // Assert on this test's thread. An assertion inside WaitAssertion is recorded on whichever
        // test first started the pooled server, so GitHub would name the wrong test.
        Report(failures);
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

        var failures = new List<string>();
        await server.WaitAssertion(() =>
        {
            var car = entityManager.SpawnEntity(CarId, coords);
            var driver = entityManager.SpawnEntity(DummyId, coords);
            var passenger = entityManager.SpawnEntity(DummyId, coords);
            var passenger2 = entityManager.SpawnEntity(DummyId, coords);
            var extra = entityManager.SpawnEntity(DummyId, coords);

            Note(failures, cars.TryEnterSlot(driver, car, "driver", skipDelay: true), "driver did not enter the driver seat");
            Note(failures, entityManager.TryGetComponent(driver, out GrosseCarRiderComponent? driverRider), "driver has no GrosseCarRider");
            Note(failures, driverRider?.IsDriver == true, "driver seat did not mark the rider as driver");
            Note(failures, entityManager.HasComponent<RelayInputMoverComponent>(driver), "driver has no RelayInputMover");
            Note(failures, containers.TryGetContainer(car, "car-driver", out var driverSlot) && driverSlot.Contains(driver), "driver is not in car-driver");

            Note(failures, cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), "passenger did not enter");
            Note(failures, entityManager.TryGetComponent(passenger, out GrosseCarRiderComponent? passRider), "passenger has no GrosseCarRider");
            Note(failures, passRider != null && !passRider.IsDriver, "passenger was marked as driver");
            Note(failures, !entityManager.HasComponent<RelayInputMoverComponent>(passenger), "passenger has RelayInputMover");

            Note(failures, cars.TryEnterSlot(passenger2, car, "passenger2", skipDelay: true), "second passenger did not enter");
            Note(failures, !cars.TryEnterSlot(extra, car, skipDelay: true), $"{CarId} has three seats; a fourth occupant must be refused");

            Note(failures, cars.TryEject((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver, driver, skipDelay: true), "driver was not ejected");
            Note(failures, !entityManager.HasComponent<GrosseCarRiderComponent>(driver), "driver still has GrosseCarRider after exit");
            Note(failures, !entityManager.HasComponent<RelayInputMoverComponent>(driver), "driver still has RelayInputMover after exit");
            Note(failures, containers.TryGetContainer(car, "car-driver", out var emptyDriver) && emptyDriver.ContainedEntities.Count == 0, "driver slot is not empty after exit");
        });
        Report(failures);
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

        var failures = new List<string>();
        await server.WaitAssertion(() =>
        {
            var car = entityManager.SpawnEntity(CarId, coords);
            var driver = entityManager.SpawnEntity(DummyId, coords);
            var passenger = entityManager.SpawnEntity(DummyId, coords);

            Note(failures, cars.TryEnterSlot(driver, car, "driver", skipDelay: true), "driver did not enter the driver seat");
            Note(failures, cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), "passenger did not enter");

            Note(failures, entityManager.TryGetComponent(driver, out RelayInputMoverComponent? relay), "driver has no RelayInputMover");
            Note(failures, relay?.RelayEntity == car, "driver WASD should be relayed to the car");
            Note(failures, mover.GetEffectiveMover(driver) == car, "effective mover of the driver is not the car");
            Note(failures, entityManager.GetComponent<InputMoverComponent>(driver).CanMove, "driver CanMove must stay true or WASD will not relay");

            Note(failures, entityManager.TryGetComponent(car, out MovementRelayTargetComponent? target), "car has no MovementRelayTarget");
            Note(failures, target?.Source == driver, "relay target source is not the driver");
            Note(failures, entityManager.HasComponent<SkipMobMovementComponent>(car), "car must skip omni-walk");
            Note(failures, entityManager.HasComponent<InputMoverComponent>(car), "car has no InputMover");

            Note(failures, !entityManager.HasComponent<RelayInputMoverComponent>(passenger), "passenger has RelayInputMover");
            Note(failures, mover.GetEffectiveMover(passenger) == passenger, "passenger effective mover changed");
            Note(failures, !entityManager.GetComponent<InputMoverComponent>(passenger).CanMove, "passenger must not walk while seated");

            Note(failures, cars.TryEject((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver, driver, skipDelay: true), "driver was not ejected");

            Note(failures, !entityManager.HasComponent<RelayInputMoverComponent>(driver), "driver still has RelayInputMover after exit");
            Note(failures, !entityManager.HasComponent<MovementRelayTargetComponent>(car), "car still has MovementRelayTarget after the driver left");
            Note(failures, mover.GetEffectiveMover(driver) == driver, "driver effective mover did not return to the driver");
            Note(failures, entityManager.GetComponent<InputMoverComponent>(driver).CanMove, "driver CanMove is false after exit");
        });
        Report(failures);
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
        var failures = new List<string>();

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity(CarId, coords);
            Note(failures, !entityManager.HasComponent<MovementRelayTargetComponent>(car), "parked car spawned with a movement relay");
        });
        Report(failures);

        await server.WaitRunTicks(10);

        var speed = 0f;
        await server.WaitAssertion(() =>
        {
            speed = entityManager.GetComponent<PhysicsComponent>(car).LinearVelocity.Length();
        });
        Assert.That(speed, Is.LessThan(0.01f), "empty parked car must not be stepped as if driven");
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
        var failures = new List<string>();

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity(CarId, coords);
            driver = entityManager.SpawnEntity(DummyId, coords);
            var passenger = entityManager.SpawnEntity(DummyId, coords);

            Note(failures, cars.TryEnterSlot(driver, car, "driver", skipDelay: true), "driver did not enter the driver seat");
            Note(failures, cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), "passenger did not enter");
            if (failures.Count > 0)
                return;

            Note(failures, hands.GetHandCount(driver) > 0, "driver has no hands");
            Note(failures, CountBlockingVirtuals(entityManager, driver, car) == hands.GetHandCount(driver),
                "every driver hand should be occupied by the car");
            Note(failures, hands.CountFreeHands(driver) == 0, "driver still has a free hand");
            Note(failures, CountBlockingVirtuals(entityManager, passenger, car) == 0, "passengers keep their hands free");

            foreach (var held in hands.EnumerateHeld(driver))
            {
                Note(failures, entityManager.HasComponent<UnremoveableComponent>(held), "occupancy virtual item can be removed");
                var blocking = entityManager.GetComponent<VirtualItemComponent>(held).BlockingEntity;
                Note(failures, blocking == car, "occupancy virtual item does not block on the car");
                Note(failures, !hands.TryDrop(driver, held), "driver must not be able to drop occupancy virtual items");
            }

            Note(failures, CountBlockingVirtuals(entityManager, driver, car) == hands.GetHandCount(driver),
                "dropping removed an occupancy virtual item");

            var driverCrowbar = entityManager.SpawnEntity("Crowbar", coords);
            Note(failures, !hands.TryPickupAnyHand(driver, driverCrowbar), "occupied driver hands must not pick up other items");
            Note(failures, hands.CountFreeHands(passenger) == hands.GetHandCount(passenger), "passenger lost a free hand");

            Note(failures, cars.TryEject((car, entityManager.GetComponent<GrosseCarComponent>(car)), driver, driver, skipDelay: true), "driver was not ejected");
        });
        Report(failures);

        await server.WaitRunTicks(5);

        var leftVirtuals = -1;
        var canPickup = false;
        await server.WaitAssertion(() =>
        {
            leftVirtuals = CountBlockingVirtuals(entityManager, driver, car);
            var crowbar = entityManager.SpawnEntity("Crowbar", coords);
            canPickup = hands.TryPickupAnyHand(driver, crowbar);
        });
        Assert.Multiple(() =>
        {
            Assert.That(leftVirtuals, Is.EqualTo(0), "occupancy virtual items remained after the driver left");
            Assert.That(canPickup, Is.True, "driver should use hands after leaving the car");
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
        var failures = new List<string>();

        await server.WaitAssertion(() =>
        {
            car = entityManager.SpawnEntity(CarId, coords);
            passenger = entityManager.SpawnEntity(DummyId, coords);
            passenger2 = entityManager.SpawnEntity(DummyId, coords);
            // Beside the hull, not inside the fixture, so the pull is not cancelled by collision shove.
            rescuer = entityManager.SpawnEntity(DummyId, coords.Offset(new Vector2(1.3f, 0f)));
            entityManager.EnsureComponent<DoAfterComponent>(rescuer);

            Note(failures, cars.TryEnterSlot(passenger, car, "passenger", skipDelay: true), "first passenger did not enter");
            Note(failures, cars.TryEnterSlot(passenger2, car, "passenger2", skipDelay: true), "second passenger did not enter");

            var carEnt = (car, entityManager.GetComponent<GrosseCarComponent>(car));
            Note(failures, cars.TryEject(carEnt, passenger, rescuer, skipDelay: false), "first pull did not start");
            Note(failures, cars.TryEject(carEnt, passenger2, rescuer, skipDelay: false), "a second seat must not cancel the first pull");

            Note(failures, containers.TryGetContainer(car, "car-passenger", out var first) && first.Contains(passenger), "first passenger left the seat before the delay");
            Note(failures, containers.TryGetContainer(car, "car-passenger2", out var second) && second.Contains(passenger2), "second passenger left the seat before the delay");
        });
        Report(failures);

        var ticks = (int)Math.Ceiling(1.6 / timing.TickPeriod.TotalSeconds) + 2;
        await server.WaitRunTicks(ticks);

        var after = new List<string>();
        await server.WaitAssertion(() =>
        {
            var pending = string.Join(", ", entityManager.GetComponent<DoAfterComponent>(rescuer).DoAfters.Values
                .Select(doAfter => $"cancelled={doAfter.Cancelled} completed={doAfter.Completed}"));
            Note(after, !entityManager.HasComponent<GrosseCarRiderComponent>(passenger), $"first passenger was not ejected. {pending}");
            Note(after, !entityManager.HasComponent<GrosseCarRiderComponent>(passenger2), "second passenger was not ejected");
            Note(after, containers.TryGetContainer(car, "car-passenger", out var first) && first.ContainedEntities.Count == 0, "first seat is still occupied");
            Note(after, containers.TryGetContainer(car, "car-passenger2", out var second) && second.ContainedEntities.Count == 0, "second seat is still occupied");
        });
        Report(after);
    }

    /// <summary>
    /// Records a failed check without calling NUnit. Assertions inside <c>WaitAssertion</c> run on the
    /// pooled server thread and are filed on whichever test first started that pair.
    /// </summary>
    private static void Note(List<string> failures, bool ok, string message)
    {
        if (!ok)
            failures.Add(message);
    }

    private static void Report(List<string> failures)
    {
        if (failures.Count > 0)
            Assert.Fail(string.Join("\n", failures));
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
