using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

/// <summary>
/// A playtime-gated display rank for a <see cref="JobPrototype"/>.
/// Requirements use the same <see cref="JobRequirement"/> types as jobs and loadouts.
/// </summary>
[DataDefinition]
public sealed partial class JobPlayTimeRank
{
    /// <summary>
    /// Display name for this rank (LocId or plain string).
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);

    /// <summary>
    /// Optional ID card (or PDA) entity to equip in the <c>id</c> slot when this rank applies.
    /// </summary>
    [DataField]
    public EntProtoId? IdCard;

    /// <summary>
    /// Optional job icon override for the ID card.
    /// </summary>
    [DataField]
    public ProtoId<JobIconPrototype>? Icon;

    /// <summary>
    /// Two-digit civil-protection callsign rank, for example <c>05</c>.
    /// </summary>
    [DataField]
    public string? Callsign;

    /// <summary>
    /// Playtime / other gates that must all pass for this rank to apply.
    /// Same types as job and loadout requirements.
    /// </summary>
    [DataField]
    public HashSet<JobRequirement>? Requirements;
}

/// <summary>
/// Helpers for resolving <see cref="JobPlayTimeRank"/> from playtime data.
/// </summary>
public static class JobPlayTimeRanks
{
    /// <summary>
    /// Picks the last rank in <see cref="JobPrototype.PlayTimeRanks"/> whose requirements are met.
    /// List order is junior → senior.
    /// </summary>
    public static bool TryGetPlayTimeRank(
        JobPrototype job,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        HumanoidCharacterProfile? profile,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        [NotNullWhen(true)] out JobPlayTimeRank? rank)
    {
        rank = null;

        if (job.PlayTimeRanks is not { Count: > 0 })
            return false;

        foreach (var candidate in job.PlayTimeRanks)
        {
            if (JobRequirements.TryRequirementsMet(
                    candidate.Requirements,
                    playTimes,
                    out _,
                    entManager,
                    protoManager,
                    profile))
            {
                rank = candidate;
            }
        }

        return rank != null;
    }
}
