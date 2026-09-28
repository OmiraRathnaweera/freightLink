using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Analytics;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAnalyticsService" />
public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the analytics service with its DB context.</summary>
    public AnalyticsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AdminAnalyticsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var loads = await BuildCategoryCountsAsync(_dbContext.Loads.Select(l => l.Status), cancellationToken);
        var agencies = await BuildCategoryCountsAsync(_dbContext.Agencies.Select(a => a.Status), cancellationToken);
        var trips = await BuildCategoryCountsAsync(_dbContext.Trips.Select(t => t.Status), cancellationToken);
        var assignments = await BuildCategoryCountsAsync(_dbContext.Assignments.Select(a => a.Status), cancellationToken);
        var disputes = await BuildCategoryCountsAsync(_dbContext.Disputes.Select(d => d.Status), cancellationToken);
        var usersByRole = await BuildCategoryCountsAsync(_dbContext.Users.Select(u => u.Role), cancellationToken);

        var invoiceCounts = await BuildCategoryCountsAsync(_dbContext.Invoices.Select(i => i.Status), cancellationToken);

        // "Invoiced" excludes Draft (not yet issued to anyone) and Void (retracted) — every other
        // status represents an amount that was actually billed to the recipient at some point.
        var totalInvoicedAmount = await _dbContext.Invoices
            .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Void)
            .SumAsync(i => i.Amount, cancellationToken);
        var totalPaidAmount = await _dbContext.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .SumAsync(i => i.Amount, cancellationToken);

        return new AdminAnalyticsSummaryDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Loads = loads,
            Agencies = agencies,
            Trips = trips,
            Assignments = assignments,
            Disputes = disputes,
            UsersByRole = usersByRole,
            Invoices = new InvoiceAnalyticsDto
            {
                Counts = invoiceCounts,
                TotalInvoicedAmount = totalInvoicedAmount,
                TotalPaidAmount = totalPaidAmount
            }
        };
    }

    /// <summary>
    /// Groups an enum-valued column by its distinct values, entirely DB-side (translates to a plain
    /// <c>SELECT col, COUNT(*) ... GROUP BY col</c>), then converts each enum value to its display
    /// name client-side — the global string-enum conversion (<c>AppDbContext.OnModelCreating</c>)
    /// already round-trips the column as a string, so grouping on the raw enum column and naming it
    /// afterward avoids relying on provider-specific translation of <c>Enum.ToString()</c> inside SQL.
    /// </summary>
    private static async Task<CategoryCountsDto> BuildCategoryCountsAsync<TEnum>(IQueryable<TEnum> source, CancellationToken cancellationToken)
        where TEnum : struct, Enum
    {
        var raw = await source
            .GroupBy(value => value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new CategoryCountsDto
        {
            Total = raw.Sum(r => r.Count),
            ByLabel = raw
                .Select(r => new LabelCountDto { Label = r.Value.ToString(), Count = r.Count })
                .OrderByDescending(l => l.Count)
                .ToList()
        };
    }
}
