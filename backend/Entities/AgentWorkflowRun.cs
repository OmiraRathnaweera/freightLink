using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class AgentWorkflowRun
{
    public Guid WorkflowRunId { get; set; }
    public Guid LoadId { get; set; }
    public Guid TriggeredByUserId { get; set; }
    public int AttemptNo { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string? PlanJson { get; set; }
    public WorkflowRunStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Load Load { get; set; } = null!;
    public User TriggeredByUser { get; set; } = null!;
    public ICollection<MatchCandidate> MatchCandidates { get; set; } = new List<MatchCandidate>();
    public ICollection<AgentStep> Steps { get; set; } = new List<AgentStep>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<ApprovalDecision> ApprovalDecisions { get; set; } = new List<ApprovalDecision>();
}
