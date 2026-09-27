using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Grosse.Cars;

public sealed partial class SharedGrosseCarJointSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedProjectileSystem _projectile = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrosseCarJointComponent, GetVerbsEvent<InteractionVerb>>(OnInteractionVerbs);
        SubscribeLocalEvent<GrosseCarJointComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerbs);
        SubscribeLocalEvent<GrosseCarJointComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<GrosseCarJointComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<GrosseCarJointComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GrosseCarJointComponent, GrosseCarExitEvent>(OnGunnerExit, before: new[] { typeof(SharedGrosseCarSystem) });
        SubscribeLocalEvent<GrosseCarJointComponent, GrosseTankReloadEvent>(OnReloadAction);
        SubscribeLocalEvent<GrosseCarJointComponent, GrosseTankGunnerEnterDoAfterEvent>(OnGunnerEnterDoAfter);
        SubscribeLocalEvent<GrosseCarJointComponent, GrosseTankGunnerEjectDoAfterEvent>(OnGunnerEjectDoAfter);
        SubscribeLocalEvent<GrosseCarJointComponent, GrosseTankReloadDoAfterEvent>(OnReloadDoAfter);
        SubscribeLocalEvent<GrosseCarJointComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<GrosseCarJointComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<GrosseCarJointComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<GrosseCarJointComponent, ComponentStartup>(OnHullStartup);
        SubscribeLocalEvent<GrosseCarJointComponent, EntInsertedIntoContainerMessage>(OnHullInserted);
        SubscribeLocalEvent<GrosseCarJointComponent, EntRemovedFromContainerMessage>(OnHullRemoved);

        SubscribeLocalEvent<GrosseCarTurretComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<GrosseCarTurretComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<GrosseCarTurretComponent, EntInsertedIntoContainerMessage>(OnTurretInserted);
        SubscribeLocalEvent<GrosseCarTurretComponent, EntRemovedFromContainerMessage>(OnTurretRemoved);
    }

    public bool IsWrecked(EntityUid hull)
    {
        if (!TryComp<GrosseCarComponent>(hull, out var car))
            return false;

        return _damageable.GetTotalDamage(hull).Float() >= car.MaxDriveDamage;
    }

    public bool IsGunnerSeated(Entity<GrosseCarJointComponent> hull)
    {
        return hull.Comp.Turret is { } turret
               && _container.TryGetContainer(turret, hull.Comp.GunnerContainer, out var container)
               && container.ContainedEntities.Count > 0;
    }

    public bool CanAim(EntityUid user, Entity<GrosseCarJointComponent> hull)
    {
        if (!TryComp<GrosseCarRiderComponent>(user, out var rider) || rider.Car != hull.Owner)
            return false;

        if (rider.ControlsTurret)
            return true;

        return rider.IsDriver && !IsGunnerSeated(hull);
    }

    public bool TryEnterGunner(EntityUid user, Entity<GrosseCarJointComponent> hull, bool skipDelay)
    {
        if (hull.Comp.Turret is not { } turret || TerminatingOrDeleted(turret))
            return false;

        if (IsGunnerSeated(hull) || !CanBecomeGunner(user))
            return false;

        if (!skipDelay)
        {
            var args = new DoAfterArgs(EntityManager, user, hull.Comp.EntryDelay, new GrosseTankGunnerEnterDoAfterEvent(), hull, user, hull)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false,
            };
            return _doAfter.TryStartDoAfter(args);
        }

        var container = _container.EnsureContainer<ContainerSlot>(turret, hull.Comp.GunnerContainer);
        return _container.Insert(user, container);
    }

    public bool TryEjectGunner(Entity<GrosseCarJointComponent> hull, EntityUid occupant, EntityUid user, bool skipDelay)
    {
        if (hull.Comp.Turret is not { } turret)
            return false;

        if (!_container.TryGetContainer(turret, hull.Comp.GunnerContainer, out var container) ||
            !container.Contains(occupant))
            return false;

        if (!skipDelay)
        {
            var args = new DoAfterArgs(EntityManager, user, hull.Comp.EntryDelay, new GrosseTankGunnerEjectDoAfterEvent(), hull, occupant, hull)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false,
            };
            return _doAfter.TryStartDoAfter(args);
        }

        return _container.Remove(occupant, container, destination: _transform.GetMoverCoordinates(hull));
    }

    public void UpdateAppearance(Entity<GrosseCarJointComponent> hull)
    {
        var wrecked = IsWrecked(hull);
        _appearance.SetData(hull, GrosseTankVisuals.Wrecked, wrecked);

        if (hull.Comp.Turret is not { } turret || TerminatingOrDeleted(turret))
            return;

        _appearance.SetData(turret, GrosseTankVisuals.Gunner, !wrecked && IsGunnerSeated(hull));
        _appearance.SetData(turret, GrosseTankVisuals.Chambered, !wrecked && IsChambered(turret));
        _appearance.SetData(turret, GrosseTankVisuals.Ammo, wrecked ? GrosseTankAmmoVisual.Empty : AmmoVisual(hull, turret));
        _appearance.SetData(turret, GrosseTankVisuals.Wrecked, wrecked);
    }

    private void OnHullStartup(Entity<GrosseCarJointComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnInteractionVerbs(Entity<GrosseCarJointComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Turret is not { } turret || TerminatingOrDeleted(turret))
            return;

        if (IsGunnerSeated(ent) || !CanBecomeGunner(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("grosse-tank-verb-gunner"),
            Category = VerbCategory.Enter,
            Act = () => TryEnterGunner(user, ent, skipDelay: false),
        });
    }

    private void OnAlternativeVerbs(Entity<GrosseCarJointComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Turret is not { } turret)
            return;

        if (!_container.TryGetContainer(turret, ent.Comp.GunnerContainer, out var container))
            return;

        var user = args.User;
        foreach (var occupant in container.ContainedEntities)
        {
            if (occupant == user)
                continue;

            var target = occupant;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("grosse-car-verb-eject", ("target", target)),
                Priority = -2,
                Act = () => TryEjectGunner(ent, target, user, skipDelay: false),
            });
        }
    }

    private void OnVerbs(Entity<GrosseCarJointComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<GrosseCarRiderComponent>(args.User, out var rider) || !rider.ControlsTurret || rider.Car != ent.Owner)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("grosse-car-verb-exit"),
            Priority = 8,
            Act = () => TryEjectGunner(ent, user, user, skipDelay: true),
        });
    }

    private void OnGunnerExit(Entity<GrosseCarJointComponent> ent, ref GrosseCarExitEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<GrosseCarRiderComponent>(args.Performer, out var rider) || !rider.ControlsTurret || rider.Car != ent.Owner)
            return;

        args.Handled = TryEjectGunner(ent, args.Performer, args.Performer, skipDelay: true);
    }

    private void OnGunnerEnterDoAfter(Entity<GrosseCarJointComponent> ent, ref GrosseTankGunnerEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = TryEnterGunner(args.User, ent, skipDelay: true);
    }

    private void OnGunnerEjectDoAfter(Entity<GrosseCarJointComponent> ent, ref GrosseTankGunnerEjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var occupant = args.Target ?? args.User;
        args.Handled = TryEjectGunner(ent, occupant, args.User, skipDelay: true);
    }

    private void OnInteractUsing(Entity<GrosseCarJointComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Turret is not { } turret || TerminatingOrDeleted(turret))
            return;

        if (_tag.HasTag(args.Used, ent.Comp.AmmoTag))
        {
            if (!TryInsertAmmo(ent, turret, args.Used, args.User))
                return;

            args.Handled = true;
            return;
        }

        if (!HasComp<BallisticAmmoProviderComponent>(args.Used))
            return;

        var boxAmmo = new GetAmmoCountEvent();
        RaiseLocalEvent(args.Used, ref boxAmmo);
        if (boxAmmo.Count <= 0)
            return;

        if (AmmoCount(turret, ent.Comp.AmmoContainer) >= ent.Comp.AmmoCapacity)
        {
            _popup.PopupClient(Loc.GetString("grosse-tank-ammo-full"), ent, args.User);
            args.Handled = true;
            return;
        }

        var taken = new List<(EntityUid? Entity, IShootable Shootable)>();
        RaiseLocalEvent(args.Used, new TakeAmmoEvent(1, taken, Transform(args.Used).Coordinates, args.User));
        if (taken.Count == 0 || taken[0].Entity is not { } shell || !_tag.HasTag(shell, ent.Comp.AmmoTag))
            return;

        if (!TryInsertAmmo(ent, turret, shell, args.User))
            return;

        args.Handled = true;
    }

    private void OnExamine(Entity<GrosseCarJointComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Turret is not { } turret)
            return;

        var count = AmmoCount(turret, ent.Comp.AmmoContainer);
        args.PushMarkup(Loc.GetString("grosse-tank-examine-ammo", ("count", count), ("capacity", ent.Comp.AmmoCapacity)));
        args.PushMarkup(Loc.GetString(IsChambered(turret) ? "grosse-tank-examine-chambered" : "grosse-tank-examine-empty"));
    }

    private void OnReloadAction(Entity<GrosseCarJointComponent> ent, ref GrosseTankReloadEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryGetLoader(ent, out var seat, out var loader) || loader != args.Performer)
        {
            _popup.PopupClient(Loc.GetString("grosse-tank-gunner-busy"), ent, args.Performer);
            return;
        }

        if (IsWrecked(ent))
        {
            _popup.PopupClient(Loc.GetString("grosse-tank-wrecked"), ent, args.Performer);
            return;
        }

        if (ent.Comp.Turret is not { } turret || IsChambered(turret))
            return;

        if (AmmoCount(turret, ent.Comp.AmmoContainer) <= 0)
        {
            _popup.PopupClient(Loc.GetString("grosse-tank-ammo-empty"), ent, args.Performer);
            return;
        }

        // The loader is inside the turret, so a range check against the hull always fails. The driver sits in the hull and passes it.
        var doAfter = new DoAfterArgs(EntityManager, args.Performer, seat.ReloadDelay, new GrosseTankReloadDoAfterEvent(), ent, args.Performer)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnReloadDoAfter(Entity<GrosseCarJointComponent> ent, ref GrosseTankReloadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (IsWrecked(ent) || ent.Comp.Turret is not { } turret || IsChambered(turret))
            return;

        if (!_container.TryGetContainer(turret, ent.Comp.AmmoContainer, out var rack) || rack.ContainedEntities.Count == 0)
            return;

        var shell = rack.ContainedEntities[0];
        if (!TryComp<BallisticAmmoProviderComponent>(turret, out var breech))
            return;

        if (_gun.TryBallisticInsert((turret, breech), shell, args.User))
            UpdateAppearance(ent);
    }

    private void OnDamaged(Entity<GrosseCarJointComponent> ent, ref DamageChangedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnDestroyed(Entity<GrosseCarJointComponent> ent, ref DestructionEventArgs args)
    {
        EjectGunner(ent);
    }

    private void OnTerminating(Entity<GrosseCarJointComponent> ent, ref EntityTerminatingEvent args)
    {
        EjectGunner(ent);
    }

    private void EjectGunner(Entity<GrosseCarJointComponent> ent)
    {
        if (ent.Comp.Turret is not { } turret || Deleted(turret))
            return;

        // Map cleanup deletes the grid first. Dropping the gunner onto that grid throws.
        var parent = Transform(ent).ParentUid;
        if (!parent.IsValid() || TerminatingOrDeleted(parent))
            return;

        if (!_container.TryGetContainer(turret, ent.Comp.GunnerContainer, out var container))
            return;

        foreach (var occupant in container.ContainedEntities.ToArray())
            _container.Remove(occupant, container, destination: _transform.GetMoverCoordinates(ent));
    }

    private void OnShotAttempted(Entity<GrosseCarTurretComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.Hull is not { } hull || IsWrecked(hull))
            args.Cancel();
    }

    private void OnAmmoShot(Entity<GrosseCarTurretComponent> ent, ref AmmoShotEvent args)
    {
        if (ent.Comp.Hull is not { } hull)
            return;

        var direction = _transform.GetWorldRotation(ent).ToWorldVec();
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TerminatingOrDeleted(projectile))
                continue;

            if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
                _projectile.SetShooter(projectile, projectileComp, hull);

            var position = _transform.GetWorldPosition(projectile);
            _transform.SetWorldPosition(projectile, position + direction * ent.Comp.Muzzle);
        }

        if (TryComp<GrosseCarJointComponent>(hull, out var joint))
            UpdateAppearance((hull, joint));
    }

    private void OnTurretInserted(Entity<GrosseCarTurretComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Container state is replayed while predicted entities reset. The rider arrives with that state.
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Hull is not { } hull || !TryComp<GrosseCarJointComponent>(hull, out var joint))
            return;

        if (args.Container.ID == joint.GunnerContainer)
            SetupGunner((hull, joint), args.Entity);

        UpdateAppearance((hull, joint));
    }

    private void OnTurretRemoved(Entity<GrosseCarTurretComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Hull is not { } hull || !TryComp<GrosseCarJointComponent>(hull, out var joint))
            return;

        if (args.Container.ID == joint.GunnerContainer)
            ClearGunner((hull, joint), args.Entity);

        UpdateAppearance((hull, joint));
    }

    private void SetupGunner(Entity<GrosseCarJointComponent> hull, EntityUid user)
    {
        var rider = EnsureComp<GrosseCarRiderComponent>(user);
        var alreadyGunner = rider.Car == hull.Owner && rider.ControlsTurret;
        rider.Car = hull;
        rider.SlotId = "gunner";
        rider.IsDriver = false;
        rider.ControlsTurret = true;
        Dirty(user, rider);

        // A second insert must not grant another exit. The client receives actions from the server.
        if (!alreadyGunner && !_net.IsClient)
        {
            foreach (var proto in hull.Comp.GunnerActions)
            {
                EntityUid? action = null;
                _actions.AddAction(user, ref action, proto, hull);
            }
        }

        UpdateTurretActions(hull);
    }

    private void ClearGunner(Entity<GrosseCarJointComponent> hull, EntityUid user)
    {
        RemComp<GrosseCarRiderComponent>(user);
        if (_net.IsClient)
            return;

        _actions.RemoveProvidedActions(user, hull);
        if (hull.Comp.TurretUser == user)
            hull.Comp.TurretUser = null;

        UpdateTurretActions(hull);
    }

    private void OnHullInserted(Entity<GrosseCarJointComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        UpdateTurretActions(ent);
    }

    private void OnHullRemoved(Entity<GrosseCarJointComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.TurretUser == args.Entity)
            ent.Comp.TurretUser = null;

        UpdateTurretActions(ent);
    }

    /// <summary>
    /// Moves <see cref="GrosseCarJointComponent.TurretActions"/> to the occupied seat that loads.
    /// </summary>
    public void UpdateTurretActions(Entity<GrosseCarJointComponent> hull)
    {
        if (_net.IsClient || _timing.ApplyingState || TerminatingOrDeleted(hull))
            return;

        EntityUid? next = TryGetLoader(hull, out _, out var loader) ? loader : null;
        if (hull.Comp.TurretUser == next)
            return;

        var previous = hull.Comp.TurretUser;
        hull.Comp.TurretUser = next;

        if (previous is { } old && old != next && !TerminatingOrDeleted(old))
            RemoveTurretActions(hull, old);

        if (next is { } user)
            GrantTurretActions(hull, user);
    }

    /// <summary>
    /// The occupied loading seat with the best priority, and how long that seat takes to chamber a shell.
    /// </summary>
    public bool TryGetLoader(Entity<GrosseCarJointComponent> hull, out GrosseCarTurretSeat seat, out EntityUid loader)
    {
        GrosseCarTurretSeat? best = null;
        EntityUid bestUser = default;

        foreach (var candidate in hull.Comp.Seats)
        {
            if (!candidate.Loads || !TryGetSeatOccupant(hull, candidate, out var occupant))
                continue;

            if (best != null && candidate.Priority >= best.Priority)
                continue;

            best = candidate;
            bestUser = occupant;
        }

        seat = best!;
        loader = bestUser;
        return best != null;
    }

    private bool TryGetSeatOccupant(Entity<GrosseCarJointComponent> hull, GrosseCarTurretSeat seat, out EntityUid occupant)
    {
        occupant = default;
        BaseContainer? container = null;

        if (seat.OnTurret)
        {
            if (hull.Comp.Turret is not { } turret)
                return false;

            var id = seat.Container ?? hull.Comp.GunnerContainer;
            if (!_container.TryGetContainer(turret, id, out container))
                return false;
        }
        else if (TryComp<GrosseCarComponent>(hull, out var car))
        {
            foreach (var slot in car.Slots)
            {
                if (slot.Id != seat.Id)
                    continue;

                _container.TryGetContainer(hull, slot.ContainerId, out container);
                break;
            }
        }

        if (container == null || container.ContainedEntities.Count == 0)
            return false;

        occupant = container.ContainedEntities[0];
        return true;
    }

    private void GrantTurretActions(Entity<GrosseCarJointComponent> hull, EntityUid user)
    {
        foreach (var proto in hull.Comp.TurretActions)
        {
            if (HasTurretAction(user, proto))
                continue;

            EntityUid? action = null;
            _actions.AddAction(user, ref action, proto, hull);
        }
    }

    private void RemoveTurretActions(Entity<GrosseCarJointComponent> hull, EntityUid user)
    {
        foreach (var action in _actions.GetActions(user).ToArray())
        {
            var id = Prototype(action.Owner)?.ID;
            if (id == null || !hull.Comp.TurretActions.Contains(id))
                continue;

            _actions.RemoveAction(user, action.Owner);
        }
    }

    private bool HasTurretAction(EntityUid user, EntProtoId proto)
    {
        foreach (var action in _actions.GetActions(user))
        {
            if (Prototype(action.Owner)?.ID == proto.Id)
                return true;
        }

        return false;
    }

    private bool TryInsertAmmo(Entity<GrosseCarJointComponent> hull, EntityUid turret, EntityUid shell, EntityUid user)
    {
        if (AmmoCount(turret, hull.Comp.AmmoContainer) >= hull.Comp.AmmoCapacity)
        {
            _popup.PopupClient(Loc.GetString("grosse-tank-ammo-full"), hull, user);
            return false;
        }

        var container = _container.EnsureContainer<Container>(turret, hull.Comp.AmmoContainer);
        if (!_container.Insert(shell, container))
            return false;

        UpdateAppearance(hull);
        return true;
    }

    private bool CanBecomeGunner(EntityUid user)
    {
        return !Deleted(user) && !HasComp<GrosseCarRiderComponent>(user);
    }

    private bool IsChambered(EntityUid turret)
    {
        if (!HasComp<BallisticAmmoProviderComponent>(turret))
            return false;

        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(turret, ref ammo);
        return ammo.Count > 0;
    }

    private int AmmoCount(EntityUid turret, string containerId)
    {
        return _container.TryGetContainer(turret, containerId, out var container)
            ? container.ContainedEntities.Count
            : 0;
    }

    private GrosseTankAmmoVisual AmmoVisual(Entity<GrosseCarJointComponent> hull, EntityUid turret)
    {
        var count = AmmoCount(turret, hull.Comp.AmmoContainer);
        if (count <= 0)
            return GrosseTankAmmoVisual.Empty;

        if (count >= hull.Comp.AmmoCapacity)
            return GrosseTankAmmoVisual.Full;

        return GrosseTankAmmoVisual.Partial;
    }
}
