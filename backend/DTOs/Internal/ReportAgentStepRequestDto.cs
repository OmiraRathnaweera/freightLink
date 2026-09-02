using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Payload for <c>POST /internal/agent-workflow-runs/{workflowRunId}/steps</c> — mirrors
/// <c>AgentStep</c> exactly. Called once per agent per run; only Agent 1 (Planner) exists in the
/// Python agent service so far, so <c>StepNo</c> is currently always 1 and <c>AgentRole</c> is
/// currently always <c>Planner</c> in practice, but every <c>AgentRole</c>/<c>StepNo</c> is accepted
/// here since the underlying table already supports all four steps per run.
/// </summary>
public class ReportAgentStepRequestDto
{
    /// <summary>1-based position of this step in the pipeline (mirrors <c>ck_agentstep_stepno</c>).</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "StepNo must be at least 1")]
    public int? StepNo { get; set; }

    /// <summary>Which agent produced this step.</summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentRole? AgentRole { get; set; }

    /// <summary>
    /// The step's outcome. When <c>Failed</c>, <see cref="ErrorMessage"/> must be supplied (mirrors
    /// <c>ck_agentstep_failure</c>) and — since only Agent 1 exists — flips the parent
    /// <c>AgentWorkflowRun.Status</c> to <c>Failed</c> as a safe, recorded failure (ADR-018).
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentStepStatus? Status { get; set; }

    /// <summary>JSON-encoded input the agent acted on, for the audit trail. Optional.</summary>
    public string? InputJson { get; set; }

    /// <summary>JSON-encoded output the agent produced, for the audit trail. Optional.</summary>
    public string? OutputJson { get; set; }

    /// <summary>Required when <see cref="Status"/> is <c>Failed</c>; otherwise ignored.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>How long the agent's step took, in milliseconds.</summary>
    public int? DurationMs { get; set; }

    /// <summary>When the agent started this step.</summary>
    [Required]
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the agent finished this step.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
