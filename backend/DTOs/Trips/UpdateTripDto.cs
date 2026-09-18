using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Payload for updating an existing Trip prior to departure (<c>PUT /api/v1/trips/{id}</c>).
/// Allows reassigning vehicle and/or driver while the trip is still in <c>Assigned</c> status.
/// </summary>
public class UpdateTripDto
{
    /// <summary>New vehicle to assign, if updating vehicle assignment.</summary>
    public Guid? VehicleId { get; set; }

    /// <summary>New driver to assign, if updating driver assignment.</summary>
    public Guid? DriverId { get; set; }

    /// <summary>Optional notes explaining the reassignment or modification.</summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
