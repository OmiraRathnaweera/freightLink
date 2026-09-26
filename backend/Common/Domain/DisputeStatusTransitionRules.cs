using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Domain;

/// <summary>
/// Single source of truth for allowed <see cref="DisputeStatus"/> transitions and lifecycle rules.
/// Enforces a strict linear state progression: Raised -> UnderReview -> Resolved.
/// Skipping states, reopening, or transitioning backwards is strictly prohibited.
/// </summary>
public static class DisputeStatusTransitionRules
{
    /// <summary>Statuses a dispute can be created directly into (entry point only).</summary>
    public static readonly IReadOnlySet<DisputeStatus> CreatableInitialStatuses =
        new HashSet<DisputeStatus> { DisputeStatus.Raised };

    /// <summary>Statuses where dispute content can be edited by the raiser.</summary>
    public static readonly IReadOnlySet<DisputeStatus> EditableStatuses =
        new HashSet<DisputeStatus> { DisputeStatus.Raised };

    /// <summary>Statuses from which a dispute can be resolved.</summary>
    public static readonly IReadOnlySet<DisputeStatus> ResolvableStatuses =
        new HashSet<DisputeStatus> { DisputeStatus.UnderReview };

    /// <summary>
    /// The strict legal transition graph:
    /// Raised -> UnderReview
    /// UnderReview -> Resolved
    /// Terminal: Resolved
    /// </summary>
    public static readonly IReadOnlyDictionary<DisputeStatus, IReadOnlySet<DisputeStatus>> AllowedTransitions =
        new Dictionary<DisputeStatus, IReadOnlySet<DisputeStatus>>
        {
            [DisputeStatus.Raised] = new HashSet<DisputeStatus> { DisputeStatus.UnderReview },
            [DisputeStatus.UnderReview] = new HashSet<DisputeStatus> { DisputeStatus.Resolved },
            [DisputeStatus.Resolved] = new HashSet<DisputeStatus>(),
            [DisputeStatus.Rejected] = new HashSet<DisputeStatus>()
        };

    /// <summary>Determines if creation directly into <paramref name="initialStatus"/> is allowed.</summary>
    public static bool CanCreateAs(DisputeStatus initialStatus) => CreatableInitialStatuses.Contains(initialStatus);

    /// <summary>Determines if a dispute in <paramref name="currentStatus"/> can be edited.</summary>
    public static bool CanEdit(DisputeStatus currentStatus) => EditableStatuses.Contains(currentStatus);

    /// <summary>Determines if a dispute in <paramref name="currentStatus"/> can be resolved.</summary>
    public static bool CanResolve(DisputeStatus currentStatus) => ResolvableStatuses.Contains(currentStatus);

    /// <summary>Determines if a transition from <paramref name="from"/> to <paramref name="to"/> is permitted.</summary>
    public static bool CanTransition(DisputeStatus from, DisputeStatus to) =>
        AllowedTransitions.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Gets the allowed next statuses for a dispute currently in <paramref name="from"/>.</summary>
    public static IReadOnlySet<DisputeStatus> GetAllowedNextStatuses(DisputeStatus from) =>
        AllowedTransitions.TryGetValue(from, out var next) ? next : new HashSet<DisputeStatus>();

    /// <summary>
    /// Validates the transition from <paramref name="from"/> to <paramref name="to"/>.
    /// Throws an <see cref="ApiException"/> with descriptive messages for invalid attempts,
    /// skipping states, or terminal reopening.
    /// </summary>
    public static void ValidateTransition(DisputeStatus from, DisputeStatus to)
    {
        if (from == to)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.INVALID_DISPUTE_STATUS_TRANSITION,
                $"Dispute is already in status '{from}'.");
        }

        // Terminal state guarding: Resolved disputes cannot be reopened or transitioned backwards
        if (from == DisputeStatus.Resolved || from == DisputeStatus.Rejected)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.DISPUTE_ALREADY_RESOLVED,
                $"Cannot transition dispute from terminal status '{from}' to '{to}'. Reopening or transitioning backwards is strictly prohibited.");
        }

        // Attempting to transition backwards or re-enter Raised
        if (to == DisputeStatus.Raised)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.INVALID_DISPUTE_STATUS_TRANSITION,
                $"Cannot transition dispute from '{from}' to initial status 'Raised'. Re-entering the initial state is strictly prohibited.");
        }

        // Invalid jump: Skipping UnderReview (Raised -> Resolved)
        if (from == DisputeStatus.Raised && to == DisputeStatus.Resolved)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.INVALID_DISPUTE_STATUS_TRANSITION,
                "Direct transition from 'Raised' to 'Resolved' is prohibited. Disputes must first transition to 'UnderReview'.");
        }

        // Check general transition mapping
        if (!CanTransition(from, to))
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.INVALID_DISPUTE_STATUS_TRANSITION,
                $"Invalid dispute status transition from '{from}' to '{to}'. Allowed progression must strictly follow: Raised -> UnderReview -> Resolved.");
        }
    }
}
