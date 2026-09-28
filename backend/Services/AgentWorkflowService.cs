using System.Net;
using System.Text.Json;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Internal.Candidates;
using FreightLink.Api.DTOs.Internal.ToolCalls;
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

        var existingStep = await _dbContext.AgentSteps
            .FirstOrDefaultAsync(s => s.WorkflowRunId == workflowRunId && s.StepNo == request.StepNo!.Value, cancellationToken);

        AgentStep step;
        if (existingStep != null)
        {
            step = existingStep;
            step.AgentRole = agentRole;
            step.Status = status;
            step.InputJson = request.InputJson;
            step.OutputJson = request.OutputJson;
            step.ErrorMessage = request.ErrorMessage;
            step.DurationMs = request.DurationMs;
            step.StartedAt = request.StartedAt!.Value;
            step.CompletedAt = request.CompletedAt;
        }
        else
        {
            step = new AgentStep
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
        }

        // A Failed step at any stage is a safe, recorded failure for the whole run (ADR-018)
        if (status == AgentStepStatus.Failed)
        {
            run.Status = WorkflowRunStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }
        else if (status == AgentStepStatus.Succeeded && agentRole == AgentRole.Planner)
        {
            using var plan = JsonDocument.Parse(request.OutputJson ?? "{}");
            run.Objective = plan.RootElement.TryGetProperty("objective", out var objectiveProperty) ? objectiveProperty.GetString() ?? string.Empty : string.Empty;
            run.ShipperMessage = plan.RootElement.TryGetProperty("shipper_message", out var shipperMessageProperty) ? shipperMessageProperty.GetString() : null;
            run.PlanJson = request.OutputJson;
        }
        else if (status == AgentStepStatus.Succeeded && agentRole == AgentRole.DomainAnalysis && !string.IsNullOrWhiteSpace(request.OutputJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(request.OutputJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var candidatesProp) && candidatesProp.ValueKind == JsonValueKind.Array)
                {
                    var dtos = new List<MatchCandidateDto>();
                    foreach (var c in candidatesProp.EnumerateArray())
                    {
                        if (c.TryGetProperty("agencyId", out var aProp) && Guid.TryParse(aProp.GetString(), out var agencyId))
                        {
                            var rank = c.TryGetProperty("rank", out var rProp) ? rProp.GetInt32() : 1;
                            var eligible = c.TryGetProperty("eligible", out var eProp) && eProp.GetBoolean();
                            var rejection = c.TryGetProperty("rejectionReason", out var rejProp) && rejProp.ValueKind == JsonValueKind.String ? rejProp.GetString() : null;
                            var score = c.TryGetProperty("eligibilityScore", out var sProp) ? sProp.GetDecimal() : (eligible ? 100m : 0m);

                            dtos.Add(new MatchCandidateDto
                            {
                                AgencyId = agencyId,
                                Rank = rank,
                                Eligible = eligible,
                                EligibilityScore = score,
                                RejectionReason = rejection
                            });
                        }
                    }

                    if (dtos.Count > 0)
                    {
                        await UpsertMatchCandidatesInternalAsync(workflowRunId, dtos, cancellationToken);
                    }
                }
            }
            catch (Exception)
            {
                // Non-blocking fallback if JSON parsing fails
            }
        }
        else if (status == AgentStepStatus.Succeeded && agentRole == AgentRole.ValidationSafety)
        {
            run.Status = WorkflowRunStatus.AwaitingApproval;
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

    /// <inheritdoc />
    public async Task<ToolCallResponseDto> RecordToolCallAsync(Guid workflowRunId, CreateToolCallRequestDto request, CancellationToken cancellationToken = default)
    {
        var runExists = await _dbContext.AgentWorkflowRuns
            .AnyAsync(r => r.WorkflowRunId == workflowRunId, cancellationToken);

        if (!runExists)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.WORKFLOW_RUN_NOT_FOUND, "The requested workflow run could not be found.");
        }

        if (!request.Success && string.IsNullOrWhiteSpace(request.ErrorMessage))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "ErrorMessage is required when Success is false.");
        }

        Guid agentStepId;
        if (request.AgentStepId.HasValue && request.AgentStepId.Value != Guid.Empty)
        {
            var stepExists = await _dbContext.AgentSteps
                .AnyAsync(s => s.AgentStepId == request.AgentStepId.Value && s.WorkflowRunId == workflowRunId, cancellationToken);
            if (!stepExists)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENT_STEP_NOT_FOUND, "The specified agent step could not be found for this workflow run.");
            }
            agentStepId = request.AgentStepId.Value;
        }
        else
        {
            // Auto-resolve or create the step for MatchingPricing (Agent 3)
            var step = await _dbContext.AgentSteps
                .FirstOrDefaultAsync(s => s.WorkflowRunId == workflowRunId && s.AgentRole == AgentRole.MatchingPricing, cancellationToken);

            if (step == null)
            {
                step = new AgentStep
                {
                    AgentStepId = Guid.NewGuid(),
                    WorkflowRunId = workflowRunId,
                    StepNo = 3,
                    AgentRole = AgentRole.MatchingPricing,
                    Status = AgentStepStatus.Running,
                    StartedAt = request.CalledAt ?? DateTimeOffset.UtcNow
                };
                _dbContext.AgentSteps.Add(step);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            agentStepId = step.AgentStepId;
        }

        var toolCall = new ToolCall
        {
            ToolCallId = Guid.NewGuid(),
            AgentStepId = agentStepId,
            ToolName = request.ToolName!.Value,
            AttemptNo = request.AttemptNo,
            RequestJson = request.RequestJson,
            ResponseJson = request.ResponseJson,
            Success = request.Success,
            HttpStatusCode = request.HttpStatusCode,
            DurationMs = request.DurationMs,
            ErrorMessage = request.ErrorMessage,
            CalledAt = request.CalledAt ?? DateTimeOffset.UtcNow
        };

        _dbContext.ToolCalls.Add(toolCall);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_toolcall_attempt" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.TOOL_CALL_DUPLICATE_ATTEMPT, "A tool call with this attempt number already exists for this step.");
        }

        return new ToolCallResponseDto
        {
            ToolCallId = toolCall.ToolCallId,
            AgentStepId = toolCall.AgentStepId,
            ToolName = toolCall.ToolName,
            AttemptNo = toolCall.AttemptNo,
            Success = toolCall.Success,
            HttpStatusCode = toolCall.HttpStatusCode,
            DurationMs = toolCall.DurationMs,
            ErrorMessage = toolCall.ErrorMessage,
            CalledAt = toolCall.CalledAt
        };
    }

    private async Task UpsertMatchCandidatesInternalAsync(Guid workflowRunId, IEnumerable<MatchCandidateDto> candidates, CancellationToken cancellationToken)
    {
        foreach (var c in candidates)
        {
            if (!c.AgencyId.HasValue || c.AgencyId.Value == Guid.Empty)
            {
                continue;
            }

            var agencyId = c.AgencyId.Value;
            var agencyExists = await _dbContext.Agencies.AnyAsync(a => a.AgencyId == agencyId, cancellationToken);
            if (!agencyExists)
            {
                continue;
            }

            var rejectionReason = c.RejectionReason;
            if (!c.Eligible && string.IsNullOrWhiteSpace(rejectionReason))
            {
                rejectionReason = "Carrier fleet does not meet criteria";
            }

            var score = Math.Clamp(c.EligibilityScore, 0m, 100m);

            var existingCandidate = await _dbContext.MatchCandidates
                .FirstOrDefaultAsync(mc => mc.WorkflowRunId == workflowRunId && mc.AgencyId == agencyId, cancellationToken);

            if (existingCandidate != null)
            {
                existingCandidate.Rank = c.Rank;
                existingCandidate.Eligible = c.Eligible;
                existingCandidate.EligibilityScore = score;
                existingCandidate.RejectionReason = rejectionReason;
            }
            else
            {
                _dbContext.MatchCandidates.Add(new MatchCandidate
                {
                    MatchCandidateId = Guid.NewGuid(),
                    WorkflowRunId = workflowRunId,
                    AgencyId = agencyId,
                    Rank = c.Rank,
                    Eligible = c.Eligible,
                    EligibilityScore = score,
                    RejectionReason = rejectionReason,
                    EvaluatedAt = DateTimeOffset.UtcNow
                });
            }
        }
    }

    /// <inheritdoc />
    public async Task RecordMatchCandidatesAsync(Guid workflowRunId, IEnumerable<MatchCandidateDto> candidates, CancellationToken cancellationToken = default)
    {
        var runExists = await _dbContext.AgentWorkflowRuns
            .AnyAsync(r => r.WorkflowRunId == workflowRunId, cancellationToken);

        if (!runExists)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.WORKFLOW_RUN_NOT_FOUND, "The requested workflow run could not be found.");
        }

        await UpsertMatchCandidatesInternalAsync(workflowRunId, candidates, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

