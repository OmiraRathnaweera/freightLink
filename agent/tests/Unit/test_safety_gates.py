"""Agentic AI safety-gate tests (Assignment 2, area G).

Covers the properties the existing suites only touch indirectly:
  * approval enforcement  - Agent 4 can never complete a run; it only ever pauses or fails
  * tool allow-list       - only the two allow-listed tools can be recorded (mirrors ck_toolcall_allowlist)
  * input boundaries      - pricing/tool-call schemas reject out-of-range values
  * service authentication - /workflows/run refuses callers without the shared secret
  * safe failure          - a backend outage yields a genuine failure, never a fabricated success
"""
import asyncio
import os
import sys
import uuid
from datetime import datetime, timezone
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from httpx import ASGITransport, AsyncClient
from pydantic import ValidationError

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "src")))
os.environ.setdefault("SHARED_SECRET", "test-shared-secret")
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-api-key")

from freightlink_agent.agents.validation_safety import run as run_agent4
from freightlink_agent.core.backend_client import BackendClientError
from freightlink_agent.main import app
from freightlink_agent.schemas.matching import CandidateAgency, CreateToolCallRequest, EstimatePricingRequest

from test_validator import _valid_state  # reuse the fully-valid Agent 4 baseline


# ---------- approval enforcement ----------

@pytest.mark.parametrize("attempt_no", [1, 2, 3])
@pytest.mark.parametrize("price", [1.0, 48000.0, 5_000_000.0])
def test_agent4_never_completes_a_run_without_human_approval(attempt_no, price):
    """Every valid run - first attempt or retry, cheap or very expensive - must stop at
    AwaitingApproval. There must be no status path that skips the Shipper's approval."""
    state = _valid_state(workflow_run_id=uuid.uuid4(), attempt_no=attempt_no, proposed_price=price)

    with patch("freightlink_agent.agents.validation_safety.report", new_callable=AsyncMock):
        result = asyncio.run(run_agent4(state))

    assert result["status"] == "AwaitingApproval"
    assert result["status"] not in {"Succeeded", "Completed", "Approved"}


def test_agent4_rejected_run_is_failed_not_awaiting_approval():
    """An invalid proposal must not be put in front of the Shipper for approval at all."""
    state = _valid_state(workflow_run_id=uuid.uuid4(), candidate_shortlist=[])

    with patch("freightlink_agent.agents.validation_safety.report", new_callable=AsyncMock):
        result = asyncio.run(run_agent4(state))

    assert result["status"] == "Failed"
    assert result["is_valid"] is False


def test_agent4_never_marks_its_own_step_approved():
    """The persisted Agent 4 step reports only Succeeded/Failed; approval is recorded
    by the backend ApprovalDecision, never by the agent."""
    state = _valid_state(workflow_run_id=uuid.uuid4())

    with patch("freightlink_agent.agents.validation_safety.report", new_callable=AsyncMock) as report:
        asyncio.run(run_agent4(state))

    output = report.call_args.kwargs["output_data"]
    assert output["status"] == "AwaitingApproval"
    assert "approvedBy" not in output and "approved" not in output


# ---------- tool allow-list ----------

@pytest.mark.parametrize("tool", ["get_route_and_eta", "estimate_price"])
def test_allow_listed_tools_are_accepted(tool):
    request = CreateToolCallRequest(tool_name=tool, called_at=datetime.now(timezone.utc))
    assert request.tool_name == tool


@pytest.mark.parametrize(
    "tool",
    ["delete_database", "shell_exec", "send_email", "", "GET_ROUTE_AND_ETA", "estimate_price; DROP TABLE x", None],
)
def test_non_allow_listed_tools_are_rejected(tool):
    with pytest.raises(ValidationError):
        CreateToolCallRequest(tool_name=tool, called_at=datetime.now(timezone.utc))


# ---------- boundary / invalid input ----------

@pytest.mark.parametrize("distance", [0, 0.0, -1, -0.001])
def test_pricing_request_rejects_non_positive_distance(distance):
    with pytest.raises(ValidationError):
        EstimatePricingRequest(load_id=uuid.uuid4(), suggested_vehicle_class="MediumLorry", distance_km=distance)


def test_pricing_request_accepts_smallest_positive_distance():
    request = EstimatePricingRequest(load_id=uuid.uuid4(), suggested_vehicle_class="MediumLorry", distance_km=0.001)
    assert request.distance_km == 0.001


def test_pricing_request_rejects_unknown_vehicle_class():
    with pytest.raises(ValidationError):
        EstimatePricingRequest(load_id=uuid.uuid4(), suggested_vehicle_class="Spaceship", distance_km=10)


@pytest.mark.parametrize("attempt_no", [0, -1])
def test_tool_call_attempt_number_must_be_at_least_one(attempt_no):
    with pytest.raises(ValidationError):
        CreateToolCallRequest(tool_name="estimate_price", attempt_no=attempt_no, called_at=datetime.now(timezone.utc))


# ---------- service authentication on the agent API ----------

def _run_payload() -> dict:
    return {
        "loadId": str(uuid.uuid4()),
        "triggeredByUserId": str(uuid.uuid4()),
        "attemptNo": 1,
        "loadContext": {"weightKg": 500, "pickupLat": 6.9, "pickupLng": 79.8, "dropoffLat": 7.2, "dropoffLng": 80.6},
    }


@pytest.mark.anyio
@pytest.mark.parametrize("headers", [{}, {"X-Internal-Api-Key": "wrong"}, {"X-Internal-Api-Key": ""}])
async def test_workflows_run_rejects_missing_or_wrong_key(headers):
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/workflows/run", json=_run_payload(), headers=headers)

    assert response.status_code == 401


@pytest.mark.anyio
async def test_workflows_run_rejects_attempt_number_zero():
    payload = _run_payload() | {"attemptNo": 0}
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/workflows/run", json=payload, headers={"X-Internal-Api-Key": os.environ["SHARED_SECRET"]})

    assert response.status_code == 422


# ---------- safe failure ----------

@pytest.mark.anyio
async def test_backend_outage_yields_502_not_a_fabricated_success():
    """If the backend cannot create the workflow run, the API must fail loudly (502) rather
    than return a run id / plan it never persisted."""
    llm = MagicMock()
    llm.plan = AsyncMock(return_value={"objective": "x", "steps": ["a", "b", "c", "d"]})

    with (
        patch("freightlink_agent.agents.planner.get_llm", return_value=llm),
        patch("freightlink_agent.agents.planner.create_workflow_run", new=AsyncMock(side_effect=BackendClientError("backend down"))),
        patch("freightlink_agent.agents.planner.report", new=AsyncMock()),
    ):
        async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
            response = await client.post(
                "/workflows/run", json=_run_payload(), headers={"X-Internal-Api-Key": os.environ["SHARED_SECRET"]}
            )

    assert response.status_code == 502
    assert "workflow_run_id" not in response.text and "workflowRunId" not in response.text
