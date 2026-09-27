using Content.Shared._Grosse.Cars;
using Content.Shared.Interaction;
using Content.Shared.Vehicle.Components;

namespace Content.Shared.MouseRotator;

/// <summary>
/// This handles rotating an entity based on mouse location
/// </summary>
/// <see cref="MouseRotatorComponent"/>
public abstract partial class SharedMouseRotatorSystem : EntitySystem
{
    [Dependency] private RotateToFaceSystem _rotate = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<RequestMouseRotatorRotationEvent>(OnRequestRotation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // TODO maybe `ActiveMouseRotatorComponent` to avoid querying over more entities than we need?
        // (if this is added to players)
        // (but arch makes these fast anyway, so)
        var query = EntityQueryEnumerator<MouseRotatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var rotator, out var xform))
        {
            if (rotator.GoalRotation == null)
                continue;

            if (_rotate.TryRotateTo(
                    uid,
                    rotator.GoalRotation.Value,
                    frameTime,
                    rotator.AngleTolerance,
                    MathHelper.DegreesToRadians(rotator.RotationSpeed),
                    xform))
            {
                // Stop rotating if we finished
                rotator.GoalRotation = null;
                Dirty(uid, rotator);
            }
        }
    }

    private void OnRequestRotation(RequestMouseRotatorRotationEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } attached)
            return;

        var target = msg.User is { } userNet ? GetEntity(userNet) : attached;

        // Allow the attached player, a vehicle operator, or the tank's gunner (or the driver, if that seat is empty).
        if (attached != target)
        {
            if (TryComp<GrosseCarTurretComponent>(target, out var turret) &&
                turret.Hull is { } hull &&
                TryComp<GrosseCarJointComponent>(hull, out var joint))
            {
                if (!EntityManager.System<SharedGrosseCarJointSystem>().CanAim(attached, (hull, joint)))
                    return;
            }
            else if (!TryComp<VehicleOperatorComponent>(attached, out var op) || op.Vehicle != target)
            {
                return;
            }
        }

        if (!TryComp<MouseRotatorComponent>(target, out var rotator))
        {
            if (attached == target)
            {
                Log.Error($"User {args.SenderSession.Name} ({args.SenderSession.UserId}) tried setting local rotation directly without a valid mouse rotator component attached!");
            }

            return;
        }

        var ev = new MouseRotatorRotationEvent(msg.Rotation);
        RaiseLocalEvent(target, ref ev);

        rotator.GoalRotation = ev.Rotation;
        Dirty(target, rotator);
    }
}
