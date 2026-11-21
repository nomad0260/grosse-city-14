using System;
using Content.Shared._Grosse.Cooling;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Тепловая инерция оборудования реактора. Температура меняется постепенно, перегрев снижает эффективность.
/// </summary>
public sealed partial class ThermalEquipmentSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;
    private const float Interval = 0.5f;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(Interval);

        var query = EntityQueryEnumerator<ThermalEquipmentComponent>();

        while (query.MoveNext(out _, out var comp))
        {
            var heat = comp.HeatGeneration * Math.Clamp(comp.Load, 0f, 1f);
            var passive = Math.Max(0f, comp.Temperature - comp.AmbientTemperature) * comp.PassiveLoss;
            var delta = (heat - comp.HeatRemoved - passive) / Math.Max(1f, comp.ThermalMass) * Interval;

            comp.Temperature = Math.Clamp(comp.Temperature + delta, 1f, 2000f);
            comp.Efficiency = GetEfficiency(comp);
            comp.Overheated = comp.Temperature >= comp.MaxTemperature * 0.95f;

            // Если сеть перестала обновлять теплоотвод, он быстро затухает.
            comp.HeatRemoved *= 0.85f;
        }
    }

    public float GetEfficiency(EntityUid uid)
    {
        return TryComp<ThermalEquipmentComponent>(uid, out var comp) ? GetEfficiency(comp) : 1f;
    }

    public static float GetEfficiency(ThermalEquipmentComponent comp)
    {
        if (comp.Temperature <= comp.NominalTemperature)
            return 1f;

        var range = Math.Max(1f, comp.MaxTemperature - comp.NominalTemperature);
        return Math.Clamp((comp.MaxTemperature - comp.Temperature) / range, 0.05f, 1f);
    }
}
