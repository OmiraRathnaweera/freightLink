namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Summary item for the job proposal inbox list (GET /api/v1/assignments).
/// </summary>
public class AssignmentListItemDto
{
    public Guid AssignmentId { get; set; }
    public Guid LoadId { get; set; }
    public Guid AgencyId { get; set; }
    public string? AgencyName { get; set; }
    public decimal ProposedPrice { get; set; }
    public decimal? RoutedDistanceKm { get; set; }
    public int? ProposedEtaMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CargoDescription { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? VolumeM3 { get; set; }
    public string? PickupAddress { get; set; }
    public string? DropoffAddress { get; set; }
    public DateTimeOffset? PickupWindowStart { get; set; }
    public DateTimeOffset? PickupWindowEnd { get; set; }
    public string? ShipperName { get; set; }
    public string? ReferenceCode { get; set; }
    public Guid? TripId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
