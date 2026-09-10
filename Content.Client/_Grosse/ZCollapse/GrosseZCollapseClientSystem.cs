/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Client._Grosse.ZCollapse.Overlays;
using Content.Shared._Grosse.ZCollapse.Events;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Grosse.ZCollapse;

public sealed partial class GrosseZCollapseClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    public Dictionary<NetEntity, Dictionary<Vector2i, int>>? Grids;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GrosseZCollapseOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<GrosseZCollapseOverlaySnapshotEvent>(OnSnapshotUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<GrosseZCollapseDebugOverlay>();
    }

    private void OnOverlayToggled(GrosseZCollapseOverlayToggledEvent ev)
    {
        if (ev.IsEnabled)
            _overlayMan.AddOverlay(new GrosseZCollapseDebugOverlay());
        else
        {
            _overlayMan.RemoveOverlay<GrosseZCollapseDebugOverlay>();
            Grids = null;
        }
    }

    private void OnSnapshotUpdate(GrosseZCollapseOverlaySnapshotEvent ev)
    {
        Grids ??= new Dictionary<NetEntity, Dictionary<Vector2i, int>>();

        foreach (var (grid, tiles) in ev.Grids)
        {
            Grids[grid] = tiles;
        }
    }
}
