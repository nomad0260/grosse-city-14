#nullable enable

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Grosse.Ration;
using Content.Server.Mind;
using Content.Shared._Grosse.Ration;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Grosse;

[TestFixture]
public sealed class RationTerminalTest : GameTest
{
    [Test]
    public async Task QuotasIssueEnvelopesAndPenalizeTheWrongOpener()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = server.ResolveDependency<IEntityManager>();
            var terminal = ent.System<RationTerminalSystem>();
            var envelopes = ent.System<RationEnvelopeSystem>();
            var minds = ent.System<MindSystem>();
            var roles = ent.System<SharedRoleSystem>();
            var hands = ent.System<SharedHandsSystem>();
            var slots = ent.System<ItemSlotsSystem>();
            var containers = ent.System<SharedContainerSystem>();
            var meta = ent.System<MetaDataSystem>();

            Assert.That(terminal.IsCity14Job("Worker"), Is.True);
            Assert.That(terminal.IsCity14Job("Citizen"), Is.True);
            Assert.That(terminal.IsCity14Job("UnionMiner"), Is.True);
            Assert.That(terminal.IsCity14Job("CivilProtactionUnit"), Is.False);
            Assert.That(terminal.IsCity14Job("Passenger"), Is.False);

            var quota = new RationQuotaComponent();
            quota.Quotas["Worker"] = 2;
            quota.Quotas["Citizen"] = 1;

            var worker = ent.SpawnEntity("MobHuman", map.GridCoords);
            var protection = ent.SpawnEntity("MobHuman", map.GridCoords);
            meta.SetEntityName(worker, "Рацион Рабочий");
            meta.SetEntityName(protection, "Рацион Оборона");

            var workerMind = minds.CreateMind(null, "Рацион Рабочий");
            minds.TransferTo(workerMind.Owner, worker, ghostCheckOverride: true, createGhost: false);
            roles.MindAddJobRole(workerMind.Owner, silent: true, jobPrototype: "Worker");

            var protectionMind = minds.CreateMind(null, "Рацион Оборона");
            minds.TransferTo(protectionMind.Owner, protection, ghostCheckOverride: true, createGhost: false);
            roles.MindAddJobRole(protectionMind.Owner, silent: true, jobPrototype: "CivilProtactionUnit");

            var listed = terminal.CollectRecipients(quota);
            Assert.That(listed.Any(recipient => recipient.Body == worker && recipient.Count == 2), Is.True);
            Assert.That(listed.Any(recipient => recipient.Body == protection), Is.False);

            var single = terminal.IssueEnvelopes(null, map.GridCoords, new[]
            {
                new RationRecipient(default, "Иван", "Worker", "Разнорабочий", 2, "print-ivan"),
            });
            Assert.That(single, Has.Count.EqualTo(1));
            var lone = single[0];
            Assert.That(ent.GetComponent<MetaDataComponent>(lone).EntityPrototype?.ID, Is.EqualTo("RationEnvelope"));
            Assert.That(containers.IsEntityInContainer(lone), Is.False);
            Assert.That(ent.GetComponent<EnvelopeComponent>(lone).State, Is.EqualTo(EnvelopeComponent.EnvelopeState.Sealed));
            Assert.That(CouponCount(ent, slots, lone), Is.EqualTo(2));
            Assert.That(ent.GetComponent<PaperComponent>(lone).Content, Does.Contain("Иван"));

            var packed = terminal.IssueEnvelopes(null, map.GridCoords, new[]
            {
                new RationRecipient(default, "Анна", "Citizen", "Гражданин", 1, "print-anna"),
                new RationRecipient(default, "Пётр", "UnionMiner", "Шахтёр", 3, "print-petr"),
            });
            Assert.That(packed, Has.Count.EqualTo(1));
            var box = packed[0];
            Assert.That(ent.GetComponent<MetaDataComponent>(box).EntityPrototype?.ID, Is.EqualTo("BoxCardboard"));
            var storage = containers.GetContainer(box, "storagebase");
            Assert.That(storage.ContainedEntities, Has.Count.EqualTo(2));
            var packedCounts = storage.ContainedEntities.Select(child =>
            {
                Assert.That(ent.GetComponent<EnvelopeComponent>(child).State, Is.EqualTo(EnvelopeComponent.EnvelopeState.Sealed));
                return CouponCount(ent, slots, child);
            }).ToArray();
            Assert.That(packedCounts, Is.EquivalentTo(new[] { 1, 3 }));

            var owner = ent.SpawnEntity("MobHuman", map.GridCoords);
            var stranger = ent.SpawnEntity("MobHuman", map.GridCoords);
            ent.EnsureComponent<FingerprintComponent>(owner).Fingerprint = "print-owner";
            ent.EnsureComponent<FingerprintComponent>(stranger).Fingerprint = "print-stranger";

            var full = envelopes.SpawnFilledEnvelope(map.GridCoords, "Хозяин", "Разнорабочий", 3, "print-owner");
            Assert.That(full, Is.Not.Null);
            envelopes.CompleteRecipientOpen(full!.Value, owner);
            Assert.That(ent.GetComponent<EnvelopeComponent>(full.Value).State, Is.EqualTo(EnvelopeComponent.EnvelopeState.Torn));
            Assert.That(HeldCouponCount(ent, hands, owner), Is.EqualTo(3));

            var stolen = envelopes.SpawnFilledEnvelope(map.GridCoords, "Хозяин", "Разнорабочий", 3, "print-owner");
            Assert.That(stolen, Is.Not.Null);
            envelopes.OpenForced(stolen!.Value, stranger);
            Assert.That(ent.GetComponent<EnvelopeComponent>(stolen.Value).State, Is.EqualTo(EnvelopeComponent.EnvelopeState.Torn));
            Assert.That(HeldCouponCount(ent, hands, stranger), Is.EqualTo(1));

            var heldBefore = hands.EnumerateHeld(stranger).Count();
            var ruined = envelopes.SpawnFilledEnvelope(map.GridCoords, "Хозяин", "Гражданин", 1, "print-owner");
            Assert.That(ruined, Is.Not.Null);
            var ruinedId = ruined!.Value;
            envelopes.OpenForced(ruinedId, stranger);
            Assert.That(ent.GetComponent<EnvelopeComponent>(ruinedId).State, Is.EqualTo(EnvelopeComponent.EnvelopeState.Torn));
            Assert.That(hands.EnumerateHeld(stranger).Count(), Is.EqualTo(heldBefore));
            if (slots.TryGetSlot(ruinedId, "letter_slot", out var ruinedSlot) && ruinedSlot.Item is { } leftover && !ent.Deleted(leftover))
                Assert.That(ent.GetComponent<StackComponent>(leftover).Count, Is.EqualTo(0));
        });
    }

    private static int CouponCount(IEntityManager ent, ItemSlotsSystem slots, EntityUid envelope)
    {
        Assert.That(slots.TryGetSlot(envelope, "letter_slot", out var slot), Is.True);
        Assert.That(slot!.Item, Is.Not.Null);
        return ent.GetComponent<StackComponent>(slot.Item!.Value).Count;
    }

    private static int HeldCouponCount(IEntityManager ent, SharedHandsSystem hands, EntityUid mob)
    {
        var held = hands.EnumerateHeld(mob).Single();
        return ent.GetComponent<StackComponent>(held).Count;
    }
}
