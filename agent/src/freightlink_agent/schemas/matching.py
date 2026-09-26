from datetime import datetime
from typing import Any
from uuid import UUID

from pydantic import Field

from .base import CamelModel
from .enums import ToolName, VehicleClass


class CandidateAgency(CamelModel):
    """An eligible candidate agency passed from Agent 2 (Domain Analysis) to Agent 3.

    Represents an active, compliant agency with verified fleet capacity that
    can service the load, located at a single physical yard (ADR-002).
    """

    agency_id: UUID
    name: str
    yard_lat: float
    yard_lng: float
    yard_address: str = ""
    available_vehicle_classes: list[VehicleClass] = Field(default_factory=list)
    available_vehicles: list[dict[str, Any]] = Field(default_factory=list)
    active_drivers: list[dict[str, Any]] = Field(default_factory=list)


class RouteAndEtaRequest(CamelModel):
    """Payload for route & ETA calculation tool (get_route_and_eta).

    Reused for both the agency yard -> load pickup positioning leg and the
    load pickup -> dropoff cargo leg.
    """

    origin_lat: float = Field(ge=-90.0, le=90.0)
    origin_lng: float = Field(ge=-180.0, le=180.0)
    destination_lat: float = Field(ge=-90.0, le=90.0)
    destination_lng: float = Field(ge=-180.0, le=180.0)


class RouteAndEtaResponse(CamelModel):
    """Result returned by get_route_and_eta.

    A routing failure is an in-band Success=False response with null distance
    and ETA, following the reliability rule in IRouteService.
    """

    distance_km: float | None = None
    eta_minutes: int | None = None
    success: bool = True
    error_message: str | None = None


class EstimatePricingRequest(CamelModel):
    """Payload sent to POST /internal/pricing/estimate on the ASP.NET Core backend.

    distance_km must strictly be the cargo leg (pickup -> dropoff), per ADR-015 addendum.
    """

    load_id: UUID
    suggested_vehicle_class: VehicleClass
    distance_km: float = Field(gt=0.0)


class EstimatePricingResponse(CamelModel):
    """Response returned from POST /internal/pricing/estimate."""

    load_id: UUID
    estimated_price: float
    distance_km: float
    vehicle_class: VehicleClass
    rate_per_km: float
    rate_per_kg: float
    base_fare: float


class CreateToolCallRequest(CamelModel):
    """Payload sent to backend to persist a ToolCall audit row in PostgreSQL.

    Mirrors backend/Entities/ToolCall.cs and its check constraints
    (ck_toolcall_allowlist, ck_toolcall_attempt, ck_toolcall_failure).
    """

    agent_step_id: UUID | None = None
    tool_name: ToolName
    attempt_no: int = Field(ge=1, default=1)
    request_json: str | None = None
    response_json: str | None = None
    success: bool = True
    http_status_code: int | None = None
    duration_ms: int | None = None
    error_message: str | None = None
    called_at: datetime


class CreateToolCallResponse(CamelModel):
    """Response returned when a ToolCall audit row is created."""

    tool_call_id: UUID
