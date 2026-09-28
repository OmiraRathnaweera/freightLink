using FreightLink.Api.DTOs.Analytics;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Computes the read-only, on-demand system-wide analytics snapshot for the Admin dashboard.
/// This service performs no authentication — the caller has already passed the
/// <c>[Authorize(Roles = "Admin")]</c> gate on <c>AdminAnalyticsController</c> before this is invoked.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Computes the current system-wide analytics summary across loads, agencies, trips, assignments, disputes, invoices, and users.</summary>
    Task<AdminAnalyticsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
