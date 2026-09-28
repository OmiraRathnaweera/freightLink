import os
import sys
import uuid
from unittest.mock import AsyncMock, MagicMock, patch
import pytest
from httpx import ASGITransport, AsyncClient

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

from freightlink_agent.graph.pipeline import get_pipeline
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.main import app
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    EstimatePricingResponse,
    RouteAndEtaResponse,
)


@pytest.fixture
def mock_planner_llm():
    mock_llm = MagicMock()
    mock_llm.plan = AsyncMock(
        return_value={
            "objective": "Find and assign suitable carrier for palletized freight.",
            "steps": [
                "Evaluate candidate agencies",
                "Select agency via routing",
                "Validate and get shipper approval",
                "Notify agency",
            ],
        }
    )
    # Deliberately no run_tool_calling_selection mock: calling it on this plain MagicMock
    # raises (not awaitable), which agents.matching_pricing.run() catches and falls back to
    # its own deterministic selection algorithm - proven separately, with a real tool-
    # calling mock, in tests/test_matching_pricing.py.
    return mock_llm


@pytest.fixture
def sample_candidates():
    c1 = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Peliyagoda Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="12 Negombo Rd, Peliyagoda",
        available_vehicle_classes=["MediumLorry", "ContainerTruck"],
        available_vehicles=[
            {"vehicleId": str(uuid.uuid4()), "registrationNo": "WP-CAB-4521", "capacityKg": 6500, "volumeM3": 30},
            {"vehicleId": str(uuid.uuid4()), "registrationNo": "WP-DA-8920", "capacityKg": 18000, "volumeM3": 65},
        ],
        active_drivers=[
            {"driverId": str(uuid.uuid4()), "name": "Suneth Alwis", "licenceNo": "B-8839210"},
        ],
    )
    c2 = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Colombo Metro Carriers",
        yard_lat=6.9319,
        yard_lng=79.8478,
        yard_address="45 Fort, Colombo",
        available_vehicle_classes=["MiniTruck", "MediumLorry"],
        available_vehicles=[
            {"vehicleId": str(uuid.uuid4()), "registrationNo": "WP-ND-1102", "capacityKg": 1400, "volumeM3": 5.5},
            {"vehicleId": str(uuid.uuid4()), "registrationNo": "WP-LC-1029", "capacityKg": 3500, "volumeM3": 14},
        ],
        active_drivers=[
            {"driverId": str(uuid.uuid4()), "name": "Priyantha Kumara", "licenceNo": "B-7740192"},
        ],
    )
    return [c1, c2]


