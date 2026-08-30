namespace Content.Shared._Grosse.Assault.Components;

[RegisterComponent]
public sealed partial class AssaultGateComponent : Component
{
    /// <summary>
    /// 0 = spawn-exit lock: sealed during prep, opens when prep ends.
    /// N &gt; 0 = opens when that zone becomes active (end of intermission after the previous capture).
    /// </summary>
    [DataField]
    public int UnlocksForZone = 1;

    [ViewVariables]
    public bool Opened;
}
