using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class User
{
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneE164 { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ShipperProfile? ShipperProfile { get; set; }
    public AgencyStaff? AgencyStaff { get; set; }
    public Driver? DriverProfile { get; set; }
}
