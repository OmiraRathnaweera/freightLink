namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Response for <c>POST /internal/agent-workflow-runs</c> — the real, backend-generated
/// <c>WorkflowRunId</c> Agent 1 must use for every subsequent step report on this run.
/// </summary>
public class CreateAgentWorkflowRunResponseDto
{
    /// <summary>The newly created <c>AgentWorkflowRun.WorkflowRunId</c>.</summary>
    public Guid WorkflowRunId { get; set; }
}
