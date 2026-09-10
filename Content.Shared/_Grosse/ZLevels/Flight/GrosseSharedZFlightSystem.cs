/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared._Grosse.ZLevels.Core.Components;
using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Content.Shared._Grosse.ZLevels.Flight.Components;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Analyzers;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.ZLevels.Flight;

public abstract partial class GrosseSharedZFlightSystem : EntitySystem
{
    [Dependency] private GrosseSharedZLevelsSystem _zLevel = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;

    protected EntityQuery<GrosseZPhysicsComponent> ZPhyzQuery;

    public override void Initialize()
    {
        base.Initialize();
        InitializeControllable();
        SubscribeLocalEvent<GrosseZFlyerComponent, GrosseZLevelChasmAttempt>(OnFlightChasmAttempt);
        SubscribeLocalEvent<GrosseZFlyerComponent, IsWeightlessEvent>(CheckWeightless);
        SubscribeLocalEvent<GrosseZFlyerComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<GrosseZFlyerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GrosseZFlyerComponent, KnockedDownEvent>(OnKnockDowned);
        SubscribeLocalEvent<GrosseZFlyerComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<GrosseZPhysicsComponent, GrosseFlightStartedEvent>(OnStartFlight);
        SubscribeLocalEvent<GrosseZPhysicsComponent, GrosseFlightStoppedEvent>(OnStopFlight);
        SubscribeLocalEvent<GrosseZFlyerComponent, GrosseGetZVelocityEvent>(OnGetZVelocity);
        SubscribeLocalEvent<GrosseZFlyerComponent, GrosseCheckGravityEvent>(OnGetGravity);

        ZPhyzQuery = GetEntityQuery<GrosseZPhysicsComponent>();
    }
    private void OnFlightChasmAttempt(Entity<GrosseZFlyerComponent> ent, ref GrosseZLevelChasmAttempt args)
    {
        if (!ent.Comp.Active || args.Cancelled)
            return;

        args.Cancel();

        if (!ZPhyzQuery.TryComp(ent.Owner, out var zPhys))
            return;

        _zLevel.SetZPosition((ent.Owner, zPhys), 0f);

        if (zPhys.Velocity < 0)
            _zLevel.SetZVelocity((ent.Owner, zPhys), 0f);
    }
    private void CheckWeightless(Entity<GrosseZFlyerComponent> ent, ref IsWeightlessEvent args)
    {
        if (!ent.Comp.Active || args.Handled)
            return;

        args.IsWeightless = true;
        args.Handled = true;
    }
    private void OnDamageDealt(Entity<GrosseZFlyerComponent> ent, ref DamageDealtEvent args)
    {
        if (!args.InterruptsDoAfters)
            return;

        var damageIncreased = false;
        foreach (var amount in args.Damage.DamageDict.Values)
        {
            if (amount <= 0)
                continue;

            damageIncreased = true;
            break;
        }

        if (!damageIncreased)
            return;

        DeactivateFlight((ent, ent));
    }
    private void OnMobStateChanged(Entity<GrosseZFlyerComponent> ent, ref MobStateChangedEvent args)
    {
        DeactivateFlight((ent, ent));
    }
    private void OnKnockDowned(Entity<GrosseZFlyerComponent> ent, ref KnockedDownEvent args)
    {
        DeactivateFlight((ent, ent));
    }
    private void OnStunned(Entity<GrosseZFlyerComponent> ent, ref StunnedEvent args)
    {
        DeactivateFlight((ent, ent));
    }
    private void OnStartFlight(Entity<GrosseZPhysicsComponent> ent, ref GrosseFlightStartedEvent args)
    {
        SetTargetHeight(ent.Owner, ent.Comp.CurrentZLevel);
        StartFlightVisuals(ent.Owner);
    }
    private void OnStopFlight(Entity<GrosseZPhysicsComponent> ent, ref GrosseFlightStoppedEvent args)
    {
        StopFlightVisuals(ent.Owner);
    }
    private void OnGetZVelocity(Entity<GrosseZFlyerComponent> ent, ref GrosseGetZVelocityEvent args)
    {
        if (!ent.Comp.Active)
            return;

        var zPhys = args.Target.Comp;
        var currentPos = zPhys.CurrentZLevel + zPhys.LocalPosition;
        var targetPos = ent.Comp.TargetMapHeight + 0.2f;
        var currentVelocity = zPhys.Velocity;

        var distanceToTarget = targetPos - currentPos;

        var targetVelocity = Math.Clamp(distanceToTarget * ent.Comp.FlightSpeed, -ent.Comp.FlightSpeed, ent.Comp.FlightSpeed);
        var velocityDelta = targetVelocity - currentVelocity;

        var upperBound = ent.Comp.TargetMapHeight + 0.9f;
        var lowerBound = ent.Comp.TargetMapHeight + 0.1f;

        var newVelocity = currentVelocity + velocityDelta;
        var nextPos = currentPos + newVelocity;

        if (nextPos > upperBound)
        {
            var maxAllowedVelocity = upperBound - currentPos;
            velocityDelta = maxAllowedVelocity - currentVelocity;
        }
        else if (nextPos < lowerBound)
        {
            var maxAllowedVelocity = lowerBound - currentPos;
            velocityDelta = maxAllowedVelocity - currentVelocity;
        }

        args.VelocityDelta = velocityDelta;
    }
    private void OnGetGravity(Entity<GrosseZFlyerComponent> ent, ref GrosseCheckGravityEvent args)
    {
        if (ent.Comp.Active)
            args.Gravity *= 0;
    }

