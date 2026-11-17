#nullable enable

using System.Collections.Generic;
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
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Grosse;

[TestFixture]
public sealed class RationTerminalTest : GameTest
{
    private static readonly ProtoId<JobPrototype> CitizenJob = "Citizen";
    private static readonly ProtoId<JobPrototype> ProtectionJob = "CivilProtactionUnit";

    /// <summary>
    /// Every <c>RationQuota</c> entry is a job the terminal actually issues, at the count written in the prototype.
    /// Jobs left out of the quota stay off the list.
    /// </summary>
    [Test]
    public async Task StationQuotasAreIssued()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var failures = new List<string>();

        await server.WaitAssertion(() =>
        {
            var ent = server.EntMan;
            var terminal = ent.System<RationTerminalSystem>();
            var minds = ent.System<MindSystem>();
            var roles = ent.System<SharedRoleSystem>();
            var meta = ent.System<MetaDataSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var stations = 0;

            foreach (var proto in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || Pair.IsTestPrototype(proto))
                    continue;

                if (!proto.TryGetComponent(out RationQuotaComponent? source, factory))
                    continue;

                stations++;
                Note(failures, source.Quotas.Count > 0, $"{proto.ID} RationQuota is empty");
                var quota = CopyQuota(source);
                var expected = new List<(EntityUid Body, string Job, int Count)>();

                foreach (var (job, count) in quota.Quotas)
                {
                    Note(failures, count >= 0 && count <= RationQuotaComponent.MaxCoupons,
                        $"{proto.ID} sets {job} to {count}, but a quota must be from 0 to {RationQuotaComponent.MaxCoupons}");

                    if (!prototypes.HasIndex(job))
                    {
                        failures.Add($"{proto.ID} lists {job}, but that job does not exist");
                        continue;
                    }

                    expected.Add((SpawnJob(ent, minds, roles, meta, map.GridCoords, job), job.Id, count));
                }

                var outsiders = new List<(EntityUid Body, string Job)>();
                if (!quota.Quotas.ContainsKey(CitizenJob))
                    outsiders.Add((SpawnJob(ent, minds, roles, meta, map.GridCoords, CitizenJob), CitizenJob.Id));
                if (!quota.Quotas.ContainsKey(ProtectionJob))
                    outsiders.Add((SpawnJob(ent, minds, roles, meta, map.GridCoords, ProtectionJob), ProtectionJob.Id));

                var listed = terminal.CollectRecipients(quota);
                foreach (var (body, job, count) in expected)
                {
                    var match = listed.FirstOrDefault(recipient => recipient.Body == body);
                    if (match == null)
                        failures.Add($"{proto.ID} sets {job} to {count}, but the terminal did not issue that ration");
                    else if (match.Count != count)
                        failures.Add($"{proto.ID} sets {job} to {count}, but the terminal issued {match.Count}");
                }

                foreach (var (body, job) in outsiders)
                {
                    Note(failures, listed.All(recipient => recipient.Body != body),
                        $"{job} is not in {proto.ID} RationQuota but was issued a ration");
                }
            }

            Note(failures, stations > 0, "no entity prototype has RationQuota");
        });

        Report(failures);
    }

    [Test]
    public async Task QuotasIssueEnvelopesAndPenalizeTheWrongOpener()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var failures = new List<string>();

        await server.WaitAssertion(() =>
        {
            var ent = server.EntMan;
            var terminal = ent.System<RationTerminalSystem>();
            var envelopes = ent.System<RationEnvelopeSystem>();
            var hands = ent.System<SharedHandsSystem>();
            var slots = ent.System<ItemSlotsSystem>();
            var containers = ent.System<SharedContainerSystem>();

            var single = terminal.IssueEnvelopes(null, map.GridCoords, new[]
            {
                new RationRecipient(default, "Иван", "Worker", "Разнорабочий", 2, "print-ivan"),
            });
            Note(failures, single.Count == 1, $"one recipient produced {single.Count} items, expected one envelope");
            if (single.Count != 1)
                return;

            var lone = single[0];
            Note(failures, ent.GetComponent<MetaDataComponent>(lone).EntityPrototype?.ID == "RationEnvelope",
                "a single ration was not a RationEnvelope");
            Note(failures, !containers.IsEntityInContainer(lone), "the single envelope was packed into a container");
            Note(failures, ent.GetComponent<EnvelopeComponent>(lone).State == EnvelopeComponent.EnvelopeState.Sealed,
                "the single envelope was not sealed");
            if (TryCouponCount(ent, slots, lone, failures, "the single envelope", out var loneCount))
                Note(failures, loneCount == 2, $"the single envelope had {loneCount} coupons, expected 2");
            Note(failures, ent.GetComponent<PaperComponent>(lone).Content.Contains("Иван"),
                "the single envelope was not addressed to Иван");

            var packed = terminal.IssueEnvelopes(null, map.GridCoords, new[]
            {
                new RationRecipient(default, "Анна", "Citizen", "Гражданин", 1, "print-anna"),
                new RationRecipient(default, "Пётр", "UnionMiner", "Шахтёр", 3, "print-petr"),
            });
            Note(failures, packed.Count == 1, $"two envelopes produced {packed.Count} items, expected one box");
            if (packed.Count != 1)
                return;

            var box = packed[0];
            Note(failures, ent.GetComponent<MetaDataComponent>(box).EntityPrototype?.ID == "BoxCardboard",
                "two envelopes were not packed into a BoxCardboard");
            var storage = containers.GetContainer(box, "storagebase");
            Note(failures, storage.ContainedEntities.Count == 2,
                $"the box held {storage.ContainedEntities.Count} envelopes, expected 2");

            var packedCounts = new List<int>();
            foreach (var child in storage.ContainedEntities)
            {
                Note(failures, ent.GetComponent<EnvelopeComponent>(child).State == EnvelopeComponent.EnvelopeState.Sealed,
                    "a boxed envelope was not sealed");
                if (TryCouponCount(ent, slots, child, failures, "a boxed envelope", out var childCount))
                    packedCounts.Add(childCount);
            }

            packedCounts.Sort();
            Note(failures, packedCounts.Count == 2 && packedCounts[0] == 1 && packedCounts[1] == 3,
                $"boxed coupon counts were {string.Join(", ", packedCounts)}, expected 1 and 3");

            var owner = ent.SpawnEntity("MobHuman", map.GridCoords);
            var stranger = ent.SpawnEntity("MobHuman", map.GridCoords);
            ent.EnsureComponent<FingerprintComponent>(owner).Fingerprint = "print-owner";
            ent.EnsureComponent<FingerprintComponent>(stranger).Fingerprint = "print-stranger";

            var full = envelopes.SpawnFilledEnvelope(map.GridCoords, "Хозяин", "Разнорабочий", 3, "print-owner");
            Note(failures, full != null, "could not spawn the owner's envelope");
            if (full == null)
                return;

            envelopes.CompleteRecipientOpen(full.Value, owner);
            Note(failures, ent.GetComponent<EnvelopeComponent>(full.Value).State == EnvelopeComponent.EnvelopeState.Torn,
                "the owner's envelope was not torn open");
            if (TryHeldCouponCount(ent, hands, owner, failures, "the owner", out var ownerHeld))
                Note(failures, ownerHeld == 3, $"the owner held {ownerHeld} coupons, expected 3");

            var stolen = envelopes.SpawnFilledEnvelope(map.GridCoords, "Хозяин", "Разнорабочий", 3, "print-owner");
            Note(failures, stolen != null, "could not spawn the stolen envelope");
            if (stolen == null)
                return;

            envelopes.OpenForced(stolen.Value, stranger);
            Note(failures, ent.GetComponent<EnvelopeComponent>(stolen.Value).State == EnvelopeComponent.EnvelopeState.Torn,
                "the stolen envelope was not torn open");
            if (TryHeldCouponCount(ent, hands, stranger, failures, "the stranger", out var strangerHeld))
                Note(failures, strangerHeld == 1, $"the stranger held {strangerHeld} coupons, expected 1");

            var heldBefore = hands.EnumerateHeld(stranger).Count();
            var ruined = envelopes.SpawnFilledEnvelope(map.GridCoords, "Хозяин", "Гражданин", 1, "print-owner");
            Note(failures, ruined != null, "could not spawn the one-coupon envelope");
            if (ruined == null)
                return;

            var ruinedId = ruined.Value;
            envelopes.OpenForced(ruinedId, stranger);
            Note(failures, ent.GetComponent<EnvelopeComponent>(ruinedId).State == EnvelopeComponent.EnvelopeState.Torn,
                "the one-coupon envelope was not torn open");
            Note(failures, hands.EnumerateHeld(stranger).Count() == heldBefore,
                "opening a one-coupon envelope put another item in the stranger's hands");
            if (slots.TryGetSlot(ruinedId, "letter_slot", out var ruinedSlot) && ruinedSlot.Item is { } leftover && !ent.Deleted(leftover))
            {
                var left = ent.GetComponent<StackComponent>(leftover).Count;
                Note(failures, left == 0, $"the ruined envelope still holds {left} coupons");
            }
        });

        Report(failures);
    }

    private static EntityUid SpawnJob(
        IEntityManager ent,
        MindSystem minds,
        SharedRoleSystem roles,
        MetaDataSystem meta,
        EntityCoordinates coords,
        ProtoId<JobPrototype> job)
    {
        var body = ent.SpawnEntity("MobHuman", coords);
        meta.SetEntityName(body, job.Id);
        var mind = minds.CreateMind(null, job.Id);
        minds.TransferTo(mind.Owner, body, ghostCheckOverride: true, createGhost: false);
        roles.MindAddJobRole(mind.Owner, silent: true, jobPrototype: job.Id);
        return body;
    }

    private static RationQuotaComponent CopyQuota(RationQuotaComponent source)
    {
        var quota = new RationQuotaComponent();
        foreach (var (job, count) in source.Quotas)
            quota.Quotas[job] = count;

        return quota;
    }

    /// <summary>
    /// Records a failed check without calling NUnit. Assertions inside <c>WaitAssertion</c> run on the
    /// pooled server thread and are filed on whichever test first started that pair.
    /// </summary>
    private static void Note(List<string> failures, bool ok, string message)
    {
        if (!ok)
            failures.Add(message);
    }

    /// <summary>
    /// Fails on this test's thread so GitHub names <see cref="RationTerminalTest"/> and prints every complaint.
    /// </summary>
    private static void Report(List<string> failures)
    {
        if (failures.Count > 0)
            Assert.Fail(string.Join("\n", failures));
    }

    private static bool TryCouponCount(
        IEntityManager ent,
        ItemSlotsSystem slots,
        EntityUid envelope,
        List<string> failures,
        string label,
        out int count)
    {
        count = 0;
        if (!slots.TryGetSlot(envelope, "letter_slot", out var slot) || slot?.Item is not { } item)
        {
            failures.Add($"{label} has no coupon stack");
            return false;
        }

        count = ent.GetComponent<StackComponent>(item).Count;
        return true;
    }

    private static bool TryHeldCouponCount(
        IEntityManager ent,
        SharedHandsSystem hands,
        EntityUid mob,
        List<string> failures,
        string who,
        out int count)
    {
        count = 0;
        var held = hands.EnumerateHeld(mob).ToArray();
        if (held.Length != 1)
        {
            failures.Add($"{who} held {held.Length} items, expected one coupon stack");
            return false;
        }

        count = ent.GetComponent<StackComponent>(held[0]).Count;
        return true;
    }
}
