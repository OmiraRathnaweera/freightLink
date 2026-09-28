using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>Agency Staff roster-management request for a driver: <c>PATCH /agencies/{id}/drivers/{driverId}/status</c>.</summary>
public class UpdateDriverStatusDto
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriverStatus Status { get; set; }
}
