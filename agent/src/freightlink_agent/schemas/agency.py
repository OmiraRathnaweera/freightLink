from datetime import date
from decimal import Decimal
from uuid import UUID

from .base import CamelModel


class VehicleSummary(CamelModel):
    vehicle_id: UUID
    vehicle_type: str
    capacity_kg: Decimal
    volume_m3: Decimal
    status: str


class ComplianceSummary(CamelModel):
    compliance_doc_id: UUID
    doc_type: str
    status: str
    expires_on: date | None = None


class AgencyCandidate(CamelModel):
    agency_id: UUID
    name: str
    yard_lat: Decimal
    yard_lng: Decimal
    vehicles: list[VehicleSummary] = []
    compliance_docs: list[ComplianceSummary] = []
