namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// A single, internally-consistent read of every current pricing-config row the internal price
/// estimator needs, taken atomically under <c>PricingConfigService</c>'s write lock so no Admin
/// write can land between the three individual reads and produce a mixed-version estimate (e.g. a
/// new fuel price combined with a stale formula configuration).
/// </summary>
public class PricingSnapshotDto
{
    /// <summary>The current efficiency figure for the requested vehicle class.</summary>
    public VehicleClassEfficiencyResponseDto Efficiency { get; set; } = null!;

    /// <summary>The current fuel price for the requested fuel type.</summary>
    public FuelPriceRateResponseDto FuelPrice { get; set; } = null!;

    /// <summary>The current pricing formula configuration.</summary>
    public PricingFormulaConfigResponseDto FormulaConfig { get; set; } = null!;
}
