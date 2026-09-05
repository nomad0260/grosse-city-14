#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Grosse.Control;
using Content.Shared._Grosse.Control;
using Content.Shared._Grosse.Control.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class ControlMapTest : GameTest
{
    private static readonly EntProtoId ControlRuleId = "City14ControlRule";
    private static readonly EntProtoId CaptureConsoleId = "ControlCaptureConsole";
    private static readonly EntProtoId SpawnPointTeamAId = "ControlSpawnPointTeamA";
    private static readonly EntProtoId SpawnPointTeamBId = "ControlSpawnPointTeamB";
    private static readonly EntProtoId GatePrepId = "ControlGatePrep";
    private static readonly EntProtoId SpawnBlockerId = "ControlSpawnBlocker";
    private static readonly EntProtoId ComebackCrateSpawnId = "ControlComebackCrateSpawn";
    private static readonly EntProtoId ComebackCrateId = "CrateControlComeback";
    private static readonly ProtoId<ControlTeamPrototype> TeamAId = "ControlTeamA";
    private static readonly ProtoId<ControlTeamPrototype> TeamBId = "ControlTeamB";
    private static readonly ProtoId<ControlTeamPrototype> RebelsId = "ControlRebels";
    private static readonly ProtoId<ControlTeamPrototype> CombineId = "ControlCombine";

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

            Assert.That(proto.HasIndex(ControlRuleId));
            Assert.That(proto.HasIndex(CaptureConsoleId));
            Assert.That(proto.HasIndex(SpawnPointTeamAId));
            Assert.That(proto.HasIndex(SpawnPointTeamBId));
            Assert.That(proto.HasIndex(GatePrepId));
            Assert.That(proto.HasIndex(SpawnBlockerId));
            Assert.That(proto.HasIndex(ComebackCrateSpawnId));
            Assert.That(proto.HasIndex(ComebackCrateId));
            Assert.That(proto.HasIndex(TeamAId));
            Assert.That(proto.HasIndex(TeamBId));
            Assert.That(proto.HasIndex(RebelsId));
            Assert.That(proto.HasIndex(CombineId));
            Assert.That(proto.HasIndex(ControlPrototypeIds.Preset));
            Assert.That(proto.TryIndex(ControlPrototypeIds.MapPool, out var pool));
            Assert.That(pool!.Maps, Does.Contain(ControlPrototypeIds.StubMap.Id));
            Assert.That(proto.TryIndex(ControlPrototypeIds.StubMap, out var map));
            var config = ControlTeamConfig.FromGameMap(map);
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.TeamA, Is.EqualTo(RebelsId));
            Assert.That(config.TeamB, Is.EqualTo(CombineId));
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

            var console = entMan.SpawnEntity(CaptureConsoleId, new EntityCoordinates(grid, Vector2.Zero));
            Assert.That(entMan.HasComponent<ControlCapturePointComponent>(console));

            var teamA = entMan.SpawnEntity(SpawnPointTeamAId, new EntityCoordinates(grid, new Vector2(2, 0)));
            Assert.That(entMan.GetComponent<ControlSpawnPointComponent>(teamA).Team, Is.EqualTo(ControlTeam.TeamA));

            var teamB = entMan.SpawnEntity(SpawnPointTeamBId, new EntityCoordinates(grid, new Vector2(-2, 0)));
            Assert.That(entMan.GetComponent<ControlSpawnPointComponent>(teamB).Team, Is.EqualTo(ControlTeam.TeamB));

            var blocker = entMan.SpawnEntity(SpawnBlockerId, new EntityCoordinates(grid, new Vector2(4, 0)));
            Assert.That(entMan.HasComponent<ControlSpawnBlockerComponent>(blocker));

            var gate = entMan.SpawnEntity(GatePrepId, new EntityCoordinates(grid, new Vector2(6, 0)));
            Assert.That(entMan.HasComponent<ControlGateComponent>(gate));

            var crate = entMan.SpawnEntity(ComebackCrateSpawnId, new EntityCoordinates(grid, new Vector2(8, 0)));
            Assert.That(entMan.HasComponent<ControlComebackCrateSpawnComponent>(crate));

            mapSys.DeleteMap(mapId);
        });
    }
}
