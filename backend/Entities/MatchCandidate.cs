namespace FreightLink.Api.Entities;

public class MatchCandidate
{
    public Guid MatchCandidateId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public Guid AgencyId { get; set; }
    public int Rank { get; set; }
    public decimal EligibilityScore { get; set; }
    public bool Eligible { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }

    public AgentWorkflowRun WorkflowRun { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
}
