namespace FreightLink.Api.DTOs.Analytics;

/// <summary>
/// System-wide analytics for the Admin dashboard (Section 5 of docs/README.md: Admin's role is
/// narrowed to agency KYC/verification + system-wide analytics, with no AI-match approval authority).
/// A read-only snapshot computed on demand from current data — nothing here is persisted or cached.
/// </summary>
public class AdminAnalyticsSummaryDto
{
    /// <summary>When this snapshot was computed.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    public CategoryCountsDto Loads { get; set; } = new();
    public CategoryCountsDto Agencies { get; set; } = new();
    public CategoryCountsDto Trips { get; set; } = new();
    public CategoryCountsDto Assignments { get; set; } = new();
    public CategoryCountsDto Disputes { get; set; } = new();
    public InvoiceAnalyticsDto Invoices { get; set; } = new();
    public CategoryCountsDto UsersByRole { get; set; } = new();
}

/// <summary>A total plus its breakdown by status/label — the shape reused for every entity in the summary.</summary>
public class CategoryCountsDto
{
    /// <summary>Total row count across every label.</summary>
    public int Total { get; set; }

    /// <summary>Per-label counts. Only labels with at least one row are included.</summary>
    public List<LabelCountDto> ByLabel { get; set; } = new();
}

/// <summary>One (label, count) pair — the label is a status or role name, e.g. <c>"Posted"</c> or <c>"Shipper"</c>.</summary>
public class LabelCountDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Invoice counts plus the two headline revenue figures.</summary>
public class InvoiceAnalyticsDto
{
    public CategoryCountsDto Counts { get; set; } = new();

    /// <summary>Sum of <c>Amount</c> across every invoice that has actually been issued (excludes Draft and Void).</summary>
    public decimal TotalInvoicedAmount { get; set; }

    /// <summary>Sum of <c>Amount</c> across invoices in <c>Paid</c> status only.</summary>
    public decimal TotalPaidAmount { get; set; }
}
