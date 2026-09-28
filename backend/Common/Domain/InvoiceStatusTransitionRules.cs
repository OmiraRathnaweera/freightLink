using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Domain;

/// <summary>
/// Single source of truth for allowed <see cref="InvoiceStatus"/> transitions and lifecycle capabilities.
/// Encodes which statuses can be set initially, edited, voided, or transitioned.
/// </summary>
public static class InvoiceStatusTransitionRules
{
    /// <summary>Statuses an invoice can be created directly into.</summary>
    public static readonly IReadOnlySet<InvoiceStatus> CreatableInitialStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Draft, InvoiceStatus.Issued };

    /// <summary>Statuses where invoice content (amount, currency, dueDate) can be updated.</summary>
    public static readonly IReadOnlySet<InvoiceStatus> EditableStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Draft };

    /// <summary>Statuses from which an invoice can be voided.</summary>
    public static readonly IReadOnlySet<InvoiceStatus> VoidableStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Draft, InvoiceStatus.Issued, InvoiceStatus.PaymentPending, InvoiceStatus.Failed };

    /// <summary>
    /// Statuses reachable only through a dedicated action, never through the generic
    /// status-update endpoint. <see cref="InvoiceStatus.Paid"/> must go through
    /// <c>InvoiceService.ConfirmPaymentAsync</c>, which requires a Shipper-submitted payment proof
    /// to already be on file; <see cref="InvoiceStatus.Failed"/> has no dedicated action today and is
    /// simply unreachable.
    /// </summary>
    public static readonly IReadOnlySet<InvoiceStatus> DedicatedActionOnlyStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Paid, InvoiceStatus.Failed };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="status"/> may only be reached via a dedicated
    /// service action, never by an authenticated user through the generic status-update endpoint.
    /// </summary>
    public static bool RequiresDedicatedAction(InvoiceStatus status) => DedicatedActionOnlyStatuses.Contains(status);

    /// <summary>The legal state machine graph for invoice status transitions.</summary>
    public static readonly IReadOnlyDictionary<InvoiceStatus, IReadOnlySet<InvoiceStatus>> AllowedTransitions =
        new Dictionary<InvoiceStatus, IReadOnlySet<InvoiceStatus>>
        {
            [InvoiceStatus.Draft] = new HashSet<InvoiceStatus> { InvoiceStatus.Issued, InvoiceStatus.Void },
            [InvoiceStatus.Issued] = new HashSet<InvoiceStatus> { InvoiceStatus.PaymentPending, InvoiceStatus.Paid, InvoiceStatus.Failed, InvoiceStatus.Void },
            [InvoiceStatus.PaymentPending] = new HashSet<InvoiceStatus> { InvoiceStatus.Paid, InvoiceStatus.Failed, InvoiceStatus.Void },
            [InvoiceStatus.Failed] = new HashSet<InvoiceStatus> { InvoiceStatus.PaymentPending, InvoiceStatus.Paid, InvoiceStatus.Void },
            [InvoiceStatus.Paid] = new HashSet<InvoiceStatus>(),
            [InvoiceStatus.Void] = new HashSet<InvoiceStatus>()
        };

    /// <summary>Determines if creation directly into <paramref name="initialStatus"/> is allowed.</summary>
    public static bool CanCreateAs(InvoiceStatus initialStatus) => CreatableInitialStatuses.Contains(initialStatus);

    /// <summary>Determines if an invoice in <paramref name="currentStatus"/> can be edited.</summary>
    public static bool CanEdit(InvoiceStatus currentStatus) => EditableStatuses.Contains(currentStatus);

    /// <summary>Determines if an invoice in <paramref name="currentStatus"/> can be voided.</summary>
    public static bool CanVoid(InvoiceStatus currentStatus) => VoidableStatuses.Contains(currentStatus);

    /// <summary>Determines if a transition from <paramref name="from"/> to <paramref name="to"/> is permitted.</summary>
    public static bool CanTransition(InvoiceStatus from, InvoiceStatus to) =>
        AllowedTransitions.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Gets the allowed next statuses for an invoice currently in <paramref name="from"/>.</summary>
    public static IReadOnlySet<InvoiceStatus> GetAllowedNextStatuses(InvoiceStatus from) =>
        AllowedTransitions.TryGetValue(from, out var next) ? next : new HashSet<InvoiceStatus>();
}
