namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// Combined response of vehicles and drivers for an agency fleet.
/// </summary>
public class AgencyFleetResponseDto
{
    public Guid AgencyId { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public List<VehicleResponseDto> Vehicles { get; set; } = new();
    public List<DriverResponseDto> Drivers { get; set; } = new();
}
