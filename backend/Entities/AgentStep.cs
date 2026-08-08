using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class AgentStep
{
    public Guid AgentStepId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public int StepNo { get; set; }
    public string AgentRole { get; set; } = string.Empty;
    public AgentStepStatus Status { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public AgentWorkflowRun WorkflowRun { get; set; } = null!;
    public ICollection<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();
}
