using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAgencyService" />
public class AgencyService : IAgencyService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the agency service with its DB context.</summary>
    public AgencyService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> CreateAsync(Guid currentUserId, UserRole currentUserRole, AgencyCreateDto request, CancellationToken cancellationToken = default)
    {
        var existingAgency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.BusinessRegNo == request.BusinessRegNo, cancellationToken);
            
        if (existingAgency != null)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "An agency with this business registration number already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = request.Name,
            BusinessRegNo = request.BusinessRegNo,
            YardAddress = request.YardAddress,
            YardLat = request.YardLat,
            YardLng = request.YardLng,
            Status = AgencyStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var statusHistory = new AgencyStatusHistory
        {
            AgencyStatusHistoryId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            ChangedByUserId = currentUserId,
            FromStatus = null,
            ToStatus = AgencyStatus.Pending,
            ChangedAt = now
        };

        _dbContext.Agencies.Add(agency);
        _dbContext.AgencyStatusHistories.Add(statusHistory);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_agency_regno" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "An agency with this business registration number already exists.");
        }

        return MapToResponse(agency);
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> GetByIdAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        return MapToResponse(agency);
    }

    /// <inheritdoc />
    public async Task<PagedAgencyResponseDto> GetListAsync(Guid currentUserId, UserRole currentUserRole, AgencyListQueryDto query, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators can list all agencies.");
        }

        var dbQuery = _dbContext.Agencies.AsNoTracking();

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            dbQuery = dbQuery.Where(a => 
                a.Name.ToLower().Contains(search) || 
                a.BusinessRegNo.ToLower().Contains(search) ||
                a.YardAddress.ToLower().Contains(search));
        }

        // Default sort by CreatedAt desc
        dbQuery = query.SortBy?.ToLower() switch
        {
            "name" => query.SortDir?.ToLower() == "asc" ? dbQuery.OrderBy(a => a.Name) : dbQuery.OrderByDescending(a => a.Name),
            "status" => query.SortDir?.ToLower() == "asc" ? dbQuery.OrderBy(a => a.Status) : dbQuery.OrderByDescending(a => a.Status),
            _ => query.SortDir?.ToLower() == "asc" ? dbQuery.OrderBy(a => a.CreatedAt) : dbQuery.OrderByDescending(a => a.CreatedAt)
        };

        var totalItems = await dbQuery.CountAsync(cancellationToken);

        var agencies = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedAgencyResponseDto
        {
            Items = agencies.Select(MapToResponse).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> UpdateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, AgencyUpdateDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (request.Name != null) agency.Name = request.Name;
        if (request.YardAddress != null) agency.YardAddress = request.YardAddress;
        if (request.YardLat.HasValue) agency.YardLat = request.YardLat.Value;
        if (request.YardLng.HasValue) agency.YardLng = request.YardLng.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(agency);
    }

    private async Task VerifyAgencyOwnershipAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken)
    {
        if (currentUserRole == UserRole.Admin)
        {
            return;
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var isOwned = await _dbContext.AgencyStaff
                .AnyAsync(s => s.UserId == currentUserId && s.AgencyId == agencyId, cancellationToken);

            if (!isOwned)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.AGENCY_NOT_OWNED, "You do not have permission to access this agency.");
            }
            
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Your role does not permit accessing agency data.");
    }

    /// <inheritdoc />
    public async Task<List<VehicleResponseDto>> GetVehiclesAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        var vehicles = await _dbContext.Vehicles.AsNoTracking()
            .Where(v => v.AgencyId == agencyId)
            .OrderBy(v => v.RegistrationNo)
            .ToListAsync(cancellationToken);

        return vehicles.Select(v => new VehicleResponseDto
        {
            VehicleId = v.VehicleId,
            AgencyId = v.AgencyId,
            RegistrationNo = v.RegistrationNo,
            VehicleType = v.VehicleType.ToString(),
            CapacityKg = v.CapacityKg,
            VolumeM3 = v.VolumeM3,
            Status = v.Status.ToString(),
            IsAvailable = v.Status == VehicleStatus.Available,
            CreatedAt = v.CreatedAt,
            UpdatedAt = v.UpdatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<DriverResponseDto>> GetDriversAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        var drivers = await _dbContext.Drivers.AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.AgencyId == agencyId)
            .OrderBy(d => d.User.FullName)
            .ToListAsync(cancellationToken);

        return drivers.Select(d => new DriverResponseDto
        {
            DriverId = d.DriverId,
            UserId = d.UserId,
            AgencyId = d.AgencyId,
            FullName = d.User?.FullName ?? string.Empty,
            Email = d.User?.Email ?? string.Empty,
            LicenceNo = d.LicenceNo,
            LicenceExpiry = d.LicenceExpiry,
            Status = d.Status.ToString(),
            IsActive = d.Status == DriverStatus.Active,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<AgencyFleetResponseDto> GetFleetAsync(Guid? agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        Guid targetAgencyId;
        if (currentUserRole == UserRole.AgencyStaff)
        {
            var staff = await _dbContext.AgencyStaff.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);
            if (staff == null)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "No agency staff profile found for caller.");
            }
            targetAgencyId = staff.AgencyId;
        }
        else if (agencyId.HasValue)
        {
            targetAgencyId = agencyId.Value;
        }
        else
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Agency ID must be specified.");
        }

        var agency = await _dbContext.Agencies.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgencyId == targetAgencyId, cancellationToken);
        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        var vehicles = await GetVehiclesAsync(targetAgencyId, currentUserId, currentUserRole, cancellationToken);
        var drivers = await GetDriversAsync(targetAgencyId, currentUserId, currentUserRole, cancellationToken);

        return new AgencyFleetResponseDto
        {
            AgencyId = targetAgencyId,
            AgencyName = agency.Name,
            Vehicles = vehicles,
            Drivers = drivers
        };
    }

    private static AgencyResponseDto MapToResponse(Agency agency)
    {
        return new AgencyResponseDto
        {
            AgencyId = agency.AgencyId,
            Name = agency.Name,
            BusinessRegNo = agency.BusinessRegNo,
            YardAddress = agency.YardAddress,
            YardLat = agency.YardLat,
            YardLng = agency.YardLng,
            Status = agency.Status,
            CreatedAt = agency.CreatedAt,
            UpdatedAt = agency.UpdatedAt
        };
    }
}
