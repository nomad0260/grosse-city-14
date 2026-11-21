using Content.Shared._Grosse.Containment;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.Containment.UI;

public sealed class ContainmentConsoleBoundUserInterface : BoundUserInterface
{
    private ContainmentConsoleWindow? _window;

    public ContainmentConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ContainmentConsoleWindow>();

        _window.OnToggleProjector += index => SendMessage(new ContainmentToggleStabilizerMessage(index));
        _window.OnProjectorIntensity += (index, intensity) =>
            SendMessage(new ContainmentSetProjectorIntensityMessage(index, intensity));
        _window.OnPowerChanged += power => SendMessage(new ReactorSetPowerMessage(power));
        _window.OnEmergencyStop += restart => SendMessage(new ReactorEmergencyStopMessage(restart));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is ContainmentConsoleState consoleState)
            _window?.UpdateState(consoleState);
    }
}
