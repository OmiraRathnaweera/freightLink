"""The two backend calls Agent 1 makes - this service's entire outbound
integration surface today. No tool-use calls (routing, pricing) belong
here: Agent 1 has no tool-use responsibility, that's Agent 3's job once
it exists.
"""

import logging
from uuid import UUID

import httpx

from freightlink_agent.core.config import get_settings
from freightlink_agent.schemas.callback import ReportAgentStepRequest
from freightlink_agent.schemas.matching import (
    CreateToolCallRequest,
    EstimatePricingRequest,
    EstimatePricingResponse,
)
from freightlink_agent.schemas.workflow import CreateWorkflowRunRequest, CreateWorkflowRunResponse

logger = logging.getLogger(__name__)

_TIMEOUT_SECONDS = 10.0


class BackendClientError(Exception):
    """Raised whenever a required backend call can't be completed -
    timeout, transport error, or non-2xx response. Callers must not treat
    a call that raised this as if it had succeeded."""


def _headers() -> dict[str, str]:
    settings = get_settings()
    return {
        "X-Internal-Api-Key": settings.internal_api_key,
        "Content-Type": "application/json",
    }


async def create_workflow_run(request: CreateWorkflowRunRequest) -> CreateWorkflowRunResponse:
    """POST /internal/agent-workflow-runs - the only way this service ever
    obtains a real workflow_run_id. There is no local placeholder UUID:
    if this call fails, the run cannot proceed, full stop.
    """
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/agent-workflow-runs"
    body = request.model_dump(mode="json", by_alias=True)

    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
            response = await client.post(url, json=body, headers=_headers())
    except httpx.HTTPError as exc:
        raise BackendClientError(f"POST /internal/agent-workflow-runs failed: {exc}") from exc

    if response.status_code >= 300:
        raise BackendClientError(
            f"POST /internal/agent-workflow-runs returned {response.status_code}: {response.text[:500]}"
        )

    return CreateWorkflowRunResponse.model_validate(response.json())


async def report_step(workflow_run_id: UUID, step: ReportAgentStepRequest) -> UUID | None:
    """POST /internal/agent-workflow-runs/{workflowRunId}/steps - reports what an agent did.

    Retries once on failure; raises BackendClientError if it still fails.
    Returns the agent_step_id assigned by the backend.
    """
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/agent-workflow-runs/{workflow_run_id}/steps"
    body = step.model_dump(mode="json", by_alias=True)

    last_error = ""
    for attempt in range(2):
        try:
            async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
                response = await client.post(url, json=body, headers=_headers())
            if response.status_code < 300:
                data = response.json() if response.content else {}
                step_id_str = data.get("agentStepId") or data.get("agent_step_id")
                return UUID(step_id_str) if step_id_str else None
            last_error = f"returned {response.status_code}: {response.text[:500]}"
        except httpx.HTTPError as exc:
            last_error = str(exc)
        if attempt == 0:
            logger.warning("Report step call failed (%s), retrying once", last_error)

    raise BackendClientError(
        f"POST /internal/agent-workflow-runs/{workflow_run_id}/steps failed after retry: {last_error}"
    )



async def estimate_pricing(request: EstimatePricingRequest) -> EstimatePricingResponse:
    """POST /internal/pricing/estimate - called by Agent 3 to price the job.

    The backend calculates the price via ADR-015 formula and writes
    Load.EstimatedPrice directly to the database.
    """
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/pricing/estimate"
    body = request.model_dump(mode="json", by_alias=True)

    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
            response = await client.post(url, json=body, headers=_headers())
    except httpx.HTTPError as exc:
        raise BackendClientError(f"POST /internal/pricing/estimate failed: {exc}") from exc

    if response.status_code >= 300:
        raise BackendClientError(
            f"POST /internal/pricing/estimate returned {response.status_code}: {response.text[:500]}"
        )

    return EstimatePricingResponse.model_validate(response.json())


async def record_tool_call(workflow_run_id: UUID, tool_call: CreateToolCallRequest) -> None:
    """POST /internal/agent-workflow-runs/{workflowRunId}/tool-calls - persists

    a ToolCall audit row in Postgres for Agent 3's tool invocations.
    If the backend endpoint is not yet deployed, logs a warning rather than
    crashing the entire matching run.
    """
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/agent-workflow-runs/{workflow_run_id}/tool-calls"
    body = tool_call.model_dump(mode="json", by_alias=True)

    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
            response = await client.post(url, json=body, headers=_headers())
        if response.status_code == 404:
            logger.warning(
                "POST /internal/agent-workflow-runs/%s/tool-calls returned 404 (endpoint not yet deployed)",
                workflow_run_id,
            )
            return
        if response.status_code >= 300:
            logger.warning(
                "Failed to persist ToolCall row: %s (%s)",
                response.status_code,
                response.text[:200],
            )
    except httpx.HTTPError as exc:
        logger.warning("Could not record ToolCall for run %s: %s", workflow_run_id, exc)

