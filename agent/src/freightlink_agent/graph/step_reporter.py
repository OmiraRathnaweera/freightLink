"""Builds Agent 1's AgentStep report and sends it to the backend.

Kept separate from agents/planner.py only because "build a
ReportAgentStepRequest and POST it" is a distinct concern from the
planning logic itself - not because more agents are expected to share it
yet. When Agent 2 is actually built, re-evaluate whether this is still
the right shape rather than assuming it generalizes as-is.
"""

import json
import logging
from datetime import UTC, datetime
from typing import Any
from uuid import UUID

from freightlink_agent.core.backend_client import report_step
from freightlink_agent.schemas.callback import ReportAgentStepRequest
from freightlink_agent.schemas.enums import AgentRole, AgentStepStatus

logger = logging.getLogger(__name__)


async def report(
    *,
    workflow_run_id: UUID,
    step_no: int,
    agent_role: AgentRole,
    status: AgentStepStatus,
    started_at: datetime,
    input_data: dict[str, Any] | None = None,
    output_data: dict[str, Any] | None = None,
    error_message: str | None = None,
) -> UUID | None:
    """Builds a ReportAgentStepRequest and POSTs it to the backend.

    Raises BackendClientError (propagated from report_step) if the report
    can't be delivered - the caller must decide how to surface that,
    since the backend has no other way to learn this step happened.
    Returns the agent_step_id assigned by the backend, or None.
    """
    completed_at = datetime.now(UTC)
    duration_ms = int((completed_at - started_at).total_seconds() * 1000)

    request = ReportAgentStepRequest(
        step_no=step_no,
        agent_role=agent_role,
        status=status,
        input_json=json.dumps(input_data, default=str) if input_data is not None else None,
        output_json=json.dumps(output_data, default=str) if output_data is not None else None,
        error_message=error_message,
        duration_ms=duration_ms,
        started_at=started_at,
        completed_at=completed_at,
    )

    return await report_step(workflow_run_id, request)


def now() -> datetime:
    return datetime.now(UTC)
