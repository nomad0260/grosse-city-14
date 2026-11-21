using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using UiControl = Robust.Client.UserInterface.Control;

namespace Content.Client._Grosse.Containment.UI;

/// <summary>
/// Едва заметная CRT-плёнка. По умолчанию выключена; не перехватывает мышь.
/// </summary>
public sealed class CrtOverlay : UiControl
{
    public Color ScanlineColor = Color.FromHex("#E0503C").WithAlpha(0.012f);

    /// <summary>Множитель яркости плёнки: 0 — выключено, 1 — штатная яркость.</summary>
    public float Intensity = 1f;

    /// <summary>Аварийный режим: по краям пульсирует красная виньетка.</summary>
    public bool Danger;

    /// <summary>
    /// Сила срыва сигнала после катастрофы ядра: 0 — чисто, 1 — сильные помехи.
    /// Даёт расхождение каналов, разрывы строк и статику поверх всего интерфейса.
    /// </summary>
    public float Glitch;

    private float _phase;
    private float _dangerAlpha;
    private float _noiseSeed;

    // Постоянные цвета: Draw вызывается каждый кадр, и разбирать hex-строку
    // заново на каждом кадре незачем.
    private static readonly Color DangerColor = Color.FromHex("#FF4444");
    private static readonly Color AberrationRed = Color.FromHex("#FF2A2A");
    private static readonly Color AberrationCyan = Color.FromHex("#25E0FF");
    private static readonly Color TearColor = Color.FromHex("#050404");

    public CrtOverlay()
    {
        MouseFilter = UiControl.MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase = (_phase + args.DeltaSeconds * 0.08f) % 1f;
        _noiseSeed = (_noiseSeed + args.DeltaSeconds * 37f) % 1000f;
        _dangerAlpha = Math.Clamp(
            _dangerAlpha + (Danger ? args.DeltaSeconds * 2.5f : -args.DeltaSeconds * 1.5f),
            0f,
            1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var width = Size.X;
        var height = Size.Y;
        var intensity = Math.Clamp(Intensity, 0f, 1.5f);

        // Плёнка зависит от Intensity, а помехи и виньетка — нет: аварийный экран
        // должен продолжать сыпать статикой, даже если плёнку выключили.
        if (intensity > 0.001f)
        {
            var scanline = ScanlineColor.WithAlpha(ScanlineColor.A * intensity);

            for (var y = 0f; y < height; y += 6f)
                handle.DrawRect(new UIBox2(0f, y, width, y + 1f), scanline);

            var sweepY = height * _phase;
            handle.DrawRect(new UIBox2(0f, sweepY, width, sweepY + 18f),
                scanline.WithAlpha(0.035f * intensity));
        }

        if (Glitch > 0.01f)
            DrawGlitch(handle, width, height);

        if (_dangerAlpha > 0.01f)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(_phase * 380f);
            const int rings = 14;

            for (var i = 0; i < rings; i++)
            {
                var inset = i * 7f;
                var alpha = (0.05f + 0.06f * pulse) * _dangerAlpha * (1f - i / (float) rings);
                handle.DrawRect(new UIBox2(inset, inset, width - inset, height - inset),
                    DangerColor.WithAlpha(alpha), false);
            }
        }

        handle.SetTransform(oldTransform);
    }

