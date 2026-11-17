using Content.Shared._Grosse.CommanderTerminal;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.CommanderTerminal;

[UsedImplicitly]
public sealed class CommanderTerminalBoundUserInterface : BoundUserInterface
{
    private CommanderTerminalWindow? _window;

    public CommanderTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CommanderTerminalWindow>();
        _window.OnCreateGroup += name => SendMessage(new CreateCommanderGroupMessage(name));
        _window.OnRenameGroup += (groupId, name) => SendMessage(new RenameCommanderGroupMessage(groupId, name));
        _window.OnDeleteGroup += groupId => SendMessage(new DeleteCommanderGroupMessage(groupId));
        _window.OnAssign += (groupId, memberId) => SendMessage(new AssignCommanderMemberMessage(groupId, memberId));
        _window.OnRemove += memberId => SendMessage(new RemoveCommanderMemberMessage(memberId));
        _window.OnSetLeader += (groupId, memberId) => SendMessage(new SetCommanderLeaderMessage(groupId, memberId));
        _window.OnIssueOrder += (groupId, grid, position) => SendMessage(new IssueCommanderOrderMessage(groupId, grid, position));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window != null && state is CommanderTerminalState terminal)
            _window.ApplyMenu(terminal);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window != null && message is CommanderMapUpdateMessage map)
            _window.ApplyMap(map.Entries);
    }
}
