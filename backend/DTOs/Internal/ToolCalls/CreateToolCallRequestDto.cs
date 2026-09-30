using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Internal.ToolCalls;

/// <summary>
/// Payload for recording one ToolCall audit row via POST /internal/agent-workflow-runs/{workflowRunId}/tool-calls.
/// </summary>
public class CreateToolCallRequestDto
{
    /// <summary>
    /// The parent agent step id. Optional: if not supplied, the backend automatically resolves
    /// or associates it with the run's MatchingPricing step (Agent 3).
    /// </summary>
    public Guid? AgentStepId { get; set; }

    /// <summary>Which allow-listed tool was invoked (e.g. get_route_and_eta).</summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToolName? ToolName { get; set; }

    /// <summary>1-based attempt number for this (AgentStep, ToolName) pair.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "AttemptNo must be at least 1.")]
    public int AttemptNo { get; set; } = 1;

    /// <summary>Raw JSON request payload sent to the tool.</summary>
    public string? RequestJson { get; set; }

    /// <summary>Raw JSON response payload received from the tool.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>Whether the tool call succeeded.</summary>
    public bool Success { get; set; } = true;

    /// <summary>HTTP status code returned by the tool call, if applicable.</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>Duration of the tool call in milliseconds.</summary>
    public int? DurationMs { get; set; }

    /// <summary>Error message, required when Success is false (enforced by ck_toolcall_failure).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Timestamp the call was made.</summary>
    public DateTimeOffset? CalledAt { get; set; }
}