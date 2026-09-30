"""FastAPI entry point for the FreightLink Agentic AI service.

Exposes POST /workflows/run, which synchronously runs the full 4-agent pipeline
(Planner -> DomainAnalysis -> MatchingPricing -> ValidationSafety, ADR-007) and
returns the resulting workflow_run_id, objective, and plan_json.

Swagger UI is served at /docs (ReDoc at /redoc) automatically by FastAPI
from the metadata below - no separate setup needed.

P0 consolidation note (see plans/01-python-service-consolidation.md §3): this file
previously also exposed POST /workflows/match, a consolidated single-response design
with no production consumer - the backend only ever calls /workflows/run. That route,
its request/response schemas' usage here, and the duplicate FastAPI app instance it
was spliced in with have been removed; /workflows/run is the only supported API surface.
"""

import hmac
import logging
from typing import Any
from uuid import UUID, uuid4

from fastapi import Depends, FastAPI, HTTPException
from fastapi.security import APIKeyHeader

from freightlink_agent.core.config import get_settings
from freightlink_agent.graph.pipeline import get_pipeline
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.matching import CandidateAgency
from freightlink_agent.schemas.workflow import WorkflowRunRequest, WorkflowRunResponse

settings = get_settings()
logging.basicConfig(level=settings.log_level)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="FreightLink Agentic AI Service",
    description=(
        "FreightLink's Agentic AI pipeline (ADR-007, ADR-010). "
        "Coordinates 4 agents (Planner, DomainAnalysis, MatchingPricing, ValidationSafety) "
        "behind POST /workflows/run, with safe-failure short-circuits and incremental "
        "per-agent audit persistence back to the ASP.NET Core backend."
    ),
    version="0.1.0",
    openapi_tags=[
        {"name": "Workflows", "description": "Triggering and running the agent pipeline."},
        {"name": "Health", "description": "Liveness check."},
    ],
)

_api_key_header = APIKeyHeader(
    name="X-Internal-Api-Key",
    description="Shared secret the backend sends when triggering a run. Must match this service's SHARED_SECRET.",
    auto_error=False,
)


async def require_shared_secret(x_internal_api_key: str | None = Depends(_api_key_header)) -> None:
    current = get_settings()
    if not current.shared_secret:
        # Fail closed: refuse to run unauthenticated rather than silently
        # accepting every request when the secret was never configured.
        raise HTTPException(status_code=500, detail="SHARED_SECRET is not configured")
    if not x_internal_api_key or not hmac.compare_digest(x_internal_api_key, current.shared_secret):
        raise HTTPException(status_code=401, detail="A valid X-Internal-Api-Key header is required")


def _parse_candidate_agencies(raw: list[Any]) -> list[CandidateAgency]:
    parsed: list[CandidateAgency] = []
    for c in raw:
        if isinstance(c, CandidateAgency):
            parsed.append(c)
        elif isinstance(c, dict):
            parsed.append(
                CandidateAgency(
                    agency_id=UUID(str(c.get("agencyId") or c.get("agency_id") or uuid4())),
                    name=str(c.get("name") or c.get("agencyName") or "Carrier"),
                    yard_lat=float(c.get("yardLat") or c.get("yard_lat") or 0.0),
                    yard_lng=float(c.get("yardLng") or c.get("yard_lng") or 0.0),
                    yard_address=str(c.get("yardAddress") or c.get("yard_address") or ""),
                    available_vehicle_classes=c.get("availableVehicleClasses")
                    or c.get("available_vehicle_classes")
                    or ["MediumLorry"],
                    available_vehicles=c.get("availableVehicles") or c.get("available_vehicles") or [],
                    active_drivers=c.get("activeDrivers") or c.get("active_drivers") or [],
                )
            )
    return parsed


@app.post(
    "/workflows/run",
    response_model=WorkflowRunResponse,
    tags=["Workflows"],
    summary="Start a workflow run",
    description=(
        "Synchronously runs the full 4-agent pipeline: Planner creates the "
        "AgentWorkflowRun on the backend and produces an objective/plan; "
        "DomainAnalysis, MatchingPricing, and ValidationSafety run in sequence "
        "with safe-failure short-circuits, each persisting its own AgentStep. "
        "Returns once the run reaches AwaitingApproval or fails."
    ),
    responses={
        401: {"description": "Missing or invalid X-Internal-Api-Key header."},
        500: {"description": "SHARED_SECRET is not configured on this service."},
        502: {"description": "The pipeline failed - see the failure_reason in the error detail."},
    },
    dependencies=[Depends(require_shared_secret)],
)
async def run_workflow(request: WorkflowRunRequest) -> WorkflowRunResponse:
    raw_candidates = (
        request.candidate_agencies
        or request.load_context.get("candidateAgencies")
        or request.load_context.get("candidates")
        or []
    )
    parsed_candidates = _parse_candidate_agencies(raw_candidates)

    initial_state = WorkflowState(
        load_id=request.load_id,
        triggered_by_user_id=request.triggered_by_user_id,
        attempt_no=request.attempt_no,
        load_context=request.load_context,
        candidate_shortlist=parsed_candidates,
    )

    pipeline = get_pipeline()
    result = await pipeline.ainvoke(initial_state)

    if result.get("failed") or not result.get("workflow_run_id"):
        logger.error("Pipeline failed for load %s: %s", request.load_id, result.get("failure_reason"))
        raise HTTPException(
            status_code=502,
            detail=f"Pipeline failed: {result.get('failure_reason', 'unknown_error')}",
        )

    return WorkflowRunResponse(
        workflow_run_id=result["workflow_run_id"],
        objective=result["objective"],
        plan_json=result["plan_json"],
    )


@app.get(
    "/health",
    tags=["Health"],
    summary="Liveness check",
    description="Always returns ok if the process is up - does not check the backend or the LLM provider.",
)
async def health() -> dict[str, str]:
    return {"status": "ok"}
