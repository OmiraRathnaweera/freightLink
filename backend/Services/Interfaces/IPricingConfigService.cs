using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Read/create/soft-delete operations on the ADR-019 pricing-config reference tables
/// (<see cref="Entities.FuelPriceRate"/>, <see cref="Entities.VehicleClassEfficiency"/>). Both tables
/// are append-only: "editing" a rate means inserting a new row with a later <c>EffectiveFrom</c>; an
/// existing row is only ever touched by <see cref="SoftDeleteFuelPriceRate"/>/
/// <see cref="SoftDeleteVehicleClassEfficiency"/>, which issue an ordinary <c>UPDATE</c>, never a raw
/// <c>DELETE</c> (the database's <c>trg_deny_delete_*</c> triggers are a backstop, not the mechanism).
/// This service performs no authentication — the acting Admin's id is accepted as a plain parameter,
/// sourced by the caller (in practice, <c>AdminPricingController</c>) from validated JWT claims.
/// </summary>
public interface IPricingConfigService
{
    /// <summary>
    /// The single current rate for <paramref name="fuelType"/> — the latest <c>EffectiveFrom</c>
    /// row among non-deleted rows for that fuel type. Used internally by Component A's price
    /// estimator, where a definite answer is required.
    /// </summary>
    /// <exception cref="Common.Exceptions.ApiException">503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/> if no current rate exists.</exception>
    Task<FuelPriceRateResponseDto> GetCurrentFuelPrice(FuelType fuelType, CancellationToken cancellationToken = default);

    /// <summary>The current (latest non-deleted <c>EffectiveFrom</c>) rate for every fuel type that has one. Never throws — an empty list is a valid result.</summary>
    Task<List<FuelPriceRateResponseDto>> GetAllCurrentFuelPrices(CancellationToken cancellationToken = default);

    /// <summary>Every row (including superseded and soft-deleted) for <paramref name="fuelType"/>, newest <c>EffectiveFrom</c> first.</summary>
    Task<List<FuelPriceRateResponseDto>> GetFuelPriceHistory(FuelType fuelType, CancellationToken cancellationToken = default);

    /// <summary>
    /// The single current figure for <paramref name="classLabel"/> — the latest <c>EffectiveFrom</c>
    /// row among non-deleted rows for that class.
    /// </summary>
    /// <exception cref="Common.Exceptions.ApiException">503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/> if no current figure exists.</exception>
    Task<VehicleClassEfficiencyResponseDto> GetCurrentVehicleClassEfficiency(VehicleClass classLabel, CancellationToken cancellationToken = default);

    /// <summary>The current (latest non-deleted <c>EffectiveFrom</c>) figure for every vehicle class that has one. Never throws — an empty list is a valid result.</summary>
    Task<List<VehicleClassEfficiencyResponseDto>> GetAllCurrentVehicleClassEfficiencies(CancellationToken cancellationToken = default);

    /// <summary>Every row (including superseded and soft-deleted) for <paramref name="classLabel"/>, newest <c>EffectiveFrom</c> first.</summary>
    Task<List<VehicleClassEfficiencyResponseDto>> GetVehicleClassEfficiencyHistory(VehicleClass classLabel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves which vehicle-class tier a given <c>Load.WeightKg</c> falls into, among the current
    /// (non-deleted, latest-<c>EffectiveFrom</c>-per-class) tiers, and returns that tier's figure.
    /// </summary>
    /// <exception cref="Common.Exceptions.ApiException">503 <see cref="Common.Errors.ErrorCode.PRICING_CONFIG_MISSING"/> if no configured tier's band contains <paramref name="weightKg"/>.</exception>
    Task<VehicleClassEfficiencyResponseDto> GetTierForWeight(decimal weightKg, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new, current <see cref="Entities.FuelPriceRate"/> row. Never updates an existing row.</summary>
    /// <param name="request">The new rate's content.</param>
    /// <param name="actingUserId">The authenticated Admin's id, recorded as <c>SetByUserId</c>.</param>
    Task<FuelPriceRateResponseDto> CreateFuelPriceRate(CreateFuelPriceRateDto request, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new, current <see cref="Entities.VehicleClassEfficiency"/> row after verifying its
    /// payload band neither overlaps nor gaps against the other classes' current bands. Never updates
    /// an existing row.
    /// </summary>
    /// <param name="request">The new figure's content.</param>
    /// <param name="actingUserId">The authenticated Admin's id, recorded as <c>SetByUserId</c>.</param>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 400 <see cref="Common.Errors.ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP"/> or
    /// <see cref="Common.Errors.ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP"/> if the new band
    /// conflicts with the other classes' current bands.
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
