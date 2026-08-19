namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// The three pricing-config pieces Component A's estimator needs, read together as one atomic snapshot
/// (see <see cref="Services.Interfaces.IPricingConfigService.GetPricingSnapshotForEstimate"/>) so no
/// concurrent Admin write can land between the individual reads and leave the formula computed from a
/// torn combination of old/new config.
/// </summary>
public class PricingSnapshotDto
{
    /// <summary>The vehicle-class tier matching the load's weight and volume.</summary>
    public VehicleClassEfficiencyResponseDto Tier { get; set; } = null!;

    /// <summary>The current fuel price for <see cref="Common.PricingConstants.EstimatorFuelType"/>.</summary>
    public FuelPriceRateResponseDto FuelPrice { get; set; } = null!;

    /// <summary>The current formula-constant configuration.</summary>
    public PricingFormulaConfigResponseDto FormulaConfig { get; set; } = null!;
}
