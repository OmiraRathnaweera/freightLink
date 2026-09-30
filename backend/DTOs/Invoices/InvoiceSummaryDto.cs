namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Admin-only, read-only aggregate cashflow view across all invoices: totals, a per-status
/// breakdown, and a short recent-activity feed.
/// </summary>
public class InvoiceSummaryDto
{
    /// <summary>Sum of TotalAmount across every non-Void invoice.</summary>
    public decimal TotalInvoiced { get; set; }

    /// <summary>Sum of TotalAmount across invoices with Status == Paid.</summary>
    public decimal TotalPaid { get; set; }

    /// <summary>TotalInvoiced minus TotalPaid — amount still outstanding.</summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>Invoice count grouped by status.</summary>
    public InvoiceStatusCountsDto CountByStatus { get; set; } = new();

    /// <summary>The most recently updated invoices, newest first.</summary>
    public List<InvoiceRecentActivityDto> RecentActivity { get; set; } = new();
}

/// <summary>Invoice counts broken down by <see cref="Entities.Enums.InvoiceStatus"/>.</summary>
public class InvoiceStatusCountsDto
{
    /// <summary>Count of invoices in Draft status.</summary>
    public int Draft { get; set; }

    /// <summary>Count of invoices in Issued status.</summary>
    public int Issued { get; set; }

    /// <summary>Count of invoices in PaymentPending status.</summary>
    public int PaymentPending { get; set; }

    /// <summary>Count of invoices in Paid status.</summary>
    public int Paid { get; set; }

    /// <summary>Count of invoices in Failed status.</summary>
    public int Failed { get; set; }

    /// <summary>Count of invoices in Void status.</summary>
    public int Void { get; set; }
}

/// <summary>A single row in the Admin cashflow dashboard's recent-activity feed.</summary>
public class InvoiceRecentActivityDto
{
    /// <summary>The invoice's unique ID.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Human-readable invoice number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Current status, as its string name.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Invoiced total amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>When this invoice row was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
