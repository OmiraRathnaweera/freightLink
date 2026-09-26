using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Agency;

public class DriverCreateDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LicenceNo { get; set; } = string.Empty;

    [Required]
    public DateOnly LicenceExpiry { get; set; }
}
