using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// A numbered row in the left deployment menu. It keeps the regular Button
/// contract so the ready/character/options/leave handlers continue to work,
/// but draws the accent bracket, active fill and underline itself.
/// </summary>
public sealed class LobbyMenuButton : Button
{
    private static readonly Color RedAccent = Color.FromHex("#D13239");
    private static readonly Color Steel = Color.FromHex("#777970");
    private static readonly Color Alert = Color.FromHex("#9E3937");

    private float _hover;
    private float _activeAnim;
    private float _time;
    private float _sweep = -1f;
    private float _press;
    private bool _wasHovered;

    public bool IsActive { get; set; }
    public bool ShowDivider { get; set; }
    public float BaseLeftMargin { get; set; } = 34f;
    public float RailX { get; set; } = 18f;
    public Color AccentColor { get; set; } = RedAccent;

    public LobbyMenuButton()
    {
        StyleBoxOverride = new StyleBoxFlat(Color.Transparent);
        RectClipContent = false;
        OnPressed += _ =>
        {
            _press = 1f;
            LobbyFx.PlayClick();
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _time += args.DeltaSeconds;
        var hovered = !Disabled && IsHovered;
        if (hovered && !_wasHovered)
        {
            LobbyFx.PlayHover();
            if (!LobbyFx.ReducedMotion)
                _sweep = 0f;
        }

        _wasHovered = hovered;
        _hover = LobbyFx.Approach(_hover, hovered ? 1f : 0f, 14f, args.DeltaSeconds);
        _activeAnim = LobbyFx.Approach(_activeAnim, IsActive ? 1f : 0f, 12f, args.DeltaSeconds);
        _press = MathF.Max(0f, _press - args.DeltaSeconds * 4.5f);

        if (_sweep >= 0f)
        {
            _sweep += args.DeltaSeconds / 0.28f;
            if (_sweep >= 1f)
                _sweep = -1f;
        }

        var shift = BaseLeftMargin + 6f * MathF.Max(_hover, _activeAnim);
        foreach (var child in Children)
        {
            var m = child.Margin;
            if (MathF.Abs(m.Left - shift) > 0.1f)
                child.Margin = new Thickness(shift, m.Top, m.Right, m.Bottom);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var pressed = DrawMode == DrawModeEnum.Pressed;
        var midY = size.Y * 0.5f;
        var emphasis = MathF.Max(_hover, _activeAnim);

        // Distinct dark Graphite wash + subtle Amber hairline so the active/hovered
        // row never blends into bright background details.
        if (emphasis > 0.02f)
        {
            const int washSteps = 10;
            var washStart = RailX;
            var washWidth = MathF.Max(0f, size.X - 12f - washStart);
            var graphite = Color.FromHex("#151817");

            for (var i = 0; i < washSteps; i++)
            {
                var t = i / (float) washSteps;
                var x0 = washStart + washWidth * t;
                var x1 = washStart + washWidth * (t + 1f / washSteps) + 0.5f;
                var bgAlpha = (0.82f - 0.65f * t) * emphasis;
                var lineAlpha = (0.45f - 0.35f * t) * emphasis;

                handle.DrawRect(new UIBox2(x0, 2f, x1, size.Y - 2f), graphite.WithAlpha(bgAlpha));
                handle.DrawRect(new UIBox2(x0, 2f, x1, 3f), AccentColor.WithAlpha(lineAlpha));
                handle.DrawRect(new UIBox2(x0, size.Y - 3f, x1, size.Y - 2f), AccentColor.WithAlpha(lineAlpha * 0.65f));
            }
        }

        // Coordinate tick on the vertical spine.
        if (emphasis > 0.02f)
        {
            var tickH = size.Y - 8f;
            handle.DrawRect(
                new UIBox2(RailX - 1f, midY - tickH * 0.5f, RailX + 2f, midY + tickH * 0.5f),
                AccentColor.WithAlpha(0.65f + 0.35f * emphasis));
        }
        else
        {
            handle.DrawRect(
                new UIBox2(RailX - 1f, midY - 1.5f, RailX + 1.5f, midY + 1.5f),
                Steel.WithAlpha(0.45f));
        }

        // Short expanding horizontal line (━━━━━) on active or hovered item.
        if (emphasis > 0.02f)
        {
            var lineMax = 38f;
            var lineLen = lineMax * emphasis;
            var rightX = size.X - 16f;
            var pulse = LobbyFx.ReducedMotion ? 1f : 0.78f + 0.22f * LobbyFx.Pulse(_time, 0.9f);

            handle.DrawRect(
                new UIBox2(rightX - lineLen, midY - 1f, rightX, midY + 1f),
                AccentColor.WithAlpha(0.85f * emphasis * pulse));
        }

        if (pressed && !Disabled)
            handle.DrawRect(new UIBox2(RailX, size.Y - 1f, size.X - 16f, size.Y), Alert.WithAlpha(0.55f));

        // Scan-beam sweep across the row on hover-enter.
        if (_sweep >= 0f && !LobbyFx.ReducedMotion)
        {
            var span = MathF.Max(0f, size.X - 12f - RailX);
            var cx = RailX + span * Easings.OutQuad(_sweep);
            var band = MathF.Min(42f, span * 0.24f);
            var alpha = 0.22f * MathF.Sin(_sweep * MathF.PI);
            handle.DrawRect(
                new UIBox2(MathF.Max(RailX, cx - band * 0.5f), 2f, MathF.Min(size.X - 12f, cx + band * 0.5f), size.Y - 2f),
                AccentColor.WithAlpha(alpha));
        }

        // Crisp click flash along the bottom rail.
        if (_press > 0.01f && !LobbyFx.ReducedMotion)
        {
            handle.DrawRect(
                new UIBox2(RailX, size.Y - 3f, size.X - 16f, size.Y - 1.5f),
                Color.White.WithAlpha(0.55f * _press));
        }

        base.Draw(handle);

        if (Disabled)
            handle.DrawRect(new UIBox2(Vector2.Zero, size), Color.Black.WithAlpha(0.32f));
        else if (HasKeyboardFocus())
            DrawBorder(handle, size, 1f, AccentColor.WithAlpha(0.45f));
    }

    private static void DrawBorder(DrawingHandleScreen handle, Vector2 size, float thickness, Color color)
    {
        handle.DrawRect(new UIBox2(0f, 0f, size.X, thickness), color);
        handle.DrawRect(new UIBox2(0f, size.Y - thickness, size.X, size.Y), color);
        handle.DrawRect(new UIBox2(0f, 0f, thickness, size.Y), color);
        handle.DrawRect(new UIBox2(size.X - thickness, 0f, size.X, size.Y), color);
    }
}
