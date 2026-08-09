using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class LoadStatusHistory
{
    public Guid LoadStatusHistoryId { get; set; }
    public Guid LoadId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public LoadStatus? FromStatus { get; set; }
    public LoadStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    public Load Load { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
