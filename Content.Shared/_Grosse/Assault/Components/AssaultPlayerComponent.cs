using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent]
public sealed partial class AssaultPlayerComponent : Component
{
    [DataField]
    public AssaultTeam Team;

    [DataField]
    public ProtoId<AssaultClassPrototype> Class;

    [DataField]
    public NetUserId UserId;
}
