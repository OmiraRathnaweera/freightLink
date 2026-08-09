using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class DisputeResolution
{
    public Guid DisputeId { get; set; }
    public Guid ResolvedByUserId { get; set; }
    public DisputeOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset ResolvedAt { get; set; }

    public Dispute Dispute { get; set; } = null!;
    public User ResolvedByUser { get; set; } = null!;
}
