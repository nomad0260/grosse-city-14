namespace Content.Server._Grosse.CivilProtection;

/// <summary>
/// Four-digit civil-protection serial stored on a mind. Rank digits are resolved separately.
/// </summary>
[RegisterComponent]
public sealed partial class CivilProtectionSerialComponent : Component
{
    [DataField]
    public int Serial;
}
