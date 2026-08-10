using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// Proof-of-pickup or proof-of-delivery captured for a <see cref="Trip"/>. A trip may have at most
/// one row per <see cref="EvidenceType"/> (enforced by uq_tripevidence_type), and
/// <c>fn_require_trip_evidence</c> (also added in the AddDatabaseConstraintsAndTriggers migration)
/// blocks <see cref="Entities.TripStatus"/> transitioning to PickedUp/Delivered unless the matching
/// evidence row already exists.
/// Append-only at the database level: <c>trg_deny_mutation_tripevidences</c> rejects every
/// UPDATE/DELETE against this table with a raw <c>PostgresException</c> — service code must never
/// attempt to modify or remove an existing row, and should catch/translate that exception into a
/// domain error rather than let it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class TripEvidence
{
    /// <summary>Primary key.</summary>
    public Guid TripEvidenceId { get; set; }

    /// <summary>The <see cref="Trip"/> this evidence was captured for.</summary>
    public Guid TripId { get; set; }

    /// <summary>The user (typically the assigned Driver) who captured the evidence.</summary>
    public Guid CapturedByUserId { get; set; }

    /// <summary>Whether this is pickup or delivery proof.</summary>
    public EvidenceType EvidenceType { get; set; }

    /// <summary>Storage key of the captured file (photo/signature/etc.), unique per row.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Optional GPS latitude captured at the moment of evidence capture.</summary>
    public decimal? CapturedLat { get; set; }

    /// <summary>Optional GPS longitude captured at the moment of evidence capture.</summary>
    public decimal? CapturedLng { get; set; }

    /// <summary>Timestamp the evidence was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; }

    /// <summary>Navigation to the trip.</summary>
    public Trip Trip { get; set; } = null!;

    /// <summary>Navigation to the user who captured the evidence.</summary>
    public User CapturedByUser { get; set; } = null!;
}