    /// <summary>
    /// Помехи: разъехавшиеся цветовые каналы, порванные строки и статика.
    /// Всё рисуется поверх готового интерфейса, поэтому вёрстку окон не трогает.
    /// </summary>
    private void DrawGlitch(DrawingHandleScreen handle, float width, float height)
    {
        var g = Math.Clamp(Glitch, 0f, 1f);
        var red = AberrationRed;
        var cyan = AberrationCyan;

        // Аберрация: красный и голубой каналы расходятся по горизонтали.
        var bands = 2 + (int) (g * 5f);

        for (var i = 0; i < bands; i++)
        {
            var y = Noise(i * 3 + 1) * height;
            var thickness = 2f + Noise(i * 7 + 2) * 14f;
            var shift = (Noise(i * 11 + 3) - 0.5f) * 18f * g;

            handle.DrawRect(new UIBox2(shift, y, width + shift, y + thickness), red.WithAlpha(0.10f * g));
            handle.DrawRect(new UIBox2(-shift, y, width - shift, y + thickness), cyan.WithAlpha(0.10f * g));
            handle.DrawRect(new UIBox2(0f, y, width, y + 1f), Color.Black.WithAlpha(0.25f * g));
        }

        // Разрывы строк: полоса уезжает в сторону, как при сбитой синхронизации.
        var tears = 1 + (int) (g * 3f);

        for (var i = 0; i < tears; i++)
        {
            var y = Noise(i * 17 + 5) * height;
            var tearHeight = 6f + Noise(i * 13 + 6) * 26f;
            var shift = (Noise(i * 19 + 7) - 0.5f) * 27f * g;

            handle.DrawRect(new UIBox2(0f, y, width, y + tearHeight),
                TearColor.WithAlpha(0.55f * g));
            handle.DrawRect(new UIBox2(shift, y, width + shift, y + 2f), Color.White.WithAlpha(0.22f * g));
            handle.DrawRect(new UIBox2(-shift, y + tearHeight, width - shift, y + tearHeight + 2f),
                red.WithAlpha(0.30f * g));
        }

        // Статика.
        var dots = (int) (60f * g);

        for (var i = 0; i < dots; i++)
        {
            var x = Noise(i * 23 + 11) * width;
            var y = Noise(i * 29 + 12) * height;
            var size = 1f + Noise(i * 31 + 13) * 3f;
            var alpha = 0.04f + 0.16f * Noise(i * 37 + 14) * g;

            handle.DrawRect(new UIBox2(x, y, x + size, y + size), Color.White.WithAlpha(alpha));
        }

        // Редкая вспышка всего экрана.
        if (Noise(101) > 0.93f)
            handle.DrawRect(new UIBox2(0f, 0f, width, height), Color.White.WithAlpha(0.05f + 0.08f * g));
    }

    /// <summary>Детерминированный шум: меняется во времени, но не скачет хаотично каждый кадр.</summary>
    private float Noise(int salt)
    {
        var value = MathF.Sin((_noiseSeed + salt * 12.9898f) * 1.7f) * 43758.5453f;
        return value - MathF.Floor(value);
    }
}

/// <summary>Компактный анимированный график для карточки выработки.</summary>
public sealed class CrtSparkline : UiControl
{
    private readonly List<float> _values = new();
    private float _phase;

    public Color LineColor = Color.FromHex("#4DA3FF");
    public Color GridColor = Color.FromHex("#2A1518");

    public void Push(float value)
    {
        _values.Add(Math.Clamp(value, 0f, 100f));

        while (_values.Count > 48)
            _values.RemoveAt(0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var width = Size.X;
        var height = Size.Y;

        handle.DrawLine(new Vector2(0f, height - 1f), new Vector2(width, height - 1f), GridColor);
        handle.DrawLine(new Vector2(0f, height * 0.5f), new Vector2(width, height * 0.5f),
            GridColor.WithAlpha(0.35f));

        if (_values.Count < 2)
        {
            handle.SetTransform(oldTransform);
            return;
        }

        var step = width / Math.Max(1, _values.Count - 1);
        var previous = new Vector2(0f, height - _values[0] / 100f * height);

        for (var i = 1; i < _values.Count; i++)
        {
            var current = new Vector2(i * step, height - _values[i] / 100f * height);
            handle.DrawLine(previous, current, LineColor.WithAlpha(0.22f));
            handle.DrawLine(previous, current, LineColor);
            previous = current;
        }

        var pulse = 0.45f + 0.35f * (0.5f + 0.5f * MathF.Sin(_phase * 3.2f));
        handle.DrawCircle(previous, 3.2f, LineColor.WithAlpha(pulse));
        handle.DrawCircle(previous, 1.6f, LineColor);

        handle.SetTransform(oldTransform);
    }
}

/// <summary>Декоративная камера сингулярности для вкладки поля.</summary>
public sealed class CrtFieldPreview : UiControl
{
    public float Compression;
    public float CompressionThreshold = 260f;
    public float Expansion;

