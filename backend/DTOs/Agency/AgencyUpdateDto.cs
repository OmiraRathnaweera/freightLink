using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Agency;

public class AgencyUpdateDto
{
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(255)]
    public string? YardAddress { get; set; }

    [Range(-90, 90)]
    public decimal? YardLat { get; set; }

    [Range(-180, 180)]
    public decimal? YardLng { get; set; }
}
