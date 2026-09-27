import os
import sys
import uuid
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

os.environ.setdefault("SHARED_SECRET", "test-shared-secret")
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-api-key")

from freightlink_agent.agents import planner
from freightlink_agent.core.backend_client import BackendClientError
from freightlink_agent.graph.state import WorkflowState

_VALID_LOAD_CONTEXT = {
    "pickupLat": 6.9271,
    "pickupLng": 79.8612,
    "dropoffLat": 7.2906,
    "dropoffLng": 80.6337,
    "weightKg": 500,
    "volumeM3": 3.2,
    "cargoDescription": "Palletized dry goods",
}


def _state(**load_context_overrides) -> WorkflowState:
    ctx = dict(_VALID_LOAD_CONTEXT)
    ctx.update(load_context_overrides)
    return WorkflowState(
        load_id=uuid.uuid4(),
        triggered_by_user_id=uuid.uuid4(),
        attempt_no=1,
        load_context=ctx,
    )


@pytest.mark.anyio
@pytest.mark.parametrize(
    "overrides",
    [
        {"pickupLng": None},
        {"dropoffLng": None},
        {"volumeM3": 0},
        {"pickupLat": 999.0},  # out of [-90, 90] range - garbage, not just missing
        {"dropoffLng": -200.0},  # out of [-180, 180] range
    ],
    ids=["missing_pickup_lng", "missing_dropoff_lng", "zero_volume", "invalid_lat_range", "invalid_lng_range"],
)
async def test_malformed_payload_rejected(overrides):
    """Strengthened input guard: longitude presence, coordinate range sanity, and volume
    presence are all checked now, not just weight and lat presence
    (plans/02-contracts-and-agent-fixes.md §2)."""
    state = _state(**overrides)

    result = await planner.run(state)

    assert result["failed"] is True
    assert result["failure_reason"] == "malformed_load_payload"
    assert result["steps"][0]["status"] == "Failed"


@pytest.mark.anyio
async def test_create_workflow_run_failure_fails_run_not_local_uuid():
    """No local placeholder UUID on backend create failure - the run must fail visibly
    instead of continuing with a fabricated workflow_run_id that the backend never
    created and that every subsequent step report would fail against
    (plans/02-contracts-and-agent-fixes.md §1.5 / §2)."""
    state = _state()

    with patch(
        "freightlink_agent.agents.planner.create_workflow_run",
        new=AsyncMock(side_effect=BackendClientError("backend unreachable")),
    ):
        result = await planner.run(state)

    assert result["failed"] is True
    assert "create_workflow_run_failed" in result["failure_reason"]
    assert "workflow_run_id" not in result
    assert result["steps"][0]["status"] == "Failed"


@pytest.mark.anyio
async def test_run_fails_when_step_report_fails_even_though_plan_succeeded():
    """A report_step failure must flip the run to Failed, even though the plan itself
    was produced successfully - Agent 1's own success must never be claimed without a
    real persisted AgentStep behind it (plans/02-contracts-and-agent-fixes.md §1.5)."""
    state = _state()
    run_id = uuid.uuid4()

    mock_llm = MagicMock()
    mock_llm.plan = AsyncMock(
        return_value={
            "objective": "Find and assign a suitable carrier.",
            "steps": ["Evaluate candidate agencies"],
        }
    )

    with (
        patch(
            "freightlink_agent.agents.planner.create_workflow_run",
            new=AsyncMock(return_value=MagicMock(workflow_run_id=run_id)),
        ),
        patch("freightlink_agent.agents.planner.get_llm", return_value=mock_llm),
        patch(
            "freightlink_agent.agents.planner.report",
            new=AsyncMock(side_effect=BackendClientError("network error")),
        ),
    ):
        result = await planner.run(state)

    assert result["failed"] is True
    assert "report_step_failed" in result["failure_reason"]
    assert result["steps"][-1]["status"] == "Failed"


@pytest.mark.anyio
async def test_run_succeeds_and_reports_step():
    state = _state()
    run_id = uuid.uuid4()

    mock_llm = MagicMock()
    mock_llm.plan = AsyncMock(
        return_value={
            "objective": "Find and assign a suitable carrier.",
            "steps": ["Evaluate candidate agencies"],
        }
    )

    with (
        patch(
            "freightlink_agent.agents.planner.create_workflow_run",
            new=AsyncMock(return_value=MagicMock(workflow_run_id=run_id)),
        ),
        patch("freightlink_agent.agents.planner.get_llm", return_value=mock_llm),
        patch("freightlink_agent.agents.planner.report", new=AsyncMock()) as mock_report,
    ):
        result = await planner.run(state)

    assert result.get("failed") is not True
    assert result["workflow_run_id"] == run_id
    assert result["objective"] == "Find and assign a suitable carrier."
    mock_report.assert_awaited_once()
    assert result["steps"][-1]["status"] == "Succeeded"
