#nullable enable

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Grosse;

[TestFixture]
public sealed class CivilProtectionCallsignTest : GameTest
{
    private const string CharacterName = "Иван Петров";

    [Test]
    public async Task CallsignMatchesRankAndMaskUsesIdName()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession;
        Assert.That(session, Is.Not.Null);

        await Server.WaitAssertion(() =>
        {
            AssertCallsign(map.GridCoords, session!, "CivilProtactionUnit", new Dictionary<string, TimeSpan>(), "05", checkVoice: true);
            AssertCallsign(map.GridCoords, session!, "CivilProtactionUnit", new Dictionary<string, TimeSpan>
            {
                ["JobCivilProtection"] = TimeSpan.FromHours(20),
            }, "04", checkVoice: false);
            AssertCallsign(map.GridCoords, session!, "CivilProtactionUnit02", new Dictionary<string, TimeSpan>(), "02", checkVoice: false);
            AssertCallsign(map.GridCoords, session!, "CivilProtactionUnitOfficer", new Dictionary<string, TimeSpan>(), "01", checkVoice: false);
        });
    }

    private void AssertCallsign(
        EntityCoordinates coords,
        Robust.Shared.Player.ICommonSession session,
        string jobId,
        Dictionary<string, TimeSpan> playTimes,
        string rank,
        bool checkVoice)
    {
        var spawning = SEntMan.System<StationSpawningSystem>();
        var minds = SEntMan.System<MindSystem>();
        var inventory = SEntMan.System<InventorySystem>();
        var masks = SEntMan.System<MaskSystem>();
        var profile = HumanoidCharacterProfile.RandomWithSpecies("Human").WithName(CharacterName);
        var job = SProtoMan.Index<JobPrototype>(jobId);

        var mob = spawning.SpawnPlayerMob(coords, jobId, profile, station: null);
        var mind = minds.CreateMind(null, CharacterName);
        minds.TransferTo(mind.Owner, mob, mind: mind.Comp);
        spawning.ApplyPlayTimeRank(mob, job, station: null, playTimes, profile);

        var spawned = new PlayerSpawnCompleteEvent(mob, session, jobId, lateJoin: false, silent: true, joinOrder: 0, station: EntityUid.Invalid, profile);
        SEntMan.EventBus.RaiseLocalEvent(mob, spawned, broadcast: true);

        Assert.That(SEntMan.GetComponent<MetaDataComponent>(mob).EntityName, Is.EqualTo(CharacterName));
        Assert.That(inventory.TryGetSlotEntity(mob, "id", out var idUid), Is.True);
        var id = SEntMan.GetComponent<IdCardComponent>(idUid!.Value);
        Assert.That(id.FullName, Does.Match($@"^\d{{4}}\.{rank}\.14$"));

        if (!checkVoice || id.FullName == null)
            return;

        Assert.That(VoiceOf(mob), Is.EqualTo(id.FullName));

        Assert.That(inventory.TryGetSlotEntity(mob, "mask", out var maskUid), Is.True);
        var mask = SEntMan.GetComponent<MaskComponent>(maskUid!.Value);
        masks.SetToggled((maskUid.Value, mask), true);
        Assert.That(VoiceOf(mob), Is.EqualTo(CharacterName));

        masks.SetToggled((maskUid.Value, mask), false);
        Assert.That(VoiceOf(mob), Is.EqualTo(id.FullName));

        Assert.That(inventory.TryUnequip(mob, "mask", silent: true, force: true), Is.True);
        Assert.That(VoiceOf(mob), Is.EqualTo(CharacterName));
    }

    private string VoiceOf(EntityUid mob)
    {
        var name = SEntMan.GetComponent<MetaDataComponent>(mob).EntityName;
        var ev = new TransformSpeakerNameEvent(mob, name);
        SEntMan.EventBus.RaiseLocalEvent(mob, ev);
        return ev.VoiceName;
    }
}
