using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.LoadProposals;

/// <summary>Payload for an Agency submitting a manual price proposal on a posted load.</summary>
public class CreateLoadProposalDto
{
    /// <summary>The agency's quoted price for hauling this load.</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Proposed price must be greater than 0.")]
    public decimal ProposedPrice { get; set; }

    /// <summary>Optional pitch/note shown to the Shipper alongside the price.</summary>
    [StringLength(1000)]
    public string? Message { get; set; }
}
