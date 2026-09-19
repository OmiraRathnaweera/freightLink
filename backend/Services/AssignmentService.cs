using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FreightLink.Api.Services;

/// <summary>
/// Service implementing assignment / job proposal lifecycle and queries (Component C).
/// </summary>
public class AssignmentService : IAssignmentService
{
    private readonly AppDbContext _dbContext;

    public AssignmentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PagedAssignmentResponseDto> GetListAsync(AssignmentListQueryDto query, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        Guid? callerAgencyId = null;

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.ASSIGNMENT_NOT_OWNED, "No agency staff record found for caller.");
            }
            callerAgencyId = agencyStaff.AgencyId;
        }

        var dbQuery = _dbContext.Assignments
            .Include(a => a.Load)
                .ThenInclude(l => l.ShipperUser)
            .Include(a => a.Agency)
            .Include(a => a.Trip)
            .AsNoTracking();

        if (callerAgencyId.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.AgencyId == callerAgencyId.Value);
        }

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(a =>
                (a.Load.CargoDescription != null && a.Load.CargoDescription.ToLower().Contains(term)) ||
                (a.Load.PickupAddress != null && a.Load.PickupAddress.ToLower().Contains(term)) ||
                (a.Load.DropoffAddress != null && a.Load.DropoffAddress.ToLower().Contains(term)) ||
                (a.Load.ReferenceCode != null && a.Load.ReferenceCode.ToLower().Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sorting
        var isAsc = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        dbQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "proposedprice" => isAsc ? dbQuery.OrderBy(a => a.ProposedPrice) : dbQuery.OrderByDescending(a => a.ProposedPrice),
            "routeddistancekm" => isAsc ? dbQuery.OrderBy(a => a.RoutedDistanceKm) : dbQuery.OrderByDescending(a => a.RoutedDistanceKm),
            "proposedetaminutes" => isAsc ? dbQuery.OrderBy(a => a.ProposedEtaMinutes) : dbQuery.OrderByDescending(a => a.ProposedEtaMinutes),
            "status" => isAsc ? dbQuery.OrderBy(a => a.Status) : dbQuery.OrderByDescending(a => a.Status),
            _ => isAsc ? dbQuery.OrderBy(a => a.CreatedAt) : dbQuery.OrderByDescending(a => a.CreatedAt)
        };

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var rawItems = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(MapToListItem).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedAssignmentResponseDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            TotalPages = totalPages
        };
    }

    /// <inheritdoc />
    public async Task<AssignmentResponseDto> GetByIdAsync(Guid assignmentId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.Assignments
            .Include(a => a.Load)
                .ThenInclude(l => l.ShipperUser)
            .Include(a => a.Agency)
            .Include(a => a.Trip)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

        if (assignment == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.ASSIGNMENT_NOT_FOUND, "The requested assignment could not be found.");
        }

        await EnforceOwnershipAsync(assignment, currentUserId, currentUserRole, cancellationToken);

        return MapToDetail(assignment);
    }

    /// <inheritdoc />
    public async Task<AssignmentResponseDto> DeclineAsync(Guid loadId, DeclineAssignmentDto? request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.Assignments
            .Include(a => a.Load)
                .ThenInclude(l => l.ShipperUser)
            .Include(a => a.Agency)
            .Include(a => a.Trip)
            .FirstOrDefaultAsync(a => a.LoadId == loadId || a.AssignmentId == loadId, cancellationToken);

        if (assignment == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.ASSIGNMENT_NOT_FOUND, "The requested assignment could not be found.");
        }

        await EnforceOwnershipAsync(assignment, currentUserId, currentUserRole, cancellationToken);

        if (assignment.Status != AssignmentStatus.Proposed)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Only proposed assignments may be declined.");
        }

        assignment.Status = AssignmentStatus.Declined;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetail(assignment);
    }

    private async Task EnforceOwnershipAsync(Assignment assignment, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken)
    {
        if (currentUserRole == UserRole.Admin) return;

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null || agencyStaff.AgencyId != assignment.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.ASSIGNMENT_NOT_OWNED, "You do not have permission to view or modify this assignment.");
            }
            return;
        }

        if (currentUserRole == UserRole.Shipper)
        {
            if (assignment.Load?.ShipperUserId != currentUserId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "You do not have permission to view this assignment.");
            }
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Your role is not authorized to access this assignment.");
    }

    private static AssignmentListItemDto MapToListItem(Assignment a)
    {
        return new AssignmentListItemDto
        {
            AssignmentId = a.AssignmentId,
            LoadId = a.LoadId,
            AgencyId = a.AgencyId,
            AgencyName = a.Agency?.Name,
            ProposedPrice = a.ProposedPrice,
            RoutedDistanceKm = a.RoutedDistanceKm,
            ProposedEtaMinutes = a.ProposedEtaMinutes,
            Status = a.Status.ToString(),
            CargoDescription = a.Load?.CargoDescription,
            WeightKg = a.Load?.WeightKg,
            VolumeM3 = a.Load?.VolumeM3,
            PickupAddress = a.Load?.PickupAddress,
            DropoffAddress = a.Load?.DropoffAddress,
            PickupWindowStart = a.Load?.PickupWindowStart,
            PickupWindowEnd = a.Load?.PickupWindowEnd,
            ShipperName = a.Load?.ShipperUser?.FullName,
            ReferenceCode = a.Load?.ReferenceCode,
            TripId = a.Trip?.TripId,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }

    private static AssignmentResponseDto MapToDetail(Assignment a)
    {
        return new AssignmentResponseDto
        {
            AssignmentId = a.AssignmentId,
            LoadId = a.LoadId,
            AgencyId = a.AgencyId,
            AgencyName = a.Agency?.Name,
            WorkflowRunId = a.WorkflowRunId,
            ProposedPrice = a.ProposedPrice,
            RoutedDistanceKm = a.RoutedDistanceKm,
            ProposedEtaMinutes = a.ProposedEtaMinutes,
            Status = a.Status.ToString(),
            CargoDescription = a.Load?.CargoDescription,
            WeightKg = a.Load?.WeightKg,
            VolumeM3 = a.Load?.VolumeM3,
            PickupAddress = a.Load?.PickupAddress,
            PickupLat = a.Load?.PickupLat,
            PickupLng = a.Load?.PickupLng,
            DropoffAddress = a.Load?.DropoffAddress,
            DropoffLat = a.Load?.DropoffLat,
            DropoffLng = a.Load?.DropoffLng,
            PickupWindowStart = a.Load?.PickupWindowStart,
            PickupWindowEnd = a.Load?.PickupWindowEnd,
            ShipperName = a.Load?.ShipperUser?.FullName,
            ReferenceCode = a.Load?.ReferenceCode,
            TripId = a.Trip?.TripId,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }
}
