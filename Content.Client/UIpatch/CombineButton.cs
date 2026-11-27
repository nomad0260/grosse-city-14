using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Кнопка в стиле Combine: тёмная панель с красной рамкой, без скруглений.
/// Состояния сглажены, а «готовность» получает пробегающий блик и медленное
/// дыхание рамки, чтобы включение читалось мгновенно.
/// </summary>
public sealed class CombineButton : Button
{
    public enum CombineButtonStyle
    {
        Primary,
        Secondary,
        Danger,
        Link,
    }

    private CombineButtonStyle _buttonStyle = CombineButtonStyle.Primary;

    public CombineButtonStyle ButtonStyle
    {
        get => _buttonStyle;
        set
        {
            if (_buttonStyle == value)
                return;

            _buttonStyle = value;
        }
    }

    private static readonly Color PrimaryTop = Color.FromHex("#3A0A0E");
    private static readonly Color PrimaryBottom = Color.FromHex("#130406");
    private static readonly Color PrimaryBorder = Color.FromHex("#7C131A");
    private static readonly Color AccentRed = Color.FromHex("#E51E2A");
    private static readonly Color AccentRedGlow = Color.FromHex("#FF2635");
    private static readonly Color SecondaryBg = Color.FromHex("#101215");
    private static readonly Color BorderGray = Color.FromHex("#2A2D31");
    private static readonly Color DangerBg = Color.FromHex("#0D0E10");
    private static readonly Color ReadyTop = Color.FromHex("#D91C28");
    private static readonly Color ReadyTopHover = Color.FromHex("#FF3340");
    private static readonly Color ReadyBottom = Color.FromHex("#6B0C12");
    private static readonly Color ReadyEdge = Color.FromHex("#FF7A82");

    private const float WipeDuration = 0.22f;
    private const float WipeFadeDuration = 0.5f;

    private float _time;
    private float _hover;
    private float _press;
    private float _wipe = -1f;
    private float _wipeTarget;
    private float _wipeFade;
    private bool _wasHovered;

    public CombineButton()
    {
        // Убираем стандартную тему, чтобы кнопка рисовалась полностью в стиле лобби.
        StyleBoxOverride = new StyleBoxFlat(Color.Transparent);
        // Пробегающий блик и штриховка не должны выходить за пределы кнопки.
        RectClipContent = true;
        OnToggled += OnToggledInternal;
        OnPressed += _ => LobbyFx.PlayClick();
    }

    private void OnToggledInternal(BaseButton.ButtonToggledEventArgs args)
    {
        // The wipe belongs to the big primary action; other toggles only change state.
        if (!ToggleMode || _buttonStyle != CombineButtonStyle.Primary)
            return;

        // Arm/disarm wipe: runs left-to-right when becoming ready and back when not.
        _wipe = args.Pressed ? 0f : 1f;
        _wipeTarget = args.Pressed ? 1f : 0f;
        _wipeFade = 1f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _time += args.DeltaSeconds;

        var hovered = !Disabled && IsHovered;
        if (hovered && !_wasHovered)
            LobbyFx.PlayHover();

        _wasHovered = hovered;
        _hover = LobbyFx.Approach(_hover, hovered ? 1f : 0f, 16f, args.DeltaSeconds);
        _press = LobbyFx.Approach(_press, DrawMode == DrawModeEnum.Pressed ? 1f : 0f, 28f, args.DeltaSeconds);

        if (_wipe < 0f)
            return;

        var step = args.DeltaSeconds / WipeDuration;
        if (_wipe < _wipeTarget)
            _wipe = MathF.Min(_wipeTarget, _wipe + step);
        else if (_wipe > _wipeTarget)
            _wipe = MathF.Max(_wipeTarget, _wipe - step);

        if (MathF.Abs(_wipe - _wipeTarget) > 0.0001f)
            return;

        _wipeFade = MathF.Max(0f, _wipeFade - args.DeltaSeconds / WipeFadeDuration);
        if (_wipeFade <= 0f)
            _wipe = -1f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var disabled = Disabled;
        var pressed = DrawMode == DrawModeEnum.Pressed;
        var toggled = ToggleMode && Pressed && !disabled;

        switch (_buttonStyle)
        {
            case CombineButtonStyle.Primary:
                DrawPrimary(handle, size, pressed, toggled);
                break;
            case CombineButtonStyle.Secondary:
                DrawSecondary(handle, size);
                break;
            case CombineButtonStyle.Danger:
                DrawDanger(handle, size);
                break;
            case CombineButtonStyle.Link:
                DrawLink(handle, size);
                break;
        }

        // Недоступная кнопка не должна выглядеть кликабельной.
        if (disabled)
            DrawDisabled(handle, size);

        if (HasKeyboardFocus())
            DrawBorder(handle, size, 1f, Color.White.WithAlpha(0.85f));
    }

