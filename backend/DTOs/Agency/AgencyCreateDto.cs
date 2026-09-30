using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Agency;

public class AgencyCreateDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string BusinessRegNo { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string YardAddress { get; set; } = string.Empty;

    [Range(-90, 90)]
    public decimal YardLat { get; set; }

    [Range(-180, 180)]
    public decimal YardLng { get; set; }
}
