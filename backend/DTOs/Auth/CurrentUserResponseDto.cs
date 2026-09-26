namespace FreightLink.Api.DTOs.Auth;

/// <summary>Response for <c>GET /api/v1/auth/me</c> — the authenticated caller's safe profile (no password hash).</summary>
public class CurrentUserResponseDto
{
    /// <summary>The user's unique id.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user's login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Optional contact phone number in E.164 format.</summary>
    public string? PhoneE164 { get; set; }

    /// <summary>The user's role (e.g. "Shipper", "AgencyStaff", "Driver", "Admin").</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Whether the account is active; inactive accounts cannot log in.</summary>
    public bool IsActive { get; set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The ID of the agency this user belongs to, if they are AgencyStaff.</summary>
    public Guid? AgencyId { get; set; }
}
