using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Верхняя статусная панель в стиле Combine.
/// </summary>
public sealed class CombineTopBar : PanelContainer
{
    private static readonly Color BgTop = Color.FromHex("#12161A").WithAlpha(0.95f);
    private static readonly Color BgBottom = Color.FromHex("#07090B").WithAlpha(0.95f);
    private static readonly Color BorderColor = Color.FromHex("#34383D").WithAlpha(0.5f);
    private static readonly Color AccentRed = Color.FromHex("#E51E2A");

    private float _time;

    public CombineTopBar()
    {
        // Тема не должна перекрывать собственную отрисовку верхней панели.
        PanelOverride = new StyleBoxFlat(Color.Transparent);
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
        {
            base.Draw(handle);
            return;
        }

        DrawBackground(handle, size);

        // Тонкая нижняя граница
        handle.DrawRect(new UIBox2(0, size.Y - 1, size.X, size.Y), BorderColor);

        // Внутренний блик по верхней кромке
        handle.DrawRect(new UIBox2(0, 0, size.X, 1f), Color.White.WithAlpha(0.04f));

        DrawAccentScan(handle, size);

        base.Draw(handle);
    }

    private static void DrawBackground(DrawingHandleScreen handle, Vector2 size)
    {
        const int steps = 14;
        var stepHeight = size.Y / steps;

        for (var i = 0; i < steps; i++)
        {
            var t = (i + 0.5f) / steps;
            var color = Color.InterpolateBetween(BgTop, BgBottom, t);
            handle.DrawRect(new UIBox2(0, i * stepHeight, size.X, (i + 1) * stepHeight + 0.5f), color);
        }
    }

    /// <summary>
    /// A short red segment that patrols the bottom edge, like a status light
    /// running across a console. Disabled for reduced motion.
    /// </summary>
    private void DrawAccentScan(DrawingHandleScreen handle, Vector2 size)
    {
        if (LobbyFx.ReducedMotion)
        {
            handle.DrawRect(new UIBox2(18f, size.Y - 2f, 18f + MathF.Min(size.X * 0.08f, 90f), size.Y),
                AccentRed.WithAlpha(0.45f));
            return;
        }

        const float period = 19f;
        const float duration = 5f;
        var cycle = _time % period;

        if (cycle > duration)
            return;

        var t = cycle / duration;
        var width = MathF.Min(size.X * 0.18f, 220f);
        var left = -width + (size.X + width) * t;

        handle.DrawRect(new UIBox2(left, size.Y - 2f, left + width, size.Y),
            AccentRed.WithAlpha(0.55f * MathF.Sin(t * MathF.PI)));
    }
}
