using System.Numerics;
using Content.Shared._Grosse.CommanderTerminal;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.CommanderTerminal;

/// <summary>
/// Draws a small squad pip above a member when the local player wears a GEO mask.
/// The leader uses the same star as the commander map.
/// </summary>
public sealed partial class CommanderGroupIconSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private CommanderGroupIconOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CommanderGroupIconOverlay(EntityManager);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}

public sealed class CommanderGroupIconOverlay : Overlay
{
    private const float MemberSize = 0.18f;
    private const float LeaderSize = 0.3f;

    private static readonly SpriteSpecifier MemberIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_circle.png"));

    private static readonly SpriteSpecifier LeaderIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_star.png"));

    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly SharedCommanderGroupSystem _groups;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private readonly ShaderInstance _unshaded;

    private Texture? _member;
    private Texture? _leader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CommanderGroupIconOverlay(IEntityManager entities)
    {
        _entities = entities;
        _player = IoCManager.Resolve<IPlayerManager>();
        _groups = _entities.System<SharedCommanderGroupSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _sprites = _entities.System<SpriteSystem>();
        _unshaded = IoCManager.Resolve<IPrototypeManager>().Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewer = _player.LocalSession?.AttachedEntity;
        if (viewer == null || !_groups.WearsGroupMask(viewer.Value))
            return;

        _member ??= _sprites.Frame0(MemberIcon);
        _leader ??= _sprites.Frame0(LeaderIcon);

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var members = _entities.EntityQueryEnumerator<CommanderGroupMemberComponent, SpriteComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var sprite, out var xform))
        {
            if (!sprite.Visible || xform.MapID != args.MapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var bounds = _sprites.GetLocalBounds((uid, sprite));
            var size = member.IsLeader ? LeaderSize : MemberSize;
            var center = new Vector2(
                (bounds.Left + bounds.Right) / 2f + sprite.Offset.X,
                bounds.Top + sprite.Offset.Y + size / 2f + 0.05f);

            var rotation = Matrix3Helpers.CreateRotation(-eyeRot);
            var translation = Matrix3Helpers.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotation, translation));
            handle.UseShader(_unshaded);

            var box = Box2.FromDimensions(center - new Vector2(size, size) / 2f, new Vector2(size, size));
            handle.DrawTextureRect(
                member.IsLeader ? _leader : _member,
                box,
                CommanderGroupColors.ForIndex(member.ColorIndex));
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
