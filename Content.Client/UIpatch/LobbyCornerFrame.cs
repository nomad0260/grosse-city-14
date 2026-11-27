using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Frames its single child with two red corner brackets, the way the personnel
/// dossier is framed in the reference. The child is laid out over the full
/// control rectangle.
/// </summary>
public sealed class LobbyCornerFrame : Container
{
    private float _time;

    public Color CornerColor { get; set; } = Color.FromHex("#777970").WithAlpha(0.75f);
    public Color AccentColor { get; set; } = Color.FromHex("#D13239").WithAlpha(0.88f);
    public Color FillColor { get; set; } = Color.FromHex("#151817").WithAlpha(0.20f);

    public float Inset { get; set; } = 1f;
    public float Arm { get; set; } = 18f;
    public float Thickness { get; set; } = 1f;

    public LobbyCornerFrame()
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
        if (size.X <= 8f || size.Y <= 8f)
            return;

        // Ultra-light Graphite tint only; no full border box.
        if (FillColor.A > 0.001f)
            handle.DrawRect(new UIBox2(Inset, Inset, size.X - Inset, size.Y - Inset), FillColor);

        var arm = MathF.Min(Arm, MathF.Min(size.X, size.Y) * 0.35f);
        var pulse = LobbyFx.ReducedMotion ? 1f : 0.78f + 0.22f * LobbyFx.Pulse(_time, 0.55f);
        var accent = AccentColor.WithAlpha(AccentColor.A * pulse);

        // Top-left corner segment (Red accent).
        handle.DrawRect(new UIBox2(Inset, Inset, Inset + arm, Inset + Thickness), accent);
        handle.DrawRect(new UIBox2(Inset, Inset, Inset + Thickness, Inset + arm), accent);

        // Top-right corner segment (Steel).
        handle.DrawRect(new UIBox2(size.X - Inset - arm * 0.7f, Inset, size.X - Inset, Inset + Thickness), CornerColor);
        handle.DrawRect(new UIBox2(size.X - Inset - Thickness, Inset, size.X - Inset, Inset + arm * 0.7f), CornerColor);

        // Bottom-left corner segment (Steel).
        handle.DrawRect(new UIBox2(Inset, size.Y - Inset - Thickness, Inset + arm * 0.7f, size.Y - Inset), CornerColor);
        handle.DrawRect(new UIBox2(Inset, size.Y - Inset - arm * 0.7f, Inset + Thickness, size.Y - Inset), CornerColor);

        // Bottom-right corner segment (Red accent).
        handle.DrawRect(new UIBox2(size.X - Inset - arm, size.Y - Inset - Thickness, size.X - Inset, size.Y - Inset), accent);
        handle.DrawRect(new UIBox2(size.X - Inset - Thickness, size.Y - Inset - arm, size.X - Inset, size.Y - Inset), accent);

        base.Draw(handle);
    }
}
