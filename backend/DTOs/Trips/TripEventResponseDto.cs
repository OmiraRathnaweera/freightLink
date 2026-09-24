namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// One <c>TripEvent</c> row — a single status transition in a trip's append-only audit trail.
/// Never exposes the <c>TripEvent</c> entity directly.
/// </summary>
public class TripEventResponseDto
{
    /// <summary>The event's unique id.</summary>
    public Guid TripEventId { get; set; }

    /// <summary>The user (typically the assigned Driver) who triggered this transition.</summary>
    public Guid RecordedByUserId { get; set; }

    /// <summary>Status before the transition; <see langword="null"/> only for a trip's very first status row.</summary>
    public string? FromStatus { get; set; }

    /// <summary>Status after the transition.</summary>
    public string ToStatus { get; set; } = string.Empty;

    /// <summary>Optional free-text context for the transition.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional GPS latitude captured at the time of the transition.</summary>
    public decimal? SnapshotLat { get; set; }

    /// <summary>Optional GPS longitude captured at the time of the transition.</summary>
    public decimal? SnapshotLng { get; set; }

    /// <summary>When the transition occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}