    /// <summary>Текущая интенсивность каждого эмиттера, %.</summary>
    public readonly float[] EmitterOutput = { 100f, 100f, 100f, 100f };

    private float _phase;

    private static readonly Color FieldBaseColor = Color.FromHex("#E0574A");
    private static readonly Color FieldHotColor = Color.FromHex("#FFB27A");
    private static readonly Color FieldDeadColor = Color.FromHex("#3A4A45");

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase = (_phase + args.DeltaSeconds * 0.45f) % MathF.Tau;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var center = Size / 2f;
        var radius = MathF.Min(Size.X, Size.Y) * 0.42f;
        var cyan = FieldBaseColor;

        for (var i = 1; i <= 5; i++)
        {
            var fraction = i / 5f;
            handle.DrawCircle(center, radius * fraction, cyan.WithAlpha(0.08f + 0.05f * (1f - fraction)), false);
        }

        handle.DrawLine(new Vector2(center.X - radius, center.Y), new Vector2(center.X + radius, center.Y),
            cyan.WithAlpha(0.18f));
        handle.DrawLine(new Vector2(center.X, center.Y - radius), new Vector2(center.X, center.Y + radius),
            cyan.WithAlpha(0.18f));

        var compression = Math.Clamp(Compression / MathF.Max(1f, CompressionThreshold), 0f, 1f);
        var expansion = Math.Clamp(Expansion / 200f, 0f, 1f);
        var coreRadius = MathF.Max(3f, radius * (0.22f - compression * 0.12f + expansion * 0.08f));
        var coreColor = Color.InterpolateBetween(cyan, FieldHotColor,
            Math.Clamp(compression * 1.5f, 0f, 1f));

        handle.DrawCircle(center, coreRadius + 10f, coreColor.WithAlpha(0.06f));
        handle.DrawCircle(center, coreRadius + 6f, coreColor.WithAlpha(0.12f));
        handle.DrawCircle(center, coreRadius + 3f, coreColor.WithAlpha(0.30f));
        handle.DrawCircle(center, coreRadius, coreColor);

        var spokeCount = 8;

        for (var i = 0; i < spokeCount; i++)
        {
            var angle = _phase + MathF.Tau * i / spokeCount;
            var from = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (coreRadius + 6f);
            var to = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            handle.DrawLine(from, to, cyan.WithAlpha(0.20f));
        }

        // Четыре узла-эмиттера по углам: яркость узла отражает интенсивность.
        for (var i = 0; i < 4; i++)
        {
            var angle = MathF.PI / 4f + MathF.Tau * i / 4f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var node = center + direction * (radius * 0.94f);
            var output = Math.Clamp(EmitterOutput[i], 0f, 100f) / 100f;
            var dead = FieldDeadColor;

            handle.DrawCircle(node, 9f, (output > 0.02f ? cyan : dead).WithAlpha(0.06f));
            handle.DrawCircle(node, 4.2f, output > 0.02f
                ? cyan.WithAlpha(0.8f)
                : dead);

            if (output <= 0.02f)
                continue;

            var flicker = 0.55f + 0.25f * MathF.Sin(_phase * 3.1f + i * 1.7f);
            var beamColor = compression > 0.85f ? coreColor : cyan;

            handle.DrawLine(node, center, beamColor.WithAlpha(0.06f + 0.10f * output));
            handle.DrawLine(node, center,
                beamColor.WithAlpha((0.10f + 0.14f * output) * flicker));
        }

        handle.SetTransform(oldTransform);
    }
}

