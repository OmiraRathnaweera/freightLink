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


def test_deterministic_validation_passes():
    """Verify that a complete, valid match passes all deterministic checks."""
    state = WorkflowState(
        load_id=uuid4(),
        triggered_by_user_id=uuid4(),
        attempt_no=1,
        load_context={
            "pickup": "Colombo 03",
            "dropoff": "Kandy City Center",
            "weight_kg": 1500,
            "cargo_description": "Apparel and Textiles",
            "shipper_budget": 50000.0,
        },
        selected_agency_id=uuid4(),
        selected_agency_name="Lanka Express Logistics",
        eta_minutes=180,
        proposed_price=48000.0,
        suggested_vehicle_class="MediumLorry",
    )

    is_valid, flags, deviation, checks = evaluate_deterministic_rules(state)

    assert is_valid is True
    assert deviation == -4.0  # (48000 - 50000) / 50000 * 100 = -4.0%
    assert any("PRICE_UNDER_BUDGET" in f for f in flags)
    assert len(checks) >= 6


def test_deterministic_validation_missing_price():
    """Missing or negative price must fail validation deterministically."""
    state = WorkflowState(
        load_id=uuid4(),
        triggered_by_user_id=uuid4(),
        attempt_no=1,
        load_context={"weight_kg": 500},
        selected_agency_id=uuid4(),
        selected_agency_name="Lanka Express",
        eta_minutes=120,
        proposed_price=-500.0,  # Invalid
        suggested_vehicle_class="MiniTruck",
    )

    is_valid, flags, deviation, checks = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("INVALID_PRICE" in f for f in flags)


def test_price_deviation_calculation():
    """Verifies deterministic arithmetic for budget comparison."""
    # Scenario: 25% over budget triggers HIGH_PRICE_DEVIATION flag
    state = WorkflowState(
        load_id=uuid4(),
        triggered_by_user_id=uuid4(),
        attempt_no=1,
        load_context={
            "weight_kg": 1000,
            "shipper_budget": 100000.0,
        },
        selected_agency_id=uuid4(),
        selected_agency_name="Premier Transport",
        eta_minutes=240,
        proposed_price=125000.0,
        suggested_vehicle_class="ContainerTruck",
    )

    is_valid, flags, deviation, checks = evaluate_deterministic_rules(state)

    assert is_valid is True  # Warning flag, not a critical safety failure
    assert deviation == 25.0
    assert any("PRICE_EXCEEDS_BUDGET" in f for f in flags)
    assert any("HIGH_PRICE_DEVIATION" in f for f in flags)


def test_deterministic_weight_limits():
    """Weights exceeding highway safety limits must fail."""
    state = WorkflowState(
        load_id=uuid4(),
        triggered_by_user_id=uuid4(),
        attempt_no=1,
        load_context={
            "weight_kg": 55000.0,  # Exceeds 45,000kg legal highway limit
        },
        selected_agency_id=uuid4(),
        selected_agency_name="Lanka Express",
        eta_minutes=120,
        proposed_price=60000.0,
        suggested_vehicle_class="ContainerTruck",
    )

    is_valid, flags, _, _ = evaluate_deterministic_rules(state)

    assert is_valid is False
    assert any("WEIGHT_EXCEEDS_MAX_LEGAL_LIMIT" in f for f in flags)


def test_agent4_run_awaiting_approval():
    """Agent 4 must unconditionally pause for human approval ('AwaitingApproval')
    and persist step incrementally via POST /internal/agent-workflow-runs/{id}/steps.
    """
    async def _async_test():
        workflow_run_id = uuid4()
        state = WorkflowState(
            workflow_run_id=workflow_run_id,
            load_id=uuid4(),
            triggered_by_user_id=uuid4(),
            attempt_no=1,
            load_context={
                "pickup": "Peliyagoda Central Market",
                "dropoff": "Galle Fort",
                "weight_kg": 2000,
                "cargo_description": "Fresh Produce",
                "shipper_budget": 35000.0,
            },
            selected_agency_id=uuid4(),
            selected_agency_name="Southern Hauliers",
            eta_minutes=150,
            proposed_price=33000.0,
            suggested_vehicle_class="MediumLorry",
        )

        with patch("freightlink_agent.agents.validation_safety.report", new_callable=AsyncMock) as mock_report:
            result = await run(state)

            assert result["status"] == "AwaitingApproval"
            assert result["is_valid"] is True
            assert result["price_deviation_percent"] == -5.71
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
