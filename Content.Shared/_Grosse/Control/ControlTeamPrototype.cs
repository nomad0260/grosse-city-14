using Content.Shared._Grosse.Assault;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Grosse.Control;

[Prototype]
public sealed partial class ControlTeamPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ControlTeamPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField]
    public LocId Name { get; private set; }

    /// <summary>
    /// Victory points granted each score tick per point this team holds.
    /// Applied onto the rule at round start like Assault captureReward/tickets.
    /// </summary>
    [DataField]
    public int ScorePerHeldPoint { get; private set; } = 10;

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
