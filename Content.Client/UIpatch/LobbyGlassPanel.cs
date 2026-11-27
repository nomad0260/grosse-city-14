using System;
using System.Numerics;
using Content.Client.Lobby.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Translucent panel that shows a blurred copy of the animated lobby backdrop
/// underneath its own screen rectangle. The blur is pre-baked at load time, so
/// the client does not pay for a full-screen blur every frame.
/// </summary>
public sealed class LobbyGlassPanel : Container
{
    private static readonly Color DefaultTint = Color.FromHex("#080A0C").WithAlpha(0.58f);
    private static readonly Color DefaultEdge = Color.FromHex("#3A3E44").WithAlpha(0.35f);

    public LobbyBackdrop? Backdrop { get; set; }

    public Color Tint { get; set; } = DefaultTint;
    public Color EdgeColor { get; set; } = DefaultEdge;
    public bool ShowLeftRail { get; set; }
    public bool ShowChatFrame { get; set; }
    public bool ChatFocused { get; set; }

    private float _time;

    public LobbyGlassPanel()
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

        Backdrop ??= ResolveBackdrop();

        if (Backdrop != null)
            DrawBackdrop(handle, size);

        DrawTint(handle, size);

        if (ShowLeftRail)
            DrawRail(handle, size);

        if (ShowChatFrame)
            DrawChatFrame(handle, size);

