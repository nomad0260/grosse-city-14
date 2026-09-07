using Content.Client._Grosse.Control.UI;
using Content.Shared._Grosse.Control;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Grosse.Control;

public sealed partial class ControlClientSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    private ControlHudControl? _hud;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ControlHudUpdateEvent>(OnHud);
    }

    private void OnHud(ControlHudUpdateEvent ev)
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

        _hud = new ControlHudControl();
        LayoutContainer.SetAnchorPreset(_hud, LayoutContainer.LayoutPreset.TopWide);
        LayoutContainer.SetGrowHorizontal(_hud, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetMarginTop(_hud, 8);
        _ui.WindowRoot.AddChild(_hud);
    }
}
