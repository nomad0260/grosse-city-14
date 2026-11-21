using Content.Shared._Grosse.Cooling;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.Cooling.UI;

public sealed class CoolerBoundUserInterface : BoundUserInterface
{
    private CoolerWindow? _window;

    public CoolerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CoolerWindow>();

        _window.OnInsertCoolant += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolerSlots.Coolant, tryEject: false, tryInsert: true));
        _window.OnEjectCoolant += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolerSlots.Coolant, tryEject: true, tryInsert: false));
        _window.OnInsertWaste += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolerSlots.Waste, tryEject: false, tryInsert: true));
        _window.OnEjectWaste += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolerSlots.Waste, tryEject: true, tryInsert: false));

        _window.OnFill += () => SendMessage(new CoolerTransferMessage(CoolerTransferMode.Fill));
        _window.OnDrainWaste += () => SendMessage(new CoolerTransferMessage(CoolerTransferMode.DrainWaste));
        _window.OnDrainAll += () => SendMessage(new CoolerTransferMessage(CoolerTransferMode.DrainAll));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CoolerBoundUserInterfaceState coolerState)
            _window?.UpdateState(coolerState);
    }
}
