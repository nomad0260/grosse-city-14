using System;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.UIpatch;

/// <summary>
/// Shared timing helpers and UI audio for the lobby HUD, plus a single place
/// where the accessibility "reduced motion" cvar is resolved.
/// </summary>
public static class LobbyFx
{
    public static readonly Color Graphite = Color.FromHex("#151817");
    public static readonly Color GraphiteDark = Color.FromHex("#111413");
    public static readonly Color Steel = Color.FromHex("#777970");
    public static readonly Color RedAccent = Color.FromHex("#D13239");
    private const float HoverGain = 0.14f;
    private const float ClickGain = 0.26f;
    private const float DeployGain = 0.34f;
    private const float LockedGain = 0.25f;

    private static bool _initialized;
    private static bool _reducedMotion;
    private static float _interfaceGain = 1f;

    private static IAudioSource? _hoverSource;
    private static IAudioSource? _clickSource;
    private static IAudioSource? _deploySource;
    private static IAudioSource? _lockedSource;

    public static bool ReducedMotion => _reducedMotion;

    /// <summary>
    /// Called once by <c>LobbyGui</c> before its controls are created. Safe to call
    /// more than once; the cvar subscription lives for the client's lifetime.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        var config = IoCManager.Resolve<IConfigurationManager>();
        _reducedMotion = config.GetCVar(CCVars.ReducedMotion);
        config.OnValueChanged(CCVars.ReducedMotion, value => _reducedMotion = value);

        _interfaceGain = config.GetCVar(CCVars.InterfaceVolume);
        config.OnValueChanged(CCVars.InterfaceVolume, value =>
        {
            _interfaceGain = value;
            UpdateAudioGains();
        });

        InitializeAudio();
    }

    private static void InitializeAudio()
    {
        var audio = IoCManager.Resolve<IAudioManager>();
        var cache = IoCManager.Resolve<IResourceCache>();

        _hoverSource = CreateSource(audio, cache, "/Audio/_Grosse/Effects/blip1.ogg", "/Audio/UserInterface/hover.ogg", HoverGain);
        _clickSource = CreateSource(audio, cache, "/Audio/_Grosse/Effects/combine_button1.ogg", "/Audio/UserInterface/click.ogg", ClickGain);
        _deploySource = CreateSource(audio, cache, "/Audio/_Grosse/Effects/combine_button1.ogg", "/Audio/UserInterface/click.ogg", DeployGain);
        _lockedSource = CreateSource(audio, cache, "/Audio/_Grosse/Effects/combine_button_locked.ogg", "/Audio/UserInterface/click.ogg", LockedGain);
    }

    private static IAudioSource? CreateSource(IAudioManager audio, IResourceCache cache, string path, string fallback, float gain)
    {
        if (!cache.TryGetResource(path, out AudioResource? resource) &&
            !cache.TryGetResource(fallback, out resource))
            return null;

        var source = audio.CreateAudioSource(resource);
        if (source == null)
            return null;

        source.Global = true;
        source.Gain = gain * _interfaceGain;
        return source;
    }

    private static void UpdateAudioGains()
    {
        if (_hoverSource != null)
            _hoverSource.Gain = HoverGain * _interfaceGain;
        if (_clickSource != null)
            _clickSource.Gain = ClickGain * _interfaceGain;
        if (_deploySource != null)
            _deploySource.Gain = DeployGain * _interfaceGain;
        if (_lockedSource != null)
            _lockedSource.Gain = LockedGain * _interfaceGain;
    }

    public static void PlayHover()
    {
        Initialize();
        _hoverSource?.Restart();
    }

    public static void PlayClick()
    {
        Initialize();
        _clickSource?.Restart();
    }

    public static void PlayDeploy()
    {
        Initialize();
        _deploySource?.Restart();
    }

    public static void PlayLocked()
    {
        Initialize();
        _lockedSource?.Restart();
    }

    /// <summary>
    /// Attaches standard lobby hover and click sounds to any button.
    /// </summary>
    public static void AttachAudio(BaseButton button, bool deploy = false)
    {
        button.OnMouseEntered += _ => PlayHover();
        button.OnPressed += _ =>
        {
            if (deploy)
                PlayDeploy();
            else
                PlayClick();
        };
    }

    /// <summary>
    /// Frame-rate independent exponential approach, used for hover/press easing.
    /// </summary>
    public static float Approach(float current, float target, float speed, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return current;

        var factor = 1f - MathF.Exp(-speed * deltaSeconds);
        return current + (target - current) * factor;
    }

    /// <summary>
    /// 0..1 sine pulse at the requested frequency.
    /// </summary>
    public static float Pulse(float time, float hertz)
    {
        return 0.5f + 0.5f * MathF.Sin(time * MathF.Tau * hertz);
    }
}
