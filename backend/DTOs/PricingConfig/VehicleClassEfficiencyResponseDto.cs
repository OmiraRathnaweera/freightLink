using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>Wire-facing representation of a <see cref="Entities.VehicleClassEfficiency"/> row.</summary>
public class VehicleClassEfficiencyResponseDto
{
    /// <summary>The row's id.</summary>
    public Guid VehicleClassEfficiencyId { get; set; }

    /// <summary>The vehicle-class tier this row prices.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleClass ClassLabel { get; set; }

    /// <summary>Lower bound (inclusive) of the payload band, in kilograms.</summary>
    public decimal MinPayloadKg { get; set; }

    /// <summary>Upper bound (exclusive) of the payload band; null for an open-ended top tier.</summary>
    public decimal? MaxPayloadKg { get; set; }

    /// <summary>Lower bound (inclusive) of the cargo-volume band, in cubic meters.</summary>
    public decimal MinVolumeM3 { get; set; }

    /// <summary>Upper bound (exclusive) of the volume band; null for an open-ended top tier.</summary>
    public decimal? MaxVolumeM3 { get; set; }

    /// <summary>Fuel consumption for this tier, in litres per 100 km.</summary>
    public decimal FuelConsumptionLPer100Km { get; set; }

    /// <summary>Citation for where this figure came from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this figure took/takes effect.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The id of the Admin who recorded this figure.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>
    /// Display name (<see cref="Entities.User.FullName"/>) of the Admin who recorded this figure.
    /// Falls back to <c>"Unknown"</c> if the setting user record could not be resolved.
    /// </summary>
    public string SetByUserName { get; set; } = string.Empty;

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null unless this row has been soft-deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The id of the Admin who soft-deleted this row; null unless this row has been soft-deleted.</summary>
    public Guid? DeletedByUserId { get; set; }
}
