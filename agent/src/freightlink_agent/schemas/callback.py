from datetime import datetime

from .base import CamelModel
from .enums import AgentRole, AgentStepStatus


class ReportAgentStepRequest(CamelModel):
    """Mirrors backend/Entities/AgentStep.cs exactly.

    POSTed once, for Agent 1's single step, to
    POST /internal/agent-workflow-runs/{workflowRunId}/steps.
    """

    step_no: int
    agent_role: AgentRole
    status: AgentStepStatus
    input_json: str | None = None
    output_json: str | None = None
    error_message: str | None = None
    duration_ms: int | None = None
    started_at: datetime
    completed_at: datetime | None = None
