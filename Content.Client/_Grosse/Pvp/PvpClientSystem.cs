using Content.Shared._Grosse.Pvp;
using Robust.Shared.Player;

namespace Content.Client._Grosse.Pvp;

public sealed class PvpClientSystem : EntitySystem
{
    public bool LobbyEnabled { get; private set; }
    public bool CanReady { get; private set; }
    public bool InWaveQueue { get; private set; }

    public event Action<PvpLobbyStateEvent>? LobbyStateChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PvpLobbyStateEvent>(OnLobbyState);
    }

    public void SelectLoadout(bool random, PvpTeam? team, string? classId)
    {
        RaiseNetworkEvent(new PvpSelectLoadoutEvent(random, team, classId));
    }

    public void RequestLateJoin()
    {
        RaiseNetworkEvent(new PvpLateJoinRequestEvent());
    }

    private void OnLobbyState(PvpLobbyStateEvent ev)
    {
        LobbyEnabled = ev.Enabled;
        CanReady = ev.CanReady;
        InWaveQueue = ev.InWaveQueue;
        LobbyStateChanged?.Invoke(ev);
    }
}
