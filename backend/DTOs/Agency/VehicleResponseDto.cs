using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class VehicleResponseDto
{
    public Guid VehicleId { get; set; }
    public Guid AgencyId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
