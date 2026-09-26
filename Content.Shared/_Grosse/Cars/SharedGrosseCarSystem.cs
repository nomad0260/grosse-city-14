using Content.Shared.Access.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Input;
using Content.Shared.Instruments;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Grosse.Cars;

public sealed partial class SharedGrosseCarSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private ActivatableUISystem _activatableUi = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtual = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeCollision();

        SubscribeLocalEvent<GrosseCarComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GrosseCarComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GrosseCarComponent, ComponentShutdown>(OnCarShutdown);
        SubscribeLocalEvent<GrosseCarComponent, GetVerbsEvent<InteractionVerb>>(OnInteractionVerbs);
        SubscribeLocalEvent<GrosseCarComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerbs);
        SubscribeLocalEvent<GrosseCarComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<GrosseCarComponent, GrosseCarEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<GrosseCarComponent, GrosseCarEjectDoAfterEvent>(OnEjectDoAfter);
        SubscribeLocalEvent<GrosseCarComponent, GrosseCarExitEvent>(OnExitAction);
        SubscribeLocalEvent<GrosseCarComponent, BoundUIOpenedEvent>(OnBoundUiOpened);
        SubscribeLocalEvent<GrosseCarComponent, ActivatableUIOpenAttemptEvent>(OnInstrumentOpenAttempt);
        SubscribeLocalEvent<GrosseCarComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<GrosseCarComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<GrosseCarComponent, CanDropTargetEvent>(OnCanDrop);
        SubscribeLocalEvent<GrosseCarComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<GrosseCarComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<GrosseCarComponent, LightToggleEvent>(OnLightToggle);
        SubscribeLocalEvent<GrosseCarComponent, ContainerRelayMovementEntityEvent>(OnContainerRelay);

        SubscribeLocalEvent<GrosseCarRiderComponent, UpdateCanMoveEvent>(OnRiderCanMove);
        SubscribeLocalEvent<GrosseCarRiderComponent, ComponentShutdown>(OnRiderShutdown);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ShuttleBrake, InputCmdHandler.FromDelegate(OnHandbrakeDown, OnHandbrakeUp, handle: false, outsidePrediction: true))
            .Register<SharedGrosseCarSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SharedGrosseCarSystem>();
    }

    public override void Update(float frameTime)
    {
        UpdateMotionVisuals();
        UpdateAudio();
    }

    private void OnStartup(Entity<GrosseCarComponent> ent, ref ComponentStartup args)
    {
        foreach (var slot in ent.Comp.Slots)
        {
            _container.EnsureContainer<ContainerSlot>(ent.Owner, slot.ContainerId);
        }

        _appearance.SetData(ent.Owner, GrosseCarVisuals.Idle, true);
        _appearance.SetData(ent.Owner, GrosseCarVisuals.Run, false);
    }

    private void OnMapInit(Entity<GrosseCarComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.RadioActionEntity, ent.Comp.RadioAction);
    }

    private void OnCarShutdown(Entity<GrosseCarComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.EngineSoundEntity = _audio.Stop(ent.Comp.EngineSoundEntity);
        ent.Comp.DriftSoundEntity = _audio.Stop(ent.Comp.DriftSoundEntity);
        _ui.CloseUi(ent.Owner, InstrumentUiKey.Key);
    }

    private void OnInteractionVerbs(Entity<GrosseCarComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        foreach (var slot in ent.Comp.Slots)
        {
            if (!IsSlotFree(ent, slot))
                continue;

            var slotId = slot.Id;
            var verb = new InteractionVerb
            {
                Text = Loc.GetString(slot.Name),
                Category = VerbCategory.Enter,
                Act = () => TryEnterSlot(user, ent, slotId),
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnAlternativeVerbs(Entity<GrosseCarComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (TryGetFirstFreeSlot(ent, out var slot))
        {
            var slotId = slot.Id;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("grosse-car-verb-enter-first", ("slot", Loc.GetString(slot.Name))),
                Priority = 10,
                Act = () => TryEnterSlot(user, ent, slotId),
            });
        }

        if (user == args.Target)
            return;

        foreach (var occupied in ent.Comp.Slots)
        {
            if (!_container.TryGetContainer(ent.Owner, occupied.ContainerId, out var container) ||
                container.ContainedEntities.Count == 0)
                continue;

            var occupant = container.ContainedEntities[0];
            if (occupant == user)
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("grosse-car-verb-eject", ("target", occupant)),
                Priority = -2,
                Act = () => TryEject(ent, occupant, user, skipDelay: false),
            });
        }
    }

    private void OnVerbs(Entity<GrosseCarComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        if (!TryComp<GrosseCarRiderComponent>(user, out var rider) || rider.Car != ent.Owner || rider.ControlsTurret)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("grosse-car-verb-exit"),
            Priority = 8,
            Act = () => TryEject(ent, user, user, skipDelay: true),
        });
    }

    private void OnEnterDoAfter(Entity<GrosseCarComponent> ent, ref GrosseCarEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var occupant = args.Target ?? args.User;
        args.Handled = TryInsert(ent, occupant, args.SlotId);
    }

    private void OnEjectDoAfter(Entity<GrosseCarComponent> ent, ref GrosseCarEjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var occupant = args.Target ?? args.User;
        args.Handled = TryEject(ent, occupant, args.User, skipDelay: true);
    }

    private void OnExitAction(Entity<GrosseCarComponent> ent, ref GrosseCarExitEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryEject(ent, args.Performer, args.Performer, skipDelay: true);
    }

    private void OnBoundUiOpened(Entity<GrosseCarComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, InstrumentUiKey.Key))
            return;

        if (TryComp<ActivatableUIComponent>(ent.Owner, out var aui))
            _activatableUi.SetCurrentSingleUser(ent.Owner, args.Actor, aui);
    }

    private void OnInstrumentOpenAttempt(Entity<GrosseCarComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!TryGetDriver(ent, out var driver) || driver != args.User)
            args.Cancel();
    }

    private void OnInserted(Entity<GrosseCarComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryGetSlotByContainer(ent.Comp, args.Container.ID, out var slot))
            return;

        SetupOccupant(ent, args.Entity, slot);
    }

    private void OnRemoved(Entity<GrosseCarComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryGetSlotByContainer(ent.Comp, args.Container.ID, out var slot))
            return;

        CleanupOccupant(ent, args.Entity, slot);
    }

    private void OnCanDrop(Entity<GrosseCarComponent> ent, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= HasComp<MobStateComponent>(args.Dragged) && TryGetFirstFreeSlot(ent, out _) && CanEnter(args.Dragged);
    }

    private void OnDragDrop(Entity<GrosseCarComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetFirstFreeSlot(ent, out var slot))
            return;

        args.Handled = true;
        TryEnterSlot(args.Dragged, ent, slot.Id, user: args.User);
    }

    private void OnGetAdditionalAccess(Entity<GrosseCarComponent> ent, ref GetAdditionalAccessEvent args)
    {
        if (!TryGetDriver(ent, out var driver))
            return;

        args.Entities.Add(driver);
    }

    private void OnLightToggle(Entity<GrosseCarComponent> ent, ref LightToggleEvent args)
    {
        _appearance.SetData(ent.Owner, GrosseCarVisuals.Lights, args.IsOn);
    }

    private void OnContainerRelay(Entity<GrosseCarComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        // Occupants stay seated; movement is either relayed (driver) or blocked (passengers).
    }

    private void OnRiderCanMove(Entity<GrosseCarRiderComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (!ent.Comp.IsDriver)
            args.Cancel();
    }

    private void OnRiderShutdown(Entity<GrosseCarRiderComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        if (TryComp<GrosseCarComponent>(ent.Comp.Car, out var car) &&
            TryGetSlot(car, ent.Comp.SlotId, out var slot) &&
            _container.TryGetContainer(ent.Comp.Car, slot.ContainerId, out var container) &&
            container.Contains(ent.Owner))
        {
            _container.Remove(ent.Owner, container, destination: _transform.GetMoverCoordinates(ent.Comp.Car));
        }
    }

    private void OnHandbrakeDown(ICommonSession? session)
    {
        SetHandbrake(session, true);
    }

    private void OnHandbrakeUp(ICommonSession? session)
    {
        SetHandbrake(session, false);
    }

    private void SetHandbrake(ICommonSession? session, bool down)
    {
        if (session?.AttachedEntity is not { } player)
            return;

        if (!TryComp<GrosseCarRiderComponent>(player, out var rider) || !rider.IsDriver)
            return;

        if (!TryComp<GrosseCarComponent>(rider.Car, out var car))
            return;

        if (car.Handbrake == down)
            return;

        car.Handbrake = down;
        Dirty(rider.Car, car);
        _appearance.SetData(rider.Car, GrosseCarVisuals.Handbrake, down);
    }

    /// <summary>
    /// Inserts <paramref name="occupant"/> into a slot. When <paramref name="skipDelay"/> is false, starts a do-after.
    /// </summary>
    public bool TryEnterSlot(EntityUid occupant, EntityUid car, string? slotId = null, bool skipDelay = false, EntityUid? user = null, GrosseCarComponent? component = null)
    {
        if (!Resolve(car, ref component))
            return false;

        var ent = new Entity<GrosseCarComponent>(car, component);
        if (slotId == null)
        {
            if (!TryGetFirstFreeSlot(ent, out var free))
                return false;

            slotId = free.Id;
        }

        if (!TryGetSlot(component, slotId, out var slot) || !IsSlotFree(ent, slot) || !CanEnter(occupant))
            return false;

        var actor = user ?? occupant;
        if (!skipDelay && component.EntryDelay > TimeSpan.Zero)
        {
            var args = new DoAfterArgs(EntityManager, actor, component.EntryDelay, new GrosseCarEnterDoAfterEvent(slotId), car, occupant, car)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false,
            };
            return _doAfter.TryStartDoAfter(args);
        }

        return TryInsert(ent, occupant, slotId);
    }

    public bool TryEject(Entity<GrosseCarComponent> car, EntityUid occupant, EntityUid user, bool skipDelay)
    {
        if (!TryComp<GrosseCarRiderComponent>(occupant, out var rider) || rider.Car != car.Owner)
            return false;

        if (!skipDelay && user != occupant && car.Comp.ExitDelay > TimeSpan.Zero)
        {
            _popup.PopupPredicted(
                Loc.GetString("grosse-car-eject-others", ("target", occupant)),
                car.Owner,
                user,
                PopupType.Large);

            var args = new DoAfterArgs(EntityManager, user, car.Comp.ExitDelay, new GrosseCarEjectDoAfterEvent(rider.SlotId), car.Owner, occupant, car.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false,
            };
            return _doAfter.TryStartDoAfter(args);
        }

        if (!TryGetSlot(car.Comp, rider.SlotId, out var slot))
            return false;

        if (!_container.TryGetContainer(car.Owner, slot.ContainerId, out var container))
            return false;

        var coords = _transform.GetMoverCoordinates(car.Owner);
        return _container.Remove(occupant, container, destination: coords);
    }

    public bool TryInsert(Entity<GrosseCarComponent> car, EntityUid occupant, string slotId)
    {
        if (!TryGetSlot(car.Comp, slotId, out var slot) || !IsSlotFree(car, slot) || !CanEnter(occupant))
            return false;

        if (!_container.TryGetContainer(car.Owner, slot.ContainerId, out var container))
            return false;

        return _container.Insert(occupant, container);
    }

    private void SetupOccupant(Entity<GrosseCarComponent> car, EntityUid occupant, GrosseCarSlot slot)
    {
        var rider = EnsureComp<GrosseCarRiderComponent>(occupant);
        rider.Car = car.Owner;
        rider.SlotId = slot.Id;
        rider.IsDriver = slot.IsDriver;
        Dirty(occupant, rider);

        if (slot.IsDriver)
        {
            _mover.SetRelay(occupant, car.Owner);
            _virtual.TryOccupyHands(car.Owner, occupant);
        }

        _blocker.UpdateCanMove(occupant);

        if (slot.Visuals is { } visuals)
            _appearance.SetData(car.Owner, visuals, true);

        if (_net.IsClient)
            return;

        foreach (var proto in car.Comp.OccupantActions)
        {
            EntityUid? action = null;
            _actions.AddAction(occupant, ref action, proto, car.Owner);
        }

        if (slot.IsDriver && TryComp<UnpoweredFlashlightComponent>(car.Owner, out var light) &&
            light.ToggleActionEntity is { } flashlight)
        {
            _actions.GrantContainedAction(occupant, car.Owner, flashlight);
        }

        if (slot.IsDriver && car.Comp.RadioActionEntity is { } radio)
            _actions.GrantContainedAction(occupant, car.Owner, radio);
    }

    private void CleanupOccupant(Entity<GrosseCarComponent> car, EntityUid occupant, GrosseCarSlot slot)
    {
        if (slot.IsDriver)
        {
            RemComp<RelayInputMoverComponent>(occupant);
            _virtual.DeleteInHandsMatching(occupant, car.Owner);
            _ui.CloseUi(car.Owner, InstrumentUiKey.Key, occupant);
        }

        RemComp<GrosseCarRiderComponent>(occupant);
        _blocker.UpdateCanMove(occupant);

        if (slot.Visuals is { } visuals)
            _appearance.SetData(car.Owner, visuals, false);

        if (!_net.IsClient)
            _actions.RemoveProvidedActions(occupant, car.Owner);

        if (slot.IsDriver && TryComp<GrosseCarComponent>(car.Owner, out var carComp) && carComp.Handbrake)
        {
            carComp.Handbrake = false;
            Dirty(car.Owner, carComp);
            _appearance.SetData(car.Owner, GrosseCarVisuals.Handbrake, false);
        }
    }

    private bool CanEnter(EntityUid occupant)
    {
        if (Deleted(occupant) || HasComp<GrosseCarRiderComponent>(occupant))
            return false;

        return _blocker.CanMove(occupant);
    }

    private bool IsSlotFree(Entity<GrosseCarComponent> car, GrosseCarSlot slot)
    {
        return _container.TryGetContainer(car.Owner, slot.ContainerId, out var container) &&
               container.ContainedEntities.Count == 0;
    }

    public bool TryGetFirstFreeSlot(Entity<GrosseCarComponent> car, [NotNullWhen(true)] out GrosseCarSlot? slot)
    {
        foreach (var candidate in car.Comp.Slots)
        {
            if (!IsSlotFree(car, candidate))
                continue;

            slot = candidate;
            return true;
        }

        slot = null;
        return false;
    }

    public bool TryGetSlot(GrosseCarComponent component, string slotId, [NotNullWhen(true)] out GrosseCarSlot? slot)
    {
        foreach (var candidate in component.Slots)
        {
            if (candidate.Id != slotId)
                continue;

            slot = candidate;
            return true;
        }

        slot = null;
        return false;
    }

    private static bool TryGetSlotByContainer(GrosseCarComponent component, string containerId, [NotNullWhen(true)] out GrosseCarSlot? slot)
    {
        foreach (var candidate in component.Slots)
        {
            if (candidate.ContainerId != containerId)
                continue;

            slot = candidate;
            return true;
        }

        slot = null;
        return false;
    }

    private bool TryGetDriver(Entity<GrosseCarComponent> car, out EntityUid driver)
    {
        foreach (var slot in car.Comp.Slots)
        {
            if (!slot.IsDriver)
                continue;

            if (_container.TryGetContainer(car.Owner, slot.ContainerId, out var container) &&
                container.ContainedEntities.Count > 0)
            {
                driver = container.ContainedEntities[0];
                return true;
            }
        }

        driver = default;
        return false;
    }

    private void UpdateMotionVisuals()
    {
        var query = EntityQueryEnumerator<GrosseCarComponent, PhysicsComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var car, out var physics, out _))
        {
            var running = physics.LinearVelocity.LengthSquared() >= 0.0225f;
            if (car.VisualRunning == running)
                continue;

            car.VisualRunning = running;
            _appearance.SetData(uid, GrosseCarVisuals.Idle, !running);
            _appearance.SetData(uid, GrosseCarVisuals.Run, running);
        }
    }

    private void UpdateAudio()
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<GrosseCarComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var car, out var physics, out _))
        {
            var speed = physics.LinearVelocity.Length();

            if (TryComp<MovementSoundComponent>(uid, out var movement) && movement.Sound != null)
            {
                var shouldPlay = speed >= car.EngineSoundSpeed;
                if (shouldPlay && car.EngineSoundEntity == null)
                    car.EngineSoundEntity = _audio.PlayPredicted(movement.Sound, uid, uid)?.Entity;
                else if (!shouldPlay && car.EngineSoundEntity != null)
                    car.EngineSoundEntity = _audio.Stop(car.EngineSoundEntity);
            }

            if (car.DriftSound != null)
            {
                EntityUid? listener = TryGetDriver((uid, car), out var driver) ? driver : uid;
                if (car.IsDrifting && car.DriftSoundEntity == null)
                {
                    var volume = Math.Clamp(car.DriftSlip * 6f - 4f, -8f, 2f);
                    if (car.Handbrake)
                        volume = Math.Max(volume, -4f);
                    car.DriftSoundEntity = _audio.PlayPredicted(car.DriftSound, uid, listener, AudioParams.Default.WithVolume(volume).WithLoop(true))?.Entity;
                }
                else if (!car.IsDrifting && car.DriftSoundEntity != null)
                {
                    car.DriftSoundEntity = _audio.Stop(car.DriftSoundEntity);
                }
            }
        }
    }
}
