using System.Net;
using System.Text.Json;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAgentWorkflowService" />
public class AgentWorkflowService : IAgentWorkflowService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the service with its DB context.</summary>
    public AgentWorkflowService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CreateAgentWorkflowRunResponseDto> CreateAsync(CreateAgentWorkflowRunRequestDto request, CancellationToken cancellationToken = default)
    {
        var loadId = request.LoadId!.Value;
        var triggeredByUserId = request.TriggeredByUserId!.Value;

        var loadExists = await _dbContext.Loads.AnyAsync(l => l.LoadId == loadId, cancellationToken);
        if (!loadExists)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND_FOR_WORKFLOW_RUN, "The requested load could not be found.");
        }

        var userExists = await _dbContext.Users.AnyAsync(u => u.UserId == triggeredByUserId, cancellationToken);
        if (!userExists)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND_FOR_WORKFLOW_RUN, "The triggering user could not be found.");
        }

        var now = DateTimeOffset.UtcNow;
        var run = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = loadId,
            TriggeredByUserId = triggeredByUserId,
            AttemptNo = request.AttemptNo!.Value,
            // Objective/PlanJson are Agent 1's own output - populated once its step-1 report
            // arrives, not here at creation time.
            Objective = string.Empty,
            PlanJson = null,
            Status = WorkflowRunStatus.Running,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.AgentWorkflowRuns.Add(run);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_awr_load_attempt" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.WORKFLOW_RUN_DUPLICATE_ATTEMPT, "A workflow run for this load and attempt number already exists.");
        }

        return new CreateAgentWorkflowRunResponseDto { WorkflowRunId = run.WorkflowRunId };
    }

    /// <inheritdoc />
    public async Task<ReportAgentStepResponseDto> ReportStepAsync(Guid workflowRunId, ReportAgentStepRequestDto request, CancellationToken cancellationToken = default)
    {
        var status = request.Status!.Value;
        if (status == AgentStepStatus.Failed && string.IsNullOrWhiteSpace(request.ErrorMessage))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "ErrorMessage is required when Status is Failed.");
        }

        var run = await _dbContext.AgentWorkflowRuns.FirstOrDefaultAsync(r => r.WorkflowRunId == workflowRunId, cancellationToken);
        if (run is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.WORKFLOW_RUN_NOT_FOUND, "The requested workflow run could not be found.");
        }

        var agentRole = request.AgentRole!.Value;
        var step = new AgentStep
        {
            AgentStepId = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            StepNo = request.StepNo!.Value,
            AgentRole = agentRole,
            Status = status,
            InputJson = request.InputJson,
            OutputJson = request.OutputJson,
            ErrorMessage = request.ErrorMessage,
            DurationMs = request.DurationMs,
            StartedAt = request.StartedAt!.Value,
            CompletedAt = request.CompletedAt
        };

        _dbContext.AgentSteps.Add(step);

        // A Failed step at any stage is a safe, recorded failure for the whole run (ADR-018) - no
        // other agent exists yet to define a further outcome for its own success, so a Succeeded
        // step only updates the run for Agent 1 (Planner) specifically, and only its own columns.
        if (status == AgentStepStatus.Failed)
        {
            run.Status = WorkflowRunStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }
        else if (status == AgentStepStatus.Succeeded && agentRole == AgentRole.Planner)
        {
            using var plan = JsonDocument.Parse(request.OutputJson ?? "{}");
            run.Objective = plan.RootElement.TryGetProperty("objective", out var objectiveProperty) ? objectiveProperty.GetString() ?? string.Empty : string.Empty;
            run.PlanJson = request.OutputJson;
        }

        // UpdatedAt is not set here - trg_set_updated_at_agentworkflowruns (see
        // AgentWorkflowRunConfiguration) writes it on every UPDATE, and EF's
        // ValueGeneratedOnAddOrUpdate reads that back after SaveChangesAsync.

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_agentstep_order" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.AGENT_STEP_DUPLICATE, "This step number was already reported for this workflow run.");
        }

        return new ReportAgentStepResponseDto { AgentStepId = step.AgentStepId, Status = step.Status };
    }
}
