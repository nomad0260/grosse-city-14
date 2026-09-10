/*
 * Grosse rewrite of CrystallEdge AddZVelocity on the vanilla EntityEffect stack.
 * Does not copy CE EntityEffect / ARR code.
 */

using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.ZLevels.EntityEffects;

/// <summary>
/// Adds vertical (Z) velocity via <see cref="GrosseSharedZLevelsSystem.AddZVelocity"/>.
/// </summary>
public sealed partial class AddZVelocityEntityEffectSystem : EntityEffectSystem<MetaDataComponent, AddZVelocity>
{
    [Dependency] private readonly GrosseSharedZLevelsSystem _zLevel = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<AddZVelocity> args)
    {
        if (args.Effect.RequiresGround && _zLevel.DistanceToGround(entity.Owner) > 0.1f)
            return;

        _zLevel.AddZVelocity(entity.Owner, args.Effect.Speed * args.Scale);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class AddZVelocity : EntityEffectBase<AddZVelocity>
{
    [DataField(required: true)]
    public float Speed;

    [DataField]
    public bool RequiresGround;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("grosse-entity-effect-guidebook-add-z-velocity", ("speed", Speed));
}
