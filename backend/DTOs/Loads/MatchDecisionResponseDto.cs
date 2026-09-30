namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Response for a Shipper reject/revise decision on a load's match recommendation.
/// </summary>
public class MatchDecisionResponseDto
{
    public Guid WorkflowRunId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
