using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.UIpatch;

/// <summary>
/// Compact segmented bar used under the node status line.
/// </summary>
public sealed class LobbySegmentedBar : Control
{
    private float _fill = 0.14f;

    public int Segments { get; set; } = 14;
    public Color ActiveColor { get; set; } = Color.FromHex("#E51E2A");
    public Color InactiveColor { get; set; } = Color.FromHex("#2B2E33");
    public bool ShowArrow { get; set; }
    public Color ArrowColor { get; set; } = Color.FromHex("#63C77B");
    public bool Slanted { get; set; } = true;

    public float Fill
    {
        get => _fill;
        set => _fill = Math.Clamp(value, 0f, 1f);
    }

    public LobbySegmentedBar()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f || Segments <= 0)
            return;

        var startX = ShowArrow ? 12f : 0f;
        if (ShowArrow)
        {
            var mid = size.Y * 0.5f;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[]
                {
                    new Vector2(0f, mid - size.Y * 0.36f),
                    new Vector2(0f, mid + size.Y * 0.36f),
                    new Vector2(7f, mid),
                },
                ArrowColor);
        }

        var gap = 3f;
        var width = (size.X - startX - gap * (Segments - 1)) / Segments;
        if (width <= 0f)
            return;

        var active = (int) MathF.Round(_fill * Segments);
        var slant = Slanted ? size.Y * 0.55f : 0f;
        for (var i = 0; i < Segments; i++)
        {
            var x = startX + i * (width + gap);
            var color = i < active ? ActiveColor : InactiveColor.WithAlpha(0.8f);
            if (i == 0 && active > 0)
                color = ActiveColor;

            if (Slanted)
            {
                var p0 = new Vector2(x, size.Y);
                var p1 = new Vector2(x + slant, 0f);
                var p2 = new Vector2(x + slant + width, 0f);
                var p3 = new Vector2(x + width, size.Y);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                    new[] { p0, p1, p2, p0, p2, p3 },
                    color);
            }
            else
            {
                handle.DrawRect(new UIBox2(x, 0f, x + width, size.Y), color);
            }
        }
    }
}
