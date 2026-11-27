using System;
using System.Numerics;
using Content.Shared._Grosse.Containment;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Grosse.Containment;

/// <summary>
/// Живой визуал ядра РТМ. Базовый постамент виден всегда, остальные слои появляются
/// при работе или пока остановленное ядро ещё раздуто и остывает.
/// Все параметры анимации считаются от реального состояния: расширение раздувает
/// ядро и кольца, температура уводит свечение в оранжевый, падение стабильности
/// ускоряет вращение, сыплет искры и учащает ударную волну.
/// </summary>
public sealed partial class ContainmentCoreVisualizerSystem : EntitySystem
{
    private const int HaloLayer = 1;
    private const int CoronaLayer = 2;
    private const int CoreLayer = 3;
    private const int AccretionLayer = 4;
    private const int OuterAccretionLayer = 5;
    private const int ShockwaveLayer = 6;
    private const int SparkALayer = 7;
    private const int SparkBLayer = 8;

    private static readonly Color HotColor = Color.FromHex("#FFB25E");

    // Цвета колец постоянные: GetRingColor вызывается каждый кадр, и разбор строки
    // на каждом кадре — лишняя работа в горячем пути отрисовки.
    private static readonly Color InitializationRingColor = Color.FromHex("#AFC8FF");
    private static readonly Color NominalRingColor = Color.FromHex("#E4D4FF");
    private static readonly Color OverloadRingColor = Color.FromHex("#FFD59A");
    private static readonly Color ExpandingRingColor = Color.FromHex("#FFAB8C");
    private static readonly Color CompressingRingColor = Color.FromHex("#FF9BC0");
    private static readonly Color FailureRingColor = Color.FromHex("#FFF0F0");

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var time = (float) _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<ContainmentCoreComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var core, out var sprite))
        {
            if (!_sprite.LayerExists((uid, sprite), CoreLayer))
                continue;

            // Остановленное, но ещё горячее ядро остаётся видимым: оно раздуто и фонит,
            // пока его не остудили, поэтому гасить картинку вместе со стержнями нельзя.
            var visible = core.IsRunning || core.Expansion > 1f;

            for (var layer = HaloLayer; layer <= SparkBLayer; layer++)
            {
                if (_sprite.LayerExists((uid, sprite), layer))
                    _sprite.LayerSetVisible((uid, sprite), layer, visible);
            }

            if (!visible)
                continue;

            var expansion = Math.Clamp(core.Expansion / 200f, 0f, 1f);
            var heat = Math.Clamp((core.Temperature - 293f) / 500f, 0f, 1f);
            var stress = core.IsRunning ? Math.Clamp(1f - core.Stability / 100f, 0f, 1f) : 0.65f;
            var danger = core.State is ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown;

            var ringColor = GetRingColor(core.State);
            var glowColor = Color.InterpolateBetween(ringColor, HotColor, heat * 0.5f);

            var (baseScale, pulse, speed) = core.State switch
            {
                ContainmentCoreState.Initialization => (0.75f, 0.02f, 2f),
                ContainmentCoreState.Nominal => (1.0f, 0.02f, 2f),
                ContainmentCoreState.Overload or ContainmentCoreState.Unstable => (1.12f, 0.05f, 4f),
                ContainmentCoreState.Expanding => (1.4f, 0.08f, 5f),
                ContainmentCoreState.Compressing => (0.8f, 0.07f, 8f),
                ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown => (1.55f, 0.1f, 10f),
                _ => (1.0f, 0.02f, 2f),
            };

            // Сама анимация ядра не перекрашивается: её исходные цвета сохраняются,
            // а тепло и состояние читаются по ореолу, короне и кольцам.
            var swell = 1f + 0.18f * expansion;
            var beat = 1f + pulse * MathF.Sin(time * speed);
            var coreScale = baseScale * swell * beat;
            _sprite.LayerSetScale((uid, sprite), CoreLayer, new Vector2(coreScale, coreScale));

            var (ringBase, ringPulse, ringSpeed) = core.State switch
            {
                ContainmentCoreState.Initialization => (0.85f, 0.015f, 1.5f),
                ContainmentCoreState.Nominal => (1.0f, 0.02f, 2f),
                ContainmentCoreState.Overload or ContainmentCoreState.Unstable => (1.08f, 0.04f, 3.5f),
                ContainmentCoreState.Expanding => (1.2f, 0.06f, 4.5f),
                ContainmentCoreState.Compressing => (0.9f, 0.05f, 7f),
                ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown => (1.3f, 0.08f, 9f),
                _ => (1.0f, 0.02f, 2f),
            };

            var spinSpeed = core.State switch
            {
                ContainmentCoreState.Initialization => 8f,
                ContainmentCoreState.Nominal => 12f,
                ContainmentCoreState.Overload or ContainmentCoreState.Unstable => 25f,
                ContainmentCoreState.Expanding => 40f,
                ContainmentCoreState.Compressing => -60f,
                ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown => 90f,
                _ => 12f,
            };

            var ringScale = ringBase *
                            (1f + 0.10f * expansion) *
                            (1f + ringPulse * MathF.Sin(time * ringSpeed));

            _sprite.LayerSetColor((uid, sprite), AccretionLayer, ringColor);
            _sprite.LayerSetRotation((uid, sprite), AccretionLayer, Angle.FromDegrees(time * spinSpeed % 360f));
            _sprite.LayerSetScale((uid, sprite), AccretionLayer, new Vector2(ringScale, ringScale));

            var outerScale = ringScale * 1.13f;
            _sprite.LayerSetColor((uid, sprite), OuterAccretionLayer, ringColor.WithAlpha(0.32f + 0.18f * stress));
            _sprite.LayerSetRotation((uid, sprite), OuterAccretionLayer,
                Angle.FromDegrees(-(time * spinSpeed * 0.55f) % 360f));
            _sprite.LayerSetScale((uid, sprite), OuterAccretionLayer, new Vector2(outerScale, outerScale));

            var haloAlpha = Math.Clamp(
                0.30f + 0.10f * MathF.Sin(time * speed * 0.5f) + heat * 0.15f + (danger ? 0.18f : 0f),
                0f,
                0.8f);

            _sprite.LayerSetColor((uid, sprite), HaloLayer, glowColor.WithAlpha(haloAlpha));
            var haloScale = ringScale * (0.92f + 0.08f * expansion);
            _sprite.LayerSetScale((uid, sprite), HaloLayer, new Vector2(haloScale, haloScale));

            var coronaAlpha = Math.Clamp(0.12f + 0.24f * stress + heat * 0.08f, 0f, 0.55f);
            _sprite.LayerSetColor((uid, sprite), CoronaLayer, glowColor.WithAlpha(coronaAlpha));
            _sprite.LayerSetRotation((uid, sprite), CoronaLayer,
                Angle.FromDegrees(-(time * (5f + 24f * stress)) % 360f));
            var coronaScale = ringScale * (1.0f + 0.04f * expansion);
            _sprite.LayerSetScale((uid, sprite), CoronaLayer, new Vector2(coronaScale, coronaScale));

            if (core.IsRunning)
            {
                var cycle = MathF.Max(1.1f, 3.4f - 2.4f * stress);
                var phase = time % cycle / cycle;
                var waveScale = ringScale * (0.75f + 1.6f * phase);
                var waveAlpha = (1f - phase) * (1f - phase) * (0.45f + 0.35f * stress);

                _sprite.LayerSetColor((uid, sprite), ShockwaveLayer, glowColor.WithAlpha(waveAlpha));
                _sprite.LayerSetScale((uid, sprite), ShockwaveLayer, new Vector2(waveScale, waveScale));
            }

            var sparkVisible = core.IsRunning && (stress > 0.15f || danger);

            if (_sprite.LayerExists((uid, sprite), SparkALayer))
                _sprite.LayerSetVisible((uid, sprite), SparkALayer, sparkVisible);

            if (_sprite.LayerExists((uid, sprite), SparkBLayer))
                _sprite.LayerSetVisible((uid, sprite), SparkBLayer, sparkVisible);

            if (sparkVisible)
            {
                var sparkScale = 1.1f + 0.5f * stress;
                var sparkRadius = 2.0f * ringScale;
                var sparkAngle = time * (0.7f + 2.2f * stress);
                var sparkColor = glowColor.WithAlpha(0.55f + 0.45f * stress);

                var first = new Vector2(MathF.Cos(sparkAngle), MathF.Sin(sparkAngle)) * sparkRadius;
                var second = new Vector2(MathF.Cos(sparkAngle + MathF.PI), MathF.Sin(sparkAngle + MathF.PI)) * sparkRadius;

                _sprite.LayerSetColor((uid, sprite), SparkALayer, sparkColor);
                _sprite.LayerSetScale((uid, sprite), SparkALayer, new Vector2(sparkScale, sparkScale));
                _sprite.LayerSetOffset((uid, sprite), SparkALayer, first);

                _sprite.LayerSetColor((uid, sprite), SparkBLayer, sparkColor.WithAlpha(sparkColor.A * 0.7f));
                _sprite.LayerSetScale((uid, sprite), SparkBLayer, new Vector2(sparkScale * 0.8f, sparkScale * 0.8f));
                _sprite.LayerSetOffset((uid, sprite), SparkBLayer, second);
            }

            _sprite.LayerSetVisible((uid, sprite), ShockwaveLayer, core.IsRunning);

            // Свет пульсирует вместе с ядром: сервер задаёт цвет и радиус по состоянию,
            // локальная яркость остаётся клиентской и не уходит в сеть.
            if (core.IsRunning && TryComp<PointLightComponent>(uid, out var light))
                _light.SetEnergy(uid, 6.5f + 2.2f * beat + heat * 3f, light);
        }
    }

    private static Color GetRingColor(ContainmentCoreState state)
    {
        return state switch
        {
            ContainmentCoreState.Initialization => InitializationRingColor,
            ContainmentCoreState.Nominal => NominalRingColor,
            ContainmentCoreState.Overload or ContainmentCoreState.Unstable => OverloadRingColor,
            ContainmentCoreState.Expanding => ExpandingRingColor,
            ContainmentCoreState.Compressing => CompressingRingColor,
            ContainmentCoreState.ContainmentFailure or ContainmentCoreState.Meltdown => FailureRingColor,
            _ => Color.White,
        };
    }
}