@pytest.mark.anyio
async def test_pipeline_happy_path_all_four_agents(mock_planner_llm, sample_candidates):
    load_id = uuid.uuid4()
    user_id = uuid.uuid4()

    state = WorkflowState(
        load_id=load_id,
        triggered_by_user_id=user_id,
        attempt_no=1,
        load_context={
            "pickupLat": 6.9271,
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,
            "dropoffLng": 80.6337,
            "weightKg": 2500,
            "volumeM3": 8.0,
            "cargoDescription": "Industrial parts",
        },
        candidate_shortlist=sample_candidates,
    )

    mock_pricing_response = EstimatePricingResponse(
        load_id=load_id,
        estimated_price=24500.0,
        distance_km=116.0,
        vehicle_class="MediumLorry",
        rate_per_km=130.0,
        rate_per_kg=1.8,
        base_fare=2000.0,
    )

    mock_step1_report = AsyncMock()
    mock_step2_report = AsyncMock()
    mock_step3_report = AsyncMock()
    mock_step4_report = AsyncMock()
    mock_record_match_candidates = AsyncMock()

    with (
        patch("freightlink_agent.agents.planner.get_llm", return_value=mock_planner_llm),
        patch("freightlink_agent.agents.matching_pricing.get_llm", return_value=mock_planner_llm),
        patch(
            "freightlink_agent.agents.matching_pricing.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200, "durationMs": 45})),
        ),
        patch(
            "freightlink_agent.agents.planner.create_workflow_run",
            new=AsyncMock(return_value=MagicMock(workflow_run_id=uuid.uuid4())),
        ),
        patch("freightlink_agent.agents.planner.report", new=mock_step1_report),
        patch("freightlink_agent.agents.domain_analysis.report", new=mock_step2_report),
        patch("freightlink_agent.agents.domain_analysis.record_match_candidates", new=mock_record_match_candidates),
        patch("freightlink_agent.agents.matching_pricing.report", new=mock_step3_report),
        patch("freightlink_agent.agents.validation_safety.report", new=mock_step4_report),
        patch("freightlink_agent.agents.matching_pricing.record_tool_call", new=AsyncMock()),
    ):
        pipeline = get_pipeline()
        result = await pipeline.ainvoke(state)

    assert result.get("failed") is False
    assert result.get("plan") is not None
    assert "objective" in result["plan"]

    # Verify that EVERY agent explicitly reported its own AgentStep row with audit data
    assert mock_step1_report.await_count == 1
    assert mock_step1_report.call_args.kwargs["step_no"] == 1
    assert mock_step1_report.call_args.kwargs["agent_role"] == "Planner"
    assert mock_step1_report.call_args.kwargs["status"] == "Succeeded"
    assert mock_step1_report.call_args.kwargs["input_data"] is not None
    assert mock_step1_report.call_args.kwargs["output_data"] is not None

    assert mock_step2_report.await_count == 1
    assert mock_step2_report.call_args.kwargs["step_no"] == 2
    assert mock_step2_report.call_args.kwargs["agent_role"] == "DomainAnalysis"
    assert mock_step2_report.call_args.kwargs["status"] == "Succeeded"
    assert mock_step2_report.call_args.kwargs["input_data"]["candidatesEvaluatedCount"] == 2
    assert mock_step2_report.call_args.kwargs["output_data"]["shortlistCount"] == 2

    assert mock_step3_report.await_count == 1
    assert mock_step3_report.call_args.kwargs["step_no"] == 3
    assert mock_step3_report.call_args.kwargs["agent_role"] == "MatchingPricing"
    assert mock_step3_report.call_args.kwargs["status"] == "Succeeded"
    assert mock_step3_report.call_args.kwargs["input_data"]["candidatesCount"] == 2
    assert mock_step3_report.call_args.kwargs["output_data"]["selectedAgencyId"] is not None

    assert mock_step4_report.await_count == 1
    assert mock_step4_report.call_args.kwargs["step_no"] == 4
    assert mock_step4_report.call_args.kwargs["agent_role"] == "ValidationSafety"
    assert mock_step4_report.call_args.kwargs["status"] == "Succeeded"
    assert mock_step4_report.call_args.kwargs["output_data"]["recommendation"] == "Approve"

    steps = result.get("steps", [])
    assert len(steps) == 4
    assert [s["agentRole"] for s in steps] == ["Planner", "DomainAnalysis", "MatchingPricing", "ValidationSafety"]
    assert all(s["status"] == "Succeeded" for s in steps)

    candidates = result.get("candidates", [])
    assert len(candidates) == 2
    assert all(c["eligible"] is True for c in candidates)

    # Agent 2 persists the full evaluated shortlist as real MatchCandidate audit rows
    # (POST /internal/agent-workflow-runs/{id}/candidates), not just embedded step JSON.
    mock_record_match_candidates.assert_awaited_once()
    recorded = mock_record_match_candidates.await_args.args[1]
    assert len(recorded) == 2
    assert all(r.eligible for r in recorded)

    tool_calls = result.get("tool_calls", [])
    assert len(tool_calls) >= 3  # 2 candidate routes + 1 cargo route + 1 pricing
    tool_names = [tc["toolName"] for tc in tool_calls]
    assert "get_route_and_eta" in tool_names
    assert "estimate_price" in tool_names

    ranked = result.get("ranked_five", [])
    assert len(ranked) == 2
    assert "agencyId" in ranked[0]
    assert "etaMinutes" in ranked[0]
    assert "distanceKm" in ranked[0]

    most_suitable = result.get("most_suitable")
    assert most_suitable is not None
    assert most_suitable["estimatedPrice"] == 24500.0
    assert most_suitable["distanceKm"] > 0
    assert most_suitable["vehicleClass"] == 1  # MediumLorry is 1

    validation = result.get("validation")
    assert validation is not None
    assert validation["recommendation"] == "Approve"
    assert len(validation["checks"]) == 6
    assert all(c["passed"] is True for c in validation["checks"])


