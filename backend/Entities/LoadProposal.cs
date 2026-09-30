using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// An Agency's manual price quote/bid submitted directly on a <c>Posted</c> load, independent of
/// the AI matching pipeline (<see cref="AgentWorkflowRun"/>/<see cref="MatchCandidate"/>). Many
/// agencies may each have one live (<see cref="LoadProposalStatus.Pending"/>) proposal on the same
/// load; the Shipper accepting one converts it into a normal <see cref="Assignment"/> and auto-rejects
/// every other still-pending proposal on that load.
/// </summary>
public class LoadProposal
{
    public Guid LoadProposalId { get; set; }
    public Guid LoadId { get; set; }
    public Guid AgencyId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public decimal ProposedPrice { get; set; }
    public string? Message { get; set; }
    public LoadProposalStatus Status { get; set; }
    public string? ResponseReason { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Load Load { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User ProposedByUser { get; set; } = null!;
}
