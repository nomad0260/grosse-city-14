using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Ration;

/// <summary>
/// A sealed ration envelope locked to one recipient's fingerprint.
/// </summary>
[RegisterComponent]
public sealed partial class RationEnvelopeComponent : Component
{
    [DataField]
    public string RecipientName = string.Empty;

    /// <summary>
    /// Coupons placed inside when the envelope was issued.
    /// </summary>
    [DataField]
    public int CouponCount;

    public DoAfterId? TearDoAfter;
}

[Serializable, NetSerializable]
public sealed partial class RationEnvelopeTearDoAfterEvent : SimpleDoAfterEvent
{
}
