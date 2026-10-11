using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Clothing;

/// <summary>
/// While this clothing is worn in a valid slot, it also requires and occupies the inventory slots listed in <see cref="Slots"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ClothingRequiredSlotsSystem))]
public sealed partial class ClothingRequiredSlotsComponent : Component
{
    /// <summary>
    /// Extra slots this clothing occupies. The slot it is actually worn in is skipped.
    /// </summary>
    [DataField(required: true)]
    public SlotFlags Slots = SlotFlags.NONE;
}
