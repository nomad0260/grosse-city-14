using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;

namespace Content.Shared._Grosse.Clothing;

/// <summary>
/// Occupies extra inventory slots with virtual copies of clothing while it is worn.
/// </summary>
public sealed partial class ClothingRequiredSlotsSystem : EntitySystem
{
    private static readonly SlotFlags[] SingleFlags =
    [
        SlotFlags.PREVENTEQUIP,
        SlotFlags.HEAD,
        SlotFlags.EYES,
        SlotFlags.EARS,
        SlotFlags.MASK,
        SlotFlags.OUTERCLOTHING,
        SlotFlags.INNERCLOTHING,
        SlotFlags.NECK,
        SlotFlags.BACK,
        SlotFlags.BELT,
        SlotFlags.GLOVES,
        SlotFlags.IDCARD,
        SlotFlags.POCKET,
        SlotFlags.LEGS,
        SlotFlags.FEET,
        SlotFlags.SUITSTORAGE,
    ];

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingRequiredSlotsComponent, BeingEquippedAttemptEvent>(OnBeingEquipped);
        SubscribeLocalEvent<ClothingRequiredSlotsComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothingRequiredSlotsComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ClothingRequiredSlotsComponent, InventoryRelayedEvent<IsEquippingTargetAttemptEvent>>(OnEquipIntoOccupiedSlot);
        SubscribeLocalEvent<ClothingRequiredSlotsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnBeingEquipped(Entity<ClothingRequiredSlotsComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || !IsValidWearSlot(ent, args.SlotFlags))
            return;

        if (!TryGetOccupiedSlot(args.EquipTarget, ent.Comp.Slots, args.Slot, out _))
            return;

        args.Reason = "clothing-required-slots-blocked";
        args.Cancel();
    }

    private void OnEquipIntoOccupiedSlot(Entity<ClothingRequiredSlotsComponent> ent, ref InventoryRelayedEvent<IsEquippingTargetAttemptEvent> args)
    {
        if (args.Args.Cancelled || (args.Args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        // The slot this clothing is already worn in is occupied by the item itself.
        if (TryComp<ClothingComponent>(ent, out var clothing) && clothing.InSlot == args.Args.Slot)
            return;

        args.Args.Reason = "clothing-required-slots-blocked";
        args.Args.Cancel();
    }

    private void OnEquipped(Entity<ClothingRequiredSlotsComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!_inventory.TryGetSlots(args.Wearer, out var slots))
            return;

        foreach (var slot in slots)
        {
            if (!IsExtraSlot(ent.Comp.Slots, slot, args.Clothing.InSlot))
                continue;

            if (_inventory.TryGetSlotEntity(args.Wearer, slot.Name, out _))
                continue;

            if (!_virtualItem.TrySpawnVirtualItemInInventory(ent, args.Wearer, slot.Name, force: true, out var virtualItem))
                continue;

            // TrySpawnVirtualItemInInventory reports success even when the equip insert fails.
            if (_inventory.TryGetSlotEntity(args.Wearer, slot.Name, out var slotted) && slotted == virtualItem)
                continue;

            if (TryComp<VirtualItemComponent>(virtualItem, out var virtualComp))
                _virtualItem.DeleteVirtualItem((virtualItem.Value, virtualComp), args.Wearer);
        }
    }

    private void OnUnequipped(Entity<ClothingRequiredSlotsComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _virtualItem.DeleteInSlotMatching(args.Wearer, ent);
    }

    private void OnExamined(Entity<ClothingRequiredSlotsComponent> ent, ref ExaminedEvent args)
    {
        var extra = ent.Comp.Slots;
        if (TryComp<ClothingComponent>(ent, out var clothing))
            extra &= ~clothing.Slots;

        var names = new List<string>();
        foreach (var flag in SingleFlags)
        {
            if ((extra & flag) == 0)
                continue;

            names.Add(Loc.GetString(SlotLocId(flag)));
        }

        if (names.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("clothing-required-slots-examine", ("slots", string.Join(", ", names))));
    }

    private bool IsValidWearSlot(EntityUid clothing, SlotFlags slotFlags)
    {
        return TryComp<ClothingComponent>(clothing, out var clothingComp)
               && (clothingComp.Slots & slotFlags) != 0;
    }

    private bool TryGetOccupiedSlot(EntityUid wearer, SlotFlags required, string skipSlot, out string? occupiedSlot)
    {
        occupiedSlot = null;
        if (!_inventory.TryGetSlots(wearer, out var slots))
            return false;

        foreach (var slot in slots)
        {
            if (!IsExtraSlot(required, slot, skipSlot))
                continue;

            if (!_inventory.TryGetSlotEntity(wearer, slot.Name, out _))
                continue;

            occupiedSlot = slot.Name;
            return true;
        }

        return false;
    }

    private static bool IsExtraSlot(SlotFlags required, SlotDefinition slot, string? skipSlot)
    {
        return slot.Name != skipSlot && (slot.SlotFlags & required) != 0;
    }

    private static string SlotLocId(SlotFlags flag)
    {
        return flag switch
        {
            SlotFlags.PREVENTEQUIP => "clothing-required-slots-slot-preventequip",
            SlotFlags.HEAD => "clothing-required-slots-slot-head",
            SlotFlags.EYES => "clothing-required-slots-slot-eyes",
            SlotFlags.EARS => "clothing-required-slots-slot-ears",
            SlotFlags.MASK => "clothing-required-slots-slot-mask",
            SlotFlags.OUTERCLOTHING => "clothing-required-slots-slot-outerclothing",
            SlotFlags.INNERCLOTHING => "clothing-required-slots-slot-innerclothing",
            SlotFlags.NECK => "clothing-required-slots-slot-neck",
            SlotFlags.BACK => "clothing-required-slots-slot-back",
            SlotFlags.BELT => "clothing-required-slots-slot-belt",
            SlotFlags.GLOVES => "clothing-required-slots-slot-gloves",
            SlotFlags.IDCARD => "clothing-required-slots-slot-idcard",
            SlotFlags.POCKET => "clothing-required-slots-slot-pocket",
            SlotFlags.LEGS => "clothing-required-slots-slot-legs",
            SlotFlags.FEET => "clothing-required-slots-slot-feet",
            SlotFlags.SUITSTORAGE => "clothing-required-slots-slot-suitstorage",
            _ => "clothing-required-slots-slot-unknown",
        };
    }
}
