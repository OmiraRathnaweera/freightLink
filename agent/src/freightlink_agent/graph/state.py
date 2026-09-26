from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field

from freightlink_agent.schemas.enums import VehicleClass
from freightlink_agent.schemas.matching import CandidateAgency


class WorkflowState(BaseModel):
    """State threaded through the pipeline across all agents (ADR-007, ADR-010)."""

    load_id: UUID
    triggered_by_user_id: UUID
    attempt_no: int
    load_context: dict[str, Any]

    # --- Agent 1: Planner / Coordinator ---
    workflow_run_id: UUID | None = None
    """Null until Agent 1 creates the AgentWorkflowRun row or generated for offline match."""
    objective: str | None = None
    plan_json: str | None = None
    plan: dict[str, Any] | None = None

    # Consolidated audit records persisted by backend in one transaction
    steps: list[dict[str, Any]] = Field(default_factory=list)

    # --- Agent 2: Domain Analysis ---
    candidates: list[dict[str, Any]] = Field(default_factory=list)
    candidate_shortlist: list[CandidateAgency] = Field(default_factory=list)
    """Eligible active agencies passing compliance, capacity, and proximity checks (top 5)."""

    # --- Agent 3: Matching & Pricing ---
    tool_calls: list[dict[str, Any]] = Field(default_factory=list)
    ranked_five: list[dict[str, Any]] = Field(default_factory=list)
    most_suitable: dict[str, Any] | None = None

    selected_agency_id: UUID | None = None
    selected_agency_name: str | None = None
    suggested_vehicle_class: VehicleClass | None = None
    eta_minutes: int | None = None
    positioning_distance_km: float | None = None
    cargo_distance_km: float | None = None
    proposed_price: float | None = None
    pricing_breakdown: dict[str, Any] | None = None
    selection_justification: str | None = None
    assigned_vehicle: dict[str, Any] | None = None
    assigned_driver: dict[str, Any] | None = None

    # --- Agent 4: Validation & Safety ---
    validation: dict[str, Any] | None = None

    # --- Pipeline Status & Failure Tracking ---
    failed: bool = False
    failure_reason: str | None = None

