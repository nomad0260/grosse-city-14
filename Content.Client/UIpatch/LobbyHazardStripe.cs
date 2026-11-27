using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.UIpatch;

/// <summary>
/// Short diagonal hazard stripe used next to the deployment heading and in
/// the bottom status line.
/// </summary>
public sealed class LobbyHazardStripe : Control
{
    public Color StripeColor { get; set; } = Color.FromHex("#E51E2A");
    public float StripeWidth { get; set; } = 6f;
    public float Gap { get; set; } = 4f;

    public LobbyHazardStripe()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var height = size.Y;
        var step = MathF.Max(1f, StripeWidth + Gap);

        for (var x = 0f; x < size.X; x += step)
        {
            var topLeft = MathF.Min(x + height, size.X);
            var topRight = MathF.Min(x + height + StripeWidth, size.X);
            var points = new[]
            {
                new Vector2(x, height),
                new Vector2(topLeft, 0f),
                new Vector2(topRight, 0f),
                new Vector2(MathF.Min(x + StripeWidth, size.X), height),
            };

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { points[0], points[1], points[2], points[0], points[2], points[3] },
                StripeColor);
        }
    }
}
