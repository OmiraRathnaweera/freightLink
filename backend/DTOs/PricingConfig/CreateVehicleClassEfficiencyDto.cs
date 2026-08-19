using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// Payload for <c>POST /api/v1/admin/pricing/vehicle-efficiency</c> — always inserts a new, current
/// <see cref="Entities.VehicleClassEfficiency"/> row (append-only versioning per ADR-019); never
/// updates an existing row's values. Carries no <c>SetByUserId</c> — that comes from the authenticated
/// Admin's access-token claims in the controller, never from client input.
/// </summary>
public class CreateVehicleClassEfficiencyDto
{
    /// <summary>
    /// The vehicle-class tier this row prices. Nullable so <see cref="RequiredAttribute"/> actually
    /// rejects an omitted JSON field instead of silently binding it to the enum's default member
    /// (mirrors why coordinate fields on <c>CreateLoadDto</c> are nullable).
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleClass? ClassLabel { get; set; }

    /// <summary>
    /// Lower bound (inclusive) of the payload band, in kilograms. Nullable so
    /// <see cref="RequiredAttribute"/> actually rejects an omitted JSON field — <c>0</c> is a valid,
    /// in-range value for this field (the lowest tier's band starts at <c>0</c>), so a non-nullable
    /// <see cref="decimal"/> would let an omitted field silently bind to <c>0</c> and pass validation
    /// unnoticed (the same trap <c>CreateLoadDto.PickupLat</c>'s doc comment calls out).
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal? MinPayloadKg { get; set; }

    /// <summary>
    /// Upper bound (exclusive) of the payload band, in kilograms. Omit for an open-ended top tier
    /// (e.g. ContainerTruck). Must be strictly greater than <see cref="MinPayloadKg"/> when present —
    /// that cross-field rule (mirroring the DB's <c>ck_vce_payload_bounds</c> CHECK) is enforced by
    /// the service layer, not here.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? MaxPayloadKg { get; set; }

    /// <summary>
    /// Lower bound (inclusive) of the cargo-volume band, in cubic meters. Nullable so
    /// <see cref="RequiredAttribute"/> actually rejects an omitted JSON field — same reasoning as
    /// <see cref="MinPayloadKg"/>. Independent of the payload band: a load's tier is resolved from
    /// whichever dimension (weight or volume) demands the larger class.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal? MinVolumeM3 { get; set; }

    /// <summary>
    /// Upper bound (exclusive) of the volume band, in cubic meters. Omit for an open-ended top tier.
    /// Must be strictly greater than <see cref="MinVolumeM3"/> when present (mirrors the DB's
    /// <c>ck_vce_volume_bounds</c> CHECK) — enforced by the service layer, not here.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? MaxVolumeM3 { get; set; }

    /// <summary>Fuel consumption for this tier, in litres per 100 km. Must be greater than zero, mirroring the DB's <c>ck_vce_consumption_positive</c> CHECK.</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "FuelConsumptionLPer100Km must be greater than zero")]
    public decimal FuelConsumptionLPer100Km { get; set; }

    /// <summary>Citation for where this figure came from.</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Source { get; set; } = string.Empty;

    /// <summary>When this figure takes effect. The current figure is the latest non-deleted row's.</summary>
    [Required]
    public DateTimeOffset EffectiveFrom { get; set; }
}
