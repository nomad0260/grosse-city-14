using Content.Server.EUI;
using Content.Shared._Grosse.GhostRespawn;
using Content.Shared.Eui;

namespace Content.Server._Grosse.GhostRespawn;

public sealed class GhostRespawnEui : BaseEui
{
    private readonly GhostRespawnSystem _system;

    public GhostRespawnEui(GhostRespawnSystem system)
    {
        _system = system;
    }

    public override EuiStateBase GetNewState()
    {
        return _system.BuildState(Player);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is GhostRespawnSelectMessage choice)
            _system.OnRespawnSelected(this, choice);
    }

    public override void Closed()
    {
        base.Closed();
        _system.OnEuiClosed(Player);
    }
}
