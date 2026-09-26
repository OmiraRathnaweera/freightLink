using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Domain;

/// <summary>
/// Single source of truth for whether an AgencyStaff caller's agency is allowed to perform
/// operations beyond compliance-document upload/read and agency-profile read. Until an agency
/// reaches <see cref="AgencyStatus.Active"/> (i.e. it is still <c>Pending</c>, <c>Verified</c>-but-
/// not-yet-activated, or <c>Suspended</c>), its staff may only submit compliance documents and view
/// their own agency/vehicle/trip/invoice/dispute data — every mutating action elsewhere (vehicles,
/// trip dispatch/reassignment/status/evidence, invoices, disputes) must call
/// <see cref="EnsureActive"/> before writing.
/// </summary>
/// <remarks>
/// Call sites gate this only for <see cref="UserRole.AgencyStaff"/> callers — Admin bypasses the
/// check entirely, mirroring the existing ownership-check convention (e.g.
/// <c>AgencyService.VerifyAgencyOwnershipAsync</c>) where Admin short-circuits before any
/// agency-specific rule is evaluated.
/// </remarks>
public static class AgencyStatusGuard
{
    /// <summary>Throws unless <paramref name="status"/> is <see cref="AgencyStatus.Active"/>.</summary>
    /// <param name="status">The acting AgencyStaff caller's agency's current status.</param>
    /// <exception cref="ApiException">
    /// 403 <see cref="ErrorCode.AGENCY_NOT_ACTIVE"/> if <paramref name="status"/> is not <see cref="AgencyStatus.Active"/>.
    /// </exception>
    public static void EnsureActive(AgencyStatus status)
    {
        if (status != AgencyStatus.Active)
        {
            throw new ApiException(
                HttpStatusCode.Forbidden,
                ErrorCode.AGENCY_NOT_ACTIVE,
                $"This agency is not yet active (current status: '{status}'). Complete compliance verification and wait for admin activation before performing this action.");
        }
    }
}
