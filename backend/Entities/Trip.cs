using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Trip
{
    public Guid TripId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid DriverId { get; set; }
    public TripStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
    public ICollection<TripEvent> Events { get; set; } = new List<TripEvent>();
    public ICollection<TripEvidence> Evidence { get; set; } = new List<TripEvidence>();
    public Invoice? Invoice { get; set; }
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}
