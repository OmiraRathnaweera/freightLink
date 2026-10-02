using System.Net;
using System.Security.Cryptography;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Email;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAgencyService" />
public class AgencyService : IAgencyService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService? _emailService;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AgencyService>? _logger;

    /// <summary>Creates the agency service with its DB context, password hasher, and email sender.</summary>
    public AgencyService(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IEmailService? emailService = null,
        IOptions<EmailOptions>? emailOptions = null,
        ILogger<AgencyService>? logger = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _emailOptions = emailOptions?.Value ?? new EmailOptions();
        _logger = logger;
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
            .Include(a => a.Drivers)
            .Include(a => a.Vehicles)
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

        IQueryable<Agency> dbQuery = _dbContext.Agencies.AsNoTracking()
            .Include(a => a.Drivers)
            .Include(a => a.Vehicles);

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
    public async Task<AgencyPlatformSummaryDto> GetPlatformSummaryAsync(Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators can view platform-wide agency summary.");
        }

        // Direct COUNT queries against the whole table - never paged through and summed
        // client-side, and never derived from a search/status-filtered listing - so these numbers
        // stay accurate no matter how many agencies exist or what the admin's list view is
        // currently filtered to (issue #45).
        return new AgencyPlatformSummaryDto
        {
            TotalAgencies = await _dbContext.Agencies.CountAsync(cancellationToken),
            ActiveAgencies = await _dbContext.Agencies.CountAsync(a => a.Status == AgencyStatus.Active, cancellationToken),
            TotalDrivers = await _dbContext.Drivers.CountAsync(cancellationToken),
            ActiveDrivers = await _dbContext.Drivers.CountAsync(d => d.Status == DriverStatus.Active, cancellationToken),
            TotalVehicles = await _dbContext.Vehicles.CountAsync(cancellationToken)
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
        await ChangeStatusAsync(agencyId, AgencyStatus.Verified, "Verified by admin", currentUserId, currentUserRole, cancellationToken);
    }

    public async Task ActivateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await ChangeStatusAsync(agencyId, AgencyStatus.Active, "Activated by admin", currentUserId, currentUserRole, cancellationToken);
    }

    public async Task SuspendAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await ChangeStatusAsync(agencyId, AgencyStatus.Suspended, "Suspended by admin", currentUserId, currentUserRole, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> UpdateStatusAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, UpdateAgencyStatusDto request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "A reason is required to change an agency's status.");
        }

        var agency = await ChangeStatusAsync(agencyId, request.Status, reason, currentUserId, currentUserRole, cancellationToken);
        return MapToResponse(agency);
    }

    /// <summary>
    /// Applies a status change after validating it against <see cref="AgencyStatusTransitionRules"/> and
    /// appends the matching <see cref="AgencyStatusHistory"/> audit row (actor, from, to, reason, time).
    /// </summary>
    private async Task<Agency> ChangeStatusAsync(Guid agencyId, AgencyStatus newStatus, string reason, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        // Ownership alone lets an agency's own staff through; status changes are Admin-only so a
        // suspended agency can never reactivate itself, even if a controller attribute regresses.
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only an Admin can change an agency's status.");
        }

        var agency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        AgencyStatusTransitionRules.ValidateTransition(agency.Status, newStatus);

        var now = DateTimeOffset.UtcNow;
        var oldStatus = agency.Status;
        agency.Status = newStatus;
        agency.UpdatedAt = now;
        agency.StatusHistory.Add(new AgencyStatusHistory
        {
            FromStatus = oldStatus,
            ToStatus = newStatus,
            ChangedByUserId = currentUserId,
            Reason = reason,
            ChangedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return agency;
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
            VehicleType = request.VehicleType!.Value,
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
            IsAvailable = true,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<VehicleResponseDto> UpdateVehicleAsync(
        Guid agencyId,
        Guid vehicleId,
        Guid currentUserId,
        UserRole currentUserRole,
        VehicleUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only agency staff may edit fleet vehicles.");
        }

        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);
        if (agency is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "Agency not found.");
        }
        AgencyStatusGuard.EnsureActive(agency.Status);

        var vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(
            v => v.VehicleId == vehicleId && v.AgencyId == agencyId,
            cancellationToken);
        if (vehicle is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.VEHICLE_NOT_FOUND, "Vehicle not found in this agency fleet.");
        }
        if (vehicle.Status is VehicleStatus.OnTrip or VehicleStatus.Retired)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.VEHICLE_CANNOT_BE_MODIFIED,
                "Vehicles on a trip or retired cannot have their fleet details changed.");
        }

        if (string.IsNullOrWhiteSpace(request.RegistrationNo))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Registration number is required.");
        }
        var registrationNo = request.RegistrationNo.Trim().ToUpperInvariant();
        if (await _dbContext.Vehicles.AnyAsync(
            v => v.AgencyId == agencyId && v.VehicleId != vehicleId && v.RegistrationNo == registrationNo,
            cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.VEHICLE_REGISTRATION_ALREADY_EXISTS,
                "A vehicle with this registration number already exists in your fleet.");
        }

        vehicle.RegistrationNo = registrationNo;
        vehicle.VehicleType = request.VehicleType!.Value;
        vehicle.CapacityKg = request.CapacityKg;
        vehicle.VolumeM3 = request.VolumeM3;
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_vehicle_agency_regno" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.VEHICLE_REGISTRATION_ALREADY_EXISTS,
                "A vehicle with this registration number already exists in your fleet.");
        }

        return new VehicleResponseDto
        {
            VehicleId = vehicle.VehicleId,
            AgencyId = vehicle.AgencyId,
            RegistrationNo = vehicle.RegistrationNo,
            VehicleType = vehicle.VehicleType.ToString(),
            CapacityKg = vehicle.CapacityKg,
            VolumeM3 = vehicle.VolumeM3,
            Status = vehicle.Status.ToString(),
            IsAvailable = vehicle.Status == VehicleStatus.Available,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<VehicleResponseDto> UpdateVehicleStatusAsync(
        Guid agencyId,
        Guid vehicleId,
        Guid currentUserId,
        UserRole currentUserRole,
        UpdateVehicleStatusDto request,
        CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);
        if (agency is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }
        AgencyStatusGuard.EnsureActive(agency.Status);

        var vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(
            v => v.VehicleId == vehicleId && v.AgencyId == agencyId,
            cancellationToken);
        if (vehicle is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.VEHICLE_NOT_FOUND, "The requested vehicle could not be found in this agency fleet.");
        }

        // Trip assignment/execution is the sole authority for OnTrip. Retired vehicles are
        // deliberately terminal so matching cannot accidentally revive a decommissioned vehicle.
        var targetStatus = request.Status!.Value;
        if (targetStatus == VehicleStatus.OnTrip || vehicle.Status == VehicleStatus.OnTrip ||
            (vehicle.Status == VehicleStatus.Retired && targetStatus != VehicleStatus.Retired))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_VEHICLE_STATUS_TRANSITION,
                "Vehicle availability can only move between Available, Maintenance, and Retired. OnTrip is managed by trip execution and Retired is terminal.");
        }

        vehicle.Status = targetStatus;
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;
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
            IsAvailable = vehicle.Status == VehicleStatus.Available,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<DriverResponseDto> UpdateDriverAsync(Guid agencyId, Guid driverId, Guid currentUserId, UserRole currentUserRole, DriverUpdateDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var driver = await _dbContext.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId && d.AgencyId == agencyId, cancellationToken);

        if (driver == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DRIVER_NOT_FOUND, "Driver not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            driver.User.FullName = request.FullName;
        }

        if (!string.IsNullOrWhiteSpace(request.LicenceNo) && request.LicenceNo != driver.LicenceNo)
        {
            var licenceExists = await _dbContext.Drivers.AnyAsync(d => d.LicenceNo == request.LicenceNo && d.DriverId != driverId, cancellationToken);
            if (licenceExists)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LICENCE_ALREADY_REGISTERED, "License number is already registered.");
            }
            driver.LicenceNo = request.LicenceNo;
        }

        if (request.LicenceExpiry.HasValue)
        {
            driver.LicenceExpiry = request.LicenceExpiry.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DriverResponseDto
        {
            DriverId = driver.DriverId,
            UserId = driver.UserId,
            AgencyId = driver.AgencyId,
            Email = driver.User.Email,
            FullName = driver.User.FullName,
            LicenceNo = driver.LicenceNo,
            LicenceExpiry = driver.LicenceExpiry,
            Status = driver.Status,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<DriverResponseDto> UpdateDriverStatusAsync(Guid agencyId, Guid driverId, Guid currentUserId, UserRole currentUserRole, UpdateDriverStatusDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var driver = await _dbContext.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId && d.AgencyId == agencyId, cancellationToken);

        if (driver == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DRIVER_NOT_FOUND, "Driver not found.");
        }

        // Trip assignment/execution is the sole authority for OnTrip, mirroring vehicle availability:
        // staff may only toggle a driver between Active and Inactive.
        if (request.Status == DriverStatus.OnTrip || driver.Status == DriverStatus.OnTrip)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_DRIVER_STATUS_TRANSITION,
                "Driver roster status can only move between Active and Inactive. OnTrip is managed by trip execution.");
        }

        driver.Status = request.Status;
        driver.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DriverResponseDto
        {
            DriverId = driver.DriverId,
            UserId = driver.UserId,
            AgencyId = driver.AgencyId,
            Email = driver.User.Email,
            FullName = driver.User.FullName,
            LicenceNo = driver.LicenceNo,
            LicenceExpiry = driver.LicenceExpiry,
            Status = driver.Status,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt
        };
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
            Status = d.Status,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<DriverResponseDto> AddDriverAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CreateDriverRequestDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);
        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (request.LicenceExpiry <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Licence expiry date must be in the future.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
        }

        if (await _dbContext.Drivers.AnyAsync(d => d.LicenceNo == request.LicenceNo, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_LICENCE_ALREADY_REGISTERED, "A driver with this driving licence number already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var temporaryPassword = GenerateTemporaryPassword();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Driver,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(temporaryPassword),
            FullName = request.FullName,
            PhoneE164 = request.PhoneE164,
            IsActive = true,
            // The employing Agency is vouching for this driver's identity/email (there is no
            // separate driver-side verification step in this flow), so the account must be
            // immediately usable with the emailed credentials rather than blocked behind
            // EmailOptions.RequireEmailVerification.
            EmailVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            UserId = user.UserId,
            AgencyId = agencyId,
            LicenceNo = request.LicenceNo,
            LicenceExpiry = request.LicenceExpiry,
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(user);
        _dbContext.Drivers.Add(driver);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            if (pg.ConstraintName == "uq_user_email")
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
            }
            if (pg.ConstraintName == "IX_Drivers_LicenceNo")
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_LICENCE_ALREADY_REGISTERED, "A driver with this driving licence number already exists.");
            }
            throw;
        }

        await TrySendDriverCredentialsAsync(user, agency.Name, temporaryPassword, cancellationToken);

        return new DriverResponseDto
        {
            DriverId = driver.DriverId,
            UserId = user.UserId,
            AgencyId = driver.AgencyId,
            FullName = user.FullName,
            Email = user.Email,
            LicenceNo = driver.LicenceNo,
            LicenceExpiry = driver.LicenceExpiry,
            Status = driver.Status,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt,
            TemporaryPassword = temporaryPassword
        };
    }

    /// <summary>
    /// Generates a cryptographically random password satisfying <see cref="Common.Validation.StrongPasswordAttribute"/>
    /// (upper, lower, digit, special character, 12 characters) for a newly-onboarded driver.
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string uppers = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // no I/O — avoids visual ambiguity
        const string lowers = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string specials = "!@#$%^&*?";
        const string all = uppers + lowers + digits + specials;
        const int length = 12;

        var chars = new char[length];
        chars[0] = uppers[RandomNumberGenerator.GetInt32(uppers.Length)];
        chars[1] = lowers[RandomNumberGenerator.GetInt32(lowers.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = specials[RandomNumberGenerator.GetInt32(specials.Length)];
        for (var i = 4; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Fisher-Yates shuffle so the guaranteed-category characters aren't always in positions 0-3.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    /// <summary>
    /// Emails the newly-onboarded driver their login email and temporary password. Failures are
    /// logged, never thrown — the account is fully created and usable (the Agency also sees the
    /// temporary password once in the API response) even if the email can't be delivered.
    /// </summary>
    private async Task TrySendDriverCredentialsAsync(User driverUser, string agencyName, string temporaryPassword, CancellationToken cancellationToken)
    {
        if (_emailService is null)
        {
            return;
        }

        try
        {
            var loginUrl = string.IsNullOrWhiteSpace(_emailOptions.FrontendBaseUrl)
                ? string.Empty
                : $"{_emailOptions.FrontendBaseUrl.TrimEnd('/')}/login";

            var (subject, htmlBody, textBody) = EmailTemplates.BuildDriverCredentials(
                driverUser.FullName, driverUser.Email, temporaryPassword, agencyName, loginUrl);

            await _emailService.SendAsync(new EmailMessage
            {
                To = driverUser.Email,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody,
                Metadata = new Dictionary<string, string> { ["template"] = "driver-credentials" }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unable to send driver-credentials email to {Email}.", driverUser.Email);
        }
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

    /// <inheritdoc />
    public async Task SeedDefaultAgenciesIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var defaultPasswordHash = _passwordHasher.Hash("Password123!");

        var seedAgencies = new[]
        {
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224ba"),
                Name = "Samagi Express Logistics",
                RegNo = "PV-88991",
                Address = "45 Harbor Access Road, Peliyagoda",
                Lat = 6.9667m,
                Lng = 79.8917m,
                StaffEmail = "agency@freightlink.lk",
                StaffName = "Kasun Jayasuriya",
                Vehicles = new[]
                {
                    (RegNo: "WP-CAB-4521", Type: VehicleType.Lorry, CapKg: 6500m, VolM3: 30m),
                    (RegNo: "WP-DA-8920", Type: VehicleType.Container, CapKg: 18000m, VolM3: 65m),
                    (RegNo: "WP-LB-6712", Type: VehicleType.FlatBed, CapKg: 12000m, VolM3: 45m),
                    (RegNo: "WP-ND-1102", Type: VehicleType.Lorry, CapKg: 1400m, VolM3: 5.5m)
                },
                Drivers = new[]
                {
                    (Email: "suneth.driver@samagi.lk", Name: "Suneth Alwis", Phone: "+94771234501", LicenceNo: "B-8839210", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "kamal.driver@samagi.lk", Name: "Kamal Weerasinghe", Phone: "+94771234502", LicenceNo: "B-8839211", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bb"),
                Name = "Lanka Freight & Cargo Hub",
                RegNo = "PV-77102",
                Address = "120 Baseline Road, Dematagoda, Colombo 09",
                Lat = 6.9380m,
                Lng = 79.8780m,
                StaffEmail = "lanka.staff@freightlink.lk",
                StaffName = "Dinesh Perera",
                Vehicles = new[]
                {
                    (RegNo: "WP-LC-1029", Type: VehicleType.Lorry, CapKg: 3500m, VolM3: 14m),
                    (RegNo: "WP-LH-5519", Type: VehicleType.Lorry, CapKg: 8500m, VolM3: 36m),
                    (RegNo: "WP-CX-9011", Type: VehicleType.Container, CapKg: 24000m, VolM3: 70m),
                    (RegNo: "WP-ND-2045", Type: VehicleType.Lorry, CapKg: 1200m, VolM3: 5.0m)
                },
                Drivers = new[]
                {
                    (Email: "priyantha.driver@lankafreight.lk", Name: "Priyantha Kumara", Phone: "+94771234503", LicenceNo: "B-7740192", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "ruwan.driver@lankafreight.lk", Name: "Ruwan Gamage", Phone: "+94771234504", LicenceNo: "B-7740193", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bc"),
                Name = "Southern Coastal Haulage",
                RegNo = "PV-65401",
                Address = "78 Matara Road, Galle",
                Lat = 6.0354m,
                Lng = 80.2170m,
                StaffEmail = "southern.staff@freightlink.lk",
                StaffName = "Rohan Mendis",
                Vehicles = new[]
                {
                    (RegNo: "SP-SC-4401", Type: VehicleType.Lorry, CapKg: 5000m, VolM3: 22m),
                    (RegNo: "SP-SC-8812", Type: VehicleType.Container, CapKg: 19000m, VolM3: 55m),
                    (RegNo: "SP-SC-1092", Type: VehicleType.Lorry, CapKg: 1500m, VolM3: 6.0m)
                },
                Drivers = new[]
                {
                    (Email: "anura.driver@southernhaul.lk", Name: "Anura Jayatissa", Phone: "+94771234506", LicenceNo: "B-6591023", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "lasantha.driver@southernhaul.lk", Name: "Lasantha Fernando", Phone: "+94771234507", LicenceNo: "B-6591024", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bd"),
                Name = "Central Highlands Express",
                RegNo = "PV-91044",
                Address = "15 Kandy Industrial Estate, Peradeniya",
                Lat = 7.2600m,
                Lng = 80.5980m,
                StaffEmail = "kandy.staff@freightlink.lk",
                StaffName = "Nuwan Bandara",
                Vehicles = new[]
                {
                    (RegNo: "CP-CH-1205", Type: VehicleType.Lorry, CapKg: 4000m, VolM3: 18m),
                    (RegNo: "CP-CH-6520", Type: VehicleType.Lorry, CapKg: 9000m, VolM3: 38m),
                    (RegNo: "CP-CH-8901", Type: VehicleType.Container, CapKg: 20000m, VolM3: 60m),
                    (RegNo: "CP-CH-3310", Type: VehicleType.Lorry, CapKg: 1300m, VolM3: 5.2m)
                },
                Drivers = new[]
                {
                    (Email: "nihal.driver@centralhigh.lk", Name: "Nihal Bandara", Phone: "+94771234508", LicenceNo: "B-9140221", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "janaka.driver@centralhigh.lk", Name: "Janaka Senanayake", Phone: "+94771234509", LicenceNo: "B-9140222", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224be"),
                Name = "Wayamba Regional Transporters",
                RegNo = "PV-44320",
                Address = "34 Dambulla Road, Kurunegala",
                Lat = 7.4863m,
                Lng = 80.3623m,
                StaffEmail = "wayamba.staff@freightlink.lk",
                StaffName = "Charith Wickramasinghe",
                Vehicles = new[]
                {
                    (RegNo: "NW-WR-3310", Type: VehicleType.Lorry, CapKg: 7500m, VolM3: 32m),
                    (RegNo: "NW-WR-7799", Type: VehicleType.Container, CapKg: 22000m, VolM3: 62m),
                    (RegNo: "NW-WR-1188", Type: VehicleType.Lorry, CapKg: 1450m, VolM3: 5.8m)
                },
                Drivers = new[]
                {
                    (Email: "sisira.driver@wayambafreight.lk", Name: "Sisira Dissanayake", Phone: "+94771234510", LicenceNo: "B-4410982", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4))),
                    (Email: "asanka.driver@wayambafreight.lk", Name: "Asanka Ranatunga", Phone: "+94771234511", LicenceNo: "B-4410983", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bf"),
                Name = "Kelani Valley Logistics Hub",
                RegNo = "PV-55109",
                Address = "88 Biyagama Free Trade Zone Road, Kelaniya",
                Lat = 6.9535m,
                Lng = 79.9240m,
                StaffEmail = "kelani.staff@freightlink.lk",
                StaffName = "Harsha Gunawardena",
                Vehicles = new[]
                {
                    (RegNo: "WP-KV-1011", Type: VehicleType.Lorry, CapKg: 1500m, VolM3: 6.0m),
                    (RegNo: "WP-KV-4420", Type: VehicleType.Lorry, CapKg: 7000m, VolM3: 28m),
                    (RegNo: "WP-KV-9901", Type: VehicleType.Container, CapKg: 25000m, VolM3: 68m)
                },
                Drivers = new[]
                {
                    (Email: "mahesh.driver@kelanifreight.lk", Name: "Mahesh Senarath", Phone: "+94771234512", LicenceNo: "B-5519821", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "tharindu.driver@kelanifreight.lk", Name: "Tharindu Perera", Phone: "+94771234513", LicenceNo: "B-5519822", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)))
                }
            }
        };

        foreach (var item in seedAgencies)
        {
            var agency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == item.Id || a.BusinessRegNo == item.RegNo, cancellationToken);
            if (agency == null)
            {
                agency = new Agency
                {
                    AgencyId = item.Id,
                    Name = item.Name,
                    BusinessRegNo = item.RegNo,
                    YardAddress = item.Address,
                    YardLat = item.Lat,
                    YardLng = item.Lng,
                    Status = AgencyStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Agencies.Add(agency);
            }
            else
            {
                agency.Status = AgencyStatus.Active;
                agency.YardAddress = item.Address;
                agency.YardLat = item.Lat;
                agency.YardLng = item.Lng;
                agency.UpdatedAt = now;
            }

            // Ensure Staff user exists
            var staffUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == item.StaffEmail, cancellationToken);
            if (staffUser == null)
            {
                staffUser = new User
                {
                    UserId = Guid.NewGuid(),
                    Role = UserRole.AgencyStaff,
                    Email = item.StaffEmail,
                    FullName = item.StaffName,
                    PasswordHash = defaultPasswordHash,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Users.Add(staffUser);
            }

            var hasStaffLink = await _dbContext.AgencyStaff.AnyAsync(s => s.UserId == staffUser.UserId && s.AgencyId == agency.AgencyId, cancellationToken);
            if (!hasStaffLink)
            {
                _dbContext.AgencyStaff.Add(new AgencyStaff
                {
                    UserId = staffUser.UserId,
                    AgencyId = agency.AgencyId,
                    JobTitle = "Operations Dispatcher",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // Ensure Fleet Vehicles exist
            foreach (var v in item.Vehicles)
            {
                var vehicleExists = await _dbContext.Vehicles.AnyAsync(veh => veh.RegistrationNo == v.RegNo, cancellationToken);
                if (!vehicleExists)
                {
                    _dbContext.Vehicles.Add(new Vehicle
                    {
                        VehicleId = Guid.NewGuid(),
                        AgencyId = agency.AgencyId,
                        RegistrationNo = v.RegNo,
                        VehicleType = v.Type,
                        CapacityKg = v.CapKg,
                        VolumeM3 = v.VolM3,
                        Status = VehicleStatus.Available,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            // Ensure Real Drivers exist
            foreach (var d in item.Drivers)
            {
                var driverUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == d.Email, cancellationToken);
                if (driverUser == null)
                {
                    driverUser = new User
                    {
                        UserId = Guid.NewGuid(),
                        Role = UserRole.Driver,
                        Email = d.Email,
                        FullName = d.Name,
                        PhoneE164 = d.Phone,
                        PasswordHash = defaultPasswordHash,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Users.Add(driverUser);
                }

                var driverExists = await _dbContext.Drivers.AnyAsync(dr => dr.LicenceNo == d.LicenceNo || dr.UserId == driverUser.UserId, cancellationToken);
                if (!driverExists)
                {
                    _dbContext.Drivers.Add(new Driver
                    {
                        DriverId = Guid.NewGuid(),
                        UserId = driverUser.UserId,
                        AgencyId = agency.AgencyId,
                        LicenceNo = d.LicenceNo,
                        LicenceExpiry = d.Expiry,
                        Status = DriverStatus.Active,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
            DriverCount = agency.Drivers?.Count ?? 0,
            ActiveDriverCount = agency.Drivers?.Count(d => d.Status == DriverStatus.Active) ?? 0,
            VehicleCount = agency.Vehicles?.Count ?? 0,
            CreatedAt = agency.CreatedAt,
            UpdatedAt = agency.UpdatedAt
        };
    }
}
