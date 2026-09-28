import re
import uuid
from unittest.mock import AsyncMock, MagicMock, patch
import pytest

from freightlink_agent.agents import matching_pricing
from freightlink_agent.core.llm import ToolCallingSelectionOutput
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    EstimatePricingResponse,
    RouteAndEtaRequest,
)
from freightlink_agent.tools.routing import calculate_haversine_distance_km, get_route_and_eta


def _extract_tool_call_id(tool_output: str) -> str:
    match = re.search(r"tool_call_id='?([0-9a-fA-F-]+)'?", tool_output)
    assert match, f"no tool_call_id found in tool output: {tool_output}"
    return match.group(1)


def _make_fake_tool_calling_llm(winner_agency_id: uuid.UUID, *, headline: str, detailed_reasoning: str):
    """Builds a fake AgentLLM whose run_tool_calling_selection genuinely drives the real
    tool objects it's handed (populating the real ledger via real tool calls), then cites
    the real tool_call_ids it got back - exactly what a real tool-calling LLM would do."""

    fake_llm = MagicMock()

    async def _fake_run_tool_calling_selection(system_prompt, context, tools, max_iterations=6, max_tool_calls=8):
        tools_by_name = {t.name: t for t in tools}
        winner_id = str(winner_agency_id)

        positioning_output = await tools_by_name["get_route_and_eta_for_candidate"].ainvoke(
            {"candidate_agency_id": winner_id, "leg": "positioning"}
        )
        positioning_id = _extract_tool_call_id(positioning_output)

        cargo_output = await tools_by_name["get_route_and_eta_for_candidate"].ainvoke(
            {"candidate_agency_id": winner_id, "leg": "cargo"}
        )
        cargo_id = _extract_tool_call_id(cargo_output)

        pricing_output = await tools_by_name["estimate_price_for_load"].ainvoke(
            {"candidate_agency_id": winner_id, "cargo_route_tool_call_id": cargo_id}
        )
        pricing_id = _extract_tool_call_id(pricing_output)

        return ToolCallingSelectionOutput(
            selected_candidate_agency_id=winner_id,
            selected_positioning_tool_call_id=positioning_id,
            selected_cargo_tool_call_id=cargo_id,
            selected_pricing_tool_call_id=pricing_id,
            headline=headline,
            detailed_reasoning=detailed_reasoning,
        )

    fake_llm.run_tool_calling_selection = AsyncMock(side_effect=_fake_run_tool_calling_selection)
    fake_llm.last_call_meta = {"provider": "openai", "model": "gpt-4o-mini", "usedFallback": False}
    return fake_llm


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
    assert telemetry["httpStatusCode"] in (200, 403)


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

    fake_llm = _make_fake_tool_calling_llm(
        agency_1.agency_id,
        headline="Recommended: Peliyagoda Logistics",
        detailed_reasoning="Fastest positioning ETA with verified MediumLorry capacity.",
    )

    with (
        patch("freightlink_agent.agents.matching_pricing.get_llm", return_value=fake_llm),
        patch(
            "freightlink_agent.tools.matching_tools.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200})),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.report",
            new=AsyncMock(return_value=uuid.uuid4()),
        ),
        patch(
            "freightlink_agent.tools.matching_tools.record_tool_call",
            new=AsyncMock(),
        ),
    ):
        result = await matching_pricing.run(state)

    assert result.get("failed") is not True
    assert result["selected_agency_id"] == agency_1.agency_id
    assert result["selected_agency_name"] == "Peliyagoda Logistics"
    assert result["suggested_vehicle_class"] == "MediumLorry"
    # The real, tool-returned price - not anything the fake LLM's text could have said
    assert result["proposed_price"] == 18500.0
    assert result["eta_minutes"] is not None
    assert result["cargo_distance_km"] is not None
    assert "Peliyagoda Logistics" in result["selection_justification"]
    assert result["steps"][-1]["outputJson"]["llmProvenance"]["provider"] == "openai"


