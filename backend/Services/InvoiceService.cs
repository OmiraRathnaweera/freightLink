using System.Net;
using System.Text.RegularExpressions;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Email;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FreightLink.Api.Services;

/// <summary>
/// Service implementing <see cref="IInvoiceService"/> for manual invoice CRUD, line-item drafting,
/// status transitions, issuing, voiding, and role-based access control.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private const int MaxInvoiceNumberGenerationAttempts = 5;

    /// <summary>
    /// Placeholder fixed invoice amount (in LKR) used when a trip is delivered via legacy hook.
    /// </summary>
    public const decimal PlaceholderInvoiceAmount = 25000.00m;

    private readonly AppDbContext _dbContext;
    private readonly IEmailService? _emailService;
    private readonly ILogger<InvoiceService>? _logger;

    /// <summary>Initializes a new instance of <see cref="InvoiceService"/>.</summary>
    public InvoiceService(
        AppDbContext dbContext,
        IEmailService? emailService = null,
        ILogger<InvoiceService>? logger = null)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> CreateAsync(Guid currentUserId, UserRole role, CreateInvoiceDto request, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only agents have permission to create invoices.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "LKR" : request.Currency.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(currency, InvoicePatterns.CurrencyCodePattern))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR,
                "Currency must be a valid ISO-4217 code: exactly three uppercase letters (e.g. LKR, USD).");
        }

        if (request.DueDate.HasValue && request.DueDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_DUE_DATE,
                "DueDate cannot be earlier than today (UTC); the invoice is issued on the current date.");
        }

        Trip? trip = null;
        if (request.TripId.HasValue)
        {
            trip = await _dbContext.Trips
                .Include(t => t.Assignment)
                    .ThenInclude(a => a.Load)
                .Include(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
                .Include(t => t.Driver)
                .FirstOrDefaultAsync(t => t.TripId == request.TripId.Value, cancellationToken);

            if (trip is null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, $"Trip '{request.TripId.Value}' was not found.");
            }

            // Verify caller has permission to generate an invoice for this trip
            EnforceTripPartyAuthorization(trip, currentUserId, role);

            if (role == UserRole.AgencyStaff)
            {
                AgencyStatusGuard.EnsureActive(trip.Assignment.Agency.Status);
            }

            // Check if an invoice already exists for this trip
            var exists = await _dbContext.Invoices.AnyAsync(i => i.TripId == request.TripId.Value, cancellationToken);
            if (exists)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP, $"An invoice already exists for trip '{request.TripId.Value}'.");
            }
        }

        if (request.RecipientId.HasValue)
        {
            var recipientExists = await _dbContext.Users.AnyAsync(u => u.UserId == request.RecipientId.Value, cancellationToken);
            if (!recipientExists)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND, $"Recipient user '{request.RecipientId.Value}' was not found.");
            }
        }

        var initialStatus = request.Status ?? (request.IssueImmediately ? InvoiceStatus.Issued : InvoiceStatus.Draft);
        if (!InvoiceStatusTransitionRules.CanCreateAs(initialStatus))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION,
                $"An invoice cannot be created directly into status '{initialStatus}'.");
        }

        // When creating from a trip and the caller didn't supply an explicit amount or line items,
        // default to the agreed price from the accepted assignment (the job proposal / AI-matched
        // price the Shipper already approved) — there is no direct-customer/manual-quote path in
        // this system, so a trip-linked invoice's starting amount is always the already-agreed budget.
        var amountToUse = request.Amount;
        if (amountToUse is null && (request.LineItems is null || request.LineItems.Count == 0) && trip is not null)
        {
            amountToUse = trip.Assignment.ProposedPrice;
        }

        // Calculate line items and totals
        var (lineEntities, subtotal, taxTotal, discountTotal, totalAmount) = CalculateLineItemsAndTotals(request.LineItems, amountToUse, request.DiscountTotal);

        var now = DateTimeOffset.UtcNow;
        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            TripId = request.TripId,
            InvoiceNumber = GenerateInvoiceNumber(),
            RecipientId = request.RecipientId ?? (trip?.Assignment?.Load?.ShipperUserId),
            RecipientRole = request.RecipientRole ?? (trip != null ? UserRole.Shipper : null),
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            DiscountTotal = discountTotal,
            Amount = totalAmount,
            Currency = currency,
            Status = initialStatus,
            Notes = request.Notes?.Trim(),
            IssuedAt = (initialStatus == InvoiceStatus.Issued) ? now : null,
            DueDate = request.DueDate,
            CreatedByUserId = currentUserId,
            UpdatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
            LineItems = lineEntities
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

        if (invoice.Status == InvoiceStatus.Issued)
        {
            await DispatchInvoiceIssuedNotificationAsync(invoice, cancellationToken);
        }

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> GetByIdAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.LineItems.OrderBy(li => li.SortOrder))
            .Include(i => i.Recipient)
            .Include(i => i.CreatedByUser)
            .Include(i => i.UpdatedByUser)
            .Include(i => i.VoidedByUser)
            .Include(i => i.PaymentProofFile)
            .Include(i => i.PaymentProofUploadedByUser)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Driver)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceInvoiceReadAuthorization(invoice, currentUserId, role);

        return MapToResponse(invoice);
    }

    /// <inheritdoc />
    public async Task<PagedInvoiceResponseDto> GetListAsync(InvoiceListQueryDto query, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.Invoices
            .Include(i => i.Recipient)
            .Include(i => i.CreatedByUser)
            .Include(i => i.UpdatedByUser)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Driver)
            .AsNoTracking();

        // Role-based visibility
        if (role == UserRole.Shipper)
        {
            // Shippers only see invoices assigned to them, and never draft invoices
            queryable = queryable.Where(i =>
                (i.RecipientId == currentUserId ||
                (i.Trip != null && i.Trip.Assignment != null && i.Trip.Assignment.Load != null && i.Trip.Assignment.Load.ShipperUserId == currentUserId))
                && i.Status != InvoiceStatus.Draft);
        }
        else if (role == UserRole.AgencyStaff)
        {
            queryable = queryable.Where(i =>
                i.RecipientId == currentUserId ||
                i.CreatedByUserId == currentUserId ||
                (i.Trip != null && i.Trip.Assignment != null && i.Trip.Assignment.Agency != null &&
                 i.Trip.Assignment.Agency.Staff.Any(s => s.UserId == currentUserId)));
        }
        else if (role == UserRole.Driver)
        {
            queryable = queryable.Where(i =>
                i.RecipientId == currentUserId ||
                (i.Trip != null && i.Trip.Driver != null && i.Trip.Driver.UserId == currentUserId));
        }
        // Admin sees all invoices (Read-Only)

        // Filters
        if (query.Status.HasValue)
        {
            queryable = queryable.Where(i => i.Status == query.Status.Value);
        }

        if (query.TripId.HasValue)
        {
            queryable = queryable.Where(i => i.TripId == query.TripId.Value);
        }

        if (query.RecipientId.HasValue)
        {
            queryable = queryable.Where(i => i.RecipientId == query.RecipientId.Value);
        }

        if (query.StartDate.HasValue)
        {
            queryable = queryable.Where(i => (i.IssuedAt ?? i.CreatedAt) >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            queryable = queryable.Where(i => (i.IssuedAt ?? i.CreatedAt) <= query.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            queryable = queryable.Where(i =>
                EF.Functions.ILike(i.InvoiceNumber, $"%{search}%") ||
                (i.Notes != null && EF.Functions.ILike(i.Notes, $"%{search}%")) ||
                (i.Recipient != null && EF.Functions.ILike(i.Recipient.FullName, $"%{search}%")));
        }

        var totalItems = await queryable.CountAsync(cancellationToken);

        var isAscending = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        queryable = isAscending
            ? queryable.OrderBy(i => i.CreatedAt)
            : queryable.OrderByDescending(i => i.CreatedAt);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 100 ? 100 : query.PageSize);

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListItemDto
            {
                InvoiceId = i.InvoiceId,
                TripId = i.TripId,
                InvoiceNumber = i.InvoiceNumber,
                RecipientId = i.RecipientId,
                RecipientName = i.Recipient != null ? i.Recipient.FullName : null,
                RecipientRole = i.RecipientRole,
                Subtotal = i.Subtotal,
                TaxTotal = i.TaxTotal,
                DiscountTotal = i.DiscountTotal,
                TotalAmount = i.Amount,
                Currency = i.Currency,
                Status = i.Status,
                IssuedAt = i.IssuedAt,
                DueDate = i.DueDate,
                PaidAt = i.PaidAt,
                PaymentReference = i.PaymentReference,
                HasPaymentProof = i.PaymentProofFileId != null,
                CreatedByName = i.CreatedByUser != null ? i.CreatedByUser.FullName : null,
                UpdatedByName = i.UpdatedByUser != null ? i.UpdatedByUser.FullName : null,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedInvoiceResponseDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> UpdateAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceDto request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceInvoiceModifyAuthorization(invoice, currentUserId, role);

        if (role == UserRole.AgencyStaff && invoice.Trip != null)
        {
            AgencyStatusGuard.EnsureActive(invoice.Trip.Assignment.Agency.Status);
        }

        // Immutability enforcement: modifications rejected if not in Draft status
        if (!InvoiceStatusTransitionRules.CanEdit(invoice.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION,
                $"Cannot update invoice '{invoiceId}' because it is in status '{invoice.Status}'. Modifications are only permitted while an invoice is in Draft status.");
        }

        if (request.Currency != null)
        {
            var currency = request.Currency.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(currency, InvoicePatterns.CurrencyCodePattern))
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR,
                    "Currency must be a valid ISO-4217 code: exactly three uppercase letters (e.g. LKR, USD).");
            }
            invoice.Currency = currency;
        }

        if (request.DueDate.HasValue)
        {
            if (invoice.IssuedAt.HasValue && request.DueDate.Value < DateOnly.FromDateTime(invoice.IssuedAt.Value.UtcDateTime))
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_DUE_DATE,
                    "DueDate cannot be earlier than the invoice issuance date.");
            }
            invoice.DueDate = request.DueDate.Value;
        }

        if (request.Notes != null)
        {
            invoice.Notes = request.Notes.Trim();
        }

        if (request.RecipientId.HasValue)
        {
            var recipientExists = await _dbContext.Users.AnyAsync(u => u.UserId == request.RecipientId.Value, cancellationToken);
            if (!recipientExists)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND, $"Recipient user '{request.RecipientId.Value}' was not found.");
            }
            invoice.RecipientId = request.RecipientId.Value;
        }

        if (request.RecipientRole.HasValue)
        {
            invoice.RecipientRole = request.RecipientRole.Value;
        }

        if (request.TripId.HasValue && request.TripId.Value != invoice.TripId)
        {
            var tripExists = await _dbContext.Trips.AnyAsync(t => t.TripId == request.TripId.Value, cancellationToken);
            if (!tripExists)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, $"Trip '{request.TripId.Value}' was not found.");
            }
            var conflict = await _dbContext.Invoices.AnyAsync(i => i.TripId == request.TripId.Value && i.InvoiceId != invoiceId, cancellationToken);
            if (conflict)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP, $"An invoice already exists for trip '{request.TripId.Value}'.");
            }
            invoice.TripId = request.TripId.Value;
        }

        // Line items and totals update
        if (request.LineItems != null)
        {
            var (newLines, subtotal, taxTotal, discountTotal, totalAmount) =
                CalculateLineItemsAndTotals(request.LineItems, request.Amount, request.DiscountTotal ?? invoice.DiscountTotal);

            _dbContext.InvoiceLineItems.RemoveRange(invoice.LineItems);
            foreach (var line in newLines)
            {
                line.InvoiceId = invoice.InvoiceId;
                _dbContext.InvoiceLineItems.Add(line);
            }
            invoice.Subtotal = subtotal;
            invoice.TaxTotal = taxTotal;
            invoice.DiscountTotal = discountTotal;
            invoice.Amount = totalAmount;
        }
        else if (request.Amount.HasValue)
        {
            if (request.Amount.Value <= 0)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_INVOICE_AMOUNT, "Invoice amount must be greater than zero.");
            }
            invoice.Subtotal = request.Amount.Value;
            invoice.Amount = request.Amount.Value;
            if (request.DiscountTotal.HasValue)
            {
                invoice.DiscountTotal = Math.Max(0m, request.DiscountTotal.Value);
                invoice.Amount = Math.Max(0m, invoice.Subtotal + invoice.TaxTotal - invoice.DiscountTotal);
            }

            // Keep the single auto-generated line item (the only shape this system's simplified,
            // trip-linked invoices ever have) in sync with the amount-only update above — otherwise
            // the line-item breakdown would keep showing the stale pre-edit amount.
            if (invoice.LineItems.Count == 1)
            {
                var onlyLine = invoice.LineItems.Single();
                onlyLine.UnitPrice = request.Amount.Value;
                onlyLine.Amount = request.Amount.Value;
            }
        }
        else if (request.DiscountTotal.HasValue)
        {
            invoice.DiscountTotal = Math.Max(0m, request.DiscountTotal.Value);
            invoice.Amount = Math.Max(0m, invoice.Subtotal + invoice.TaxTotal - invoice.DiscountTotal);
        }

        invoice.UpdatedByUserId = currentUserId;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> IssueAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceInvoiceModifyAuthorization(invoice, currentUserId, role);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION,
                $"Cannot issue invoice '{invoiceId}' because it is in status '{invoice.Status}'. Only Draft invoices can be transitioned to Issued.");
        }

        if (invoice.Amount <= 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_INVOICE_AMOUNT,
                "Cannot issue an invoice with zero or negative total amount.");
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssuedAt = now;
        invoice.UpdatedByUserId = currentUserId;
        invoice.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await DispatchInvoiceIssuedNotificationAsync(invoice, cancellationToken);

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> UpdateStatusAsync(Guid invoiceId, Guid currentUserId, UserRole role, UpdateInvoiceStatusDto request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceInvoiceModifyAuthorization(invoice, currentUserId, role);

        if (role == UserRole.AgencyStaff && invoice.Trip != null)
        {
            AgencyStatusGuard.EnsureActive(invoice.Trip.Assignment.Agency.Status);
        }

        if (InvoiceStatusTransitionRules.RequiresDedicatedAction(request.Status))
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION,
                $"Status '{request.Status}' cannot be set directly; use the dedicated action for it instead.");
        }

        if (!InvoiceStatusTransitionRules.CanTransition(invoice.Status, request.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION,
                $"Transition from '{invoice.Status}' to '{request.Status}' is not permitted.");
        }

        var now = DateTimeOffset.UtcNow;
        if (request.Status == InvoiceStatus.Issued && invoice.Status == InvoiceStatus.Draft)
        {
            invoice.IssuedAt = now;
        }

        invoice.Status = request.Status;
        invoice.UpdatedByUserId = currentUserId;
        invoice.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> VoidAsync(Guid invoiceId, Guid currentUserId, UserRole role, string voidReason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voidReason))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR,
                "A non-empty void reason is required to void an invoice.");
        }

        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceInvoiceModifyAuthorization(invoice, currentUserId, role);

        if (role == UserRole.AgencyStaff && invoice.Trip != null)
        {
            AgencyStatusGuard.EnsureActive(invoice.Trip.Assignment.Agency.Status);
        }

        if (!InvoiceStatusTransitionRules.CanVoid(invoice.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION,
                $"Invoice in status '{invoice.Status}' cannot be voided.");
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Void;
        invoice.VoidReason = voidReason.Trim();
        invoice.VoidedByUserId = currentUserId;
        invoice.VoidedAt = now;
        invoice.UpdatedByUserId = currentUserId;
        invoice.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    /// <inheritdoc />
    public Task<InvoiceResponseDto> VoidAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
        => VoidAsync(invoiceId, currentUserId, role, "Voided by user", cancellationToken);

    /// <inheritdoc />
    public async Task<List<InvoiceRecipientDto>> GetRecipientsAsync(Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Shipper || u.Role == UserRole.AgencyStaff || u.Role == UserRole.Driver))
            .OrderBy(u => u.FullName)
            .Select(u => new InvoiceRecipientDto
            {
                RecipientId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> CreateOnTripDeliveredAsync(Guid tripId, Guid? currentUserId = null, UserRole? role = null, CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
                    .ThenInclude(ag => ag.Staff)
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, $"Trip '{tripId}' was not found.");
        }

        if (currentUserId.HasValue && role.HasValue)
        {
            EnforceTripPartyAuthorization(trip, currentUserId.Value, role.Value);
        }

        if (trip.Status != TripStatus.Delivered)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.TRIP_NOT_DELIVERED,
                $"Trip '{tripId}' cannot generate an invoice on delivery because it is in status '{trip.Status}', not 'Delivered'.");
        }

        // Idempotency guard: prevent duplicate invoices for the same trip
        var exists = await _dbContext.Invoices.AnyAsync(i => i.TripId == tripId, cancellationToken);
        if (exists)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP,
                $"An invoice already exists for trip '{tripId}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            TripId = tripId,
            InvoiceNumber = GenerateInvoiceNumber(),
            RecipientId = trip.Assignment?.Load?.ShipperUserId,
            RecipientRole = UserRole.Shipper,
            Subtotal = PlaceholderInvoiceAmount,
            TaxTotal = 0m,
            DiscountTotal = 0m,
            Amount = PlaceholderInvoiceAmount,
            Currency = "LKR",
            Status = InvoiceStatus.Issued,
            IssuedAt = now,
            DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(7)),
            CreatedByUserId = currentUserId,
            UpdatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
            LineItems = new List<InvoiceLineItem>
            {
                new()
                {
                    InvoiceLineItemId = Guid.NewGuid(),
                    Description = $"Freight Delivery for Trip {tripId.ToString()[..8]}",
                    Quantity = 1,
                    UnitPrice = PlaceholderInvoiceAmount,
                    TaxRate = 0,
                    Amount = PlaceholderInvoiceAmount,
                    SortOrder = 0
                }
            }
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
                if (pgEx.ConstraintName == "uq_invoice_trip_id")
                {
                    // Race condition guard: return existing created invoice
                    var raceExisting = await _dbContext.Invoices
                        .Include(i => i.LineItems)
                        .Include(i => i.Recipient)
                        .Include(i => i.CreatedByUser)
                        .Include(i => i.UpdatedByUser)
                        .Include(i => i.VoidedByUser)
                        .Include(i => i.Trip)
                        .FirstOrDefaultAsync(i => i.TripId == tripId, cancellationToken);
                    if (raceExisting != null)
                    {
                        return MapToResponse(raceExisting);
                    }
                    throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP,
                        $"An invoice already exists for trip '{tripId}'.");
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

        return await GetByIdAsync(invoice.InvoiceId, currentUserId ?? Guid.Empty, role ?? UserRole.Admin, cancellationToken);
    }

    private static (List<InvoiceLineItem> Items, decimal Subtotal, decimal TaxTotal, decimal DiscountTotal, decimal TotalAmount)
        CalculateLineItemsAndTotals(List<InvoiceLineItemDto>? lineDtos, decimal? explicitAmount, decimal discountInput)
    {
        var lineEntities = new List<InvoiceLineItem>();
        decimal subtotal = 0m;
        decimal taxTotal = 0m;

        if (lineDtos != null && lineDtos.Count > 0)
        {
            var sort = 0;
            foreach (var item in lineDtos)
            {
                if (string.IsNullOrWhiteSpace(item.Description))
                {
                    throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Line item description cannot be empty.");
                }
                if (item.Quantity <= 0)
                {
                    throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Line item quantity must be greater than zero.");
                }
                if (item.UnitPrice < 0)
                {
                    throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Line item unit price cannot be negative.");
                }
                if (item.TaxRate < 0 || item.TaxRate > 100)
                {
                    throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Line item tax rate must be between 0% and 100%.");
                }

                var itemSubtotal = decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
                var taxMultiplier = item.TaxRate > 1m ? item.TaxRate / 100m : item.TaxRate;
                var itemTax = decimal.Round(itemSubtotal * taxMultiplier, 2, MidpointRounding.AwayFromZero);
                var itemAmount = itemSubtotal + itemTax;

                subtotal += itemSubtotal;
                taxTotal += itemTax;

                lineEntities.Add(new InvoiceLineItem
                {
                    InvoiceLineItemId = item.InvoiceLineItemId ?? Guid.NewGuid(),
                    Description = item.Description.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TaxRate = item.TaxRate,
                    Amount = itemAmount,
                    SortOrder = sort++
                });
            }
        }
        else if (explicitAmount.HasValue && explicitAmount.Value > 0)
        {
            subtotal = explicitAmount.Value;
            taxTotal = 0m;
            lineEntities.Add(new InvoiceLineItem
            {
                InvoiceLineItemId = Guid.NewGuid(),
                Description = "Freight Services",
                Quantity = 1,
                UnitPrice = subtotal,
                TaxRate = 0,
                Amount = subtotal,
                SortOrder = 0
            });
        }
        else
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_INVOICE_AMOUNT,
                "Invoice must contain at least one line item or a valid positive amount.");
        }

        var discountTotal = Math.Max(0m, discountInput);
        var totalAmount = Math.Max(0m, subtotal + taxTotal - discountTotal);

        return (lineEntities, subtotal, taxTotal, discountTotal, totalAmount);
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

    private static void EnforceInvoiceReadAuthorization(Invoice invoice, Guid currentUserId, UserRole role)
    {
        if (role == UserRole.Admin)
        {
            return;
        }

        if (role == UserRole.Shipper)
        {
            bool isAssigned = invoice.RecipientId == currentUserId ||
                (invoice.Trip != null && invoice.Trip.Assignment?.Load?.ShipperUserId == currentUserId);

            if (!isAssigned)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Shippers cannot view invoices assigned to other users.");
            }

            if (invoice.Status == InvoiceStatus.Draft)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Shippers cannot view draft invoices.");
            }

            return;
        }

        if (role == UserRole.AgencyStaff)
        {
            return;
        }

        if (role == UserRole.Driver && invoice.Trip != null)
        {
            EnforceTripPartyAuthorization(invoice.Trip, currentUserId, role);
            return;
        }

        if (invoice.CreatedByUserId == currentUserId || invoice.RecipientId == currentUserId)
        {
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.INVOICE_NOT_OWNED, "You do not have permission to view this invoice.");
    }

    private static void EnforceInvoiceModifyAuthorization(Invoice invoice, Guid currentUserId, UserRole role)
    {
        if (role != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only agents have permission to modify invoices.");
        }
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> UploadPaymentProofAsync(Guid invoiceId, Guid currentUserId, UserRole role, UploadPaymentProofDto request, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Shipper)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only shippers can submit a payment receipt.");
        }

        var invoice = await _dbContext.Invoices
            .Include(i => i.Recipient)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        bool isAssigned = invoice.RecipientId == currentUserId ||
            (invoice.Trip != null && invoice.Trip.Assignment?.Load?.ShipperUserId == currentUserId);

        if (!isAssigned)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "You are not authorized to pay this invoice.");
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVOICE_ALREADY_PAID, "Invoice is already paid.");
        }

        if (invoice.Status == InvoiceStatus.Draft)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Cannot submit a payment receipt for an invoice in Draft status. The invoice must be Issued first.");
        }

        if (invoice.Status == InvoiceStatus.Void || invoice.Status == InvoiceStatus.Voided)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Cannot submit a payment receipt for a voided invoice.");
        }

        if (invoice.Status != InvoiceStatus.Issued && invoice.Status != InvoiceStatus.PaymentPending)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, $"Invoice in status '{invoice.Status}' cannot accept a payment receipt.");
        }

        var uploadedFile = await _dbContext.UploadedFiles.FirstOrDefaultAsync(f => f.PublicId == request.PublicId, cancellationToken);
        if (uploadedFile is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_PAYMENT_PROOF_FILE_NOT_FOUND, "The referenced uploaded file could not be found.");
        }

        if (uploadedFile.UploadedByUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FILE_NOT_OWNED, "You do not have permission to attach this file.");
        }

        var now = DateTimeOffset.UtcNow;
        invoice.PaymentProofFileId = uploadedFile.FileId;
        invoice.PaymentProofUploadedAt = now;
        invoice.PaymentProofUploadedByUserId = currentUserId;
        invoice.Status = InvoiceStatus.PaymentPending;
        invoice.UpdatedByUserId = currentUserId;
        invoice.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Shipper {ShipperId} submitted a payment receipt for Invoice #{InvoiceNumber}; awaiting Agency confirmation.",
            currentUserId, invoice.InvoiceNumber);

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponseDto> ConfirmPaymentAsync(Guid invoiceId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(i => i.Trip)
                .ThenInclude(t => t!.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice '{invoiceId}' was not found.");
        }

        EnforceInvoiceModifyAuthorization(invoice, currentUserId, role);

        if (invoice.Trip != null)
        {
            AgencyStatusGuard.EnsureActive(invoice.Trip.Assignment.Agency.Status);
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVOICE_ALREADY_PAID, "Invoice is already paid.");
        }

        if (invoice.PaymentProofFileId is null || invoice.Status != InvoiceStatus.PaymentPending)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVOICE_PAYMENT_PROOF_REQUIRED,
                "This invoice has no payment receipt submitted by the Shipper yet.");
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = now;
        invoice.UpdatedByUserId = currentUserId;
        invoice.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Agency user {AgentId} confirmed payment and closed Invoice #{InvoiceNumber}.",
            currentUserId, invoice.InvoiceNumber);

        return await GetByIdAsync(invoice.InvoiceId, currentUserId, role, cancellationToken);
    }

    private async Task DispatchInvoiceIssuedNotificationAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        try
        {
            var recipientEmail = invoice.Recipient?.Email;
            var recipientName = invoice.Recipient?.FullName ?? "Customer";

            if (string.IsNullOrWhiteSpace(recipientEmail) && invoice.RecipientId.HasValue)
            {
                var recipient = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == invoice.RecipientId.Value, cancellationToken);
                if (recipient != null)
                {
                    recipientEmail = recipient.Email;
                    recipientName = recipient.FullName;
                }
            }

            if (!string.IsNullOrWhiteSpace(recipientEmail) && _emailService != null)
            {
                var emailMsg = new EmailMessage
                {
                    To = recipientEmail,
                    Subject = $"Invoice #{invoice.InvoiceNumber} Issued - FreightLink",
                    HtmlBody = $"<p>Dear {recipientName},</p><p>An invoice <strong>#{invoice.InvoiceNumber}</strong> for the amount of <strong>{invoice.TotalAmount:N2} {invoice.Currency}</strong> has been issued and is now available in your FreightLink Billing portal.</p><p>Please log in to review and pay this invoice.</p>",
                    TextBody = $"Dear {recipientName},\n\nAn invoice #{invoice.InvoiceNumber} for {invoice.TotalAmount:N2} {invoice.Currency} has been issued and is now available in your FreightLink Billing portal.\n\nPlease log in to review and pay this invoice."
                };
                await _emailService.SendAsync(emailMsg, cancellationToken);
            }

            _logger?.LogInformation("Dispatched invoice delivery notification to Shipper {RecipientId} for Invoice #{InvoiceNumber}",
                invoice.RecipientId, invoice.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to dispatch invoice delivery notification for Invoice #{InvoiceNumber}", invoice.InvoiceNumber);
        }
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
        RecipientId = invoice.RecipientId,
        RecipientRole = invoice.RecipientRole,
        RecipientName = invoice.Recipient?.FullName,
        Subtotal = invoice.Subtotal,
        TaxTotal = invoice.TaxTotal,
        DiscountTotal = invoice.DiscountTotal,
        TotalAmount = invoice.Amount,
        Currency = invoice.Currency,
        Status = invoice.Status,
        Notes = invoice.Notes,
        IssuedAt = invoice.IssuedAt,
        DueDate = invoice.DueDate,
        PaidAt = invoice.PaidAt,
        PaymentReference = invoice.PaymentReference,
        PaymentProofUrl = invoice.PaymentProofFile?.SecureUrl,
        PaymentProofFileName = invoice.PaymentProofFile?.OriginalFileName,
        PaymentProofUploadedAt = invoice.PaymentProofUploadedAt,
        PaymentProofUploadedByName = invoice.PaymentProofUploadedByUser?.FullName,
        CreatedAt = invoice.CreatedAt,
        UpdatedAt = invoice.UpdatedAt,
        LineItems = invoice.LineItems.OrderBy(li => li.SortOrder).Select(li => new InvoiceLineItemDto
        {
            InvoiceLineItemId = li.InvoiceLineItemId,
            Description = li.Description,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice,
            TaxRate = li.TaxRate,
            Amount = li.Amount
        }).ToList(),
        AuditTrail = new InvoiceAuditTrailDto
        {
            CreatedBy = invoice.CreatedByUserId,
            CreatedByName = invoice.CreatedByUser?.FullName,
            UpdatedBy = invoice.UpdatedByUserId,
            UpdatedByName = invoice.UpdatedByUser?.FullName,
            VoidedBy = invoice.VoidedByUserId,
            VoidedByName = invoice.VoidedByUser?.FullName,
            VoidReason = invoice.VoidReason,
            VoidedAt = invoice.VoidedAt,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        }
    };

    /// <inheritdoc />
    public async Task<InvoiceSummaryDto> GetSummaryAsync(Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only Admin can view the invoice cashflow summary.");
        }

        var statusCounts = await _dbContext.Invoices
            .AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countByStatus = new InvoiceStatusCountsDto();
        foreach (var group in statusCounts)
        {
            switch (group.Status)
            {
                case InvoiceStatus.Draft: countByStatus.Draft = group.Count; break;
                case InvoiceStatus.Issued: countByStatus.Issued = group.Count; break;
                case InvoiceStatus.PaymentPending: countByStatus.PaymentPending = group.Count; break;
                case InvoiceStatus.Paid: countByStatus.Paid = group.Count; break;
                case InvoiceStatus.Failed: countByStatus.Failed = group.Count; break;
                case InvoiceStatus.Void: countByStatus.Void = group.Count; break;
            }
        }

        var totalInvoiced = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Void)
            .SumAsync(i => (decimal?)i.Amount, cancellationToken) ?? 0m;

        var totalPaid = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Paid)
            .SumAsync(i => (decimal?)i.Amount, cancellationToken) ?? 0m;

        var recentActivity = await _dbContext.Invoices
            .AsNoTracking()
            .OrderByDescending(i => i.UpdatedAt)
            .Take(10)
            .Select(i => new InvoiceRecentActivityDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                Status = i.Status.ToString(),
                Amount = i.Amount,
                Currency = i.Currency,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new InvoiceSummaryDto
        {
            TotalInvoiced = totalInvoiced,
            TotalPaid = totalPaid,
            TotalOutstanding = totalInvoiced - totalPaid,
            CountByStatus = countByStatus,
            RecentActivity = recentActivity
        };
    }
}
