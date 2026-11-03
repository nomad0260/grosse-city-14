using Content.Client.Eui;
using Content.Shared._Grosse.GhostRespawn;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Grosse.GhostRespawn;

[UsedImplicitly]
public sealed class GhostRespawnEui : BaseEui
{
    private readonly GhostRespawnWindow _window = new();

    public GhostRespawnEui()
    {
        _window.OnConfirm += (station, jobId, slot) =>
            SendMessage(new GhostRespawnSelectMessage(station, jobId, slot));
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is GhostRespawnEuiState respawn)
            _window.UpdateState(respawn);
    }
}
