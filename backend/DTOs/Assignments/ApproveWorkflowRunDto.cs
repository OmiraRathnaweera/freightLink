using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Request payload for approving an AI agent workflow run from the admin console (POST /api/v1/admin/agent-workflows/{workflowRunId}/approve).
/// Finalizes the proposed match candidate into an operational Assignment (Accepted) and Trip (Assigned) per Y3S01-96.
/// </summary>
public class ApproveWorkflowRunDto
{
    /// <summary>Optional specific agency id if overriding the #1 candidate from the workflow run.</summary>
    public Guid? AgencyId { get; set; }

    /// <summary>Optional specific vehicle id from the agency to assign to the trip.</summary>
    public Guid? VehicleId { get; set; }

    /// <summary>Optional specific driver id from the agency to assign to the trip.</summary>
    public Guid? DriverId { get; set; }

    /// <summary>Optional notes / approval remarks recorded in the TripEvent and ApprovalDecision.</summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
