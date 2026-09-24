using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Payload for <c>POST /internal/agent-workflow-runs</c> — called by the Agentic AI pipeline's
/// Agent 1 (Planner) to create its own <c>AgentWorkflowRun</c> row and obtain the real
/// <c>WorkflowRunId</c> it reports every subsequent step against. Never called by a shipper or any
/// public client.
/// </summary>
public class CreateAgentWorkflowRunRequestDto
{
    /// <summary>
    /// The load this run is matching an agency for. Nullable so <see cref="RequiredAttribute"/>
    /// actually rejects an omitted JSON field instead of silently binding it to
    /// <see cref="Guid.Empty"/> (mirrors the same nullable-trick pattern used elsewhere in this
    /// project for required identifiers/values).
    /// </summary>
    [Required]
    public Guid? LoadId { get; set; }

    /// <summary>The user (typically the shipper) whose action triggered this run.</summary>
    [Required]
    public Guid? TriggeredByUserId { get; set; }

    /// <summary>
    /// 1 for the load's first match attempt; incremented on each automatic retry after an agency
    /// decline (ADR-018). Must be unique per <c>LoadId</c> (mirrors <c>uq_awr_load_attempt</c>).
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "AttemptNo must be at least 1")]
    public int? AttemptNo { get; set; }
}
