namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Lightweight agency summary used for public registration dropdowns (e.g., driver signup on mobile).
/// </summary>
public class AgencyLookupDto
{
    /// <summary>
    /// Unique identifier of the agency.
    /// </summary>
    public Guid AgencyId { get; set; }

    /// <summary>
    /// Display name of the agency.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
