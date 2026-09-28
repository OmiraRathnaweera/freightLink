import asyncio
import os
import sys
from unittest.mock import AsyncMock, patch
from uuid import uuid4

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

# Set test environment settings
os.environ.setdefault("SHARED_SECRET", "test-shared-secret")
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-api-key")

from freightlink_agent.agents.validation_safety import evaluate_deterministic_rules, run
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.matching import CandidateAgency


def _valid_state(**overrides) -> WorkflowState:
    """A fully-populated, genuinely-valid Agent 4 input state - the shortlist actually
    contains the selected agency, and cargo distance/vehicle/driver are all assigned.
    Individual tests override just the field(s) they want to break, so each false-pass
    fix is exercised in isolation against an otherwise-passing baseline."""
    agency_id = overrides.pop("selected_agency_id", None) or uuid4()
    candidate_shortlist = overrides.pop("candidate_shortlist", None)
    if candidate_shortlist is None:
        candidate_shortlist = [
            CandidateAgency(
                agency_id=agency_id,
                name="Lanka Express Logistics",
                yard_lat=6.9271,
                yard_lng=79.8612,
                yard_address="Colombo Yard",
            )
        ]

    defaults: dict = dict(
        load_id=uuid4(),
        triggered_by_user_id=uuid4(),
        attempt_no=1,
        load_context={
            "pickup": "Colombo 03",
            "dropoff": "Kandy City Center",
            "weight_kg": 1500,
            "cargo_description": "Apparel and Textiles",
        },
        candidate_shortlist=candidate_shortlist,
        selected_agency_id=agency_id,
        selected_agency_name="Lanka Express Logistics",
        eta_minutes=180,
        cargo_distance_km=115.0,
        proposed_price=48000.0,
        suggested_vehicle_class="MediumLorry",
        assigned_vehicle={"vehicleId": str(uuid4()), "registrationNo": "WP-CAB-4521"},
        assigned_driver={"driverId": str(uuid4()), "name": "Suneth Alwis"},
    )
    defaults.update(overrides)
    return WorkflowState(**defaults)


def test_deterministic_validation_passes():
    """A complete, valid match - real shortlist, cargo distance, vehicle, and driver -
    passes every deterministic check."""
    state = _valid_state()

    is_valid, flags, deviation, checks = evaluate_deterministic_rules(state)

    assert is_valid is True
    assert deviation is None
    assert len(checks) >= 6
    assert all(c["passed"] for c in checks)


def test_deterministic_validation_missing_price():
    """Missing or negative price must fail validation deterministically."""
    state = _valid_state(proposed_price=-500.0)

    is_valid, flags, _, _ = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("INVALID_PRICE" in f for f in flags)


def test_price_deviation_check_removed():
    """The price-deviation check is deliberately dropped, not merely untested - Load
    creation has no shipperBudget/budget field anywhere in the normal payload (confirmed
    by audit), so the old arithmetic was always silently a no-op in production
    (plans/02-contracts-and-agent-fixes.md §5). Even when a load_context happens to carry
    a shipper_budget-shaped key, no deviation is computed and no budget flag is raised."""
    state = _valid_state(load_context={"weight_kg": 1000, "shipper_budget": 100000.0})

    is_valid, flags, deviation, _ = evaluate_deterministic_rules(state)

    assert deviation is None
    assert is_valid is True
    assert not any("BUDGET" in f or "PRICE_EXCEEDS" in f or "PRICE_UNDER" in f for f in flags)


def test_deterministic_weight_limits():
    """Weights exceeding highway safety limits must fail."""
    state = _valid_state(
        load_context={"weight_kg": 55000.0},  # Exceeds 45,000kg legal highway limit
        suggested_vehicle_class="ContainerTruck",
    )

    is_valid, flags, _, _ = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("WEIGHT_EXCEEDS_MAX_LEGAL_LIMIT" in f for f in flags)


def test_carrier_eligibility_fails_closed_on_missing_shortlist():
    """False-pass fix (the audit's most safety-critical finding): a missing/empty
    shortlist must fail carrier eligibility, not pass just because an agency id exists.
    Previously `not eligible_agency_ids` made an absent shortlist auto-pass."""
    state = _valid_state(candidate_shortlist=[])

    is_valid, flags, _, checks = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("CARRIER_ELIGIBILITY_FAILED" in f for f in flags)
    carrier_check = next(c for c in checks if c["name"] == "carrier_eligibility")
    assert carrier_check["passed"] is False


