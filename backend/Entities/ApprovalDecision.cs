using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class ApprovalDecision
{
    public Guid ApprovalDecisionId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public Guid DecidedByUserId { get; set; }
    public int SequenceNo { get; set; }
    public ApprovalDecisionType Decision { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset DecidedAt { get; set; }

    public AgentWorkflowRun WorkflowRun { get; set; } = null!;
    public User DecidedByUser { get; set; } = null!;
}
