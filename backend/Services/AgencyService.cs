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
    public async Task<IEnumerable<AgencyResponseDto>> GetListAsync(Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators can list all agencies.");
        }

        var agencies = await _dbContext.Agencies.AsNoTracking()
            .ToListAsync(cancellationToken);

        return agencies.Select(MapToResponse);
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
