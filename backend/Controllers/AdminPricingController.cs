using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Admin-only CRUD over the ADR-019 pricing-config reference tables (<c>FuelPriceRate</c>,
/// <c>VehicleClassEfficiency</c>). Deliberately thin — every action just extracts the caller's
/// identity from the access token and delegates to <see cref="IPricingConfigService"/>, which owns all
/// business rules including the append-only versioning and soft-delete semantics.
/// </summary>
[ApiController]
[Route("api/v1/admin/pricing")]
[Authorize]
public class AdminPricingController : ControllerBase
{
    /// <summary>
    /// <see cref="Authorize"/>'s <c>Roles</c> property must be a compile-time constant, so it can't
    /// take a <see cref="UserRole"/> value directly — <see langword="nameof"/> is used instead of a
    /// string literal so a renamed enum member fails to compile here rather than silently desyncing.
    /// </summary>
    private const string AdminRole = nameof(UserRole.Admin);

    private readonly IPricingConfigService _pricingConfigService;

    /// <summary>Creates the controller with its injected pricing config service.</summary>
    public AdminPricingController(IPricingConfigService pricingConfigService)
    {
        _pricingConfigService = pricingConfigService;
    }

    /// <summary>Lists the current (non-deleted, latest <c>EffectiveFrom</c>) rate for every fuel type that has one.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of current fuel price rates.</returns>
    [HttpGet("fuel-rates")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<List<FuelPriceRateResponseDto>>> GetCurrentFuelRates(CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.GetAllCurrentFuelPrices(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists every rate ever recorded for one fuel type, including superseded and soft-deleted rows, newest first.</summary>
    /// <param name="fuelType">The fuel type to fetch history for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the full history for that fuel type.</returns>
    [HttpGet("fuel-rates/history")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<List<FuelPriceRateResponseDto>>> GetFuelRateHistory([FromQuery, Required] FuelType? fuelType, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.GetFuelPriceHistory(fuelType!.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>Records a new, current fuel price rate. Always inserts a new row — never updates an existing one.</summary>
    /// <param name="request">The new rate's content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created <see cref="FuelPriceRateResponseDto"/>.</returns>
    [HttpPost("fuel-rates")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<FuelPriceRateResponseDto>> CreateFuelRate([FromBody] CreateFuelPriceRateDto request, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.CreateFuelPriceRate(request, GetCurrentUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Soft-deletes a fuel price rate (sets <c>DeletedAt</c>/<c>DeletedByUserId</c>). Never issues a hard delete.</summary>
    /// <param name="id">The rate's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a success message.</returns>
    [HttpDelete("fuel-rates/{id:guid}")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<PricingConfigDeleteResponseDto>> DeleteFuelRate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.SoftDeleteFuelPriceRate(id, GetCurrentUserId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists the current (non-deleted, latest <c>EffectiveFrom</c>) figure for every vehicle class that has one.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of current vehicle-class efficiency figures.</returns>
    [HttpGet("vehicle-efficiency")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<List<VehicleClassEfficiencyResponseDto>>> GetCurrentVehicleEfficiency(CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.GetAllCurrentVehicleClassEfficiencies(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists every figure ever recorded for one vehicle class, including superseded and soft-deleted rows, newest first.</summary>
    /// <param name="vehicleClass">The vehicle class to fetch history for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the full history for that vehicle class.</returns>
    [HttpGet("vehicle-efficiency/history")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<List<VehicleClassEfficiencyResponseDto>>> GetVehicleEfficiencyHistory([FromQuery, Required] VehicleClass? vehicleClass, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.GetVehicleClassEfficiencyHistory(vehicleClass!.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Records a new, current vehicle-class efficiency figure. Always inserts a new row — never
    /// updates an existing one. Rejected if the new payload band overlaps or gaps against the other
    /// classes' current bands.
    /// </summary>
    /// <param name="request">The new figure's content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created <see cref="VehicleClassEfficiencyResponseDto"/>.</returns>
    [HttpPost("vehicle-efficiency")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<VehicleClassEfficiencyResponseDto>> CreateVehicleEfficiency([FromBody] CreateVehicleClassEfficiencyDto request, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.CreateVehicleClassEfficiency(request, GetCurrentUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Soft-deletes a vehicle-class efficiency figure (sets <c>DeletedAt</c>/<c>DeletedByUserId</c>). Never issues a hard delete.</summary>
    /// <param name="id">The figure's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a success message.</returns>
    [HttpDelete("vehicle-efficiency/{id:guid}")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<PricingConfigDeleteResponseDto>> DeleteVehicleEfficiency(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.SoftDeleteVehicleClassEfficiency(id, GetCurrentUserId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>The single current formula-constant configuration (base fare, per-kg rate, maintenance allowance).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the current <see cref="PricingFormulaConfigResponseDto"/>.</returns>
    [HttpGet("formula-config")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<PricingFormulaConfigResponseDto>> GetCurrentFormulaConfig(CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.GetCurrentPricingFormulaConfig(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists every formula-config row ever recorded, including superseded and soft-deleted rows, newest first.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the full formula-config history.</returns>
    [HttpGet("formula-config/history")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<List<PricingFormulaConfigResponseDto>>> GetFormulaConfigHistory(CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.GetPricingFormulaConfigHistory(cancellationToken);
        return Ok(result);
    }

    /// <summary>Records a new, current formula-constant configuration. Always inserts a new row — never updates an existing one.</summary>
    /// <param name="request">The new configuration's content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created <see cref="PricingFormulaConfigResponseDto"/>.</returns>
    [HttpPost("formula-config")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<PricingFormulaConfigResponseDto>> CreateFormulaConfig([FromBody] CreatePricingFormulaConfigDto request, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.CreatePricingFormulaConfig(request, GetCurrentUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Soft-deletes a formula-constant configuration (sets <c>DeletedAt</c>/<c>DeletedByUserId</c>). Never issues a hard delete.</summary>
    /// <param name="id">The configuration's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a success message.</returns>
    [HttpDelete("formula-config/{id:guid}")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<PricingConfigDeleteResponseDto>> DeleteFormulaConfig(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pricingConfigService.SoftDeletePricingFormulaConfig(id, GetCurrentUserId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Extracts the authenticated user's id from the <c>NameIdentifier</c> claim on the access token.</summary>
    /// <returns>The caller's user id.</returns>
    /// <exception cref="ApiException">401 if the claim is absent or not a well-formed GUID.</exception>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }
}
