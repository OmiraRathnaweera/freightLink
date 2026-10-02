using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Domain;

/// <summary>
/// Single source of truth for allowed <see cref="AgencyStatus"/> transitions. Unlike the other
/// status machines, no agency status is terminal: <see cref="AgencyStatus.Suspended"/> is
/// reversible so a wrongly suspended agency can be restored by an Admin.
/// </summary>
/// <remarks>
/// Only <see cref="AgencyStatus.Active"/> agencies are eligible for matching and may mutate
/// agency data (see <see cref="AgencyStatusGuard"/>; ADR-020), so reactivating a suspended agency
/// to <c>Active</c> is what puts it back into Agent 2's shortlist. Loads already assigned to the
/// agency are never touched by a status change in either direction.
/// <para>
/// A suspended agency may also be restored to <c>Pending</c> or <c>Verified</c> — the right target
/// when it was suspended before it had completed verification/activation, so reactivation can't
/// skip the compliance steps it had not yet passed.
/// </para>
/// </remarks>
public static class AgencyStatusTransitionRules
{
    /// <summary>
    /// Pending -> Verified | Suspended
    /// Verified -> Active | Suspended
    /// Active -> Suspended
    /// Suspended -> Active | Verified | Pending
    /// </summary>
    public static readonly IReadOnlyDictionary<AgencyStatus, IReadOnlySet<AgencyStatus>> AllowedTransitions =
        new Dictionary<AgencyStatus, IReadOnlySet<AgencyStatus>>
        {
            [AgencyStatus.Pending] = new HashSet<AgencyStatus> { AgencyStatus.Verified, AgencyStatus.Suspended },
            [AgencyStatus.Verified] = new HashSet<AgencyStatus> { AgencyStatus.Active, AgencyStatus.Suspended },
            [AgencyStatus.Active] = new HashSet<AgencyStatus> { AgencyStatus.Suspended },
            [AgencyStatus.Suspended] = new HashSet<AgencyStatus> { AgencyStatus.Active, AgencyStatus.Verified, AgencyStatus.Pending }
        };

    /// <summary>Determines if a transition from <paramref name="from"/> to <paramref name="to"/> is permitted.</summary>
    public static bool CanTransition(AgencyStatus from, AgencyStatus to) =>
        AllowedTransitions.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Gets the allowed next statuses for an agency currently in <paramref name="from"/>.</summary>
    public static IReadOnlySet<AgencyStatus> GetAllowedNextStatuses(AgencyStatus from) =>
        AllowedTransitions.TryGetValue(from, out var next) ? next : new HashSet<AgencyStatus>();

    /// <summary>
    /// Validates the transition from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <exception cref="ApiException">
    /// 400 <see cref="ErrorCode.INVALID_AGENCY_STATUS_TRANSITION"/> if the transition is not allowed.
    /// </exception>
    public static void ValidateTransition(AgencyStatus from, AgencyStatus to)
    {
        if (from == to)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.INVALID_AGENCY_STATUS_TRANSITION,
                $"Agency is already in status '{from}'.");
        }

        if (!CanTransition(from, to))
        {
            var allowed = string.Join(", ", GetAllowedNextStatuses(from).Select(s => $"'{s}'"));
            throw new ApiException(
                HttpStatusCode.BadRequest,
                ErrorCode.INVALID_AGENCY_STATUS_TRANSITION,
                $"Invalid agency status transition from '{from}' to '{to}'. Allowed next statuses: {allowed}.");
        }
    }
}
