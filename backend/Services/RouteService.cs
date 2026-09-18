using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Services.Interfaces;

namespace FreightLink.Api.Services;

/// <summary>
/// Skeleton implementation of <see cref="IRouteService"/> — exists so <see cref="Controllers.RoutingController"/>
/// compiles and can be registered for DI immediately. Throws <see cref="NotImplementedException"/>
/// rather than a fake/simulated distance, so a call against a running skeleton fails loudly instead
/// of silently looking like a working integration. Deferred specifically because no real
/// <c>OPENROUTESERVICE__APIKEY</c> exists yet in <c>.env</c> to build and test a real HTTP call
/// against — see <see cref="IRouteService"/>'s XML docs for the exact required behavior once one is
/// available (the retry-once-then-<c>Success = false</c> rule in particular — this is a meaningfully
/// different failure contract than most of this codebase's other services, which throw
/// <c>ApiException</c> on failure).
/// </summary>
public class RouteService : IRouteService
{
    // TODO(Component C / Agent 3 owner): once OPENROUTESERVICE__APIKEY/BASEURL have real values,
    // inject IHttpClientFactory (or a typed HttpClient via AddHttpClient<IRouteService, RouteService>()
    // in Program.cs) here, call OpenRouteService's directions/matrix endpoint, and implement the
    // one-retry-then-Success=false rule from IRouteService's XML docs. Do not throw ApiException for
    // an OpenRouteService-side failure — that path must return Success = false, per the ticket.

    /// <inheritdoc />
    public Task<RouteEtaResponseDto> GetRouteEtaAsync(RouteEtaRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "RouteService.GetRouteEtaAsync is not yet implemented — no real OpenRouteService API key " +
            "is configured yet. See IRouteService for the required call shape, the retry-once-then-" +
            "Success=false reliability rule, and the still-open ToolCall audit-logging question (Y3S01-65).");
}