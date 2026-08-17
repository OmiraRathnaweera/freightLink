using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// One sourced, dated fuel price used as pricing-formula input (see ADR-019). Append-only:
/// "editing" a price means inserting a new row with a later <see cref="EffectiveFrom"/> — existing
/// rows are never mutated except by the service layer setting <see cref="DeletedAt"/>/
/// <see cref="DeletedByUserId"/> on soft delete. <c>trg_deny_delete_fuelpricerates</c> (added in the
/// AddPricingConfiguration migration) is a database-level backstop against a literal <c>DELETE</c>,
/// not the deletion mechanism itself — service code must always soft-delete via an <c>UPDATE</c>.
/// </summary>
public class FuelPriceRate
{
    /// <summary>Primary key.</summary>
    public Guid FuelPriceRateId { get; set; }

    /// <summary>The fuel grade this row prices.</summary>
    public FuelType FuelType { get; set; }

    /// <summary>Price per litre, in the project's base currency.</summary>
    public decimal PricePerLitre { get; set; }

    /// <summary>Citation for where this price came from, e.g. "CPC official price list, ceypetco.gov.lk, Aug 2026".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this price took effect; the current price is the latest non-deleted row's.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The Admin who recorded this rate.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the row was last updated (soft delete is the only expected update).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null while the row is live.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The Admin who soft-deleted this row; null while the row is live.</summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>Navigation to the user who recorded this rate.</summary>
    public User SetByUser { get; set; } = null!;

    /// <summary>Navigation to the user who soft-deleted this row, if any.</summary>
    public User? DeletedByUser { get; set; }
}
