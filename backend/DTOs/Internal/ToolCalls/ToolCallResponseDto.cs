using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Internal.ToolCalls;

/// <summary>
/// Response for POST /internal/agent-workflow-runs/{workflowRunId}/tool-calls.
/// </summary>
public class ToolCallResponseDto
{
    public Guid ToolCallId { get; set; }
    public Guid AgentStepId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToolName ToolName { get; set; }

    public int AttemptNo { get; set; }
    public bool Success { get; set; }
    public int? HttpStatusCode { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CalledAt { get; set; }
}