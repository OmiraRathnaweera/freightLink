using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Read/create/soft-delete operations on the ADR-019 pricing-config reference tables
/// (<see cref="Entities.FuelPriceRate"/>, <see cref="Entities.VehicleClassEfficiency"/>). Both are
/// append-only: "editing" a row means inserting a new one with a later <c>EffectiveFrom</c>; an existing
/// row is only ever touched by a <c>SoftDelete*</c> method, which issues an ordinary <c>UPDATE</c>, never
/// a raw <c>DELETE</c> (the database's <c>trg_deny_delete_*</c> triggers are a backstop, not the
/// mechanism). "Current" row selection excludes both soft-deleted and not-yet-effective
/// (<c>EffectiveFrom</c> in the future) rows, and ties on <c>EffectiveFrom</c> are broken deterministically
/// by <c>CreatedAt</c> then id (both descending) — never an arbitrary/unordered pick. Every write is
/// serialized by a single in-process lock (see <c>PricingConfigService</c>'s <c>_pricingConfigLock</c>) so
/// concurrent requests can't both validate against the same stale snapshot. This data is reference
/// information for the AI agent's own price estimation, not consumed by <c>LoadService</c> — Load
/// creation/editing does not depend on any pricing config existing. This service performs no
/// authentication — the acting Admin's id is accepted as a plain parameter, sourced by the caller (in
/// practice, <c>AdminPricingController</c>) from validated JWT claims.
/// </summary>
public interface IPricingConfigService
{
    /// <summary>
    /// The single current rate for <paramref name="fuelType"/> — the latest non-future-dated,
    /// non-deleted <c>EffectiveFrom</c> row for that fuel type.
    /// </summary>
    /// <exception cref="Common.Exceptions.ApiException">503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/> if no current rate exists.</exception>
    Task<FuelPriceRateResponseDto> GetCurrentFuelPrice(FuelType fuelType, CancellationToken cancellationToken = default);

    /// <summary>The current rate for every fuel type that has one. Never throws — an empty list is a valid result.</summary>
    Task<List<FuelPriceRateResponseDto>> GetAllCurrentFuelPrices(CancellationToken cancellationToken = default);

    /// <summary>Every row (including future-dated, superseded, and soft-deleted) for <paramref name="fuelType"/>, newest <c>EffectiveFrom</c> first.</summary>
    Task<List<FuelPriceRateResponseDto>> GetFuelPriceHistory(FuelType fuelType, CancellationToken cancellationToken = default);

    /// <summary>The single current figure for <paramref name="classLabel"/> — the latest non-future-dated, non-deleted <c>EffectiveFrom</c> row for that class.</summary>
    /// <exception cref="Common.Exceptions.ApiException">503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/> if no current figure exists.</exception>
    Task<VehicleClassEfficiencyResponseDto> GetCurrentVehicleClassEfficiency(VehicleClass classLabel, CancellationToken cancellationToken = default);

    /// <summary>The current figure for every vehicle class that has one. Never throws — an empty list is a valid result.</summary>
    Task<List<VehicleClassEfficiencyResponseDto>> GetAllCurrentVehicleClassEfficiencies(CancellationToken cancellationToken = default);

    /// <summary>Every row (including future-dated, superseded, and soft-deleted) for <paramref name="classLabel"/>, newest <c>EffectiveFrom</c> first.</summary>
    Task<List<VehicleClassEfficiencyResponseDto>> GetVehicleClassEfficiencyHistory(VehicleClass classLabel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves which vehicle-class tier a given load falls into, among the current tiers. Each tier
    /// has independent weight and volume bands; the selected tier is whichever dimension (weight or
    /// volume) demands the larger class, so a bulky-but-light load is correctly upsized instead of
    /// receiving an inappropriate weight-only tier.
    /// </summary>
    /// <exception cref="Common.Exceptions.ApiException">503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/> if no configured tier's weight band, or none's volume band, covers the given values.</exception>
    Task<VehicleClassEfficiencyResponseDto> GetTierForWeightAndVolume(decimal weightKg, decimal volumeM3, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new, current <see cref="Entities.FuelPriceRate"/> row. Never updates an existing row.</summary>
    /// <param name="request">The new rate's content.</param>
    /// <param name="actingUserId">The authenticated Admin's id, recorded as <c>SetByUserId</c>.</param>
    Task<FuelPriceRateResponseDto> CreateFuelPriceRate(CreateFuelPriceRateDto request, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new, current <see cref="Entities.VehicleClassEfficiency"/> row after verifying its
    /// weight band and its volume band each neither overlap nor gap against the other classes' current
    /// bands in that same dimension. Never updates an existing row.
    /// </summary>
    /// <param name="request">The new figure's content.</param>
    /// <param name="actingUserId">The authenticated Admin's id, recorded as <c>SetByUserId</c>.</param>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 400 <see cref="Common.Errors.ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP"/> or
    /// <see cref="Common.Errors.ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP"/> if the new weight or
    /// volume band conflicts with the other classes' current bands in that dimension.
    /// </exception>
    Task<VehicleClassEfficiencyResponseDto> CreateVehicleClassEfficiency(CreateVehicleClassEfficiencyDto request, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a <see cref="Entities.FuelPriceRate"/> row via an ordinary <c>UPDATE</c> (never a
    /// raw <c>DELETE</c>). Returns a plain success confirmation, not the soft-deleted row — fetch
    /// <see cref="GetFuelPriceHistory"/> if the deleted values are still needed.
    /// </summary>
    /// <param name="fuelPriceRateId">The row's id.</param>
    /// <param name="actingUserId">The authenticated Admin's id, recorded as <c>DeletedByUserId</c>.</param>
    /// <exception cref="Common.Exceptions.ApiException">404 if no such row exists; 422 if it is already soft-deleted.</exception>
    Task<PricingConfigDeleteResponseDto> SoftDeleteFuelPriceRate(Guid fuelPriceRateId, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a <see cref="Entities.VehicleClassEfficiency"/> row via an ordinary <c>UPDATE</c>
    /// (never a raw <c>DELETE</c>). Returns a plain success confirmation, not the soft-deleted row —
    /// fetch <see cref="GetVehicleClassEfficiencyHistory"/> if the deleted values are still needed.
    /// </summary>
    /// <param name="vehicleClassEfficiencyId">The row's id.</param>
    /// <param name="actingUserId">The authenticated Admin's id, recorded as <c>DeletedByUserId</c>.</param>
    /// <exception cref="Common.Exceptions.ApiException">404 if no such row exists; 422 if it is already soft-deleted.</exception>
    Task<PricingConfigDeleteResponseDto> SoftDeleteVehicleClassEfficiency(Guid vehicleClassEfficiencyId, Guid actingUserId, CancellationToken cancellationToken = default);
}
