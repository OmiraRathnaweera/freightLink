using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Response for <c>POST /internal/pricing/estimate</c> — the computed price plus the breakdown used
/// to derive it. Only <c>estimatedPrice</c> is persisted, onto the existing <c>Load.EstimatedPrice</c>
/// column; every other field here is response-only, not stored anywhere.
/// </summary>
public class PricingEstimateResponseDto
{
    /// <summary>The load this estimate was computed for.</summary>
    public Guid LoadId { get; set; }

    /// <summary>The computed price, per ADR-015: <c>baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)</c>. Also written to <c>Load.EstimatedPrice</c>.</summary>
    public decimal EstimatedPrice { get; set; }

    /// <summary>The distance used for this estimate, echoed back from the request.</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>The vehicle class used for this estimate, echoed back from the request.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleClass VehicleClass { get; set; }

    /// <summary>
    /// The per-kilometre rate actually used, after combining fuel cost, driver cost, and maintenance
    /// allowance, and applying the configured margin.
    /// </summary>
    public decimal RatePerKm { get; set; }

    /// <summary>The per-kilogram rate used, taken directly from the current pricing formula configuration.</summary>
    public decimal RatePerKg { get; set; }

    /// <summary>The flat base fare used, taken directly from the current pricing formula configuration.</summary>
    public decimal BaseFare { get; set; }
}
