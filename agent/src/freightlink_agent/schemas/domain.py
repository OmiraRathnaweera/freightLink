from datetime import date
from uuid import UUID

from pydantic import Field

from freightlink_agent.schemas.base import CamelModel


class VehicleClass(CamelModel):
    id: UUID
    name: str
    max_payload_kg: float
    max_volume_m3: float


class ComplianceDoc(CamelModel):
    id: UUID
    expiry_date: date


class Agency(CamelModel):
    id: UUID
    name: str
    status: str
    yard_lat: float
    yard_lng: float
    compliance: ComplianceDoc | None = None
    fleet: list[VehicleClass] = Field(default_factory=list)


class EvaluatedAgency(CamelModel):
    agency_id: UUID
    passed: bool
    rejection_reasons: list[str]


class RankedCandidate(CamelModel):
    agency_id: UUID
    haversine_distance_km: float
