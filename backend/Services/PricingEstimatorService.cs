using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IPricingEstimatorService" />
public class PricingEstimatorService : IPricingEstimatorService
{
    private readonly AppDbContext _dbContext;
    private readonly IPricingConfigService _pricingConfigService;

    /// <summary>Creates the estimator service with its DB context and the shared pricing-config reference-data service.</summary>
    public PricingEstimatorService(AppDbContext dbContext, IPricingConfigService pricingConfigService)
    {
        _dbContext = dbContext;
        _pricingConfigService = pricingConfigService;
    }

    /// <inheritdoc />
    public async Task<PricingEstimateResponseDto> EstimateAsync(EstimatePricingRequestDto request, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == request.LoadId!.Value, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        var vehicleClass = request.SuggestedVehicleClass!.Value;
        var distanceKm = request.DistanceKm!.Value;

        // A single, locked read of all three current config rows together — not three separate
        // calls — so a concurrent Admin write can't land between them and produce an estimate that
        // mixes a pre-write value from one table with a post-write value from another. By exact class
        // label, not GetTierForWeightAndVolume — Agent 3 has already chosen the vehicle class, so
        // this endpoint must not re-derive one from Load.WeightKg/VolumeM3. AutoDiesel is the
        // estimator's fixed fuel type, per the task this endpoint implements — not worth a dedicated
        // constants file for a single literal.
        var snapshot = await _pricingConfigService.GetPricingSnapshotForEstimate(vehicleClass, FuelType.AutoDiesel, cancellationToken);
        var efficiency = snapshot.Efficiency;
        var fuelPrice = snapshot.FuelPrice;
        var formulaConfig = snapshot.FormulaConfig;

        var ratePerKm = (fuelPrice.PricePerLitre / 100m) * efficiency.FuelConsumptionLPer100Km
                       + formulaConfig.DriverCostPerKm + formulaConfig.MaintenanceAllowancePerKm;
        ratePerKm *= 1 + formulaConfig.MarginPercent;

        var estimatedPrice = formulaConfig.BaseFare
                            + (distanceKm * ratePerKm)
                            + (load.WeightKg * formulaConfig.RatePerKg);

        load.EstimatedPrice = estimatedPrice;
        await SaveChangesWithConcurrencyCheckAsync(cancellationToken);

        return new PricingEstimateResponseDto
        {
            LoadId = load.LoadId,
            EstimatedPrice = estimatedPrice,
            DistanceKm = distanceKm,
            VehicleClass = vehicleClass,
            RatePerKm = ratePerKm,
            RatePerKg = formulaConfig.RatePerKg,
            BaseFare = formulaConfig.BaseFare
        };
    }

    /// <summary>
    /// Saves pending changes, translating a concurrent write caught by <c>Load</c>'s xmin
    /// concurrency token into a client-facing 409 instead of an unhandled
    /// <see cref="DbUpdateConcurrencyException"/>. Duplicated from <c>LoadService</c>'s identical
    /// private helper rather than shared, since that one is private to its own class.
    /// </summary>
    private async Task SaveChangesWithConcurrencyCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.LOAD_CONCURRENCY_CONFLICT, "This load was modified by another request. Please reload and try again.");
        }
    }
}
