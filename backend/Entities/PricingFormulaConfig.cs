namespace FreightLink.Api.Entities;

/// <summary>
/// One sourced, dated snapshot of the ADR-015 pricing formula's tunable inputs
/// (<see cref="BaseFare"/>, <see cref="RatePerKg"/>, <see cref="DriverMaintenanceMarginAllowancePerKm"/>),
/// kept together as a single row since Component A's estimator always needs all three at once (see
/// ADR-019's "one coherent configuration snapshot"). Append-only, same versioning and soft-delete rules
/// as <see cref="FuelPriceRate"/>: "editing" a row means inserting a new one with a later
/// <see cref="EffectiveFrom"/>, and <c>trg_deny_delete_pricingformulaconfigs</c> backstops the
/// service-layer soft delete against a literal <c>DELETE</c>. Unlike <see cref="FuelPriceRate"/>/
/// <see cref="VehicleClassEfficiency"/>, there is no per-key dimension here — at most one row is ever
/// "current" at a time.
/// </summary>
public class PricingFormulaConfig
{
    /// <summary>Primary key.</summary>
    public Guid PricingFormulaConfigId { get; set; }

    /// <summary>Flat base fare added to every estimate, in the project's base currency (LKR).</summary>
    public decimal BaseFare { get; set; }

    /// <summary>Per-kilogram rate added to every estimate, in the project's base currency (LKR/kg).</summary>
    public decimal RatePerKg { get; set; }

    /// <summary>
    /// Driver/maintenance/margin allowance added on top of the pure fuel-cost component of
    /// <c>ratePerKm</c>, in the project's base currency (LKR/km).
    /// </summary>
    public decimal DriverMaintenanceMarginAllowancePerKm { get; set; }

    /// <summary>Citation for where these figures came from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this configuration took/takes effect; the current configuration is the latest non-deleted row's.</summary>
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
