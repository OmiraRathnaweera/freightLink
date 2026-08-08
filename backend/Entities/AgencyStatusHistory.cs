using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class AgencyStatusHistory
{
    public Guid AgencyStatusHistoryId { get; set; }
    public Guid AgencyId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public AgencyStatus? FromStatus { get; set; }
    public AgencyStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    public Agency Agency { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
