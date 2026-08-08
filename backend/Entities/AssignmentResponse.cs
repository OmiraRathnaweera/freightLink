using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class AssignmentResponse
{
    public Guid AssignmentId { get; set; }
    public Guid RespondedByUserId { get; set; }
    public AssignmentResponseType Response { get; set; }
    public string? DeclineReason { get; set; }
    public DateTimeOffset RespondedAt { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public User RespondedByUser { get; set; } = null!;
}
