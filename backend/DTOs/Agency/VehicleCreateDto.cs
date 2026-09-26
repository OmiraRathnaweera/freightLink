using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class VehicleCreateDto
{
    [Required]
    [StringLength(50)]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required]
    public VehicleType VehicleType { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CapacityKg { get; set; }

    [Range(0, double.MaxValue)]
    public decimal VolumeM3 { get; set; }
}
