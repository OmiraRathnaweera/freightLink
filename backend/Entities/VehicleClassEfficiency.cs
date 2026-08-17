using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// One sourced, dated fuel-efficiency figure for a <see cref="VehicleClass"/> tier, keyed by a
/// <c>Load.WeightKg</c>/<c>VolumeM3</c> payload band rather than <c>Vehicle.VehicleType</c> — the
/// actual <c>Vehicle</c> isn't selected until <c>Trip</c> creation, after Agent 3 has already priced
/// the load (see ADR-019). Append-only, same versioning and soft-delete rules as
/// <see cref="FuelPriceRate"/>: "editing" a row means inserting a new one with a later
/// <see cref="EffectiveFrom"/>, and <c>trg_deny_delete_vehicleclassefficiencies</c> backstops the
/// service-layer soft delete against a literal <c>DELETE</c>.
/// </summary>
public class VehicleClassEfficiency
{
    /// <summary>Primary key.</summary>
    public Guid VehicleClassEfficiencyId { get; set; }

    /// <summary>The vehicle-class tier this row prices.</summary>
    public VehicleClass ClassLabel { get; set; }

    /// <summary>Lower bound (inclusive) of the payload band this tier covers, in kilograms.</summary>
    public decimal MinPayloadKg { get; set; }

    /// <summary>Upper bound (exclusive) of the payload band; null for an open-ended top tier (e.g. ContainerTruck).</summary>
    public decimal? MaxPayloadKg { get; set; }

    /// <summary>Fuel consumption for this tier, in litres per 100 km.</summary>
    public decimal FuelConsumptionLPer100Km { get; set; }

    /// <summary>Citation for where this figure came from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this figure took effect; the current figure is the latest non-deleted row's.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The Admin who recorded this figure.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the row was last updated (soft delete is the only expected update).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null while the row is live.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The Admin who soft-deleted this row; null while the row is live.</summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>Navigation to the user who recorded this figure.</summary>
    public User SetByUser { get; set; } = null!;

    /// <summary>Navigation to the user who soft-deleted this row, if any.</summary>
    public User? DeletedByUser { get; set; }
}
