using Robust.Shared.Maths;

namespace Content.Shared._Grosse.CommanderTerminal;

public static class CommanderGroupColors
{
    public static readonly Color[] Palette =
    [
        new(60, 140, 255),
        new(255, 170, 50),
        new(70, 220, 130),
        new(220, 80, 220),
        new(255, 90, 90),
        new(80, 220, 220),
        new(230, 230, 80),
        new(170, 170, 170),
    ];

    public static readonly Color Unassigned = new(150, 150, 150);

    public static Color ForIndex(int index)
    {
        return Palette[Math.Abs(index) % Palette.Length];
    }
}
