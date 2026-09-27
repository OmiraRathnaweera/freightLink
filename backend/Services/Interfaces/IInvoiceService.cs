using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Service interface for manual Invoice CRUD, status transitions, issuing, voiding, and role-scoped query operations.
/// </summary>
public interface IInvoiceService
{
    /// <summary>Creates a new invoice in Draft or Issued status with manual line items.</summary>
    Task<InvoiceResponseDto> CreateAsync(Guid currentUserId, UserRole role, CreateInvoiceDto request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves an invoice by its unique ID with line items, recipient details, and audit trail.</summary>
    Task<InvoiceResponseDto> GetByIdAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a paginated list of invoices filtered by role, status, date range, recipient, trip, and search query.</summary>
    Task<PagedInvoiceResponseDto> GetListAsync(InvoiceListQueryDto query, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Updates editable fields on a draft invoice. Rejects modifications if status is Issued, Paid, or Voided.</summary>
    Task<InvoiceResponseDto> UpdateAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceDto request, CancellationToken cancellationToken = default);

    /// <summary>Transitions status from Draft to Issued, finalizing totals and locking edits.</summary>
    Task<InvoiceResponseDto> IssueAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Updates the status of an invoice according to domain state machine rules.</summary>
    Task<InvoiceResponseDto> UpdateStatusAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceStatusDto request, CancellationToken cancellationToken = default);

    /// <summary>Voids/cancels an invoice with a mandatory non-empty void reason.</summary>
    Task<InvoiceResponseDto> VoidAsync(Guid invoiceId, Guid currentUserId, UserRole role, string voidReason, CancellationToken cancellationToken = default);

    /// <summary>Voids/cancels an invoice with default reason.</summary>
    Task<InvoiceResponseDto> VoidAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Lists candidate recipients for invoice creation.</summary>
    Task<List<InvoiceRecipientDto>> GetRecipientsAsync(Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an already-uploaded file as the Shipper's proof-of-payment receipt for an Issued
    /// (or previously-submitted) invoice, and advances its status to PaymentPending for Agency review.
    /// </summary>
    Task<InvoiceResponseDto> UploadPaymentProofAsync(Guid invoiceId, Guid currentUserId, UserRole role, UploadPaymentProofDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes out a PaymentPending invoice as Paid (Agency only), after the Agency has reviewed the
    /// Shipper's submitted payment proof. Fails if no payment proof has been submitted yet.
    /// </summary>
    Task<InvoiceResponseDto> ConfirmPaymentAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotency guard for legacy trip-delivered callbacks. Returns existing invoice if one exists,
    /// or safely creates a placeholder invoice if none exists.
    /// </summary>
    Task<InvoiceResponseDto> CreateOnTripDeliveredAsync(Guid tripId, Guid? currentUserId = null, UserRole? role = null, CancellationToken cancellationToken = default);
}
