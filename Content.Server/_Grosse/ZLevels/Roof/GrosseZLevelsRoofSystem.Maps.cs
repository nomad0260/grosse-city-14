/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using System.Linq;
using Robust.Shared.Analyzers;
using Content.Server._Grosse.ZLevels.Core;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Maps;

namespace Content.Server._Grosse.ZLevels.Roof;

public sealed partial class GrosseZLevelsRoofSystem
{
    private void InitMaps()
    {
        SubscribeLocalEvent<GrosseZLevelMapNetworkUpdatedEvent>(OnMapNetworkUpdated);
    }

    private void OnMapNetworkUpdated(GrosseZLevelMapNetworkUpdatedEvent args)
    {
        if (!TryComp<GrosseZMapNetworkComponent>(args.Network, out var network))
            return;

        RecalculateMapRoofs((args.Network, network));
    }

    private void RecalculateMapRoofs(Entity<GrosseZMapNetworkComponent> network)
    {
        _roofMap.Clear();

        List<EntityUid> sortedMaps = new();
        foreach (var mapUid in network.Comp.ZLevels
                     .OrderByDescending(kv => kv.Key) // depth sorting
                     .Select(kv => kv.Value)
                     .Where(uid => uid.HasValue)
                     .Select(uid => uid!.Value))
        {
            sortedMaps.Add(mapUid);
        }

        foreach (var map in sortedMaps)
        {
            if (!GridQuery.TryComp(map, out var mapGrid))
                continue;

            var enumerator = Map.GetAllTilesEnumerator(map, mapGrid);
            var roofComp = EnsureComp<RoofComponent>(map);

            while (enumerator.MoveNext(out var tileRef))
            {
                Roof.SetRoof((map, mapGrid, roofComp), tileRef.Value.GridIndices, _roofMap.Contains(tileRef.Value.GridIndices));

                var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];

                if (!GrosseZLevelOpeningCache.IsTransparentTile(tileDef))
                    _roofMap.Add(tileRef.Value.GridIndices);
            }
        }
    }
}
