using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// A single human approve/reject/revise decision recorded against an <see cref="AgentWorkflowRun"/>.
/// Append-only at the database level: <c>trg_deny_mutation_approvaldecisions</c> (added in the
/// AddDatabaseConstraintsAndTriggers migration) rejects every UPDATE/DELETE against this table
/// with a raw <c>PostgresException</c> — service code must never attempt to modify or remove an
/// existing row, and should catch/translate that exception into a domain error rather than let
/// it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class ApprovalDecision
{
    /// <summary>Primary key.</summary>
    public Guid ApprovalDecisionId { get; set; }

    /// <summary>The <see cref="AgentWorkflowRun"/> this decision belongs to.</summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>The user (Shipper, per ADR-016 ownership rules) who made the decision.</summary>
    public Guid DecidedByUserId { get; set; }

    /// <summary>1-based position of this decision within its workflow run's approval sequence.</summary>
    public int SequenceNo { get; set; }

    /// <summary>The decision made (Approve/Reject/Revise).</summary>
    public ApprovalDecisionType Decision { get; set; }

    /// <summary>Required unless <see cref="Decision"/> is Approve (enforced by ck_ad_reason).</summary>
    public string? Reason { get; set; }

    /// <summary>Timestamp the decision was recorded.</summary>
    public DateTimeOffset DecidedAt { get; set; }

    /// <summary>Navigation to the parent workflow run.</summary>
    public AgentWorkflowRun WorkflowRun { get; set; } = null!;

    /// <summary>Navigation to the deciding user.</summary>
    public User DecidedByUser { get; set; } = null!;
}
