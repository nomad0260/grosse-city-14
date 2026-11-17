using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared._Grosse.GhostRespawn;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.GhostRespawn;

public sealed partial class GhostRespawnSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public bool Available { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GhostRespawnAvailabilityEvent>(OnAvailability);
    }

    public void UpdateButton()
    {
        var gui = _ui.GetActiveUIWidgetOrNull<GhostGui>();
        if (gui == null)
            return;

        gui.ReturnToRoundButton.Visible = Available;
    }

    private void OnAvailability(GhostRespawnAvailabilityEvent ev)
    {
        Available = ev.Available;
        UpdateButton();
    }
}
