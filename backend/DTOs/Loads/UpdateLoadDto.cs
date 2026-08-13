using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Payload for <c>PUT /api/v1/loads/{id}</c> — edits an existing load's content. Carries no status
/// field: status transitions (including the rule that a load is only editable while
/// <c>Draft</c>/<c>Posted</c>) are a service-layer concern, not part of this DTO.
/// </summary>
public class UpdateLoadDto
{
    /// <summary>Free-text description of the cargo being shipped.</summary>
    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string CargoDescription { get; set; } = string.Empty;

    /// <summary>
    /// Cargo weight in kilograms. Must be greater than zero, mirroring the DB's <c>ck_load_weight</c>
    /// CHECK via <see cref="LoadRanges.MinWeightKg"/>/<see cref="LoadRanges.MaxWeightKg"/>.
    /// </summary>
    [Required]
    [Range(LoadRanges.MinWeightKg, LoadRanges.MaxWeightKg)]
    public decimal WeightKg { get; set; }

    /// <summary>
    /// Cargo volume in cubic meters. Must be greater than zero, mirroring the DB's
    /// <c>ck_load_volume</c> CHECK via <see cref="LoadRanges.MinVolumeM3"/>/<see cref="LoadRanges.MaxVolumeM3"/>.
    /// </summary>
    [Required]
    [Range(LoadRanges.MinVolumeM3, LoadRanges.MaxVolumeM3)]
    public decimal VolumeM3 { get; set; }

    /// <summary>Human-readable pickup address.</summary>
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string PickupAddress { get; set; } = string.Empty;

    /// <summary>
    /// Pickup latitude, mirroring the DB's <c>ck_load_pickup_lat</c> CHECK. Nullable so
    /// <see cref="RequiredAttribute"/> actually rejects an omitted JSON field instead of silently
    /// binding it to <c>0</c> (a non-nullable <see cref="decimal"/> passes <c>[Required]</c>
    /// unconditionally, since <c>0</c> is never treated as "missing" for a value type).
    /// </summary>
    [Required]
    [Range(LoadRanges.MinLatitude, LoadRanges.MaxLatitude)]
    public decimal? PickupLat { get; set; }

    /// <summary>Pickup longitude, mirroring the DB's <c>ck_load_pickup_lng</c> CHECK. See <see cref="PickupLat"/> for why this is nullable.</summary>
    [Required]
    [Range(LoadRanges.MinLongitude, LoadRanges.MaxLongitude)]
    public decimal? PickupLng { get; set; }

    /// <summary>Human-readable dropoff address.</summary>
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string DropoffAddress { get; set; } = string.Empty;

    /// <summary>Dropoff latitude, mirroring the DB's <c>ck_load_dropoff_lat</c> CHECK. See <see cref="PickupLat"/> for why this is nullable.</summary>
    [Required]
    [Range(LoadRanges.MinLatitude, LoadRanges.MaxLatitude)]
    public decimal? DropoffLat { get; set; }

    /// <summary>Dropoff longitude, mirroring the DB's <c>ck_load_dropoff_lng</c> CHECK. See <see cref="PickupLat"/> for why this is nullable.</summary>
    [Required]
    [Range(LoadRanges.MinLongitude, LoadRanges.MaxLongitude)]
    public decimal? DropoffLng { get; set; }

    /// <summary>
    /// Start of the pickup window. Must be earlier than <see cref="PickupWindowEnd"/>, but that
    /// cross-field rule (<c>ck_load_window</c>) is enforced by the service layer, not here.
    /// </summary>
    [Required]
    public DateTimeOffset PickupWindowStart { get; set; }

    /// <summary>End of the pickup window. See <see cref="PickupWindowStart"/> for the deferred ordering rule.</summary>
    [Required]
    public DateTimeOffset PickupWindowEnd { get; set; }
}
