using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Request payload for <c>POST /internal/routing/route-eta</c>. Deliberately leg-agnostic — the
/// caller (Agent 3) reuses this same shape for both the yard→pickup leg (up to 5 calls, one per
/// Agent 2's top-5 shortlist) and the pickup→dropoff leg (1 call, #1 candidate only, feeds
/// <c>distanceKm</c> into <c>POST /internal/pricing/estimate</c> per the ADR-015 addendum). This
/// endpoint has no concept of "which leg" — that distinction exists only in how the caller uses the
/// result, not in this request/response contract.
/// </summary>
public class RouteEtaRequestDto
{
    /// <summary>
    /// Origin latitude. Nullable so <see cref="RequiredAttribute"/> actually rejects an omitted
    /// JSON field instead of silently binding it to <c>0</c> (mirrors <see cref="Loads.CreateLoadDto"/>'s
    /// coordinate fields).
    /// </summary>
    [Required]
    [Range(-90, 90)]
    public decimal? OriginLat { get; set; }

    /// <summary>Origin longitude.</summary>
    [Required]
    [Range(-180, 180)]
    public decimal? OriginLng { get; set; }

    /// <summary>Destination latitude.</summary>
    [Required]
    [Range(-90, 90)]
    public decimal? DestinationLat { get; set; }

    /// <summary>Destination longitude.</summary>
    [Required]
    [Range(-180, 180)]
    public decimal? DestinationLng { get; set; }
}