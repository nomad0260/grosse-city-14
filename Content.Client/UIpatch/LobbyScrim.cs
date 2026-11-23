using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Cinematic scrim for the lobby background. A flat dark overlay hides the artwork;
/// this keeps the centre readable but lets the background breathe through a
/// directional gradient, then frames it with a soft top and bottom vignette.
/// On top of that it adds slow atmosphere: a passing light sweep, fine scanlines
/// and a few drifting dust motes.
/// </summary>
public sealed class LobbyScrim : Control
{
    private const int HorizontalSteps = 80;
    private const int BottomSteps = 28;
    private const int TopSteps = 18;

    private const float LeftAlpha = 0.62f;
    private const float LeftMidAlpha = 0.24f;
    private const float CenterAlpha = 0.06f;
    private const float RightAlpha = 0.34f;

    private static readonly Color SweepColor = Color.FromHex("#C8D6E5");
    private static readonly Color DustColor = Color.FromHex("#D6DEE8");
    private static readonly Color ScanlineColor = Color.Black.WithAlpha(0.03f);

    private float _time;

    public LobbyScrim()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var breathe = LobbyFx.ReducedMotion
            ? 1f
            : 1f + 0.05f * MathF.Sin(_time * 0.7f);

        DrawHorizontalGradient(handle, size);
        DrawBottomVignette(handle, size, breathe);
        DrawTopVignette(handle, size, breathe);
        DrawLightSweep(handle, size);
        DrawScanlines(handle, size);
        DrawDust(handle, size);
    }

    private static void DrawHorizontalGradient(DrawingHandleScreen handle, Vector2 size)
    {
        var step = size.X / HorizontalSteps;

        for (var i = 0; i < HorizontalSteps; i++)
        {
            var x0 = i * step;
            var x1 = i == HorizontalSteps - 1 ? size.X : (i + 1) * step;
            var alpha = HorizontalAlpha((i + 0.5f) / HorizontalSteps);

            handle.DrawRect(new UIBox2(x0, 0, x1, size.Y), Color.Black.WithAlpha(alpha));
        }
    }

    private static void DrawBottomVignette(DrawingHandleScreen handle, Vector2 size, float scale)
    {
        var height = MathF.Min(size.Y * 0.34f, 260f);
        var step = height / BottomSteps;

        for (var i = 0; i < BottomSteps; i++)
        {
            var t = (i + 0.5f) / BottomSteps;
            var y0 = size.Y - height + i * step;
            var y1 = i == BottomSteps - 1 ? size.Y : y0 + step;
            var alpha = MathF.Min(0.42f * Smooth(t) * scale, 0.6f);

            handle.DrawRect(new UIBox2(0, y0, size.X, y1), Color.Black.WithAlpha(alpha));
        }
    }

    private static void DrawTopVignette(DrawingHandleScreen handle, Vector2 size, float scale)
    {
        var height = MathF.Min(size.Y * 0.14f, 120f);
        var step = height / TopSteps;

        for (var i = 0; i < TopSteps; i++)
        {
            var t = (i + 0.5f) / TopSteps;
            var y0 = i * step;
            var y1 = i == TopSteps - 1 ? height : y0 + step;
            var alpha = MathF.Min(0.20f * (1f - Smooth(t)) * scale, 0.3f);

            handle.DrawRect(new UIBox2(0, y0, size.X, y1), Color.Black.WithAlpha(alpha));
        }
    }

    /// <summary>
    /// A soft diagonal-ish band of light that crosses the scene every ~21 seconds.
    /// </summary>
    private void DrawLightSweep(DrawingHandleScreen handle, Vector2 size)
    {
        if (LobbyFx.ReducedMotion)
            return;

        const float period = 21f;
        const float duration = 3.4f;

        var cycle = _time % period;
        if (cycle > duration)
            return;

        var t = cycle / duration;
        var width = size.X * 0.26f;
        var left = -width + (size.X + width * 2f) * t;

        const int steps = 24;
        var step = width / steps;

        for (var i = 0; i < steps; i++)
        {
            var local = (i + 0.5f) / steps;
            var alpha = 0.055f * MathF.Sin(local * MathF.PI) * MathF.Sin(t * MathF.PI);
            if (alpha <= 0.001f)
                continue;

            var x0 = left + i * step;
            handle.DrawRect(new UIBox2(x0, 0, x0 + step + 0.5f, size.Y),
                SweepColor.WithAlpha(alpha));
        }
    }

    private static void DrawScanlines(DrawingHandleScreen handle, Vector2 size)
    {
        const float spacing = 4f;

        for (var y = 1f; y < size.Y; y += spacing)
            handle.DrawRect(new UIBox2(0, y, size.X, y + 1f), ScanlineColor);
    }

    private void DrawDust(DrawingHandleScreen handle, Vector2 size)
    {
        if (LobbyFx.ReducedMotion)
            return;

        const int count = 18;

        for (var i = 0; i < count; i++)
        {
            var seed = i * 0.6180339f;
            var speed = 0.006f + i % 5 * 0.0018f;
            var x = PositiveFraction(seed + _time * speed * 0.35f) * size.X;
            var y = PositiveFraction(seed * 1.7f + _time * speed) * size.Y;
            var alpha = 0.025f + 0.02f * LobbyFx.Pulse(_time + i * 0.7f, 0.09f);

            handle.DrawRect(new UIBox2(x, y, x + 1.5f, y + 1.5f), DustColor.WithAlpha(alpha));
        }
    }

    private static float PositiveFraction(float value)
    {
        return value - MathF.Floor(value);
    }

    private static float HorizontalAlpha(float x)
    {
        if (x < 0.4f)
            return Lerp(LeftAlpha, LeftMidAlpha, Smooth(x / 0.4f));

        if (x < 0.7f)
            return Lerp(LeftMidAlpha, CenterAlpha, Smooth((x - 0.4f) / 0.3f));

        return Lerp(CenterAlpha, RightAlpha, Smooth((x - 0.7f) / 0.3f));
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * t;
    }
}
