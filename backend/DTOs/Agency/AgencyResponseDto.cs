using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class AgencyResponseDto
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BusinessRegNo { get; set; } = string.Empty;
    public string YardAddress { get; set; } = string.Empty;
    public decimal YardLat { get; set; }
    public decimal YardLng { get; set; }
    public AgencyStatus Status { get; set; }
    public int DriverCount { get; set; }
    public int ActiveDriverCount { get; set; }
    public int VehicleCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
