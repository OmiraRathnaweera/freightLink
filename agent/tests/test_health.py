import pytest
from httpx import ASGITransport, AsyncClient

from freightlink_agent.main import app
from freightlink_agent.schemas.matching import CandidateAgency, EstimatePricingRequest


@pytest.mark.anyio
async def test_health_endpoint():
    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://test"
    ) as client:
        response = await client.get("/health")
        assert response.status_code == 200
        assert response.json() == {"status": "ok"}


def test_schema_serialization():
    req = EstimatePricingRequest(
        load_id="11111111-1111-1111-1111-111111111111",
        suggested_vehicle_class="MediumLorry",
        distance_km=120.5,
    )
    dumped = req.model_dump(mode="json", by_alias=True)
    assert dumped["loadId"] == "11111111-1111-1111-1111-111111111111"
    assert dumped["suggestedVehicleClass"] == "MediumLorry"
    assert dumped["distanceKm"] == 120.5
