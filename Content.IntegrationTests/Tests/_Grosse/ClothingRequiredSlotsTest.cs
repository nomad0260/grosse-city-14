#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Grosse;

[TestFixture]
public sealed class ClothingRequiredSlotsTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ClothingRequiredSlotsHumanDummy
  components:
  - type: Inventory
  - type: ContainerContainer

- type: entity
  id: ClothingRequiredSlotsSuitDummy
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: ClothingRequiredSlots
    slots: [belt, feet]
  - type: Item
    size: Tiny

- type: entity
  id: ClothingRequiredSlotsBeltDummy
  components:
  - type: Clothing
    slots: [belt]
  - type: Item
    size: Tiny

- type: entity
  id: ClothingRequiredSlotsShoesDummy
  components:
  - type: Clothing
    slots: [feet]
  - type: Item
    size: Tiny

- type: entity
  id: ClothingRequiredSlotsUniformDummy
  components:
  - type: Clothing
    slots: [innerclothing]
  - type: Item
    size: Tiny
";

    [Test]
    public async Task WornSuitOccupiesBeltAndShoesUntilRemoved()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = server.ResolveDependency<IEntityManager>();
            var inv = ent.System<InventorySystem>();
            var human = ent.SpawnEntity("ClothingRequiredSlotsHumanDummy", map.GridCoords);
            var suit = ent.SpawnEntity("ClothingRequiredSlotsSuitDummy", map.GridCoords);
            var belt = ent.SpawnEntity("ClothingRequiredSlotsBeltDummy", map.GridCoords);
            var shoes = ent.SpawnEntity("ClothingRequiredSlotsShoesDummy", map.GridCoords);

            Assert.That(inv.TryEquip(human, belt, "belt", silent: true), Is.True);
            Assert.That(inv.CanEquip(human, suit, "outerClothing", out var beltReason), Is.False);
            Assert.That(beltReason, Is.EqualTo("clothing-required-slots-blocked"));
            Assert.That(inv.TryEquip(human, suit, "outerClothing", silent: true), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "outerClothing", out _), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "belt", out var wornBelt) && wornBelt == belt);
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out _), Is.False);

            Assert.That(inv.TryUnequip(human, "belt", silent: true), Is.True);
            Assert.That(inv.TryEquip(human, shoes, "shoes", silent: true), Is.True);
            Assert.That(inv.CanEquip(human, suit, "outerClothing", out var shoesReason), Is.False);
            Assert.That(shoesReason, Is.EqualTo("clothing-required-slots-blocked"));
            Assert.That(inv.TryEquip(human, suit, "outerClothing", silent: true), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out var wornShoes) && wornShoes == shoes);
            Assert.That(inv.TryGetSlotEntity(human, "belt", out _), Is.False);

            Assert.That(inv.TryUnequip(human, "shoes", silent: true), Is.True);
            Assert.That(inv.TryEquip(human, suit, "outerClothing", silent: true), Is.True);
            Assert.That(inv.TryGetSlotEntity(human, "outerClothing", out var wornSuit) && wornSuit == suit);
            AssertVirtualOccupant(ent, inv, human, "belt", suit);
            AssertVirtualOccupant(ent, inv, human, "shoes", suit);

            var otherSuit = ent.SpawnEntity("ClothingRequiredSlotsSuitDummy", map.GridCoords);
            var otherBelt = ent.SpawnEntity("ClothingRequiredSlotsBeltDummy", map.GridCoords);
            var otherShoes = ent.SpawnEntity("ClothingRequiredSlotsShoesDummy", map.GridCoords);
            Assert.That(inv.CanEquip(human, otherBelt, "belt", out _), Is.False);
            Assert.That(inv.TryEquip(human, otherBelt, "belt", silent: true), Is.False);
            Assert.That(inv.CanEquip(human, otherShoes, "shoes", out _), Is.False);
            Assert.That(inv.TryEquip(human, otherShoes, "shoes", silent: true), Is.False);
            Assert.That(inv.CanEquip(human, otherSuit, "outerClothing", out _), Is.False);
            Assert.That(inv.TryEquip(human, otherSuit, "outerClothing", silent: true), Is.False);
            Assert.That(inv.TryUnequip(human, "shoes", silent: true), Is.False);
            Assert.That(inv.TryUnequip(human, "belt", silent: true), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "outerClothing", out wornSuit) && wornSuit == suit);

            Assert.That(inv.TryUnequip(human, "outerClothing", silent: true), Is.True);
            ent.DeleteEntity(human);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() => mapSystem.DeleteMap(map.MapId));
    }

    [Test]
    public async Task UnequippingSuitFreesBeltAndShoes()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var mapSystem = server.System<SharedMapSystem>();
        var ent = server.ResolveDependency<IEntityManager>();
        EntityUid human = default;
        EntityUid belt = default;
        EntityUid shoes = default;

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            human = ent.SpawnEntity("ClothingRequiredSlotsHumanDummy", map.GridCoords);
            var suit = ent.SpawnEntity("ClothingRequiredSlotsSuitDummy", map.GridCoords);
            Assert.That(inv.TryEquip(human, suit, "outerClothing", silent: true), Is.True);
            Assert.That(inv.TryUnequip(human, "outerClothing", silent: true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            Assert.That(inv.TryGetSlotEntity(human, "belt", out _), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out _), Is.False);

            belt = ent.SpawnEntity("ClothingRequiredSlotsBeltDummy", map.GridCoords);
            shoes = ent.SpawnEntity("ClothingRequiredSlotsShoesDummy", map.GridCoords);
            Assert.That(inv.TryEquip(human, belt, "belt", silent: true), Is.True);
            Assert.That(inv.TryEquip(human, shoes, "shoes", silent: true), Is.True);
            Assert.That(inv.TryGetSlotEntity(human, "belt", out var wornBelt) && wornBelt == belt);
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out var wornShoes) && wornShoes == shoes);
            ent.DeleteEntity(human);
        });

        await server.WaitAssertion(() => mapSystem.DeleteMap(map.MapId));
    }

    [Test]
    public async Task PocketedSuitDoesNotOccupySlots()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = server.ResolveDependency<IEntityManager>();
            var inv = ent.System<InventorySystem>();
            var human = ent.SpawnEntity("ClothingRequiredSlotsHumanDummy", map.GridCoords);
            var uniform = ent.SpawnEntity("ClothingRequiredSlotsUniformDummy", map.GridCoords);
            var suit = ent.SpawnEntity("ClothingRequiredSlotsSuitDummy", map.GridCoords);
            var belt = ent.SpawnEntity("ClothingRequiredSlotsBeltDummy", map.GridCoords);
            var shoes = ent.SpawnEntity("ClothingRequiredSlotsShoesDummy", map.GridCoords);

            Assert.That(inv.TryEquip(human, uniform, "jumpsuit", silent: true), Is.True);
            Assert.That(inv.TryEquip(human, suit, "pocket1", silent: true), Is.True);
            Assert.That(inv.TryGetSlotEntity(human, "belt", out _), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out _), Is.False);
            Assert.That(inv.CanEquip(human, belt, "belt", out _), Is.True);
            Assert.That(inv.CanEquip(human, shoes, "shoes", out _), Is.True);
            Assert.That(inv.TryEquip(human, belt, "belt", silent: true), Is.True);
            Assert.That(inv.TryEquip(human, shoes, "shoes", silent: true), Is.True);

            ent.DeleteEntity(human);
            mapSystem.DeleteMap(map.MapId);
        });
    }

    [Test]
    public async Task DeletingWornSuitFreesSlots()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var mapSystem = server.System<SharedMapSystem>();
        var ent = server.ResolveDependency<IEntityManager>();
        EntityUid human = default;
        EntityUid virtualBelt = default;
        EntityUid virtualShoes = default;

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            human = ent.SpawnEntity("ClothingRequiredSlotsHumanDummy", map.GridCoords);
            var suit = ent.SpawnEntity("ClothingRequiredSlotsSuitDummy", map.GridCoords);
            Assert.That(inv.TryEquip(human, suit, "outerClothing", silent: true), Is.True);
            Assert.That(inv.TryGetSlotEntity(human, "belt", out var beltSlot));
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out var shoesSlot));
            virtualBelt = beltSlot!.Value;
            virtualShoes = shoesSlot!.Value;
            ent.DeleteEntity(suit);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var inv = ent.System<InventorySystem>();
            Assert.That(ent.Deleted(virtualBelt), Is.True);
            Assert.That(ent.Deleted(virtualShoes), Is.True);
            Assert.That(inv.TryGetSlotEntity(human, "belt", out _), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "shoes", out _), Is.False);
            Assert.That(inv.TryGetSlotEntity(human, "outerClothing", out _), Is.False);
            ent.DeleteEntity(human);
            mapSystem.DeleteMap(map.MapId);
        });
    }

    [Test]
    public async Task ExamineListsExtraSlots()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = server.ResolveDependency<IEntityManager>();
            var loc = server.ResolveDependency<ILocalizationManager>();
            var suit = ent.SpawnEntity("ClothingRequiredSlotsSuitDummy", map.GridCoords);
            var examined = new ExaminedEvent(new FormattedMessage(), suit, suit, true, false);
            ent.EventBus.RaiseLocalEvent(suit, examined);

            var slots = string.Join(", ",
                loc.GetString("clothing-required-slots-slot-belt"),
                loc.GetString("clothing-required-slots-slot-feet"));
            var expected = loc.GetString("clothing-required-slots-examine", ("slots", slots));
            Assert.That(examined.GetTotalMessage().ToString(), Does.Contain(expected));

            ent.DeleteEntity(suit);
            mapSystem.DeleteMap(map.MapId);
        });
    }

    private static void AssertVirtualOccupant(
        IEntityManager ent,
        InventorySystem inv,
        EntityUid wearer,
        string slot,
        EntityUid suit)
    {
        Assert.That(inv.TryGetSlotEntity(wearer, slot, out var occupant), Is.True);
        Assert.That(ent.TryGetComponent(occupant, out VirtualItemComponent? virtualItem), Is.True);
        Assert.That(virtualItem!.BlockingEntity, Is.EqualTo(suit));
    }
}
