from decimal import Decimal
from uuid import UUID

from .base import CamelModel
from .enums import VehicleClass


class EstimatePricingRequest(CamelModel):
    """Mirrors backend/DTOs/Internal/EstimatePricingRequestDto.cs exactly."""

    load_id: UUID
    suggested_vehicle_class: VehicleClass
    distance_km: Decimal


class PricingEstimateResponse(CamelModel):
    """Mirrors backend/DTOs/Internal/PricingEstimateResponseDto.cs exactly."""

    load_id: UUID
    estimated_price: Decimal
    distance_km: Decimal
    vehicle_class: VehicleClass
    rate_per_km: Decimal
    rate_per_kg: Decimal
    base_fare: Decimal


class PriceEstimateResult(CamelModel):
    """Wraps a PricingEstimateResponse with the same diagnostic data as
    RouteEtaResult, for building a ReportToolCall record."""

    success: bool
    response: PricingEstimateResponse | None = None
    error_message: str | None = None

    request_json: str | None = None
    response_json: str | None = None
    http_status_code: int | None = None
    duration_ms: int | None = None


class RouteEtaResult(CamelModel):
    """Result of a direct OpenRouteService call (this service's own shape,
    not a backend DTO — ORS is never proxied through the backend).

    Carries enough diagnostic data (request/response JSON, timing, HTTP
    status) for the calling agent to build a ReportToolCall record for the
    backend's ToolCall audit trail, in addition to the parsed result.
    """

    success: bool
    distance_km: Decimal | None = None
    eta_minutes: int | None = None
    summary: str | None = None
    error_message: str | None = None

    request_json: str | None = None
    response_json: str | None = None
    http_status_code: int | None = None
    duration_ms: int | None = None
