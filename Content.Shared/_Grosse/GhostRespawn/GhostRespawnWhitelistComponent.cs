using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.GhostRespawn;

/// <summary>
/// Jobs a ghost may respawn into on this station, if a slot is still free.
/// </summary>
[RegisterComponent]
public sealed partial class GhostRespawnWhitelistComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();
}
