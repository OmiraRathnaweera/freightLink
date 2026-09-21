using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Request payload for approving a proposed assignment or load (POST /api/v1/assignments/{id}/approve).
/// Finalizes the AI proposed match into an operational Assignment (Accepted) and Trip (Assigned) per Y3S01-96.
/// </summary>
public class ApproveAssignmentDto
{
    /// <summary>Optional specific vehicle id from the agency to assign to the trip. If omitted, first available active vehicle is auto-selected.</summary>
    public Guid? VehicleId { get; set; }

    /// <summary>Optional specific driver id from the agency to assign to the trip. If omitted, first available active driver is auto-selected.</summary>
    public Guid? DriverId { get; set; }

    /// <summary>Optional notes / approval remarks recorded in the TripEvent and ApprovalDecision.</summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