@pytest.mark.anyio
async def test_short_circuit_agent_1_malformed_load():
    """Agent 1 short-circuit: missing weight/coordinates stops pipeline immediately."""
    state = WorkflowState(
        load_id=uuid.uuid4(),
        triggered_by_user_id=uuid.uuid4(),
        attempt_no=1,
        load_context={
            "weightKg": 0,  # Invalid weight
            "pickupLat": None,
        },
    )

    pipeline = get_pipeline()
    result = await pipeline.ainvoke(state)

    assert result.get("failed") is True
    assert result.get("failure_reason") == "malformed_load_payload"

    steps = result.get("steps", [])
    assert len(steps) == 1
    assert steps[0]["agentRole"] == "Planner"
    assert steps[0]["status"] == "Failed"
    assert steps[0]["errorMessage"] == "malformed_load_payload"

    # Subsequent agents must not have run
    assert result.get("candidates") == []
    assert result.get("tool_calls") == []
    assert result.get("most_suitable") is None
    assert result.get("validation") is None


@pytest.mark.anyio
async def test_short_circuit_agent_2_zero_eligible_agencies(mock_planner_llm):
    """Agent 2 short-circuit: zero eligible agencies stops pipeline before Agent 3/4."""
    # Heavy container cargo (12,000 kg) but candidates only have MiniTruck
    candidate = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Small Vans Co",
        yard_lat=6.9271,
        yard_lng=79.8612,
        yard_address="Colombo",
        available_vehicle_classes=["MiniTruck"],
    )

    state = WorkflowState(
        load_id=uuid.uuid4(),
        triggered_by_user_id=uuid.uuid4(),
        attempt_no=1,
        load_context={
            "pickupLat": 6.9271,
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,
            "dropoffLng": 80.6337,
            "weightKg": 12000.0,
            "volumeM3": 25.0,
        },
        candidate_shortlist=[candidate],
    )

    with (
        patch("freightlink_agent.agents.planner.get_llm", return_value=mock_planner_llm),
        patch(
            "freightlink_agent.agents.planner.create_workflow_run",
            new=AsyncMock(return_value=MagicMock(workflow_run_id=uuid.uuid4())),
        ),
        patch("freightlink_agent.agents.planner.report", new=AsyncMock()),
        patch("freightlink_agent.agents.domain_analysis.report", new=AsyncMock()),
        patch("freightlink_agent.agents.domain_analysis.record_match_candidates", new=AsyncMock()),
    ):
        pipeline = get_pipeline()
        result = await pipeline.ainvoke(state)

    assert result.get("failed") is True
    assert result.get("failure_reason") == "zero_eligible_agencies"

    steps = result.get("steps", [])
    assert len(steps) == 2
    assert steps[0]["agentRole"] == "Planner" and steps[0]["status"] == "Succeeded"
    assert steps[1]["agentRole"] == "DomainAnalysis" and steps[1]["status"] == "Failed"
    assert steps[1]["errorMessage"] == "zero_eligible_agencies"

    # Candidates evaluated and recorded with rejection reason
    candidates = result.get("candidates", [])
    assert len(candidates) == 1
    assert candidates[0]["eligible"] is False

    # Agents 3 and 4 must not have executed
    assert result.get("tool_calls") == []
    assert result.get("most_suitable") is None
    assert result.get("validation") is None


