namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// Driver summary response DTO (GET /api/v1/agencies/{id}/drivers).
/// </summary>
public class DriverResponseDto
{
    public Guid DriverId { get; set; }
    public Guid UserId { get; set; }
    public Guid AgencyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LicenceNo { get; set; } = string.Empty;
    public DateOnly LicenceExpiry { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
