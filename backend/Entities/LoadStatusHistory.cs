using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// One <see cref="Entities.LoadStatus"/> transition in a <see cref="Load"/>'s audit trail.
/// Append-only at the database level: <c>trg_deny_mutation_loadstatushistories</c> (added in the
/// AddDatabaseConstraintsAndTriggers migration) rejects every UPDATE/DELETE against this table
/// with a raw <c>PostgresException</c> — service code must never attempt to modify or remove an
/// existing row, and should catch/translate that exception into a domain error rather than let
/// it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class LoadStatusHistory
{
    /// <summary>Primary key.</summary>
    public Guid LoadStatusHistoryId { get; set; }

    /// <summary>The <see cref="Load"/> whose status changed.</summary>
    public Guid LoadId { get; set; }

    /// <summary>The user who triggered the transition.</summary>
    public Guid ChangedByUserId { get; set; }

    /// <summary>Status before the transition; null only for the load's very first status row.</summary>
    public LoadStatus? FromStatus { get; set; }

    /// <summary>Status after the transition.</summary>
    public LoadStatus ToStatus { get; set; }

    /// <summary>Required when <see cref="ToStatus"/> is Cancelled (enforced by ck_lsh_cancel_reason).</summary>
    public string? Reason { get; set; }

    /// <summary>Timestamp the transition was recorded.</summary>
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>Navigation to the load.</summary>
    public Load Load { get; set; } = null!;

    /// <summary>Navigation to the user who changed the status.</summary>
    public User ChangedByUser { get; set; } = null!;
}
