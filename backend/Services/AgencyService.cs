using System.Net;
using FreightLink.Api.Common.Domain;
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
    public async Task<IEnumerable<AgencyExpiringComplianceDto>> GetAgenciesWithExpiringComplianceAsync(int days, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thresholdDate = today.AddDays(days);

        var agencies = await _dbContext.Agencies
            .Include(a => a.ComplianceDocs)
            .Where(a => a.ComplianceDocs.Any(d => 
                d.Status == ComplianceDocStatus.Verified &&
                d.ExpiresOn.HasValue &&
                d.ExpiresOn.Value <= thresholdDate &&
                d.ExpiresOn.Value >= today))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return agencies.Select(a => new AgencyExpiringComplianceDto
        {
            Agency = MapToResponse(a),
            ExpiringDocs = a.ComplianceDocs
                .Where(d => d.Status == ComplianceDocStatus.Verified && d.ExpiresOn.HasValue && d.ExpiresOn.Value <= thresholdDate && d.ExpiresOn.Value >= today)
                .Select(d => new ComplianceDocResponseDto
                {
                    ComplianceDocId = d.ComplianceDocId,
                    DocType = d.DocType,
                    DocNumber = d.DocNumber,
                    StorageKey = d.StorageKey,
                    IssuedOn = d.IssuedOn,
                    ExpiresOn = d.ExpiresOn,
                    Status = d.Status
                })
        });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AgencyVerificationQueueItemDto>> GetVerificationQueueAsync(CancellationToken cancellationToken = default)
    {
        var agencies = await _dbContext.Agencies
            .Include(a => a.ComplianceDocs)
            .Where(a => a.Status == AgencyStatus.Pending)
            .AsNoTracking()
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return agencies.Select(a => new AgencyVerificationQueueItemDto
        {
            Agency = MapToResponse(a),
            ComplianceDocs = a.ComplianceDocs
                .OrderByDescending(d => d.CreatedAt)
                .Select(MapToComplianceDocResponse)
        });
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

        if (currentUserRole == UserRole.AgencyStaff)
        {
            AgencyStatusGuard.EnsureActive(agency.Status);
        }

        if (request.Name != null) agency.Name = request.Name;
        if (request.YardAddress != null) agency.YardAddress = request.YardAddress;
        if (request.YardLat.HasValue) agency.YardLat = request.YardLat.Value;
        if (request.YardLng.HasValue) agency.YardLng = request.YardLng.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(agency);
    }

    public async Task VerifyAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (agency.Status != AgencyStatus.Pending)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, "Only pending agencies can be verified.");
        }

        agency.Status = AgencyStatus.Verified;
        agency.StatusHistory.Add(new AgencyStatusHistory
        {
            ToStatus = AgencyStatus.Verified,
            ChangedByUserId = currentUserId,
            Reason = "Verified by admin"
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (agency.Status != AgencyStatus.Verified)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, "Only verified agencies can be activated.");
        }

        agency.Status = AgencyStatus.Active;
        agency.StatusHistory.Add(new AgencyStatusHistory
        {
            ToStatus = AgencyStatus.Active,
            ChangedByUserId = currentUserId,
            Reason = "Activated by admin"
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SuspendAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (agency.Status == AgencyStatus.Suspended)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, "Agency is already suspended.");
        }

        agency.Status = AgencyStatus.Suspended;
        agency.StatusHistory.Add(new AgencyStatusHistory
        {
            ToStatus = AgencyStatus.Suspended,
            ChangedByUserId = currentUserId,
            Reason = "Suspended by admin"
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ComplianceDocResponseDto> AddComplianceDocAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, ComplianceDocCreateDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        var agency = await _dbContext.Agencies.FindAsync(new object[] { agencyId }, cancellationToken);
        if (agency == null) throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "Agency not found.");

        var doc = new ComplianceDoc
        {
            AgencyId = agencyId,
            DocType = request.DocType,
            DocNumber = request.DocNumber,
            StorageKey = request.PublicId,
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            Status = ComplianceDocStatus.Pending
        };

        _dbContext.ComplianceDocs.Add(doc);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToComplianceDocResponse(doc);
    }

    /// <inheritdoc />
    public async Task<ComplianceDocResponseDto> UpdateComplianceDocAsync(Guid agencyId, Guid complianceDocId, Guid currentUserId, UserRole currentUserRole, ComplianceDocUpdateDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var doc = await _dbContext.ComplianceDocs
            .FirstOrDefaultAsync(d => d.ComplianceDocId == complianceDocId && d.AgencyId == agencyId, cancellationToken);

        if (doc == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.COMPLIANCE_DOC_NOT_FOUND, "The requested compliance document could not be found.");
        }

        doc.StorageKey = request.PublicId;
        doc.DocNumber = request.DocNumber;
        doc.IssuedOn = request.IssuedOn;
        doc.ExpiresOn = request.ExpiresOn;
        // A replacement file must be re-verified by an admin, regardless of the document's previous status.
        doc.Status = ComplianceDocStatus.Pending;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToComplianceDocResponse(doc);
    }

    /// <inheritdoc />
    public async Task<ComplianceDocResponseDto> VerifyComplianceDocAsync(Guid agencyId, Guid complianceDocId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var doc = await GetComplianceDocForReviewAsync(agencyId, complianceDocId, currentUserRole, cancellationToken);
        doc.Status = ComplianceDocStatus.Verified;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToComplianceDocResponse(doc);
    }

    /// <inheritdoc />
    public async Task<ComplianceDocResponseDto> RejectComplianceDocAsync(Guid agencyId, Guid complianceDocId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var doc = await GetComplianceDocForReviewAsync(agencyId, complianceDocId, currentUserRole, cancellationToken);
        doc.Status = ComplianceDocStatus.Rejected;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToComplianceDocResponse(doc);
    }

    /// <summary>
    /// Shared lookup/guard for the two admin review actions: only an Admin may call them, the document
    /// must exist under the given agency, and it must currently be <c>Pending</c> — an already
    /// verified/rejected document is not re-reviewable through this path.
    /// </summary>
    private async Task<ComplianceDoc> GetComplianceDocForReviewAsync(Guid agencyId, Guid complianceDocId, UserRole currentUserRole, CancellationToken cancellationToken)
    {
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators can verify or reject compliance documents.");
        }

        var doc = await _dbContext.ComplianceDocs
            .FirstOrDefaultAsync(d => d.ComplianceDocId == complianceDocId && d.AgencyId == agencyId, cancellationToken);

        if (doc == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.COMPLIANCE_DOC_NOT_FOUND, "The requested compliance document could not be found.");
        }

        if (doc.Status != ComplianceDocStatus.Pending)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_COMPLIANCE_DOC_STATUS_TRANSITION, $"Only pending compliance documents can be reviewed (current status: '{doc.Status}').");
        }

        return doc;
    }

    private static ComplianceDocResponseDto MapToComplianceDocResponse(ComplianceDoc doc) => new()
    {
        ComplianceDocId = doc.ComplianceDocId,
        DocType = doc.DocType,
        DocNumber = doc.DocNumber,
        StorageKey = doc.StorageKey,
        IssuedOn = doc.IssuedOn,
        ExpiresOn = doc.ExpiresOn,
        Status = doc.Status
    };

    public async Task<IEnumerable<ComplianceDocResponseDto>> GetComplianceDocsAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        
        var docs = await _dbContext.ComplianceDocs
            .Where(d => d.AgencyId == agencyId)
            .Select(d => new ComplianceDocResponseDto
            {
                ComplianceDocId = d.ComplianceDocId,
                DocType = d.DocType,
                DocNumber = d.DocNumber,
                StorageKey = d.StorageKey,
                IssuedOn = d.IssuedOn,
                ExpiresOn = d.ExpiresOn,
                Status = d.Status
            })
            .ToListAsync(cancellationToken);

        return docs;
    }

    /// <inheritdoc />
    public async Task<VehicleResponseDto> AddVehicleAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, VehicleCreateDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agency = await _dbContext.Agencies.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

            if (agency == null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "Agency not found.");
            }

            AgencyStatusGuard.EnsureActive(agency.Status);
        }

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agencyId,
            RegistrationNo = request.RegistrationNo,
            VehicleType = request.VehicleType,
            CapacityKg = request.CapacityKg,
            VolumeM3 = request.VolumeM3,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Vehicles.Add(vehicle);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new VehicleResponseDto
        {
            VehicleId = vehicle.VehicleId,
            AgencyId = vehicle.AgencyId,
            RegistrationNo = vehicle.RegistrationNo,
            VehicleType = vehicle.VehicleType.ToString(),
            CapacityKg = vehicle.CapacityKg,
            VolumeM3 = vehicle.VolumeM3,
            Status = vehicle.Status.ToString(),
            CreatedAt = vehicle.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<VehicleResponseDto>> GetVehiclesAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        
        var vehicles = await _dbContext.Vehicles
            .Where(v => v.AgencyId == agencyId)
            .Select(v => new VehicleResponseDto
            {
                VehicleId = v.VehicleId,
                AgencyId = v.AgencyId,
                RegistrationNo = v.RegistrationNo,
                VehicleType = v.VehicleType.ToString(),
                CapacityKg = v.CapacityKg,
                VolumeM3 = v.VolumeM3,
                Status = v.Status.ToString(),
                CreatedAt = v.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return vehicles;
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
