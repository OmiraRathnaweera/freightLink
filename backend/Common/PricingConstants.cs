using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common;

/// <summary>
/// Shared constants for Component A's automatic price estimation (ADR-015/ADR-019). <see cref="BaseFare"/>,
/// <see cref="RatePerKg"/>, and <see cref="DriverMaintenanceMarginAllowancePerKm"/> are placeholder
/// figures — no authoritative Sri Lankan freight-pricing value exists anywhere in this repo or its ADRs
/// (ADR-015 explicitly defers them as "an implementation/tuning detail, not an architectural decision").
/// Tune these to real figures before relying on the computed <c>estimatedPrice</c> for anything beyond
/// a placeholder demo value.
/// </summary>
public static class PricingConstants
{
    /// <summary>Flat base fare added to every estimate, in the project's base currency (LKR). Placeholder.</summary>
    public const decimal BaseFare = 500m;

    /// <summary>Per-kilogram rate added to every estimate, in the project's base currency (LKR/kg). Placeholder — ADR-019 only tiers the distance-based rate.</summary>
    public const decimal RatePerKg = 10m;

    /// <summary>
    /// Driver/maintenance/margin allowance added on top of the pure fuel-cost component of
    /// <c>ratePerKm</c>, in the project's base currency (LKR/km). Placeholder.
    /// </summary>
    public const decimal DriverMaintenanceMarginAllowancePerKm = 50m;

    /// <summary>The fuel type Component A's estimate always prices against.</summary>
    public const FuelType EstimatorFuelType = FuelType.AutoDiesel;
}
