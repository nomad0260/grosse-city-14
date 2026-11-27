using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Энергетический распределитель: задаёт нагрузку на реактор и забирает его выработку.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReactorDistributorComponent : Component
{
    /// <summary>Потребление сети в кВт.</summary>
    [DataField]
    public float Load = 800f;

    [DataField]
    public float LinkRange = 20f;

    /// <summary>Исправность распределителя; ремонтируется сварочным аппаратом.</summary>
    [DataField]
    public float Integrity = 1f;
}
