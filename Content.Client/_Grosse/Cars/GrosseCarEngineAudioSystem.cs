using System.Numerics;
using Content.Shared._Grosse.Cars;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Client._Grosse.Cars;

/// <summary>
/// Raises the pitch and volume of the running engine as the driver presses the throttle.
/// Pitch is applied on the client because the audio source is not replicated every frame.
/// </summary>
public sealed partial class GrosseCarEngineAudioSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<GrosseCarComponent, InputMoverComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var car, out var mover, out var physics, out var xform))
        {
            if (car.EngineState != GrosseCarEngineState.On)
            {
                car.CurrentRpm = 0f;
                continue;
            }

            var target = TargetRpm(uid, car, mover, physics, xform);
            var rate = target > car.CurrentRpm ? car.RpmRiseRate : car.RpmFallRate;
            var step = rate * frameTime;
            if (Math.Abs(target - car.CurrentRpm) <= step)
                car.CurrentRpm = target;
            else
                car.CurrentRpm += Math.Sign(target - car.CurrentRpm) * step;

            var pitch = car.IdlePitch + (car.RevPitch - car.IdlePitch) * car.CurrentRpm;
            var idleVolume = car.IdleVolume + 3f * car.CurrentRpm;
            var driveVolume = car.DriveVolume + 3f * car.CurrentRpm;
            var idlePath = AudioPath(car.EngineIdleSound);
            var drivePath = AudioPath(car.EngineDriveSound);
            var movePath = AudioPath(car.EngineMoveStartSound);

            Tune(car.IdleSoundEntity, idlePath, pitch, idleVolume);
            Tune(car.DriveSoundEntity, drivePath, pitch, driveVolume);
            Tune(car.MoveStartSoundEntity, movePath, pitch, driveVolume);

            var children = xform.ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (!TryComp<AudioComponent>(child, out var audio))
                    continue;

                if (SameFile(audio.FileName, idlePath))
                    SetSource(audio, pitch, idleVolume);
                else if (SameFile(audio.FileName, drivePath) || SameFile(audio.FileName, movePath))
                    SetSource(audio, pitch, driveVolume);
            }
        }
    }

    private float TargetRpm(
        EntityUid uid,
        GrosseCarComponent car,
        InputMoverComponent mover,
        PhysicsComponent physics,
        TransformComponent xform)
    {
        var buttons = HasComp<MovementRelayTargetComponent>(uid)
            ? SharedMoverController.GetNormalizedMovement(mover.HeldMoveButtons)
            : MoveButtons.None;

        var heading = _xform.GetWorldRotation(xform) - car.VisualRotationOffset;
        var facing = heading.ToWorldVec();
        facing = facing.LengthSquared() < 0.0001f ? Vector2.UnitX : Vector2.Normalize(facing);
        var signed = Vector2.Dot(physics.LinearVelocity, facing);

        var throttle = 0f;
        if ((buttons & MoveButtons.Up) != 0)
            throttle = 1f;
        else if ((buttons & MoveButtons.Down) != 0 && signed <= 0.15f)
            throttle = 0.55f;

        var speedFactor = Math.Clamp(Math.Abs(signed) / Math.Max(car.MaxForwardSpeed, 0.01f), 0f, 1f) * 0.35f;
        return Math.Clamp(throttle + speedFactor * (1f - throttle), 0f, 1f);
    }

    private string? AudioPath(Robust.Shared.Audio.SoundSpecifier? sound)
    {
        if (sound == null)
            return null;

        return _audio.GetAudioPath(_audio.ResolveSound(sound));
    }

    private void Tune(EntityUid? entity, string? path, float pitch, float volume)
    {
        if (entity == null || !TryComp<AudioComponent>(entity.Value, out var audio))
            return;

        if (path != null && !SameFile(audio.FileName, path))
            return;

        SetSource(audio, pitch, volume);
    }

    private static void SetSource(AudioComponent audio, float pitch, float volume)
    {
        // Pitch is public on the audio source. The engine access analyzer only allows
        // SharedAudioSystem to write it, and that submodule cannot be changed.
#pragma warning disable RA0002
        audio.Pitch = pitch;
#pragma warning restore RA0002
        audio.Volume = volume;
    }

    private static bool SameFile(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        return left.TrimStart('/').Equals(right.TrimStart('/'), StringComparison.OrdinalIgnoreCase);
    }
}
