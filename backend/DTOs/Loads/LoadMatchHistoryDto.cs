namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Full agent call history for a load (GET /api/v1/loads/{loadId}/match/history) - every
/// AgentWorkflowRun attempt ever made for it (not just the latest, unlike
/// <see cref="LoadMatchRecommendationDto"/>), each with its own 4 agent steps and every
/// tool call (get_route_and_eta / estimate_price) made during them.
/// </summary>
public class LoadMatchHistoryDto
{
    public Guid LoadId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public List<AgentWorkflowRunHistoryItemDto> Attempts { get; set; } = new();
}

public class AgentWorkflowRunHistoryItemDto
{
    public Guid WorkflowRunId { get; set; }
    public int AttemptNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Objective { get; set; }
    public string? ShipperMessage { get; set; }
    public Guid? SelectedAgencyId { get; set; }
    public string? SelectedAgencyName { get; set; }
    public decimal? ProposedPrice { get; set; }
    /// <summary>The Shipper's decision on this attempt (Approve/Reject/Revise), if any was recorded yet.</summary>
    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public List<WorkflowStepHistoryDto> Steps { get; set; } = new();
}

public class WorkflowStepHistoryDto
{
    public int StepNo { get; set; }
    public string AgentRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public List<ToolCallHistoryDto> ToolCalls { get; set; } = new();
}

public class ToolCallHistoryDto
{
    public string ToolName { get; set; } = string.Empty;
    public int AttemptNo { get; set; }
    public bool Success { get; set; }
    public int? DurationMs { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CalledAt { get; set; }
}