/// <summary>Сегментированная полоса с плавным заполнением и лёгким свечением.</summary>
public sealed class CrtSegmentBar : UiControl
{
    public float Value;
    public float MaxValue = 100f;
    public int SegmentCount = 24;
    public Color FillColor = Color.FromHex("#43DD85");
    public Color EmptyColor = Color.FromHex("#1E1214");
    public Color BorderColor = Color.FromHex("#301A1C");
    public bool ShowBorder = true;
    public bool Animate = true;

    private float _displayValue;
    private float _phase;
    private bool _initialized;
    private float _previousValue;
    private float _dropFlash;

    private static readonly Color DropFlashColor = Color.FromHex("#FF6961");

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Резкое падение — красная волна по заполненной части.
        if (_initialized && Value < _previousValue - MaxValue * 0.015f)
            _dropFlash = 1f;

        _previousValue = Value;

        _dropFlash = MathF.Max(0f, _dropFlash - args.DeltaSeconds * 1.6f);

        if (Animate)
        {
            if (!_initialized)
            {
                _displayValue = Value;
                _initialized = true;
            }
            else
            {
                var speed = Math.Clamp(args.DeltaSeconds * 7f, 0f, 1f);
                _displayValue += (Value - _displayValue) * speed;
            }
        }
        else
        {
            _displayValue = Value;
        }

        _phase += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var width = Size.X;
        var height = Size.Y;

        if (width <= 0f || height <= 0f || MaxValue <= 0f)
        {
            handle.SetTransform(oldTransform);
            return;
        }

        var gap = 2f;
        var segmentWidth = MathF.Max(1f, (width - gap * (SegmentCount - 1)) / SegmentCount);
        var filled = Math.Clamp(_displayValue / MaxValue, 0f, 1f) * SegmentCount;
        var shimmer = 0.80f + 0.20f * (0.5f + 0.5f * MathF.Sin(_phase * 2.6f));

        for (var i = 0; i < SegmentCount; i++)
        {
            var x = i * (segmentWidth + gap);
            var color = i < MathF.Floor(filled)
                ? FillColor.WithAlpha(shimmer)
                : i < filled
                    ? FillColor.WithAlpha(0.55f)
                    : EmptyColor;

            handle.DrawRect(new UIBox2(x, 0f, x + segmentWidth, height), color);
        }

        var filledWidth = Math.Clamp(filled / SegmentCount, 0f, 1f) * width;

        if (_dropFlash > 0.01f)
        {
            handle.DrawRect(new UIBox2(0f, 0f, filledWidth, height),
                DropFlashColor.WithAlpha(0.20f * _dropFlash));
        }

        // Бегущий блик по кромке заполнения.
        var spark = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_phase * 3.4f));
        handle.DrawRect(
            new UIBox2(MathF.Max(0f, filledWidth - 2f), 0f, MathF.Min(width, filledWidth + 1f), height),
            FillColor.WithAlpha(spark * 0.85f));

        if (ShowBorder)
            handle.DrawRect(new UIBox2(0f, 0f, width, height), BorderColor, false);

        handle.SetTransform(oldTransform);
    }
}

/// <summary>
/// График телеметрии с тремя сериями: стабильность, температура и выработка.
/// Значения приходят нормализованными в процентах.
/// </summary>
public sealed class CrtTelemetryGraph : UiControl
{
    private readonly List<float> _stability = new();
    private readonly List<float> _temperature = new();
    private readonly List<float> _energy = new();

    public Color StabilityColor = Color.FromHex("#43DD85");
    public Color TemperatureColor = Color.FromHex("#F5A623");
    public Color EnergyColor = Color.FromHex("#4DA3FF");
    public int VisibleSamples = 80;

    /// <summary>Вертикальное усиление кривых: 1 — штатный масштаб.</summary>
    public float Gain = 1f;

    private float _phase;
    private float _drawIn = 1f;

