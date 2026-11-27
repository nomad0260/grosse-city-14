using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.UIpatch;

/// <summary>
/// Small monochrome vector icons used by the lobby HUD.
/// They are drawn with primitives so the lobby does not depend on emoji or
/// symbol glyph coverage of the selected UI font.
/// </summary>
public sealed class LobbyIcon : Robust.Client.UserInterface.Control
{
    public enum IconType : byte
    {
        Logo,
        Players,
        Station,
        Clock,
        Map,
        Star,
        Shield,
        Info,
        Gear,
        Book,
        Warning,
        Eye,
        Menu,
        Power,
        Person,
        ChevronRight,
        Signal,
        Chat,
        Question,
        ArrowTopRight,
    }

    private static readonly Color LogoHole = Color.FromHex("#0B0D0F");

    public IconType Icon { get; set; } = IconType.Info;

    public Color IconColor { get; set; } = Color.FromHex("#969BA2");

    /// <summary>
    /// Target rotation in radians. The icon eases towards it, which lets a
    /// chevron flip without a hard swap.
    /// </summary>
    public float Rotation { get; set; }

    private float _displayRotation;

    public LobbyIcon()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (MathF.Abs(Rotation - _displayRotation) <= 0.001f)
        {
            _displayRotation = Rotation;
            return;
        }

        _displayRotation = LobbyFx.Approach(_displayRotation, Rotation, 18f, args.DeltaSeconds);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(16f, 16f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var pixel = PixelSize;
        var size = MathF.Min(pixel.X, pixel.Y);
        if (size <= 2f)
            return;

        var origin = new Vector2((pixel.X - size) * 0.5f, (pixel.Y - size) * 0.5f);
        var color = IconColor;
        var stroke = MathF.Max(1.15f, size * 0.082f);

        var rotation = _displayRotation;
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);

        Vector2 P(float x, float y)
        {
            var vx = x - 0.5f;
            var vy = y - 0.5f;

            if (rotation != 0f)
            {
                var rx = vx * cos - vy * sin;
                var ry = vx * sin + vy * cos;
                vx = rx;
                vy = ry;
            }

            return origin + new Vector2((vx + 0.5f) * size, (vy + 0.5f) * size);
        }

