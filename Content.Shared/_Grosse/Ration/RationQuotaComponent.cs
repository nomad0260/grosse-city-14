using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Ration;

/// <summary>
/// Round-scoped ration coupon quotas for City14 jobs. Lives on the station, not on a console.
/// </summary>
[RegisterComponent]
public sealed partial class RationQuotaComponent : Component
{
    public const int MaxCoupons = 10;

    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> Quotas = new();
}
