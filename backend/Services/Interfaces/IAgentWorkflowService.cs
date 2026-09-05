using FreightLink.Api.DTOs.Internal;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Backs the internal, non-shipper-facing <c>/internal/agent-workflow-runs</c> endpoints called by
/// the Agentic AI pipeline's Agent 1 (Planner). This service performs no authentication — the
/// caller has already passed <c>InternalApiKeyAuthFilter</c>'s shared-secret check before either
/// method here is invoked.
/// </summary>
public interface IAgentWorkflowService
{
    /// <summary>
    /// Creates a new <c>AgentWorkflowRun</c> row for the given load/user/attempt and returns its
    /// real, server-generated id — the only id Agent 1 is allowed to report subsequent steps
    /// against (never a locally generated placeholder on the caller's side).
    /// </summary>
    /// <param name="request">The load id, triggering user id, and attempt number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created run's id.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if <c>LoadId</c> or <c>TriggeredByUserId</c> doesn't exist; 409
    /// <see cref="Common.Errors.ErrorCode.WORKFLOW_RUN_DUPLICATE_ATTEMPT"/> if this
    /// <c>(LoadId, AttemptNo)</c> pair already has a run.
    /// </exception>
    Task<CreateAgentWorkflowRunResponseDto> CreateAsync(CreateAgentWorkflowRunRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one <c>AgentStep</c> row under the given run. A <c>Failed</c> status flips the
    /// parent <c>AgentWorkflowRun.Status</c> to <c>Failed</c> (a safe, recorded failure — ADR-018);
    /// a <c>Succeeded</c> Planner (step 1) step additionally writes the run's <c>Objective</c>/
    /// <c>PlanJson</c> columns. No other step/role currently changes run-level state, since no
    /// other agent exists yet to define what its own success should mean for the run.
    /// </summary>
    /// <param name="workflowRunId">The run this step belongs to.</param>
    /// <param name="request">The step's outcome and audit-trail data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created step's id and echoed status.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 <see cref="Common.Errors.ErrorCode.WORKFLOW_RUN_NOT_FOUND"/> if <paramref name="workflowRunId"/>
    /// doesn't exist; 400 <see cref="Common.Errors.ErrorCode.VALIDATION_ERROR"/> if <c>Status</c> is
    /// <c>Failed</c> with no <c>ErrorMessage</c>; 409
    /// <see cref="Common.Errors.ErrorCode.AGENT_STEP_DUPLICATE"/> if this
    /// <c>(WorkflowRunId, StepNo)</c> pair was already reported.
    /// </exception>
    Task<ReportAgentStepResponseDto> ReportStepAsync(Guid workflowRunId, ReportAgentStepRequestDto request, CancellationToken cancellationToken = default);
}
