using FreightLink.Api.DTOs.Internal;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Backend wrapper around OpenRouteService — the allow-listed routing tool (<c>ToolName = 'get_route_and_eta'</c>
/// per <c>ck_toolcall_allowlist</c>) Agent 3 calls to compute real-world distance and travel time
/// between two points. Backs the internal, non-shipper-facing <c>POST /internal/routing/route-eta</c>
/// endpoint. This service performs no authentication — the caller has already passed
/// <c>InternalApiKeyAuthFilter</c>'s shared-secret check before this is invoked. Mirrors
/// <see cref="IPricingEstimatorService"/>'s "internal, no controller-level auth" shape.
///
/// <para>
/// <b>Used for two different legs by the caller — this service has no concept of which:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Agency yard → load pickup — up to 5 calls per workflow run, one per Agent 2's top-5 shortlist, used for real candidate ranking.</description></item>
/// <item><description>Load pickup → load dropoff — exactly 1 call per run, for the #1 candidate only, feeding <c>distanceKm</c> into <c>POST /internal/pricing/estimate</c> (per the ADR-015 addendum: pricing uses the cargo leg, not the yard-positioning leg).</description></item>
/// </list>
///
/// <para>
/// <b>Not yet implemented:</b> the real OpenRouteService HTTP call, and its "one retry on timeout,
/// then return a <c>Success = false</c> result rather than throwing" reliability rule. A failed
/// lookup is a normal, in-band result (see <see cref="RouteEtaResponseDto"/>), not an exception —
/// the real implementation must not let a timeout/HTTP error propagate as an unhandled 500.
/// </para>
///
/// <para>
/// <b>Deliberately not implemented here either:</b> whether/where a <c>ToolCall</c> audit row gets
/// written for each call. The ticket explicitly leaves this open pending coordination with the
/// workflow-wiring ticket (Y3S01-65) — attributed to Agent 3, but C# is the one physically making the
/// HTTP call, so a decision is needed on whether logging happens here or only in the final
/// <c>/workflows/match</c> response, to avoid double-logging. This service writes no <c>ToolCall</c>
/// rows until that's resolved.
/// </para>
/// </summary>
public interface IRouteService
{
    /// <summary>
    /// Computes the routed distance and estimated travel time between two coordinates via
    /// OpenRouteService.
    /// </summary>
    /// <param name="request">The origin and destination coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The distance/ETA on success, or a <c>Success = false</c> result with no distance/ETA after
    /// one retry fails — never throws for an OpenRouteService-side failure.
    /// </returns>
    Task<RouteEtaResponseDto> GetRouteEtaAsync(RouteEtaRequestDto request, CancellationToken cancellationToken = default);
}