@pytest.mark.anyio
async def test_llm_restated_wrong_price_is_ignored_real_price_persists():
    """Regression proof of the deterministic-vs-LLM boundary: even if the model's own
    narrative text restates a wrong number, the persisted proposed_price must come from
    the real tool result the model cited - never from its own words."""
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    agency = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Peliyagoda Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="123 Negombo Rd, Peliyagoda",
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
            "weightKg": 2500,
            "volumeM3": 8.0,
        },
        candidate_shortlist=[agency],
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

    fake_llm = _make_fake_tool_calling_llm(
        agency.agency_id,
        headline="Recommended carrier at a bargain LKR 1.00",
        detailed_reasoning="This deliberately wrong price of LKR 1.00 must never be trusted.",
    )

    with (
        patch("freightlink_agent.agents.matching_pricing.get_llm", return_value=fake_llm),
        patch(
            "freightlink_agent.tools.matching_tools.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200})),
        ),
        patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock()),
        patch("freightlink_agent.tools.matching_tools.record_tool_call", new=AsyncMock()),
    ):
        result = await matching_pricing.run(state)

    assert result.get("failed") is not True
    # The LLM's own restated "LKR 1.00" never reaches the persisted price
    assert result["proposed_price"] == 18500.0
    assert "LKR 1.00" in result["selection_justification"]  # the wrong text is still just prose


@pytest.mark.anyio
async def test_invalid_tool_call_citation_falls_back_to_deterministic_selection():
    """If the model's final decision cites a tool_call_id that isn't in the ledger (or
    points at nothing real), that must be treated exactly like a total LLM failure - the
    run still succeeds, via the deterministic fallback algorithm, never a fabricated result."""
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    agency = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Peliyagoda Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="123 Negombo Rd, Peliyagoda",
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
            "weightKg": 2500,
            "volumeM3": 8.0,
        },
        candidate_shortlist=[agency],
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

    fake_llm = MagicMock()
    fake_llm.run_tool_calling_selection = AsyncMock(
        return_value=ToolCallingSelectionOutput(
            selected_candidate_agency_id=str(agency.agency_id),
            selected_positioning_tool_call_id="not-a-real-tool-call-id",
            selected_cargo_tool_call_id="also-not-real",
            selected_pricing_tool_call_id="still-not-real",
            headline="Hallucinated citation",
            detailed_reasoning="These tool_call_ids were never actually returned by any tool.",
        )
    )

    with (
        patch("freightlink_agent.agents.matching_pricing.get_llm", return_value=fake_llm),
        patch(
            "freightlink_agent.agents.matching_pricing.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200})),
        ),
        patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock()),
        patch("freightlink_agent.agents.matching_pricing.record_tool_call", new=AsyncMock()),
    ):
        result = await matching_pricing.run(state)

    assert result.get("failed") is not True
    assert result["proposed_price"] == 18500.0
    assert result["steps"][-1]["outputJson"]["llmProvenance"]["provider"] == "deterministic_fallback"


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
async def test_pricing_failure_fails_run_cleanly_not_fabricated_price():
    """Verifies that when backend pricing fails, Agent 3 fails the run rather than
    fabricating a price via a local formula (plans/02-contracts-and-agent-fixes.md §1.3).
    """
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    agency = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Peliyagoda Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="123 Negombo Rd, Peliyagoda",
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
            "weightKg": 2500,
            "volumeM3": 8.0,
        },
        candidate_shortlist=[agency],
    )

    with (
        patch(
            "freightlink_agent.agents.matching_pricing.get_price_estimate",
            new=AsyncMock(
                return_value=(None, {"error": "backend pricing unreachable", "httpStatusCode": 500})
            ),
        ),
        patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock()),
        patch(
            "freightlink_agent.agents.matching_pricing.record_tool_call",
            new=AsyncMock(),
        ) as mock_record_call,
    ):
        result = await matching_pricing.run(state)

    # Fails cleanly - no proposed_price/most_suitable ever gets fabricated
    assert result.get("failed") is True
    assert "pricing estimation failed" in result.get("failure_reason", "").lower()
    assert "proposed_price" not in result
    assert "most_suitable" not in result

    # The failed pricing tool call is still recorded for the audit trail, alongside the
    # two routing tool calls (positioning leg + cargo leg) made before pricing ran
    pricing_calls = [
        call.args[1] for call in mock_record_call.await_args_list if call.args[1].tool_name == "estimate_price"
    ]
    assert len(pricing_calls) == 1
    assert pricing_calls[0].success is False

    steps = result.get("steps", [])
    assert steps[-1]["status"] == "Failed"
    assert steps[-1]["agentRole"] == "MatchingPricing"


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


