"""FastAPI entry point for the FreightLink Agentic AI service.

Exposes exactly one route: POST /workflows/run. Runs the pipeline
synchronously and returns the resulting workflow_run_id, objective, and
plan_json directly in the response. There is no background-task/callback
split here: Agent 1 is the only agent that exists, and its own output is
exactly what the caller is waiting for - that changes once later agents
make a run take long enough to need an async ack + callback pattern.

Swagger UI is served at /docs (ReDoc at /redoc) automatically by FastAPI
from the metadata below - no separate setup needed.
"""

import hmac
import logging
from uuid import UUID, uuid4

from fastapi import Depends, FastAPI, HTTPException
from fastapi.security import APIKeyHeader

from freightlink_agent.core.config import get_settings
from freightlink_agent.graph.pipeline import get_pipeline
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.matching import CandidateAgency
from freightlink_agent.schemas.workflow import (
    WorkflowMatchRequest,
    WorkflowMatchResponse,
    WorkflowRunRequest,
    WorkflowRunResponse,
)

settings = get_settings()
logging.basicConfig(level=settings.log_level)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="FreightLink Agentic AI Service",
    description=(
        "FreightLink's Agentic AI pipeline (ADR-007, ADR-010). "
        "Coordinates 4 agents (Planner, DomainAnalysis, MatchingPricing, ValidationSafety) "
        "behind POST /workflows/match, with safe-failure short-circuits and consolidated "
        "audit persistence."
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


@app.post(
    "/workflows/run",
    response_model=WorkflowRunResponse,
    tags=["Workflows"],
    summary="Start a workflow run",
    description=(
        "Synchronously runs Agent 1 (Planner): creates the AgentWorkflowRun "
        "on the backend, generates the objective/plan via a structured LLM "
        "call, and reports the step back to the backend before responding. "
        "There is no separate poll-for-result step - the plan is returned "
        "directly in this response."
    ),
    responses={
        401: {"description": "Missing or invalid X-Internal-Api-Key header."},
        500: {"description": "SHARED_SECRET is not configured on this service."},
        502: {"description": "Agent 1 failed - see the failure_reason in the error detail."},
    },
    dependencies=[Depends(require_shared_secret)],
)
async def run_workflow(request: WorkflowRunRequest) -> WorkflowRunResponse:
    initial_state = WorkflowState(
        load_id=request.load_id,
        triggered_by_user_id=request.triggered_by_user_id,
        attempt_no=request.attempt_no,
        load_context=request.load_context,
    )

    pipeline = get_pipeline()
    result = await pipeline.ainvoke(initial_state)

    if result.get("failed") or not result.get("workflow_run_id"):
        logger.error("Planner failed for load %s: %s", request.load_id, result.get("failure_reason"))
        raise HTTPException(
            status_code=502,
            detail=f"Planner failed: {result.get('failure_reason', 'unknown_error')}",
        )

    return WorkflowRunResponse(
        workflow_run_id=result["workflow_run_id"],
        objective=result["objective"],
        plan_json=result["plan_json"],
    )


@app.post(
    "/workflows/match",
    response_model=WorkflowMatchResponse,
    tags=["Workflows"],
    summary="Run full 4-agent matching pipeline",
    description=(
        "Synchronously executes the 4-agent pipeline (Planner -> DomainAnalysis -> "
        "MatchingPricing -> ValidationSafety) with safe-failure short-circuits. "
        "Returns the consolidated results (plan, steps, candidates, toolCalls, "
        "rankedFive, mostSuitable, validation) for single-transaction persistence."
    ),
    responses={
        401: {"description": "Missing or invalid X-Internal-Api-Key header."},
        500: {"description": "SHARED_SECRET is not configured on this service."},
    },
    dependencies=[Depends(require_shared_secret)],
)
async def match_workflow(request: WorkflowMatchRequest) -> WorkflowMatchResponse:
    parsed_candidates: list[CandidateAgency] = []
    raw_candidates = (
        request.candidate_agencies
        or request.load_context.get("candidateAgencies")
        or request.load_context.get("candidates")
        or []
    )
    for c in raw_candidates:
        if isinstance(c, CandidateAgency):
            parsed_candidates.append(c)
        elif isinstance(c, dict):
            parsed_candidates.append(
                CandidateAgency(
                    agency_id=UUID(str(c.get("agencyId") or c.get("agency_id") or uuid4())),
                    name=str(c.get("name") or c.get("agencyName") or "Carrier"),
                    yard_lat=float(c.get("yardLat") or c.get("yard_lat") or 0.0),
                    yard_lng=float(c.get("yardLng") or c.get("yard_lng") or 0.0),
                    yard_address=str(c.get("yardAddress") or c.get("yard_address") or ""),
                    available_vehicle_classes=c.get("availableVehicleClasses")
                    or c.get("available_vehicle_classes")
                    or ["MediumLorry"],
                )
            )

    initial_state = WorkflowState(
        load_id=request.load_id,
        triggered_by_user_id=request.triggered_by_user_id,
        attempt_no=request.attempt_no,
        load_context=request.load_context,
        candidate_shortlist=parsed_candidates,
    )

    pipeline = get_pipeline()
    result = await pipeline.ainvoke(initial_state)

    if isinstance(result, WorkflowState):
        res_data = result.model_dump()
    elif isinstance(result, dict):
        res_data = result
    else:
        res_data = dict(result)

    return WorkflowMatchResponse(
        plan=res_data.get("plan") or {},
        steps=res_data.get("steps") or [],
        candidates=res_data.get("candidates") or [],
        tool_calls=res_data.get("tool_calls") or [],
        ranked_five=res_data.get("ranked_five") or [],
        most_suitable=res_data.get("most_suitable"),
        validation=res_data.get("validation") or {},
    )


@app.get(
    "/health",
    tags=["Health"],
    summary="Liveness check",
    description="Always returns ok if the process is up - does not check the backend or the LLM provider.",
)
async def health() -> dict[str, str]:
    return {"status": "ok"}
