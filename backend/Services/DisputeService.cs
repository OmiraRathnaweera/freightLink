using System.Net;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <summary>
/// Service implementing <see cref="IDisputeService"/> enforcing the strict linear dispute lifecycle:
/// Raised -> UnderReview -> Resolved.
/// </summary>
public class DisputeService : IDisputeService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Initializes a new instance of <see cref="DisputeService"/>.</summary>
    public DisputeService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<DisputeResponseDto> CreateAsync(Guid currentUserId, UserRole role, CreateDisputeDto request, CancellationToken cancellationToken = default)
    {
        // Business Rule 1: Created exclusively by a Shipper or an Agency
        if (role != UserRole.Shipper && role != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Disputes can be created exclusively by a Shipper or an Agency.");
        }

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Dispute description must be at least 10 characters long.");
        }

        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
                    .ThenInclude(ag => ag.Staff)
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TripId == request.TripId, cancellationToken);

        if (trip is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, $"Trip '{request.TripId}' was not found.");
        }

        EnforceTripPartyAuthorization(trip, currentUserId, role);

        // Check for active dispute on this trip & category (Raised or UnderReview)
        var liveStatuses = new[] { DisputeStatus.Raised, DisputeStatus.UnderReview };
        var liveDuplicateExists = await _dbContext.Disputes.AnyAsync(
            d => d.TripId == request.TripId
              && d.Category == request.Category
              && liveStatuses.Contains(d.Status),
            cancellationToken);

        if (liveDuplicateExists)
        {
            throw new ApiException(HttpStatusCode.Conflict,
                ErrorCode.DISPUTE_ALREADY_EXISTS_FOR_TRIP_AND_CATEGORY,
                $"An active dispute for category '{request.Category}' already exists for trip '{request.TripId}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var dispute = new Dispute
        {
            DisputeId = Guid.NewGuid(),
            TripId = request.TripId,
            RaisedByUserId = currentUserId,
            Category = request.Category,
            Description = request.Description.Trim(),
            Status = DisputeStatus.Raised,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Disputes.Add(dispute);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            if (pgEx.ConstraintName == "ux_dispute_open")
            {
                throw new ApiException(HttpStatusCode.Conflict,
                    ErrorCode.DISPUTE_ALREADY_EXISTS_FOR_TRIP_AND_CATEGORY,
                    $"An active dispute for category '{request.Category}' already exists for trip '{request.TripId}'.");
            }

            throw;
        }

        return MapToResponse(dispute);
    }

    /// <inheritdoc />
    public async Task<DisputeResponseDto> GetByIdAsync(Guid disputeId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(d => d.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .Include(d => d.Trip)
                .ThenInclude(t => t.Driver)
            .Include(d => d.Resolution)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId, cancellationToken);

        if (dispute is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DISPUTE_NOT_FOUND, $"Dispute '{disputeId}' was not found.");
        }

        EnforceDisputeAccessAuthorization(dispute, currentUserId, role);

        return MapToResponse(dispute);
    }

    /// <inheritdoc />
    public async Task<PagedDisputeResponseDto> GetListAsync(DisputeListQueryDto query, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
            .Include(d => d.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Agency)
                        .ThenInclude(ag => ag.Staff)
            .Include(d => d.Trip)
                .ThenInclude(t => t.Driver)
            .Include(d => d.Resolution)
            .AsQueryable();

        if (role == UserRole.Shipper)
        {
            baseQuery = baseQuery.Where(d => d.RaisedByUserId == currentUserId || d.Trip.Assignment.Load.ShipperUserId == currentUserId);
        }
        else if (role == UserRole.AgencyStaff)
        {
            var userAgencyIds = _dbContext.AgencyStaff
                .Where(s => s.UserId == currentUserId)
                .Select(s => s.AgencyId);

            baseQuery = baseQuery.Where(d => d.RaisedByUserId == currentUserId || userAgencyIds.Contains(d.Trip.Assignment.AgencyId));
        }
        else if (role == UserRole.Driver)
        {
            baseQuery = baseQuery.Where(d => d.RaisedByUserId == currentUserId || d.Trip.Driver.UserId == currentUserId);
        }

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.Status == query.Status.Value);
        }

        if (query.Category.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.Category == query.Category.Value);
        }

        if (query.TripId.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.TripId == query.TripId.Value);
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var isAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        baseQuery = isAsc ? baseQuery.OrderBy(d => d.CreatedAt) : baseQuery.OrderByDescending(d => d.CreatedAt);

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DisputeListItemDto
            {
                DisputeId = d.DisputeId,
                TripId = d.TripId,
                RaisedByUserId = d.RaisedByUserId,
                Category = d.Category,
                Description = d.Description,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                HasResolution = d.Resolution != null
            })
            .ToListAsync(cancellationToken);

        return new PagedDisputeResponseDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    /// <inheritdoc />
    public async Task<DisputeResponseDto> UpdateAsync(Guid disputeId, Guid currentUserId, UserRole role, UpdateDisputeDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Dispute description must be at least 10 characters long.");
        }

        var dispute = await _dbContext.Disputes
            .Include(d => d.Resolution)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId, cancellationToken);

        if (dispute is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DISPUTE_NOT_FOUND, $"Dispute '{disputeId}' was not found.");
        }

        if (role != UserRole.Admin && dispute.RaisedByUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.DISPUTE_NOT_OWNED, "You can only edit disputes you raised.");
        }

        if (!DisputeStatusTransitionRules.CanEdit(dispute.Status))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_DISPUTE_STATUS_TRANSITION, $"Dispute in status '{dispute.Status}' cannot be edited.");
        }

        dispute.Category = request.Category;
        dispute.Description = request.Description.Trim();
        dispute.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(dispute);
    }

    /// <inheritdoc />
    public async Task<DisputeResponseDto> MoveToReviewAsync(Guid disputeId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        // Business Rule 2: Authorized actor: Admin only
        if (role != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators may move disputes to under review.");
        }

        var dispute = await _dbContext.Disputes
            .Include(d => d.Resolution)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId, cancellationToken);

        if (dispute is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DISPUTE_NOT_FOUND, $"Dispute '{disputeId}' was not found.");
        }

        // Strict state machine validation: Raised -> UnderReview
        DisputeStatusTransitionRules.ValidateTransition(dispute.Status, DisputeStatus.UnderReview);

        dispute.Status = DisputeStatus.UnderReview;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(dispute);
    }

    /// <inheritdoc />
    public async Task<DisputeResponseDto> ResolveAsync(Guid disputeId, Guid currentUserId, UserRole role, ResolveDisputeDto request, CancellationToken cancellationToken = default)
    {
        // Business Rule 3: Authorized actor: Admin only
        if (role != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators may resolve disputes.");
        }

        var dispute = await _dbContext.Disputes
            .Include(d => d.Resolution)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId, cancellationToken);

        if (dispute is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DISPUTE_NOT_FOUND, $"Dispute '{disputeId}' was not found.");
        }

        // Strict state machine validation: UnderReview -> Resolved
        DisputeStatusTransitionRules.ValidateTransition(dispute.Status, DisputeStatus.Resolved);

        // Business Rule 3: Mandatory payload - Requires a non-empty resolutionNote (string, trimmed)
        var resolutionNote = request?.GetEffectiveResolutionNote();
        if (string.IsNullOrWhiteSpace(resolutionNote))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "A non-empty resolutionNote is required to resolve a dispute.");
        }

        var now = DateTimeOffset.UtcNow;
        var outcome = request?.Outcome ?? DisputeOutcome.Upheld;

        var resolution = dispute.Resolution;
        if (resolution is null)
        {
            resolution = new DisputeResolution
            {
                DisputeId = dispute.DisputeId,
                ResolvedByUserId = currentUserId,
                Outcome = outcome,
                Notes = resolutionNote,
                ResolvedAt = now
            };
            _dbContext.DisputeResolutions.Add(resolution);
            dispute.Resolution = resolution;
        }
        else
        {
            resolution.ResolvedByUserId = currentUserId;
            resolution.Outcome = outcome;
            resolution.Notes = resolutionNote;
            resolution.ResolvedAt = now;
        }

        dispute.Status = DisputeStatus.Resolved;
        dispute.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(dispute);
    }

    /// <inheritdoc />
    public async Task<DisputeResponseDto> TransitionStatusAsync(Guid disputeId, Guid currentUserId, UserRole role, TransitionDisputeStatusDto request, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators may transition dispute statuses.");
        }

        if (request.Status == DisputeStatus.UnderReview)
        {
            return await MoveToReviewAsync(disputeId, currentUserId, role, cancellationToken);
        }

        if (request.Status == DisputeStatus.Resolved)
        {
            return await ResolveAsync(disputeId, currentUserId, role, new ResolveDisputeDto
            {
                Outcome = request.Outcome ?? DisputeOutcome.Upheld,
                ResolutionNote = request.ResolutionNote,
                Notes = request.Notes
            }, cancellationToken);
        }

        var dispute = await _dbContext.Disputes.FirstOrDefaultAsync(d => d.DisputeId == disputeId, cancellationToken);
        if (dispute is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DISPUTE_NOT_FOUND, $"Dispute '{disputeId}' was not found.");
        }

        DisputeStatusTransitionRules.ValidateTransition(dispute.Status, request.Status);
        throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_DISPUTE_STATUS_TRANSITION, $"Unsupported status transition target '{request.Status}'.");
    }

    private static void EnforceTripPartyAuthorization(Trip trip, Guid currentUserId, UserRole role)
    {
        if (role == UserRole.Shipper && trip.Assignment?.Load?.ShipperUserId == currentUserId)
        {
            return;
        }

        if (role == UserRole.AgencyStaff && trip.Assignment?.Agency?.Staff != null && trip.Assignment.Agency.Staff.Any(s => s.UserId == currentUserId))
        {
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only the shipper or assigned agency staff may raise a dispute for this trip.");
    }

    private static void EnforceDisputeAccessAuthorization(Dispute dispute, Guid currentUserId, UserRole role)
    {
        if (role == UserRole.Admin || dispute.RaisedByUserId == currentUserId)
        {
            return;
        }

        if (role == UserRole.Shipper && dispute.Trip?.Assignment?.Load?.ShipperUserId == currentUserId)
        {
            return;
        }

        if (role == UserRole.AgencyStaff && dispute.Trip?.Assignment?.Agency?.Staff != null && dispute.Trip.Assignment.Agency.Staff.Any(s => s.UserId == currentUserId))
        {
            return;
        }

        if (role == UserRole.Driver && dispute.Trip?.Driver?.UserId == currentUserId)
        {
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.DISPUTE_NOT_OWNED, "You do not have permission to access this dispute.");
    }

    private static DisputeResponseDto MapToResponse(Dispute dispute) => new()
    {
        DisputeId = dispute.DisputeId,
        TripId = dispute.TripId,
        RaisedByUserId = dispute.RaisedByUserId,
        Category = dispute.Category,
        Description = dispute.Description,
        Status = dispute.Status,
        CreatedAt = dispute.CreatedAt,
        UpdatedAt = dispute.UpdatedAt,
        Resolution = dispute.Resolution is null ? null : new DisputeResolutionResponseDto
        {
            DisputeId = dispute.Resolution.DisputeId,
            ResolvedByUserId = dispute.Resolution.ResolvedByUserId,
            Outcome = dispute.Resolution.Outcome,
            Notes = dispute.Resolution.Notes,
            ResolvedAt = dispute.Resolution.ResolvedAt
        }
    };
}
