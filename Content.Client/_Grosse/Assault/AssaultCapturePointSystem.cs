using System.Numerics;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Assault.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.Assault;

public sealed partial class AssaultCapturePointSystem : SharedAssaultCapturePointSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi BarSprite =
        new(new ResPath("/Textures/Interface/Misc/progress_bar.rsi"), "icon");

    private const float BarStartPx = 2f;
    private const float BarEndPx = 22f;
    private const float BarWidthPx = 24f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultCapturePointComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AssaultCapturePointComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AssaultCapturePointComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<AssaultCapturePointComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterState(Entity<AssaultCapturePointComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnShutdown(Entity<AssaultCapturePointComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Visual is not { } visual || !TryComp<SpriteComponent>(visual, out var sprite))
            return;

        RemoveLayer((visual, sprite), AssaultCaptureVisualLayers.BarBackground);
        RemoveLayer((visual, sprite), AssaultCaptureVisualLayers.BarFill);
    }

    private void UpdateVisuals(Entity<AssaultCapturePointComponent> ent)
    {
        // Marker sprite stays hidden in-round. Draw on the spawned overlay instead.
        if (ent.Comp.Visual is not { } visual || !TryComp<SpriteComponent>(visual, out var sprite))
            return;

        var spriteEnt = (visual, sprite);
        EnsureBarLayers(spriteEnt);
        UpdateZoneColor(spriteEnt, ent.Comp);
        UpdateProgressBar(spriteEnt, ent.Comp);
    }

    private void UpdateZoneColor(Entity<SpriteComponent?> spriteEnt, AssaultCapturePointComponent point)
    {
        if (!_sprite.LayerMapTryGet(spriteEnt, AssaultCaptureVisualLayers.Zone, out _, false))
            return;

        var color = point.VisualState switch
        {
            AssaultCaptureState.Captured => Color.FromHex("#33cc6688"),
            AssaultCaptureState.Capturing => Color.FromHex("#cc333388"),
            AssaultCaptureState.Contested => Color.FromHex("#cccc3388"),
            _ => Color.FromHex("#88888866"),
        };

        _sprite.LayerSetColor(spriteEnt, AssaultCaptureVisualLayers.Zone, color);
        _sprite.LayerSetScale(spriteEnt, AssaultCaptureVisualLayers.Zone, new Vector2(point.Radius * 2f, point.Radius * 2f));
    }

    private void UpdateProgressBar(Entity<SpriteComponent?> spriteEnt, AssaultCapturePointComponent point)
    {
        var ratio = Math.Clamp(point.Progress, 0f, 1f);
        var fillWidthPx = (BarEndPx - BarStartPx) * ratio;
        var yOffset = GetBarYOffset(spriteEnt);
        var show = fillWidthPx > 0f && point.VisualState is AssaultCaptureState.Capturing or AssaultCaptureState.Contested or AssaultCaptureState.Captured;

        _sprite.LayerSetOffset(spriteEnt, AssaultCaptureVisualLayers.BarBackground, new Vector2(0f, yOffset));
        _sprite.LayerSetVisible(spriteEnt, AssaultCaptureVisualLayers.BarBackground, show || point.Progress > 0f);
        _sprite.LayerSetVisible(spriteEnt, AssaultCaptureVisualLayers.BarFill, fillWidthPx > 0f);
        if (fillWidthPx <= 0f)
            return;

        var fillCenterPx = -BarWidthPx / 2f + BarStartPx + fillWidthPx / 2f;
        _sprite.LayerSetScale(spriteEnt, AssaultCaptureVisualLayers.BarFill, new Vector2(fillWidthPx, 1f));
        _sprite.LayerSetOffset(spriteEnt,
            AssaultCaptureVisualLayers.BarFill,
            new Vector2(fillCenterPx / EyeManager.PixelsPerMeter, yOffset));
        _sprite.LayerSetColor(spriteEnt, AssaultCaptureVisualLayers.BarFill, GetProgressColor(point.VisualState));
    }

    private void EnsureBarLayers(Entity<SpriteComponent?> spriteEnt)
    {
        if (_sprite.LayerMapTryGet(spriteEnt, AssaultCaptureVisualLayers.BarBackground, out _, false))
            return;

        var yOffset = _sprite.GetLocalBounds((spriteEnt.Owner, spriteEnt.Comp!)).Height / 2f + 0.05f;

        var bg = _sprite.AddLayer(spriteEnt, BarSprite);
        _sprite.LayerMapSet(spriteEnt, AssaultCaptureVisualLayers.BarBackground, bg);
        spriteEnt.Comp!.LayerSetShader(bg, "unshaded");
        _sprite.LayerSetOffset(spriteEnt, bg, new Vector2(0f, yOffset));

        var fill = _sprite.AddTextureLayer(spriteEnt, Texture.White);
        _sprite.LayerMapSet(spriteEnt, AssaultCaptureVisualLayers.BarFill, fill);
        spriteEnt.Comp.LayerSetShader(fill, "unshaded");
    }

    private float GetBarYOffset(Entity<SpriteComponent?> spriteEnt)
    {
        if (_sprite.TryGetLayer(spriteEnt, AssaultCaptureVisualLayers.BarBackground, out var layer, false))
            return layer.Offset.Y;

        return _sprite.GetLocalBounds((spriteEnt.Owner, spriteEnt.Comp!)).Height / 2f + 0.05f;
    }

    private void RemoveLayer(Entity<SpriteComponent?> spriteEnt, AssaultCaptureVisualLayers key)
    {
        if (!_sprite.LayerMapTryGet(spriteEnt, key, out var layer, false))
            return;

        _sprite.RemoveLayer(spriteEnt, layer);
    }

    private static Color GetProgressColor(AssaultCaptureState state)
    {
        return state switch
        {
            AssaultCaptureState.Captured => Color.DarkGreen,
            AssaultCaptureState.Contested => Color.Goldenrod,
            _ => Color.DarkRed,
        };
    }
}
