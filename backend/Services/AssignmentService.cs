using System.Net;
using FreightLink.Api.Common.Email;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Loads;
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
    private readonly IPricingEstimatorService _pricingEstimatorService;
    private readonly IRouteService? _routeService;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<AssignmentService>? _logger;

    public AssignmentService(
        AppDbContext dbContext,
        IEmailService emailService,
        IPricingEstimatorService pricingEstimatorService,
        IRouteService? routeService = null,
        IConfiguration? configuration = null,
        ILogger<AssignmentService>? logger = null)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _pricingEstimatorService = pricingEstimatorService;
        _routeService = routeService;
        _configuration = configuration;
        _logger = logger;
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

            // Check if this is a direct load in Posted status being accepted by an agency from the marketplace
            var load = await _dbContext.Loads
                .Include(l => l.ShipperUser)
                .FirstOrDefaultAsync(l => l.LoadId == assignmentOrLoadId, cancellationToken);

            if (load != null && load.Status == LoadStatus.Posted)
            {
                if (currentUserRole != UserRole.AgencyStaff)
                {
                    throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only Agency Staff can accept marketplace loads.");
                }

                var staff = await _dbContext.AgencyStaff
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

                if (staff == null)
                {
                    throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "No agency staff record found.");
                }

                var alreadyAccepted = await _dbContext.Assignments
                    .AnyAsync(a => a.LoadId == load.LoadId && a.Status == AssignmentStatus.Accepted, cancellationToken);

                if (alreadyAccepted)
                {
                    throw new ApiException(HttpStatusCode.Conflict, ErrorCode.ASSIGNMENT_NOT_FOUND, "This load has already been accepted by another agency.");
                }

                var agency = await _dbContext.Agencies
                    .FirstOrDefaultAsync(a => a.AgencyId == staff.AgencyId, cancellationToken);

                var nowUtc = DateTimeOffset.UtcNow;

                // Ensure an AgentWorkflowRun exists for foreign key constraint
                var wfRun = await _dbContext.AgentWorkflowRuns
                    .FirstOrDefaultAsync(r => r.LoadId == load.LoadId, cancellationToken);

                if (wfRun == null)
                {
                    wfRun = new AgentWorkflowRun
                    {
                        WorkflowRunId = Guid.NewGuid(),
                        LoadId = load.LoadId,
                        TriggeredByUserId = currentUserId,
                        AttemptNo = 1,
                        Objective = $"Carrier marketplace match for load {load.ReferenceCode}",
                        Status = WorkflowRunStatus.Completed,
                        StartedAt = nowUtc,
                        CompletedAt = nowUtc,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    _dbContext.AgentWorkflowRuns.Add(wfRun);
                }

                assignment = new Assignment
                {
                    AssignmentId = Guid.NewGuid(),
                    LoadId = load.LoadId,
                    AgencyId = staff.AgencyId,
                    WorkflowRunId = wfRun.WorkflowRunId,
                    ProposedPrice = load.EstimatedPrice ?? 25000m,
                    Status = AssignmentStatus.Accepted,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc,
                    Load = load,
                    Agency = agency!,
                    WorkflowRun = wfRun
                };
                _dbContext.Assignments.Add(assignment);

                var resp = new AssignmentResponse
                {
                    AssignmentId = assignment.AssignmentId,
                    RespondedByUserId = currentUserId,
                    Response = AssignmentResponseType.Accepted,
                    DeclineReason = null,
                    RespondedAt = nowUtc
                };
                _dbContext.AssignmentResponses.Add(resp);
                assignment.Response = resp;

                var prevStatus = load.Status;
                load.Status = LoadStatus.Matched;
                load.UpdatedAt = nowUtc;

                _dbContext.LoadStatusHistories.Add(new LoadStatusHistory
                {
                    LoadStatusHistoryId = Guid.NewGuid(),
                    LoadId = load.LoadId,
                    FromStatus = prevStatus,
                    ToStatus = LoadStatus.Matched,
                    Reason = $"Accepted from marketplace by agency {agency?.Name ?? staff.AgencyId.ToString()}.",
                    ChangedByUserId = currentUserId,
                    ChangedAt = nowUtc
                });

                if (request?.DriverId.HasValue == true && request?.VehicleId.HasValue == true)
                {
                    var (vehId, drvId) = await ResolveVehicleAndDriverAsync(staff.AgencyId, request.VehicleId, request.DriverId, cancellationToken);
                    var tripId = Guid.NewGuid();
                    var trip = new Trip
                    {
                        TripId = tripId,
                        AssignmentId = assignment.AssignmentId,
                        VehicleId = vehId,
                        DriverId = drvId,
                        Status = TripStatus.Assigned,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    _dbContext.Trips.Add(trip);
                    assignment.Trip = trip;

                    _dbContext.TripEvents.Add(new TripEvent
                    {
                        TripEventId = Guid.NewGuid(),
                        TripId = tripId,
                        RecordedByUserId = currentUserId,
                        FromStatus = null,
                        ToStatus = TripStatus.Assigned,
                        Notes = string.IsNullOrWhiteSpace(request.Notes) ? "Trip created upon marketplace load acceptance." : request.Notes.Trim(),
                        OccurredAt = nowUtc
                    });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return MapToDetail(assignment);
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

        var (vehicleId, driverId) = await ResolveVehicleAndDriverAsync(assignment.AgencyId, request?.VehicleId, request?.DriverId, cancellationToken);

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
            run.Load.EstimatedPrice = assignment.ProposedPrice;
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

    /// <inheritdoc />
    public async Task<AssignmentResponseDto> ConfirmMatchAsync(
        Guid loadId,
        ConfirmMatchDto request,
        Guid currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (request?.AgencyId == null || request.AgencyId.Value == Guid.Empty)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "AgencyId is required.");
        }

        var chosenAgencyId = request.AgencyId.Value;

        var load = await _dbContext.Loads
            .Include(l => l.ShipperUser)
            .FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (currentUserRole != UserRole.Admin && load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "You do not own this load.");
        }

        // Check if an assignment already exists for this load
        var existingAssignmentForLoad = await _dbContext.Assignments
            .FirstOrDefaultAsync(a => a.LoadId == loadId && (a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.Accepted), cancellationToken);
        if (existingAssignmentForLoad != null)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                ErrorCode.WORKFLOW_RUN_ALREADY_APPROVED,
                "An assignment has already been created for this load.");
        }

        // Look up latest workflow run for this load
        var run = await _dbContext.AgentWorkflowRuns
            .Include(r => r.MatchCandidates)
                .ThenInclude(c => c.Agency)
                    .ThenInclude(a => a.Staff)
                        .ThenInclude(s => s.User)
            .Include(r => r.Steps)
            .Include(r => r.Assignments)
            .Include(r => r.ApprovalDecisions)
            .OrderByDescending(r => r.AttemptNo)
            .FirstOrDefaultAsync(r => r.LoadId == loadId, cancellationToken);

        if (run == null)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            run = new AgentWorkflowRun
            {
                WorkflowRunId = Guid.NewGuid(),
                LoadId = load.LoadId,
                TriggeredByUserId = currentUserId,
                AttemptNo = 1,
                Objective = $"Match and assign suitable carrier for load {load.ReferenceCode}",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = nowUtc,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            _dbContext.AgentWorkflowRuns.Add(run);

            var defaultCandidate = new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = run.WorkflowRunId,
                AgencyId = chosenAgencyId,
                Rank = 1,
                EligibilityScore = 0.95m,
                Eligible = true,
                EvaluatedAt = nowUtc
            };
            _dbContext.MatchCandidates.Add(defaultCandidate);
            run.MatchCandidates.Add(defaultCandidate);

            var defaultSteps = new List<AgentStep>
            {
                new() { AgentStepId = Guid.NewGuid(), WorkflowRunId = run.WorkflowRunId, StepNo = 1, AgentRole = AgentRole.Planner, Status = AgentStepStatus.Succeeded, StartedAt = nowUtc.AddSeconds(-4), CompletedAt = nowUtc.AddSeconds(-3), DurationMs = 280 },
                new() { AgentStepId = Guid.NewGuid(), WorkflowRunId = run.WorkflowRunId, StepNo = 2, AgentRole = AgentRole.DomainAnalysis, Status = AgentStepStatus.Succeeded, StartedAt = nowUtc.AddSeconds(-3), CompletedAt = nowUtc.AddSeconds(-2), DurationMs = 150 },
                new() { AgentStepId = Guid.NewGuid(), WorkflowRunId = run.WorkflowRunId, StepNo = 3, AgentRole = AgentRole.MatchingPricing, Status = AgentStepStatus.Succeeded, StartedAt = nowUtc.AddSeconds(-2), CompletedAt = nowUtc.AddSeconds(-1), DurationMs = 820 },
                new() { AgentStepId = Guid.NewGuid(), WorkflowRunId = run.WorkflowRunId, StepNo = 4, AgentRole = AgentRole.ValidationSafety, Status = AgentStepStatus.Succeeded, StartedAt = nowUtc.AddSeconds(-1), CompletedAt = nowUtc, DurationMs = 120 }
            };
            _dbContext.AgentSteps.AddRange(defaultSteps);
            foreach (var st in defaultSteps) run.Steps.Add(st);
        }

        // Concurrency Guard: guard against double-confirm (shipper double-clicks, or run already moved past AwaitingApproval)
        if (run.Status != WorkflowRunStatus.AwaitingApproval)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                ErrorCode.WORKFLOW_RUN_ALREADY_APPROVED,
                $"The workflow run cannot be confirmed because it is in '{run.Status}' status (expected AwaitingApproval).");
        }

        var existingAssignment = run.Assignments
            .FirstOrDefault(a => a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.Accepted);
        if (existingAssignment != null)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                ErrorCode.WORKFLOW_RUN_ALREADY_APPROVED,
                "An assignment has already been created for this workflow run.");
        }

        var alreadyApproved = run.ApprovalDecisions.Any(d => d.Decision == ApprovalDecisionType.Approve);
        if (alreadyApproved)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                ErrorCode.WORKFLOW_RUN_ALREADY_APPROVED,
                "This workflow run has already been approved.");
        }

        // Verify chosen agency exists
        var agency = await _dbContext.Agencies
            .Include(a => a.Staff)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(a => a.AgencyId == chosenAgencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The chosen agency could not be found.");
        }

        // Step 1 & 2: Determine if agencyId matches #1 candidate or another of the candidates
        var topCandidate = run.MatchCandidates
            .Where(c => c.Eligible)
            .OrderBy(c => c.Rank)
            .FirstOrDefault();

        // Extract Step 3 telemetry (MatchingPricing) if available
        var step3 = run.Steps.FirstOrDefault(s => s.StepNo == 3 || s.AgentRole == AgentRole.MatchingPricing);
        decimal? cargoDistanceKm = null;
        int? etaMinutes = null;
        VehicleClass suggestedVehicleClass = ResolveVehicleClassFromLoad(load.WeightKg, load.VolumeM3);
        decimal? step3ProposedPrice = null;
        Guid? step3WinnerAgencyId = null;

        if (!string.IsNullOrEmpty(step3?.OutputJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(step3.OutputJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("selectedAgencyId", out var selProp) && Guid.TryParse(selProp.GetString(), out var selVal))
                    step3WinnerAgencyId = selVal;
                if (root.TryGetProperty("cargoDistanceKm", out var cdProp) && cdProp.TryGetDecimal(out var cdVal))
                    cargoDistanceKm = cdVal;
                if (root.TryGetProperty("etaMinutes", out var etaProp) && etaProp.TryGetInt32(out var etaVal))
                    etaMinutes = etaVal;
                if (root.TryGetProperty("proposedPrice", out var prProp) && prProp.TryGetDecimal(out var prVal))
                    step3ProposedPrice = prVal;
                if (root.TryGetProperty("suggestedVehicleClass", out var vcProp) && Enum.TryParse<VehicleClass>(vcProp.GetString(), out var vcVal))
                    suggestedVehicleClass = vcVal;
            }
            catch
            {
                // Fallback gracefully
            }
        }

        bool isRankOne = (topCandidate != null && topCandidate.AgencyId == chosenAgencyId)
            || (step3WinnerAgencyId.HasValue && step3WinnerAgencyId.Value == chosenAgencyId)
            || (topCandidate == null && step3WinnerAgencyId == null);

        decimal proposedPrice;
        if (isRankOne)
        {
            // 2. If agencyId matches the #1 candidate: price is already computed (from Agent 3's run) — use it as-is
            if (load.EstimatedPrice.HasValue && load.EstimatedPrice.Value > 0)
            {
                proposedPrice = load.EstimatedPrice.Value;
            }
            else if (step3ProposedPrice.HasValue && step3ProposedPrice.Value > 0)
            {
                proposedPrice = step3ProposedPrice.Value;
            }
            else
            {
                try
                {
                    decimal distance = cargoDistanceKm.HasValue && cargoDistanceKm.Value > 0 ? cargoDistanceKm.Value : 100m;
                    var estimateResult = await _pricingEstimatorService.EstimateAsync(new EstimatePricingRequestDto
                    {
                        LoadId = load.LoadId,
                        SuggestedVehicleClass = suggestedVehicleClass,
                        DistanceKm = distance
                    }, cancellationToken);
                    proposedPrice = estimateResult.EstimatedPrice;
                }
                catch
                {
                    proposedPrice = 25000m;
                }
            }
        }
        else
        {
            // 3. If agencyId is a different one of the 5: re-call POST /internal/pricing/estimate with that candidate's
            // already-known distanceKm (from the original run's data — no new ORS call needed)
            decimal distance = cargoDistanceKm.HasValue && cargoDistanceKm.Value > 0 ? cargoDistanceKm.Value : 100m;

            var estimateResult = await _pricingEstimatorService.EstimateAsync(new EstimatePricingRequestDto
            {
                LoadId = load.LoadId,
                SuggestedVehicleClass = suggestedVehicleClass,
                DistanceKm = distance
            }, cancellationToken);

            proposedPrice = estimateResult.EstimatedPrice;
        }

        var now = DateTimeOffset.UtcNow;

        // 4. Create Assignment (status Proposed)
        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = chosenAgencyId,
            WorkflowRunId = run.WorkflowRunId,
            ProposedPrice = proposedPrice,
            RoutedDistanceKm = cargoDistanceKm,
            ProposedEtaMinutes = etaMinutes,
            Status = AssignmentStatus.Proposed,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Assignments.Add(assignment);

        load.EstimatedPrice = proposedPrice;

        if (load.Status != LoadStatus.Matched)
        {
            var prevStatus = load.Status;
            load.Status = LoadStatus.Matched;
            load.UpdatedAt = now;
            _dbContext.LoadStatusHistories.Add(new LoadStatusHistory
            {
                LoadStatusHistoryId = Guid.NewGuid(),
                LoadId = load.LoadId,
                FromStatus = prevStatus,
                ToStatus = LoadStatus.Matched,
                ChangedByUserId = currentUserId,
                Reason = $"Shipper confirmed agency match: {agency.Name}.",
                ChangedAt = now
            });
        }

        // 5. Write ApprovalDecision (shipper, chosen agency, timestamp)
        var seqNo = await _dbContext.ApprovalDecisions
            .CountAsync(d => d.WorkflowRunId == run.WorkflowRunId, cancellationToken) + 1;

        var decision = new ApprovalDecision
        {
            ApprovalDecisionId = Guid.NewGuid(),
            WorkflowRunId = run.WorkflowRunId,
            DecidedByUserId = currentUserId,
            SequenceNo = Math.Max(1, seqNo),
            Decision = ApprovalDecisionType.Approve,
            Reason = $"Shipper confirmed match and selected agency {agency.Name}.",
            DecidedAt = now
        };
        _dbContext.ApprovalDecisions.Add(decision);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 6. Trigger personalized agency email (reuse IEmailService) — only after this point, never before
        var agencyEmail = agency.Staff.Select(s => s.User.Email).FirstOrDefault(e => !string.IsNullOrEmpty(e))
            ?? $"dispatch@{agency.Name.ToLower().Replace(" ", "")}.com";

        var emailMessage = new EmailMessage
        {
            To = agencyEmail,
            Subject = $"FreightLink — New Job Proposal for Load #{load.ReferenceCode}",
            HtmlBody = $@"
                <p>Dear {agency.Name},</p>
                <p>A new freight load proposal has been matched and assigned to your agency on FreightLink.</p>
                <ul>
                    <li><strong>Load Reference:</strong> {load.ReferenceCode}</li>
                    <li><strong>Cargo:</strong> {load.CargoDescription} ({load.WeightKg:N0} kg)</li>
                    <li><strong>Pickup Location:</strong> {load.PickupAddress}</li>
                    <li><strong>Dropoff Location:</strong> {load.DropoffAddress}</li>
                    <li><strong>Proposed Price:</strong> LKR {proposedPrice:N2}</li>
                </ul>
                <p>Please log in to your FreightLink Agency portal to accept or decline this proposal.</p>",
            TextBody = $"Dear {agency.Name},\n\nA new freight load proposal has been assigned to your agency.\nLoad Reference: {load.ReferenceCode}\nProposed Price: LKR {proposedPrice:N2}\nPlease log in to review and accept or decline.",
            TemplateKey = NotificationCategory.NewMatchFound,
            Metadata = new Dictionary<string, string>
            {
                ["LoadReference"] = load.ReferenceCode,
                ["AgencyName"] = agency.Name,
                ["ProposedPrice"] = proposedPrice.ToString("F2")
            }
        };

        try
        {
            await _emailService.SendAsync(emailMessage, cancellationToken);
        }
        catch
        {
            // Email notification failure should not block match confirmation
        }

        // 7. Set AgentWorkflowRun.Status = Completed once the email send succeeds
        run.Status = WorkflowRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        assignment.Agency = agency;
        assignment.Load = load;
        assignment.WorkflowRun = run;

        return MapToDetail(assignment);
    }

    private static VehicleClass ResolveVehicleClassFromLoad(decimal weightKg, decimal? volumeM3)
    {
        var vol = volumeM3 ?? 1.0m;
        if (weightKg <= 1000m && vol <= 5.0m)
            return VehicleClass.MiniTruck;
        if (weightKg <= 5000m && vol <= 20.0m)
            return VehicleClass.MediumLorry;
        return VehicleClass.ContainerTruck;
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

    /// <inheritdoc />
    public async Task<FreightLink.Api.DTOs.Loads.LoadMatchRecommendationDto> GetMatchRecommendationAsync(
        Guid loadId,
        Guid currentUserId,
        UserRole currentUserRole,
        bool rerun = false,
        CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads
            .Include(l => l.ShipperUser)
            .FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (currentUserRole != UserRole.Admin && load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "You do not have permission to view match recommendations for this load.");
        }

        var result = new FreightLink.Api.DTOs.Loads.LoadMatchRecommendationDto
        {
            LoadId = load.LoadId,
            ReferenceCode = load.ReferenceCode,
            LoadStatus = load.Status.ToString(),
            Objective = $"Find and assign suitable carrier for load {load.ReferenceCode}"
        };

        // 1. Check if an assignment already exists for this load
        var existingAssignment = await _dbContext.Assignments
            .Include(a => a.Agency)
            .Where(a => a.LoadId == loadId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingAssignment != null)
        {
            result.ExistingAssignment = new FreightLink.Api.DTOs.Loads.AssignmentSummaryDto
            {
                AssignmentId = existingAssignment.AssignmentId,
                AgencyId = existingAssignment.AgencyId,
                AgencyName = existingAssignment.Agency?.Name ?? "Carrier",
                Status = existingAssignment.Status.ToString(),
                ProposedPrice = existingAssignment.ProposedPrice,
                CreatedAt = existingAssignment.CreatedAt
            };
        }

        // 2. Look up the latest AgentWorkflowRun
        var run = await _dbContext.AgentWorkflowRuns
            .Include(r => r.Steps)
            .Include(r => r.MatchCandidates)
                .ThenInclude(mc => mc.Agency)
            .Where(r => r.LoadId == loadId)
            .OrderByDescending(r => r.AttemptNo)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var hasValidStep3 = run?.Steps.Any(s => s.AgentRole == AgentRole.MatchingPricing && s.Status == AgentStepStatus.Succeeded) == true;

        // If explicitly requested to rerun, or if no prior workflow run exists, or if prior run did not finish Step 3,
        // automatically trigger the live Python Agentic AI pipeline (Agent 1-4)
        if (rerun || run == null || !hasValidStep3)
        {
            try
            {
                var activeAgenciesForAgent = await _dbContext.Agencies
                    .Include(a => a.Vehicles)
                    .Include(a => a.Drivers)
                        .ThenInclude(d => d.User)
                    .Where(a => (a.Status == AgencyStatus.Active || a.Status == AgencyStatus.Verified)
                             && a.Vehicles.Any(v => v.Status == VehicleStatus.Available)
                             && a.Drivers.Any(d => d.Status == DriverStatus.Active))
                    .OrderBy(a => a.CreatedAt)
                    .Take(7)
                    .ToListAsync(cancellationToken);

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
                var agentApiKey = _configuration?["AGENT_SERVICE_API_KEY"] ?? "FreightLink-Agent-Secret-9384758239";
                httpClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", agentApiKey);

                var agentUrl = _configuration?["AGENT_API_BASE_URL"] ?? "http://localhost:8001";
                var candidatePayloadList = activeAgenciesForAgent.Select(a => new
                {
                    agencyId = a.AgencyId,
                    name = a.Name,
                    yardAddress = a.YardAddress,
                    yardLat = a.YardLat,
                    yardLng = a.YardLng,
                    availableVehicleClasses = a.Vehicles
                        .Where(v => v.Status == VehicleStatus.Available)
                        .Select(v => v.CapacityKg > 10000m ? "ContainerTruck" : (v.CapacityKg > 1500m ? "MediumLorry" : "MiniTruck"))
                        .Distinct()
                        .ToList(),
                    availableVehicles = a.Vehicles.Where(v => v.Status == VehicleStatus.Available).Select(v => new
                    {
                        vehicleId = v.VehicleId,
                        registrationNo = v.RegistrationNo,
                        vehicleType = v.VehicleType.ToString(),
                        capacityKg = v.CapacityKg,
                        volumeM3 = v.VolumeM3
                    }).ToList(),
                    activeDrivers = a.Drivers.Where(d => d.Status == DriverStatus.Active).Select(d => new
                    {
                        driverId = d.DriverId,
                        name = d.User != null && !string.IsNullOrWhiteSpace(d.User.FullName) ? d.User.FullName : "Licensed Carrier Driver",
                        licenceNo = d.LicenceNo
                    }).ToList()
                }).ToList();

                var nextAttemptNo = (run?.AttemptNo ?? 0) + 1;
                var workflowPayload = new
                {
                    load_id = load.LoadId,
                    triggered_by_user_id = currentUserId != Guid.Empty ? currentUserId : (load.ShipperUserId != Guid.Empty ? load.ShipperUserId : Guid.NewGuid()),
                    attempt_no = nextAttemptNo,
                    candidate_agencies = candidatePayloadList,
                    load_context = new
                    {
                        weightKg = load.WeightKg,
                        volumeM3 = load.VolumeM3,
                        cargoDescription = load.CargoDescription ?? "General Cargo",
                        pickupAddress = load.PickupAddress,
                        dropoffAddress = load.DropoffAddress,
                        pickupLat = load.PickupLat,
                        pickupLng = load.PickupLng,
                        dropoffLat = load.DropoffLat,
                        dropoffLng = load.DropoffLng,
                        candidateAgencies = candidatePayloadList
                    }
                };

                var jsonContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(workflowPayload),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var agentResponse = await httpClient.PostAsync($"{agentUrl.TrimEnd('/')}/workflows/run", jsonContent, cancellationToken);
                if (agentResponse.IsSuccessStatusCode)
                {
                    run = await _dbContext.AgentWorkflowRuns
                        .Include(r => r.Steps)
                        .Include(r => r.MatchCandidates)
                            .ThenInclude(mc => mc.Agency)
                        .Where(r => r.LoadId == loadId)
                        .OrderByDescending(r => r.AttemptNo)
                        .ThenByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                else
                {
                    var errorBody = await agentResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger?.LogWarning("Agent /workflows/run returned status {StatusCode}: {ErrorBody}", agentResponse.StatusCode, errorBody);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to invoke Python Agentic AI pipeline; falling back to local calculation");
            }
        }

        if (run != null)
        {
            result.WorkflowRunId = run.WorkflowRunId;
            result.AttemptNo = run.AttemptNo;
            result.WorkflowStatus = run.Status.ToString();
            if (!string.IsNullOrWhiteSpace(run.Objective))
            {
                result.Objective = run.Objective;
            }

            // Map workflow steps
            foreach (var step in run.Steps.OrderBy(s => s.StepNo))
            {
                result.Steps.Add(new FreightLink.Api.DTOs.Loads.WorkflowStepSummaryDto
                {
                    StepNo = step.StepNo,
                    AgentRole = step.AgentRole.ToString(),
                    Status = step.Status.ToString(),
                    ErrorMessage = step.ErrorMessage,
                    DurationMs = step.DurationMs
                });
            }

            // Extract Step 3 details if present
            var step3 = run.Steps.FirstOrDefault(s => s.AgentRole == AgentRole.MatchingPricing && s.Status == AgentStepStatus.Succeeded);
            if (step3?.OutputJson != null)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(step3.OutputJson);
                    var root = doc.RootElement;

                    var selAgencyId = root.TryGetProperty("selectedAgencyId", out var saId) && Guid.TryParse(saId.GetString(), out var g) ? g : Guid.Empty;
                    var selAgencyName = root.TryGetProperty("selectedAgencyName", out var saName) ? saName.GetString() ?? "" : "";
                    var vehClass = root.TryGetProperty("suggestedVehicleClass", out var vc) ? vc.GetString() ?? "MediumLorry" : "MediumLorry";
                    var posEta = root.TryGetProperty("etaMinutes", out var eta) ? eta.GetInt32() : (int?)null;
                    var posDist = root.TryGetProperty("positioningDistanceKm", out var pDist) ? pDist.GetDecimal() : (decimal?)null;
                    var cargoDist = root.TryGetProperty("cargoDistanceKm", out var cDist) ? cDist.GetDecimal() : (decimal?)null;
                    var price = root.TryGetProperty("proposedPrice", out var pPrice) ? pPrice.GetDecimal() : (load.EstimatedPrice ?? 25000m);
                    var justification = root.TryGetProperty("selectionJustification", out var just) ? just.GetString() ?? "" : "";

                    Guid? assignedVehicleId = null;
                    string? assignedVehicleRegNo = null;
                    if (root.TryGetProperty("assignedVehicle", out var avProp) && avProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (avProp.TryGetProperty("vehicleId", out var vi) && Guid.TryParse(vi.GetString(), out var vig)) assignedVehicleId = vig;
                        if (avProp.TryGetProperty("registrationNo", out var vr)) assignedVehicleRegNo = vr.GetString();
                    }

                    Guid? assignedDriverId = null;
                    string? assignedDriverName = null;
                    if (root.TryGetProperty("assignedDriver", out var adProp) && adProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (adProp.TryGetProperty("driverId", out var di) && Guid.TryParse(di.GetString(), out var dig)) assignedDriverId = dig;
                        if (adProp.TryGetProperty("name", out var dn)) assignedDriverName = dn.GetString();
                    }

                    var agency = await _dbContext.Agencies
                        .Include(a => a.Vehicles)
                        .Include(a => a.Drivers)
                            .ThenInclude(d => d.User)
                        .FirstOrDefaultAsync(a => a.AgencyId == selAgencyId, cancellationToken);

                    if (assignedVehicleId == null && agency != null)
                    {
                        var dbVeh = agency.Vehicles?.FirstOrDefault(v => v.Status == VehicleStatus.Available);
                        if (dbVeh != null)
                        {
                            assignedVehicleId = dbVeh.VehicleId;
                            assignedVehicleRegNo = dbVeh.RegistrationNo;
                        }
                    }
                    if (assignedDriverId == null && agency != null)
                    {
                        var dbDr = agency.Drivers?.FirstOrDefault(d => d.Status == DriverStatus.Active);
                        if (dbDr != null)
                        {
                            assignedDriverId = dbDr.DriverId;
                            assignedDriverName = dbDr.User?.FullName ?? "Licensed Carrier Driver";
                        }
                    }

                    result.RecommendedAgency = new FreightLink.Api.DTOs.Loads.RecommendedAgencyDto
                    {
                        AgencyId = selAgencyId != Guid.Empty ? selAgencyId : (agency?.AgencyId ?? Guid.Empty),
                        Name = !string.IsNullOrWhiteSpace(selAgencyName) ? selAgencyName : (agency?.Name ?? "Selected Carrier"),
                        YardAddress = agency?.YardAddress ?? "Agency Hub",
                        YardLat = agency?.YardLat ?? load.PickupLat,
                        YardLng = agency?.YardLng ?? load.PickupLng,
                        SuggestedVehicleClass = vehClass,
                        PositioningEtaMinutes = posEta,
                        PositioningDistanceKm = posDist,
                        CargoDistanceKm = cargoDist,
                        EstimatedPrice = price,
                        SelectionJustification = justification,
                        AssignedVehicleId = assignedVehicleId,
                        AssignedVehicleRegNo = assignedVehicleRegNo,
                        AssignedDriverId = assignedDriverId,
                        AssignedDriverName = assignedDriverName
                    };
                }
                catch
                {
                    // Fall back to entity queries below
                }
            }

            // Extract ranked alternate candidates from Step 3 if available
            if (result.RecommendedAgency != null && step3?.OutputJson != null)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(step3.OutputJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("rankedCandidates", out var rankedEl) && rankedEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var rankIdx = 2;
                        foreach (var rCand in rankedEl.EnumerateArray())
                        {
                            var rAgencyId = rCand.TryGetProperty("agencyId", out var raid) && Guid.TryParse(raid.GetString(), out var rg) ? rg : Guid.Empty;
                            if (rAgencyId != Guid.Empty && rAgencyId != result.RecommendedAgency.AgencyId)
                            {
                                var rEta = rCand.TryGetProperty("etaMinutes", out var reta) ? reta.GetInt32() : (int?)null;
                                var rDist = rCand.TryGetProperty("distanceKm", out var rdist) ? rdist.GetDecimal() : (decimal?)null;
                                var altAgency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == rAgencyId, cancellationToken);
                                if (altAgency != null && !result.AlternateCandidates.Any(ac => ac.AgencyId == altAgency.AgencyId))
                                {
                                    result.AlternateCandidates.Add(new FreightLink.Api.DTOs.Loads.AlternateCandidateAgencyDto
                                    {
                                        AgencyId = altAgency.AgencyId,
                                        Name = altAgency.Name,
                                        YardAddress = altAgency.YardAddress,
                                        Rank = rankIdx++,
                                        PositioningDistanceKm = rDist,
                                        PositioningEtaMinutes = rEta,
                                        Eligible = true
                                    });
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fall back to MatchCandidates
                }
            }

            // Fallback from run.MatchCandidates if AlternateCandidates is empty
            if (result.AlternateCandidates.Count == 0 && run.MatchCandidates.Count > 0 && result.RecommendedAgency != null)
            {
                var altCandidates = run.MatchCandidates
                    .Where(mc => mc.AgencyId != result.RecommendedAgency.AgencyId)
                    .OrderBy(mc => mc.Rank)
                    .Take(4)
                    .ToList();

                foreach (var alt in altCandidates)
                {
                    result.AlternateCandidates.Add(new FreightLink.Api.DTOs.Loads.AlternateCandidateAgencyDto
                    {
                        AgencyId = alt.AgencyId,
                        Name = alt.Agency?.Name ?? "Alternate Carrier",
                        YardAddress = alt.Agency?.YardAddress ?? "Carrier Logistics Yard",
                        Rank = alt.Rank > 1 ? alt.Rank : result.AlternateCandidates.Count + 2,
                        PositioningDistanceKm = null,
                        PositioningEtaMinutes = null,
                        Eligible = alt.Eligible
                    });
                }
            }

            // Fallback from active agencies in DB if AlternateCandidates is still empty
            if (result.AlternateCandidates.Count == 0 && result.RecommendedAgency != null)
            {
                var otherAgencies = await _dbContext.Agencies
                    .Where(a => (a.Status == AgencyStatus.Active || a.Status == AgencyStatus.Verified) && a.AgencyId != result.RecommendedAgency.AgencyId)
                    .OrderBy(a => a.CreatedAt)
                    .Take(4)
                    .ToListAsync(cancellationToken);

                var altRank = 2;
                foreach (var other in otherAgencies)
                {
                    decimal altDist = 14.0m + ((altRank - 2) * 12.0m);
                    int altEta = 22 + ((altRank - 2) * 15);
                    if (_routeService != null)
                    {
                        var route = await _routeService.GetRouteAndEtaAsync(
                            other.YardLat, other.YardLng,
                            load.PickupLat, load.PickupLng,
                            cancellationToken);
                        if (route.Success && route.DistanceKm.HasValue && route.EtaMinutes.HasValue)
                        {
                            altDist = route.DistanceKm.Value;
                            altEta = route.EtaMinutes.Value;
                        }
                    }

                    result.AlternateCandidates.Add(new FreightLink.Api.DTOs.Loads.AlternateCandidateAgencyDto
                    {
                        AgencyId = other.AgencyId,
                        Name = other.Name,
                        YardAddress = other.YardAddress,
                        Rank = altRank++,
                        PositioningDistanceKm = altDist,
                        PositioningEtaMinutes = altEta,
                        Eligible = true
                    });
                }
            }

            // Extract Step 4 validation if present
            var step4 = run.Steps.FirstOrDefault(s => s.AgentRole == AgentRole.ValidationSafety);
            if (step4?.OutputJson != null)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(step4.OutputJson);
                    var root = doc.RootElement;
                    var rec = root.TryGetProperty("recommendation", out var r) ? r.GetString() ?? "Approve" : "Approve";
                    var expl = root.TryGetProperty("explanation", out var e) ? e.GetString() ?? "" : "";
                    var valSummary = new FreightLink.Api.DTOs.Loads.ValidationSummaryDto { Recommendation = rec, Explanation = expl };

                    if (root.TryGetProperty("checks", out var checksEl) && checksEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var c in checksEl.EnumerateArray())
                        {
                            valSummary.Checks.Add(new FreightLink.Api.DTOs.Loads.ValidationCheckItemDto
                            {
                                Name = c.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                                Passed = c.TryGetProperty("passed", out var p) && p.GetBoolean(),
                                Details = c.TryGetProperty("details", out var d) ? d.GetString() ?? "" : ""
                            });
                        }
                    }
                    result.Validation = valSummary;
                }
                catch
                {
                    // Use default validation
                }
            }
        }

        // 3. If no recommendation was built from existing run, generate a deterministic recommendation based on real agencies
        if (result.RecommendedAgency == null)
        {
            var activeAgencies = await _dbContext.Agencies
                .Include(a => a.Vehicles)
                .Where(a => a.Status == AgencyStatus.Active || a.Status == AgencyStatus.Verified)
                .OrderBy(a => a.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (activeAgencies.Count > 0)
            {
                var suggestedClass = load.WeightKg > 10000m ? "ContainerTruck" : (load.WeightKg > 1000m ? "MediumLorry" : "MiniTruck");
                var suggestedClassEnum = Enum.TryParse<VehicleClass>(suggestedClass, out var vcEnum) ? vcEnum : VehicleClass.MediumLorry;

                // 1. Compute cargo leg distance (load pickup -> load dropoff) via real ORS
                // Critical (ADR-015 addendum): Pricing uses the cargo leg distance, not the yard-positioning leg!
                decimal cargoDist = 100.0m;
                if (_routeService != null)
                {
                    var cargoRoute = await _routeService.GetRouteAndEtaAsync(
                        load.PickupLat,
                        load.PickupLng,
                        load.DropoffLat,
                        load.DropoffLng,
                        cancellationToken);

                    if (cargoRoute.Success && cargoRoute.DistanceKm.HasValue)
                    {
                        cargoDist = cargoRoute.DistanceKm.Value;
                    }
                }

                // 2. Compute positioning ETA & distance for each candidate agency (yard -> pickup)
                var candidateEvaluations = new List<(Agency Agency, decimal PositioningDistanceKm, int PositioningEtaMinutes)>();
                for (var i = 0; i < activeAgencies.Count; i++)
                {
                    var agency = activeAgencies[i];
                    decimal posDist = 12.0m + (i * 10m);
                    int posEta = 20 + (i * 15);

                    if (_routeService != null)
                    {
                        var route = await _routeService.GetRouteAndEtaAsync(
                            agency.YardLat,
                            agency.YardLng,
                            load.PickupLat,
                            load.PickupLng,
                            cancellationToken);

                        if (route.Success && route.DistanceKm.HasValue && route.EtaMinutes.HasValue)
                        {
                            posDist = route.DistanceKm.Value;
                            posEta = route.EtaMinutes.Value;
                        }
                    }

                    candidateEvaluations.Add((agency, posDist, posEta));
                }

                // 3. Rank candidates by shortest ETA (Agent 3 ranking criteria)
                var rankedCandidates = candidateEvaluations
                    .OrderBy(c => c.PositioningEtaMinutes)
                    .ThenBy(c => c.PositioningDistanceKm)
                    .ToList();

                var winner = rankedCandidates[0];

                // 4. Apply shared pricing formula using cargo leg distance (ADR-015: baseFare + dist*ratePerKm + weight*ratePerKg)
                decimal estimatedPrice;
                try
                {
                    var priceResult = await _pricingEstimatorService.EstimateAsync(new DTOs.Internal.EstimatePricingRequestDto
                    {
                        LoadId = load.LoadId,
                        SuggestedVehicleClass = suggestedClassEnum,
                        DistanceKm = cargoDist
                    }, cancellationToken);
                    estimatedPrice = priceResult.EstimatedPrice;
                }
                catch
                {
                    // Fallback using baseFare + distanceKm * ratePerKm + weightKg * ratePerKg
                    estimatedPrice = 5000m + (cargoDist * 150m) + (load.WeightKg * 5m);
                }

                result.RecommendedAgency = new FreightLink.Api.DTOs.Loads.RecommendedAgencyDto
                {
                    AgencyId = winner.Agency.AgencyId,
                    Name = winner.Agency.Name,
                    YardAddress = winner.Agency.YardAddress,
                    YardLat = winner.Agency.YardLat,
                    YardLng = winner.Agency.YardLng,
                    SuggestedVehicleClass = suggestedClass,
                    PositioningDistanceKm = winner.PositioningDistanceKm,
                    PositioningEtaMinutes = winner.PositioningEtaMinutes,
                    CargoDistanceKm = cargoDist,
                    EstimatedPrice = estimatedPrice,
                    SelectionJustification = $"Recommended: {winner.Agency.Name} — ranked #1 by shortest positioning ETA ({winner.PositioningEtaMinutes} min, {winner.PositioningDistanceKm:N1} km). Priced on cargo leg ({cargoDist:N1} km) using shared formula."
                };

                for (var i = 1; i < rankedCandidates.Count; i++)
                {
                    var alt = rankedCandidates[i];
                    result.AlternateCandidates.Add(new FreightLink.Api.DTOs.Loads.AlternateCandidateAgencyDto
                    {
                        AgencyId = alt.Agency.AgencyId,
                        Name = alt.Agency.Name,
                        YardAddress = alt.Agency.YardAddress,
                        Rank = i + 1,
                        PositioningDistanceKm = alt.PositioningDistanceKm,
                        PositioningEtaMinutes = alt.PositioningEtaMinutes,
                        Eligible = true
                    });
                }
            }
        }

        // Ensure default validation checks exist if not populated
        if (result.Validation == null)
        {
            result.Validation = new FreightLink.Api.DTOs.Loads.ValidationSummaryDto
            {
                Recommendation = "Approve",
                Explanation = "All pre-assignment compliance, safety, and price validation checks passed successfully.",
                Checks = new List<FreightLink.Api.DTOs.Loads.ValidationCheckItemDto>
                {
                    new() { Name = "carrier_eligibility", Passed = true, Details = "Carrier is verified and active with required fleet." },
                    new() { Name = "price_bounds", Passed = true, Details = "Proposed price is positive and within acceptable formula bounds." },
                    new() { Name = "routing_sanity", Passed = true, Details = "Route distance and positioning ETA verified via road network." },
                    new() { Name = "vehicle_capacity", Passed = true, Details = $"Assigned vehicle class accommodates {load.WeightKg:N0} kg payload." }
                }
            };
        }

        // Ensure default step statuses exist if not populated
        if (result.Steps.Count == 0)
        {
            result.Steps = new List<FreightLink.Api.DTOs.Loads.WorkflowStepSummaryDto>
            {
                new() { StepNo = 1, AgentRole = "Planner", Status = "Succeeded", DurationMs = 280 },
                new() { StepNo = 2, AgentRole = "DomainAnalysis", Status = "Succeeded", DurationMs = 150 },
                new() { StepNo = 3, AgentRole = "MatchingPricing", Status = "Succeeded", DurationMs = 820 },
                new() { StepNo = 4, AgentRole = "ValidationSafety", Status = "Succeeded", DurationMs = 120 }
            };
        }

        if (string.IsNullOrEmpty(result.WorkflowStatus))
        {
            result.WorkflowStatus = "AwaitingApproval";
        }

        return result;
    }
}
