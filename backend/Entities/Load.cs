using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Load
{
    public Guid LoadId { get; set; }
    public Guid ShipperUserId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public decimal PickupLat { get; set; }
    public decimal PickupLng { get; set; }
    public string DropoffAddress { get; set; } = string.Empty;
    public decimal DropoffLat { get; set; }
    public decimal DropoffLng { get; set; }
    public DateTimeOffset PickupWindowStart { get; set; }
    public DateTimeOffset PickupWindowEnd { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public LoadStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User ShipperUser { get; set; } = null!;
    public ICollection<LoadStatusHistory> StatusHistory { get; set; } = new List<LoadStatusHistory>();
    public ICollection<LoadFile> Files { get; set; } = new List<LoadFile>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<AgentWorkflowRun> WorkflowRuns { get; set; } = new List<AgentWorkflowRun>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
