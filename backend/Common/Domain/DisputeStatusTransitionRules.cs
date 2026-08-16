using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Domain;

/// <summary>
/// Single source of truth for allowed <see cref="DisputeStatus"/> transitions and lifecycle capabilities.
/// </summary>
public static class DisputeStatusTransitionRules
{
    /// <summary>Statuses a dispute can be created directly into.</summary>
    public static readonly IReadOnlySet<DisputeStatus> CreatableInitialStatuses =
        new HashSet<DisputeStatus> { DisputeStatus.Open };

    /// <summary>Statuses where dispute content (category, description) can be edited by the raiser.</summary>
    public static readonly IReadOnlySet<DisputeStatus> EditableStatuses =
        new HashSet<DisputeStatus> { DisputeStatus.Open };

    /// <summary>Statuses from which a dispute can be resolved or rejected.</summary>
    public static readonly IReadOnlySet<DisputeStatus> ResolvableStatuses =
        new HashSet<DisputeStatus> { DisputeStatus.Open, DisputeStatus.UnderReview };

    /// <summary>The legal state machine graph for dispute status transitions.</summary>
    public static readonly IReadOnlyDictionary<DisputeStatus, IReadOnlySet<DisputeStatus>> AllowedTransitions =
        new Dictionary<DisputeStatus, IReadOnlySet<DisputeStatus>>
        {
            [DisputeStatus.Open] = new HashSet<DisputeStatus> { DisputeStatus.UnderReview, DisputeStatus.Resolved, DisputeStatus.Rejected },
            [DisputeStatus.UnderReview] = new HashSet<DisputeStatus> { DisputeStatus.Resolved, DisputeStatus.Rejected },
            [DisputeStatus.Resolved] = new HashSet<DisputeStatus>(),
            [DisputeStatus.Rejected] = new HashSet<DisputeStatus>()
        };

    /// <summary>Determines if creation directly into <paramref name="initialStatus"/> is allowed.</summary>
    public static bool CanCreateAs(DisputeStatus initialStatus) => CreatableInitialStatuses.Contains(initialStatus);

    /// <summary>Determines if a dispute in <paramref name="currentStatus"/> can be edited.</summary>
    public static bool CanEdit(DisputeStatus currentStatus) => EditableStatuses.Contains(currentStatus);

    /// <summary>Determines if a dispute in <paramref name="currentStatus"/> can be resolved/rejected.</summary>
    public static bool CanResolve(DisputeStatus currentStatus) => ResolvableStatuses.Contains(currentStatus);

    /// <summary>Determines if a transition from <paramref name="from"/> to <paramref name="to"/> is permitted.</summary>
    public static bool CanTransition(DisputeStatus from, DisputeStatus to) =>
        AllowedTransitions.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Gets the allowed next statuses for a dispute currently in <paramref name="from"/>.</summary>
    public static IReadOnlySet<DisputeStatus> GetAllowedNextStatuses(DisputeStatus from) =>
        AllowedTransitions.TryGetValue(from, out var next) ? next : new HashSet<DisputeStatus>();
}
