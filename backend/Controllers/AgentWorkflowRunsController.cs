using FreightLink.Api.Common.Filters;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Internal, service-to-service endpoints for the Agentic AI pipeline's Agent 1 (Planner) — never
/// called by a shipper or any public client. Deliberately routed outside the <c>/api/v1</c> prefix,
/// same as <see cref="InternalPricingController"/> (see
/// <c>../docs/api-contract-openapi-skeleton.md</c>'s scoping note on ASP.NET↔Python service calls).
/// Guarded by <see cref="InternalApiKeyAuthFilter"/> (a shared-secret header), not JWT — this has no
/// human caller and carries no <c>[Authorize]</c> attribute.
/// </summary>
[ApiController]
[Route("internal/agent-workflow-runs")]
[ServiceFilter(typeof(InternalApiKeyAuthFilter))]
public class AgentWorkflowRunsController : ControllerBase
{
    private readonly IAgentWorkflowService _agentWorkflowService;

    /// <summary>Creates the controller with its injected agent-workflow service.</summary>
    public AgentWorkflowRunsController(IAgentWorkflowService agentWorkflowService)
    {
        _agentWorkflowService = agentWorkflowService;
    }

    /// <summary>
    /// Creates a new <c>AgentWorkflowRun</c> row so Agent 1 has a real id to report every
    /// subsequent step against.
    /// </summary>
    /// <param name="request">The load id, triggering user id, and attempt number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the new run's id.</returns>
    [HttpPost]
    public async Task<ActionResult<CreateAgentWorkflowRunResponseDto>> Create([FromBody] CreateAgentWorkflowRunRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _agentWorkflowService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { workflowRunId = result.WorkflowRunId }, result);
    }

    /// <summary>Records one agent's step outcome under an existing workflow run.</summary>
    /// <param name="workflowRunId">The run this step belongs to.</param>
    /// <param name="request">The step's outcome and audit-trail data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the new step's id.</returns>
    [HttpPost("{workflowRunId:guid}/steps")]
    public async Task<ActionResult<ReportAgentStepResponseDto>> ReportStep(Guid workflowRunId, [FromBody] ReportAgentStepRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _agentWorkflowService.ReportStepAsync(workflowRunId, request, cancellationToken);
        return CreatedAtAction(nameof(ReportStep), new { workflowRunId }, result);
    }

    /// <summary>
    /// Records one tool invocation attempt (ToolCall) under an existing workflow run (Agent 3 Matching/Pricing).
    /// </summary>
    /// <param name="workflowRunId">The run this tool call belongs to.</param>
    /// <param name="request">The tool call's telemetry and audit-trail data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created tool call's id and details.</returns>
    [HttpPost("{workflowRunId:guid}/tool-calls")]
    public async Task<ActionResult<FreightLink.Api.DTOs.Internal.ToolCalls.ToolCallResponseDto>> RecordToolCall(Guid workflowRunId, [FromBody] FreightLink.Api.DTOs.Internal.ToolCalls.CreateToolCallRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _agentWorkflowService.RecordToolCallAsync(workflowRunId, request, cancellationToken);
        return CreatedAtAction(nameof(RecordToolCall), new { workflowRunId }, result);
    }
}

