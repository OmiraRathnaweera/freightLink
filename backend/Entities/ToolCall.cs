using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// One invocation attempt of an allow-listed tool (see <see cref="Entities.Enums.ToolName"/> and
/// ck_toolcall_allowlist) made during an <see cref="AgentStep"/>.
/// Append-only at the database level: <c>trg_deny_mutation_toolcalls</c> (added in the
/// AddDatabaseConstraintsAndTriggers migration) rejects every UPDATE/DELETE against this table
/// with a raw <c>PostgresException</c> — service code must never attempt to modify or remove an
/// existing row, and should catch/translate that exception into a domain error rather than let
/// it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class ToolCall
{
    /// <summary>Primary key.</summary>
    public Guid ToolCallId { get; set; }

    /// <summary>The <see cref="AgentStep"/> this call was made during.</summary>
    public Guid AgentStepId { get; set; }

    /// <summary>Which allow-listed tool was invoked.</summary>
    public ToolName ToolName { get; set; }

    /// <summary>1-based attempt number for this (AgentStep, ToolName) pair.</summary>
    public int AttemptNo { get; set; }

    /// <summary>Raw JSON request payload sent to the tool, if captured.</summary>
    public string? RequestJson { get; set; }

    /// <summary>Raw JSON response payload received from the tool, if captured.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>Whether the call succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>HTTP status code returned by the tool call, if applicable.</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>How long the call took, in milliseconds.</summary>
    public int? DurationMs { get; set; }

    /// <summary>Required when <see cref="Success"/> is false (enforced by ck_toolcall_failure).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Timestamp the call was made.</summary>
    public DateTimeOffset CalledAt { get; set; }

    /// <summary>Navigation to the parent agent step.</summary>
    public AgentStep AgentStep { get; set; } = null!;
}