        base.Draw(handle);
    }

    private void DrawChatFrame(DrawingHandleScreen handle, Vector2 size)
    {
        var steel = Color.FromHex("#777970");
        var red = Color.FromHex("#D13239");
        var border = ChatFocused ? red.WithAlpha(0.85f) : steel.WithAlpha(0.55f);

        const float chamferTL = 22f;
        const float chamferBL = 8f;
        const float chamferBR = 12f;

        var poly = new[]
        {
            new Vector2(chamferTL, 0.5f),
            new Vector2(size.X - 0.5f, 0.5f),
            new Vector2(size.X - 0.5f, size.Y - chamferBR),
            new Vector2(size.X - chamferBR, size.Y - 0.5f),
            new Vector2(chamferBL, size.Y - 0.5f),
            new Vector2(0.5f, size.Y - chamferBL),
            new Vector2(0.5f, chamferTL),
        };

        // Chamfered dark Graphite bed.
        for (var i = 1; i < poly.Length - 1; i++)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { poly[0], poly[i], poly[i + 1] },
                Color.FromHex("#0D100F").WithAlpha(ChatFocused ? 0.68f : 0.52f));
        }

        // Sleek top terminal strip inside the chamfered frame.
        var headerPoly = new[]
        {
            new Vector2(chamferTL, 0.5f),
            new Vector2(size.X - 0.5f, 0.5f),
            new Vector2(size.X - 0.5f, 16f),
            new Vector2(6.5f, 16f),
        };
        for (var i = 1; i < headerPoly.Length - 1; i++)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { headerPoly[0], headerPoly[i], headerPoly[i + 1] },
                Color.FromHex("#151817").WithAlpha(0.75f));
        }
        handle.DrawRect(new UIBox2(6.5f, 16f, size.X - 0.5f, 17f), steel.WithAlpha(0.22f));

        // Pulsing red status dot on the top terminal strip.
        var pulse = LobbyFx.ReducedMotion ? 1f : 0.60f + 0.40f * LobbyFx.Pulse(_time, 0.85f);
        handle.DrawRect(new UIBox2(chamferTL + 4f, 6f, chamferTL + 9f, 11f), red.WithAlpha(pulse));

        // Crisp 1px chamfered wireframe border.
        for (var i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Length];
            var dir = b - a;
            if (dir.LengthSquared() <= 0.0001f)
                continue;

            var normal = Vector2.Normalize(new Vector2(-dir.Y, dir.X)) * 0.55f;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { a + normal, b + normal, b - normal, a + normal, b - normal, a - normal },
                border);
        }

        // Subtle inner top red coordinate accent.
        handle.DrawRect(new UIBox2(chamferTL, 0.5f, MathF.Min(size.X - 10f, chamferTL + 48f), 2f),
            red.WithAlpha(ChatFocused ? 0.95f : 0.72f));

        // Red vertical wedge marker on the left edge, matching the reference.
        var tabTop = size.Y * 0.40f;
        var tabBottom = size.Y * 0.65f;
        var tab = new[]
        {
            new Vector2(0f, tabTop),
            new Vector2(3.5f, tabTop + 4f),
            new Vector2(3.5f, tabBottom - 4f),
            new Vector2(0f, tabBottom),
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
            new[] { tab[0], tab[1], tab[2], tab[0], tab[2], tab[3] },
            red);

        // Bottom-right red accent underline segment.
        handle.DrawRect(
            new UIBox2(MathF.Max(chamferBL, size.X - 96f), size.Y - 1.5f, size.X - chamferBR, size.Y),
            red.WithAlpha(ChatFocused ? 0.95f : 0.72f));
    }

    /// <summary>
    /// Thin vertical coordinate rail for the left navigation column.
    /// </summary>
    private static void DrawRail(DrawingHandleScreen handle, Vector2 size)
    {
        var top = 0f;
        var bottom = MathF.Max(top, size.Y - 8f);
        var span = bottom - top;
        if (span <= 0f)
            return;

        var steel = Color.FromHex("#777970");
        var accent = Color.FromHex("#D13239");

        // Thin 1px coordinate spine fading gently towards the bottom.
        const int steps = 24;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float) steps;
            var alpha = 0.36f * (1f - t * 0.55f);
            handle.DrawRect(
                new UIBox2(40f, top + span * t, 41f, top + span * (t + 1f / steps) + 0.5f),
                steel.WithAlpha(alpha));
        }

        // Subtle red top coordinate anchor.
        handle.DrawRect(new UIBox2(40f, 0f, 41f, MathF.Min(26f, span)), accent.WithAlpha(0.80f));
    }

    private LobbyBackdrop? ResolveBackdrop()
    {
        for (var p = Parent; p != null; p = p.Parent)
        {
            if (p is LobbyGui lobby)
                return lobby.Background;
        }

        if (IoCManager.Instance?.TryResolveType<IUserInterfaceManager>(out var uiMan) == true
            && uiMan.ActiveScreen is LobbyGui activeLobby)
        {
            return activeLobby.Background;
        }

        return null;
    }

    private void DrawBackdrop(DrawingHandleScreen handle, Vector2 size)
    {
        var backdrop = Backdrop!;
        var view = backdrop.ViewSize;
        if (view.X <= 0f || view.Y <= 0f)
            return;

        var screenPosition = new Vector2(GlobalPixelPosition.X, GlobalPixelPosition.Y);
        var zoom = backdrop.ViewZoom;
        var pan = backdrop.ViewPan;
        var fade = backdrop.BlurBlend;

        var current = backdrop.CurrentBlurredTexture;
        if (current != null)
            DrawRegion(handle, current, screenPosition, size, view, zoom, pan, 1f);

        var next = backdrop.NextBlurredTexture;
        if (next == null || fade < 0f)
            return;

        DrawRegion(handle, next, screenPosition, size, view, zoom, pan, Easings.InOutQuad(fade));
    }

    private static void DrawRegion(
        DrawingHandleScreen handle,
        Texture texture,
        Vector2 screenPosition,
        Vector2 size,
        Vector2 view,
        float zoom,
        Vector2 pan,
        float alpha)
    {
        if (alpha <= 0.001f)
            return;

        var textureSize = texture.Size;
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
            return;

        // Same cover transform as LobbyBackdrop.DrawCover.
        var scale = MathF.Max(view.X / textureSize.X, view.Y / textureSize.Y) * zoom;
        var drawSize = textureSize * scale;
        var overflow = drawSize - view;
        var offset = -overflow * 0.5f + overflow * 0.5f * pan;

        var uvTopLeft = (screenPosition - offset) / drawSize;
        var uvBottomRight = (screenPosition + size - offset) / drawSize;

        uvTopLeft = Vector2.Clamp(uvTopLeft, Vector2.Zero, Vector2.One);
        uvBottomRight = Vector2.Clamp(uvBottomRight, Vector2.Zero, Vector2.One);

        var subRegion = new UIBox2(uvTopLeft * textureSize, uvBottomRight * textureSize);
        if (subRegion.Width <= 0f || subRegion.Height <= 0f)
            return;

        handle.DrawTextureRectRegion(
            texture,
            new UIBox2(Vector2.Zero, size),
            subRegion,
            Color.White.WithAlpha(alpha));
    }

    private void DrawTint(DrawingHandleScreen handle, Vector2 size)
    {
        if (Tint.A <= 0.001f && EdgeColor.A <= 0.001f)
            return;

        if (Tint.A > 0.001f)
        {
            handle.DrawRect(new UIBox2(Vector2.Zero, size), Tint);

            // A faint top light keeps the glass from reading as a flat dark box.
            const int steps = 10;
            var height = MathF.Min(size.Y * 0.45f, 160f);
            var step = height / steps;
            for (var i = 0; i < steps; i++)
            {
                var t = (i + 0.5f) / steps;
                handle.DrawRect(
                    new UIBox2(0f, i * step, size.X, (i + 1) * step + 0.5f),
                    Color.White.WithAlpha(0.008f * (1f - t)));
            }
        }

        if (EdgeColor.A > 0.001f)
        {
            handle.DrawRect(new UIBox2(0f, 0f, size.X, 1f), Color.White.WithAlpha(0.04f));
            handle.DrawRect(new UIBox2(0f, 0f, 1f, size.Y), EdgeColor);
            handle.DrawRect(new UIBox2(size.X - 1f, 0f, size.X, size.Y), EdgeColor);
            handle.DrawRect(new UIBox2(0f, size.Y - 1f, size.X, size.Y), Color.Black.WithAlpha(0.18f));
        }
    }
}