    private static readonly Color GridLineColor = Color.FromHex("#241416");
    private static readonly Color SweepColor = Color.FromHex("#E23B3B");

    public void Push(float stability, float temperature, float energy)
    {
        _stability.Add(Math.Clamp(stability, 0f, 100f));
        _temperature.Add(Math.Clamp(temperature, 0f, 100f));
        _energy.Add(Math.Clamp(energy, 0f, 100f));

        Trim(_stability);
        Trim(_temperature);
        Trim(_energy);
    }

    public void SetRange(int samples)
    {
        VisibleSamples = Math.Clamp(samples, 8, 120);

        // Смена окна перерисовывает кривые слева направо.
        _drawIn = 0f;
    }

    private static void Trim(List<float> values)
    {
        while (values.Count > 120)
            values.RemoveAt(0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase += args.DeltaSeconds;
        _drawIn = Math.Clamp(_drawIn + args.DeltaSeconds * 3.2f, 0f, 1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var width = Size.X;
        var height = Size.Y;

        if (width <= 0f || height <= 0f)
        {
            handle.SetTransform(oldTransform);
            return;
        }

        var grid = GridLineColor;

        for (var i = 0; i <= 4; i++)
        {
            var y = height * i / 4f;
            handle.DrawLine(new Vector2(0f, y), new Vector2(width, y), grid);
        }

        for (var i = 1; i < 4; i++)
        {
            var x = width * i / 4f;
            handle.DrawLine(new Vector2(x, 0f), new Vector2(x, height), grid.WithAlpha(0.45f));
        }

        var samples = Math.Max(1, Math.Min(VisibleSamples, _stability.Count));

        if (_drawIn < 0.999f)
            samples = Math.Max(1, (int) (samples * _drawIn));
        DrawSeries(handle, _stability, StabilityColor, width, height, samples, 0.10f);
        DrawSeries(handle, _temperature, TemperatureColor, width, height, samples, 0.08f);
        DrawSeries(handle, _energy, EnergyColor, width, height, samples, 0.10f);

        var sweep = (0.5f + 0.5f * MathF.Sin(_phase * 0.7f)) * width;
        handle.DrawLine(new Vector2(sweep, 0f), new Vector2(sweep, height),
            SweepColor.WithAlpha(0.14f));

        handle.SetTransform(oldTransform);
    }

    private void DrawSeries(DrawingHandleScreen handle, List<float> values, Color color,
        float width, float height, int samples, float areaAlpha)
    {
        if (values.Count < 2 || samples < 2)
            return;

        var gain = Math.Clamp(Gain, 0.25f, 4f);
        var start = Math.Max(0, values.Count - samples);
        var count = values.Count - start;
        var step = width / Math.Max(1, count - 1);
        var previous = new Vector2(0f,
            height - Math.Clamp(values[start] * gain, 0f, 100f) / 100f * height);

        for (var i = 1; i < count; i++)
        {
            var current = new Vector2(i * step,
                height - Math.Clamp(values[start + i] * gain, 0f, 100f) / 100f * height);
            handle.DrawLine(previous, current, color.WithAlpha(areaAlpha));
            handle.DrawLine(previous, current, color);
            previous = current;
        }

        handle.DrawCircle(previous, 2.5f, color.WithAlpha(0.35f));
        handle.DrawCircle(previous, 1.4f, color);
    }
}

/// <summary>Крупная анимированная схема ядра РТМ.</summary>
public sealed class ReactorCoreView : UiControl
{
    public bool Running;
    public bool Danger;
    public bool Warning;
    public bool Starting;
    public float Stability = 100f;
    public float Temperature;
    public float Energy;

    private float _phase;
    private Color _displayColor = GoodCoreColor;
    private bool _colorInitialized;

    private static readonly Color GoodCoreColor = Color.FromHex("#43DD85");
    private static readonly Color DangerCoreColor = Color.FromHex("#FF4B55");
    private static readonly Color StartingCoreColor = Color.FromHex("#54A7FF");
    private static readonly Color WarningCoreColor = Color.FromHex("#F2B84B");
    private static readonly Color IdleCoreColor = Color.FromHex("#635354");
    private static readonly Color InsetColor = Color.FromHex("#080505");

    public ReactorCoreView()
    {
        MouseFilter = UiControl.MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var speed = Running ? (Danger ? 3.2f : 1.15f) : 0.35f;
        _phase = (_phase + args.DeltaSeconds * speed) % MathF.Tau;

        var target = GetTargetColor();
        if (!_colorInitialized)
        {
            _displayColor = target;
            _colorInitialized = true;
            return;
        }

        _displayColor = Color.InterpolateBetween(
            _displayColor,
            target,
            Math.Clamp(args.DeltaSeconds * 3.5f, 0f, 1f));
    }

    private Color GetTargetColor()
    {
        if (!Running)
            return IdleCoreColor;

        if (Danger)
            return DangerCoreColor;

        if (Starting)
            return StartingCoreColor;

        if (Warning || Stability < 70f || Temperature >= 500f)
            return WarningCoreColor;

        return GoodCoreColor;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        var baseTransform = Matrix3x2.CreateScale(UIScale) * oldTransform;
        handle.SetTransform(baseTransform);

        var width = Size.X;
        var height = Size.Y;

        if (width <= 0f || height <= 0f)
        {
            handle.SetTransform(oldTransform);
            return;
        }

        var center = new Vector2(width * 0.5f, height * 0.5f);
        var radius = MathF.Min(width * 0.36f, height * 0.42f);

        if (radius <= 4f)
        {
            handle.SetTransform(oldTransform);
            return;
        }

        var color = _displayColor;
        var pulse = 0.5f + 0.5f * MathF.Sin(_phase * 2.4f);
        var activity = Running ? 1f : 0.35f;

        // Мягкое свечение вокруг знака: оно пульсирует, но сам знак остаётся на месте.
        handle.DrawCircle(center, radius * 1.55f,
            color.WithAlpha((0.012f + 0.010f * pulse) * activity));
        handle.DrawCircle(center, radius * 1.32f,
            color.WithAlpha((0.025f + 0.020f * pulse) * activity));
        handle.DrawCircle(center, radius * 1.13f, color.WithAlpha(0.045f * activity));
        handle.DrawCircle(center, radius * 1.05f, color.WithAlpha(0.10f * activity), false);

        // Внешнее кольцо-датчик вращается против знака и прореживается по секторам.
        var outerPhase = -_phase * 0.55f;
        const int tickCount = 24;

        for (var i = 0; i < tickCount; i++)
        {
            var angle = outerPhase + MathF.Tau * i / tickCount;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var bright = (i + (int) (_phase * 2f)) % 6 == 0;

            handle.DrawLine(center + direction * (radius * 1.20f),
                center + direction * (radius * 1.28f),
                color.WithAlpha(bright ? 0.55f : 0.16f));
        }

        // Три широких изогнутых сектора из знака на фотографии.
        const int bladeCount = 3;
        const float bladeStart = -MathF.PI / 2f + 0.12f;

        for (var i = 0; i < bladeCount; i++)
        {
            var start = bladeStart + MathF.Tau * i / bladeCount;
            var end = start + 1.12f;
            DrawBlade(handle, baseTransform, center, radius * 0.46f, radius * 0.97f, start, end, color);
        }

        DrawCenterRing(handle, baseTransform, center, radius, color, pulse, _phase);

        if (Danger)
        {
            var warningPulse = 0.5f + 0.5f * MathF.Sin(_phase * 5f);
            handle.DrawCircle(center, radius * (1.13f + 0.02f * warningPulse),
                color.WithAlpha(0.05f + 0.04f * warningPulse), false);
        }

        handle.SetTransform(oldTransform);
    }

    private static void DrawBlade(DrawingHandleScreen handle, Matrix3x2 baseTransform,
        Vector2 center, float inner, float outer, float start, float end, Color color)
    {
        const int steps = 72;
        var angularStep = (end - start) / steps;
        var halfThickness = MathF.Max(0.9f, (inner + outer) * 0.5f * angularStep * 0.72f);

        for (var i = 0; i < steps; i++)
        {
            var angle = start + (i + 0.5f) * angularStep;
            handle.SetTransform(Matrix3x2.CreateRotation(angle) *
                                Matrix3x2.CreateTranslation(center) *
                                baseTransform);
            handle.DrawRect(new UIBox2(inner, -halfThickness, outer, halfThickness),
                color.WithAlpha(0.94f));
        }

        handle.SetTransform(baseTransform);
    }

    private static void DrawCenterRing(DrawingHandleScreen handle, Matrix3x2 baseTransform,
        Vector2 center, float radius, Color color, float pulse, float phase)
    {
        var inner = radius * 0.245f;
        var outer = radius * 0.335f;

        handle.DrawCircle(center, outer * 1.22f, color.WithAlpha(0.07f + 0.04f * pulse));

        // Сплошное кольцо собирается из тонких окружностей.
        for (var r = inner; r <= outer; r += 1f)
            handle.DrawCircle(center, r, color.WithAlpha(0.92f), false);

        // Восемь радиальных прорезей делят кольцо на сегменты; по одному
        // из них бежит подсветка индикации.
        var gapHalf = MathF.Max(1.1f, radius * 0.014f);
        var activeSegment = (int) (phase * 1.5f) % 8;

        for (var i = 0; i < 8; i++)
        {
            var angle = MathF.Tau * i / 8f + MathF.PI / 8f;
            handle.SetTransform(Matrix3x2.CreateRotation(angle) *
                                Matrix3x2.CreateTranslation(center) *
                                baseTransform);
            handle.DrawRect(new UIBox2(inner * 0.93f, -gapHalf, outer * 1.05f, gapHalf),
                InsetColor);

            if (i == activeSegment)
            {
                handle.DrawRect(new UIBox2(inner + 1f, -gapHalf, outer - 1f, gapHalf),
                    color.WithAlpha(0.45f));
            }
        }

        handle.SetTransform(baseTransform);

        // Светящаяся середина знака.
        handle.DrawCircle(center, radius * 0.20f, color.WithAlpha(0.05f + 0.04f * pulse));
        handle.DrawCircle(center, radius * 0.105f, color);
        handle.DrawCircle(center, radius * 0.048f, InsetColor);
    }
}

/// <summary>Пульсирующая точка состояния в шапке.</summary>
public sealed class PulseDot : UiControl
{
    public Color Color = Color.FromHex("#43DD85");
    public bool Danger;
    public bool Active = true;

    private float _phase;

    private static readonly Color DangerColor = Color.FromHex("#FF6961");

    public PulseDot()
    {
        MouseFilter = UiControl.MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var center = Size / 2f;
        var radius = MathF.Max(2.5f, MathF.Min(Size.X, Size.Y) * 0.27f);
        var color = Danger ? DangerColor : Color;
        var pulse = Active ? 0.50f + 0.50f * (0.5f + 0.5f * MathF.Sin(_phase * 4f)) : 0.25f;

        handle.DrawCircle(center, radius * 2.4f, color.WithAlpha(0.08f * pulse));
        handle.DrawCircle(center, radius * 1.55f, color.WithAlpha(0.16f * pulse));
        handle.DrawCircle(center, radius, color.WithAlpha(0.85f));

        handle.SetTransform(oldTransform);
    }
}

/// <summary>Эмблема «Вселенского союза»: три луча в общей орбите.</summary>
public sealed class UnionEmblem : UiControl
{
    public Color Accent = Color.FromHex("#E23B3B");
    private float _phase;

    private static readonly Color MetalColor = Color.FromHex("#3E2224");

    public UnionEmblem()
    {
        MouseFilter = UiControl.MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase += args.DeltaSeconds * 0.25f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var center = Size / 2f;
        var radius = MathF.Min(Size.X, Size.Y) * 0.35f;
        var metal = MetalColor;

        handle.DrawCircle(center, radius * 1.20f, new Color(0.03f, 0.006f, 0.006f, 0.85f));
        handle.DrawCircle(center, radius * 1.20f, Accent.WithAlpha(0.20f), false);
        handle.DrawCircle(center, radius * 0.86f, metal, false);

        for (var i = 0; i < 3; i++)
        {
            var angle = _phase + MathF.Tau * i / 3f + MathF.PI / 2f;
            var next = angle + MathF.Tau / 3f;
            var start = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * 0.30f);
            var end = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * 0.95f);
            handle.DrawLine(start, end, Accent.WithAlpha(0.90f));

            var side = center + new Vector2(MathF.Cos(next), MathF.Sin(next)) * (radius * 0.95f);
            handle.DrawLine(end, side, Accent.WithAlpha(0.30f));
        }

        handle.DrawCircle(center, radius * 0.18f, Accent);

        handle.SetTransform(oldTransform);
    }
}

