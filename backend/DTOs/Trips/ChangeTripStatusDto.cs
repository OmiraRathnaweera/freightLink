using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Payload for <c>POST /api/v1/trips/{id}/status</c> — advances a trip's status
/// (<c>Assigned → PickedUp → InTransit → Delivered</c>, or → <c>Cancelled</c>). Per ADR-004 and the
/// <c>fn_require_trip_evidence</c> database trigger, a transition to <c>PickedUp</c> or
/// <c>Delivered</c> is hard-blocked at the database level unless the matching <c>TripEvidence</c> row
/// already exists — the (not-yet-implemented) service layer must catch that trigger's
/// <c>PostgresException</c> and translate it into a proper <c>ApiException</c>/<c>ErrorCode</c>
/// rather than let it surface as an unhandled 500.
/// </summary>
public class ChangeTripStatusDto
{
    /// <summary>
    /// The status to transition the trip to. Nullable so <see cref="RequiredAttribute"/> actually
    /// rejects an omitted JSON field instead of silently binding it to the enum's default member
    /// (mirrors <see cref="Loads.ChangeLoadStatusDto.Status"/>'s reasoning). Uses the plain
    /// <see cref="JsonStringEnumConverter"/>, matching <c>ChangeLoadStatusDto.Status</c>'s existing
    /// precedent exactly — not the hardened converter used on <c>UploadTripEvidenceDto.EvidenceType</c>,
    /// since this field's closest analog (Load's own status-change field) was left unhardened too.
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TripStatus? TargetStatus { get; set; }

    /// <summary>Optional free-text context for the transition (e.g. a delay reason).</summary>
    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>Optional GPS latitude captured at the moment of this transition.</summary>
    [Range(-90, 90)]
    public decimal? SnapshotLat { get; set; }

    /// <summary>Optional GPS longitude captured at the moment of this transition.</summary>
    [Range(-180, 180)]
    public decimal? SnapshotLng { get; set; }
}