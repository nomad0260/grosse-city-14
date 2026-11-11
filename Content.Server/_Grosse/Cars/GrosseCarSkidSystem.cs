using System.Numerics;
using Content.Server.Decals;
using Content.Shared._Grosse.Cars;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._Grosse.Cars;

public sealed partial class GrosseCarSkidSystem : EntitySystem
{
    /// <summary>
    /// Half the distance between the two tire tracks. KrAZ body is about 1.65 tiles wide.
    /// </summary>
    private const float TrackHalfWidth = 0.8f;

    /// <summary>
    /// <c>dirtheavy-horizontal-slats</c> lies east-west at angle 0. World angle 0 faces south,
    /// so the streak needs a quarter turn to run along the direction of travel.
    /// </summary>
    private static readonly Angle StreakAlongTravel = Angle.FromDegrees(-90);

    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<GrosseCarComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out _, out var car, out var xform, out var physics))
        {
            if (!car.IsDrifting)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            if ((worldPos - car.LastSkidPosition).Length() < car.DriftDecalSpacing && car.LastSkidPosition != default)
                continue;

            car.LastSkidPosition = worldPos;

            var velocity = physics.LinearVelocity;
            var travel = velocity.LengthSquared() > 0.04f
                ? velocity
                : _transform.GetWorldRotation(xform).ToWorldVec();
            if (travel.LengthSquared() < 0.0001f)
                continue;

            travel = Vector2.Normalize(travel);
            var side = new Vector2(-travel.Y, travel.X);
            var gridAngle = xform.GridUid is { } grid ? _transform.GetWorldRotation(grid) : Angle.Zero;
            var decalAngle = Angle.FromWorldVec(travel) - gridAngle + StreakAlongTravel;
            var origin = Vector2.Transform(car.DriftDecalOffset, _transform.GetWorldMatrix(xform));

            TrySkid(car, WorldToGrid(xform, origin + side * TrackHalfWidth), decalAngle);
            TrySkid(car, WorldToGrid(xform, origin - side * TrackHalfWidth), decalAngle);
        }
    }

    private EntityCoordinates WorldToGrid(TransformComponent xform, Vector2 worldPos)
    {
        if (xform.GridUid is { } grid)
            return new EntityCoordinates(grid, Vector2.Transform(worldPos, _transform.GetInvWorldMatrix(grid)));

        var parent = xform.MapUid ?? xform.ParentUid;
        return new EntityCoordinates(parent, worldPos);
    }

    private void TrySkid(GrosseCarComponent car, EntityCoordinates coords, Angle rotation)
    {
        _decals.TryAddDecal(car.DriftDecal, coords, out _, car.DriftDecalColor, rotation, zIndex: -4, cleanable: true);
    }
}
