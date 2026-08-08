using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Dispute
{
    public Guid DisputeId { get; set; }
    public Guid TripId { get; set; }
    public Guid RaisedByUserId { get; set; }
    public DisputeCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public User RaisedByUser { get; set; } = null!;
    public DisputeResolution? Resolution { get; set; }
}
