namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent]
public sealed partial class AssaultSpawnPointComponent : Component
{
    [DataField]
    public AssaultTeam Team;

    [DataField]
    public int ZoneIndex;
}
