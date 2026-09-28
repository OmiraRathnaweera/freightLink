namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Consolidated payload for the Shipper Match Approval & AI Workflow Console (GET /api/v1/loads/{loadId}/match).
/// Provides Agent 3's recommendation, candidate comparison, pricing breakdown, validation, and workflow audit steps.
/// </summary>
public class LoadMatchRecommendationDto
{
    public Guid LoadId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string LoadStatus { get; set; } = string.Empty;
    public Guid? WorkflowRunId { get; set; }
    public int AttemptNo { get; set; } = 1;
    public string WorkflowStatus { get; set; } = "PendingReview";
    public string Objective { get; set; } = string.Empty;
    public string? ShipperMessage { get; set; }

    public RecommendedAgencyDto? RecommendedAgency { get; set; }
    public List<AlternateCandidateAgencyDto> AlternateCandidates { get; set; } = new();
    public ValidationSummaryDto? Validation { get; set; }
    public List<WorkflowStepSummaryDto> Steps { get; set; } = new();
    public AssignmentSummaryDto? ExistingAssignment { get; set; }
}

public class RecommendedAgencyDto
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string YardAddress { get; set; } = string.Empty;
    public decimal YardLat { get; set; }
    public decimal YardLng { get; set; }
    public string SuggestedVehicleClass { get; set; } = "MediumLorry";
    public decimal? PositioningDistanceKm { get; set; }
    public int? PositioningEtaMinutes { get; set; }
    public decimal? CargoDistanceKm { get; set; }
    public decimal EstimatedPrice { get; set; }
    public string SelectionJustification { get; set; } = string.Empty;
    public Guid? AssignedVehicleId { get; set; }
    public string? AssignedVehicleRegNo { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public string? AssignedDriverName { get; set; }
}

public class AlternateCandidateAgencyDto
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string YardAddress { get; set; } = string.Empty;
    public int Rank { get; set; }
    public decimal? PositioningDistanceKm { get; set; }
    public int? PositioningEtaMinutes { get; set; }
    public bool Eligible { get; set; } = true;
    public string? RejectionReason { get; set; }
}

public class ValidationSummaryDto
{
    public string Recommendation { get; set; } = "Approve";
    public string Explanation { get; set; } = string.Empty;
    public List<ValidationCheckItemDto> Checks { get; set; } = new();
}

public class ValidationCheckItemDto
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class WorkflowStepSummaryDto
{
    public int StepNo { get; set; }
    public string AgentRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
}

public class AssignmentSummaryDto
{
    public Guid AssignmentId { get; set; }
    public Guid AgencyId { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ProposedPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
