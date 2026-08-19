using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common;

/// <summary>
/// Shared constants for Component A's automatic price estimation (ADR-015/ADR-019). Only
/// <see cref="EstimatorFuelType"/> lives here — it's a structural choice (which fuel type the estimator
/// always prices against), not a tunable market number. The formula's actual tunable inputs
/// (<c>BaseFare</c>, <c>RatePerKg</c>, <c>DriverMaintenanceMarginAllowancePerKm</c>) are Admin-managed,
/// versioned, and audited via <see cref="Entities.PricingFormulaConfig"/> instead of hardcoded here — the
/// same sourced/dated pattern already used for <see cref="Entities.FuelPriceRate"/>.
/// </summary>
public static class PricingConstants
{
    /// <summary>The fuel type Component A's estimate always prices against.</summary>
    public const FuelType EstimatorFuelType = FuelType.AutoDiesel;
}
