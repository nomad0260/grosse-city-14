using Content.Shared._Grosse.Assault.Components;
using Content.Shared.Examine;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Grosse.Assault;

public abstract partial class SharedAssaultCapturePointSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultCapturePointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AssaultCapturePointComponent, ExaminedEvent>(OnExamined);
    }

    protected virtual void OnMapInit(Entity<AssaultCapturePointComponent> ent, ref MapInitEvent args)
    {
        var physics = EnsureComp<PhysicsComponent>(ent);
        EnsureComp<FixturesComponent>(ent);

        if (_fixtures.GetFixtureOrNull(ent, AssaultConstants.CaptureFixtureId) == null)
        {
            _fixtures.TryCreateFixture(
                ent,
                new PhysShapeCircle(ent.Comp.Radius),
                AssaultConstants.CaptureFixtureId,
                hard: false,
                collisionLayer: (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable),
                body: physics);
        }

        _physics.WakeBody(ent, body: physics);
    }

    private void OnExamined(Entity<AssaultCapturePointComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Captured)
        {
            args.PushMarkup(Loc.GetString("assault-capture-examined-captured", ("zone", ent.Comp.ZoneIndex + 1)));
            return;
        }

        var percent = (int) (ent.Comp.Progress * 100f);
        args.PushMarkup(Loc.GetString("assault-capture-examined",
            ("zone", ent.Comp.ZoneIndex + 1),
            ("percent", percent)));
    }
}
