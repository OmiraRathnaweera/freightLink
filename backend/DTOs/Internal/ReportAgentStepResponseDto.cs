using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Internal;

/// <summary>Response for <c>POST /internal/agent-workflow-runs/{workflowRunId}/steps</c>.</summary>
public class ReportAgentStepResponseDto
{
    /// <summary>The newly created <c>AgentStep.AgentStepId</c>.</summary>
    public Guid AgentStepId { get; set; }

    /// <summary>Echoes the reported step's status back to the caller.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentStepStatus Status { get; set; }
}
