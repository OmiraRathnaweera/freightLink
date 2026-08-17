namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Full single-resource response for <c>POST /api/v1/loads</c>, <c>GET /api/v1/loads/{id}</c>, and
/// <c>PUT /api/v1/loads/{id}</c>. Never exposes the <c>Load</c> entity directly.
/// </summary>
public class LoadResponseDto
{
    /// <summary>The load's unique id.</summary>
    public Guid LoadId { get; set; }

    /// <summary>The id of the Shipper user who owns this load.</summary>
    public Guid ShipperUserId { get; set; }

    /// <summary>
    /// Display name (<see cref="Entities.User.FullName"/>) of the Shipper user who owns this load, for
    /// Admin views that list/inspect loads across multiple shippers. Falls back to <c>"Unknown"</c> if
    /// the owning user record could not be resolved.
    /// </summary>
    public string ShipperName { get; set; } = string.Empty;

    /// <summary>Server-generated, unique reference code for this load.</summary>
    public string ReferenceCode { get; set; } = string.Empty;

    /// <summary>Free-text description of the cargo being shipped.</summary>
    public string CargoDescription { get; set; } = string.Empty;

    /// <summary>Cargo weight in kilograms.</summary>
    public decimal WeightKg { get; set; }

    /// <summary>Cargo volume in cubic meters.</summary>
    public decimal VolumeM3 { get; set; }

    /// <summary>Human-readable pickup address.</summary>
    public string PickupAddress { get; set; } = string.Empty;

    /// <summary>Pickup latitude.</summary>
    public decimal PickupLat { get; set; }

    /// <summary>Pickup longitude.</summary>
    public decimal PickupLng { get; set; }

    /// <summary>Human-readable dropoff address.</summary>
    public string DropoffAddress { get; set; } = string.Empty;

    /// <summary>Dropoff latitude.</summary>
    public decimal DropoffLat { get; set; }

    /// <summary>Dropoff longitude.</summary>
    public decimal DropoffLng { get; set; }

    /// <summary>Start of the pickup window.</summary>
    public DateTimeOffset PickupWindowStart { get; set; }

    /// <summary>End of the pickup window.</summary>
    public DateTimeOffset PickupWindowEnd { get; set; }

    /// <summary>System-computed price estimate, if one has been generated via the estimate endpoint.</summary>
    public decimal? EstimatedPrice { get; set; }

    /// <summary>The load's current status (e.g. "Draft", "Posted", "Matched").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The id of the current agent workflow run matching this load, if one exists.</summary>
    public Guid? WorkflowRunId { get; set; }

    /// <summary>When the load was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the load was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The load's full status-change audit trail, newest first. Only populated by
    /// <c>GET /api/v1/loads/{id}</c> — <c>POST</c>/<c>PUT</c>/<c>PATCH .../cancel</c> responses leave
    /// this as an empty list, since the caller already knows the single transition it just made.
    /// </summary>
    public List<LoadStatusHistoryResponseDto> StatusHistory { get; set; } = new();
}
