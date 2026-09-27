using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class ComplianceDocCreateDto
{
    [Required]
    public string PublicId { get; set; } = string.Empty;

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ComplianceDocType DocType { get; set; }

    [Required]
    [StringLength(100)]
    public string DocNumber { get; set; } = string.Empty;

    public DateOnly IssuedOn { get; set; }
    
    public DateOnly? ExpiresOn { get; set; }
}
