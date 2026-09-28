"""Contract tests: Python -> backend, and backend -> Python (plans/06-testing-and-
verification-plan.md §1).

These don't spin up the real ASP.NET Core backend - that's what the C# integration
tests (backend/FreightLink.Api.Tests/Integration/InternalAgentWorkflowRunsControllerTests.cs,
InternalPricingControllerTests.cs) already do, using these same camelCase field names
directly. What this file proves is the other half: that each Python request schema's
*actual serialized JSON* has exactly the field names the corresponding C# DTO expects
(no more, no less), and that Python's response schemas can parse exactly the JSON shape
the corresponding C# response DTO actually produces. If either side renames, adds, or
drops a field without updating the other, one of these tests breaks - closing the class
of silent-drift bugs the Sep 27 2026 audit found (candidates route, tool telemetry,
pricing response shape all mismatched at once).
"""

import os
import sys
from datetime import UTC, datetime
from uuid import uuid4

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

os.environ.setdefault("SHARED_SECRET", "test-shared-secret")
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-api-key")

from freightlink_agent.schemas.callback import MatchCandidateRequest, ReportAgentStepRequest
from freightlink_agent.schemas.matching import (
    CreateToolCallRequest,
    EstimatePricingRequest,
    EstimatePricingResponse,
)
from freightlink_agent.schemas.workflow import CreateWorkflowRunRequest, CreateWorkflowRunResponse


def _keys(model) -> set[str]:
    return set(model.model_dump(mode="json", by_alias=True).keys())


def test_create_workflow_run_request_matches_CreateAgentWorkflowRunRequestDto():
    """backend/DTOs/Internal/CreateAgentWorkflowRunRequestDto.cs: LoadId, TriggeredByUserId, AttemptNo."""
    request = CreateWorkflowRunRequest(load_id=uuid4(), triggered_by_user_id=uuid4(), attempt_no=1)
    assert _keys(request) == {"loadId", "triggeredByUserId", "attemptNo"}


def test_create_workflow_run_response_parses_CreateAgentWorkflowRunResponseDto_shape():
    """backend/DTOs/Internal/CreateAgentWorkflowRunResponseDto.cs: WorkflowRunId."""
    backend_response = {"workflowRunId": str(uuid4())}
    parsed = CreateWorkflowRunResponse.model_validate(backend_response)
    assert parsed.workflow_run_id is not None


def test_report_agent_step_request_matches_ReportAgentStepRequestDto():
    """backend/DTOs/Internal/ReportAgentStepRequestDto.cs's exact field set."""
    request = ReportAgentStepRequest(
        step_no=1,
        agent_role="Planner",
        status="Succeeded",
        input_json="{}",
        output_json="{}",
        error_message=None,
        duration_ms=100,
        started_at=datetime.now(UTC),
        completed_at=datetime.now(UTC),
    )
    assert _keys(request) == {
        "stepNo",
        "agentRole",
        "status",
        "inputJson",
        "outputJson",
        "errorMessage",
        "durationMs",
        "startedAt",
        "completedAt",
    }


def test_match_candidate_request_matches_MatchCandidateDto():
    """backend/DTOs/Internal/Candidates/MatchCandidateDto.cs's exact field set."""
    request = MatchCandidateRequest(agency_id=uuid4(), rank=1, eligible=True, eligibility_score=95.0)
    assert _keys(request) == {"agencyId", "rank", "eligible", "eligibilityScore", "rejectionReason"}


def test_create_tool_call_request_matches_CreateToolCallRequestDto():
    """backend/DTOs/Internal/ToolCalls/CreateToolCallRequestDto.cs's exact field set."""
    request = CreateToolCallRequest(
        tool_name="get_route_and_eta",
        attempt_no=1,
        request_json="{}",
        response_json="{}",
        success=True,
        called_at=datetime.now(UTC),
    )
    assert _keys(request) == {
        "agentStepId",
        "toolName",
        "attemptNo",
        "requestJson",
        "responseJson",
        "success",
        "httpStatusCode",
        "durationMs",
        "errorMessage",
        "calledAt",
    }


def test_estimate_pricing_request_matches_EstimatePricingRequestDto():
    """backend/DTOs/Internal/EstimatePricingRequestDto.cs's exact field set."""
    request = EstimatePricingRequest(load_id=uuid4(), suggested_vehicle_class="MediumLorry", distance_km=10.0)
    assert _keys(request) == {"loadId", "suggestedVehicleClass", "distanceKm"}


def test_estimate_pricing_response_parses_PricingEstimateResponseDto_shape():
    """backend/DTOs/Internal/PricingEstimateResponseDto.cs's exact field set - this is the
    contract the audit found broken (Agent 3's fallback built a response with `currency`,
    `suggested_vehicle_class`, `breakdown` instead of these real fields)."""
    backend_response = {
        "loadId": str(uuid4()),
        "estimatedPrice": 29547.50,
        "distanceKm": 115.0,
        "vehicleClass": "MediumLorry",
        "ratePerKm": 126.50,
        "ratePerKg": 4.0,
        "baseFare": 5000.0,
    }
    parsed = EstimatePricingResponse.model_validate(backend_response)
    assert parsed.estimated_price == 29547.50
    assert parsed.vehicle_class == "MediumLorry"