def test_carrier_eligibility_fails_when_selected_agency_not_in_shortlist():
    """The selected agency must actually be a member of the evaluated shortlist."""
    state = _valid_state(
        candidate_shortlist=[
            CandidateAgency(
                agency_id=uuid4(),  # a different agency than the one selected
                name="Some Other Carrier",
                yard_lat=6.9,
                yard_lng=79.9,
                yard_address="Somewhere",
            )
        ]
    )

    is_valid, flags, _, _ = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("CARRIER_ELIGIBILITY_FAILED" in f for f in flags)


def test_driver_compliance_fails_closed_on_missing_driver():
    """False-pass fix: a missing driver assignment must fail, not silently pass."""
    state = _valid_state(assigned_driver=None)

    is_valid, flags, _, checks = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("DRIVER_COMPLIANCE_FAILED" in f for f in flags)
    assert next(c for c in checks if c["name"] == "driver_compliance")["passed"] is False


def test_vehicle_verification_fails_closed_on_missing_vehicle():
    """False-pass fix: a missing vehicle assignment must fail, not silently pass."""
    state = _valid_state(assigned_vehicle=None)

    is_valid, flags, _, checks = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("VEHICLE_VERIFICATION_FAILED" in f for f in flags)
    assert next(c for c in checks if c["name"] == "vehicle_verification")["passed"] is False


def test_routing_sanity_fails_closed_on_missing_cargo_distance():
    """False-pass fix: a missing cargo distance must fail routing sanity, not pass it -
    previously `cargo_distance_km is None or ...` let a run with no distance data through."""
    state = _valid_state(cargo_distance_km=None)

    is_valid, flags, _, checks = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("INVALID_ROUTING" in f for f in flags)
    assert next(c for c in checks if c["name"] == "routing_sanity")["passed"] is False


def test_agent4_run_awaiting_approval():
    """Agent 4 must unconditionally pause for human approval ('AwaitingApproval')
    and persist step incrementally via POST /internal/agent-workflow-runs/{id}/steps.
    """
    async def _async_test():
        workflow_run_id = uuid4()
        state = _valid_state(
            workflow_run_id=workflow_run_id,
            selected_agency_name="Southern Hauliers",
            eta_minutes=150,
            proposed_price=33000.0,
            load_context={
                "pickup": "Peliyagoda Central Market",
                "dropoff": "Galle Fort",
                "weight_kg": 2000,
                "cargo_description": "Fresh Produce",
            },
        )

        with patch("freightlink_agent.agents.validation_safety.report", new_callable=AsyncMock) as mock_report:
            result = await run(state)

            assert result["status"] == "AwaitingApproval"
            assert result["is_valid"] is True
            assert result["price_deviation_percent"] is None
            assert "validation_summary" in result
            assert "Southern Hauliers" in result["validation_summary"]
            assert "proposal_email_subject" in result
            assert "proposal_email_body" in result

            # Verify ADR-017 compliance: Fixed price job proposal, no bidding
            assert "Job Proposal" in result["proposal_email_subject"] or "Job Proposal" in result["proposal_email_body"]
            assert "bid" not in result["proposal_email_body"].lower() or "no bidding" in result["proposal_email_body"].lower()

            # Channel 1: Incremental step persistence verified
            mock_report.assert_called_once()
            call_kwargs = mock_report.call_args.kwargs
            assert call_kwargs["workflow_run_id"] == workflow_run_id
            assert call_kwargs["step_no"] == 4
            assert call_kwargs["agent_role"] == "ValidationSafety"
            assert call_kwargs["status"] == "Succeeded"

    asyncio.run(_async_test())


def test_agent4_run_fails_when_step_report_fails():
    """A report_step failure must flip the run to Failed, even when validation itself
    passed - a Succeeded/AwaitingApproval outcome must never be returned when Agent 4's
    own AgentStep row was never actually persisted (plans/02-contracts-and-agent-fixes.md
    §1.5, the same "step-report failure swallowed" pattern fixed for Agent 1)."""
    from freightlink_agent.core.backend_client import BackendClientError

    async def _async_test():
        state = _valid_state(workflow_run_id=uuid4())

        with patch(
            "freightlink_agent.agents.validation_safety.report",
            new=AsyncMock(side_effect=BackendClientError("network error")),
        ):
            result = await run(state)

        assert result["failed"] is True
        assert result["is_valid"] is False
        assert result["status"] == "Failed"
        assert result["steps"][-1]["status"] == "Failed"

    asyncio.run(_async_test())
