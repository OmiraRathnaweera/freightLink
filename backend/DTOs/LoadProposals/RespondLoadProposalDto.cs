using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.LoadProposals;

/// <summary>Payload for a Shipper rejecting one of their load's manual proposals.</summary>
public class RespondLoadProposalDto
{
    /// <summary>Optional reason shown back to the proposing agency.</summary>
    [StringLength(1000)]
    public string? Reason { get; set; }
}
