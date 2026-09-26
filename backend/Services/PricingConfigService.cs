using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IPricingConfigService" />
public class PricingConfigService : IPricingConfigService
{
    /// <summary>
    /// Serializes every pricing-config write (<c>Create*</c>/<c>SoftDelete*</c> across all three
    /// tables) and <see cref="GetPricingSnapshotForEstimate"/>'s combined read within this process.
    /// This is what makes <see cref="CreateVehicleClassEfficiency"/>'s
    /// read-validate-insert sequence safe against a concurrent request racing the same stale snapshot,
    /// makes two concurrent soft-deletes of the same row resolve deterministically (the second sees
    /// the first's committed state instead of silently overwriting <c>DeletedByUserId</c>), and keeps
    /// <see cref="GetPricingSnapshotForEstimate"/>'s three reads from straddling a concurrent write
    /// (which would otherwise let the internal price estimator combine a pre-write value from one
    /// table with a post-write value from another). <c>static</c> is required — a new
    /// <see cref="PricingConfigService"/> instance is constructed per request (scoped DI), so only a
    /// process-wide field actually coordinates across concurrent requests. Deliberately a plain mutex,
    /// not a reader/writer lock — this app runs as a single instance (one <c>compose.yaml</c> service,
    /// no load balancer anywhere in the repo), so full write/write and read/write serialization is
    /// correct and simple; it would need to become a DB-level primitive (e.g. an advisory lock or
    /// <c>SERIALIZABLE</c> transaction) if this app were ever horizontally scaled to multiple processes.
    /// </summary>
    private static readonly SemaphoreSlim _pricingConfigLock = new(1, 1);

    private readonly AppDbContext _dbContext;

