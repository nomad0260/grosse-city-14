using Content.Shared._Grosse.Control.Components;
using Content.Shared.Examine;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Grosse.Control;

public abstract partial class SharedControlCapturePointSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ControlCapturePointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ControlCapturePointComponent, ExaminedEvent>(OnExamined);
    }

    protected virtual void OnMapInit(Entity<ControlCapturePointComponent> ent, ref MapInitEvent args)
    {
        var physics = EnsureComp<PhysicsComponent>(ent);
        EnsureComp<FixturesComponent>(ent);

        if (_fixtures.GetFixtureOrNull(ent, ControlConstants.CaptureFixtureId) == null)
        {
            _fixtures.TryCreateFixture(
                ent,
                new PhysShapeCircle(ent.Comp.Radius),
                ControlConstants.CaptureFixtureId,
                hard: false,
                collisionLayer: (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable),
                body: physics);
        }

        _physics.WakeBody(ent, body: physics);
    }

    private void OnExamined(Entity<ControlCapturePointComponent> ent, ref ExaminedEvent args)
    {
        var name = string.IsNullOrEmpty(ent.Comp.PointName)
            ? Loc.GetString("control-capture-unnamed")
            : Loc.GetString(ent.Comp.PointName);
        var percent = (int) (ent.Comp.Progress * 100f);
        if (ent.Comp.OwningTeam is { } owner)
        {
            args.PushMarkup(Loc.GetString("control-capture-examined-owned",
                ("name", name),
                ("owner", Loc.GetString(owner == ControlTeam.TeamA
                    ? "control-team-a"
                    : "control-team-b")),
                ("percent", percent)));
            return;
        }

        args.PushMarkup(Loc.GetString("control-capture-examined",
            ("name", name),
            ("percent", percent)));
    }
}
