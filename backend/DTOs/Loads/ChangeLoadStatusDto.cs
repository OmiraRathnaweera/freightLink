using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Payload for <c>PATCH /api/v1/loads/{id}/status</c> — the single endpoint through which a Shipper
/// changes their own load's status. Only two target statuses are accepted: <see cref="LoadStatus.Posted"/>
/// (publishing a <c>Draft</c> load) and <see cref="LoadStatus.Cancelled"/> (cancelling from any status
/// <see cref="Common.Domain.LoadStatusTransitionRules.CanCancel"/> permits) — every later status
/// (<c>Matched</c> and beyond) is reached only by internal processes (the AI matching workflow, trip
/// events), never by this endpoint, regardless of what <see cref="Common.Domain.LoadStatusTransitionRules"/>'s
/// transition graph otherwise permits.
/// </summary>
public class ChangeLoadStatusDto
{
    /// <summary>
    /// The status to transition the load to. Nullable so <see cref="RequiredAttribute"/> actually
    /// rejects an omitted JSON field instead of silently binding it to the enum's default member
    /// (mirrors why <c>ClassLabel</c> on <c>CreateVehicleClassEfficiencyDto</c> is nullable).
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LoadStatus? Status { get; set; }

    /// <summary>
    /// Required when <see cref="Status"/> is <see cref="LoadStatus.Cancelled"/> (enforced by the
    /// service layer, not a DataAnnotation, since it's conditional on <see cref="Status"/>); ignored
    /// for every other target status.
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}
