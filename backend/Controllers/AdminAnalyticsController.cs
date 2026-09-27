using FreightLink.Api.DTOs.Analytics;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Admin-only system-wide analytics (Section 5 of docs/README.md: Admin's role is agency
/// KYC/verification + system-wide analytics — it does not approve/reject AI-proposed matches).
/// Deliberately thin — delegates entirely to <see cref="IAnalyticsService"/>.
/// </summary>
[ApiController]
[Route("api/v1/admin/analytics")]
[Authorize]
public class AdminAnalyticsController : ControllerBase
{
    /// <summary>
    /// <see cref="Authorize"/>'s <c>Roles</c> property must be a compile-time constant, so it can't
    /// take a <see cref="UserRole"/> value directly — <see langword="nameof"/> is used instead of a
    /// string literal so a renamed enum member fails to compile here rather than silently desyncing.
    /// </summary>
    private const string AdminRole = nameof(UserRole.Admin);

    private readonly IAnalyticsService _analyticsService;

    /// <summary>Creates the controller with its injected analytics service.</summary>
    public AdminAnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>Computes the current system-wide analytics summary.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the <see cref="AdminAnalyticsSummaryDto"/>.</returns>
    [HttpGet("summary")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<AdminAnalyticsSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _analyticsService.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }
}