/// <summary>
/// Рамка всего экрана: уголковые скобки, засечки по кромкам
/// и медленный блик вдоль верхнего края.
/// </summary>
public sealed class CrtScreenFrame : UiControl
{
    public Color FrameColor = Color.FromHex("#3A1C1F");
    public Color AccentColor = Color.FromHex("#E23B3B");

    private float _phase;

    public CrtScreenFrame()
    {
        MouseFilter = UiControl.MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase = (_phase + args.DeltaSeconds * 0.12f) % 1f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var width = Size.X;
        var height = Size.Y;

        if (width <= 24f || height <= 24f)
        {
            handle.SetTransform(oldTransform);
            return;
        }

        const float inset = 5f;
        const float length = 26f;

        // Четыре уголковые скобки.
        for (var corner = 0; corner < 4; corner++)
        {
            var left = corner is 0 or 2;
            var top = corner is 0 or 1;
            var x = left ? inset : width - inset;
            var y = top ? inset : height - inset;
            var dx = left ? length : -length;
            var dy = top ? length : -length;

            handle.DrawLine(new Vector2(x, y), new Vector2(x + dx, y), FrameColor);
            handle.DrawLine(new Vector2(x, y), new Vector2(x, y + dy), FrameColor);
            handle.DrawRect(new UIBox2(x - 1f, y - 1f, x + 2f, y + 2f), AccentColor.WithAlpha(0.5f));
        }

        // Засечки по верхней и нижней кромке.
        for (var x = 64f; x < width - 48f; x += 48f)
        {
            handle.DrawRect(new UIBox2(x, inset, x + 1f, inset + 4f), FrameColor);
            handle.DrawRect(new UIBox2(x, height - inset - 4f, x + 1f, height - inset), FrameColor);
        }

        // Медленный блик вдоль верхнего края.
        var sweep = width * _phase;
        handle.DrawRect(new UIBox2(sweep, inset, MathF.Min(width - inset, sweep + 90f), inset + 1f),
            AccentColor.WithAlpha(0.30f));

        handle.SetTransform(oldTransform);
    }
}

/// <summary>Декоративные диагональные штрихи в углах панели.</summary>
public sealed class AccentSlashes : UiControl
{
    public Color Color = Color.FromHex("#301A1C");
    public int Count = 4;

    public AccentSlashes()
    {
        MouseFilter = UiControl.MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        var width = Size.X;
        var height = Size.Y;
        var step = MathF.Max(5f, width / (Count + 2));

        for (var i = 0; i < Count; i++)
        {
            var x = width - i * step;
            handle.DrawLine(new Vector2(x, height), new Vector2(x - height * 0.60f, 0f), Color);
        }

        handle.SetTransform(oldTransform);
    }
}
