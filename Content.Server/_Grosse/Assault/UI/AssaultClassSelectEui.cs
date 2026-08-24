using Content.Server.EUI;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Assault.UI;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Assault.UI;

public sealed class AssaultClassSelectEui : BaseEui
{
    private readonly AssaultRuleSystem _rule;
    private readonly NetUserId _user;
    private AssaultClassSelectEuiState _state;

    public AssaultClassSelectEui(AssaultRuleSystem rule, NetUserId user, AssaultClassSelectEuiState state)
    {
        _rule = rule;
        _user = user;
        _state = state;
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public void UpdateState(AssaultClassSelectEuiState state)
    {
        _state = state;
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is AssaultClassSelectMessage choice)
            _rule.TrySelectClass(_user, choice.ClassId);
    }

    public override void Closed()
    {
        _rule.OnClassSelectClosed(_user);
    }
}
