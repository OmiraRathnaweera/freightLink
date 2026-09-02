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
        examples=[{"weightKg": 500, "volumeM3": 3.2, "cargoDescription": "Palletized dry goods"}],
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
