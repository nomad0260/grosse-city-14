using System.Numerics;
using Content.Shared._Grosse.CommanderTerminal;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.CommanderTerminal;

public sealed partial class CommanderGroupArrowSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private CommanderGroupArrowOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CommanderGroupArrowOverlay(EntityManager);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}

public sealed class CommanderGroupArrowOverlay : Overlay
{
    private static readonly SpriteSpecifier NorthArrow =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_arrow_north.png"));

    private static readonly SpriteSpecifier SouthArrow =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_arrow_south.png"));

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly SharedCommanderGroupSystem _groups;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;

    private Texture? _north;
    private Texture? _south;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CommanderGroupArrowOverlay(IEntityManager entities)
    {
        _entities = entities;
        _player = IoCManager.Resolve<IPlayerManager>();
        _groups = _entities.System<SharedCommanderGroupSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _sprites = _entities.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewer = _player.LocalSession?.AttachedEntity;
        if (viewer == null)
            return;

        var wearsMask = _groups.WearsGroupMask(viewer.Value);
        _north ??= _sprites.Frame0(NorthArrow);
        _south ??= _sprites.Frame0(SouthArrow);

        var handle = args.WorldHandle;
        var orders = _entities.EntityQueryEnumerator<CommanderGroupOrderComponent, TransformComponent>();
        while (orders.MoveNext(out var uid, out var order, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (uid != viewer && !wearsMask)
                continue;

            var here = _transform.GetWorldPosition(xform);
            var color = _entities.TryGetComponent<CommanderGroupMemberComponent>(uid, out var member)
                ? CommanderGroupColors.ForIndex(member.ColorIndex)
                : Color.White;

            var targetMap = _transform.ToMapCoordinates(_entities.GetCoordinates(order.Coordinates));
            if (targetMap.MapId == xform.MapID)
            {
                var delta = targetMap.Position - here;
                if (delta.LengthSquared() < 2.25f)
                    continue;

                DrawArrow(handle, _north, here, Angle.FromWorldVec(delta) - Angle.FromDegrees(90), color);
                continue;
            }

            var hereDepth = MapDepth(xform.MapUid);
            DrawArrow(handle, order.Depth > hereDepth ? _north : _south, here, Angle.Zero, color);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private int MapDepth(EntityUid? map)
    {
        if (map != null && _entities.TryGetComponent<GrosseZMapComponent>(map.Value, out var zMap))
            return zMap.Depth;

        return 0;
    }

    private static void DrawArrow(DrawingHandleWorld handle, Texture texture, Vector2 feet, Angle angle, Color color)
    {
        const float size = 0.5f;
        var center = feet + new Vector2(0f, 0.95f);
        var box = Box2.FromDimensions(center - new Vector2(size, size) / 2f, new Vector2(size, size));
        handle.DrawTextureRect(texture, new Box2Rotated(box, angle, box.Center), color);
    }
}
