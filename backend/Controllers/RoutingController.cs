using FreightLink.Api.Common.Filters;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Internal, service-to-service routing endpoint for the Agentic AI pipeline (Agent 3) — never
/// called by a shipper or any public client. Deliberately routed outside the <c>/api/v1</c> prefix,
/// same reasoning as <see cref="InternalPricingController"/>: explicitly outside the public/versioned
/// API surface (see <c>../docs/api-contract-openapi-skeleton.md</c>'s scoping note on ASP.NET↔Python
/// service calls). Guarded by <see cref="InternalApiKeyAuthFilter"/> (a shared-secret header), not
/// JWT — this has no human caller and carries no <c>[Authorize]</c> attribute.
///
/// <para>
/// <b>Skeleton status:</b> the route, auth guard, and DTO wiring are final; the injected
/// <see cref="IRouteService"/> implementation (<see cref="Services.RouteService"/>) is a stub that
/// throws <see cref="NotImplementedException"/> until a real OpenRouteService API key exists — see
/// <see cref="IRouteService"/> for the full deferred behavior.
/// </para>
/// </summary>
[ApiController]
[Route("internal/routing")]
[ServiceFilter(typeof(InternalApiKeyAuthFilter))]
public class RoutingController : ControllerBase
{
    private readonly IRouteService _routeService;

    /// <summary>Creates the controller with its injected route service.</summary>
    public RoutingController(IRouteService routeService)
    {
        _routeService = routeService;
    }

    /// <summary>
    /// Computes the routed distance and estimated travel time between two coordinates. Called by
    /// Agent 3 for two different legs in a single workflow run — see <see cref="IRouteService"/>'s
    /// class-level remarks for which — but this endpoint itself has no concept of "which leg"; it is
    /// a plain, reusable coordinate-to-coordinate lookup.
    /// </summary>
    /// <param name="request">The origin and destination coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with the distance/ETA on success, or 200 with <c>success: false</c> and no distance/ETA
    /// if the lookup fails after one retry — a routing failure is a normal in-band result, never an
    /// HTTP error, per the ticket's reliability rule.
    /// </returns>
    [HttpPost("route-eta")]
    public async Task<ActionResult<RouteEtaResponseDto>> GetRouteEta([FromBody] RouteEtaRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _routeService.GetRouteEtaAsync(request, cancellationToken);
        return Ok(result);
    }
}