    private static void DrawFill(DrawingHandleScreen handle, UIBox2 box, Color color)
    {
        handle.DrawRect(box, color);
    }

    private static void DrawBorder(DrawingHandleScreen handle, Vector2 size, float thickness, Color color)
    {
        handle.DrawRect(new UIBox2(0, 0, size.X, thickness), color);
        handle.DrawRect(new UIBox2(0, size.Y - thickness, size.X, size.Y), color);
        handle.DrawRect(new UIBox2(0, 0, thickness, size.Y), color);
        handle.DrawRect(new UIBox2(size.X - thickness, 0, size.X, size.Y), color);
    }

    private void DrawPrimary(DrawingHandleScreen handle, Vector2 size, bool pressed, bool toggled)
    {
        if (toggled)
        {
            DrawReady(handle, size);
            return;
        }

        var top = Color.InterpolateBetween(PrimaryTop.WithAlpha(0.92f), PrimaryTop, _hover);
        top = Color.InterpolateBetween(top, PrimaryTop.WithAlpha(0.72f), _press);
        var bottom = Color.InterpolateBetween(PrimaryBottom, PrimaryBottom.WithAlpha(0.7f), _press);

        // Горизонтальный градиент: плотный красный слева уходит в почти чёрный справа.
        const int steps = 14;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float) steps;
            var x1 = size.X * t;
            var x2 = size.X * (t + 1f / steps);
            var color = Color.InterpolateBetween(top, bottom, t);
            DrawFill(handle, new UIBox2(x1, 0, x2, size.Y), color);
        }

        // Красная полоса слева, как в референсе.
        DrawFill(handle, new UIBox2(0, 0, 3f, size.Y), AccentRed);
        DrawFill(handle, new UIBox2(3f, 0, 10f, size.Y), AccentRed.WithAlpha(0.18f));

        DrawBorder(handle, size, 1f, Color.InterpolateBetween(PrimaryBorder, AccentRedGlow, _hover));

        // Внутренний блик по верхней кромке.
        DrawFill(handle, new UIBox2(1f, 1f, size.X - 1f, 2f), Color.White.WithAlpha(0.06f));

        if (_hover > 0.01f)
            DrawFill(handle, new UIBox2(1, 1, size.X - 1, size.Y - 1), AccentRed.WithAlpha(0.08f * _hover));
    }

    /// <summary>
    /// The persistent "ready" state. It needs to read differently from a dark red
    /// idle primary button, otherwise the toggle only changes the label text.
    /// </summary>
    private void DrawReady(DrawingHandleScreen handle, Vector2 size)
    {
        var glow = LobbyFx.ReducedMotion ? 0.5f : LobbyFx.Pulse(_time, 0.42f);

        var top = Color.InterpolateBetween(ReadyTop, ReadyTopHover, _hover);
        top = Color.InterpolateBetween(top, ReadyTopHover, _press * 0.6f);

        const int steps = 20;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float) steps;
            var y1 = size.Y * t;
            var y2 = size.Y * (t + 1f / steps);
            var color = Color.InterpolateBetween(top, ReadyBottom, t);
            DrawFill(handle, new UIBox2(0, y1, size.X, y2), color);
        }

        DrawFill(handle, new UIBox2(0, 0, 3f, size.Y), ReadyEdge);
        DrawFill(handle, new UIBox2(3f, 0, 10f, size.Y), ReadyEdge.WithAlpha(0.18f));
        DrawBorder(handle, size, 1f, ReadyEdge.WithAlpha(0.6f + 0.3f * glow));
        DrawFill(handle, new UIBox2(1f, 1f, size.X - 1f, 2f), Color.White.WithAlpha(0.28f));
        DrawFill(handle, new UIBox2(1f, 1f, size.X - 1f, size.Y - 1f),
            Color.White.WithAlpha(0.03f + 0.045f * glow));

        if (_hover > 0.01f)
            DrawFill(handle, new UIBox2(1f, 1f, size.X - 1f, size.Y - 1f), Color.White.WithAlpha(0.06f * _hover));

        DrawWipe(handle, size);
    }

    /// <summary>
    /// A soft light band that sweeps across the button when readiness changes.
    /// </summary>
    private void DrawWipe(DrawingHandleScreen handle, Vector2 size)
    {
        if (_wipe < 0f || _wipeFade <= 0f)
            return;

        var front = size.X * _wipe;
        var half = MathF.Min(size.X * 0.32f, 130f);

        const int steps = 16;
        var step = half * 2f / steps;

        for (var i = 0; i < steps; i++)
        {
            var local = (i + 0.5f) / steps;
            var distance = MathF.Abs(local - 0.5f) * 2f;
            var alpha = 0.16f * (1f - distance) * _wipeFade;
            if (alpha <= 0.001f)
                continue;

            var x0 = front - half + i * step;
            DrawFill(handle, new UIBox2(x0, 0, x0 + step + 0.5f, size.Y), Color.White.WithAlpha(alpha));
        }

        DrawFill(handle, new UIBox2(front - 1.5f, 0, front + 1.5f, size.Y),
            Color.White.WithAlpha(0.3f * _wipeFade));
    }

    private void DrawSecondary(DrawingHandleScreen handle, Vector2 size)
    {
        var background = Color.InterpolateBetween(SecondaryBg.WithAlpha(0.92f), SecondaryBg.WithAlpha(0.7f), _press);
        DrawFill(handle, new UIBox2(0, 0, size.X, size.Y), background);

        if (_hover > 0.01f)
            DrawFill(handle, new UIBox2(0, 0, size.X, size.Y), AccentRed.WithAlpha(0.07f * _hover));

        DrawBorder(handle, size, 1f,
            Color.InterpolateBetween(BorderGray, AccentRed.WithAlpha(0.65f), _hover));
    }

    private void DrawLink(DrawingHandleScreen handle, Vector2 size)
    {
        if (_hover <= 0.01f && !Pressed)
            return;

        var alpha = Pressed ? 0.16f : 0.08f * _hover;
        DrawFill(handle, new UIBox2(0f, 0f, size.X, size.Y), AccentRed.WithAlpha(alpha));
        DrawFill(handle, new UIBox2(0f, size.Y - 1f, size.X, size.Y), AccentRed.WithAlpha(0.55f * _hover));
    }

    private void DrawDanger(DrawingHandleScreen handle, Vector2 size)
    {
        DrawFill(handle, new UIBox2(0, 0, size.X, size.Y),
            Color.InterpolateBetween(DangerBg.WithAlpha(0.92f), DangerBg.WithAlpha(0.7f), _press));

        if (_hover > 0.01f)
            DrawFill(handle, new UIBox2(0, 0, size.X, size.Y), AccentRed.WithAlpha(0.1f * _hover));

        DrawBorder(handle, size, 1f,
            Color.InterpolateBetween(AccentRed.WithAlpha(0.8f), AccentRedGlow, _hover));
    }

    private void DrawDisabled(DrawingHandleScreen handle, Vector2 size)
    {
        DrawFill(handle, new UIBox2(0, 0, size.X, size.Y), Color.Black.WithAlpha(0.42f));

        var shift = LobbyFx.ReducedMotion ? 0f : _time * 9f % 17f;
        var hatch = Color.White.WithAlpha(0.035f);

        for (var x = -size.Y - 17f + shift; x < size.X + 17f; x += 17f)
            handle.DrawLine(new Vector2(x, size.Y), new Vector2(x + size.Y, 0f), hatch);

        DrawBorder(handle, size, 1f, Color.FromHex("#2A2C2F"));
    }
}
