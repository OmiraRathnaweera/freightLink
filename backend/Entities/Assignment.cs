using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Assignment
{
    public Guid AssignmentId { get; set; }
    public Guid LoadId { get; set; }
    public Guid AgencyId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public decimal ProposedPrice { get; set; }
    public decimal? RoutedDistanceKm { get; set; }
    public int? ProposedEtaMinutes { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Load Load { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public AgentWorkflowRun WorkflowRun { get; set; } = null!;
    public AssignmentResponse? Response { get; set; }
    public Trip? Trip { get; set; }
}
