using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Vehicle
{
    public Guid VehicleId { get; set; }
    public Guid AgencyId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Agency Agency { get; set; } = null!;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
