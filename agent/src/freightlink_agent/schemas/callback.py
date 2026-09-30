from datetime import datetime
from uuid import UUID

from .base import CamelModel
from .enums import AgentRole, AgentStepStatus


class ReportAgentStepRequest(CamelModel):
    """Mirrors backend/Entities/AgentStep.cs exactly.

    POSTed once per agent, to POST /internal/agent-workflow-runs/{workflowRunId}/steps.
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


class MatchCandidateRequest(CamelModel):
    """Mirrors backend/DTOs/Internal/Candidates/MatchCandidateDto.cs exactly.

    POSTed as a list (the whole shortlist evaluation in one call) to
    POST /internal/agent-workflow-runs/{workflowRunId}/candidates by Agent 2
    (DomainAnalysis) - this is the real current contract confirmed by reading
    AgentWorkflowRunsController.cs, not the older GET-agencies/match-candidates
    pattern removed in the P0 consolidation (see plans/01-python-service-consolidation.md).
    """

    agency_id: UUID
    rank: int
    eligible: bool = True
    eligibility_score: float = 100.0
    rejection_reason: str | None = None
