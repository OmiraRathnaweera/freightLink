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

from fastapi import Depends, FastAPI, HTTPException
from fastapi.security import APIKeyHeader

from freightlink_agent.core.config import get_settings
from freightlink_agent.graph.pipeline import get_pipeline
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.workflow import WorkflowRunRequest, WorkflowRunResponse

settings = get_settings()
logging.basicConfig(level=settings.log_level)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="FreightLink Agentic AI Service",
    description=(
        "Agent 1 (Planner) of FreightLink's Agentic AI pipeline (ADR-007). "
        "Called by the ASP.NET Core backend to start a match run; creates "
        "the AgentWorkflowRun on the backend, produces an objective and a "
        "plan constrained to the fixed pipeline stages via a structured "
        "Gemini/Ollama call, and reports its own step back to the backend."
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


@app.get(
    "/health",
    tags=["Health"],
    summary="Liveness check",
    description="Always returns ok if the process is up - does not check the backend or the LLM provider.",
)
async def health() -> dict[str, str]:
    return {"status": "ok"}
