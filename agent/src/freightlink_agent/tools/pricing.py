"""Pricing tool (get_price_estimate).

Calls the internal ASP.NET Core backend pricing endpoint (POST /internal/pricing/estimate).
Calculates estimated price based on ADR-015 formula:
baseFare + (distanceKm * ratePerKm) + (weightKg * ratePerKg)
using the actual cargo leg distance (pickup -> dropoff) and suggested vehicle class.
"""

import asyncio
import logging
import time
from typing import Any

from freightlink_agent.core.backend_client import BackendClientError, estimate_pricing
from freightlink_agent.schemas.matching import EstimatePricingRequest, EstimatePricingResponse

logger = logging.getLogger(__name__)


async def get_price_estimate(
    request: EstimatePricingRequest,
) -> tuple[EstimatePricingResponse | None, dict[str, Any]]:
    """Invokes backend internal pricing estimator with 1-retry backoff reliability rule.

    Returns (response, telemetry) where telemetry captures timing and status
    for auditing.
    """
    start_time = time.monotonic()
    req_dict = request.model_dump(by_alias=True)
    last_error = ""

    for attempt in range(2):
        try:
            response = await estimate_pricing(request)
            duration_ms = int((time.monotonic() - start_time) * 1000)
            telemetry = {
                "durationMs": duration_ms,
                "httpStatusCode": 200,
                "request": req_dict,
                "response": response.model_dump(by_alias=True),
                "success": True,
            }
            return response, telemetry
        except BackendClientError as exc:
            last_error = str(exc)
            if attempt == 0:
                logger.warning("Price estimation call failed (%s), retrying once with backoff...", last_error)
                await asyncio.sleep(0.5)

    duration_ms = int((time.monotonic() - start_time) * 1000)
    err_msg = f"hold for review: pricing estimation failed after retry: {last_error}"
    logger.error("Failed to compute price estimate for load %s after retry: %s", request.load_id, last_error)
    telemetry = {
        "durationMs": duration_ms,
        "httpStatusCode": 500,
        "request": req_dict,
        "response": {"status": "hold for review", "error": last_error},
        "error": err_msg,
        "success": False,
    }
    return None, telemetry
