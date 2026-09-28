using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Agency;

public class DriverUpdateDto
{
    [StringLength(100)]
    public string? FullName { get; set; }

    [StringLength(50)]
    public string? LicenceNo { get; set; }

    public DateOnly? LicenceExpiry { get; set; }
}
