using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Grosse.Cars;

public sealed partial class SharedGrosseCarSystem
{
    private void OnToggleEngine(Entity<GrosseCarComponent> ent, ref GrosseCarToggleEngineEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryToggleEngine(ent, args.Performer);
    }

    /// <summary>
    /// Starts the engine from <see cref="GrosseCarEngineState.Off"/> or stops it from <see cref="GrosseCarEngineState.On"/>.
    /// </summary>
    public bool TryToggleEngine(Entity<GrosseCarComponent> car, EntityUid user)
    {
        if (!TryGetDriver(car, out var driver) || driver != user)
            return false;

        switch (car.Comp.EngineState)
        {
            case GrosseCarEngineState.Off:
                if (IsWrecked(car))
                {
                    _popup.PopupPredicted(Loc.GetString("grosse-car-engine-wrecked"), car, user);
                    return false;
                }

                BeginStart(car, user);
                return true;
            case GrosseCarEngineState.On:
                BeginStop(car, user);
                return true;
            default:
                return false;
        }
    }

    private void UpdateEngine()
    {
        var query = EntityQueryEnumerator<GrosseCarComponent>();
        while (query.MoveNext(out var uid, out var car))
        {
            var ent = (uid, car);
            if (_net.IsServer && IsWrecked(ent) && car.EngineState is not (GrosseCarEngineState.Off or GrosseCarEngineState.Stopping))
                BeginStop(ent, null);

            if (car.EngineState is GrosseCarEngineState.Starting or GrosseCarEngineState.Stopping &&
                _timing.CurTime >= car.EngineTransitionEnd)
            {
                var next = car.EngineState == GrosseCarEngineState.Starting
                    ? GrosseCarEngineState.On
                    : GrosseCarEngineState.Off;
                SetEngineState(ent, next);
            }
        }

        UpdateEngineAudio();
    }

    private void BeginStart(Entity<GrosseCarComponent> car, EntityUid user)
    {
        StopEngineLoops(car.Comp);
        var length = SoundLength(car.Comp.EngineStartSound, 0.7);
        SetEngineState(car, GrosseCarEngineState.Starting, _timing.CurTime + length);
        _popup.PopupPredicted(Loc.GetString("grosse-car-engine-started"), car, user);
        if (car.Comp.EngineStartSound != null)
            _audio.PlayPredicted(car.Comp.EngineStartSound, car, user);
    }

    private void BeginStop(Entity<GrosseCarComponent> car, EntityUid? user)
    {
        if (car.Comp.EngineState is GrosseCarEngineState.Off or GrosseCarEngineState.Stopping)
            return;

        StopEngineLoops(car.Comp);
        var length = SoundLength(car.Comp.EngineStopSound, 0.4);
        SetEngineState(car, GrosseCarEngineState.Stopping, _timing.CurTime + length);

        if (user != null)
        {
            _popup.PopupPredicted(Loc.GetString("grosse-car-engine-stopped"), car, user.Value);
            if (car.Comp.EngineStopSound != null)
                _audio.PlayPredicted(car.Comp.EngineStopSound, car, user);
            return;
        }

        if (!_net.IsServer || car.Comp.EngineStopSound == null)
            return;

        _audio.PlayPvs(car.Comp.EngineStopSound, car);
        if (TryGetDriver(car, out var driver))
            _popup.PopupPredicted(Loc.GetString("grosse-car-engine-stopped"), car, driver);
    }

    private void SetEngineState(Entity<GrosseCarComponent> car, GrosseCarEngineState state, TimeSpan? endsAt = null)
    {
        car.Comp.EngineState = state;
        if (endsAt != null)
            car.Comp.EngineTransitionEnd = endsAt.Value;

        if (state != GrosseCarEngineState.On)
        {
            car.Comp.WasRolling = false;
            car.Comp.MoveStartUntil = TimeSpan.Zero;
        }

        if (state == GrosseCarEngineState.Off)
            StopEngineLoops(car.Comp);

        if (_net.IsServer)
            Dirty(car.Owner, car.Comp);

        UpdateEngineToggle(car);
    }

    private void UpdateEngineToggle(Entity<GrosseCarComponent> car)
    {
        if (_net.IsClient || !TryGetDriver(car, out var driver))
            return;

        var on = car.Comp.EngineState is GrosseCarEngineState.On or GrosseCarEngineState.Starting;
        foreach (var action in _actions.GetActions(driver))
        {
            if (MetaData(action).EntityPrototype?.ID != car.Comp.EngineToggleAction.Id)
                continue;

            _actions.SetToggled(action.Owner, on);
        }
    }