    /// <summary>Creates the pricing config service with its DB context.</summary>
    public PricingConfigService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<FuelPriceRateResponseDto> GetCurrentFuelPrice(FuelType fuelType, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentFuelPriceEntityAsync(fuelType, cancellationToken);
        return MapToResponse(current, ResolveUserName(current.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<List<FuelPriceRateResponseDto>> GetAllCurrentFuelPrices(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await _dbContext.FuelPriceRates.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.DeletedAt == null && x.EffectiveFrom <= now)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.FuelType)
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FuelPriceRateId).First())
            .Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<FuelPriceRateResponseDto>> GetFuelPriceHistory(FuelType fuelType, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FuelPriceRates.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.FuelType == fuelType)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FuelPriceRateId)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName))).ToList();
    }

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> GetCurrentVehicleClassEfficiency(VehicleClass classLabel, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentVehicleClassEfficiencyEntityAsync(classLabel, cancellationToken);
        return MapToResponse(current, ResolveUserName(current.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<List<VehicleClassEfficiencyResponseDto>> GetAllCurrentVehicleClassEfficiencies(CancellationToken cancellationToken = default)
    {
        var currentTiers = await GetCurrentVehicleClassEfficiencyEntitiesAsync(cancellationToken);
        return currentTiers.Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName))).ToList();
    }

    /// <inheritdoc />
    public async Task<List<VehicleClassEfficiencyResponseDto>> GetVehicleClassEfficiencyHistory(VehicleClass classLabel, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.VehicleClassEfficiencies.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.ClassLabel == classLabel)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.VehicleClassEfficiencyId)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName))).ToList();
    }

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> GetTierForWeightAndVolume(decimal weightKg, decimal volumeM3, CancellationToken cancellationToken = default)
    {
        var match = await GetCurrentTierEntityForWeightAndVolumeAsync(weightKg, volumeM3, cancellationToken);
        return MapToResponse(match, ResolveUserName(match.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<FuelPriceRateResponseDto> CreateFuelPriceRate(CreateFuelPriceRateDto request, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var rate = new FuelPriceRate
            {
                FuelPriceRateId = Guid.NewGuid(),
                FuelType = request.FuelType!.Value,
                PricePerLitre = request.PricePerLitre,
                Source = request.Source,
                EffectiveFrom = request.EffectiveFrom,
                SetByUserId = actingUserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.FuelPriceRates.Add(rate);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // rate.SetByUser is never populated at this point (a freshly-added tracked entity has no
            // navigation fix-up from the DB), so the setter's display name is resolved with a dedicated
            // lookup rather than an Include on an entity that was just inserted, not queried.
            var setByUserName = await _dbContext.Users.AsNoTracking()
                .Where(u => u.UserId == actingUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            return MapToResponse(rate, ResolveUserName(setByUserName));
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> CreateVehicleClassEfficiency(CreateVehicleClassEfficiencyDto request, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var classLabel = request.ClassLabel!.Value;
        var minPayloadKg = request.MinPayloadKg!.Value;
        var minVolumeM3 = request.MinVolumeM3!.Value;

        ValidateBand(minPayloadKg, request.MaxPayloadKg, nameof(request.MinPayloadKg), nameof(request.MaxPayloadKg), ErrorCode.VEHICLE_CLASS_EFFICIENCY_INVALID_PAYLOAD_BAND);
        ValidateBand(minVolumeM3, request.MaxVolumeM3, nameof(request.MinVolumeM3), nameof(request.MaxVolumeM3), ErrorCode.VEHICLE_CLASS_EFFICIENCY_INVALID_VOLUME_BAND);

        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            // Read-validate-insert against "the other classes' current bands" all happens while holding
            // _pricingConfigLock, so a second concurrent call can't read this same pre-insert snapshot —
            // it blocks here until this call's SaveChangesAsync (or throw) completes, then re-reads the
            // now-current state for real. Closes the TOCTOU race a plain unlocked check-then-write would
            // have (two concurrent inserts for two different classes could otherwise both validate
            // against an empty/stale snapshot and both commit, leaving overlapping "current" bands).
            var otherCurrentTiers = (await GetCurrentVehicleClassEfficiencyEntitiesAsync(cancellationToken))
                .Where(t => t.ClassLabel != classLabel)
                .ToList();

            var candidateWeightBands = otherCurrentTiers
                .Select(t => (Min: t.MinPayloadKg, Max: t.MaxPayloadKg))
                .Append((Min: minPayloadKg, Max: request.MaxPayloadKg))
                .OrderBy(b => b.Min)
                .ToList();
            ValidateNoOverlapOrGap(candidateWeightBands, "payload (kg)");

            var candidateVolumeBands = otherCurrentTiers
                .Select(t => (Min: t.MinVolumeM3, Max: t.MaxVolumeM3))
                .Append((Min: minVolumeM3, Max: request.MaxVolumeM3))
                .OrderBy(b => b.Min)
                .ToList();
            ValidateNoOverlapOrGap(candidateVolumeBands, "volume (m³)");

            var now = DateTimeOffset.UtcNow;
            var efficiency = new VehicleClassEfficiency
            {
                VehicleClassEfficiencyId = Guid.NewGuid(),
                ClassLabel = classLabel,
                MinPayloadKg = minPayloadKg,
                MaxPayloadKg = request.MaxPayloadKg,
                MinVolumeM3 = minVolumeM3,
                MaxVolumeM3 = request.MaxVolumeM3,
                FuelConsumptionLPer100Km = request.FuelConsumptionLPer100Km,
                Source = request.Source,
                EffectiveFrom = request.EffectiveFrom,
                SetByUserId = actingUserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.VehicleClassEfficiencies.Add(efficiency);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var setByUserName = await _dbContext.Users.AsNoTracking()
                .Where(u => u.UserId == actingUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            return MapToResponse(efficiency, ResolveUserName(setByUserName));
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PricingConfigDeleteResponseDto> SoftDeleteFuelPriceRate(Guid fuelPriceRateId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            var rate = await _dbContext.FuelPriceRates
                .FirstOrDefaultAsync(x => x.FuelPriceRateId == fuelPriceRateId, cancellationToken);

            if (rate is null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.FUEL_PRICE_RATE_NOT_FOUND, "The requested fuel price rate could not be found.");
            }

            if (rate.DeletedAt is not null)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.FUEL_PRICE_RATE_ALREADY_DELETED, "This fuel price rate has already been soft-deleted.");
            }

            rate.DeletedAt = DateTimeOffset.UtcNow;
            rate.DeletedByUserId = actingUserId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PricingConfigDeleteResponseDto { Message = "Fuel price rate deleted successfully.", Id = rate.FuelPriceRateId };
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PricingConfigDeleteResponseDto> SoftDeleteVehicleClassEfficiency(Guid vehicleClassEfficiencyId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            var efficiency = await _dbContext.VehicleClassEfficiencies
                .FirstOrDefaultAsync(x => x.VehicleClassEfficiencyId == vehicleClassEfficiencyId, cancellationToken);

            if (efficiency is null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.VEHICLE_CLASS_EFFICIENCY_NOT_FOUND, "The requested vehicle-class efficiency figure could not be found.");
            }

            if (efficiency.DeletedAt is not null)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.VEHICLE_CLASS_EFFICIENCY_ALREADY_DELETED, "This vehicle-class efficiency figure has already been soft-deleted.");
            }

            efficiency.DeletedAt = DateTimeOffset.UtcNow;
            efficiency.DeletedByUserId = actingUserId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PricingConfigDeleteResponseDto { Message = "Vehicle-class efficiency figure deleted successfully.", Id = efficiency.VehicleClassEfficiencyId };
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PricingFormulaConfigResponseDto> GetCurrentPricingFormulaConfig(CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentPricingFormulaConfigEntityAsync(cancellationToken);
        return MapToResponse(current, ResolveUserName(current.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<List<PricingFormulaConfigResponseDto>> GetPricingFormulaConfigHistory(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.PricingFormulaConfigs.AsNoTracking()
            .Include(x => x.SetByUser)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.PricingFormulaConfigId)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName))).ToList();
    }

    /// <inheritdoc />
    public async Task<PricingFormulaConfigResponseDto> CreatePricingFormulaConfig(CreatePricingFormulaConfigDto request, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var config = new PricingFormulaConfig
            {
                PricingFormulaConfigId = Guid.NewGuid(),
                BaseFare = request.BaseFare!.Value,
                RatePerKg = request.RatePerKg!.Value,
                DriverCostPerKm = request.DriverCostPerKm!.Value,
                MaintenanceAllowancePerKm = request.MaintenanceAllowancePerKm!.Value,
                MarginPercent = request.MarginPercent!.Value,
                Source = request.Source,
                EffectiveFrom = request.EffectiveFrom,
                SetByUserId = actingUserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.PricingFormulaConfigs.Add(config);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // config.SetByUser is never populated at this point (a freshly-added tracked entity has
            // no navigation fix-up from the DB), so the setter's display name is resolved with a
            // dedicated lookup rather than an Include on an entity that was just inserted, not queried.
            var setByUserName = await _dbContext.Users.AsNoTracking()
                .Where(u => u.UserId == actingUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            return MapToResponse(config, ResolveUserName(setByUserName));
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PricingConfigDeleteResponseDto> SoftDeletePricingFormulaConfig(Guid pricingFormulaConfigId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            var config = await _dbContext.PricingFormulaConfigs
                .FirstOrDefaultAsync(x => x.PricingFormulaConfigId == pricingFormulaConfigId, cancellationToken);

            if (config is null)
            {
                throw new ApiException(HttpStatusCode.NotFound, ErrorCode.PRICING_FORMULA_CONFIG_NOT_FOUND, "The requested pricing formula configuration could not be found.");
            }

            if (config.DeletedAt is not null)
            {
                throw new ApiException(HttpStatusCode.UnprocessableEntity, ErrorCode.PRICING_FORMULA_CONFIG_ALREADY_DELETED, "This pricing formula configuration has already been soft-deleted.");
            }

            config.DeletedAt = DateTimeOffset.UtcNow;
            config.DeletedByUserId = actingUserId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PricingConfigDeleteResponseDto { Message = "Pricing formula configuration deleted successfully.", Id = config.PricingFormulaConfigId };
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PricingSnapshotDto> GetPricingSnapshotForEstimate(VehicleClass classLabel, FuelType fuelType, CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            // All three reads happen while holding the same lock every Create*/SoftDelete* write is
            // serialized by, so a concurrent Admin write can't land between them — the estimator gets
            // one internally-consistent snapshot, never a mix of a pre-write and post-write value.
            var efficiency = await GetCurrentVehicleClassEfficiencyEntityAsync(classLabel, cancellationToken);
            var fuelPrice = await GetCurrentFuelPriceEntityAsync(fuelType, cancellationToken);
            var formulaConfig = await GetCurrentPricingFormulaConfigEntityAsync(cancellationToken);

            return new PricingSnapshotDto
            {
                Efficiency = MapToResponse(efficiency, ResolveUserName(efficiency.SetByUser?.FullName)),
                FuelPrice = MapToResponse(fuelPrice, ResolveUserName(fuelPrice.SetByUser?.FullName)),
                FormulaConfig = MapToResponse(formulaConfig, ResolveUserName(formulaConfig.SetByUser?.FullName))
            };
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }

    /// <summary>The current (non-deleted, non-future-dated, latest-<c>EffectiveFrom</c>) row for <paramref name="fuelType"/>.</summary>
    private async Task<FuelPriceRate> GetCurrentFuelPriceEntityAsync(FuelType fuelType, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _dbContext.FuelPriceRates.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.FuelType == fuelType && x.DeletedAt == null && x.EffectiveFrom <= now)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FuelPriceRateId)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                $"No current fuel price configured for {fuelType}. An Admin must add one before loads can be priced.");
        }

        return current;
    }

    /// <summary>The current (non-deleted, non-future-dated, latest-<c>EffectiveFrom</c>) row for <paramref name="classLabel"/>.</summary>
    private async Task<VehicleClassEfficiency> GetCurrentVehicleClassEfficiencyEntityAsync(VehicleClass classLabel, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _dbContext.VehicleClassEfficiencies.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.ClassLabel == classLabel && x.DeletedAt == null && x.EffectiveFrom <= now)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.VehicleClassEfficiencyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                $"No current vehicle-class efficiency figure configured for {classLabel}. An Admin must add one before loads can be priced.");
        }

        return current;
    }

    /// <summary>
    /// The current (non-deleted, non-future-dated, latest-<c>EffectiveFrom</c>) <see cref="VehicleClassEfficiency"/> row
    /// for every <see cref="VehicleClass"/> that has one. Shared by <see cref="GetAllCurrentVehicleClassEfficiencies"/>,
    /// <see cref="GetCurrentTierEntityForWeightAndVolumeAsync"/>, and <see cref="CreateVehicleClassEfficiency"/>'s
    /// overlap/gap check.
    /// </summary>
    private async Task<List<VehicleClassEfficiency>> GetCurrentVehicleClassEfficiencyEntitiesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await _dbContext.VehicleClassEfficiencies.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.DeletedAt == null && x.EffectiveFrom <= now)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ClassLabel)
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.VehicleClassEfficiencyId).First())
            .ToList();
    }

    /// <summary>The current (non-deleted, non-future-dated, latest-<c>EffectiveFrom</c>) <see cref="PricingFormulaConfig"/> row.</summary>
    private async Task<PricingFormulaConfig> GetCurrentPricingFormulaConfigEntityAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _dbContext.PricingFormulaConfigs.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.DeletedAt == null && x.EffectiveFrom <= now)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.PricingFormulaConfigId)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                "No current pricing formula configuration exists. An Admin must add one before loads can be priced.");
        }

        return current;
    }

    /// <summary>
    /// Resolves which current tier a load of the given weight and volume falls into. Tiers are sorted
    /// ascending by <c>MinPayloadKg</c> (this project's canonical vehicle-size order). The tier required
    /// by weight alone and the tier required by volume alone are found independently; the larger
    /// (later-indexed) of the two is selected, so a bulky-but-light load is correctly upsized to a bigger
    /// class instead of being priced against a weight-only match.
    /// </summary>
    private async Task<VehicleClassEfficiency> GetCurrentTierEntityForWeightAndVolumeAsync(decimal weightKg, decimal volumeM3, CancellationToken cancellationToken)
    {
        var tiers = (await GetCurrentVehicleClassEfficiencyEntitiesAsync(cancellationToken))
            .OrderBy(t => t.MinPayloadKg)
            .ToList();

        var weightIndex = tiers.FindIndex(t => t.MinPayloadKg <= weightKg && (t.MaxPayloadKg is null || weightKg < t.MaxPayloadKg.Value));
        var volumeIndex = tiers.FindIndex(t => t.MinVolumeM3 <= volumeM3 && (t.MaxVolumeM3 is null || volumeM3 < t.MaxVolumeM3.Value));

        if (weightIndex < 0 || volumeIndex < 0)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                $"No configured vehicle-class efficiency tier covers a payload of {weightKg} kg and {volumeM3} m³ together. An Admin must configure a matching tier before loads of this size can be priced.");
        }

        return tiers[Math.Max(weightIndex, volumeIndex)];
    }

    /// <summary>Throws if <paramref name="max"/> is provided but not strictly greater than <paramref name="min"/> (mirrors <c>ck_vce_payload_bounds</c>/<c>ck_vce_volume_bounds</c>).</summary>
    private static void ValidateBand(decimal min, decimal? max, string minFieldName, string maxFieldName, ErrorCode errorCode)
    {
        if (max is { } m && m <= min)
        {
            throw new ApiException(HttpStatusCode.BadRequest, errorCode,
                $"{maxFieldName} must be strictly greater than {minFieldName} when provided.");
        }
    }

    /// <summary>
    /// Throws if <paramref name="sortedBands"/> (ascending by <c>Min</c>, one candidate new band plus
    /// every other class's current band in this same <paramref name="dimensionLabel"/> dimension) leaves
    /// a gap below zero, leaves a gap between two bands, or has two bands overlap. A well-formed set is
    /// fully contiguous: each band's <c>Max</c> equals the next band's <c>Min</c>, and the lowest band's
    /// <c>Min</c> is <c>0</c>.
    /// </summary>
    private static void ValidateNoOverlapOrGap(List<(decimal Min, decimal? Max)> sortedBands, string dimensionLabel)
    {
        if (sortedBands.Count == 0)
        {
            return;
        }

        if (sortedBands[0].Min != 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP,
                $"The lowest configured {dimensionLabel} band must start at 0; the lowest band here starts at {sortedBands[0].Min}.");
        }

        for (var i = 0; i < sortedBands.Count - 1; i++)
        {
            var current = sortedBands[i];
            var next = sortedBands[i + 1];

            if (current.Max is null)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP,
                    $"An open-ended {dimensionLabel} band starting at {current.Min} cannot be followed by another band starting at {next.Min}.");
            }

            if (current.Max.Value < next.Min)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP,
                    $"There is a gap in {dimensionLabel} coverage between {current.Max.Value} and {next.Min} with no configured tier.");
            }

            if (current.Max.Value > next.Min)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP,
                    $"The {dimensionLabel} band ending at {current.Max.Value} overlaps the band starting at {next.Min}.");
            }
        }
    }

    /// <summary>
    /// Normalizes a possibly-missing display name to a non-null, non-empty string. A blank result
    /// means the setting <see cref="User"/> row could not be resolved — deliberately never surfaced as
    /// null on the wire.
    /// </summary>
    private static string ResolveUserName(string? fullName) =>
        string.IsNullOrWhiteSpace(fullName) ? "Unknown" : fullName;

    /// <summary>Maps a <see cref="FuelPriceRate"/> entity to its wire-facing representation.</summary>
    private static FuelPriceRateResponseDto MapToResponse(FuelPriceRate rate, string setByUserName) => new()
    {
        FuelPriceRateId = rate.FuelPriceRateId,
        FuelType = rate.FuelType,
        PricePerLitre = rate.PricePerLitre,
        Source = rate.Source,
        EffectiveFrom = rate.EffectiveFrom,
        SetByUserId = rate.SetByUserId,
        SetByUserName = setByUserName,
        CreatedAt = rate.CreatedAt,
        DeletedAt = rate.DeletedAt,
        DeletedByUserId = rate.DeletedByUserId
    };

    /// <summary>Maps a <see cref="VehicleClassEfficiency"/> entity to its wire-facing representation.</summary>
    private static VehicleClassEfficiencyResponseDto MapToResponse(VehicleClassEfficiency efficiency, string setByUserName) => new()
    {
        VehicleClassEfficiencyId = efficiency.VehicleClassEfficiencyId,
        ClassLabel = efficiency.ClassLabel,
        MinPayloadKg = efficiency.MinPayloadKg,
        MaxPayloadKg = efficiency.MaxPayloadKg,
        MinVolumeM3 = efficiency.MinVolumeM3,
        MaxVolumeM3 = efficiency.MaxVolumeM3,
        FuelConsumptionLPer100Km = efficiency.FuelConsumptionLPer100Km,
        Source = efficiency.Source,
        EffectiveFrom = efficiency.EffectiveFrom,
        SetByUserId = efficiency.SetByUserId,
        SetByUserName = setByUserName,
        CreatedAt = efficiency.CreatedAt,
        DeletedAt = efficiency.DeletedAt,
        DeletedByUserId = efficiency.DeletedByUserId
    };

    /// <summary>Maps a <see cref="PricingFormulaConfig"/> entity to its wire-facing representation.</summary>
    private static PricingFormulaConfigResponseDto MapToResponse(PricingFormulaConfig config, string setByUserName) => new()
    {
        PricingFormulaConfigId = config.PricingFormulaConfigId,
        BaseFare = config.BaseFare,
        RatePerKg = config.RatePerKg,
        DriverCostPerKm = config.DriverCostPerKm,
        MaintenanceAllowancePerKm = config.MaintenanceAllowancePerKm,
        MarginPercent = config.MarginPercent,
        Source = config.Source,
        EffectiveFrom = config.EffectiveFrom,
        SetByUserId = config.SetByUserId,
        SetByUserName = setByUserName,
        CreatedAt = config.CreatedAt,
        DeletedAt = config.DeletedAt,
        DeletedByUserId = config.DeletedByUserId
    };

    /// <inheritdoc />
    public async Task SeedDefaultPricingConfigIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        await _pricingConfigLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var adminUser = await _dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Role == UserRole.Admin, cancellationToken);
            var actingUserId = adminUser?.UserId ?? Guid.NewGuid();

            // 1. Seed Fuel Price Rate (AutoDiesel)
            var hasFuelPrice = await _dbContext.FuelPriceRates
                .AnyAsync(f => f.DeletedAt == null && f.EffectiveFrom <= now, cancellationToken);

            if (!hasFuelPrice)
            {
                _dbContext.FuelPriceRates.Add(new FuelPriceRate
                {
                    FuelPriceRateId = Guid.NewGuid(),
                    FuelType = FuelType.AutoDiesel,
                    PricePerLitre = 350m,
                    Source = "CPC Market Reference Rate (Seeded)",
                    EffectiveFrom = now,
                    SetByUserId = actingUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // 2. Seed Pricing Formula Config
            var hasFormulaConfig = await _dbContext.PricingFormulaConfigs
                .AnyAsync(f => f.DeletedAt == null && f.EffectiveFrom <= now, cancellationToken);

            if (!hasFormulaConfig)
            {
                _dbContext.PricingFormulaConfigs.Add(new PricingFormulaConfig
                {
                    PricingFormulaConfigId = Guid.NewGuid(),
                    BaseFare = 5000m,
                    RatePerKg = 2m,
                    DriverCostPerKm = 25m,
                    MaintenanceAllowancePerKm = 15m,
                    MarginPercent = 0.15m,
                    Source = "ADR-015 Standard Rate Matrix (Seeded)",
                    EffectiveFrom = now,
                    SetByUserId = actingUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // 3. Seed Vehicle Class Efficiencies (MiniTruck, MediumLorry, ContainerTruck)
            var existingClasses = await _dbContext.VehicleClassEfficiencies
                .Where(v => v.DeletedAt == null && v.EffectiveFrom <= now)
                .Select(v => v.ClassLabel)
                .ToListAsync(cancellationToken);

            if (existingClasses.Count == 0)
            {
                _dbContext.VehicleClassEfficiencies.AddRange(
                    new VehicleClassEfficiency
                    {
                        VehicleClassEfficiencyId = Guid.NewGuid(),
                        ClassLabel = VehicleClass.MiniTruck,
                        MinPayloadKg = 0m,
                        MaxPayloadKg = 1500m,
                        MinVolumeM3 = 0m,
                        MaxVolumeM3 = 6m,
                        FuelConsumptionLPer100Km = 10m,
                        Source = "Sri Lanka Transport Efficiency Standards (Seeded)",
                        EffectiveFrom = now,
                        SetByUserId = actingUserId,
                        CreatedAt = now,
                        UpdatedAt = now
                    },
                    new VehicleClassEfficiency
                    {
                        VehicleClassEfficiencyId = Guid.NewGuid(),
                        ClassLabel = VehicleClass.MediumLorry,
                        MinPayloadKg = 1500m,
                        MaxPayloadKg = 10000m,
                        MinVolumeM3 = 6m,
                        MaxVolumeM3 = 25m,
                        FuelConsumptionLPer100Km = 18m,
                        Source = "Sri Lanka Transport Efficiency Standards (Seeded)",
                        EffectiveFrom = now,
                        SetByUserId = actingUserId,
                        CreatedAt = now,
                        UpdatedAt = now
                    },
                    new VehicleClassEfficiency
                    {
                        VehicleClassEfficiencyId = Guid.NewGuid(),
                        ClassLabel = VehicleClass.ContainerTruck,
                        MinPayloadKg = 10000m,
                        MaxPayloadKg = null,
                        MinVolumeM3 = 25m,
                        MaxVolumeM3 = null,
                        FuelConsumptionLPer100Km = 30m,
                        Source = "Sri Lanka Transport Efficiency Standards (Seeded)",
                        EffectiveFrom = now,
                        SetByUserId = actingUserId,
                        CreatedAt = now,
                        UpdatedAt = now
                    }
                );
            }
            else
            {
                // If only partial classes exist, ensure MediumLorry and ContainerTruck are present
                if (!existingClasses.Contains(VehicleClass.MediumLorry))
                {
                    _dbContext.VehicleClassEfficiencies.Add(new VehicleClassEfficiency
                    {
                        VehicleClassEfficiencyId = Guid.NewGuid(),
                        ClassLabel = VehicleClass.MediumLorry,
                        MinPayloadKg = 1500m,
                        MaxPayloadKg = 10000m,
                        MinVolumeM3 = 6m,
                        MaxVolumeM3 = 25m,
                        FuelConsumptionLPer100Km = 18m,
                        Source = "Sri Lanka Transport Efficiency Standards (Seeded)",
                        EffectiveFrom = now,
                        SetByUserId = actingUserId,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                if (!existingClasses.Contains(VehicleClass.ContainerTruck))
                {
                    _dbContext.VehicleClassEfficiencies.Add(new VehicleClassEfficiency
                    {
                        VehicleClassEfficiencyId = Guid.NewGuid(),
                        ClassLabel = VehicleClass.ContainerTruck,
                        MinPayloadKg = 10000m,
                        MaxPayloadKg = null,
                        MinVolumeM3 = 25m,
                        MaxVolumeM3 = null,
                        FuelConsumptionLPer100Km = 30m,
                        Source = "Sri Lanka Transport Efficiency Standards (Seeded)",
                        EffectiveFrom = now,
                        SetByUserId = actingUserId,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _pricingConfigLock.Release();
        }
    }
}
