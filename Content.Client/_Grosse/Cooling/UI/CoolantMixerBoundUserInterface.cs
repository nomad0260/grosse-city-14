using Content.Shared._Grosse.Cooling;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.Cooling.UI;

public sealed class CoolantMixerBoundUserInterface : BoundUserInterface
{
    private CoolantMixerWindow? _window;

    public CoolantMixerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CoolantMixerWindow>();

        _window.OnInsertInput += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolantMixerSlots.Input, tryEject: false, tryInsert: true));
        _window.OnEjectInput += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolantMixerSlots.Input, tryEject: true, tryInsert: false));
        _window.OnInsertOutput += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolantMixerSlots.Output, tryEject: false, tryInsert: true));
        _window.OnEjectOutput += () =>
            SendMessage(new ItemSlotButtonPressedEvent(CoolantMixerSlots.Output, tryEject: true, tryInsert: false));

        _window.OnPourIn += () => SendMessage(new CoolantMixerTransferMessage(CoolantMixerTransferMode.PourIn));
        _window.OnPourOut += () => SendMessage(new CoolantMixerTransferMessage(CoolantMixerTransferMode.PourOut));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CoolantMixerBoundUserInterfaceState mixerState)
            _window?.UpdateState(mixerState);
    }
}
