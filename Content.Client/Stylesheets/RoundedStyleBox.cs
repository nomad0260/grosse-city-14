using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.Stylesheets;

/// <summary>
/// A flat stylebox with per-corner rounding and an optional border. Unlike
/// <see cref="StyleBoxFlat"/> it supports rounded corners, and unlike
/// <see cref="StyleBoxTexture"/> it has no bevels, giving a clean modern look.
/// </summary>
/// <remarks>
/// The shape is drawn as a single triangle fan so semi-transparent colors don't
/// double-blend where the border and the fill overlap.
/// <para>
/// Uses <see cref="List{T}"/> rather than spans: the Robust content sandbox only whitelists
/// <c>Span&lt;T&gt;.ToArray()</c>, so indexing a span is a sandbox violation.
/// </para>
/// </remarks>
public sealed class RoundedStyleBox : StyleBox
{
    /// <summary>Number of segments used per rounded corner.</summary>
    private const int CornerSegments = 12;

    // Reused vertex buffer; UI drawing is single-threaded.
    private readonly List<Vector2> _points = new((CornerSegments + 1) * 4);

    private float _topLeft = 8f;
    private float _topRight = 8f;
    private float _bottomRight = 8f;
    private float _bottomLeft = 8f;

    public Color BackgroundColor { get; set; }
    public Color BorderColor { get; set; }
    public float BorderThickness { get; set; }

    /// <summary>Sets the radius of every corner at once.</summary>
    public float CornerRadius
    {
        get => _topLeft;
        set => _topLeft = _topRight = _bottomRight = _bottomLeft = value;
    }

    public float RadiusTopLeft
    {
        get => _topLeft;
        set => _topLeft = value;
    }

    public float RadiusTopRight
    {
        get => _topRight;
        set => _topRight = value;
    }

    public float RadiusBottomRight
    {
        get => _bottomRight;
        set => _bottomRight = value;
    }

    public float RadiusBottomLeft
    {
        get => _bottomLeft;
        set => _bottomLeft = value;
    }

    /// <summary>Convenience helper for button-group styles: only the left corners are rounded.</summary>
    public static RoundedStyleBox OpenLeft(Color background, Color border, float borderThickness, float radius)
    {
        var box = new RoundedStyleBox
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = borderThickness,
        };
        box._topRight = 0f;
        box._bottomRight = 0f;
        box._topLeft = radius;
        box._bottomLeft = radius;
        return box;
    }

    /// <summary>Convenience helper for button-group styles: only the right corners are rounded.</summary>
    public static RoundedStyleBox OpenRight(Color background, Color border, float borderThickness, float radius)
    {
        var box = new RoundedStyleBox
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = borderThickness,
        };
        box._topLeft = 0f;
        box._bottomLeft = 0f;
        box._topRight = radius;
        box._bottomRight = radius;
        return box;
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        if (box.Width <= 0f || box.Height <= 0f)
            return;

        var maxRadius = Math.Min(box.Width, box.Height) / 2f;
        var topLeft = Math.Min(_topLeft * uiScale, maxRadius);
        var topRight = Math.Min(_topRight * uiScale, maxRadius);
        var bottomRight = Math.Min(_bottomRight * uiScale, maxRadius);
        var bottomLeft = Math.Min(_bottomLeft * uiScale, maxRadius);

        var border = BorderThickness * uiScale;
        if (border > 0f && BorderColor.A > 0f)
        {
            DrawShape(handle, box, topLeft, topRight, bottomRight, bottomLeft, BorderColor);

            box = new UIBox2(box.Left + border, box.Top + border, box.Right - border, box.Bottom - border);
            topLeft = Math.Max(0f, topLeft - border);
            topRight = Math.Max(0f, topRight - border);
            bottomRight = Math.Max(0f, bottomRight - border);
            bottomLeft = Math.Max(0f, bottomLeft - border);
        }

        DrawShape(handle, box, topLeft, topRight, bottomRight, bottomLeft, BackgroundColor);
    }

    private void DrawShape(DrawingHandleScreen handle, UIBox2 box, float topLeft, float topRight, float bottomRight,
        float bottomLeft, Color color)
    {
        if (box.Width <= 0f || box.Height <= 0f || color.A <= 0f)
            return;

        if (topLeft <= 0.5f && topRight <= 0.5f && bottomRight <= 0.5f && bottomLeft <= 0.5f)
        {
            handle.DrawRect(box, color);
            return;
        }

        var left = box.Left;
        var top = box.Top;
        var right = box.Right;
        var bottom = box.Bottom;

        _points.Clear();
        AddArc(new Vector2(left + topLeft, top + topLeft), topLeft, MathF.PI, MathF.PI * 1.5f);
        AddArc(new Vector2(right - topRight, top + topRight), topRight, MathF.PI * 1.5f, MathF.PI * 2f);
        AddArc(new Vector2(right - bottomRight, bottom - bottomRight), bottomRight, 0f, MathF.PI * 0.5f);
        AddArc(new Vector2(left + bottomLeft, bottom - bottomLeft), bottomLeft, MathF.PI * 0.5f, MathF.PI);

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _points, color);
    }

    private void AddArc(Vector2 center, float radius, float start, float end)
    {
        // A zero radius collapses every point onto the corner itself, which keeps the
        // fan convex and makes that corner perfectly square.
        for (var segment = 0; segment <= CornerSegments; segment++)
        {
            var angle = start + (end - start) * (segment / (float) CornerSegments);
            _points.Add(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
    }
}
