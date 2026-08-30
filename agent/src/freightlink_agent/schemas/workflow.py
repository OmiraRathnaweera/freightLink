from typing import Any
from uuid import UUID

from .base import CamelModel


class WorkflowRunRequest(CamelModel):
    """What triggers POST /workflows/run."""

    load_id: UUID
    triggered_by_user_id: UUID
    attempt_no: int
    load_context: dict[str, Any]


class WorkflowRunResponse(CamelModel):
    """What POST /workflows/run returns once Agent 1 has produced a plan."""

    workflow_run_id: UUID
    objective: str
    plan_json: str


class CreateWorkflowRunRequest(CamelModel):
    """Mirrors the fields of backend/Entities/AgentWorkflowRun.cs that this
    service supplies. POSTed to /internal/agent-workflow-runs."""

    load_id: UUID
    triggered_by_user_id: UUID
    attempt_no: int


class CreateWorkflowRunResponse(CamelModel):
    """The real workflow_run_id assigned by the backend - never generated
    locally as a placeholder."""

    workflow_run_id: UUID
