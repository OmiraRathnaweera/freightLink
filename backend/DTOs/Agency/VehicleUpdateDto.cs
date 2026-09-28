using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>Editable fleet details. Availability is changed through the dedicated status endpoint.</summary>
public class VehicleUpdateDto
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(VehicleType))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleType? VehicleType { get; set; }

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal CapacityKg { get; set; }

    [Range(typeof(decimal), "0.001", "1000")]
    public decimal VolumeM3 { get; set; }
}
