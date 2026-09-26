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


async def report_step(workflow_run_id: UUID, step: ReportAgentStepRequest) -> None:
    """POST /internal/agent-workflow-runs/{workflowRunId}/steps - the only
    way this service ever reports what Agent 1 did. Retries once on
    failure; raises BackendClientError if it still fails rather than
    silently swallowing the error, since the backend has no other way to
    learn this step happened.
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
                return
            last_error = f"returned {response.status_code}: {response.text[:500]}"
        except httpx.HTTPError as exc:
            last_error = str(exc)
        if attempt == 0:
            logger.warning("Report step call failed (%s), retrying once", last_error)

    raise BackendClientError(
        f"POST /internal/agent-workflow-runs/{workflow_run_id}/steps failed after retry: {last_error}"
    )

from freightlink_agent.schemas.domain import Agency
from freightlink_agent.schemas.callback import CreateMatchCandidateRequest

async def get_active_agencies() -> list[Agency]:
    """GET /internal/agencies - Fetches all agencies for Agent 2."""
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/agencies"
    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
            response = await client.get(url, headers=_headers())
    except httpx.HTTPError as exc:
        raise BackendClientError(f"GET /internal/agencies failed: {exc}") from exc

    if response.status_code >= 300:
        raise BackendClientError(
            f"GET /internal/agencies returned {response.status_code}: {response.text[:500]}"
        )
    return [Agency.model_validate(agency) for agency in response.json()]

async def create_match_candidate(workflow_run_id: UUID, candidate: CreateMatchCandidateRequest) -> None:
    """POST /internal/agent-workflow-runs/{workflowRunId}/match-candidates"""
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/agent-workflow-runs/{workflow_run_id}/match-candidates"
    body = candidate.model_dump(mode="json", by_alias=True)

    last_error = ""
    for attempt in range(2):
        try:
            async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
                response = await client.post(url, json=body, headers=_headers())
            if response.status_code < 300:
                return
            last_error = f"returned {response.status_code}: {response.text[:500]}"
        except httpx.HTTPError as exc:
            last_error = str(exc)
        if attempt == 0:
            logger.warning("Create match candidate call failed (%s), retrying once", last_error)

    raise BackendClientError(
        f"POST /internal/agent-workflow-runs/{workflow_run_id}/match-candidates failed after retry: {last_error}"
    )
