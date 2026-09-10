/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Server._Grosse.ZLevels.Core;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Robust.Shared.Analyzers;
using Robust.Shared.Map.Components;

namespace Content.Server._Grosse.ZLevels.Mapping;

public sealed partial class GrosseZLevelMappingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseZMapComponent, GrosseMapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
        SubscribeLocalEvent<GrosseZMapComponent, MapInitEvent>(OnMapInit);
    }
    [Dependency] private GrosseZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    private void OnAddedIntoZNetwork(Entity<GrosseZMapComponent> ent, ref GrosseMapAddedIntoZNetworkEvent args)
    {
        if (_map.IsInitialized(ent))
            EntityManager.AddComponents(ent, args.Network.Comp.Components);
        else
        {
            var hasInitializedMaps = false;
            foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
            {
                if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
                {
                    hasInitializedMaps = true;
                    break;
                }
            }

            if (hasInitializedMaps)
                _map.InitializeMap(ent.Owner);
        }
    }
    private void OnMapInit(Entity<GrosseZMapComponent> ent, ref MapInitEvent args)
    {
        if (!_zLevels.TryGetMapNetwork(ent, out var network))
            return;

        EntityManager.AddComponents(ent, network.Comp.Components);
    }
}
