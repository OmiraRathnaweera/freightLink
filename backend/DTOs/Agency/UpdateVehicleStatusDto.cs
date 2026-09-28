using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>Agency Staff availability-management request for a fleet vehicle.</summary>
public class UpdateVehicleStatusDto
{
    [Required]
    [EnumDataType(typeof(VehicleStatus))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleStatus? Status { get; set; }
}
