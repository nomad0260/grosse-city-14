#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

namespace Content.IntegrationTests.Tests.Access
{
    [TestOf(typeof(ExpireIdCardComponent))]
    public sealed class ExpireIdCardTest : GameTest
    {
        private const string TestExpireIdCard = "TestExpireIdCard";
        private static readonly ProtoId<AccessLevelPrototype> GenpopEnter = "GenpopEnter";
        private static readonly ProtoId<AccessLevelPrototype> GenpopLeave = "GenpopLeave";

        [TestPrototypes]
        private const string Prototypes = $@"
- type: entity
  id: {TestExpireIdCard}
  name: Expire ID Card
  components:
  - type: Access
    tags:
    - GenpopEnter
  - type: ExpireIdCard
    expireMessage: genpop-prisoner-id-expire
    expiredAccess:
    - GenpopLeave
";

        [SidedDependency(Side.Server)] private readonly SharedIdCardSystem _sharedIdCardSystem = null!;

        [Test]
        public async Task TestExpireIdCardResetsAccessTagsWhenExpiring()
        {
            EntityUid ent = default;
            ExpireIdCardComponent expireComp = default!;
            AccessComponent accessComp = default!;
            // ExpireTime is absolute CurTime, not a relative delay. Using wall-clock "2 seconds
            // from zero" flakes once the pooled server has already advanced past that.
            var lifetime = TimeSpan.FromSeconds(2);
            TimeSpan expireTime = default;

            await Pair.Server.WaitPost(() =>
            {
                ent = SSpawn(TestExpireIdCard);
                expireComp = SComp<ExpireIdCardComponent>(ent);
                accessComp = SComp<AccessComponent>(ent);
            });

            // Check that default component values are all correct
            using (Assert.EnterMultipleScope())
            {
                Assert.That(expireComp.Expired, Is.False);
                Assert.That(expireComp.Permanent, Is.False);
                Assert.That(expireComp.ExpireTime, Is.EqualTo(TimeSpan.Zero));
                Assert.That(accessComp.Tags, Is.EqualTo(new HashSet<ProtoId<AccessLevelPrototype>> { GenpopEnter }));
                Assert.That(expireComp.ExpiredAccess, Is.EqualTo(new HashSet<ProtoId<AccessLevelPrototype>> { GenpopLeave }));
                Assert.That(expireComp.ExpireMessage, Is.EqualTo(new LocId("genpop-prisoner-id-expire")));
            }

            await Pair.Server.WaitPost(() =>
            {
                expireTime = SGameTiming.CurTime + lifetime;
                _sharedIdCardSystem.SetExpireTime(ent, expireTime);
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(expireComp.Expired, Is.False);
                Assert.That(expireComp.Permanent, Is.False);
                Assert.That(expireComp.ExpireTime, Is.EqualTo(expireTime));
                Assert.That(accessComp.Tags, Is.EqualTo(new HashSet<ProtoId<AccessLevelPrototype>> { GenpopEnter }));
            }

            // Halfway through the lifetime — still valid.
            await Pair.RunSeconds((float)lifetime.TotalSeconds / 2f);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SGameTiming.CurTime, Is.LessThan(expireTime));
                Assert.That(expireComp.Expired, Is.False);
                Assert.That(accessComp.Tags, Is.EqualTo(new HashSet<ProtoId<AccessLevelPrototype>> { GenpopEnter }));
            }

            // Past expiry — access swapped.
            await Pair.RunSeconds((float)lifetime.TotalSeconds / 2f + 0.25f);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SGameTiming.CurTime, Is.GreaterThanOrEqualTo(expireTime));
                Assert.That(expireComp.Expired, Is.True);
                Assert.That(expireComp.Permanent, Is.False);
                Assert.That(accessComp.Tags, Is.EqualTo(new HashSet<ProtoId<AccessLevelPrototype>> { GenpopLeave }));
            }
        }
    }
}
