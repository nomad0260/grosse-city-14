using WorkPhaseKind = Content.Shared._Grosse.WorkPhase.WorkPhase;

namespace Content.Server._Grosse.WorkPhase;

/// <summary>
/// City work schedule. Independent of the station alert level.
/// </summary>
[RegisterComponent]
public sealed partial class WorkPhaseComponent : Component
{
    [DataField]
    public WorkPhaseKind CurrentPhase = WorkPhaseKind.Free;

    [DataField]
    public TimeSpan WorkDuration = TimeSpan.FromMinutes(35);

    [DataField]
    public TimeSpan FreeDuration = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Absolute game time when the current timed phase ends. Null during curfew.
    /// </summary>
    [ViewVariables]
    public TimeSpan? PhaseEnd;

    /// <summary>
    /// True after the round-start phase has been armed. Separate from <see cref="Component.Initialized"/>.
    /// </summary>
    [ViewVariables]
    public bool Started;
}
