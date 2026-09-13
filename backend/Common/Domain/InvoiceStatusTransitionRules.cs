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
    /// Statuses that may only be written by the signature-verified payment-gateway webhook.
    /// Authenticated users must never be permitted to drive an invoice into one of these states
    /// directly; doing so would bypass the gateway's signature verification entirely.
    /// </summary>
    public static readonly IReadOnlySet<InvoiceStatus> GatewayOwnedStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Paid, InvoiceStatus.Failed };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="status"/> may only be set by the payment gateway,
    /// never by an authenticated application user.
    /// </summary>
    public static bool IsGatewayOwned(InvoiceStatus status) => GatewayOwnedStatuses.Contains(status);

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
