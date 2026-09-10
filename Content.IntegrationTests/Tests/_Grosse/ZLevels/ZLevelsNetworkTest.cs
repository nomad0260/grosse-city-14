#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Grosse.ZLevels.Core;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Grosse.ZLevels;

[TestFixture]
[TestOf(typeof(GrosseZLevelsSystem))]
[TestOf(typeof(GrosseSharedZLevelsSystem))]
public sealed class ZLevelsNetworkTest : GameTest
{
    private static readonly EntProtoId ZLevelEyeId = "GrosseZLevelEye";
    private static readonly EntProtoId DustEffectId = "GrosseDustEffect";
    private static readonly EntProtoId DustTileEffectId = "GrosseDustTileEffect";
    private static readonly EntProtoId ActionZLevelUpId = "GrosseActionZLevelUp";
    private static readonly EntProtoId ActionZLevelDownId = "GrosseActionZLevelDown";
    private static readonly EntProtoId ActionToggleLookUpId = "GrosseActionToggleLookUp";
    private static readonly EntProtoId ActionZFlightUpId = "GrosseActionZFlightUp";
    private static readonly EntProtoId ActionZFlightDownId = "GrosseActionZFlightDown";
    private static readonly EntProtoId ActionZFlightToggleId = "GrosseActionZFlightToggle";
    private static readonly ProtoId<ShaderPrototype> ZBlurShaderId = "GrosseZBlur";
    private static readonly ProtoId<ContentTileDefinition> SpaceTileId = "Space";
    private static readonly ProtoId<ContentTileDefinition> LatticeTileId = "Lattice";
    private static readonly ProtoId<ContentTileDefinition> TrainLatticeTileId = "TrainLattice";
    private static readonly ProtoId<ContentTileDefinition> FloorGlassTileId = "FloorGlass";
    private static readonly ProtoId<ContentTileDefinition> FloorRGlassTileId = "FloorRGlass";
    private static readonly ProtoId<ContentTileDefinition> FloorSteelTileId = "FloorSteel";

    public override PoolSettings PoolSettings => new() { Connected = false, DummyTicker = false };

