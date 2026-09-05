using FreightLink.Api.Common.Filters;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Internal, service-to-service pricing endpoint for the Agentic AI pipeline (Agent 3) — never called
/// by a shipper or any public client. Deliberately routed outside the <c>/api/v1</c> prefix, since it
/// is explicitly outside the public/versioned API surface (see
/// <c>../docs/api-contract-openapi-skeleton.md</c>'s scoping note on ASP.NET↔Python service calls).
/// Guarded by <see cref="InternalApiKeyAuthFilter"/> (a shared-secret header), not JWT — this has no
/// human caller and carries no <c>[Authorize]</c> attribute.
/// </summary>
[ApiController]
[Route("internal/pricing")]
[ServiceFilter(typeof(InternalApiKeyAuthFilter))]
public class InternalPricingController : ControllerBase
{
    private readonly IPricingEstimatorService _pricingEstimatorService;

    /// <summary>Creates the controller with its injected price estimator service.</summary>
    public InternalPricingController(IPricingEstimatorService pricingEstimatorService)
    {
        _pricingEstimatorService = pricingEstimatorService;
    }

    /// <summary>
    /// Computes and persists <c>Load.EstimatedPrice</c> for the given load, using an
    /// already-selected vehicle class and an already-routed distance supplied by the caller.
    /// </summary>
    /// <param name="request">The load id, agent-selected vehicle class, and agent-routed distance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the computed price and its breakdown.</returns>
    [HttpPost("estimate")]
    public async Task<ActionResult<PricingEstimateResponseDto>> Estimate([FromBody] EstimatePricingRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _pricingEstimatorService.EstimateAsync(request, cancellationToken);
        return Ok(result);
    }
}
