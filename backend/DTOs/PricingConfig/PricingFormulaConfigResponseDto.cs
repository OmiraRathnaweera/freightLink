namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>Wire-facing representation of a <see cref="Entities.PricingFormulaConfig"/> row.</summary>
public class PricingFormulaConfigResponseDto
{
    /// <summary>The row's id.</summary>
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

    /// <summary>When this configuration took/takes effect.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The id of the Admin who recorded this configuration.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>
    /// Display name (<see cref="Entities.User.FullName"/>) of the Admin who recorded this configuration.
    /// Falls back to <c>"Unknown"</c> if the setting user record could not be resolved.
    /// </summary>
    public string SetByUserName { get; set; } = string.Empty;

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null unless this row has been soft-deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The id of the Admin who soft-deleted this row; null unless this row has been soft-deleted.</summary>
    public Guid? DeletedByUserId { get; set; }
}
