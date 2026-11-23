using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Diegetic overlay for the dossier portrait: thin corner brackets and a slow
/// scanning line. It only adorns the portrait card and never handles input.
/// </summary>
public sealed class DossierScanOverlay : Control
{
    private const float ScanPeriod = 6f;
    private const float ScanDuration = 1.8f;

    private float _time;

    public Color AccentColor { get; set; } = Color.FromHex("#D13239");

    public DossierScanOverlay()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X < 12f || size.Y < 12f)
            return;

        var inset = 5f;
        var length = MathF.Min(16f, MathF.Min(size.X, size.Y) * 0.14f);
        var bracket = AccentColor.WithAlpha(0.5f);

        DrawCorner(handle, new Vector2(inset, inset), 1f, 1f, length, bracket);
        DrawCorner(handle, new Vector2(size.X - inset, inset), -1f, 1f, length, bracket);
        DrawCorner(handle, new Vector2(inset, size.Y - inset), 1f, -1f, length, bracket);
        DrawCorner(handle, new Vector2(size.X - inset, size.Y - inset), -1f, -1f, length, bracket);

        if (LobbyFx.ReducedMotion)
            return;

        var cycle = _time % ScanPeriod;
        if (cycle > ScanDuration)
            return;

        var progress = cycle / ScanDuration;
        var strength = MathF.Sin(progress * MathF.PI);
        var y = 5f + (size.Y - 10f) * progress;

        const int trailSteps = 10;
        const float trailHeight = 28f;
        var step = trailHeight / trailSteps;

        for (var i = 0; i < trailSteps; i++)
        {
            var local = (i + 0.5f) / trailSteps;
            var alpha = 0.09f * (1f - local) * strength;
            if (alpha <= 0.001f)
                continue;

            var y0 = y - local * trailHeight;
            handle.DrawRect(new UIBox2(3f, y0, size.X - 3f, y0 + step + 0.5f),
                AccentColor.WithAlpha(alpha));
        }

        handle.DrawRect(new UIBox2(3f, y, size.X - 3f, y + 1f),
            AccentColor.WithAlpha(0.32f * strength));
    }

    private static void DrawCorner(DrawingHandleScreen handle, Vector2 origin, float directionX, float directionY,
        float length, Color color)
    {
        handle.DrawLine(origin, origin + new Vector2(directionX * length, 0f), color);
        handle.DrawLine(origin, origin + new Vector2(0f, directionY * length), color);
    }
}
