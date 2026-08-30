from dataclasses import dataclass
from typing import Any, TypedDict
from uuid import UUID

from freightlink_agent.schemas.agency import AgencyCandidate
from freightlink_agent.schemas.workflow import WorkflowStartRequest


@dataclass
class CandidateScore:
    """One agency evaluated by Agent 2, carried through to Agent 3."""

    agency: AgencyCandidate
    eligible: bool
    eligibility_score: float
    rejection_reason: str | None
    haversine_distance_km: float
    haversine_rank: int | None = None

    # Populated by Agent 3, for the top 3-5 only
    final_rank: int | None = None
    eta_minutes: int | None = None
    distance_km: float | None = None
    estimated_price: float | None = None
    is_highlighted: bool = False


class PipelineState(TypedDict, total=False):
    run_id: UUID
    request: WorkflowStartRequest

    objective: str
    plan_json: str

    all_candidates: list[CandidateScore]
    shortlist: list[CandidateScore]
    """Top 3-5 by haversine distance, handed to Agent 3."""
    top_candidates: list[CandidateScore]
    """The same shortlist, re-ranked by real ETA and enriched with pricing."""

    validation: dict[str, Any]

    failed: bool
    failure_reason: str
