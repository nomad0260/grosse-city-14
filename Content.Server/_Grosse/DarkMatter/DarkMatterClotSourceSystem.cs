using System;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.DarkMatter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server._Grosse.DarkMatter;

/// <summary>
/// Produces physical clots from a stable running reactor. The old separate collector machine is
/// intentionally gone: the reactor itself is the source, and the matrix consumes the item.
/// </summary>
public sealed partial class DarkMatterClotSourceSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<DarkMatterClotSourceComponent, ContainmentCoreComponent>();

        while (query.MoveNext(out var uid, out var source, out var core))
        {
            if (!core.IsRunning || core.Stability < 60f)
            {
                source.Accumulator = 0f;
                continue;
            }

            source.Accumulator += frameTime;

            if (source.Accumulator < source.Interval)
                continue;

            source.Accumulator = 0f;

            if (CountNearby(uid, source.MaxNearby))
                continue;

            SpawnClot(uid, core);
        }
    }

    private bool CountNearby(EntityUid source, int max)
    {
        if (max <= 0)
            return false;

        var origin = _transform.GetMapCoordinates(source);
        var count = 0;
        var query = EntityQueryEnumerator<DarkMatterClotComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == source)
                continue;

            var position = _transform.GetMapCoordinates(uid, xform);

            if (position.MapId != origin.MapId)
                continue;

            if ((position.Position - origin.Position).LengthSquared() > 16f)
                continue;

            count++;

            if (count >= max)
                return true;
        }

        return false;
    }

    private void SpawnClot(EntityUid reactor, ContainmentCoreComponent core)
    {
        var clot = Spawn("DarkMatterClot", Transform(reactor).Coordinates);

        if (!TryComp<DarkMatterClotComponent>(clot, out var component))
            return;

        component.Clot = new DarkMatterClotData
        {
            Name = "Сгусток тёмной материи",
            CoreState = core.State.ToString(),
            Stability = Math.Clamp(core.Stability, 5f, 100f),
            Strength = Math.Clamp(0.6f + core.EnergyOutput / 1000f, 0.5f, 5f),
            Density = Math.Clamp(core.Density / 100f, 0.1f, 5f),
            EnergyCapacity = Math.Clamp(80f + core.EnergyOutput * 0.5f + core.Stability, 10f, 2000f),
            RadiationActivity = Math.Clamp(core.Radiation / 8f, 0f, 20f),
            ThermalResistance = Math.Clamp(1f + core.Temperature / 500f, 0.5f, 5f),
            Degradation = Math.Clamp(0.2f - core.Stability * 0.001f, 0.01f, 0.5f),
            Efficiency = Math.Clamp(0.6f + core.Stability / 200f, 0.5f, 2f),
            Purity = Math.Clamp(0.5f + core.ContainmentIntegrity / 200f, 0.1f, 1f),
            Potency = Math.Clamp(0.5f + core.Stability / 100f, 0.5f, 3f),
        };
    }
}
