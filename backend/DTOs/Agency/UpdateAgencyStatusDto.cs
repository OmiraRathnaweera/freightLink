using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>Admin request to change an agency's status: <c>PATCH /agencies/{id}/status</c>.</summary>
public class UpdateAgencyStatusDto
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgencyStatus Status { get; set; }

    /// <summary>Why the status is changing; recorded in the agency's status history.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
