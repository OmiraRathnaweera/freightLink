"""FastAPI entry point for the FreightLink Agentic AI service.

Exposes exactly one route the backend calls: POST /workflows/start. This is
deliberately not named the same as the backend's own POST /workflows/match,
to avoid confusion across the two services - this one only *starts* a run
and returns immediately (202); the real work happens as a background task
that reports its own progress back to the backend (see
graph/step_reporter.py), not in this request/response cycle.
"""

import hmac
import logging

from fastapi import BackgroundTasks, FastAPI, Header, HTTPException

from freightlink_agent.core.config import get_settings
from freightlink_agent.graph.pipeline import get_pipeline
from freightlink_agent.schemas.workflow import WorkflowStartAck, WorkflowStartRequest

settings = get_settings()
logging.basicConfig(level=settings.log_level)
logger = logging.getLogger(__name__)

app = FastAPI(title="FreightLink Agentic AI Service")


def _check_shared_secret(x_internal_api_key: str | None) -> None:
    current = get_settings()
    if not current.shared_secret:
        # Fail closed: refuse to run unauthenticated rather than silently
        # accepting every request when the secret was never configured.
        raise HTTPException(status_code=500, detail="SHARED_SECRET is not configured")
    if not x_internal_api_key or not hmac.compare_digest(x_internal_api_key, current.shared_secret):
        raise HTTPException(status_code=401, detail="A valid X-Internal-Api-Key header is required")


async def _run_pipeline(request: WorkflowStartRequest) -> None:
    pipeline = get_pipeline()
    initial_state = {"run_id": request.workflow_run_id, "request": request}
    try:
        await pipeline.ainvoke(initial_state)
    except Exception:  # noqa: BLE001 - last-resort log, the run is otherwise unobserved
        logger.exception("Unhandled error running workflow %s", request.workflow_run_id)


@app.post("/workflows/start", response_model=WorkflowStartAck, status_code=202)
async def start_workflow(
    request: WorkflowStartRequest,
    background_tasks: BackgroundTasks,
    x_internal_api_key: str | None = Header(default=None),
) -> WorkflowStartAck:
    _check_shared_secret(x_internal_api_key)
    background_tasks.add_task(_run_pipeline, request)
    return WorkflowStartAck(accepted=True, workflow_run_id=request.workflow_run_id)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
