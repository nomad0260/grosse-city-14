namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent]
public sealed partial class AssaultGateComponent : Component
{
    /// <summary>
    /// Opens when this zone becomes the active attack zone (after the previous one is captured).
    /// </summary>
    [DataField]
    public int UnlocksForZone = 1;

    [ViewVariables]
    public bool Opened;
}
