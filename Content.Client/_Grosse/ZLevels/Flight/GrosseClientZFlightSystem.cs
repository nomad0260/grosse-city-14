/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using System.Numerics;
using Content.Client._Grosse.ZLevels.Core;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Content.Shared._Grosse.ZLevels.Flight;
using Content.Shared._Grosse.ZLevels.Flight.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Grosse.ZLevels.Flight;

public sealed partial class GrosseClientZFlightSystem : GrosseSharedZFlightSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GrosseZFlyerComponent, GrosseZPhysicsComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var flyer, out var zPhys, out var xform, out var sprite))
        {
            if (!flyer.Active)
                continue;
            if (_timing.CurTime < flyer.NextVfx)
                continue;
            flyer.NextVfx = _timing.CurTime + TimeSpan.FromSeconds(0.2f);

            if (flyer.FlightVfx is not null)
            {
                var vfx = SpawnAtPosition(flyer.FlightVfx, xform.Coordinates);
                _sprite.SetOffset(vfx, new Vector2(0, zPhys.LocalPosition * GrosseClientZLevelsSystem.ZLevelOffset) + zPhys.SpriteOffsetDefault);
            }
        }
    }
}
