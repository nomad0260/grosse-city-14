using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Grosse.DarkMatter;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Grosse.DarkMatter;

/// <summary>
/// Displays the private marker result of a crystallized formula. The server sends positions only
/// to the artifact owner; this system does not derive or expose them to anyone else.
/// </summary>
public sealed partial class ResearchMarkerOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<NetEntity, MarkerData> _markers = new();
    private ResearchMarkerOverlay? _activeOverlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ResearchMarkerEvent>(OnMarkers);
    }

    private void OnMarkers(ResearchMarkerEvent ev)
    {
        if (ev.Positions.Length == 0)
        {
            _markers.Remove(ev.Artifact);
            return;
        }

        _markers[ev.Artifact] = new MarkerData(ev.Positions, _timing.RealTime + TimeSpan.FromSeconds(ev.Duration));

        if (_activeOverlay != null)
            return;

        _activeOverlay = new ResearchMarkerOverlay(this);
        _overlay.AddOverlay(_activeOverlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.RealTime;
        List<NetEntity>? expired = null;
        foreach (var (artifact, data) in _markers)
        {
            if (now < data.Expires)
                continue;

            expired ??= new List<NetEntity>();
            expired.Add(artifact);
        }

        if (expired == null)
            return;

        foreach (var artifact in expired)
            _markers.Remove(artifact);

        if (_markers.Count > 0 || _activeOverlay == null)
            return;

        _overlay.RemoveOverlay(_activeOverlay);
        _activeOverlay = null;
    }

    public bool TryGetPositions(NetEntity artifact, out Vector2[] positions)
    {
        if (_markers.TryGetValue(artifact, out var data))
        {
            positions = data.Positions;
            return true;
        }

        positions = Array.Empty<Vector2>();
        return false;
    }

    public IEnumerable<NetEntity> ActiveArtifacts => _markers.Keys;

    private sealed record MarkerData(Vector2[] Positions, TimeSpan Expires);
}

public sealed class ResearchMarkerOverlay : Overlay
{
    private readonly ResearchMarkerOverlaySystem _system;
    private readonly IEntityManager _entity;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ResearchMarkerOverlay(ResearchMarkerOverlaySystem system)
    {
        _system = system;
        _entity = IoCManager.Resolve<IEntityManager>();
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        foreach (var artifact in _system.ActiveArtifacts)
        {
            if (!_entity.TryGetEntity(artifact, out var artifactUid) ||
                !_entity.TryGetComponent<TransformComponent>(artifactUid.Value, out var artifactXform) ||
                artifactXform.MapID != args.MapId ||
                !_system.TryGetPositions(artifact, out var positions))
            {
                continue;
            }

            var artifactPosition = _transform.GetWorldPosition(artifactXform);
            handle.DrawLine(artifactPosition, artifactPosition + new Vector2(0.3f, 0f), Color.FromHex("#9b7cff"));
            handle.DrawLine(artifactPosition, artifactPosition + new Vector2(0f, 0.3f), Color.FromHex("#9b7cff"));

            foreach (var position in positions)
            {
                var color = Color.FromHex("#ffcf73");
                handle.DrawLine(position + new Vector2(-0.25f, 0f), position + new Vector2(0.25f, 0f), color);
                handle.DrawLine(position + new Vector2(0f, -0.25f), position + new Vector2(0f, 0.25f), color);
                handle.DrawLine(position + new Vector2(-0.18f, -0.18f), position + new Vector2(0.18f, 0.18f), color);
                handle.DrawLine(position + new Vector2(-0.18f, 0.18f), position + new Vector2(0.18f, -0.18f), color);
            }
        }
    }
}
