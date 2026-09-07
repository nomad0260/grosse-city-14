using Content.Shared.CCVar;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Mobs.Components;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client._Grosse;

/// <summary>
/// Keeps lighting enabled for non-admins and enforces a minimum screen-shake intensity.
/// Ported from Backmen LightHandleSystem.
/// </summary>
public sealed partial class LightHandleSystem : EntitySystem
{
    [Dependency] private ILightManager _light = default!;
    [Dependency] private IClientConGroupController _conGroup = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;

    private const float MinScreenShakeIntensity = 0.35f;

    public override void Initialize()
    {
        base.Initialize();

        var screenShakeIntensity = _configurationManager.GetCVar(CCVars.ScreenShakeIntensity);
        if (screenShakeIntensity < MinScreenShakeIntensity)
        {
            _configurationManager.SetCVar(CCVars.ScreenShakeIntensity, MinScreenShakeIntensity, true);
            _configurationManager.SaveToFile();
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_light is { Enabled: true, DrawShadows: true, DrawHardFov: true, DrawLighting: true })
            return;

        if (_conGroup.CanAdminPlace())
            return;

        var plr = _playerManager.LocalSession?.AttachedEntity;
        if (plr == null)
            return;

        if (!HasComp<MobStateComponent>(plr))
            return;

        if (TryComp<BlindableComponent>(plr, out var blindableComponent) && blindableComponent.LightSetup)
            return;

        _light.Enabled = true;
        _light.DrawShadows = true;
        _light.DrawHardFov = true;
        _light.DrawLighting = true;
    }
}
