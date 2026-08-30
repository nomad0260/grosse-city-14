#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Grosse.Assault;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Assault.Components;
using Content.Shared.CCVar;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class AssaultMapTest : GameTest
{
    private const string PoolId = "AssaultMapPool";
    private const string PresetId = "City14Assault";
    private const float GateDoorMaxDistance = 0.75f;
    private const float SpawnRadius = 2.5f;

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task PoolMapsHaveRequiredMarkers()
    {
        var pair = Pair;
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryIndex<GameMapPoolPrototype>(PoolId, out var pool),
                $"Missing game map pool {PoolId}");
            Assert.That(pool!.Maps, Is.Not.Empty, $"{PoolId} has no maps");

            foreach (var mapId in pool.Maps)
            {
                Assert.That(proto.TryIndex<GameMapPrototype>(mapId, out var mapProto),
                    $"Pool {PoolId} references unknown map {mapId}");

                var opts = DeserializationOptions.Default with { InitializeMaps = true };
                ticker.LoadGameMap(mapProto!, out var loadedMap, opts);
                try
                {
                    AssertAssaultLayout(server.EntMan, mapId);
                }
                finally
                {
                    mapSys.DeleteMap(loadedMap);
                }
            }
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task RoundStartsOnPoolMapAndOpensGates()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var xformSys = server.System<SharedTransformSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        var dummy = (await pair.Server.AddDummySessions(1))[0];
        await pair.RunTicksSync(5);

        ticker.ToggleReadyAll(true);
        server.CfgMan.SetCVar(CCVars.GameMap, "AssaultTestMisterNobody1");
        await pair.WaitCommand("forcemap AssaultTestMisterNobody1");
        await pair.WaitCommand($"setgamepreset {PresetId}");
        await pair.WaitCommand("startround");
        await pair.RunTicksSync(15);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.JoinedGame));
        Assert.That(client.EntMan.EntityExists(client.AttachedEntity));

        var player = pair.Player!.AttachedEntity!.Value;
        var dummyEnt = dummy.AttachedEntity;
        Assert.That(entMan.EntityExists(player));
        Assert.That(dummyEnt, Is.Not.Null);
        Assert.That(entMan.EntityExists(dummyEnt!.Value));

        AssaultRuleComponent? rule = null;
        await server.WaitAssertion(() =>
        {
            AssertAssaultLayout(entMan, "round-start map");

            var query = entMan.EntityQueryEnumerator<AssaultRuleComponent>();
            Assert.That(query.MoveNext(out _, out var foundRule), "City14Assault rule did not start");
            Assert.That(foundRule, Is.Not.Null);
            var started = foundRule!;
            rule = started;
            Assert.That(started.Phase, Is.EqualTo(AssaultPhase.Prep));
            Assert.That(started.CurrentZone, Is.EqualTo(0));
            Assert.That(started.TotalZones, Is.EqualTo(2), "MisterNobody1 test map is two sequential zones");

            Assert.That(entMan.TryGetComponent(player, out AssaultPlayerComponent? playerComp));
            Assert.That(entMan.TryGetComponent(dummyEnt.Value, out AssaultPlayerComponent? dummyComp));
            Assert.That(playerComp!.Team, Is.Not.EqualTo(dummyComp!.Team), "Teams should auto-balance with two players");

            AssertNearTeamSpawn(entMan, xformSys, player, playerComp.Team, 0);
            AssertNearTeamSpawn(entMan, xformSys, dummyEnt.Value, dummyComp.Team, 0);
            AssertBlockersMatchCurrentSpawns(entMan, 0);

            // Advance to the intermission that opens zone-1 gates.
            started.CurrentZone = 1;
            started.Phase = AssaultPhase.Intermission;
            started.IntermissionEndsAt = timing.CurTime;
        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule!.Phase, Is.EqualTo(AssaultPhase.Attack));
            Assert.That(rule.CurrentZone, Is.EqualTo(1));

            var opened = 0;
            var doors = new List<(EntityUid Uid, TransformComponent Xform, DoorComponent Door)>();
            var doorQuery = entMan.EntityQueryEnumerator<DoorComponent, TransformComponent>();
            while (doorQuery.MoveNext(out var uid, out var door, out var xform))
            {
                doors.Add((uid, xform, door));
            }

            var gateQuery = entMan.EntityQueryEnumerator<AssaultGateComponent, TransformComponent>();
            while (gateQuery.MoveNext(out var gateUid, out var gate, out var gateXform))
            {
                if (gate.UnlocksForZone != 1)
                    continue;

                Assert.That(gate.Opened, Is.True, $"Gate {entMan.ToPrettyString(gateUid)} for zone 1 did not open");

                var door = FindGateDoor(entMan, gateUid, gateXform, doors);
                Assert.That(door, Is.Not.Null, $"No door found for opened gate {entMan.ToPrettyString(gateUid)}");
                Assert.That(door!.Value.Door.State,
                    Is.AnyOf(DoorState.Opening, DoorState.Open),
                    $"Door {entMan.ToPrettyString(door.Value.Uid)} for zone 1 is {door.Value.Door.State}");
                opened++;
            }

            Assert.That(opened, Is.GreaterThan(0), "No zone-1 gates opened");
        });
    }

    private static void AssertAssaultLayout(IEntityManager entMan, string context)
    {
        var captures = new Dictionary<int, int>();
        var capQuery = entMan.EntityQueryEnumerator<AssaultCapturePointComponent>();
        while (capQuery.MoveNext(out _, out var point))
            captures[point.ZoneIndex] = captures.GetValueOrDefault(point.ZoneIndex) + 1;

        Assert.That(captures, Is.Not.Empty, $"{context}: no AssaultCapturePoint markers");
        var maxZone = captures.Keys.Max();
        Assert.That(maxZone, Is.GreaterThanOrEqualTo(0));
        for (var zone = 0; zone <= maxZone; zone++)
        {
            Assert.That(captures.ContainsKey(zone), Is.True, $"{context}: missing capture point for zone {zone}");
            Assert.That(captures[zone], Is.GreaterThan(0), $"{context}: capture point count for zone {zone}");
        }

        var attackerSpawns = new Dictionary<int, int>();
        var defenderSpawns = new Dictionary<int, int>();
        var spawnQuery = entMan.EntityQueryEnumerator<AssaultSpawnPointComponent>();
        while (spawnQuery.MoveNext(out _, out var spawn))
        {
            var dict = spawn.Team == AssaultTeam.Attackers ? attackerSpawns : defenderSpawns;
            dict[spawn.ZoneIndex] = dict.GetValueOrDefault(spawn.ZoneIndex) + 1;
        }

        Assert.That(attackerSpawns.GetValueOrDefault(0), Is.GreaterThan(0), $"{context}: attackers need zone 0 spawns");
        Assert.That(defenderSpawns.GetValueOrDefault(0), Is.GreaterThan(0), $"{context}: defenders need zone 0 spawns");
        for (var zone = 1; zone <= maxZone; zone++)
        {
            Assert.That(defenderSpawns.GetValueOrDefault(zone), Is.GreaterThan(0),
                $"{context}: defenders need a spawn on zone {zone}");
        }

        var doors = new List<(EntityUid Uid, TransformComponent Xform, DoorComponent Door)>();
        var doorQuery = entMan.EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (doorQuery.MoveNext(out var uid, out var door, out var xform))
            doors.Add((uid, xform, door));

        var zone1Gates = 0;
        var gateQuery = entMan.EntityQueryEnumerator<AssaultGateComponent, TransformComponent>();
        while (gateQuery.MoveNext(out var uid, out var gate, out var xform))
        {
            Assert.That(gate.UnlocksForZone, Is.GreaterThan(0),
                $"{context}: {entMan.ToPrettyString(uid)} unlocks zone 0, which must stay open at round start");
            Assert.That(gate.UnlocksForZone, Is.LessThanOrEqualTo(maxZone),
                $"{context}: {entMan.ToPrettyString(uid)} unlocks zone {gate.UnlocksForZone} but max zone is {maxZone}");
            Assert.That(FindGateDoor(entMan, uid, xform, doors), Is.Not.Null,
                $"{context}: {entMan.ToPrettyString(uid)} is a marker, not a door, and no door sits on the same tile. Put AssaultGate on the airlock/shutter.");
            if (gate.UnlocksForZone == 1)
                zone1Gates++;
        }

        if (maxZone >= 1)
            Assert.That(zone1Gates, Is.GreaterThan(0), $"{context}: two-zone maps need a gate that unlocks zone 1");
    }

    private static void AssertNearTeamSpawn(
        IEntityManager entMan,
        SharedTransformSystem xformSys,
        EntityUid mob,
        AssaultTeam team,
        int zone)
    {
        var mobPos = xformSys.GetWorldPosition(mob);
        var best = float.MaxValue;
        var spawnQuery = entMan.EntityQueryEnumerator<AssaultSpawnPointComponent, TransformComponent>();
        while (spawnQuery.MoveNext(out var uid, out var spawn, out _))
        {
            if (spawn.Team != team || spawn.ZoneIndex != zone)
                continue;

            var dist = Vector2.Distance(mobPos, xformSys.GetWorldPosition(uid));
            if (dist < best)
                best = dist;
        }

        Assert.That(best, Is.LessThanOrEqualTo(SpawnRadius),
            $"{entMan.ToPrettyString(mob)} ({team} zone {zone}) spawned {best:0.00} tiles from the nearest spawn marker");
    }

    private static void AssertBlockersMatchCurrentSpawns(IEntityManager entMan, int currentZone)
    {
        var atkZone = Math.Max(0, currentZone - 1);
        var query = entMan.EntityQueryEnumerator<AssaultSpawnBlockerComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var blocker, out var physics))
        {
            var expected = blocker.Team == AssaultTeam.Attackers
                ? blocker.ZoneIndex == atkZone
                : blocker.ZoneIndex == currentZone;
            Assert.That(physics.CanCollide, Is.EqualTo(expected),
                $"{entMan.ToPrettyString(uid)} blocker team={blocker.Team} zone={blocker.ZoneIndex} collide={physics.CanCollide}, expected {expected}");
        }
    }

    private static (EntityUid Uid, DoorComponent Door)? FindGateDoor(
        IEntityManager entMan,
        EntityUid gateUid,
        TransformComponent gateXform,
        List<(EntityUid Uid, TransformComponent Xform, DoorComponent Door)> doors)
    {
        if (entMan.TryGetComponent(gateUid, out DoorComponent? selfDoor))
            return (gateUid, selfDoor);

        foreach (var (uid, xform, door) in doors)
        {
            if (!gateXform.Coordinates.TryDistance(entMan, xform.Coordinates, out var dist))
                continue;
            if (dist <= GateDoorMaxDistance)
                return (uid, door);
        }

        return null;
    }
}
