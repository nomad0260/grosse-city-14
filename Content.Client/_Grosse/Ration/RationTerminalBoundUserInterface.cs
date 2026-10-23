using Content.Shared._Grosse.Ration;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.Ration;

[UsedImplicitly]
public sealed class RationTerminalBoundUserInterface : BoundUserInterface
{
    private RationTerminalWindow? _window;
    private RationTerminalBoundUserInterfaceState? _state;

    public RationTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RationTerminalWindow>();
        _window.OnQuotaChanged += (jobId, quota) => SendMessage(new RationTerminalSetQuotaMessage(jobId, quota));
        _window.OnIssueOne += body => SendMessage(new RationTerminalIssueMessage(body));
        _window.OnIssueAll += () => SendMessage(new RationTerminalIssueAllMessage());

        if (_state != null)
            _window.UpdateEntries(_state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RationTerminalBoundUserInterfaceState cast)
            return;

        _state = cast;
        _window?.UpdateEntries(cast);
    }
}
