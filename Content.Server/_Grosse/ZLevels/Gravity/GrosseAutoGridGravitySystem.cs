/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Server._Grosse.ZCollapse;
using Content.Shared.Gravity;
using Robust.Shared.Analyzers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Grosse.ZLevels.Gravity;

public sealed partial class GrosseAutoGridGravitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseAutoGridGravityComponent, MapInitEvent>(OnComponentInit);
    }
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Fires when the component is added to the map entity.
    // If the map is already initialized (zLevelsComponentOverrides flow), iterate existing grids.
    // If not yet initialized, GridInitializeEvent handles each grid as it comes up.
    private void OnComponentInit(Entity<GrosseAutoGridGravityComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<MapComponent>(ent, out var mapComp) || !_map.IsInitialized(ent.Owner))
            return;

        foreach (var grid in _map.GetAllGrids(mapComp.MapId))
        {
            EnableGravity(grid.Owner);
        }

        EnableGravity(ent);
    }

    // Fires for every grid that initializes. Handles both map-load time (component already on map)
    // and runtime grid spawning (e.g. shuttles arriving).
    private void OnGridInit(GridInitializeEvent ev)
    {
        var mapUid = Transform(ev.EntityUid).MapUid;
        if (mapUid == null || !HasComp<GrosseAutoGridGravityComponent>(mapUid.Value))
            return;

        EnableGravity(ev.EntityUid);
    }

    private void EnableGravity(EntityUid ent)
    {
        EnsureComp<GrosseGridStabilityComponent>(ent);
        var gravity = EnsureComp<GravityComponent>(ent);
        gravity.Inherent = true;
        gravity.Enabled = true;
        Dirty(ent, gravity);

        var xform = Transform(ent);
        _transform.SetLocalRotation(ent, Angle.Zero, xform);
        xform.NoLocalRotation = true;
    }
}
