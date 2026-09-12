using System.Net;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <summary>
/// Service implementing <see cref="IInvoiceService"/> for Invoice operations with role-based access control.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private const int MaxInvoiceNumberGenerationAttempts = 5;
    private readonly AppDbContext _dbContext;

    /// <summary>Initializes a new instance of <see cref="InvoiceService"/>.</summary>
    public InvoiceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> CreateAsync(Guid currentUserId, UserRole role, CreateInvoiceDto request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_INVOICE_AMOUNT, "Invoice amount must be greater than zero.");
        }

        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
                    .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(t => t.TripId == request.TripId, cancellationToken);

        if (trip is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, $"Trip '{request.TripId}' was not found.");
        }

        // Verify caller has permission to generate an invoice for this trip
        EnforceTripPartyAuthorization(trip, currentUserId, role);

        // Check if an invoice already exists for this trip (1-to-1 relationship)
        var exists = await _dbContext.Invoices.AnyAsync(i => i.TripId == request.TripId, cancellationToken);
        if (exists)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP, $"An invoice already exists for trip '{request.TripId}'.");
        }

        var initialStatus = request.IssueImmediately ? InvoiceStatus.Issued : InvoiceStatus.Draft;
        if (!InvoiceStatusTransitionRules.CanCreateAs(initialStatus))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION, $"An invoice cannot be created directly into status '{initialStatus}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            TripId = request.TripId,
            InvoiceNumber = GenerateInvoiceNumber(),
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "LKR" : request.Currency.Trim().ToUpperInvariant(),
            Status = initialStatus,
            IssuedAt = now,
            DueDate = request.DueDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Invoices.Add(invoice);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                // 23505 = unique_violation. Two separate unique constraints can fire here:
                //
                //   uq_invoice_number  – InvoiceNumber collision (random suffix, very rare).
                //                        Safe to regenerate and retry.
                //
                //   uq_invoice_trip_id – A concurrent request for the same TripId committed
                //                        between the AnyAsync check above (TOCTOU gap) and this
                //                        SaveChanges call.  Retrying with a new number would
                //                        fail again on the same constraint, so surface the
                //                        correct business error immediately.
                //
                // Any other constraint name is unexpected; re-throw so it surfaces as an
                // unhandled exception (→ 500) rather than masking the real cause.
                if (pgEx.ConstraintName == "uq_invoice_trip_id")
                {
                    throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP,
                        $"An invoice already exists for trip '{request.TripId}'.");
                }

                if (pgEx.ConstraintName != "uq_invoice_number")
                {
                    throw;
                }

                if (attempt >= MaxInvoiceNumberGenerationAttempts)
                {
                    throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_REFERENCE_CODE_CONFLICT,
                        "Could not generate a unique invoice number after several attempts; please retry.");
                }

                invoice.InvoiceNumber = GenerateInvoiceNumber();
            }
        }

        return MapToResponse(invoice);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> GetByIdAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Driver)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceTripPartyAuthorization(invoice.Trip, currentUserId, role);

        return MapToResponse(invoice);
    }

    /// <inheritdoc />
    public async Task<PagedInvoiceResponseDto> GetListAsync(InvoiceListQueryDto query, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Driver)
            .AsQueryable();

        // Scope query to caller's role
        if (role == UserRole.Shipper)
        {
            baseQuery = baseQuery.Where(i => i.Trip.Assignment.Load.ShipperUserId == currentUserId);
        }
        else if (role == UserRole.AgencyStaff)
        {
            var userAgencyIds = _dbContext.AgencyStaff
                .Where(s => s.UserId == currentUserId)
                .Select(s => s.AgencyId);

            baseQuery = baseQuery.Where(i => userAgencyIds.Contains(i.Trip.Assignment.AgencyId));
        }
        else if (role == UserRole.Driver)
        {
            baseQuery = baseQuery.Where(i => i.Trip.Driver.UserId == currentUserId);
        }
        // Admin sees all invoices

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.Status == query.Status.Value);
        }

        if (query.TripId.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.TripId == query.TripId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            baseQuery = baseQuery.Where(i => i.InvoiceNumber.Contains(term));
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var isAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        baseQuery = isAsc ? baseQuery.OrderBy(i => i.CreatedAt) : baseQuery.OrderByDescending(i => i.CreatedAt);

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListItemDto
            {
                InvoiceId = i.InvoiceId,
                TripId = i.TripId,
                InvoiceNumber = i.InvoiceNumber,
                Amount = i.Amount,
                Currency = i.Currency,
                Status = i.Status,
                IssuedAt = i.IssuedAt,
                DueDate = i.DueDate,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedInvoiceResponseDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> UpdateAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceDto request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_INVOICE_AMOUNT, "Invoice amount must be greater than zero.");
        }

        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceTripPartyAuthorization(invoice.Trip, currentUserId, role);

        if (!InvoiceStatusTransitionRules.CanEdit(invoice.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION, $"Invoice in status '{invoice.Status}' cannot be edited.");
        }

        invoice.Amount = request.Amount;
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "LKR" : request.Currency.Trim().ToUpperInvariant();
        invoice.DueDate = request.DueDate;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(invoice);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> UpdateStatusAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceStatusDto request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceTripPartyAuthorization(invoice.Trip, currentUserId, role);

        if (!InvoiceStatusTransitionRules.CanTransition(invoice.Status, request.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION, $"Cannot transition invoice from '{invoice.Status}' to '{request.Status}'.");
        }

        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(invoice);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> VoidAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceTripPartyAuthorization(invoice.Trip, currentUserId, role);

        if (!InvoiceStatusTransitionRules.CanVoid(invoice.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION, $"Invoice in status '{invoice.Status}' cannot be voided.");
        }

        invoice.Status = InvoiceStatus.Void;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(invoice);
    }

    private static void EnforceTripPartyAuthorization(Trip trip, Guid currentUserId, UserRole role)
    {
        if (role == UserRole.Admin)
        {
            return;
        }

        if (role == UserRole.Shipper && trip.Assignment?.Load?.ShipperUserId == currentUserId)
        {
            return;
        }

        if (role == UserRole.AgencyStaff && trip.Assignment?.Agency?.Staff != null && trip.Assignment.Agency.Staff.Any(s => s.UserId == currentUserId))
        {
            return;
        }

        if (role == UserRole.Driver && trip.Driver?.UserId == currentUserId)
        {
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.INVOICE_NOT_OWNED, "You do not have permission to access this invoice or trip.");
    }

    private static string GenerateInvoiceNumber()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var rand = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"INV-{date}-{rand}";
    }

    private static InvoiceResponseDto MapToResponse(Invoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        TripId = invoice.TripId,
        InvoiceNumber = invoice.InvoiceNumber,
        Amount = invoice.Amount,
        Currency = invoice.Currency,
        Status = invoice.Status,
        IssuedAt = invoice.IssuedAt,
        DueDate = invoice.DueDate,
        CreatedAt = invoice.CreatedAt,
        UpdatedAt = invoice.UpdatedAt
    };
}
