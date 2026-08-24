using Content.Client.Eui;
using Content.Shared._Grosse.Assault.UI;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Grosse.Assault.UI;

[UsedImplicitly]
public sealed class AssaultClassSelectEui : BaseEui
{
    private readonly AssaultClassSelectMenu _menu = new();

    public AssaultClassSelectEui()
    {
        _menu.ClassPicked += id => SendMessage(new AssaultClassSelectMessage(id));
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
        if (state is AssaultClassSelectEuiState assault)
            _menu.Update(assault);
    }
}