    private void UpdateEngineAudio()
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<GrosseCarComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var car, out var physics))
        {
            var ent = (uid, car);
            if (car.EngineState != GrosseCarEngineState.On)
            {
                StopEngineLoops(car);
                continue;
            }

            var speed = physics.LinearVelocity.Length();
            var rolling = speed >= (car.WasRolling ? car.DriveStopSpeed : car.DriveStartSpeed);
            var startedRolling = rolling && !car.WasRolling;
            if (car.WasRolling != rolling)
            {
                car.WasRolling = rolling;
                if (_net.IsServer)
                    Dirty(uid, car);
            }

            if (!rolling)
            {
                car.MoveStartSoundEntity = _audio.Stop(car.MoveStartSoundEntity);
                car.MoveStartUntil = TimeSpan.Zero;
                EnsureLoop(ent, false, car.EngineDriveSound, ref car.DriveSoundEntity, ref car.DriveSoundExcluded, car.DriveVolume);
                EnsureLoop(ent, true, car.EngineIdleSound, ref car.IdleSoundEntity, ref car.IdleSoundExcluded, car.IdleVolume);
                continue;
            }

            EnsureLoop(ent, false, car.EngineIdleSound, ref car.IdleSoundEntity, ref car.IdleSoundExcluded, car.IdleVolume);
            var moveStartPlaying = car.MoveStartUntil > _timing.CurTime;
            if (startedRolling && car.EngineMoveStartSound != null)
            {
                EnsureLoop(ent, false, car.EngineDriveSound, ref car.DriveSoundEntity, ref car.DriveSoundExcluded, car.DriveVolume);
                car.MoveStartUntil = _timing.CurTime + SoundLength(car.EngineMoveStartSound, 1);
                PlayGated(ent, car.EngineMoveStartSound, ref car.MoveStartSoundEntity);
                continue;
            }

            if (moveStartPlaying)
            {
                EnsureLoop(ent, false, car.EngineDriveSound, ref car.DriveSoundEntity, ref car.DriveSoundExcluded, car.DriveVolume);
                continue;
            }

            car.MoveStartSoundEntity = _audio.Stop(car.MoveStartSoundEntity);
            EnsureLoop(ent, true, car.EngineDriveSound, ref car.DriveSoundEntity, ref car.DriveSoundExcluded, car.DriveVolume);
        }
    }

    private void EnsureLoop(
        Entity<GrosseCarComponent> car,
        bool play,
        SoundSpecifier? sound,
        ref EntityUid? entity,
        ref EntityUid? excluded,
        float volume)
    {
        if (!play || sound == null)
        {
            entity = _audio.Stop(entity);
            excluded = null;
            return;
        }

        EntityUid? driver = TryGetDriver(car, out var driverUid) ? driverUid : null;
        if (_net.IsServer)
        {
            if (entity != null && (!Exists(entity.Value) || excluded != driver))
            {
                entity = _audio.Stop(entity);
                excluded = null;
            }

            if (entity == null)
            {
                var param = AudioParams.Default.WithLoop(true).WithVolume(volume);
                entity = _audio.PlayPredicted(sound, car, driver, param)?.Entity;
                excluded = driver;
            }

            return;
        }

        var local = _player.LocalEntity;
        if (local == null || driver != local)
        {
            entity = _audio.Stop(entity);
            excluded = null;
            return;
        }

        if (entity == null || !Exists(entity.Value))
        {
            var param = AudioParams.Default.WithLoop(true).WithVolume(volume);
            entity = _audio.PlayPredicted(sound, car, local, param)?.Entity;
            excluded = local;
        }
    }

    private void PlayGated(Entity<GrosseCarComponent> car, SoundSpecifier sound, ref EntityUid? entity)
    {
        entity = _audio.Stop(entity);
        if (_net.IsServer)
        {
            EntityUid? driver = TryGetDriver(car, out var driverUid) ? driverUid : null;
            entity = _audio.PlayPredicted(sound, car, driver)?.Entity;
            return;
        }

        if (_player.LocalEntity is { } local && TryGetDriver(car, out var localDriver) && localDriver == local)
            entity = _audio.PlayPredicted(sound, car, local)?.Entity;
    }

    private void StopEngineLoops(GrosseCarComponent car)
    {
        car.IdleSoundEntity = _audio.Stop(car.IdleSoundEntity);
        car.DriveSoundEntity = _audio.Stop(car.DriveSoundEntity);
        car.MoveStartSoundEntity = _audio.Stop(car.MoveStartSoundEntity);
        car.IdleSoundExcluded = null;
        car.DriveSoundExcluded = null;
        car.MoveStartUntil = TimeSpan.Zero;
    }

    private bool IsWrecked(Entity<GrosseCarComponent> car)
    {
        return _damageable.GetTotalDamage(car.Owner).Float() >= car.Comp.MaxDriveDamage;
    }

    private TimeSpan SoundLength(SoundSpecifier? sound, double fallbackSeconds)
    {
        if (sound == null)
            return TimeSpan.FromSeconds(fallbackSeconds);

        try
        {
            var resolved = _audio.ResolveSound(sound);
            if (string.IsNullOrEmpty(_audio.GetAudioPath(resolved)))
                return TimeSpan.FromSeconds(fallbackSeconds);

            var length = _audio.GetAudioLength(resolved);
            return length > TimeSpan.Zero ? length : TimeSpan.FromSeconds(fallbackSeconds);
        }
        catch (Exception)
        {
            return TimeSpan.FromSeconds(fallbackSeconds);
        }
    }
}
