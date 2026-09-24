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
    private readonly IEmailService _emailService;

    public AssignmentService(AppDbContext dbContext, IEmailService emailService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
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

        if (query.HasTrip.HasValue)
        {
            dbQuery = query.HasTrip.Value
                ? dbQuery.Where(a => a.Trip != null)
                : dbQuery.Where(a => a.Trip == null);
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
            .Include(a => a.Response)
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
            .Include(a => a.WorkflowRun)
            .Include(a => a.Response)
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

        var now = DateTimeOffset.UtcNow;
        assignment.Status = AssignmentStatus.Declined;
        assignment.UpdatedAt = now;

        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Declined by agency staff" : request.Reason.Trim();
        var assignmentResponse = new AssignmentResponse
        {
            AssignmentId = assignment.AssignmentId,
            RespondedByUserId = currentUserId,
            Response = AssignmentResponseType.Declined,
            DeclineReason = reason,
            RespondedAt = now
        };
        _dbContext.AssignmentResponses.Add(assignmentResponse);
        assignment.Response = assignmentResponse;

        // ADR-018 Retry Cascade and Email Notification
        var shipperUser = assignment.Load?.ShipperUser;
        var shipperEmail = shipperUser?.Email;
        var shipperName = shipperUser?.FullName ?? "Shipper";
        var loadRef = assignment.Load?.ReferenceCode ?? assignment.LoadId.ToString();
        var agencyName = assignment.Agency?.Name ?? "Assigned Agency";

        var workflowRun = assignment.WorkflowRun ?? await _dbContext.AgentWorkflowRuns
            .FirstOrDefaultAsync(r => r.WorkflowRunId == assignment.WorkflowRunId || r.LoadId == assignment.LoadId, cancellationToken);

        if (workflowRun != null)
        {
            if (workflowRun.AttemptNo < 3)
            {
                // Send immediate notification email to shipper (attempt 1 or 2)
                if (!string.IsNullOrWhiteSpace(shipperEmail))
                {
                    await _emailService.SendAgencyDeclinedAsync(shipperEmail, shipperName, loadRef, agencyName, workflowRun.AttemptNo, cancellationToken);
                }
                // Load remains in Posted status for subsequent matching attempt
            }
            else
            {
                // Attempt cap reached (>= 3 attempts): record safe failure per ADR-018 / Section 9.1
                workflowRun.Status = WorkflowRunStatus.Failed;
                workflowRun.CompletedAt = now;
                workflowRun.UpdatedAt = now;

                if (!string.IsNullOrWhiteSpace(shipperEmail))
                {
                    await _emailService.SendNoAutomaticMatchFoundAsync(shipperEmail, shipperName, loadRef, cancellationToken);
                }
            }
        }
        else
        {
            // If no workflow run record exists (e.g. standalone proposal), still notify the shipper
            if (!string.IsNullOrWhiteSpace(shipperEmail))
            {
                await _emailService.SendAgencyDeclinedAsync(shipperEmail, shipperName, loadRef, agencyName, 1, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetail(assignment);
    }

    /// <inheritdoc />
    public Task<AssignmentResponseDto> AcceptAsync(
        Guid assignmentOrLoadId,
        ApproveAssignmentDto? request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        return ApproveAsync(assignmentOrLoadId, request, currentUserId, currentUserRole, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AssignmentResponseDto> ApproveAsync(
        Guid assignmentOrLoadId,
        ApproveAssignmentDto? request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.Assignments
            .Include(a => a.Load)
                .ThenInclude(l => l.ShipperUser)
            .Include(a => a.Agency)
            .Include(a => a.WorkflowRun)
            .Include(a => a.Trip)
            .Include(a => a.Response)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentOrLoadId || a.LoadId == assignmentOrLoadId, cancellationToken);

        if (assignment == null)
        {
            var isRun = await _dbContext.AgentWorkflowRuns.AnyAsync(r => r.WorkflowRunId == assignmentOrLoadId, cancellationToken);
            if (isRun)
            {
                return await ApproveWorkflowRunAsync(assignmentOrLoadId, new ApproveWorkflowRunDto
                {
                    VehicleId = request?.VehicleId,
                    DriverId = request?.DriverId,
                    Notes = request?.Notes
                }, currentUserId, currentUserRole, cancellationToken);
            }

            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.ASSIGNMENT_NOT_FOUND, "The requested assignment could not be found.");
        }

        await EnforceOwnershipAsync(assignment, currentUserId, currentUserRole, cancellationToken);

        if (assignment.Status == AssignmentStatus.Declined)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Declined assignments cannot be approved.");
        }

        if (assignment.Status == AssignmentStatus.Cancelled)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.INVALID_TRIP_STATUS_TRANSITION, "Cancelled assignments cannot be approved.");
        }

        if (assignment.Status == AssignmentStatus.Accepted && assignment.Trip != null && assignment.Trip.Status == TripStatus.Assigned)
        {
            return MapToDetail(assignment);
        }

        var (vehicleId, driverId) = await ResolveVehicleAndDriverAsync(assignment.AgencyId, request?.VehicleId, request?.DriverId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        assignment.Status = AssignmentStatus.Accepted;
        assignment.UpdatedAt = now;

        if (assignment.Response == null)
        {
            var assignmentResponse = new AssignmentResponse
            {
                AssignmentId = assignment.AssignmentId,
                RespondedByUserId = currentUserId,
                Response = AssignmentResponseType.Accepted,
                DeclineReason = null,
                RespondedAt = now
            };
            _dbContext.AssignmentResponses.Add(assignmentResponse);
            assignment.Response = assignmentResponse;
        }

        if (assignment.Trip == null)
        {
            var tripId = Guid.NewGuid();
            var trip = new Trip
            {
                TripId = tripId,
                AssignmentId = assignment.AssignmentId,
                VehicleId = vehicleId,
                DriverId = driverId,
                Status = TripStatus.Assigned,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Trips.Add(trip);
            assignment.Trip = trip;

            var tripEvent = new TripEvent
            {
                TripEventId = Guid.NewGuid(),
                TripId = tripId,
                RecordedByUserId = currentUserId,
                FromStatus = null,
                ToStatus = TripStatus.Assigned,
                Notes = string.IsNullOrWhiteSpace(request?.Notes) ? "Trip created and vehicle/driver assigned upon approval." : request.Notes.Trim(),
                OccurredAt = now
            };
            _dbContext.TripEvents.Add(tripEvent);
        }
        else
        {
            if (assignment.Trip.Status != TripStatus.Assigned)
            {
                var prevStatus = assignment.Trip.Status;
                assignment.Trip.Status = TripStatus.Assigned;
                assignment.Trip.VehicleId = vehicleId;
                assignment.Trip.DriverId = driverId;
                assignment.Trip.UpdatedAt = now;

                var tripEvent = new TripEvent
                {
                    TripEventId = Guid.NewGuid(),
                    TripId = assignment.Trip.TripId,
                    RecordedByUserId = currentUserId,
                    FromStatus = prevStatus,
                    ToStatus = TripStatus.Assigned,
                    Notes = string.IsNullOrWhiteSpace(request?.Notes) ? "Trip updated to Assigned status upon approval." : request.Notes.Trim(),
                    OccurredAt = now
                };
                _dbContext.TripEvents.Add(tripEvent);
            }
        }

        if (assignment.Load != null)
        {
            var prevLoadStatus = assignment.Load.Status;
            assignment.Load.Status = LoadStatus.Matched;
            assignment.Load.UpdatedAt = now;

            _dbContext.LoadStatusHistories.Add(new LoadStatusHistory
            {
                LoadStatusHistoryId = Guid.NewGuid(),
                LoadId = assignment.LoadId,
                FromStatus = prevLoadStatus,
                ToStatus = LoadStatus.Matched,
                ChangedByUserId = currentUserId,
                Reason = "Load matched and assignment approved.",
                ChangedAt = now
            });
        }

        var workflowRun = assignment.WorkflowRun ?? await _dbContext.AgentWorkflowRuns.FirstOrDefaultAsync(r => r.WorkflowRunId == assignment.WorkflowRunId, cancellationToken);
        if (workflowRun != null)
        {
            workflowRun.Status = WorkflowRunStatus.Completed;
            workflowRun.CompletedAt = now;
            workflowRun.UpdatedAt = now;

            var hasApproveDecision = await _dbContext.ApprovalDecisions
                .AnyAsync(d => d.WorkflowRunId == workflowRun.WorkflowRunId && d.Decision == ApprovalDecisionType.Approve, cancellationToken);

            if (!hasApproveDecision)
            {
                var seqNo = await _dbContext.ApprovalDecisions
                    .CountAsync(d => d.WorkflowRunId == workflowRun.WorkflowRunId, cancellationToken) + 1;

                _dbContext.ApprovalDecisions.Add(new ApprovalDecision
                {
                    ApprovalDecisionId = Guid.NewGuid(),
                    WorkflowRunId = workflowRun.WorkflowRunId,
                    DecidedByUserId = currentUserId,
                    SequenceNo = Math.Max(1, seqNo),
                    Decision = ApprovalDecisionType.Approve,
                    Reason = request?.Notes,
                    DecidedAt = now
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetail(assignment);
    }

    /// <inheritdoc />
    public async Task<AssignmentResponseDto> ApproveWorkflowRunAsync(
        Guid workflowRunId,
        ApproveWorkflowRunDto? request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only Admin may approve agent workflow runs.");
        }

        var run = await _dbContext.AgentWorkflowRuns
            .Include(r => r.Load)
                .ThenInclude(l => l.ShipperUser)
            .Include(r => r.MatchCandidates)
            .Include(r => r.Assignments)
                .ThenInclude(a => a.Trip)
            .FirstOrDefaultAsync(r => r.WorkflowRunId == workflowRunId, cancellationToken);

        if (run == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.WORKFLOW_RUN_NOT_FOUND, "The requested workflow run could not be found.");
        }

        var existingAssignment = run.Assignments.FirstOrDefault(a => a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.Accepted);
        if (existingAssignment != null)
        {
            return await ApproveAsync(existingAssignment.AssignmentId, new ApproveAssignmentDto
            {
                VehicleId = request?.VehicleId,
                DriverId = request?.DriverId,
                Notes = request?.Notes
            }, currentUserId, currentUserRole, cancellationToken);
        }

        Guid agencyId;
        if (request?.AgencyId.HasValue == true)
        {
            agencyId = request.AgencyId.Value;
        }
        else
        {
            var topCandidate = run.MatchCandidates
                .Where(c => c.Eligible)
                .OrderBy(c => c.Rank)
                .FirstOrDefault();

            if (topCandidate == null)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.NO_ELIGIBLE_MATCH_CANDIDATE, "No eligible agency match candidate found for this workflow run.");
            }
            agencyId = topCandidate.AgencyId;
        }

        var (vehicleId, driverId) = await ResolveVehicleAndDriverAsync(agencyId, request?.VehicleId, request?.DriverId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var proposedPrice = run.Load?.EstimatedPrice ?? 50000m;
        decimal? routedDistanceKm = null;
        int? proposedEtaMinutes = null;

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = run.LoadId,
            AgencyId = agencyId,
            WorkflowRunId = run.WorkflowRunId,
            ProposedPrice = proposedPrice,
            RoutedDistanceKm = routedDistanceKm,
            ProposedEtaMinutes = proposedEtaMinutes,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Assignments.Add(assignment);

        var assignmentResponse = new AssignmentResponse
        {
            AssignmentId = assignment.AssignmentId,
            RespondedByUserId = currentUserId,
            Response = AssignmentResponseType.Accepted,
            DeclineReason = null,
            RespondedAt = now
        };
        _dbContext.AssignmentResponses.Add(assignmentResponse);
        assignment.Response = assignmentResponse;

        var tripId = Guid.NewGuid();
        var trip = new Trip
        {
            TripId = tripId,
            AssignmentId = assignment.AssignmentId,
            VehicleId = vehicleId,
            DriverId = driverId,
            Status = TripStatus.Assigned,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Trips.Add(trip);
        assignment.Trip = trip;

        var tripEvent = new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = tripId,
            RecordedByUserId = currentUserId,
            FromStatus = null,
            ToStatus = TripStatus.Assigned,
            Notes = string.IsNullOrWhiteSpace(request?.Notes) ? "Trip created upon admin match approval." : request.Notes.Trim(),
            OccurredAt = now
        };
        _dbContext.TripEvents.Add(tripEvent);

        if (run.Load != null)
        {
            var prevLoadStatus = run.Load.Status;
            run.Load.Status = LoadStatus.Matched;
            run.Load.UpdatedAt = now;

            _dbContext.LoadStatusHistories.Add(new LoadStatusHistory
            {
                LoadStatusHistoryId = Guid.NewGuid(),
                LoadId = run.LoadId,
                FromStatus = prevLoadStatus,
                ToStatus = LoadStatus.Matched,
                ChangedByUserId = currentUserId,
                Reason = "Load matched and assignment approved by admin.",
                ChangedAt = now
            });
        }

        run.Status = WorkflowRunStatus.Completed;
        run.CompletedAt = now;
        run.UpdatedAt = now;

        var seqNo = await _dbContext.ApprovalDecisions
            .CountAsync(d => d.WorkflowRunId == run.WorkflowRunId, cancellationToken) + 1;

        _dbContext.ApprovalDecisions.Add(new ApprovalDecision
        {
            ApprovalDecisionId = Guid.NewGuid(),
            WorkflowRunId = run.WorkflowRunId,
            DecidedByUserId = currentUserId,
            SequenceNo = Math.Max(1, seqNo),
            Decision = ApprovalDecisionType.Approve,
            Reason = request?.Notes,
            DecidedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(assignment.AssignmentId, currentUserId, currentUserRole, cancellationToken);
    }

    private async Task<(Guid VehicleId, Guid DriverId)> ResolveVehicleAndDriverAsync(
        Guid agencyId,
        Guid? requestedVehicleId,
        Guid? requestedDriverId,
        CancellationToken cancellationToken)
    {
        Guid vehicleId;
        if (requestedVehicleId.HasValue)
        {
            var vehicle = await _dbContext.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VehicleId == requestedVehicleId.Value, cancellationToken);

            if (vehicle == null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.VEHICLE_NOT_FOUND, "The requested vehicle could not be found.");
            }

            if (vehicle.AgencyId != agencyId)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_NOT_OWNED, "The assigned vehicle must belong to the executing agency.");
            }

            if (vehicle.Status == VehicleStatus.Maintenance || vehicle.Status == VehicleStatus.Retired)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.VEHICLE_UNAVAILABLE, "The selected vehicle is not available (in maintenance or retired).");
            }

            var isVehicleBusy = await _dbContext.Trips
                .AnyAsync(t => t.VehicleId == requestedVehicleId.Value &&
                               (t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit),
                          cancellationToken);

            if (isVehicleBusy)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.VEHICLE_UNAVAILABLE, "The selected vehicle is currently engaged in another active trip.");
            }

            vehicleId = vehicle.VehicleId;
        }
        else
        {
            var busyVehicleIds = await _dbContext.Trips
                .Where(t => t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit)
                .Select(t => t.VehicleId)
                .ToListAsync(cancellationToken);

            var availableVehicle = await _dbContext.Vehicles
                .Where(v => v.AgencyId == agencyId &&
                            v.Status != VehicleStatus.Maintenance &&
                            v.Status != VehicleStatus.Retired &&
                            !busyVehicleIds.Contains(v.VehicleId))
                .OrderBy(v => v.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (availableVehicle == null)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.VEHICLE_UNAVAILABLE, "No available active vehicle found for the agency. Please specify a vehicle.");
            }

            vehicleId = availableVehicle.VehicleId;
        }

        Guid driverId;
        if (requestedDriverId.HasValue)
        {
            var driver = await _dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DriverId == requestedDriverId.Value, cancellationToken);

            if (driver == null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.DRIVER_NOT_FOUND, "The requested driver could not be found.");
            }

            if (driver.AgencyId != agencyId)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.DRIVER_NOT_OWNED, "The assigned driver must belong to the executing agency.");
            }

            if (driver.Status != DriverStatus.Active)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.DRIVER_UNAVAILABLE, "The selected driver is not active.");
            }

            var isDriverBusy = await _dbContext.Trips
                .AnyAsync(t => t.DriverId == requestedDriverId.Value &&
                               (t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit),
                          cancellationToken);

            if (isDriverBusy)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_UNAVAILABLE, "The selected driver is currently engaged in another active trip.");
            }

            driverId = driver.DriverId;
        }
        else
        {
            var busyDriverIds = await _dbContext.Trips
                .Where(t => t.Status == TripStatus.Assigned || t.Status == TripStatus.PickedUp || t.Status == TripStatus.InTransit)
                .Select(t => t.DriverId)
                .ToListAsync(cancellationToken);

            var availableDriver = await _dbContext.Drivers
                .Where(d => d.AgencyId == agencyId && d.Status == DriverStatus.Active && !busyDriverIds.Contains(d.DriverId))
                .OrderBy(d => d.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (availableDriver == null)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.DRIVER_UNAVAILABLE, "No available active driver found for the agency. Please specify a driver.");
            }

            driverId = availableDriver.DriverId;
        }

        return (vehicleId, driverId);
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
            DeclineReason = a.Response?.DeclineReason,
            RespondedAt = a.Response?.RespondedAt,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }
}
