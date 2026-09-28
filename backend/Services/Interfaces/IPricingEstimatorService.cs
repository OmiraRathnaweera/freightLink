using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Computes an <see cref="Entities.Load.EstimatedPrice"/> for the internal, non-shipper-facing
/// <c>POST /internal/pricing/estimate</c> endpoint, called by the Agentic AI pipeline's Agent 3.
/// This service performs no authentication — the caller has already passed
/// <c>InternalApiKeyAuthFilter</c>'s shared-secret check before this is invoked. This call is not
/// logged to the <c>ToolCall</c> table: its check constraint (<c>ck_toolcall_allowlist</c>) currently
/// only permits <c>ToolName = 'get_route_and_eta'</c>, and no service in this codebase writes
/// <c>ToolCall</c> rows yet at all — extending the allowlist is a migration, out of scope here. If
/// Agent 3 needs an audit trail of this call, it is either logged by the Python agent side, or added
/// later via its own migration.
/// </summary>
public interface IPricingEstimatorService
{
    /// <summary>
    /// Computes the price for <paramref name="request"/>'s load, using the current
    /// <see cref="Entities.FuelPriceRate"/> (fixed to <see cref="Entities.Enums.FuelType.AutoDiesel"/>),
    /// the current <see cref="Entities.VehicleClassEfficiency"/> for the supplied vehicle class (by
    /// exact class label, not the weight/volume-band lookup), and the current
    /// <see cref="Entities.PricingFormulaConfig"/>, per the ADR-015 formula. Writes only
    /// <see cref="Entities.Load.EstimatedPrice"/> — no other column, no new column.
    /// </summary>
    /// <param name="request">The load id, agent-selected vehicle class, and agent-routed distance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The computed price and its breakdown.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if the load doesn't exist; 503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/>
    /// if no current fuel price, vehicle-class efficiency figure, or formula configuration exists;
    /// 409 <see cref="Common.Errors.ErrorCode.LOAD_CONCURRENCY_CONFLICT"/> on a concurrent write race.
    /// </exception>
    Task<PricingEstimateResponseDto> EstimateAsync(EstimatePricingRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a rough, pre-matching price quote for the Shipper-facing <c>POST /api/v1/loads/{id}/estimate</c>,
    /// per Component A's price-estimation contract: the straight-line (haversine) distance between the
    /// load's own pickup/dropoff coordinates (no routing call), a vehicle class resolved from the load's
    /// weight/volume via <see cref="IPricingConfigService.GetTierForWeightAndVolume"/> (no agency/vehicle
    /// chosen yet), and the same <c>baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)</c> formula
    /// and reference-rate tables as <see cref="EstimateAsync"/>. Unlike <see cref="EstimateAsync"/>, this
    /// does not write to <see cref="Entities.Load.EstimatedPrice"/> — that column is the AI agent's own
    /// computed price (Agent 3), and this rough shipper preview must not be confused with or overwrite it.
    /// </summary>
    /// <param name="loadId">The load to estimate.</param>
    /// <param name="currentUserId">The authenticated caller's id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if the load doesn't exist; 403 <see cref="Common.Errors.ErrorCode.LOAD_NOT_OWNED"/> if the
    /// caller is not the owning Shipper; 503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/>
    /// if no matching vehicle-class tier or current reference-rate row exists.
    /// </exception>
    Task<FreightLink.Api.DTOs.Loads.LoadPriceEstimateResponseDto> EstimateForShipperAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}
