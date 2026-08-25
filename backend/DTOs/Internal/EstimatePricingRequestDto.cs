using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Payload for <c>POST /internal/pricing/estimate</c> — called by the Agentic AI pipeline's Agent 3
/// after it has already selected a candidate agency/vehicle class and called <c>get_route_and_eta</c>
/// for a real, ORS-routed distance. Never called by a shipper or through <c>LoadService</c>.
/// </summary>
public class EstimatePricingRequestDto
{
    /// <summary>
    /// The load being priced. Nullable so <see cref="RequiredAttribute"/> actually rejects an omitted
    /// JSON field instead of silently binding it to <see cref="Guid.Empty"/> (mirrors the same
    /// nullable-trick pattern used elsewhere in this project for required identifiers/values).
    /// </summary>
    [Required]
    public Guid? LoadId { get; set; }

    /// <summary>
    /// The vehicle class Agent 3 has already selected for this job. Supplied by the caller — this
    /// endpoint does not infer a class from <c>Load.WeightKg</c>/<c>VolumeM3</c>.
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleClass? SuggestedVehicleClass { get; set; }

    /// <summary>
    /// The real, ORS-routed distance for the cargo leg (pickup → dropoff), in kilometres, from the
    /// caller's own <c>get_route_and_eta</c> tool call. This endpoint does not compute distance itself.
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "DistanceKm must be greater than zero")]
    public decimal? DistanceKm { get; set; }
}
