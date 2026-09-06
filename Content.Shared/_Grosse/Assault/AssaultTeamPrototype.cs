using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Grosse.Assault;

[Prototype]
public sealed partial class AssaultTeamPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<AssaultTeamPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField]
    public LocId Name { get; private set; }

    [DataField]
    public int StartingTickets { get; private set; } = 200;

    [DataField]
    public int CaptureReward { get; private set; }

    [DataField]
    [AlwaysPushInheritance]
    public List<ProtoId<AssaultClassPrototype>> Classes { get; private set; } = new();

    public bool ContainsClass(ProtoId<AssaultClassPrototype> classId)
    {
        foreach (var id in Classes)
        {
            if (id == classId)
                return true;
        }

        return false;
    }
}
