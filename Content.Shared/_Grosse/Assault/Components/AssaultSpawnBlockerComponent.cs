namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent]
public sealed partial class AssaultSpawnBlockerComponent : Component
{
    [DataField]
    public AssaultTeam Team;

    [DataField]
    public int ZoneIndex;
}
