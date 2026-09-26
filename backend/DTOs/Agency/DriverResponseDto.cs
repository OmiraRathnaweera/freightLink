using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class DriverResponseDto
{
    public Guid DriverId { get; set; }
    public Guid UserId { get; set; }
    public Guid AgencyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LicenceNo { get; set; } = string.Empty;
    public DateOnly LicenceExpiry { get; set; }
    public DriverStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
