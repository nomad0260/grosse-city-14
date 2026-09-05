using Content.Shared._Grosse.Assault;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class ControlPlayerComponent : Component
{
    [DataField]
    public ControlTeam Team;

    [DataField]
    public ProtoId<AssaultClassPrototype> Class;

    [DataField]
    public NetUserId UserId;
}
