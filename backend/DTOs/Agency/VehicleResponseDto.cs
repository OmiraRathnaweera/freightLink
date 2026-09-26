namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// Vehicle summary response DTO (GET /api/v1/agencies/{id}/vehicles).
/// </summary>
public class VehicleResponseDto
{
    public Guid VehicleId { get; set; }
    public Guid AgencyId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
