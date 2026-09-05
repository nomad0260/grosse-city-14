using System.Numerics;
using Content.Shared._Grosse.Control;
using Content.Shared._Grosse.Control.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.Control;

public sealed partial class ControlCapturePointSystem : SharedControlCapturePointSystem
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
        SubscribeLocalEvent<ControlCapturePointComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ControlCapturePointComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ControlCapturePointComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<ControlCapturePointComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterState(Entity<ControlCapturePointComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnShutdown(Entity<ControlCapturePointComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        RemoveLayer((ent.Owner, sprite), ControlCaptureVisualLayers.BarBackground);
        RemoveLayer((ent.Owner, sprite), ControlCaptureVisualLayers.BarFill);
    }

    private void UpdateVisuals(Entity<ControlCapturePointComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        EnsureBarLayers(spriteEnt);
        UpdateScreenColor(spriteEnt, ent.Comp);
        UpdateProgressBar(spriteEnt, ent.Comp);
    }

    private void UpdateScreenColor(Entity<SpriteComponent?> spriteEnt, ControlCapturePointComponent point)
    {
        if (!_sprite.LayerMapTryGet(spriteEnt, ControlCaptureVisualLayers.Screen, out _, false)
            && !_sprite.LayerMapTryGet(spriteEnt, "computerLayerKeys", out _, false))
            return;

        var color = OwnerColor(point.Owner);
        if (_sprite.LayerMapTryGet(spriteEnt, ControlCaptureVisualLayers.Screen, out _, false))
            _sprite.LayerSetColor(spriteEnt, ControlCaptureVisualLayers.Screen, color);
        else
            _sprite.LayerSetColor(spriteEnt, "computerLayerKeys", color);
    }

    private void UpdateProgressBar(Entity<SpriteComponent?> spriteEnt, ControlCapturePointComponent point)
    {
        var ratio = Math.Clamp(point.Progress, 0f, 1f);
        var fillWidthPx = (BarEndPx - BarStartPx) * ratio;
        var yOffset = GetBarYOffset(spriteEnt);
        var show = point.VisualState is ControlCaptureState.Capturing or ControlCaptureState.Contested
            || point.Progress > 0f;

        _sprite.LayerSetOffset(spriteEnt, ControlCaptureVisualLayers.BarBackground, new Vector2(0f, yOffset));
        _sprite.LayerSetVisible(spriteEnt, ControlCaptureVisualLayers.BarBackground, show);
        _sprite.LayerSetVisible(spriteEnt, ControlCaptureVisualLayers.BarFill, fillWidthPx > 0f);
        if (fillWidthPx <= 0f)
            return;

        var fillCenterPx = -BarWidthPx / 2f + BarStartPx + fillWidthPx / 2f;
        _sprite.LayerSetScale(spriteEnt, ControlCaptureVisualLayers.BarFill, new Vector2(fillWidthPx, 1f));
        _sprite.LayerSetOffset(spriteEnt,
            ControlCaptureVisualLayers.BarFill,
            new Vector2(fillCenterPx / EyeManager.PixelsPerMeter, yOffset));
        _sprite.LayerSetColor(spriteEnt, ControlCaptureVisualLayers.BarFill, GetProgressColor(point));
    }

    private void EnsureBarLayers(Entity<SpriteComponent?> spriteEnt)
    {
        if (_sprite.LayerMapTryGet(spriteEnt, ControlCaptureVisualLayers.BarBackground, out _, false))
            return;

        var yOffset = _sprite.GetLocalBounds((spriteEnt.Owner, spriteEnt.Comp!)).Height / 2f + 0.15f;

        var bg = _sprite.AddLayer(spriteEnt, BarSprite);
        _sprite.LayerMapSet(spriteEnt, ControlCaptureVisualLayers.BarBackground, bg);
        spriteEnt.Comp!.LayerSetShader(bg, "unshaded");
        _sprite.LayerSetOffset(spriteEnt, bg, new Vector2(0f, yOffset));

        var fill = _sprite.AddTextureLayer(spriteEnt, Texture.White);
        _sprite.LayerMapSet(spriteEnt, ControlCaptureVisualLayers.BarFill, fill);
        spriteEnt.Comp.LayerSetShader(fill, "unshaded");
    }

    private float GetBarYOffset(Entity<SpriteComponent?> spriteEnt)
    {
        if (_sprite.TryGetLayer(spriteEnt, ControlCaptureVisualLayers.BarBackground, out var layer, false))
            return layer.Offset.Y;

        return _sprite.GetLocalBounds((spriteEnt.Owner, spriteEnt.Comp!)).Height / 2f + 0.15f;
    }

    private void RemoveLayer(Entity<SpriteComponent?> spriteEnt, ControlCaptureVisualLayers key)
    {
        if (!_sprite.LayerMapTryGet(spriteEnt, key, out var layer, false))
            return;

        _sprite.RemoveLayer(spriteEnt, layer);
    }

    private static Color OwnerColor(ControlTeam? owner)
    {
        return owner switch
        {
            ControlTeam.TeamA => Color.FromHex("#cc3333"),
            ControlTeam.TeamB => Color.FromHex("#3377cc"),
            _ => Color.FromHex("#888888"),
        };
    }

    private static Color GetProgressColor(ControlCapturePointComponent point)
    {
        if (point.VisualState == ControlCaptureState.Contested)
            return Color.Goldenrod;

        return point.CapturingTeam switch
        {
            ControlTeam.TeamA => Color.DarkRed,
            ControlTeam.TeamB => Color.DarkBlue,
            _ => OwnerColor(point.Owner),
        };
    }
}
