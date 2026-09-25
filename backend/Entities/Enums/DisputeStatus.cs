namespace FreightLink.Api.Entities.Enums;

/// <summary>
/// Status of a dispute following the strict lifecycle state machine:
/// Raised -> UnderReview -> Resolved.
/// </summary>
public enum DisputeStatus
{
    Raised = 0,
    Open = 0, // Backward compatibility alias
    UnderReview = 1,
    Resolved = 2,
    Rejected = 3
}
