using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Response for the Shipper-facing <c>POST /api/v1/loads/{id}/estimate</c> — a rough, pre-matching
/// price quote using the straight-line (haversine) distance between the load's pickup and dropoff
/// coordinates, per the Component A price-estimation contract. This is a preview only: unlike the
/// internal <c>POST /internal/pricing/estimate</c> (used by Agent 3 with the real routed distance),
/// nothing here is persisted onto <c>Load.EstimatedPrice</c> — that column remains the AI agent's
/// own computed price, so this endpoint cannot be confused with or overwrite it.
/// </summary>
public class LoadPriceEstimateResponseDto
{
    /// <summary>The load this estimate was computed for.</summary>
    public Guid LoadId { get; set; }

    /// <summary>The computed price: <c>baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)</c>.</summary>
    public decimal EstimatedPrice { get; set; }

    /// <summary>The straight-line (haversine) distance between the load's pickup and dropoff coordinates.</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>The vehicle-class tier resolved from the load's weight/volume, used to derive <see cref="RatePerKm"/>.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleClass VehicleClass { get; set; }

    /// <summary>
    /// The per-kilometre rate used, after combining fuel cost, driver cost, and maintenance
    /// allowance, and applying the configured margin.
    /// </summary>
    public decimal RatePerKm { get; set; }

    /// <summary>The per-kilogram rate used, taken directly from the current pricing formula configuration.</summary>
    public decimal RatePerKg { get; set; }

    /// <summary>The flat base fare used, taken directly from the current pricing formula configuration.</summary>
    public decimal BaseFare { get; set; }
}