@pytest.mark.anyio
async def test_deterministic_fallback_continues_attempt_numbering_after_partial_llm_tool_calls():
    """Regression test for a production bug: a real LLM often makes several genuine
    positioning tool calls (consuming get_route_and_eta attempt numbers 1, 2, 3...) before
    its final decision turns out to cite an invalid/failed id, triggering the deterministic
    fallback. The fallback's own tool calls, against that SAME AgentStep, must continue that
    numbering rather than restart at 1 - the backend's uq_toolcall_attempt constraint is
    unique on (AgentStepId, ToolName, AttemptNo), so restarting causes every one of the
    fallback's calls to collide and fail to persist (observed in production as repeated
    409 TOOL_CALL_DUPLICATE_ATTEMPT responses and a lost audit trail)."""
    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    agency_1 = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Peliyagoda Logistics",
        yard_lat=6.9667,
        yard_lng=79.8917,
        yard_address="123 Negombo Rd, Peliyagoda",
        available_vehicle_classes=["MediumLorry"],
    )
    agency_2 = CandidateAgency(
        agency_id=uuid.uuid4(),
        name="Southern Freight Express",
        yard_lat=6.0535,
        yard_lng=80.2210,
        yard_address="45 Port Rd, Galle",
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
            "weightKg": 2500,
            "volumeM3": 8.0,
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

    # Simulates a real, imperfect LLM: it genuinely checks BOTH candidates' positioning ETA
    # (two real get_route_and_eta tool calls, consuming attempt numbers 1 and 2) but its
    # final decision cites a pricing tool_call_id that was never actually returned -
    # forcing a fallback to the deterministic algorithm.
    fake_llm = MagicMock()

    async def _fake_run_tool_calling_selection(system_prompt, context, tools, max_iterations=6, max_tool_calls=8):
        tools_by_name = {t.name: t for t in tools}
        for candidate in (agency_1, agency_2):
            await tools_by_name["get_route_and_eta_for_candidate"].ainvoke(
                {"candidate_agency_id": str(candidate.agency_id), "leg": "positioning"}
            )
        return ToolCallingSelectionOutput(
            selected_candidate_agency_id=str(agency_1.agency_id),
            selected_positioning_tool_call_id="hallucinated-id",
            selected_cargo_tool_call_id="hallucinated-id",
            selected_pricing_tool_call_id="hallucinated-id",
            headline="Bad decision",
            detailed_reasoning="Cites ids that were never actually returned by any tool.",
        )

    fake_llm.run_tool_calling_selection = AsyncMock(side_effect=_fake_run_tool_calling_selection)

    recorded_calls: list = []

    async def _record_tool_call_side_effect(workflow_run_id, request):
        recorded_calls.append(request)

    with (
        patch("freightlink_agent.agents.matching_pricing.get_llm", return_value=fake_llm),
        patch(
            "freightlink_agent.tools.matching_tools.record_tool_call",
            new=AsyncMock(side_effect=_record_tool_call_side_effect),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.get_price_estimate",
            new=AsyncMock(return_value=(mock_pricing_response, {"httpStatusCode": 200})),
        ),
        patch(
            "freightlink_agent.agents.matching_pricing.record_tool_call",
            new=AsyncMock(side_effect=_record_tool_call_side_effect),
        ),
        patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock()),
    ):
        result = await matching_pricing.run(state)

    assert result.get("failed") is not True
    assert result["proposed_price"] == 18500.0

    # Every recorded ToolCall for a given tool name must have a unique attempt number - no
    # (tool_name, attempt_no) pair repeats, which is exactly what uq_toolcall_attempt enforces.
    seen: set[tuple[str, int]] = set()
    for call in recorded_calls:
        key = (call.tool_name, call.attempt_no)
        assert key not in seen, f"duplicate (tool_name, attempt_no) recorded: {key}"
        seen.add(key)

    # The LLM path's 2 positioning calls plus the fallback's own calls must all have landed.
    get_route_calls = [c for c in recorded_calls if c.tool_name == "get_route_and_eta"]
    assert len(get_route_calls) >= 4  # 2 from the LLM path + at least 2 from the fallback

