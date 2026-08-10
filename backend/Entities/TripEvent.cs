using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// One <see cref="Entities.TripStatus"/> transition in a <see cref="Trip"/>'s audit trail, with an
/// optional GPS snapshot at the moment it occurred.
/// Append-only at the database level: <c>trg_deny_mutation_tripevents</c> (added in the
/// AddDatabaseConstraintsAndTriggers migration) rejects every UPDATE/DELETE against this table
/// with a raw <c>PostgresException</c> — service code must never attempt to modify or remove an
/// existing row, and should catch/translate that exception into a domain error rather than let
/// it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class TripEvent
{
    /// <summary>Primary key.</summary>
    public Guid TripEventId { get; set; }

    /// <summary>The <see cref="Trip"/> whose status changed.</summary>
    public Guid TripId { get; set; }

    /// <summary>The user (typically the assigned Driver) who triggered the transition.</summary>
    public Guid RecordedByUserId { get; set; }

    /// <summary>Status before the transition; null only for the trip's very first status row.</summary>
    public TripStatus? FromStatus { get; set; }

    /// <summary>Status after the transition.</summary>
    public TripStatus ToStatus { get; set; }

    /// <summary>Optional free-text context for the transition.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional GPS latitude captured at the time of the transition.</summary>
    public decimal? SnapshotLat { get; set; }

    /// <summary>Optional GPS longitude captured at the time of the transition.</summary>
    public decimal? SnapshotLng { get; set; }

    /// <summary>Timestamp the transition occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Navigation to the trip.</summary>
    public Trip Trip { get; set; } = null!;

    /// <summary>Navigation to the user who recorded the event.</summary>
    public User RecordedByUser { get; set; } = null!;
}
