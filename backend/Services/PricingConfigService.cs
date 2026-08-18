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
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the pricing config service with its DB context.</summary>
    public PricingConfigService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<FuelPriceRateResponseDto> GetCurrentFuelPrice(FuelType fuelType, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.FuelPriceRates.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.FuelType == fuelType && x.DeletedAt == null)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                $"No current fuel price configured for {fuelType}. An Admin must add one before loads can be priced.");
        }

        return MapToResponse(current, ResolveUserName(current.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<List<FuelPriceRateResponseDto>> GetAllCurrentFuelPrices(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FuelPriceRates.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.DeletedAt == null)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.FuelType)
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<FuelPriceRateResponseDto>> GetFuelPriceHistory(FuelType fuelType, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.FuelPriceRates.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.FuelType == fuelType)
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName))).ToList();
    }

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> GetCurrentVehicleClassEfficiency(VehicleClass classLabel, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.VehicleClassEfficiencies.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.ClassLabel == classLabel && x.DeletedAt == null)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                $"No current vehicle-class efficiency figure configured for {classLabel}. An Admin must add one before loads can be priced.");
        }

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
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapToResponse(x, ResolveUserName(x.SetByUser?.FullName))).ToList();
    }

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> GetTierForWeight(decimal weightKg, CancellationToken cancellationToken = default)
    {
        var currentTiers = await GetCurrentVehicleClassEfficiencyEntitiesAsync(cancellationToken);
        var match = currentTiers.FirstOrDefault(t => t.MinPayloadKg <= weightKg && (t.MaxPayloadKg is null || weightKg < t.MaxPayloadKg.Value));

        if (match is null)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, ErrorCode.PRICING_CONFIG_MISSING,
                $"No configured vehicle-class efficiency tier covers a payload of {weightKg} kg. An Admin must configure a matching tier before loads of this weight can be priced.");
        }

        return MapToResponse(match, ResolveUserName(match.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<FuelPriceRateResponseDto> CreateFuelPriceRate(CreateFuelPriceRateDto request, Guid actingUserId, CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> CreateVehicleClassEfficiency(CreateVehicleClassEfficiencyDto request, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var classLabel = request.ClassLabel!.Value;
        var minPayloadKg = request.MinPayloadKg!.Value;

        ValidatePayloadBand(minPayloadKg, request.MaxPayloadKg);

        var otherCurrentBands = (await GetCurrentVehicleClassEfficiencyEntitiesAsync(cancellationToken))
            .Where(t => t.ClassLabel != classLabel)
            .Select(t => (Min: t.MinPayloadKg, Max: t.MaxPayloadKg));

        var candidateBands = otherCurrentBands
            .Append((Min: minPayloadKg, Max: request.MaxPayloadKg))
            .OrderBy(b => b.Min)
            .ToList();

        ValidateNoOverlapOrGap(candidateBands);

        var now = DateTimeOffset.UtcNow;
        var efficiency = new VehicleClassEfficiency
        {
            VehicleClassEfficiencyId = Guid.NewGuid(),
            ClassLabel = classLabel,
            MinPayloadKg = minPayloadKg,
            MaxPayloadKg = request.MaxPayloadKg,
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

    /// <inheritdoc />
    public async Task<FuelPriceRateResponseDto> SoftDeleteFuelPriceRate(Guid fuelPriceRateId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.FuelPriceRates
            .Include(x => x.SetByUser)
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

        return MapToResponse(rate, ResolveUserName(rate.SetByUser?.FullName));
    }

    /// <inheritdoc />
    public async Task<VehicleClassEfficiencyResponseDto> SoftDeleteVehicleClassEfficiency(Guid vehicleClassEfficiencyId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var efficiency = await _dbContext.VehicleClassEfficiencies
            .Include(x => x.SetByUser)
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

        return MapToResponse(efficiency, ResolveUserName(efficiency.SetByUser?.FullName));
    }

    /// <summary>
    /// The current (non-deleted, latest <c>EffectiveFrom</c>) <see cref="VehicleClassEfficiency"/> row
    /// for every <see cref="VehicleClass"/> that has one. Shared by <see cref="GetAllCurrentVehicleClassEfficiencies"/>,
    /// <see cref="GetTierForWeight"/>, and <see cref="CreateVehicleClassEfficiency"/>'s overlap/gap check.
    /// </summary>
    private async Task<List<VehicleClassEfficiency>> GetCurrentVehicleClassEfficiencyEntitiesAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.VehicleClassEfficiencies.AsNoTracking()
            .Include(x => x.SetByUser)
            .Where(x => x.DeletedAt == null)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ClassLabel)
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .ToList();
    }

    /// <summary>Throws if <paramref name="maxPayloadKg"/> is provided but not strictly greater than <paramref name="minPayloadKg"/> (mirrors <c>ck_vce_payload_bounds</c>).</summary>
    private static void ValidatePayloadBand(decimal minPayloadKg, decimal? maxPayloadKg)
    {
        if (maxPayloadKg is { } max && max <= minPayloadKg)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_INVALID_PAYLOAD_BAND,
                "MaxPayloadKg must be strictly greater than MinPayloadKg when provided.");
        }
    }

    /// <summary>
    /// Throws if <paramref name="sortedBands"/> (ascending by <c>Min</c>, one candidate new band plus
    /// every other class's current band) leaves a gap below zero, leaves a gap between two bands, or
    /// has two bands overlap. A well-formed set is fully contiguous: each band's <c>Max</c> equals the
    /// next band's <c>Min</c>, and the lowest band's <c>Min</c> is <c>0</c>.
    /// </summary>
    private static void ValidateNoOverlapOrGap(List<(decimal Min, decimal? Max)> sortedBands)
    {
        if (sortedBands.Count == 0)
        {
            return;
        }

        if (sortedBands[0].Min != 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP,
                $"The lowest configured payload band must start at 0 kg; the lowest band here starts at {sortedBands[0].Min} kg.");
        }

        for (var i = 0; i < sortedBands.Count - 1; i++)
        {
            var current = sortedBands[i];
            var next = sortedBands[i + 1];

            if (current.Max is null)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP,
                    $"An open-ended band starting at {current.Min} kg cannot be followed by another band starting at {next.Min} kg.");
            }

            if (current.Max.Value < next.Min)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP,
                    $"There is a gap between {current.Max.Value} kg and {next.Min} kg with no configured tier.");
            }

            if (current.Max.Value > next.Min)
            {
                throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP,
                    $"The band ending at {current.Max.Value} kg overlaps the band starting at {next.Min} kg.");
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
        FuelConsumptionLPer100Km = efficiency.FuelConsumptionLPer100Km,
        Source = efficiency.Source,
        EffectiveFrom = efficiency.EffectiveFrom,
        SetByUserId = efficiency.SetByUserId,
        SetByUserName = setByUserName,
        CreatedAt = efficiency.CreatedAt,
        DeletedAt = efficiency.DeletedAt,
        DeletedByUserId = efficiency.DeletedByUserId
    };
}
