from typing import Any
from uuid import UUID

from pydantic import BaseModel


class WorkflowState(BaseModel):
    """State threaded through the pipeline. Only carries what Agent 1
    itself reads or writes - fields a later agent (2-4) would need don't
    belong here until that agent actually exists."""

    load_id: UUID
    triggered_by_user_id: UUID
    attempt_no: int
    load_context: dict[str, Any]

    workflow_run_id: UUID | None = None
    """Null until Agent 1 creates the AgentWorkflowRun row on the backend
    and gets the real id back."""
    objective: str | None = None
    plan_json: str | None = None

    failed: bool = False
    failure_reason: str | None = None
