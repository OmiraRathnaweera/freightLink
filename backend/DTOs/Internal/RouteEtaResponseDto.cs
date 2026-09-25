namespace FreightLink.Api.DTOs.Internal;

/// <summary>
/// Response payload for <c>POST /internal/routing/route-eta</c>. Per the ticket's reliability rule,
/// a failed OpenRouteService call (after one retry) is a normal, in-band <c>Success = false</c>
/// result with no distance/ETA — never an HTTP error response. <see cref="DistanceKm"/>/
/// <see cref="EtaMinutes"/> are nullable specifically so a failure genuinely carries no distance
/// value at all, rather than a misleading <c>0</c> — Agent 3 must never estimate a distance it
/// doesn't actually have (per the ticket: "drop this candidate," not "assume zero distance").
/// </summary>
public class RouteEtaResponseDto
{
    /// <summary>The routed distance in kilometres, or <see langword="null"/> if <see cref="Success"/> is <see langword="false"/>.</summary>
    public decimal? DistanceKm { get; set; }

    /// <summary>The estimated travel time in minutes, or <see langword="null"/> if <see cref="Success"/> is <see langword="false"/>.</summary>
    public int? EtaMinutes { get; set; }

    /// <summary>
    /// Whether the OpenRouteService lookup succeeded. <see langword="false"/> after one retry on
    /// timeout/failure — the caller (Agent 3) must treat this as "drop this candidate", never as a
    /// prompt to guess or fall back to an estimated distance.
    /// </summary>
    public bool Success { get; set; }
}