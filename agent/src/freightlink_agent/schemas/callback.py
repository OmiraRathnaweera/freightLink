from datetime import datetime
from decimal import Decimal
from uuid import UUID

from .base import CamelModel
from .enums import AgentRole, AgentStepStatus, ToolName


class ReportToolCall(CamelModel):
    """Mirrors backend/DTOs/Internal/ReportToolCallDto.cs exactly."""

    tool_name: ToolName
    attempt_no: int
    request_json: str | None = None
    response_json: str | None = None
    success: bool
    http_status_code: int | None = None
    duration_ms: int | None = None
    error_message: str | None = None


class ReportMatchCandidate(CamelModel):
    """Mirrors backend/DTOs/Internal/ReportMatchCandidateDto.cs exactly.

    All routing/pricing fields are optional: Agent 2's report inserts new rows
    with only eligibility data populated; Agent 3's report updates the top-5
    subset's routing/pricing fields on rows that already exist.
    """

    agency_id: UUID
    rank: int | None = None
    eligibility_score: Decimal | None = None
    eligible: bool
    rejection_reason: str | None = None
    eta_minutes: int | None = None
    distance_km: Decimal | None = None
    estimated_price: Decimal | None = None
    is_highlighted: bool | None = None


class ReportAgentStepRequest(CamelModel):
    """Mirrors backend/DTOs/Internal/ReportAgentStepRequestDto.cs exactly.

    POSTed once per agent (4 times per run) to
    POST /internal/workflows/{runId}/steps. The report for the final
    (ValidationSafety) step succeeding is what the backend uses to flip
    AgentWorkflowRun.Status -> AwaitingApproval (the human-in-the-loop
    handoff) - there is no separate "finalize" call.
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
    tool_calls: list[ReportToolCall] = []
    match_candidates: list[ReportMatchCandidate] | None = None
