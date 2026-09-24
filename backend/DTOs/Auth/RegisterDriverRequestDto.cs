using System.ComponentModel.DataAnnotations;
using FreightLink.Api.DTOs.Agency;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Payload for public self-service driver registration (mobile app): <c>POST /api/v1/auth/register/driver</c>.
/// </summary>
public class RegisterDriverRequestDto : CreateDriverRequestDto
{
    /// <summary>
    /// The ID of the agency the driver is registering under.
    /// </summary>
    [Required]
    public Guid AgencyId { get; set; }
}
