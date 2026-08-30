"""Shared plumbing every agent node calls after finishing, so the report
shape and error handling live in one place instead of being duplicated
across agents/planner.py, domain_analysis.py, matching_pricing.py, and
validation_safety.py.
"""

import functools
import json
import logging
import time
from collections.abc import Awaitable, Callable
from datetime import UTC, datetime
from typing import Any
from uuid import UUID

from freightlink_agent.schemas.callback import ReportAgentStepRequest, ReportMatchCandidate, ReportToolCall
from freightlink_agent.schemas.enums import AgentRole, AgentStepStatus
from freightlink_agent.tools.internal_api import report_step

logger = logging.getLogger(__name__)


async def report(
    *,
    run_id: UUID,
    step_no: int,
    agent_role: AgentRole,
    status: AgentStepStatus,
    started_at: datetime,
    input_data: dict[str, Any] | None = None,
    output_data: dict[str, Any] | None = None,
    error_message: str | None = None,
    tool_calls: list[ReportToolCall] | None = None,
    match_candidates: list[ReportMatchCandidate] | None = None,
) -> None:
    """Builds a ReportAgentStepRequest and POSTs it to the backend.

    The report for step_no=4 (ValidationSafety) succeeding is what flips
    AgentWorkflowRun.Status -> AwaitingApproval on the backend side - the
    human-in-the-loop handoff. A Failed-status report at any step is what
    flips it to Failed (safe failure). Neither of those side effects lives
    here; this function only sends the report.
    """
    completed_at = datetime.now(UTC)
    duration_ms = int((completed_at - started_at).total_seconds() * 1000)

    request = ReportAgentStepRequest(
        step_no=step_no,
        agent_role=agent_role,
        status=status,
        input_json=json.dumps(input_data) if input_data is not None else None,
        output_json=json.dumps(output_data) if output_data is not None else None,
        error_message=error_message,
        duration_ms=duration_ms,
        started_at=started_at,
        completed_at=completed_at,
        tool_calls=tool_calls or [],
        match_candidates=match_candidates,
    )

    ok = await report_step(run_id, request)
    if not ok:
        logger.error(
            "Failed to report step %s (%s, run %s) to the backend after retry - "
            "the backend has no other way to learn this happened.",
            step_no,
            agent_role,
            run_id,
        )


def now() -> datetime:
    return datetime.now(UTC)


def timer() -> float:
    return time.monotonic()


def reported_step(step_no: int, agent_role: AgentRole) -> Callable:
    """Safety net around an agent node: each node already calls report()
    itself on its own expected paths (success, or a controlled failure like
    "no eligible agencies"). This decorator only fires for *unexpected*
    exceptions (e.g. the LLM provider is completely unreachable) that would
    otherwise propagate past the node without the backend ever learning the
    run stalled - which would leave AgentWorkflowRun stuck in Running
    forever with no further callback to explain why.
    """

    def decorator(func: Callable[[dict], Awaitable[dict]]) -> Callable[[dict], Awaitable[dict]]:
        @functools.wraps(func)
        async def wrapper(state: dict) -> dict:
            started = now()
            try:
                return await func(state)
            except Exception as exc:  # noqa: BLE001 - last-resort per-step safety net
                logger.exception("Agent step %s (%s) raised an unhandled exception", step_no, agent_role)
                await report(
                    run_id=state["run_id"],
                    step_no=step_no,
                    agent_role=agent_role,
                    status="Failed",
                    started_at=started,
                    error_message=f"Unhandled exception: {exc}",
                )
                return {"failed": True, "failure_reason": f"unhandled_exception_step_{step_no}"}

        return wrapper

    return decorator
