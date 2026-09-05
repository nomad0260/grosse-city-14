using Content.Shared._Grosse.Pvp;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class ControlSpawnBlockerComponent : Component
{
    [DataField]
    public PvpTeam Team;
}
