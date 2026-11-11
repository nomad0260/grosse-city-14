using Content.Client.Pinpointer.UI;
using Robust.Client.Graphics;

namespace Content.Client._Grosse.CommanderTerminal;

public sealed class CommanderTerminalNavMapControl : NavMapControl
{
    public CommanderTerminalNavMapControl()
    {
        WallColor = new Color(90, 170, 220);
        TileColor = new Color(18, 42, 72);
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));
    }
}
