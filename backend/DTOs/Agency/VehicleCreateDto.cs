using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class VehicleCreateDto
{
    [Required]
    [StringLength(50)]
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
