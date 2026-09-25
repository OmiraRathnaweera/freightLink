import uuid
from unittest.mock import AsyncMock, patch
import pytest

from freightlink_agent.agents import matching_pricing
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    EstimatePricingResponse,
    RouteAndEtaRequest,
)
from freightlink_agent.tools.routing import calculate_haversine_distance_km, get_route_and_eta


def test_calculate_haversine_distance():
    # Colombo Fort to Kandy Clock Tower is ~95-100 km straight line
    colombo_lat, colombo_lng = 6.9344, 79.8428
    kandy_lat, kandy_lng = 7.2906, 80.6337

    dist = calculate_haversine_distance_km(colombo_lat, colombo_lng, kandy_lat, kandy_lng)
    assert 90.0 <= dist <= 110.0


@pytest.mark.anyio
async def test_get_route_and_eta_simulated():
    req = RouteAndEtaRequest(
        origin_lat=6.9344,
        origin_lng=79.8428,
        destination_lat=7.2906,
        destination_lng=80.6337,
    )
    res, telemetry = await get_route_and_eta(req)

    assert res.success is True
    assert res.distance_km is not None and res.distance_km > 100.0
    assert res.eta_minutes is not None and res.eta_minutes > 60
    assert telemetry["httpStatusCode"] == 200


@pytest.mark.anyio
async def test_get_route_and_eta_invalid_coordinates():
    from pydantic import ValidationError

    with pytest.raises(ValidationError):
        RouteAndEtaRequest(
            origin_lat=999.0,  # Invalid latitude out of [-90, 90] bounds
            origin_lng=79.8428,
            destination_lat=7.2906,
            destination_lng=80.6337,
        )



def test_vehicle_class_resolution():
    assert (
        matching_pricing._determine_vehicle_class(500, 2.0, ["MiniTruck", "MediumLorry"])
        == "MiniTruck"
    )
    assert (
        matching_pricing._determine_vehicle_class(3000, 10.0, ["MiniTruck", "MediumLorry"])
        == "MediumLorry"
    )
    assert (
        matching_pricing._determine_vehicle_class(8000, 30.0, ["MediumLorry", "ContainerTruck"])
        == "ContainerTruck"
    )
    # Upsizing when preferred tier is not available in candidate fleet
    assert (
        matching_pricing._determine_vehicle_class(500, 2.0, ["MediumLorry", "ContainerTruck"])
        == "MediumLorry"
    )


@pytest.mark.anyio
async def test_matching_pricing_agent_success():
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    # Candidate 1: Yard in Peliyagoda (close to Colombo pickup)
    agency_1 = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Peliyagoda Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="123 Negombo Rd, Peliyagoda",
        available_vehicle_classes=["MediumLorry", "ContainerTruck"],
    )

    # Candidate 2: Yard in Galle (farther away from Colombo pickup)
    agency_2 = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Southern Freight Express",
        yard_lat=6.0535,
        yard_lng=80.2210,
        yard_address="45 Port Rd, Galle",
        available_vehicle_classes=["MiniTruck", "MediumLorry"],
    )

    state = WorkflowState(
        load_id=load_id,
        triggered_by_user_id=shipper_id,
        attempt_no=1,
        workflow_run_id=run_id,
        load_context={
            "pickupLat": 6.9271,  # Colombo
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,  # Kandy
            "dropoffLng": 80.6337,
            "weightKg": 2500,
            "volumeM3": 8.0,
            "cargoDescription": "Factory spare parts",
        },
        candidate_shortlist=[agency_1, agency_2],
    )

    mock_pricing_response = EstimatePricingResponse(
        load_id=load_id,
        estimated_price=18500.0,
        distance_km=115.0,
        vehicle_class="MediumLorry",
        rate_per_km=120.0,
        rate_per_kg=1.5,
        base_fare=1500.0,
    )

    with (
        patch(
            "freightlink_agent.agents.matching_pricing.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200})),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.report",
            new=AsyncMock(return_value=uuid.uuid4()),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.record_tool_call",
            new=AsyncMock(),
        ),
    ):
        result = await matching_pricing.run(state)

    assert result.get("failed") is not True
    assert result["selected_agency_id"] == agency_1.agency_id
    assert result["selected_agency_name"] == "Peliyagoda Logistics"
    assert result["suggested_vehicle_class"] == "MediumLorry"
    assert result["proposed_price"] == 18500.0
    assert result["eta_minutes"] is not None
    assert result["cargo_distance_km"] is not None
    assert "Peliyagoda Logistics" in result["selection_justification"]


@pytest.mark.anyio
async def test_matching_pricing_empty_candidates():
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    state = WorkflowState(
        load_id=load_id,
        triggered_by_user_id=shipper_id,
        attempt_no=1,
        workflow_run_id=run_id,
        load_context={
            "pickupLat": 6.9271,
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,
            "dropoffLng": 80.6337,
        },
        candidate_shortlist=[],
    )

    with patch(
        "freightlink_agent.agents.matching_pricing.report",
        new=AsyncMock(),
    ):
        result = await matching_pricing.run(state)

    assert result.get("failed") is True
    assert "No eligible candidate agencies" in result.get("failure_reason", "")


@pytest.mark.anyio
async def test_tool_failure_routing_safe_hold_for_review():
    """Verifies that when routing tool calls fail after retry, safe 'hold for review' is recorded."""
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    agency = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Broken Route Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="123 Road",
        available_vehicle_classes=["MediumLorry"],
    )

    state = WorkflowState(
        load_id=load_id,
        triggered_by_user_id=shipper_id,
        attempt_no=1,
        workflow_run_id=run_id,
        load_context={
            "pickupLat": 6.9271,
            "pickupLng": 79.8612,
            "dropoffLat": 7.2906,
            "dropoffLng": 80.6337,
        },
        candidate_shortlist=[agency],
    )

    # Mock routing tool to simulate permanent failure after retries
    failing_route = (
        matching_pricing.RouteAndEtaResponse(
            distance_km=None,
            eta_minutes=None,
            success=False,
            error_message="hold for review: routing lookup failed after retry: timeout",
        ),
        {"httpStatusCode": 504, "durationMs": 450, "error": "timeout"},
    )

    with (
        patch(
            "freightlink_agent.agents.matching_pricing.get_route_and_eta",
            new=AsyncMock(return_value=failing_route),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.report",
            new=AsyncMock(),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.record_tool_call",
            new=AsyncMock(),
        ) as mock_record_call,
    ):
        result = await matching_pricing.run(state)

    # Pipeline must stop cleanly rather than crash
    assert result.get("failed") is True
    assert "hold for review" in result.get("failure_reason", "").lower()

    # ToolCall row must record safe "hold for review" state
    tool_calls = result.get("tool_calls", [])
    assert len(tool_calls) >= 1
    assert tool_calls[0]["success"] is False
    assert "hold for review" in tool_calls[0]["errorMessage"].lower()
    assert "hold for review" in tool_calls[0]["responseJson"].lower()

    # Step must be recorded as Failed with hold for review
    steps = result.get("steps", [])
    assert len(steps) >= 1
    assert steps[-1]["status"] == "Failed"
    assert "hold for review" in steps[-1]["errorMessage"].lower()

