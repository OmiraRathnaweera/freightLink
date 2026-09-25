from typing import Any
from uuid import UUID

from pydantic import Field

from .base import CamelModel


class WorkflowRunRequest(CamelModel):
    """What triggers POST /workflows/run."""

    load_id: UUID = Field(
        description="The Load this run is matching an agency for.",
        examples=["11111111-1111-1111-1111-111111111111"],
    )
    triggered_by_user_id: UUID = Field(
        description="The user (typically the shipper) whose action triggered this run.",
        examples=["22222222-2222-2222-2222-222222222222"],
    )
    attempt_no: int = Field(
        description="1 for the load's first match attempt; incremented on each automatic retry after an agency decline (ADR-018).",
        examples=[1],
        ge=1,
    )
    load_context: dict[str, Any] = Field(
        description="Free-form load details passed straight into Agent 1's prompt - not validated against a fixed shape.",
        examples=[
            {
                "pickupLat": 6.9271,
                "pickupLng": 79.8612,
                "dropoffLat": 7.2906,
                "dropoffLng": 80.6337,
                "weightKg": 500,
                "volumeM3": 3.2,
                "cargoDescription": "Palletized dry goods",
            }
        ],
    )


class WorkflowRunResponse(CamelModel):
    """What POST /workflows/run returns once Agent 1 has produced a plan."""

    workflow_run_id: UUID = Field(
        description="The real AgentWorkflowRun id assigned by the backend - never a locally generated placeholder.",
    )
    objective: str = Field(
        description="Agent 1's one-sentence objective for this run.",
        examples=["Find and confirm a suitable agency for this 500kg load."],
    )
    plan_json: str = Field(
        description="JSON-encoded {objective, steps} - steps are drawn only from the fixed pipeline stages.",
        examples=[
            '{"objective": "Find and confirm a suitable agency for this 500kg load.", '
            '"steps": ["Evaluate candidate agencies", "Select agency via routing", '
            '"Validate and get shipper approval", "Notify agency"]}'
        ],
    )


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


class WorkflowMatchRequest(CamelModel):
    """Payload for POST /workflows/match triggering the 4-agent LangGraph pipeline."""

    load_id: UUID = Field(
        description="The Load to match an agency for.",
        examples=["11111111-1111-1111-1111-111111111111"],
    )
    triggered_by_user_id: UUID = Field(
        description="The shipper whose action triggered this run.",
        examples=["22222222-2222-2222-2222-222222222222"],
    )
    attempt_no: int = Field(
        default=1,
        description="Match attempt number.",
        examples=[1],
        ge=1,
    )
    load_context: dict[str, Any] = Field(
        description="Load details including coordinates, weight, volume.",
        examples=[
            {
                "pickupLat": 6.9271,
                "pickupLng": 79.8612,
                "dropoffLat": 7.2906,
                "dropoffLng": 80.6337,
                "weightKg": 2500.0,
                "volumeM3": 8.0,
                "cargoDescription": "Industrial machine spare parts",
            }
        ],
    )
    candidate_agencies: list[dict[str, Any]] = Field(
        default_factory=list,
        description="Eligible candidate carriers passed for evaluation.",
        examples=[
            [
                {
                    "agencyId": "33333333-3333-3333-3333-333333333333",
                    "name": "Peliyagoda Logistics Yard",
                    "yardLat": 6.9667,
                    "yardLng": 79.8917,
                    "yardAddress": "12 Negombo Rd, Peliyagoda",
                    "availableVehicleClasses": ["MediumLorry", "ContainerTruck"],
                },
                {
                    "agencyId": "44444444-4444-4444-4444-444444444444",
                    "name": "Colombo Metro Transport",
                    "yardLat": 6.9319,
                    "yardLng": 79.8478,
                    "yardAddress": "Port Access Rd, Colombo",
                    "availableVehicleClasses": ["MiniTruck", "MediumLorry"],
                },
            ]
        ],
    )
    vehicle_classes: list[dict[str, Any]] = Field(default_factory=list)


class WorkflowMatchResponse(CamelModel):
    """Consolidated response contract returned by POST /workflows/match for one-transaction persistence."""

    plan: dict[str, Any] = Field(default_factory=dict)
    steps: list[dict[str, Any]] = Field(default_factory=list)
    candidates: list[dict[str, Any]] = Field(default_factory=list)
    tool_calls: list[dict[str, Any]] = Field(default_factory=list, alias="toolCalls")
    ranked_five: list[dict[str, Any]] = Field(default_factory=list, alias="rankedFive")
    most_suitable: dict[str, Any] | None = Field(default=None, alias="mostSuitable")
    validation: dict[str, Any] = Field(default_factory=dict)

