using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Grosse.Assault;

[Prototype]
public sealed partial class AssaultClassPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField]
    public LocId Description { get; private set; } = string.Empty;

    [DataField(required: true)]
    public AssaultTeam Team { get; private set; }

    [DataField(required: true)]
    public int Cost { get; private set; } = 1;

    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> StartingGear { get; private set; }

    [DataField]
    public SpriteSpecifier? Icon { get; private set; }
}
