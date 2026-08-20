using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// Payload for <c>POST /api/v1/admin/pricing/formula-config</c> — always inserts a new, current
/// <see cref="Entities.PricingFormulaConfig"/> row (append-only versioning, same as
/// <c>FuelPriceRate</c>/<c>VehicleClassEfficiency</c>); never updates an existing row's values.
/// Carries no <c>SetByUserId</c> — that comes from the authenticated Admin's access-token claims in
/// the controller, never from client input.
/// </summary>
public class CreatePricingFormulaConfigDto
{
    /// <summary>
    /// Flat fee added to every estimate. Nullable so <see cref="RequiredAttribute"/> actually rejects
    /// an omitted JSON field instead of silently binding it to <c>0</c> — unlike <c>PricePerLitre</c>,
    /// <c>0</c> is a legitimate value here, so a non-nullable field would let an omitted field pass
    /// validation unnoticed (mirrors the same nullable-trick pattern used on
    /// <c>CreateVehicleClassEfficiencyDto.MinPayloadKg</c>).
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "BaseFare must be zero or greater")]
    public decimal? BaseFare { get; set; }

    /// <summary>Per-kilogram rate applied to the load's weight. See <see cref="BaseFare"/> for why this is nullable.</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "RatePerKg must be zero or greater")]
    public decimal? RatePerKg { get; set; }

    /// <summary>Per-kilometre driver cost allowance. See <see cref="BaseFare"/> for why this is nullable.</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "DriverCostPerKm must be zero or greater")]
    public decimal? DriverCostPerKm { get; set; }

    /// <summary>Per-kilometre vehicle maintenance allowance. See <see cref="BaseFare"/> for why this is nullable.</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "MaintenanceAllowancePerKm must be zero or greater")]
    public decimal? MaintenanceAllowancePerKm { get; set; }

    /// <summary>
    /// Profit margin as a fraction (e.g. <c>0.15</c> for 15%), not a whole percent. See
    /// <see cref="BaseFare"/> for why this is nullable.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "MarginPercent must be zero or greater")]
    public decimal? MarginPercent { get; set; }

    /// <summary>Citation for where these values came from, e.g. "Cost-based derivation, ADR-019 methodology, dated".</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Source { get; set; } = string.Empty;

    /// <summary>When this configuration takes effect. The current configuration is the latest non-deleted row's.</summary>
    [Required]
    public DateTimeOffset EffectiveFrom { get; set; }
}
