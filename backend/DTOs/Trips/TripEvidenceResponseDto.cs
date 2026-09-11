namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// One <c>TripEvidence</c> row — a captured proof-of-pickup or proof-of-delivery. Never exposes the
/// <c>TripEvidence</c> entity directly.
/// </summary>
public class TripEvidenceResponseDto
{
    /// <summary>The evidence row's unique id.</summary>
    public Guid TripEvidenceId { get; set; }

    /// <summary>The user (typically the assigned Driver) who captured this evidence.</summary>
    public Guid CapturedByUserId { get; set; }

    /// <summary>Whether this is pickup or delivery proof (<c>"PickupProof"</c> / <c>"DeliveryProof"</c>).</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>
    /// Cloudinary public id of the captured file, as returned by the prior <c>POST /api/v1/files/single</c>
    /// call — same "reference an already-uploaded file" pattern as <c>LoadFile</c>, not a raw re-upload.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Optional GPS latitude captured at the moment of evidence capture.</summary>
    public decimal? CapturedLat { get; set; }

    /// <summary>Optional GPS longitude captured at the moment of evidence capture.</summary>
    public decimal? CapturedLng { get; set; }

    /// <summary>When the evidence was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; }
}