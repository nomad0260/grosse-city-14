/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using System.Linq;
using Robust.Shared.Analyzers;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Content.Shared._Grosse.ZLevels.Roof;
using Content.Shared.Light.Components;
using Content.Shared.Maps;

namespace Content.Server._Grosse.ZLevels.Roof;

public sealed partial class GrosseZLevelsRoofSystem
{
    private void InitGrids()
    {
        SubscribeLocalEvent<GrosseZGridNetworkComponent, GrosseZLevelGridNetworkUpdatedEvent>(OnZGridNetworkUpdate);
        SubscribeLocalEvent<GrosseZGridComponent, MapInitEvent>(OnZGridMapInit);
    }
    [Dependency] private EntityQuery<GrosseZGridComponent> _zgridQuery = default!;
    [Dependency] private EntityQuery<GrosseZGridNetworkComponent> _zGridNetworkQuery = default!;

    private void InitGrids()
    {
    }
    private void OnZGridNetworkUpdate(Entity<GrosseZGridNetworkComponent> ent, ref GrosseZLevelGridNetworkUpdatedEvent args)
    {
        RecalculateGridRoofs(ent);
    }
    private void OnZGridMapInit(Entity<GrosseZGridComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<GrosseZLevelRoofComponent>(ent.Owner);
    }

    public void RecalculateGridRoofs(Entity<GrosseZGridNetworkComponent> network)
    {
        _roofMap.Clear();

        var sorted = network.Comp.Grids
            .Select(g => (Grid: g, Depth: ZLevel.TryGetGridZDepth(g)))
            .Where(x => x.Depth.HasValue)
            .OrderByDescending(x => x.Depth!.Value);

        foreach (var (gridUid, _) in sorted)
        {
            RemCompDeferred<ImplicitRoofComponent>(gridUid); //hack but that way we dont need edit vanilla code

            if (!GridQuery.TryComp(gridUid, out var grid))
                continue;
            var roofComp = EnsureComp<RoofComponent>(gridUid);
            var enumerator = Map.GetAllTilesEnumerator(gridUid, grid);

            while (enumerator.MoveNext(out var tileRef))
            {
                var worldTile = ZLevel.GridTileToWorldTile(gridUid, grid, tileRef.Value.GridIndices);

                Roof.SetRoof((gridUid, grid, roofComp),
                    tileRef.Value.GridIndices,
                    _roofMap.Contains(worldTile));

                var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];
                if (!tileDef.Transparent)
                    _roofMap.Add(worldTile);
            }
        }
    }
}
