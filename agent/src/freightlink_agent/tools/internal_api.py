"""The two allow-listed calls this service makes into the ASP.NET Core
backend: estimate_price (Agent 3's second tool) and report_step (this
service's only durable-write path - see graph/step_reporter.py).
"""

import json
import time
from uuid import UUID

import httpx

from freightlink_agent.core.config import get_settings
from freightlink_agent.schemas.callback import ReportAgentStepRequest
from freightlink_agent.schemas.tools import EstimatePricingRequest, PriceEstimateResult, PricingEstimateResponse

_TIMEOUT_SECONDS = 10.0


def _headers() -> dict[str, str]:
    settings = get_settings()
    return {
        "X-Internal-Api-Key": settings.backend_internal_api_key,
        "Content-Type": "application/json",
    }


async def estimate_price(request: EstimatePricingRequest) -> PriceEstimateResult:
    """POST /internal/pricing/estimate - the pre-existing, already-built
    backend endpoint. Called once per run, for the #1-ranked candidate only
    (or again at approve-time if the shipper picks a different one of the
    top 5 - see WorkflowsController.Approve).
    """
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/pricing/estimate"
    body = request.model_dump(mode="json", by_alias=True)

    started = time.monotonic()
    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
            response = await client.post(url, json=body, headers=_headers())
        duration_ms = int((time.monotonic() - started) * 1000)
        response_text = response.text

        if response.status_code != 200:
            return PriceEstimateResult(
                success=False,
                error_message=f"/internal/pricing/estimate returned {response.status_code}: {response_text[:500]}",
                request_json=json.dumps(body),
                response_json=response_text,
                http_status_code=response.status_code,
                duration_ms=duration_ms,
            )

        parsed = PricingEstimateResponse.model_validate(response.json())
        return PriceEstimateResult(
            success=True,
            response=parsed,
            request_json=json.dumps(body),
            response_json=response_text,
            http_status_code=response.status_code,
            duration_ms=duration_ms,
        )
    except httpx.HTTPError as exc:
        duration_ms = int((time.monotonic() - started) * 1000)
        return PriceEstimateResult(
            success=False,
            error_message=f"/internal/pricing/estimate request failed: {exc}",
            request_json=json.dumps(body),
            duration_ms=duration_ms,
        )


async def report_step(run_id: UUID, step: ReportAgentStepRequest) -> bool:
    """POST /internal/workflows/{runId}/steps - the only way this service
    ever writes anything durable. Retries once on failure; if it still
    fails, logs and lets the graph continue/fail on its own terms, since the
    backend has no other way to learn this step happened.
    """
    settings = get_settings()
    url = f"{settings.backend_base_url}/internal/workflows/{run_id}/steps"
    body = step.model_dump(mode="json", by_alias=True)

    for attempt in range(2):
        try:
            async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
                response = await client.post(url, json=body, headers=_headers())
            if response.status_code < 300:
                return True
        except httpx.HTTPError:
            pass
        if attempt == 0:
            continue

    return False
