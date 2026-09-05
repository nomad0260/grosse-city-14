using Content.Server.EUI;
using Content.Shared._Grosse.Pvp.UI;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Server._Grosse.Pvp.UI;

public sealed class PvpClassSelectEui : BaseEui
{
    private readonly Action<NetUserId, string> _onSelect;
    private readonly Action<NetUserId> _onClosed;
    private readonly NetUserId _user;
    private PvpClassSelectEuiState _state;

    public PvpClassSelectEui(
        NetUserId user,
        PvpClassSelectEuiState state,
        Action<NetUserId, string> onSelect,
        Action<NetUserId> onClosed)
    {
        _user = user;
        _state = state;
        _onSelect = onSelect;
        _onClosed = onClosed;
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public void UpdateState(PvpClassSelectEuiState state)
    {
        _state = state;
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is PvpClassSelectMessage choice)
            _onSelect(_user, choice.ClassId);
    }

    public override void Closed()
    {
        _onClosed(_user);
    }
}
