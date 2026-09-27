using System.Net;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.LoadProposals;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="ILoadProposalService" />
public class LoadProposalService : ILoadProposalService
{
    private readonly AppDbContext _dbContext;

    public LoadProposalService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoadProposalResponseDto> CreateAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CreateLoadProposalDto request, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only agencies can submit a load proposal.");
        }

        var staff = await _dbContext.AgencyStaff
            .Include(s => s.Agency)
            .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

        if (staff is null)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "No agency staff record found for caller.");
        }

        AgencyStatusGuard.EnsureActive(staff.Agency.Status);

        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);
        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        await EnsureLoadIsBiddableAsync(load, cancellationToken);

        var alreadyPending = await _dbContext.LoadProposals.AnyAsync(
            p => p.LoadId == loadId && p.AgencyId == staff.AgencyId && p.Status == LoadProposalStatus.Pending,
            cancellationToken);
        if (alreadyPending)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_PROPOSAL_ALREADY_EXISTS, "Your agency already has a pending proposal on this load.");
        }

        var now = DateTimeOffset.UtcNow;
        var proposal = new LoadProposal
        {
            LoadProposalId = Guid.NewGuid(),
            LoadId = loadId,
            AgencyId = staff.AgencyId,
            ProposedByUserId = currentUserId,
            ProposedPrice = request.ProposedPrice,
            Message = request.Message?.Trim(),
            Status = LoadProposalStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.LoadProposals.Add(proposal);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg && pg.ConstraintName == "ux_loadproposal_live_per_load_agency")
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_PROPOSAL_ALREADY_EXISTS, "Your agency already has a pending proposal on this load.");
        }

        return await MapToDtoAsync(proposal.LoadProposalId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<LoadProposalResponseDto>> GetListAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);
        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        var query = _dbContext.LoadProposals
            .Include(p => p.Load)
            .Include(p => p.Agency)
            .Include(p => p.ProposedByUser)
            .Where(p => p.LoadId == loadId)
            .AsQueryable();

        if (currentUserRole == UserRole.Shipper)
        {
            if (load.ShipperUserId != currentUserId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
            }
        }
        else if (currentUserRole == UserRole.AgencyStaff)
        {
            var staff = await _dbContext.AgencyStaff.FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);
            if (staff is null)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "No agency staff record found for caller.");
            }

            query = query.Where(p => p.AgencyId == staff.AgencyId);
        }
        else if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "You are not authorized to view proposals for this load.");
        }

        var proposals = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
        return proposals.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<LoadProposalResponseDto> AcceptAsync(Guid loadId, Guid proposalId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Shipper)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only the Shipper who posted this load can accept a proposal.");
        }

        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);
        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        await EnsureLoadIsBiddableAsync(load, cancellationToken);

        var proposal = await _dbContext.LoadProposals
            .Include(p => p.Agency)
            .FirstOrDefaultAsync(p => p.LoadProposalId == proposalId && p.LoadId == loadId, cancellationToken);

        if (proposal is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_PROPOSAL_NOT_FOUND, "The requested proposal could not be found.");
        }

        if (proposal.Status != LoadProposalStatus.Pending)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_PROPOSAL_STATUS_TRANSITION, $"This proposal is '{proposal.Status}' and can no longer be accepted.");
        }

        var now = DateTimeOffset.UtcNow;

        // Reuse (or create) the load's AgentWorkflowRun purely to satisfy Assignment's foreign key —
        // this Assignment did not come from the matching pipeline. Mirrors the marketplace
        // direct-accept path in AssignmentService.ApproveAsync.
        var workflowRun = await _dbContext.AgentWorkflowRuns.FirstOrDefaultAsync(r => r.LoadId == loadId, cancellationToken);
        if (workflowRun is null)
        {
            workflowRun = new AgentWorkflowRun
            {
                WorkflowRunId = Guid.NewGuid(),
                LoadId = loadId,
                TriggeredByUserId = currentUserId,
                AttemptNo = 1,
                Objective = $"Manual proposal accepted for load {load.ReferenceCode}",
                Status = WorkflowRunStatus.Completed,
                StartedAt = now,
                CompletedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.AgentWorkflowRuns.Add(workflowRun);
        }

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = loadId,
            AgencyId = proposal.AgencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = proposal.ProposedPrice,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Assignments.Add(assignment);

        _dbContext.AssignmentResponses.Add(new AssignmentResponse
        {
            AssignmentId = assignment.AssignmentId,
            RespondedByUserId = proposal.ProposedByUserId,
            Response = AssignmentResponseType.Accepted,
            RespondedAt = now
        });

        var previousStatus = load.Status;
        load.Status = LoadStatus.Matched;
        load.UpdatedAt = now;

        _dbContext.LoadStatusHistories.Add(new LoadStatusHistory
        {
            LoadStatusHistoryId = Guid.NewGuid(),
            LoadId = load.LoadId,
            FromStatus = previousStatus,
            ToStatus = LoadStatus.Matched,
            Reason = $"Shipper accepted a manual proposal from agency {proposal.Agency.Name}.",
            ChangedByUserId = currentUserId,
            ChangedAt = now
        });

        proposal.Status = LoadProposalStatus.Accepted;
        proposal.RespondedAt = now;
        proposal.UpdatedAt = now;

        // Every other still-pending proposal on this load is now moot.
        var otherPending = await _dbContext.LoadProposals
            .Where(p => p.LoadId == loadId && p.LoadProposalId != proposalId && p.Status == LoadProposalStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var other in otherPending)
        {
            other.Status = LoadProposalStatus.Rejected;
            other.ResponseReason = "Another agency's proposal was accepted for this load.";
            other.RespondedAt = now;
            other.UpdatedAt = now;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg && pg.ConstraintName == "ux_assignment_live_per_load")
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_NOT_BIDDABLE, "This load was already matched through another path.");
        }

        return MapToDto(proposal);
    }

    /// <inheritdoc />
    public async Task<LoadProposalResponseDto> RejectAsync(Guid loadId, Guid proposalId, Guid currentUserId, UserRole currentUserRole, RespondLoadProposalDto? request, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Shipper)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only the Shipper who posted this load can reject a proposal.");
        }

        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);
        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        var proposal = await _dbContext.LoadProposals
            .Include(p => p.Agency)
            .Include(p => p.ProposedByUser)
            .FirstOrDefaultAsync(p => p.LoadProposalId == proposalId && p.LoadId == loadId, cancellationToken);

        if (proposal is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_PROPOSAL_NOT_FOUND, "The requested proposal could not be found.");
        }

        if (proposal.Status != LoadProposalStatus.Pending)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_PROPOSAL_STATUS_TRANSITION, $"This proposal is '{proposal.Status}' and can no longer be rejected.");
        }

        var now = DateTimeOffset.UtcNow;
        proposal.Status = LoadProposalStatus.Rejected;
        proposal.ResponseReason = request?.Reason?.Trim();
        proposal.RespondedAt = now;
        proposal.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(proposal);
    }

    /// <inheritdoc />
    public async Task<LoadProposalResponseDto> WithdrawAsync(Guid loadId, Guid proposalId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.AgencyStaff)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only the proposing agency can withdraw a proposal.");
        }

        var staff = await _dbContext.AgencyStaff.FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);
        if (staff is null)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "No agency staff record found for caller.");
        }

        var proposal = await _dbContext.LoadProposals
            .Include(p => p.Agency)
            .Include(p => p.ProposedByUser)
            .FirstOrDefaultAsync(p => p.LoadProposalId == proposalId && p.LoadId == loadId, cancellationToken);

        if (proposal is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_PROPOSAL_NOT_FOUND, "The requested proposal could not be found.");
        }

        if (proposal.AgencyId != staff.AgencyId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "This proposal does not belong to your agency.");
        }

        if (proposal.Status != LoadProposalStatus.Pending)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.INVALID_LOAD_PROPOSAL_STATUS_TRANSITION, $"This proposal is '{proposal.Status}' and can no longer be withdrawn.");
        }

        var now = DateTimeOffset.UtcNow;
        proposal.Status = LoadProposalStatus.Withdrawn;
        proposal.RespondedAt = now;
        proposal.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(proposal);
    }

    /// <summary>
    /// A load only accepts new proposals — and only accepts an existing one — while it's still
    /// <c>Posted</c> with no live (Proposed/Accepted) <c>Assignment</c> of its own; the AI matching
    /// pipeline and manual proposals compete for the same single live-assignment slot per load.
    /// </summary>
    private async Task EnsureLoadIsBiddableAsync(Load load, CancellationToken cancellationToken)
    {
        if (load.Status != LoadStatus.Posted)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.LOAD_NOT_BIDDABLE, $"This load is '{load.Status}' and is no longer open to proposals.");
        }

        var hasLiveAssignment = await _dbContext.Assignments.AnyAsync(
            a => a.LoadId == load.LoadId && (a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.Accepted),
            cancellationToken);

        if (hasLiveAssignment)
        {
            throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.LOAD_NOT_BIDDABLE, "This load already has an active match and is no longer open to proposals.");
        }
    }

    private async Task<LoadProposalResponseDto> MapToDtoAsync(Guid proposalId, CancellationToken cancellationToken)
    {
        var proposal = await _dbContext.LoadProposals
            .Include(p => p.Load)
            .Include(p => p.Agency)
            .Include(p => p.ProposedByUser)
            .FirstAsync(p => p.LoadProposalId == proposalId, cancellationToken);

        return MapToDto(proposal);
    }

    private static LoadProposalResponseDto MapToDto(LoadProposal proposal) => new()
    {
        LoadProposalId = proposal.LoadProposalId,
        LoadId = proposal.LoadId,
        LoadReferenceCode = proposal.Load?.ReferenceCode,
        AgencyId = proposal.AgencyId,
        AgencyName = proposal.Agency?.Name,
        ProposedByUserId = proposal.ProposedByUserId,
        ProposedByName = proposal.ProposedByUser?.FullName,
        ProposedPrice = proposal.ProposedPrice,
        Message = proposal.Message,
        Status = proposal.Status.ToString(),
        ResponseReason = proposal.ResponseReason,
        RespondedAt = proposal.RespondedAt,
        CreatedAt = proposal.CreatedAt,
        UpdatedAt = proposal.UpdatedAt
    };
}
