using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Station.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.GameTicking.Rules;

public abstract partial class GameRuleSystem<T> where T: IComponent
{
    protected EntityQueryEnumerator<ActiveGameRuleComponent, T, GameRuleComponent> QueryActiveRules()
    {
        return EntityQueryEnumerator<ActiveGameRuleComponent, T, GameRuleComponent>();
    }

    protected EntityQueryEnumerator<DelayedStartRuleComponent, T, GameRuleComponent> QueryDelayedRules()
    {
        return EntityQueryEnumerator<DelayedStartRuleComponent, T, GameRuleComponent>();
    }

    /// <summary>
    /// Queries all gamerules, regardless of if they're active or not.
    /// </summary>
    protected EntityQueryEnumerator<T, GameRuleComponent> QueryAllRules()
    {
        return EntityQueryEnumerator<T, GameRuleComponent>();
    }

    /// <summary>
    ///     Utility function for finding a random event-eligible station entity
    /// </summary>
    protected bool TryGetRandomStation([NotNullWhen(true)] out EntityUid? station, Func<EntityUid, bool>? filter = null)
    {
        var stations = new ValueList<EntityUid>(Count<StationEventEligibleComponent>());

        filter ??= _ => true;
        var query = AllEntityQuery<StationEventEligibleComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (!filter(uid))
                continue;

            stations.Add(uid);
        }

        if (stations.Count == 0)
        {
            station = null;
            return false;
        }

        // TODO: Engine PR.
        station = stations[RobustRandom.Next(stations.Count)];
        return true;
    }

    protected bool TryFindRandomTile(out Vector2i tile,
        [NotNullWhen(true)] out EntityUid? targetStation,
        out EntityUid targetGrid,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetStation = EntityUid.Invalid;
        targetGrid = EntityUid.Invalid;
        targetCoords = EntityCoordinates.Invalid;
        if (TryGetRandomStation(out targetStation))
        {
            return TryFindRandomTileOnStation((targetStation.Value, Comp<StationDataComponent>(targetStation.Value)),
                out tile,
                out targetGrid,
                out targetCoords);
        }

        return false;
    }

    protected bool TryFindRandomTileOnStation(Entity<StationDataComponent> station,
        out Vector2i tile,
        out EntityUid targetGrid,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetCoords = EntityCoordinates.Invalid;
        targetGrid = EntityUid.Invalid;

        // Weight grid choice by tilecount
        var weights = new Dictionary<Entity<MapGridComponent>, float>();
        foreach (var possibleTarget in station.Comp.Grids)
        {
            if (!TryComp<MapGridComponent>(possibleTarget, out var comp))
                continue;

            weights.Add((possibleTarget, comp), _map.GetAllTiles(possibleTarget, comp).Count());
        }

        if (weights.Count == 0)
        {
            targetGrid = EntityUid.Invalid;
            return false;
        }

        // Prefer a weighted random grid, then fall through other grids if that one has no
        // placeable tiles. Random AABB sampling alone is heisentest-prone on sparse maps
        // (ParadoxCloneSpawn on Saltern in AntagGhostRoleTest).
        (targetGrid, var preferredGrid) = RobustRandom.Pick(weights);
        if (TryFindRandomTileOnGrid(targetGrid, preferredGrid, out tile, out targetCoords))
            return true;

        foreach (var (gridUid, gridComp) in weights.Keys)
        {
            if (gridUid == targetGrid)
                continue;

            if (!TryFindRandomTileOnGrid(gridUid, gridComp, out tile, out targetCoords))
                continue;

            targetGrid = gridUid;
            return true;
        }

        targetGrid = EntityUid.Invalid;
        return false;
    }

    private bool TryFindRandomTileOnGrid(
        EntityUid targetGrid,
        MapGridComponent gridComp,
        out Vector2i tile,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetCoords = EntityCoordinates.Invalid;

        var aabb = gridComp.LocalAABB;
        var mapUid = Transform(targetGrid).MapUid;

        for (var i = 0; i < 25; i++)
        {
            var randomX = RobustRandom.Next((int) aabb.Left, (int) aabb.Right);
            var randomY = RobustRandom.Next((int) aabb.Bottom, (int) aabb.Top);

            tile = new Vector2i(randomX, randomY);
            if (_atmosphere.IsTileSpace(targetGrid, mapUid, tile)
                || _atmosphere.IsTileAirBlockedCached(targetGrid, tile))
            {
                continue;
            }

            targetCoords = _map.GridTileToLocal(targetGrid, gridComp, tile);
            return true;
        }

        // Exhaustive fallback so antag ghost-role tests / events don't flake when sampling misses.
        var candidates = new List<Vector2i>();
        foreach (var t in _map.GetAllTiles(targetGrid, gridComp))
        {
            var indices = t.GridIndices;
            if (_atmosphere.IsTileSpace(targetGrid, mapUid, indices)
                || _atmosphere.IsTileAirBlockedCached(targetGrid, indices))
            {
                continue;
            }

            candidates.Add(indices);
        }

        if (candidates.Count == 0)
            return false;

        tile = candidates[RobustRandom.Next(candidates.Count)];
        targetCoords = _map.GridTileToLocal(targetGrid, gridComp, tile);
        return true;
    }

    protected void ForceEndSelf(EntityUid uid, GameRuleComponent? component = null)
    {
        GameTicker.EndGameRule(uid, component);
    }
}