    [Test]
    public async Task RequiredPrototypesExist()
    {
        var serverProto = Server.ResolveDependency<IPrototypeManager>();
        var clientProto = Client.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(serverProto.HasIndex(ZLevelEyeId));
                Assert.That(serverProto.HasIndex(DustEffectId));
                Assert.That(serverProto.HasIndex(DustTileEffectId));
                Assert.That(serverProto.HasIndex(ActionZLevelUpId));
                Assert.That(serverProto.HasIndex(ActionZLevelDownId));
                Assert.That(serverProto.HasIndex(ActionToggleLookUpId));
                Assert.That(serverProto.HasIndex(ActionZFlightUpId));
                Assert.That(serverProto.HasIndex(ActionZFlightDownId));
                Assert.That(serverProto.HasIndex(ActionZFlightToggleId));
            });
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(serverProto.Index(SpaceTileId).Transparent);
                Assert.That(serverProto.Index(LatticeTileId).Transparent);
                Assert.That(serverProto.Index(TrainLatticeTileId).Transparent);
                Assert.That(serverProto.Index(FloorGlassTileId).Transparent);
                Assert.That(serverProto.Index(FloorRGlassTileId).Transparent);
                Assert.That(serverProto.Index(FloorSteelTileId).Transparent, Is.False);
            });
        });

        // ShaderPrototype is client-only.
        await Client.WaitAssertion(() =>
        {
            Assert.That(clientProto.HasIndex(ZBlurShaderId));
        });
    }

    [Test]
    public async Task NetworkLinksMapsByDepthAndOffset()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var zLevels = server.System<GrosseZLevelsSystem>();

        EntityUid map0 = default;
        EntityUid map1 = default;
        EntityUid mapNeg1 = default;
        EntityUid network = default;

        await server.WaitAssertion(() =>
        {
            map0 = mapSys.CreateMap(out _);
            map1 = mapSys.CreateMap(out _);
            mapNeg1 = mapSys.CreateMap(out _);

            // Planet-style: map entity is also the grid (CE convention).
            entMan.EnsureComponent<MapGridComponent>(map0);
            entMan.EnsureComponent<MapGridComponent>(map1);
            entMan.EnsureComponent<MapGridComponent>(mapNeg1);

            var net = zLevels.CreateMapNetwork();
            network = net.Owner;

            Assert.That(zLevels.TryAddMapsIntoNetwork(net, new Dictionary<EntityUid, int>
            {
                [mapNeg1] = -1,
                [map0] = 0,
                [map1] = 1,
            }));

            Assert.Multiple(() =>
            {
                Assert.That(entMan.TryGetComponent(map0, out GrosseZMapComponent? z0));
                Assert.That(z0!.Depth, Is.EqualTo(0));
                Assert.That(z0.MapAbove, Is.EqualTo(map1));
                Assert.That(z0.MapBelow, Is.EqualTo(mapNeg1));
                Assert.That(z0.NetworkUid, Is.EqualTo(network));

                Assert.That(entMan.TryGetComponent(map1, out GrosseZMapComponent? z1));
                Assert.That(z1!.Depth, Is.EqualTo(1));
                Assert.That(z1.MapBelow, Is.EqualTo(map0));

                Assert.That(entMan.TryGetComponent(mapNeg1, out GrosseZMapComponent? zNeg));
                Assert.That(zNeg!.Depth, Is.EqualTo(-1));
                Assert.That(zNeg.MapAbove, Is.EqualTo(map0));
            });

            Assert.That(zLevels.TryMapUp(map0, out var above));
            Assert.That(above.Owner, Is.EqualTo(map1));
            Assert.That(zLevels.TryMapDown(map0, out var below));
            Assert.That(below.Owner, Is.EqualTo(mapNeg1));

            Assert.That(zLevels.TryGetZLevelOffset(map0, map1, out var offsetUp));
            Assert.That(offsetUp, Is.EqualTo(1));
            Assert.That(zLevels.TryGetZLevelOffset(map0, mapNeg1, out var offsetDown));
            Assert.That(offsetDown, Is.EqualTo(-1));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(zLevels.TryRemoveMapsFromNetwork(
                (network, entMan.GetComponent<GrosseZMapNetworkComponent>(network)),
                new[] { map1 }));

            Assert.That(entMan.HasComponent<GrosseZMapComponent>(map1), Is.False);
            Assert.That(zLevels.TryMapUp(map0, out _), Is.False);
            Assert.That(zLevels.TryMapDown(map0, out var stillBelow));
            Assert.That(stillBelow.Owner, Is.EqualTo(mapNeg1));
        });
    }

    [Test]
    public async Task TryMoveChangesMapUidBetweenLinkedLevels()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var zLevels = server.System<GrosseZLevelsSystem>();

        await server.WaitAssertion(() =>
        {
            var mapBelow = mapSys.CreateMap(out _);
            var mapMid = mapSys.CreateMap(out _);
            var mapAbove = mapSys.CreateMap(out _);

            foreach (var map in new[] { mapBelow, mapMid, mapAbove })
            {
                var grid = entMan.EnsureComponent<MapGridComponent>(map);
                // Solid floor so movement between levels has somewhere to stand.
                mapSys.SetTile(map, grid, Vector2i.Zero, new Tile(1));
            }

            var net = zLevels.CreateMapNetwork();
            Assert.That(zLevels.TryAddMapsIntoNetwork(net, new Dictionary<EntityUid, int>
            {
                [mapBelow] = -1,
                [mapMid] = 0,
                [mapAbove] = 1,
            }));

            var mob = entMan.Spawn();
            xformSys.SetCoordinates(mob, new EntityCoordinates(mapMid, Vector2.Zero));
            entMan.EnsureComponent<GrosseZPhysicsComponent>(mob);

            Assert.That(xformSys.GetMap(mob), Is.EqualTo(mapMid));

            Assert.That(zLevels.TryMoveUp(mob));
            Assert.That(xformSys.GetMap(mob), Is.EqualTo(mapAbove));

            Assert.That(zLevels.TryMoveDown(mob));
            Assert.That(xformSys.GetMap(mob), Is.EqualTo(mapMid));

            Assert.That(zLevels.TryMoveDown(mob));
            Assert.That(xformSys.GetMap(mob), Is.EqualTo(mapBelow));
        });
    }
}
