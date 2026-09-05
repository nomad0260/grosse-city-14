#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared._Grosse.Control;
using Content.Shared._Grosse.Control.Components;
using Content.Shared._Grosse.Pvp;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class ControlMapTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = true,
        Connected = false,
        InLobby = true,
    };

    [Test]
    public async Task PrototypesAndMapConfigExist()
    {
        var pair = Pair;
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();

            Assert.That(proto.HasIndex<EntityPrototype>("City14ControlRule"));
            Assert.That(proto.HasIndex<EntityPrototype>("ControlCaptureConsole"));
            Assert.That(proto.HasIndex<EntityPrototype>("ControlSpawnPointAttackers"));
            Assert.That(proto.HasIndex<EntityPrototype>("ControlSpawnPointDefenders"));
            Assert.That(proto.HasIndex<EntityPrototype>("ControlGatePrep"));
            Assert.That(proto.HasIndex<EntityPrototype>("ControlSpawnBlocker"));
            Assert.That(proto.HasIndex<EntityPrototype>("ControlComebackCrateSpawn"));
            Assert.That(proto.HasIndex<EntityPrototype>("CrateControlComeback"));
            Assert.That(proto.HasIndex<ControlTeamPrototype>("ControlRebels"));
            Assert.That(proto.HasIndex<ControlTeamPrototype>("ControlCombine"));
            Assert.That(proto.HasIndex<GamePresetPrototype>("City14Control"));
            Assert.That(proto.TryIndex<GameMapPoolPrototype>("ControlMapPool", out var pool));
            Assert.That(pool!.Maps, Does.Contain("ControlStub"));
            Assert.That(proto.TryIndex<GameMapPrototype>("ControlStub", out var map));
            var config = ControlTeamConfig.FromGameMap(map);
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Attackers.Id, Is.EqualTo("ControlRebels"));
            Assert.That(config.Defenders.Id, Is.EqualTo("ControlCombine"));
        });
    }

    [Test]
    public async Task RequiredEntitiesSpawn()
    {
        var pair = Pair;
        var server = pair.Server;
        var mapSys = server.System<SharedMapSystem>();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var grid = mapSys.CreateGridEntity(mapId);

            var console = entMan.SpawnEntity("ControlCaptureConsole", new EntityCoordinates(grid, Vector2.Zero));
            Assert.That(entMan.HasComponent<ControlCapturePointComponent>(console));

            var atk = entMan.SpawnEntity("ControlSpawnPointAttackers", new EntityCoordinates(grid, new Vector2(2, 0)));
            Assert.That(entMan.GetComponent<ControlSpawnPointComponent>(atk).Team, Is.EqualTo(PvpTeam.Attackers));

            var def = entMan.SpawnEntity("ControlSpawnPointDefenders", new EntityCoordinates(grid, new Vector2(-2, 0)));
            Assert.That(entMan.GetComponent<ControlSpawnPointComponent>(def).Team, Is.EqualTo(PvpTeam.Defenders));

            var blocker = entMan.SpawnEntity("ControlSpawnBlocker", new EntityCoordinates(grid, new Vector2(4, 0)));
            Assert.That(entMan.HasComponent<ControlSpawnBlockerComponent>(blocker));

            var gate = entMan.SpawnEntity("ControlGatePrep", new EntityCoordinates(grid, new Vector2(6, 0)));
            Assert.That(entMan.HasComponent<ControlGateComponent>(gate));

            var crate = entMan.SpawnEntity("ControlComebackCrateSpawn", new EntityCoordinates(grid, new Vector2(8, 0)));
            Assert.That(entMan.HasComponent<ControlComebackCrateSpawnComponent>(crate));

            mapSys.DeleteMap(mapId);
        });
    }
}
