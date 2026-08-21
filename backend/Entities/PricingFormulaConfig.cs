namespace FreightLink.Api.Entities;

/// <summary>
/// One sourced, dated snapshot of the ADR-015 pricing formula's remaining tunable constants
/// (everything not already covered by <see cref="FuelPriceRate"/>/<see cref="VehicleClassEfficiency"/>),
/// used by the internal price estimator. Append-only, same pattern as <see cref="FuelPriceRate"/>:
/// "editing" a value means inserting a new row with a later <see cref="EffectiveFrom"/> — existing
/// rows are never mutated except by the service layer setting <see cref="DeletedAt"/>/
/// <see cref="DeletedByUserId"/> on soft delete. <c>trg_deny_delete_pricingformulaconfigs</c> is a
/// database-level backstop against a literal <c>DELETE</c>, not the deletion mechanism itself —
/// service code must always soft-delete via an <c>UPDATE</c>.
/// </summary>
public class PricingFormulaConfig
{
    /// <summary>Primary key.</summary>
    public Guid PricingFormulaConfigId { get; set; }

    /// <summary>Flat fee added to every estimate regardless of distance or weight.</summary>
    public decimal BaseFare { get; set; }

    /// <summary>Per-kilogram rate applied to the load's weight.</summary>
    public decimal RatePerKg { get; set; }

    /// <summary>Per-kilometre driver cost allowance, added into the estimator's <c>ratePerKm</c>.</summary>
    public decimal DriverCostPerKm { get; set; }

    /// <summary>Per-kilometre vehicle maintenance allowance, added into the estimator's <c>ratePerKm</c>.</summary>
    public decimal MaintenanceAllowancePerKm { get; set; }

    /// <summary>
    /// Profit margin applied to <c>ratePerKm</c> as a fraction, not a whole percent — e.g. <c>0.15</c>
    /// means 15%, applied as <c>ratePerKm * (1 + MarginPercent)</c>.
    /// </summary>
    public decimal MarginPercent { get; set; }

    /// <summary>Citation for where these values came from, e.g. "Cost-based derivation, ADR-019 methodology, dated".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this configuration took effect; the current configuration is the latest non-deleted row's.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The Admin who recorded this configuration.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the row was last updated (soft delete is the only expected update).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null while the row is live.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The Admin who soft-deleted this row; null while the row is live.</summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>Navigation to the user who recorded this configuration.</summary>
    public User SetByUser { get; set; } = null!;

    /// <summary>Navigation to the user who soft-deleted this row, if any.</summary>
    public User? DeletedByUser { get; set; }
}
