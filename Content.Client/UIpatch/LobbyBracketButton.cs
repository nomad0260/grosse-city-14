using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Primary deployment action: a dark red plate with a pointed tail, a bright
/// frame and a heavy marker on the leading edge. It keeps the normal Button
/// contract used by the lobby state, including ToggleMode for the ready state.
/// </summary>
public sealed class LobbyBracketButton : Button
{
    private static readonly Color DisabledColor = Color.FromHex("#50534D");
    private static readonly Color BaseFill = Color.FromHex("#151817");
    private static readonly Color Steel = Color.FromHex("#777970");

    /// <summary>Accent colour while hovered or active.</summary>
    public Color AccentColor { get; set; } = Color.FromHex("#D13239");

    /// <summary>Accent colour while hovered, pressed or ready (kept for LobbyTutorialWindow compatibility).</summary>
    public Color HoverColor { get; set; } = Color.FromHex("#E53940");

    /// <summary>Accent colour when queued/ready (Alert).</summary>
    public Color ReadyColor { get; set; } = Color.FromHex("#9E3937");

    private float _hover;
    private float _time;
    private float _sweep = -1f;
    private float _press;
    private bool _wasHovered;

    public LobbyBracketButton()
    {
        StyleBoxOverride = new StyleBoxFlat(Color.Transparent);
        RectClipContent = false;
        OnPressed += _ =>
        {
            _press = 1f;
            LobbyFx.PlayDeploy();
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
        _hover = LobbyFx.Approach(_hover, hovered ? 1f : 0f, 12f, args.DeltaSeconds);
        _press = MathF.Max(0f, _press - args.DeltaSeconds * 4.5f);

        if (_sweep >= 0f)
        {
            _sweep += args.DeltaSeconds / 0.32f;
            if (_sweep >= 1f)
                _sweep = -1f;
        }

        var shift = 5f * _hover;
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

        var ready = ToggleMode && Pressed && !Disabled;
        var accent = Disabled
            ? DisabledColor
            : ready
                ? ReadyColor
                : AccentColor;

        // Subtle Graphite bed only — no heavy red plate.
        var fillAlpha = Disabled ? 0.15f : ready ? 0.42f : 0.24f + 0.12f * _hover;
        handle.DrawRect(new UIBox2(0f, 0f, size.X, size.Y), BaseFill.WithAlpha(fillAlpha));

        // Thin structural top line in Steel, brightening on hover/ready.
        handle.DrawRect(new UIBox2(0f, 0f, size.X, 1f),
            Color.InterpolateBetween(Steel.WithAlpha(0.32f), accent.WithAlpha(0.75f), ready ? 1f : _hover));

        // Left coordinate anchor tick.
        handle.DrawRect(new UIBox2(0f, 0f, 2f, size.Y),
            ready ? ReadyColor.WithAlpha(0.9f) : Color.InterpolateBetween(Steel.WithAlpha(0.55f), accent, _hover));

        // Expanding bottom line with light pulse on hover or ready.
        var lineFactor = ready ? 1f : 0.30f + 0.70f * _hover;
        var pulse = LobbyFx.ReducedMotion ? 1f : 0.78f + 0.22f * LobbyFx.Pulse(_time, 1.1f);
        var lineWidth = size.X * lineFactor;
        handle.DrawRect(new UIBox2(0f, size.Y - 1.5f, lineWidth, size.Y),
            accent.WithAlpha((ready ? 0.92f : 0.45f + 0.50f * _hover) * pulse));

        // Moving pulse highlight along the expanding line when hovered.
        if (!Disabled && (_hover > 0.05f || ready) && !LobbyFx.ReducedMotion)
        {
            var travel = (_time * 0.65f) % 1f;
            var px = lineWidth * travel;
            var pw = MathF.Min(28f, lineWidth * 0.25f);
            handle.DrawRect(new UIBox2(MathF.Max(0f, px - pw * 0.5f), size.Y - 2f, MathF.Min(lineWidth, px + pw * 0.5f), size.Y),
                Color.White.WithAlpha(0.45f * MathF.Max(_hover, ready ? 0.6f : 0f)));
        }

        // Scan-beam sweep across the plaque on hover-enter.
        if (_sweep >= 0f && !LobbyFx.ReducedMotion)
        {
            var cx = size.X * Easings.OutQuad(_sweep);
            var band = MathF.Min(48f, size.X * 0.22f);
            var alpha = 0.20f * MathF.Sin(_sweep * MathF.PI);
            handle.DrawRect(
                new UIBox2(MathF.Max(0f, cx - band * 0.5f), 0f, MathF.Min(size.X, cx + band * 0.5f), size.Y),
                accent.WithAlpha(alpha));
        }

        // Crisp click flash on activation.
        if (_press > 0.01f && !LobbyFx.ReducedMotion)
        {
            handle.DrawRect(new UIBox2(0f, 0f, size.X, size.Y), accent.WithAlpha(0.22f * _press));
            handle.DrawRect(new UIBox2(0f, size.Y - 2f, size.X, size.Y), Color.White.WithAlpha(0.65f * _press));
        }

        base.Draw(handle);

        if (HasKeyboardFocus())
            handle.DrawRect(new UIBox2(0f, 0f, size.X, 1f), accent.WithAlpha(0.8f));

        if (Disabled)
            handle.DrawRect(new UIBox2(Vector2.Zero, size), Color.Black.WithAlpha(0.25f));
    }
}
