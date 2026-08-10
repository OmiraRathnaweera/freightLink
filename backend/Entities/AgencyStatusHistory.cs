using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// One <see cref="Entities.AgencyStatus"/> transition in an <see cref="Agency"/>'s audit trail.
/// Append-only at the database level: <c>trg_deny_mutation_agencystatushistories</c> (added in the
/// AddDatabaseConstraintsAndTriggers migration) rejects every UPDATE/DELETE against this table
/// with a raw <c>PostgresException</c> — service code must never attempt to modify or remove an
/// existing row, and should catch/translate that exception into a domain error rather than let
/// it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class AgencyStatusHistory
{
    /// <summary>Primary key.</summary>
    public Guid AgencyStatusHistoryId { get; set; }

    /// <summary>The <see cref="Agency"/> whose status changed.</summary>
    public Guid AgencyId { get; set; }

    /// <summary>The user who triggered the transition.</summary>
    public Guid ChangedByUserId { get; set; }

    /// <summary>Status before the transition; null only for the agency's very first status row.</summary>
    public AgencyStatus? FromStatus { get; set; }

    /// <summary>Status after the transition.</summary>
    public AgencyStatus ToStatus { get; set; }

    /// <summary>Optional context for the transition.</summary>
    public string? Reason { get; set; }

    /// <summary>Timestamp the transition was recorded.</summary>
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>Navigation to the agency.</summary>
    public Agency Agency { get; set; } = null!;

    /// <summary>Navigation to the user who changed the status.</summary>
    public User ChangedByUser { get; set; } = null!;
}
