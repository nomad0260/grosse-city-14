using Content.Client.Eui;
using Content.Shared._Grosse.Pvp.UI;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Grosse.Pvp.UI;

[UsedImplicitly]
public sealed class PvpClassSelectEui : BaseEui
{
    private readonly PvpClassSelectMenu _menu = new();

    public PvpClassSelectEui()
    {
        _menu.ClassPicked += id => SendMessage(new PvpClassSelectMessage(id));
        _menu.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _menu.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _menu.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is PvpClassSelectEuiState pvp)
            _menu.Update(pvp);
    }
}
