using System.Net;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="ITripService" />
public class TripService : ITripService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the trip service with its DB context.</summary>
    public TripService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PagedTripResponseDto> GetListAsync(
        TripListQueryDto query,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (long)(page - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.LOAD_PAGE_OUT_OF_RANGE, "The requested page/pageSize combination is out of range.");
        }

        IQueryable<Trip> tripsQuery = _dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Driver)
                .ThenInclude(d => d.User)
            .Include(t => t.Vehicle);

        if (currentUserRole == UserRole.Admin)
        {
            if (query.AgencyId.HasValue)
            {
                tripsQuery = tripsQuery.Where(t => t.Assignment.AgencyId == query.AgencyId.Value);
            }

            if (query.DriverId.HasValue)
            {
                tripsQuery = tripsQuery.Where(t => t.DriverId == query.DriverId.Value);
            }
        }
        else if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null)
            {
                tripsQuery = tripsQuery.Where(t => false);
            }
            else
            {
                tripsQuery = tripsQuery.Where(t => t.Assignment.AgencyId == agencyStaff.AgencyId);
                if (query.DriverId.HasValue)
                {
                    tripsQuery = tripsQuery.Where(t => t.DriverId == query.DriverId.Value);
                }
            }
        }
        else if (currentUserRole == UserRole.Driver)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == currentUserId, cancellationToken);

            if (driver == null)
            {
                tripsQuery = tripsQuery.Where(t => false);
            }
            else
            {
                tripsQuery = tripsQuery.Where(t => t.DriverId == driver.DriverId);
            }
        }
        else
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to list trips.");
        }

        if (query.Status.HasValue)
        {
            tripsQuery = tripsQuery.Where(t => t.Status == query.Status.Value);
        }

        tripsQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedat" => query.SortDir?.ToLowerInvariant() == "asc"
                ? tripsQuery.OrderBy(t => t.UpdatedAt)
                : tripsQuery.OrderByDescending(t => t.UpdatedAt),
            _ => query.SortDir?.ToLowerInvariant() == "asc"
                ? tripsQuery.OrderBy(t => t.CreatedAt)
                : tripsQuery.OrderByDescending(t => t.CreatedAt)
        };

        var totalItems = await tripsQuery.CountAsync(cancellationToken);
        var pageOfTrips = await tripsQuery
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedTripResponseDto
        {
            Items = pageOfTrips.Select(MapToListItem).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    /// <inheritdoc />
    public async Task<TripResponseDto> GetByIdAsync(
        Guid tripId,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Driver)
                .ThenInclude(d => d.User)
            .Include(t => t.Vehicle)
            .Include(t => t.Events)
            .Include(t => t.Evidence)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        await VerifyTripReadAccessAsync(trip, currentUserId, currentUserRole, cancellationToken);

        return MapToDetailResponse(trip);
    }

    /// <inheritdoc />
    public async Task<TripResponseDto> ChangeStatusAsync(
        Guid tripId,
        Guid actingUserId,
        UserRole actingUserRole,
        ChangeTripStatusDto request,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Driver)
                .ThenInclude(d => d.User)
            .Include(t => t.Vehicle)
            .Include(t => t.Evidence)
            .Include(t => t.Events)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        if (actingUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == actingUserId, cancellationToken);

            if (agencyStaff == null || trip.Assignment.AgencyId != agencyStaff.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to advance status on this trip.");
            }

            AgencyStatusGuard.EnsureActive(trip.Assignment.Agency.Status);
        }
        else if (actingUserRole == UserRole.Driver)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == actingUserId, cancellationToken);

            if (driver == null || trip.DriverId != driver.DriverId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to advance status on this trip.");
            }
        }
        else
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to advance status on this trip.");
        }

        if (!request.TargetStatus.HasValue)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Target status is required.");
        }

        var target = request.TargetStatus.Value;

        if (!IsValidTransition(trip.Status, target))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, $"Trip cannot transition from '{trip.Status}' to '{target}'.");
        }

        if (target == TripStatus.PickedUp && !trip.Evidence.Any(e => e.EvidenceType == EvidenceType.PickupProof))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.TRIP_EVIDENCE_REQUIRED, "Proof of pickup is required before advancing status to PickedUp.");
        }

        if (target == TripStatus.Delivered && !trip.Evidence.Any(e => e.EvidenceType == EvidenceType.DeliveryProof))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.TRIP_EVIDENCE_REQUIRED, "Proof of delivery is required before advancing status to Delivered.");
        }

        var now = DateTimeOffset.UtcNow;
        var tripEvent = new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = trip.TripId,
            RecordedByUserId = actingUserId,
            FromStatus = trip.Status,
            ToStatus = target,
            Notes = request.Notes,
            SnapshotLat = request.SnapshotLat,
            SnapshotLng = request.SnapshotLng,
            OccurredAt = now
        };

        _dbContext.TripEvents.Add(tripEvent);
        trip.Status = target;
        trip.UpdatedAt = now;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pex && pex.MessageText.Contains("evidence"))
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.TRIP_EVIDENCE_REQUIRED, "Proof of evidence is required before advancing status.");
        }

        return MapToDetailResponse(trip);
    }

    /// <inheritdoc />
    public async Task<TripEvidenceResponseDto> UploadEvidenceAsync(
        Guid tripId,
        Guid actingUserId,
        UserRole actingUserRole,
        UploadTripEvidenceDto request,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        if (!request.EvidenceType.HasValue)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "EvidenceType is required.");
        }

        if (actingUserRole == UserRole.AgencyStaff && request.EvidenceType.Value != EvidenceType.PickupProof)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_EVIDENCE_ROLE_MISMATCH, "Agency staff may only submit Proof of Pickup.");
        }

        if (actingUserRole == UserRole.Driver && request.EvidenceType.Value != EvidenceType.DeliveryProof)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_EVIDENCE_ROLE_MISMATCH, "Drivers may only submit Proof of Delivery.");
        }

        if (actingUserRole != UserRole.AgencyStaff && actingUserRole != UserRole.Driver)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "Only AgencyStaff and Drivers may submit trip evidence.");
        }

        if (actingUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == actingUserId, cancellationToken);

            if (agencyStaff == null || trip.Assignment.AgencyId != agencyStaff.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to submit evidence for this trip.");
            }

            AgencyStatusGuard.EnsureActive(trip.Assignment.Agency.Status);
        }
        else if (actingUserRole == UserRole.Driver)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == actingUserId, cancellationToken);

            if (driver == null || trip.DriverId != driver.DriverId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to submit evidence for this trip.");
            }
        }

        var alreadyExists = await _dbContext.TripEvidences
            .AnyAsync(e => e.TripId == tripId && e.EvidenceType == request.EvidenceType.Value, cancellationToken);

        if (alreadyExists)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.TRIP_EVIDENCE_ALREADY_EXISTS, $"Evidence of type '{request.EvidenceType}' has already been submitted for this trip.");
        }

        var evidence = new TripEvidence
        {
            TripEvidenceId = Guid.NewGuid(),
            TripId = tripId,
            CapturedByUserId = actingUserId,
            EvidenceType = request.EvidenceType.Value,
            StorageKey = request.PublicId,
            CapturedLat = request.CapturedLat,
            CapturedLng = request.CapturedLng,
            CapturedAt = DateTimeOffset.UtcNow
        };

        _dbContext.TripEvidences.Add(evidence);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.TRIP_EVIDENCE_ALREADY_EXISTS, $"Evidence of type '{request.EvidenceType}' has already been submitted for this trip.");
        }

        return MapToEvidenceResponse(evidence);
    }

    /// <inheritdoc />
    public async Task<List<TripEvidenceResponseDto>> GetEvidenceAsync(
        Guid tripId,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        await VerifyTripReadAccessAsync(trip, currentUserId, currentUserRole, cancellationToken);

        var evidences = await _dbContext.TripEvidences
            .AsNoTracking()
            .Where(e => e.TripId == tripId)
            .OrderByDescending(e => e.CapturedAt)
            .ToListAsync(cancellationToken);

        return evidences.Select(MapToEvidenceResponse).ToList();
    }

    private async Task VerifyTripReadAccessAsync(Trip trip, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken)
    {
        if (currentUserRole == UserRole.Admin)
        {
            return;
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null || trip.Assignment.AgencyId != agencyStaff.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to view this trip.");
            }
            return;
        }

        if (currentUserRole == UserRole.Driver)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == currentUserId, cancellationToken);

            if (driver == null || trip.DriverId != driver.DriverId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to view this trip.");
            }
            return;
        }

        if (currentUserRole == UserRole.Shipper)
        {
            if (trip.Assignment?.Load?.ShipperUserId != currentUserId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to view this trip.");
            }
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to view this trip.");
    }

    private static bool IsValidTransition(TripStatus current, TripStatus target) =>
        (current, target) switch
        {
            (TripStatus.Assigned, TripStatus.PickedUp) => true,
            (TripStatus.Assigned, TripStatus.Cancelled) => true,
            (TripStatus.PickedUp, TripStatus.InTransit) => true,
            (TripStatus.PickedUp, TripStatus.Cancelled) => true,
            (TripStatus.InTransit, TripStatus.Delivered) => true,
            (TripStatus.InTransit, TripStatus.Cancelled) => true,
            _ => false
        };

    private static TripListItemDto MapToListItem(Trip t) => new()
    {
        TripId = t.TripId,
        AssignmentId = t.AssignmentId,
        LoadId = t.Assignment?.LoadId ?? Guid.Empty,
        AgencyId = t.Assignment?.AgencyId ?? Guid.Empty,
        AgencyName = t.Assignment?.Agency?.Name,
        VehicleId = t.VehicleId,
        DriverId = t.DriverId,
        DriverName = t.Driver?.User?.FullName,
        PickupAddress = t.Assignment?.Load?.PickupAddress,
        DropoffAddress = t.Assignment?.Load?.DropoffAddress,
        ReferenceCode = t.Assignment?.Load?.ReferenceCode,
        Status = t.Status.ToString(),
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    private static TripResponseDto MapToDetailResponse(Trip t) => new()
    {
        TripId = t.TripId,
        AssignmentId = t.AssignmentId,
        LoadId = t.Assignment?.LoadId ?? Guid.Empty,
        AgencyId = t.Assignment?.AgencyId ?? Guid.Empty,
        AgencyName = t.Assignment?.Agency?.Name,
        VehicleId = t.VehicleId,
        VehicleRegistrationNo = t.Vehicle?.RegistrationNo,
        DriverId = t.DriverId,
        DriverName = t.Driver?.User?.FullName,
        PickupAddress = t.Assignment?.Load?.PickupAddress,
        DropoffAddress = t.Assignment?.Load?.DropoffAddress,
        PickupLat = t.Assignment?.Load?.PickupLat,
        PickupLng = t.Assignment?.Load?.PickupLng,
        DropoffLat = t.Assignment?.Load?.DropoffLat,
        DropoffLng = t.Assignment?.Load?.DropoffLng,
        CargoDescription = t.Assignment?.Load?.CargoDescription,
        WeightKg = t.Assignment?.Load?.WeightKg,
        VolumeM3 = t.Assignment?.Load?.VolumeM3,
        PickupWindowStart = t.Assignment?.Load?.PickupWindowStart,
        PickupWindowEnd = t.Assignment?.Load?.PickupWindowEnd,
        ReferenceCode = t.Assignment?.Load?.ReferenceCode,
        RoutedDistanceKm = t.Assignment?.RoutedDistanceKm,
        ProposedEtaMinutes = t.Assignment?.ProposedEtaMinutes,
        Status = t.Status.ToString(),
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        Events = t.Events
            .OrderByDescending(e => e.OccurredAt)
            .Select(MapToEventResponse)
            .ToList(),
        Evidence = t.Evidence
            .OrderByDescending(e => e.CapturedAt)
            .Select(MapToEvidenceResponse)
            .ToList()
    };

    private static TripEventResponseDto MapToEventResponse(TripEvent e) => new()
    {
        TripEventId = e.TripEventId,
        RecordedByUserId = e.RecordedByUserId,
        FromStatus = e.FromStatus?.ToString(),
        ToStatus = e.ToStatus.ToString(),
        Notes = e.Notes,
        SnapshotLat = e.SnapshotLat,
        SnapshotLng = e.SnapshotLng,
        OccurredAt = e.OccurredAt
    };

    private static TripEvidenceResponseDto MapToEvidenceResponse(TripEvidence e) => new()
    {
        TripEvidenceId = e.TripEvidenceId,
        CapturedByUserId = e.CapturedByUserId,
        EvidenceType = e.EvidenceType.ToString(),
        StorageKey = e.StorageKey,
        SecureUrl = e.StorageKey.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? e.StorageKey
            : $"/api/v1/files/content/{e.StorageKey}",
        CapturedLat = e.CapturedLat,
        CapturedLng = e.CapturedLng,
        CapturedAt = e.CapturedAt
    };

    /// <inheritdoc />
    public async Task<TripResponseDto> CreateAsync(
        CreateTripDto request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin && currentUserRole != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "Only Agency Staff or Admin may create and dispatch trips.");
        }

        Guid? callerAgencyId = null;
        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "No agency staff record found for caller.");
            }
            callerAgencyId = agencyStaff.AgencyId;
        }

        var assignment = await _dbContext.Assignments
            .Include(a => a.Load)
            .Include(a => a.Agency)
            .Include(a => a.Trip)
            .FirstOrDefaultAsync(a => a.AssignmentId == request.AssignmentId, cancellationToken);

        if (assignment == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.ASSIGNMENT_NOT_FOUND, "The requested assignment could not be found.");
        }

        if (callerAgencyId.HasValue && assignment.AgencyId != callerAgencyId.Value)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.ASSIGNMENT_NOT_OWNED, "You do not have access to dispatch this assignment.");
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            AgencyStatusGuard.EnsureActive(assignment.Agency.Status);
        }

        if (assignment.Trip != null)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.TRIP_ALREADY_EXISTS, "A trip has already been created for this assignment.");
        }

        if (assignment.Status == AssignmentStatus.Declined)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Declined assignments cannot be dispatched into a trip.");
        }

        if (assignment.Status == AssignmentStatus.Cancelled)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Cancelled assignments cannot be dispatched into a trip.");
        }

        // Validate vehicle
        var vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.VEHICLE_NOT_FOUND, "The requested vehicle could not be found.");
        }

        if (vehicle.AgencyId != assignment.AgencyId)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_NOT_OWNED, "The assigned vehicle must belong to the executing agency.");
        }

        if (vehicle.Status == VehicleStatus.Maintenance || vehicle.Status == VehicleStatus.Retired)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.VEHICLE_UNAVAILABLE, "The selected vehicle is not available (in maintenance or retired).");
        }

        var isVehicleBusy = await _dbContext.Trips
            .AnyAsync(t => t.VehicleId == request.VehicleId &&
                           (t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit),
                      cancellationToken);

        if (isVehicleBusy)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.VEHICLE_UNAVAILABLE, "The selected vehicle is currently engaged in another active trip.");
        }

        // Validate driver
        var driver = await _dbContext.Drivers
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DriverId == request.DriverId, cancellationToken);

        if (driver == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DRIVER_NOT_FOUND, "The requested driver could not be found.");
        }

        if (driver.AgencyId != assignment.AgencyId)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.DRIVER_NOT_OWNED, "The assigned driver must belong to the executing agency.");
        }

        if (driver.Status != DriverStatus.Active)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.DRIVER_UNAVAILABLE, "The selected driver is not active.");
        }

        var isDriverBusy = await _dbContext.Trips
            .AnyAsync(t => t.DriverId == request.DriverId &&
                           (t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit),
                      cancellationToken);

        if (isDriverBusy)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_UNAVAILABLE, "The selected driver is currently engaged in another active trip.");
        }

        var now = DateTimeOffset.UtcNow;

        if (assignment.Status == AssignmentStatus.Proposed)
        {
            assignment.Status = AssignmentStatus.Accepted;
            assignment.UpdatedAt = now;

            var existingResponse = await _dbContext.AssignmentResponses
                .FirstOrDefaultAsync(r => r.AssignmentId == assignment.AssignmentId, cancellationToken);
            if (existingResponse == null)
            {
                _dbContext.AssignmentResponses.Add(new AssignmentResponse
                {
                    AssignmentId = assignment.AssignmentId,
                    RespondedByUserId = currentUserId,
                    Response = AssignmentResponseType.Accepted,
                    DeclineReason = null,
                    RespondedAt = now
                });
            }
        }

        if (assignment.Load != null)
        {
            assignment.Load.Status = LoadStatus.Matched;
            assignment.Load.UpdatedAt = now;
        }

        var tripId = Guid.NewGuid();
        var trip = new Trip
        {
            TripId = tripId,
            AssignmentId = assignment.AssignmentId,
            VehicleId = request.VehicleId,
            DriverId = request.DriverId,
            Status = TripStatus.Assigned,
            CreatedAt = now,
            UpdatedAt = now
        };

        var tripEvent = new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = tripId,
            RecordedByUserId = currentUserId,
            FromStatus = null,
            ToStatus = TripStatus.Assigned,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? "Trip created and vehicle/driver assigned." : request.Notes.Trim(),
            OccurredAt = now
        };

        _dbContext.Trips.Add(trip);
        _dbContext.TripEvents.Add(tripEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(tripId, currentUserId, currentUserRole, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TripResponseDto> UpdateAsync(
        Guid tripId,
        UpdateTripDto request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin && currentUserRole != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "Only Agency Staff or Admin may update trip assignments.");
        }

        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null || trip.Assignment.AgencyId != agencyStaff.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to modify this trip.");
            }

            AgencyStatusGuard.EnsureActive(trip.Assignment.Agency.Status);
        }

        if (trip.Status != TripStatus.Assigned)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.TRIP_CANNOT_BE_MODIFIED, "Trips can only be updated/reassigned while in 'Assigned' status before departure.");
        }

        var now = DateTimeOffset.UtcNow;
        var modified = false;
        var changeNotes = new List<string>();

        if (request.VehicleId.HasValue && request.VehicleId.Value != trip.VehicleId)
        {
            var newVehicle = await _dbContext.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId.Value, cancellationToken);

            if (newVehicle == null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.VEHICLE_NOT_FOUND, "The replacement vehicle could not be found.");
            }

            if (newVehicle.AgencyId != trip.Assignment.AgencyId)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_NOT_OWNED, "The replacement vehicle must belong to the executing agency.");
            }

            if (newVehicle.Status == VehicleStatus.Maintenance || newVehicle.Status == VehicleStatus.Retired)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.VEHICLE_UNAVAILABLE, "The replacement vehicle is not available (in maintenance or retired).");
            }

            var isBusy = await _dbContext.Trips
                .AnyAsync(t => t.TripId != tripId &&
                               t.VehicleId == request.VehicleId.Value &&
                               (t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit),
                          cancellationToken);

            if (isBusy)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.VEHICLE_UNAVAILABLE, "The replacement vehicle is currently engaged in another active trip.");
            }

            trip.VehicleId = request.VehicleId.Value;
            modified = true;
            changeNotes.Add($"Vehicle reassigned to {newVehicle.RegistrationNo}");
        }

        if (request.DriverId.HasValue && request.DriverId.Value != trip.DriverId)
        {
            var newDriver = await _dbContext.Drivers
                .Include(d => d.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DriverId == request.DriverId.Value, cancellationToken);

            if (newDriver == null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DRIVER_NOT_FOUND, "The replacement driver could not be found.");
            }

            if (newDriver.AgencyId != trip.Assignment.AgencyId)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.DRIVER_NOT_OWNED, "The replacement driver must belong to the executing agency.");
            }

            if (newDriver.Status != DriverStatus.Active)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.DRIVER_UNAVAILABLE, "The replacement driver is not active.");
            }

            var isBusy = await _dbContext.Trips
                .AnyAsync(t => t.TripId != tripId &&
                               t.DriverId == request.DriverId.Value &&
                               (t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit),
                          cancellationToken);

            if (isBusy)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_UNAVAILABLE, "The replacement driver is currently engaged in another active trip.");
            }

            trip.DriverId = request.DriverId.Value;
            modified = true;
            changeNotes.Add($"Driver reassigned to {newDriver.User?.FullName ?? newDriver.DriverId.ToString()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            changeNotes.Add(request.Notes.Trim());
        }

        if (modified)
        {
            trip.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(tripId, currentUserId, currentUserRole, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TripResponseDto> CancelAsync(
        Guid tripId,
        CancelTripDto? request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null || trip.Assignment.AgencyId != agencyStaff.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to cancel this trip.");
            }

            AgencyStatusGuard.EnsureActive(trip.Assignment.Agency.Status);
        }
        else if (currentUserRole == UserRole.Driver)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == currentUserId, cancellationToken);

            if (driver == null || trip.DriverId != driver.DriverId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to cancel this trip.");
            }
        }
        else if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to cancel this trip.");
        }

        if (trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Cancelled)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, $"Cannot cancel a trip in '{trip.Status}' status.");
        }

        var now = DateTimeOffset.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Trip cancelled." : request.Reason.Trim();

        var tripEvent = new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = trip.TripId,
            RecordedByUserId = currentUserId,
            FromStatus = trip.Status,
            ToStatus = TripStatus.Cancelled,
            Notes = reason,
            OccurredAt = now
        };

        trip.Status = TripStatus.Cancelled;
        trip.UpdatedAt = now;

        _dbContext.TripEvents.Add(tripEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(tripId, currentUserId, currentUserRole, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid tripId,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.Trips
            .Include(t => t.Assignment)
            .Include(t => t.Events)
            .Include(t => t.Evidence)
            .Include(t => t.Invoice)
            .Include(t => t.Disputes)
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

        if (trip == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.TRIP_NOT_FOUND, "The requested trip could not be found.");
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var agencyStaff = await _dbContext.AgencyStaff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

            if (agencyStaff == null || trip.Assignment?.AgencyId != agencyStaff.AgencyId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to delete this trip.");
            }
        }
        else if (currentUserRole == UserRole.Driver)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == currentUserId, cancellationToken);

            if (driver == null || trip.DriverId != driver.DriverId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to delete this trip.");
            }
        }
        else if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.TRIP_ACCESS_DENIED, "You do not have permission to delete this trip.");
        }

        if (trip.Status == TripStatus.Delivered)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Cannot delete a trip that has already been delivered.");
        }

        if (trip.Status == TripStatus.PickedUp || trip.Status == TripStatus.InTransit)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Active trips in pickup or transit must be cancelled before they can be deleted.");
        }

        if (trip.Events.Count > 0)
        {
            _dbContext.TripEvents.RemoveRange(trip.Events);
        }

        if (trip.Evidence.Count > 0)
        {
            _dbContext.TripEvidences.RemoveRange(trip.Evidence);
        }

        if (trip.Invoice != null)
        {
            _dbContext.Invoices.Remove(trip.Invoice);
        }

        if (trip.Disputes.Count > 0)
        {
            _dbContext.Disputes.RemoveRange(trip.Disputes);
        }

        _dbContext.Trips.Remove(trip);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TripResponseDto>> SeedExampleTripsAsync(CancellationToken cancellationToken = default)
    {
        var trip1Id = Guid.Parse("c1000000-0000-0000-0000-000000000001");
        var trip2Id = Guid.Parse("c2000000-0000-0000-0000-000000000002");
        var trip3Id = Guid.Parse("c3000000-0000-0000-0000-000000000003");

        var existingTrips = await _dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Driver)
                .ThenInclude(d => d.User)
            .Include(t => t.Vehicle)
            .Include(t => t.Events)
            .Include(t => t.Evidence)
            .Where(t => t.TripId == trip1Id || t.TripId == trip2Id || t.TripId == trip3Id)
            .ToListAsync(cancellationToken);

        var assignment4Id = Guid.Parse("b4000000-0000-0000-0000-000000000004");
        var hasAssignment4 = await _dbContext.Assignments.AnyAsync(a => a.AssignmentId == assignment4Id, cancellationToken);

        if (existingTrips.Count == 3 && hasAssignment4)
        {
            return existingTrips.Select(MapToDetailResponse).ToList();
        }

        var now = DateTimeOffset.UtcNow;

        // 1. Agency
        var agencyId = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224ba");
        var agency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);
        if (agency == null)
        {
            agency = new Agency
            {
                AgencyId = agencyId,
                Name = "Samagi Express Logistics",
                BusinessRegNo = "PV-88991",
                YardAddress = "45 Harbor Access Road, Peliyagoda",
                Status = AgencyStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Agencies.Add(agency);
        }
        else
        {
            agency.Status = AgencyStatus.Active;
        }

        // 2. Users (Drivers)
        var driverUser1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var driverUser2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var driverUser3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var staffUserId = Guid.Parse("2d276e2c-8ada-400d-b9f8-f5b49c1507af");
        var shipperUserId = Guid.Parse("c67e538b-3247-44a0-9362-09667ba6d97f");

        var existingDriverUser1 = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == driverUser1Id || u.Email == "driver1@freightlink.lk", cancellationToken);
        if (existingDriverUser1 == null)
        {
            _dbContext.Users.Add(new User
            {
                UserId = driverUser1Id,
                Role = UserRole.Driver,
                Email = "driver1@freightlink.lk",
                FullName = "Sunil Perera",
                PasswordHash = "$2a$11$A8Hb51phJ3SgRiT1.ec4OOwF0EhvmuY6g5tLu1aeideTGaAY2z10C",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            driverUser1Id = existingDriverUser1.UserId;
        }

        var existingDriverUser2 = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == driverUser2Id || u.Email == "driver2@freightlink.lk", cancellationToken);
        if (existingDriverUser2 == null)
        {
            _dbContext.Users.Add(new User
            {
                UserId = driverUser2Id,
                Role = UserRole.Driver,
                Email = "driver2@freightlink.lk",
                FullName = "Kamal Fernando",
                PasswordHash = "$2a$11$A8Hb51phJ3SgRiT1.ec4OOwF0EhvmuY6g5tLu1aeideTGaAY2z10C",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            driverUser2Id = existingDriverUser2.UserId;
        }

        var existingDriverUser3 = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == driverUser3Id || u.Email == "driver3@freightlink.lk", cancellationToken);
        if (existingDriverUser3 == null)
        {
            _dbContext.Users.Add(new User
            {
                UserId = driverUser3Id,
                Role = UserRole.Driver,
                Email = "driver3@freightlink.lk",
                FullName = "Nimal Jayawardena",
                PasswordHash = "$2a$11$A8Hb51phJ3SgRiT1.ec4OOwF0EhvmuY6g5tLu1aeideTGaAY2z10C",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            driverUser3Id = existingDriverUser3.UserId;
        }

        var existingShipper = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == shipperUserId || u.Email == "user@example.com", cancellationToken);
        if (existingShipper == null)
        {
            _dbContext.Users.Add(new User
            {
                UserId = shipperUserId,
                Role = UserRole.Shipper,
                Email = "user@example.com",
                FullName = "Amara Silva",
                PasswordHash = "$2a$11$A8Hb51phJ3SgRiT1.ec4OOwF0EhvmuY6g5tLu1aeideTGaAY2z10C",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            shipperUserId = existingShipper.UserId;
        }

        var existingStaff = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == staffUserId || u.Email == "agency@freightlink.lk", cancellationToken);
        if (existingStaff == null)
        {
            _dbContext.Users.Add(new User
            {
                UserId = staffUserId,
                Role = UserRole.AgencyStaff,
                Email = "agency@freightlink.lk",
                FullName = "Kasun Jayasuriya",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            staffUserId = existingStaff.UserId;
        }

        if (!await _dbContext.AgencyStaff.AnyAsync(s => s.UserId == staffUserId && s.AgencyId == agencyId, cancellationToken))
        {
            _dbContext.AgencyStaff.Add(new AgencyStaff
            {
                UserId = staffUserId,
                AgencyId = agencyId,
                JobTitle = "Operations Dispatcher",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // 3. Drivers
        var driver1Id = Guid.Parse("d1000000-0000-0000-0000-000000000001");
        var driver2Id = Guid.Parse("d2000000-0000-0000-0000-000000000002");
        var driver3Id = Guid.Parse("d3000000-0000-0000-0000-000000000003");

        if (!await _dbContext.Drivers.AnyAsync(d => d.DriverId == driver1Id, cancellationToken))
        {
            _dbContext.Drivers.Add(new Driver
            {
                DriverId = driver1Id,
                UserId = driverUser1Id,
                AgencyId = agencyId,
                LicenceNo = "B-3849102",
                LicenceExpiry = new DateOnly(2028, 12, 31),
                Status = DriverStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Drivers.AnyAsync(d => d.DriverId == driver2Id, cancellationToken))
        {
            _dbContext.Drivers.Add(new Driver
            {
                DriverId = driver2Id,
                UserId = driverUser2Id,
                AgencyId = agencyId,
                LicenceNo = "B-9023415",
                LicenceExpiry = new DateOnly(2029, 6, 30),
                Status = DriverStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Drivers.AnyAsync(d => d.DriverId == driver3Id, cancellationToken))
        {
            _dbContext.Drivers.Add(new Driver
            {
                DriverId = driver3Id,
                UserId = driverUser3Id,
                AgencyId = agencyId,
                LicenceNo = "B-5512940",
                LicenceExpiry = new DateOnly(2027, 10, 15),
                Status = DriverStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // 4. Vehicles
        var vehicle1Id = Guid.Parse("ba100000-0000-0000-0000-000000000001");
        var vehicle2Id = Guid.Parse("ba200000-0000-0000-0000-000000000002");
        var vehicle3Id = Guid.Parse("ba300000-0000-0000-0000-000000000003");

        if (!await _dbContext.Vehicles.AnyAsync(v => v.VehicleId == vehicle1Id, cancellationToken))
        {
            _dbContext.Vehicles.Add(new Vehicle
            {
                VehicleId = vehicle1Id,
                AgencyId = agencyId,
                RegistrationNo = "WP-CAB-4521",
                VehicleType = VehicleType.Lorry,
                CapacityKg = 6500,
                VolumeM3 = 30,
                Status = VehicleStatus.Available,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Vehicles.AnyAsync(v => v.VehicleId == vehicle2Id, cancellationToken))
        {
            _dbContext.Vehicles.Add(new Vehicle
            {
                VehicleId = vehicle2Id,
                AgencyId = agencyId,
                RegistrationNo = "WP-DA-8920",
                VehicleType = VehicleType.Container,
                CapacityKg = 18000,
                VolumeM3 = 65,
                Status = VehicleStatus.Available,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Vehicles.AnyAsync(v => v.VehicleId == vehicle3Id, cancellationToken))
        {
            _dbContext.Vehicles.Add(new Vehicle
            {
                VehicleId = vehicle3Id,
                AgencyId = agencyId,
                RegistrationNo = "WP-LB-6712",
                VehicleType = VehicleType.FlatBed,
                CapacityKg = 12000,
                VolumeM3 = 45,
                Status = VehicleStatus.Available,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // 5. Loads
        var load1Id = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var load2Id = Guid.Parse("a2000000-0000-0000-0000-000000000002");
        var load3Id = Guid.Parse("a3000000-0000-0000-0000-000000000003");

        if (!await _dbContext.Loads.AnyAsync(l => l.LoadId == load1Id, cancellationToken))
        {
            _dbContext.Loads.Add(new Load
            {
                LoadId = load1Id,
                ShipperUserId = shipperUserId,
                ReferenceCode = "LD-CMB-KDY-01",
                CargoDescription = "Industrial Machinery Components & Hydraulics",
                WeightKg = 3200,
                VolumeM3 = 14.5m,
                PickupAddress = "Colombo Port Terminal 3, Colombo 01",
                PickupLat = 6.940000m,
                PickupLng = 79.850000m,
                DropoffAddress = "Pallekele BOI Industrial Zone, Kandy",
                DropoffLat = 7.280000m,
                DropoffLng = 80.700000m,
                PickupWindowStart = now.AddDays(-2),
                PickupWindowEnd = now.AddDays(1),
                EstimatedPrice = 58500.00m,
                Status = LoadStatus.InTransit,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Loads.AnyAsync(l => l.LoadId == load2Id, cancellationToken))
        {
            _dbContext.Loads.Add(new Load
            {
                LoadId = load2Id,
                ShipperUserId = shipperUserId,
                ReferenceCode = "LD-KLY-GAL-02",
                CargoDescription = "High-Capacity Solar Inverters & Batteries",
                WeightKg = 2400,
                VolumeM3 = 10.0m,
                PickupAddress = "Kelaniya Distribution Hub, Peliyagoda",
                PickupLat = 6.960000m,
                PickupLng = 79.920000m,
                DropoffAddress = "Galle Port Warehouse Complex, Galle",
                DropoffLat = 6.035000m,
                DropoffLng = 80.215000m,
                PickupWindowStart = now.AddHours(-1),
                PickupWindowEnd = now.AddDays(2),
                EstimatedPrice = 64200.00m,
                Status = LoadStatus.Matched,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Loads.AnyAsync(l => l.LoadId == load3Id, cancellationToken))
        {
            _dbContext.Loads.Add(new Load
            {
                LoadId = load3Id,
                ShipperUserId = shipperUserId,
                ReferenceCode = "LD-NWE-CMB-03",
                CargoDescription = "Ceylon Pure Black Tea (Grade BOPF, 400 Cartons)",
                WeightKg = 4500,
                VolumeM3 = 20.0m,
                PickupAddress = "Nuwara Eliya Tea Packaging Facility, Nuwara Eliya",
                PickupLat = 6.970000m,
                PickupLng = 80.780000m,
                DropoffAddress = "Colombo South Harbor Logistics Gate, Colombo",
                DropoffLat = 6.935000m,
                DropoffLng = 79.845000m,
                PickupWindowStart = now.AddDays(-4),
                PickupWindowEnd = now.AddDays(-2),
                EstimatedPrice = 72000.00m,
                Status = LoadStatus.Delivered,
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now
            });
        }

        var load4Id = Guid.Parse("a4000000-0000-0000-0000-000000000004");
        if (!await _dbContext.Loads.AnyAsync(l => l.LoadId == load4Id, cancellationToken))
        {
            _dbContext.Loads.Add(new Load
            {
                LoadId = load4Id,
                ShipperUserId = shipperUserId,
                ReferenceCode = "LD-JAF-CMB-04",
                CargoDescription = "Fresh Northern Agricultural Produce & Dry Goods (500 Crates)",
                WeightKg = 4200,
                VolumeM3 = 18.0m,
                PickupAddress = "Jaffna Central Wholesale Market, Hospital Road, Jaffna",
                PickupLat = 9.661500m,
                PickupLng = 80.025500m,
                DropoffAddress = "Manning Market Wholesale Complex, Peliyagoda",
                DropoffLat = 6.965000m,
                DropoffLng = 79.885000m,
                PickupWindowStart = now.AddHours(2),
                PickupWindowEnd = now.AddDays(2),
                EstimatedPrice = 78500.00m,
                Status = LoadStatus.Posted,
                CreatedAt = now.AddHours(-3),
                UpdatedAt = now
            });
        }

        // 6. WorkflowRuns
        var workflow1Id = Guid.Parse("f1000000-0000-0000-0000-000000000001");
        var workflow2Id = Guid.Parse("f2000000-0000-0000-0000-000000000002");
        var workflow3Id = Guid.Parse("f3000000-0000-0000-0000-000000000003");
        var workflow4Id = Guid.Parse("f4000000-0000-0000-0000-000000000004");

        if (!await _dbContext.AgentWorkflowRuns.AnyAsync(w => w.WorkflowRunId == workflow1Id, cancellationToken))
        {
            _dbContext.AgentWorkflowRuns.Add(new AgentWorkflowRun
            {
                WorkflowRunId = workflow1Id,
                LoadId = load1Id,
                TriggeredByUserId = shipperUserId,
                AttemptNo = 1,
                Objective = "Match Colombo-Kandy Machinery Load",
                Status = WorkflowRunStatus.Completed,
                StartedAt = now.AddDays(-2),
                CompletedAt = now.AddDays(-2),
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.AgentWorkflowRuns.AnyAsync(w => w.WorkflowRunId == workflow2Id, cancellationToken))
        {
            _dbContext.AgentWorkflowRuns.Add(new AgentWorkflowRun
            {
                WorkflowRunId = workflow2Id,
                LoadId = load2Id,
                TriggeredByUserId = shipperUserId,
                AttemptNo = 1,
                Objective = "Match Kelaniya-Galle Solar Inverters",
                Status = WorkflowRunStatus.Completed,
                StartedAt = now.AddDays(-1),
                CompletedAt = now.AddDays(-1),
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.AgentWorkflowRuns.AnyAsync(w => w.WorkflowRunId == workflow3Id, cancellationToken))
        {
            _dbContext.AgentWorkflowRuns.Add(new AgentWorkflowRun
            {
                WorkflowRunId = workflow3Id,
                LoadId = load3Id,
                TriggeredByUserId = shipperUserId,
                AttemptNo = 1,
                Objective = "Match Nuwara Eliya-Colombo Tea Load",
                Status = WorkflowRunStatus.Completed,
                StartedAt = now.AddDays(-4),
                CompletedAt = now.AddDays(-4),
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.AgentWorkflowRuns.AnyAsync(w => w.WorkflowRunId == workflow4Id, cancellationToken))
        {
            _dbContext.AgentWorkflowRuns.Add(new AgentWorkflowRun
            {
                WorkflowRunId = workflow4Id,
                LoadId = load4Id,
                TriggeredByUserId = shipperUserId,
                AttemptNo = 1,
                Objective = "Match Jaffna-Colombo Agricultural Load",
                Status = WorkflowRunStatus.Completed,
                StartedAt = now.AddHours(-2),
                CompletedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now
            });
        }

        // 7. Assignments
        var assignment1Id = Guid.Parse("b1000000-0000-0000-0000-000000000001");
        var assignment2Id = Guid.Parse("b2000000-0000-0000-0000-000000000002");
        var assignment3Id = Guid.Parse("b3000000-0000-0000-0000-000000000003");
        assignment4Id = Guid.Parse("b4000000-0000-0000-0000-000000000004");

        if (!await _dbContext.Assignments.AnyAsync(a => a.AssignmentId == assignment1Id, cancellationToken))
        {
            _dbContext.Assignments.Add(new Assignment
            {
                AssignmentId = assignment1Id,
                LoadId = load1Id,
                AgencyId = agencyId,
                WorkflowRunId = workflow1Id,
                ProposedPrice = 58500.00m,
                RoutedDistanceKm = 118.5m,
                ProposedEtaMinutes = 190,
                Status = AssignmentStatus.Accepted,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Assignments.AnyAsync(a => a.AssignmentId == assignment2Id, cancellationToken))
        {
            _dbContext.Assignments.Add(new Assignment
            {
                AssignmentId = assignment2Id,
                LoadId = load2Id,
                AgencyId = agencyId,
                WorkflowRunId = workflow2Id,
                ProposedPrice = 64200.00m,
                RoutedDistanceKm = 125.0m,
                ProposedEtaMinutes = 150,
                Status = AssignmentStatus.Accepted,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Assignments.AnyAsync(a => a.AssignmentId == assignment3Id, cancellationToken))
        {
            _dbContext.Assignments.Add(new Assignment
            {
                AssignmentId = assignment3Id,
                LoadId = load3Id,
                AgencyId = agencyId,
                WorkflowRunId = workflow3Id,
                ProposedPrice = 72000.00m,
                RoutedDistanceKm = 162.0m,
                ProposedEtaMinutes = 280,
                Status = AssignmentStatus.Accepted,
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now
            });
        }

        if (!await _dbContext.Assignments.AnyAsync(a => a.AssignmentId == assignment4Id, cancellationToken))
        {
            _dbContext.Assignments.Add(new Assignment
            {
                AssignmentId = assignment4Id,
                LoadId = load4Id,
                AgencyId = agencyId,
                WorkflowRunId = workflow4Id,
                ProposedPrice = 78500.00m,
                RoutedDistanceKm = 395.5m,
                ProposedEtaMinutes = 420,
                Status = AssignmentStatus.Proposed,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now
            });
        }

        // 8. Trips (insert as Assigned first)
        if (!await _dbContext.Trips.AnyAsync(t => t.TripId == trip1Id, cancellationToken))
        {
            _dbContext.Trips.Add(new Trip
            {
                TripId = trip1Id,
                AssignmentId = assignment1Id,
                VehicleId = vehicle1Id,
                DriverId = driver1Id,
                Status = TripStatus.Assigned,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            });
        }

        if (!await _dbContext.Trips.AnyAsync(t => t.TripId == trip2Id, cancellationToken))
        {
            _dbContext.Trips.Add(new Trip
            {
                TripId = trip2Id,
                AssignmentId = assignment2Id,
                VehicleId = vehicle2Id,
                DriverId = driver2Id,
                Status = TripStatus.Assigned,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-2)
            });
        }

        if (!await _dbContext.Trips.AnyAsync(t => t.TripId == trip3Id, cancellationToken))
        {
            _dbContext.Trips.Add(new Trip
            {
                TripId = trip3Id,
                AssignmentId = assignment3Id,
                VehicleId = vehicle3Id,
                DriverId = driver3Id,
                Status = TripStatus.Assigned,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-3)
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 9. Evidences
        var evidence1Id = Guid.Parse("e1000000-0000-0000-0000-000000000001");
        var evidence3aId = Guid.Parse("e3000000-0000-0000-0000-000000000001");
        var evidence3bId = Guid.Parse("e3000000-0000-0000-0000-000000000002");

        if (!await _dbContext.TripEvidences.AnyAsync(e => e.TripEvidenceId == evidence1Id, cancellationToken))
        {
            _dbContext.TripEvidences.Add(new TripEvidence
            {
                TripEvidenceId = evidence1Id,
                TripId = trip1Id,
                CapturedByUserId = staffUserId,
                EvidenceType = EvidenceType.PickupProof,
                StorageKey = "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?w=800&trip=1",
                CapturedLat = 6.940000m,
                CapturedLng = 79.850000m,
                CapturedAt = now.AddHours(-5)
            });
        }

        if (!await _dbContext.TripEvidences.AnyAsync(e => e.TripEvidenceId == evidence3aId, cancellationToken))
        {
            _dbContext.TripEvidences.Add(new TripEvidence
            {
                TripEvidenceId = evidence3aId,
                TripId = trip3Id,
                CapturedByUserId = staffUserId,
                EvidenceType = EvidenceType.PickupProof,
                StorageKey = "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?w=800&trip=3",
                CapturedLat = 6.970000m,
                CapturedLng = 80.780000m,
                CapturedAt = now.AddDays(-2)
            });
        }

        if (!await _dbContext.TripEvidences.AnyAsync(e => e.TripEvidenceId == evidence3bId, cancellationToken))
        {
            _dbContext.TripEvidences.Add(new TripEvidence
            {
                TripEvidenceId = evidence3bId,
                TripId = trip3Id,
                CapturedByUserId = driverUser3Id,
                EvidenceType = EvidenceType.DeliveryProof,
                StorageKey = "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?w=800&trip=3",
                CapturedLat = 6.935000m,
                CapturedLng = 79.845000m,
                CapturedAt = now.AddHours(-6)
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 10. Update Trip Statuses
        if (!existingTrips.Any(t => t.TripId == trip1Id))
        {
            var trip1 = await _dbContext.Trips.FirstOrDefaultAsync(t => t.TripId == trip1Id, cancellationToken);
            if (trip1 != null)
            {
                trip1.Status = TripStatus.InTransit;
                trip1.UpdatedAt = now;
            }
        }

        if (!existingTrips.Any(t => t.TripId == trip3Id))
        {
            var trip3 = await _dbContext.Trips.FirstOrDefaultAsync(t => t.TripId == trip3Id, cancellationToken);
            if (trip3 != null)
            {
                trip3.Status = TripStatus.Delivered;
                trip3.UpdatedAt = now;
            }
        }

        // 11. Timeline Events
        var event1Id = Guid.Parse("ee100000-0000-0000-0000-000000000001");
        if (!await _dbContext.TripEvents.AnyAsync(e => e.TripEventId == event1Id, cancellationToken))
        {
            _dbContext.TripEvents.AddRange(
                new TripEvent
                {
                    TripEventId = event1Id,
                    TripId = trip1Id,
                    RecordedByUserId = staffUserId,
                    FromStatus = null,
                    ToStatus = TripStatus.Assigned,
                    Notes = "Trip assigned to driver Sunil Perera with lorry WP-CAB-4521.",
                    SnapshotLat = 6.940000m,
                    SnapshotLng = 79.850000m,
                    OccurredAt = now.AddHours(-8)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee100000-0000-0000-0000-000000000002"),
                    TripId = trip1Id,
                    RecordedByUserId = staffUserId,
                    FromStatus = TripStatus.Assigned,
                    ToStatus = TripStatus.PickedUp,
                    Notes = "Cargo loaded and verified against manifest. Waybill #FL-8821 signed.",
                    SnapshotLat = 6.940000m,
                    SnapshotLng = 79.850000m,
                    OccurredAt = now.AddHours(-5)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee100000-0000-0000-0000-000000000003"),
                    TripId = trip1Id,
                    RecordedByUserId = driverUser1Id,
                    FromStatus = TripStatus.PickedUp,
                    ToStatus = TripStatus.InTransit,
                    Notes = "Departed Colombo Port via Colombo-Kandy Road (A1). Running smoothly.",
                    SnapshotLat = 6.960000m,
                    SnapshotLng = 79.880000m,
                    OccurredAt = now.AddHours(-3)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee200000-0000-0000-0000-000000000001"),
                    TripId = trip2Id,
                    RecordedByUserId = staffUserId,
                    FromStatus = null,
                    ToStatus = TripStatus.Assigned,
                    Notes = "Dispatched to Kamal Fernando. Awaiting pickup at Kelaniya Hub.",
                    SnapshotLat = 6.960000m,
                    SnapshotLng = 79.920000m,
                    OccurredAt = now.AddHours(-2)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee300000-0000-0000-0000-000000000001"),
                    TripId = trip3Id,
                    RecordedByUserId = staffUserId,
                    FromStatus = null,
                    ToStatus = TripStatus.Assigned,
                    Notes = "Trip initialized for Nuwara Eliya export tea delivery.",
                    SnapshotLat = 6.970000m,
                    SnapshotLng = 80.780000m,
                    OccurredAt = now.AddDays(-3)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee300000-0000-0000-0000-000000000002"),
                    TripId = trip3Id,
                    RecordedByUserId = staffUserId,
                    FromStatus = TripStatus.Assigned,
                    ToStatus = TripStatus.PickedUp,
                    Notes = "Loaded at factory bay with export seal intact.",
                    SnapshotLat = 6.970000m,
                    SnapshotLng = 80.780000m,
                    OccurredAt = now.AddDays(-2)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee300000-0000-0000-0000-000000000003"),
                    TripId = trip3Id,
                    RecordedByUserId = driverUser3Id,
                    FromStatus = TripStatus.PickedUp,
                    ToStatus = TripStatus.InTransit,
                    Notes = "Departed Nuwara Eliya via Gampola route towards Colombo.",
                    SnapshotLat = 7.150000m,
                    SnapshotLng = 80.570000m,
                    OccurredAt = now.AddDays(-1)
                },
                new TripEvent
                {
                    TripEventId = Guid.Parse("ee300000-0000-0000-0000-000000000004"),
                    TripId = trip3Id,
                    RecordedByUserId = driverUser3Id,
                    FromStatus = TripStatus.InTransit,
                    ToStatus = TripStatus.Delivered,
                    Notes = "Delivered safely at Colombo Port Export Bay. Consignee signature obtained.",
                    SnapshotLat = 6.935000m,
                    SnapshotLng = 79.845000m,
                    OccurredAt = now.AddHours(-6)
                }
            );
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Agency)
            .Include(t => t.Assignment)
                .ThenInclude(a => a.Load)
            .Include(t => t.Driver)
                .ThenInclude(d => d.User)
            .Include(t => t.Vehicle)
            .Include(t => t.Events)
            .Include(t => t.Evidence)
            .Where(t => t.TripId == trip1Id || t.TripId == trip2Id || t.TripId == trip3Id)
            .Select(t => MapToDetailResponse(t))
            .ToListAsync(cancellationToken);
    }
}