using FreightLink.Api.Entities.Enums;

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
    public DriverStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The server-generated temporary password, shown exactly once in the response returned from
    /// creating this driver (as a fallback in case the credentials email is delayed or undeliverable).
    /// Always <see langword="null"/> on every other response (list/update) — never persisted or
    /// retrievable again after creation.
    /// </summary>
    public string? TemporaryPassword { get; set; }
}