@pytest.mark.anyio
async def test_short_circuit_agent_3_routing_failure(mock_planner_llm, sample_candidates):
    """Agent 3 short-circuit: all routing lookups fail stops pipeline before Agent 4."""
    state = WorkflowState(
        load_id=uuid.uuid4(),
        triggered_by_user_id=uuid.uuid4(),
        attempt_no=1,
        load_context={
            "pickupLat": 6.9271,
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,
            "dropoffLng": 80.6337,
            "weightKg": 2000.0,
            "volumeM3": 5.0,
        },
        candidate_shortlist=sample_candidates,
    )

    failed_route = RouteAndEtaResponse(
        success=False,
        error_message="ORS upstream server timeout",
    )

    with (
        patch("freightlink_agent.agents.planner.get_llm", return_value=mock_planner_llm),
        patch(
            "freightlink_agent.agents.planner.create_workflow_run",
            new=AsyncMock(return_value=MagicMock(workflow_run_id=uuid.uuid4())),
        ),
        patch("freightlink_agent.agents.planner.report", new=AsyncMock()),
        patch("freightlink_agent.agents.domain_analysis.report", new=AsyncMock()),
        patch("freightlink_agent.agents.domain_analysis.record_match_candidates", new=AsyncMock()),
        patch(
            "freightlink_agent.agents.matching_pricing.get_route_and_eta",
            new=AsyncMock(return_value=(failed_route, {"httpStatusCode": 504, "request": {}})),
        ),
        patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock()),
        patch("freightlink_agent.agents.matching_pricing.record_tool_call", new=AsyncMock()),
    ):
        pipeline = get_pipeline()
        result = await pipeline.ainvoke(state)

    assert result.get("failed") is True
    assert "routing lookups failed" in result.get("failure_reason", "")

    steps = result.get("steps", [])
    assert len(steps) == 3
    assert steps[0]["agentRole"] == "Planner" and steps[0]["status"] == "Succeeded"
    assert steps[1]["agentRole"] == "DomainAnalysis" and steps[1]["status"] == "Succeeded"
    assert steps[2]["agentRole"] == "MatchingPricing" and steps[2]["status"] == "Failed"

    # Agent 4 must not have executed
    assert result.get("validation") is None


@pytest.mark.anyio
async def test_post_workflows_run_endpoint(mock_planner_llm, sample_candidates):
    """Tests the HTTP POST /workflows/run endpoint contract end-to-end.

    Replaces the old /workflows/match endpoint test - that route had no
    production consumer (the backend only ever calls /workflows/run) and
    was removed per plans/01-python-service-consolidation.md §3.
    """
    from freightlink_agent.core.config import get_settings

    settings = get_settings()
    api_key = settings.shared_secret or "development-agent-secret"

    payload = {
        "loadId": str(uuid.uuid4()),
        "triggeredByUserId": str(uuid.uuid4()),
        "attemptNo": 1,
        "loadContext": {
            "pickupLat": 6.9271,
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,
            "dropoffLng": 80.6337,
            "weightKg": 1500,
            "volumeM3": 4.0,
            "cargoDescription": "Boxes of garments",
        },
        "candidateAgencies": [
            {
                "agencyId": str(sample_candidates[0].agency_id),
                "name": sample_candidates[0].name,
                "yardLat": sample_candidates[0].yard_lat,
                "yardLng": sample_candidates[0].yard_lng,
                "yardAddress": sample_candidates[0].yard_address,
                "availableVehicleClasses": ["MediumLorry"],
                "availableVehicles": sample_candidates[0].available_vehicles,
                "activeDrivers": sample_candidates[0].active_drivers,
            }
        ],
    }

    mock_pricing_response = EstimatePricingResponse(
        load_id=uuid.UUID(payload["loadId"]),
        estimated_price=19000.0,
        distance_km=115.0,
        vehicle_class="MediumLorry",
        rate_per_km=120.0,
        rate_per_kg=1.5,
        base_fare=1500.0,
    )

    with (
        patch("freightlink_agent.agents.planner.get_llm", return_value=mock_planner_llm),
        patch("freightlink_agent.agents.matching_pricing.get_llm", return_value=mock_planner_llm),
        patch(
            "freightlink_agent.agents.matching_pricing.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200, "durationMs": 30})),
        ),
        patch("freightlink_agent.agents.planner.report", new=AsyncMock()),
        patch("freightlink_agent.agents.domain_analysis.report", new=AsyncMock()),
        patch("freightlink_agent.agents.domain_analysis.record_match_candidates", new=AsyncMock()),
        patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock()),
        patch("freightlink_agent.agents.validation_safety.report", new=AsyncMock()),
        patch("freightlink_agent.agents.matching_pricing.record_tool_call", new=AsyncMock()),
        patch(
            "freightlink_agent.agents.planner.create_workflow_run",
            new=AsyncMock(return_value=MagicMock(workflow_run_id=uuid.uuid4())),
        ),
    ):
        async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
            res = await client.post(
                "/workflows/run",
                json=payload,
                headers={"X-Internal-Api-Key": api_key},
            )

    assert res.status_code == 200
    data = res.json()

    assert "workflowRunId" in data
    assert "objective" in data
    assert "planJson" in data
