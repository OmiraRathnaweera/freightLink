using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class TripEvent
{
    public Guid TripEventId { get; set; }
    public Guid TripId { get; set; }
    public Guid RecordedByUserId { get; set; }
    public TripStatus? FromStatus { get; set; }
    public TripStatus ToStatus { get; set; }
    public string? Notes { get; set; }
    public decimal? SnapshotLat { get; set; }
    public decimal? SnapshotLng { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
}
