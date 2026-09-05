using Content.Shared._Grosse.Pvp;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class ControlComebackCrateSpawnComponent : Component
{
    [DataField]
    public PvpTeam Team;
}
