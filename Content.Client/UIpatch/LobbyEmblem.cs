using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.UIpatch;

/// <summary>
/// Faction mark shown next to the lobby title. Drawn with primitives so it
/// scales crisply and needs no texture asset.
/// </summary>
public sealed class LobbyEmblem : Control
{
    public Color EmblemColor { get; set; } = Color.FromHex("#C6CBD1").WithAlpha(0.55f);

    public LobbyEmblem()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(64f, 64f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var pixel = PixelSize;
        var size = MathF.Min(pixel.X, pixel.Y);
        if (size <= 10f)
            return;

        var center = new Vector2(pixel.X * 0.5f, pixel.Y * 0.5f);
        var radius = size * 0.42f;
        var stroke = MathF.Max(1.2f, size * 0.035f);

        Ring(handle, center, radius, stroke, EmblemColor);
        Ring(handle, center, radius * 0.58f, stroke * 0.8f, EmblemColor.WithAlpha(0.55f));

        for (var i = 0; i < 4; i++)
        {
            var angle = MathF.PI * 0.25f + MathF.PI * 0.5f * i;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tip = center + direction * radius * 0.92f;
            var root = center + direction * radius * 0.22f;
            var side = new Vector2(-direction.Y, direction.X) * (radius * 0.17f);

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { tip, root + side, root - side },
                EmblemColor.WithAlpha(0.8f));
        }

        // Vertical spine through the ring.
        handle.DrawRect(new UIBox2(
                center.X - stroke * 0.5f,
                center.Y - radius * 1.12f,
                center.X + stroke * 0.5f,
                center.Y + radius * 1.12f),
            EmblemColor.WithAlpha(0.8f));
    }

    private static void Ring(DrawingHandleScreen handle, Vector2 center, float radius, float width, Color color)
    {
        const int segments = 40;
        var inner = radius - width * 0.5f;
        var outer = radius + width * 0.5f;

        for (var i = 0; i < segments; i++)
        {
            var a0 = MathF.Tau * i / segments;
            var a1 = MathF.Tau * (i + 1) / segments;

            var dir0 = new Vector2(MathF.Cos(a0), MathF.Sin(a0));
            var dir1 = new Vector2(MathF.Cos(a1), MathF.Sin(a1));

            var i0 = center + dir0 * inner;
            var i1 = center + dir1 * inner;
            var o0 = center + dir0 * outer;
            var o1 = center + dir1 * outer;

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { i0, o0, o1, i0, o1, i1 },
                color);
        }
    }
}