        void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { a, b, c }, color);
        }

        void Line(Vector2 a, Vector2 b, float width)
        {
            var dir = b - a;
            if (dir.LengthSquared() <= 0.0001f)
                return;

            var normal = Vector2.Normalize(new Vector2(-dir.Y, dir.X)) * (width * 0.5f);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { a + normal, b + normal, b - normal }, color);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                new[] { a + normal, b - normal, a - normal }, color);
        }

        void LineNorm(float x1, float y1, float x2, float y2, float width)
        {
            Line(P(x1, y1), P(x2, y2), width);
        }

        void Poly(Vector2[] points)
        {
            for (var i = 1; i < points.Length - 1; i++)
                Triangle(points[0], points[i], points[i + 1]);
        }

        void PolyOutline(Vector2[] points, float width)
        {
            for (var i = 0; i < points.Length; i++)
                Line(points[i], points[(i + 1) % points.Length], width);
        }

        Vector2[] CirclePoints(float cx, float cy, float radius, int segments = 24)
        {
            var points = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = MathF.Tau * i / segments;
                points[i] = P(cx + MathF.Cos(angle) * radius, cy + MathF.Sin(angle) * radius);
            }

            return points;
        }

        void CircleOutline(float cx, float cy, float radius, float width)
        {
            var points = CirclePoints(cx, cy, radius);
            for (var i = 0; i < points.Length; i++)
                Line(points[i], points[(i + 1) % points.Length], width);
        }

        void CircleFilled(float cx, float cy, float radius)
        {
            var points = CirclePoints(cx, cy, radius, 20);
            var center = P(cx, cy);
            for (var i = 0; i < points.Length; i++)
                Triangle(center, points[i], points[(i + 1) % points.Length]);
        }

        void Arc(float cx, float cy, float radius, float from, float to, float width)
        {
            const int segments = 20;
            var previous = P(cx + MathF.Cos(from) * radius, cy + MathF.Sin(from) * radius);
            for (var i = 1; i <= segments; i++)
            {
                var angle = from + (to - from) * i / segments;
                var next = P(cx + MathF.Cos(angle) * radius, cy + MathF.Sin(angle) * radius);
                Line(previous, next, width);
                previous = next;
            }
        }

        switch (Icon)
        {
            case IconType.Logo:
            {
                var outer = new Vector2[6];
                var inner = new Vector2[6];
                for (var i = 0; i < 6; i++)
                {
                    var angle = -MathF.PI / 2f + MathF.PI / 3f * i;
                    outer[i] = P(0.5f + MathF.Cos(angle) * 0.47f, 0.5f + MathF.Sin(angle) * 0.47f);
                    inner[i] = P(0.5f + MathF.Cos(angle) * 0.21f, 0.5f + MathF.Sin(angle) * 0.21f);
                }

                Poly(outer);
                var saved = color;
                color = LogoHole;
                Poly(inner);
                color = saved;
                break;
            }
            case IconType.Players:
                CircleFilled(0.68f, 0.34f, 0.10f);
                Arc(0.68f, 0.9f, 0.26f, MathF.PI, MathF.Tau, stroke * 2.2f);
                CircleFilled(0.35f, 0.35f, 0.125f);
                Arc(0.35f, 0.92f, 0.31f, MathF.PI, MathF.Tau, stroke * 2.6f);
                break;
            case IconType.Station:
                LineNorm(0.24f, 0.32f, 0.76f, 0.32f, stroke);
                LineNorm(0.24f, 0.32f, 0.24f, 0.82f, stroke);
                LineNorm(0.76f, 0.32f, 0.76f, 0.82f, stroke);
                LineNorm(0.24f, 0.82f, 0.76f, 0.82f, stroke);
                LineNorm(0.16f, 0.3f, 0.5f, 0.14f, stroke);
                LineNorm(0.5f, 0.14f, 0.84f, 0.3f, stroke);
                LineNorm(0.33f, 0.44f, 0.45f, 0.44f, stroke);
                LineNorm(0.33f, 0.44f, 0.33f, 0.56f, stroke);
                LineNorm(0.45f, 0.44f, 0.45f, 0.56f, stroke);
                LineNorm(0.55f, 0.44f, 0.67f, 0.44f, stroke);
                LineNorm(0.55f, 0.44f, 0.55f, 0.56f, stroke);
                LineNorm(0.67f, 0.44f, 0.67f, 0.56f, stroke);
                LineNorm(0.44f, 0.82f, 0.44f, 0.66f, stroke);
                LineNorm(0.56f, 0.82f, 0.56f, 0.66f, stroke);
                LineNorm(0.44f, 0.66f, 0.56f, 0.66f, stroke);
                break;
            case IconType.Clock:
                CircleOutline(0.5f, 0.5f, 0.38f, stroke);
                LineNorm(0.5f, 0.5f, 0.5f, 0.25f, stroke * 1.2f);
                LineNorm(0.5f, 0.5f, 0.68f, 0.56f, stroke * 1.2f);
                CircleFilled(0.5f, 0.5f, 0.045f);
                break;
            case IconType.Map:
                CircleOutline(0.5f, 0.4f, 0.23f, stroke);
                LineNorm(0.33f, 0.54f, 0.5f, 0.85f, stroke);
                LineNorm(0.67f, 0.54f, 0.5f, 0.85f, stroke);
                CircleFilled(0.5f, 0.4f, 0.055f);
                break;
            case IconType.Star:
            {
                var center = P(0.5f, 0.53f);
                var outer = new Vector2[5];
                var inner = new Vector2[5];
                for (var i = 0; i < 5; i++)
                {
                    var angle = -MathF.PI / 2f + MathF.Tau / 5f * i;
                    var innerAngle = angle + MathF.Tau / 10f;
                    outer[i] = P(0.5f + MathF.Cos(angle) * 0.46f, 0.53f + MathF.Sin(angle) * 0.46f);
                    inner[i] = P(0.5f + MathF.Cos(innerAngle) * 0.2f, 0.53f + MathF.Sin(innerAngle) * 0.2f);
                }

                for (var i = 0; i < 5; i++)
                {
                    Triangle(center, outer[i], inner[i]);
                    Triangle(center, inner[i], outer[(i + 1) % 5]);
                }

                break;
            }
            case IconType.Shield:
                PolyOutline(new[]
                {
                    P(0.22f, 0.2f), P(0.78f, 0.2f), P(0.78f, 0.48f),
                    P(0.5f, 0.86f), P(0.22f, 0.48f),
                }, stroke);
                LineNorm(0.36f, 0.49f, 0.47f, 0.61f, stroke * 1.2f);
                LineNorm(0.47f, 0.61f, 0.66f, 0.39f, stroke * 1.2f);
                break;
            case IconType.Info:
                CircleOutline(0.5f, 0.5f, 0.4f, stroke);
                CircleFilled(0.5f, 0.3f, 0.055f);
                LineNorm(0.5f, 0.43f, 0.5f, 0.71f, stroke);
                break;
            case IconType.Gear:
                CircleOutline(0.5f, 0.5f, 0.2f, stroke);
                for (var i = 0; i < 8; i++)
                {
                    var angle = MathF.Tau * i / 8f;
                    Line(
                        P(0.5f + MathF.Cos(angle) * 0.27f, 0.5f + MathF.Sin(angle) * 0.27f),
                        P(0.5f + MathF.Cos(angle) * 0.43f, 0.5f + MathF.Sin(angle) * 0.43f),
                        stroke * 1.2f);
                }

                break;
            case IconType.Book:
                PolyOutline(new[] { P(0.1f, 0.24f), P(0.47f, 0.3f), P(0.47f, 0.8f), P(0.1f, 0.74f) }, stroke);
                PolyOutline(new[] { P(0.53f, 0.3f), P(0.9f, 0.24f), P(0.9f, 0.74f), P(0.53f, 0.8f) }, stroke);
                LineNorm(0.5f, 0.3f, 0.5f, 0.8f, stroke);
                break;
            case IconType.Warning:
                PolyOutline(new[] { P(0.5f, 0.12f), P(0.9f, 0.84f), P(0.1f, 0.84f) }, stroke);
                LineNorm(0.5f, 0.38f, 0.5f, 0.6f, stroke * 1.15f);
                CircleFilled(0.5f, 0.72f, 0.05f);
                break;
            case IconType.Eye:
                LineNorm(0.1f, 0.5f, 0.3f, 0.33f, stroke);
                LineNorm(0.3f, 0.33f, 0.5f, 0.3f, stroke);
                LineNorm(0.5f, 0.3f, 0.7f, 0.33f, stroke);
                LineNorm(0.7f, 0.33f, 0.9f, 0.5f, stroke);
                LineNorm(0.1f, 0.5f, 0.3f, 0.67f, stroke);
                LineNorm(0.3f, 0.67f, 0.5f, 0.7f, stroke);
                LineNorm(0.5f, 0.7f, 0.7f, 0.67f, stroke);
                LineNorm(0.7f, 0.67f, 0.9f, 0.5f, stroke);
                CircleFilled(0.5f, 0.5f, 0.13f);
                break;
            case IconType.Menu:
                LineNorm(0.18f, 0.3f, 0.82f, 0.3f, stroke * 1.15f);
                LineNorm(0.18f, 0.5f, 0.82f, 0.5f, stroke * 1.15f);
                LineNorm(0.18f, 0.7f, 0.82f, 0.7f, stroke * 1.15f);
                break;
            case IconType.Power:
                Arc(0.5f, 0.52f, 0.38f, -MathF.PI / 3f, MathF.PI * 1.33f, stroke);
                LineNorm(0.5f, 0.12f, 0.5f, 0.5f, stroke * 1.15f);
                break;
            case IconType.Person:
                CircleFilled(0.5f, 0.3f, 0.14f);
                Arc(0.5f, 0.92f, 0.34f, MathF.PI, MathF.Tau, stroke * 2.8f);
                break;
            case IconType.ChevronRight:
                LineNorm(0.38f, 0.24f, 0.64f, 0.5f, stroke * 1.3f);
                LineNorm(0.64f, 0.5f, 0.38f, 0.76f, stroke * 1.3f);
                break;
            case IconType.Signal:
                LineNorm(0.18f, 0.82f, 0.18f, 0.56f, stroke * 1.5f);
                LineNorm(0.4f, 0.82f, 0.4f, 0.45f, stroke * 1.5f);
                LineNorm(0.62f, 0.82f, 0.62f, 0.34f, stroke * 1.5f);
                LineNorm(0.84f, 0.82f, 0.84f, 0.22f, stroke * 1.5f);
                break;
            case IconType.Chat:
                LineNorm(0.14f, 0.2f, 0.86f, 0.2f, stroke);
                LineNorm(0.86f, 0.2f, 0.86f, 0.64f, stroke);
                LineNorm(0.86f, 0.64f, 0.54f, 0.64f, stroke);
                LineNorm(0.54f, 0.64f, 0.34f, 0.84f, stroke);
                LineNorm(0.34f, 0.84f, 0.34f, 0.64f, stroke);
                LineNorm(0.34f, 0.64f, 0.14f, 0.64f, stroke);
                LineNorm(0.14f, 0.64f, 0.14f, 0.2f, stroke);
                CircleFilled(0.32f, 0.42f, 0.045f);
                CircleFilled(0.5f, 0.42f, 0.045f);
                CircleFilled(0.68f, 0.42f, 0.045f);
                break;
            case IconType.Question:
                CircleOutline(0.5f, 0.5f, 0.4f, stroke);
                Arc(0.5f, 0.38f, 0.14f, MathF.PI, MathF.PI * 2.15f, stroke * 1.1f);
                LineNorm(0.62f, 0.42f, 0.5f, 0.56f, stroke * 1.1f);
                CircleFilled(0.5f, 0.70f, 0.055f);
                break;
            case IconType.ArrowTopRight:
                LineNorm(0.22f, 0.78f, 0.78f, 0.22f, stroke * 1.25f);
                LineNorm(0.36f, 0.22f, 0.78f, 0.22f, stroke * 1.25f);
                LineNorm(0.78f, 0.22f, 0.78f, 0.64f, stroke * 1.25f);
                break;
        }
    }
}
