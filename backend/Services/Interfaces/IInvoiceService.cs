using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Service interface for Invoice CRUD, status transitions, and role-scoped query operations.
/// </summary>
public interface IInvoiceService
{
    /// <summary>Creates a new invoice for a trip.</summary>
    Task<InvoiceResponseDto> CreateAsync(Guid currentUserId, UserRole role, CreateInvoiceDto request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves an invoice by its unique ID with role/ownership enforcement.</summary>
    Task<InvoiceResponseDto> GetByIdAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a paginated list of invoices filtered by role, status, trip, and search query.</summary>
    Task<PagedInvoiceResponseDto> GetListAsync(InvoiceListQueryDto query, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Updates the details of an existing draft invoice.</summary>
    Task<InvoiceResponseDto> UpdateAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceDto request, CancellationToken cancellationToken = default);

    /// <summary>Updates the status of an invoice according to domain state machine rules.</summary>
    Task<InvoiceResponseDto> UpdateStatusAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceStatusDto request, CancellationToken cancellationToken = default);

    /// <summary>Voids/cancels an invoice if it is in a voidable state.</summary>
    Task<InvoiceResponseDto> VoidAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);
}
