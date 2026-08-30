from decimal import Decimal
from uuid import UUID

from .agency import AgencyCandidate
from .base import CamelModel
from .enums import VehicleClass


class LoadSummary(CamelModel):
    load_id: UUID
    cargo_description: str
    weight_kg: Decimal
    volume_m3: Decimal
    pickup_lat: Decimal
    pickup_lng: Decimal
    dropoff_lat: Decimal
    dropoff_lng: Decimal
    estimated_price: Decimal | None = None
    """Shipper's own pre-matching estimate (haversine-based), used by Agent 4
    as decision-support context per ADR-013 - not a gating condition."""


class WorkflowStartRequest(CamelModel):
    """What WorkflowService.StartMatchAsync sends to POST /workflows/start."""

    workflow_run_id: UUID
    attempt_no: int
    load: LoadSummary
    eligible_agencies: list[AgencyCandidate]
    preselected_vehicle_class: VehicleClass
    """Resolved server-side via IPricingConfigService.GetTierForWeightAndVolume
    - this service never re-derives the tie-break logic itself."""
    excluded_agency_ids: list[UUID] = []
    """Agencies that declined this load in a prior attempt (ADR-018 retry) -
    already excluded from eligible_agencies, carried here only for the
    Planner's PlanJson audit trail."""

    resume_from_step_no: int | None = None
    """Set only on a shipper-requested revise (/workflows/{id}/revise): the
    graph re-enters at Agent 3 instead of redoing Agent 1-2."""
    prior_plan_json: str | None = None
    prior_match_candidates: list[dict] | None = None
    """Agent 2's previously-reported eligible set, replayed as input when
    resuming - avoids re-querying/re-scoring agencies on a revise."""


class WorkflowStartAck(CamelModel):
    """The only response WorkflowService.StartMatchAsync waits for - actual
    progress arrives later via this service's callbacks to the backend."""

    accepted: bool
    workflow_run_id: UUID