    [PublicAPI]
    public bool TryActivateFlight(Entity<GrosseZFlyerComponent?> ent, GrosseZPhysicsComponent? zPhys = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!Resolve(ent, ref zPhys, false))
            return false;

        if (ent.Comp.Active)
            return false;

        var ev = new GrosseStartFlightAttemptEvent();
        RaiseLocalEvent(ent, ev);

        if (ev.Cancelled)
            return false;

        ent.Comp.Active = true;
        DirtyField(ent, ent.Comp, nameof(GrosseZFlyerComponent.Active));

        zPhys.VelocityRaiseEvent = true;

        _zLevel.UpdateGravityState((ent, zPhys));
        _zLevel.WakeBody((ent, zPhys));
        _gravity.RefreshWeightless(ent.Owner);

        RaiseLocalEvent(ent, new GrosseFlightStartedEvent());
        return true;
    }

    [PublicAPI]
    public void DeactivateFlight(Entity<GrosseZFlyerComponent?> ent, GrosseZPhysicsComponent? zPhys = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!Resolve(ent, ref zPhys, false))
            return;

        if (!ent.Comp.Active)
            return;

        ent.Comp.Active = false;
        DirtyField(ent, ent.Comp, nameof(GrosseZFlyerComponent.Active));

        zPhys.VelocityRaiseEvent = false;

        _zLevel.UpdateGravityState((ent, zPhys));
        _gravity.RefreshWeightless(ent.Owner);

        RaiseLocalEvent(ent, new GrosseFlightStoppedEvent());
    }

    [PublicAPI]
    public void SetTargetHeight(Entity<GrosseZFlyerComponent?> ent, int targetHeight)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.TargetMapHeight = targetHeight;
        DirtyField(ent, ent.Comp, nameof(GrosseZFlyerComponent.TargetMapHeight));
    }

    private void StartFlightVisuals(Entity<GrosseZFlyerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        _appearance.SetData(ent, GrosseFlightVisuals.Active, true);
        _ambient.SetAmbience(ent, true);
    }

    private void StopFlightVisuals(Entity<GrosseZFlyerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        _appearance.SetData(ent, GrosseFlightVisuals.Active, false);
        _ambient.SetAmbience(ent, false);
    }
}

/// <summary>
/// Called on an entity when it attempts to start flight mode. Subscribe and cancel this event if you want to cancel your flight for any reason.
/// </summary>
public sealed partial class GrosseStartFlightAttemptEvent : CancellableEntityEventArgs;

/// <summary>
/// Called on an entity when it enters flight mode
/// </summary>
public sealed partial class GrosseFlightStartedEvent : EntityEventArgs;

/// <summary>
/// Called on an entity when it exits flight mode
/// </summary>
public sealed partial class GrosseFlightStoppedEvent : EntityEventArgs;


/// <summary>
/// Instant Action, raising the target flight level by 1
/// </summary>
public sealed partial class GrosseZFlightActionUp : InstantActionEvent
{
}

/// <summary>
/// Instant Action, lowering the target flight level by 1
/// </summary>
public sealed partial class GrosseZFlightActionDown : InstantActionEvent
{
}


[Serializable, NetSerializable]
public enum GrosseFlightVisuals
{
    Active,
}

/// <summary>
/// DoAfter event for starting flight with a delay
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GrosseStartFlightDoAfterEvent : SimpleDoAfterEvent
{
}
