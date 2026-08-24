using Content.Shared._Grosse.Assault;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Assault;

public sealed class AssaultLobbyChoice
{
    public bool Random;
    public AssaultTeam? Team;
    public ProtoId<AssaultClassPrototype>? Class;
}
