using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class VehicleResponseDto
{
    public Guid VehicleId { get; set; }
    public Guid AgencyId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
