namespace FreightLink.Api.DTOs.LoadProposals;

/// <summary>Response DTO for a manual load proposal (agency bid on a posted load).</summary>
public class LoadProposalResponseDto
{
    public Guid LoadProposalId { get; set; }
    public Guid LoadId { get; set; }

    /// <summary>The load's human-readable reference code, for display without a second fetch.</summary>
    public string? LoadReferenceCode { get; set; }

    public Guid AgencyId { get; set; }
    public string? AgencyName { get; set; }

    public Guid ProposedByUserId { get; set; }
    public string? ProposedByName { get; set; }

    public decimal ProposedPrice { get; set; }
    public string? Message { get; set; }

    /// <summary>Pending, Accepted, Rejected, or Withdrawn.</summary>
    public string Status { get; set; } = string.Empty;

    public string? ResponseReason { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
