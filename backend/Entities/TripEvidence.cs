using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class TripEvidence
{
    public Guid TripEvidenceId { get; set; }
    public Guid TripId { get; set; }
    public Guid CapturedByUserId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public decimal? CapturedLat { get; set; }
    public decimal? CapturedLng { get; set; }
    public DateTimeOffset CapturedAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public User CapturedByUser { get; set; } = null!;
}
