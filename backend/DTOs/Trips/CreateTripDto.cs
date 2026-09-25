using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Payload for dispatching and creating a new Trip (<c>POST /api/v1/trips</c>).
/// Assigns an agency vehicle and driver to fulfill an accepted or proposed assignment.
/// </summary>
public class CreateTripDto
{
    /// <summary>The Assignment id that this Trip fulfills.</summary>
    [Required]
    public Guid AssignmentId { get; set; }

    /// <summary>The agency vehicle assigned to execute this Trip.</summary>
    [Required]
    public Guid VehicleId { get; set; }

    /// <summary>The agency driver assigned to execute this Trip.</summary>
    [Required]
    public Guid DriverId { get; set; }

    /// <summary>Optional dispatch instructions or notes for the driver.</summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
