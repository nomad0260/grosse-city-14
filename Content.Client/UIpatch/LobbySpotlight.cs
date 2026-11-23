using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.UIpatch;

/// <summary>
/// Soft overhead spotlight and targeting brackets for the personnel dossier.
/// Drawn with primitives so it stays crisp at any resolution.
/// </summary>
public sealed class LobbySpotlight : Control
{
    private static readonly Color BeamColor = Color.FromHex("#D7E6F4");
    private static readonly Color BracketColor = Color.FromHex("#7E858D").WithAlpha(0.75f);

    public LobbySpotlight()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f)
            return;

        DrawCone(handle, size);
        DrawFloorGlow(handle, size);
        DrawBrackets(handle, size);
    }

    private static void DrawCone(DrawingHandleScreen handle, Vector2 size)
    {
        const int bands = 18;
        var centerX = size.X * 0.5f;
        var maxHalf = size.X * 0.42f;

        for (var i = 0; i < bands; i++)
        {
            var t0 = i / (float) bands;
            var t1 = (i + 1) / (float) bands;
            var y0 = size.Y * 0.04f + (size.Y * 0.88f) * t0;
            var y1 = size.Y * 0.04f + (size.Y * 0.88f) * t1;
            var half0 = MathF.Max(2f, maxHalf * (0.08f + 0.92f * t0));
            var half1 = MathF.Max(2f, maxHalf * (0.08f + 0.92f * t1));
            var alpha = 0.045f * (1f - t0 * 0.7f);

            var points = new[]
            {
                new Vector2(centerX - half0, y0),
                new Vector2(centerX + half0, y0),
                new Vector2(centerX + half1, y1),
                new Vector2(centerX - half0, y0),
                new Vector2(centerX + half1, y1),
                new Vector2(centerX - half1, y1),
            };

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, points, BeamColor.WithAlpha(alpha));
        }
    }

    private static void DrawFloorGlow(DrawingHandleScreen handle, Vector2 size)
    {
        var center = new Vector2(size.X * 0.5f, size.Y * 0.82f);
        var radiusX = size.X * 0.30f;
        var radiusY = size.Y * 0.035f;
        const int segments = 28;

        var points = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            points[i] = center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
        }

        for (var i = 0; i < segments; i++)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { center, points[i], points[(i + 1) % segments] },
                BeamColor.WithAlpha(0.055f));
        }

        // A second, tighter pool under the feet.
        var inner = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            inner[i] = center + new Vector2(MathF.Cos(angle) * radiusX * 0.55f, MathF.Sin(angle) * radiusY * 0.55f);
        }

        for (var i = 0; i < segments; i++)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { center, inner[i], inner[(i + 1) % segments] },
                BeamColor.WithAlpha(0.045f));
        }
    }

    private static void DrawBrackets(DrawingHandleScreen handle, Vector2 size)
    {
        const float inset = 2f;
        const float length = 24f;

        // Top left
        handle.DrawLine(new Vector2(inset, inset), new Vector2(inset + length, inset), BracketColor);
        handle.DrawLine(new Vector2(inset, inset), new Vector2(inset, inset + length), BracketColor);
        // Top right
        handle.DrawLine(new Vector2(size.X - inset, inset), new Vector2(size.X - inset - length, inset), BracketColor);
        handle.DrawLine(new Vector2(size.X - inset, inset), new Vector2(size.X - inset, inset + length), BracketColor);
        // Bottom left
        handle.DrawLine(new Vector2(inset, size.Y - inset), new Vector2(inset + length, size.Y - inset), BracketColor);
        handle.DrawLine(new Vector2(inset, size.Y - inset), new Vector2(inset, size.Y - inset - length), BracketColor);
        // Bottom right
        handle.DrawLine(new Vector2(size.X - inset, size.Y - inset), new Vector2(size.X - inset - length, size.Y - inset), BracketColor);
        handle.DrawLine(new Vector2(size.X - inset, size.Y - inset), new Vector2(size.X - inset, size.Y - inset - length), BracketColor);
    }
}
