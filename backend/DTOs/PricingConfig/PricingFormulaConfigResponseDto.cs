namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>Wire-facing representation of a <see cref="Entities.PricingFormulaConfig"/> row.</summary>
public class PricingFormulaConfigResponseDto
{
    /// <summary>The row's id.</summary>
    public Guid PricingFormulaConfigId { get; set; }

    /// <summary>Flat fee added to every estimate.</summary>
    public decimal BaseFare { get; set; }

    /// <summary>Per-kilogram rate applied to the load's weight.</summary>
    public decimal RatePerKg { get; set; }

    /// <summary>Per-kilometre driver cost allowance.</summary>
    public decimal DriverCostPerKm { get; set; }

    /// <summary>Per-kilometre vehicle maintenance allowance.</summary>
    public decimal MaintenanceAllowancePerKm { get; set; }

    /// <summary>Profit margin as a fraction (e.g. <c>0.15</c> for 15%), not a whole percent.</summary>
    public decimal MarginPercent { get; set; }

    /// <summary>Citation for where these values came from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this configuration took/takes effect.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The id of the Admin who recorded this configuration.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>
    /// Display name (<see cref="Entities.User.FullName"/>) of the Admin who recorded this
    /// configuration. Falls back to <c>"Unknown"</c> if the setting user record could not be resolved.
    /// </summary>
    public string SetByUserName { get; set; } = string.Empty;

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null unless this row has been soft-deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The id of the Admin who soft-deleted this row; null unless this row has been soft-deleted.</summary>
    public Guid? DeletedByUserId { get; set; }
}
