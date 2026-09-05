using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Pvp;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class ControlPlayerComponent : Component
{
    [DataField]
    public PvpTeam Team;

    [DataField]
    public ProtoId<AssaultClassPrototype> Class;

    [DataField]
    public NetUserId UserId;
}
