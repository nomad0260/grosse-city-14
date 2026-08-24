using Content.Client._Grosse.Assault.UI;
using Content.Shared._Grosse.Assault;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Grosse.Assault;

public sealed partial class AssaultClientSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public bool LobbyEnabled { get; private set; }
    public bool CanReady { get; private set; }
    public bool InWaveQueue { get; private set; }

    public event Action<AssaultLobbyStateEvent>? LobbyStateChanged;

    private AssaultHudControl? _hud;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AssaultLobbyStateEvent>(OnLobbyState);
        SubscribeNetworkEvent<AssaultHudUpdateEvent>(OnHud);
    }

    public void SelectLoadout(bool random, AssaultTeam? team, string? classId)
    {
        RaiseNetworkEvent(new AssaultSelectLoadoutEvent(random, team, classId));
    }

    public void RequestLateJoin()
    {
        RaiseNetworkEvent(new AssaultLateJoinRequestEvent());
    }

    private void OnLobbyState(AssaultLobbyStateEvent ev)
    {
        LobbyEnabled = ev.Enabled;
        CanReady = ev.CanReady;
        InWaveQueue = ev.InWaveQueue;
        LobbyStateChanged?.Invoke(ev);
    }

    private void OnHud(AssaultHudUpdateEvent ev)
    {
        EnsureHud();
        _hud!.Update(ev);
        if (!ev.Enabled)
            _hud.Visible = false;
    }

    private void EnsureHud()
    {
        if (_hud != null)
            return;

        _hud = new AssaultHudControl();
        LayoutContainer.SetAnchorPreset(_hud, LayoutContainer.LayoutPreset.TopWide);
        LayoutContainer.SetGrowHorizontal(_hud, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetMarginTop(_hud, 8);
        _ui.WindowRoot.AddChild(_hud);
    }
}
