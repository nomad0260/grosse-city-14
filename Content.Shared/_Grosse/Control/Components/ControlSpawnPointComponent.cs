using Content.Shared._Grosse.Pvp;

namespace Content.Shared._Grosse.Control.Components;

[RegisterComponent]
public sealed partial class ControlSpawnPointComponent : Component
{
    [DataField]
    public PvpTeam Team;
}
