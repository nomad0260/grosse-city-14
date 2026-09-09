using Content.Client._Grosse.Assault.UI;
using Content.Shared._Grosse.Assault;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Grosse.Assault;

public sealed partial class AssaultClientSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    private AssaultHudControl? _hud;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AssaultHudUpdateEvent>(OnHud);
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
