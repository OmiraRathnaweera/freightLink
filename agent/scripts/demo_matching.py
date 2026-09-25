"""FreightLink — Agent 3 (Matching & Pricing) Standalone Demo Runner.

Owner: Ratnaweera O.V. (Component C — Matching & Trip Execution)

Interactive demonstration of Agent 3:
1. Simulates realistic Sri Lankan logistics scenarios (Colombo, Kandy, Galle, Kurunegala).
2. Runs OpenRouteService tool (get_route_and_eta) for candidate positioning ETA.
3. Deterministically selects the closest agency and resolves vehicle class.
4. Routes cargo leg and calls pricing estimation (POST /internal/pricing/estimate).
5. Prompts Gemini LLM for shipper-facing recommendation justification.
6. Persists ToolCall audits and reports AgentStep 3.
"""

import asyncio
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
from unittest.mock import AsyncMock, patch
import uuid

# Ensure freightlink_agent can be imported
AGENT_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(AGENT_ROOT / "src"))

if sys.platform == "win32":
    try:
        reconfigure = getattr(sys.stdout, "reconfigure", None)
        if callable(reconfigure):
            reconfigure(encoding="utf-8")
    except Exception:
        pass

import httpx

from freightlink_agent.agents import matching_pricing
from freightlink_agent.core.config import get_settings
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    EstimatePricingResponse,
)


# ANSI Colors for terminal styling
CYAN = "\033[96m"
GREEN = "\033[92m"
YELLOW = "\033[93m"
BLUE = "\033[94m"
MAGENTA = "\033[95m"
BOLD = "\033[1m"
RESET = "\033[0m"


def print_banner():
    print(f"\n{CYAN}{BOLD}" + "=" * 70)
    print("   FREIGHTLINK AGENTIC AI — AGENT 3 (MATCHING & PRICING)")
    print("   Component C: Matching & Trip Execution (Rathnaweera O.V.)")
    print("=" * 70 + f"{RESET}\n")


async def is_backend_running(backend_url: str) -> bool:
    try:
        async with httpx.AsyncClient(timeout=1.5) as client:
            res = await client.get(f"{backend_url}/api/v1/health")
            return res.status_code == 200
    except Exception:
        return False


def get_sample_scenarios():
    return [
        {
            "title": "Scenario 1: Industrial Machinery — Colombo Port to Kandy",
            "load_context": {
                "pickupAddress": "Colombo Port Container Terminal",
                "pickupLat": 6.9416,
                "pickupLng": 79.8512,
                "dropoffAddress": "Kandy Industrial Zone, Pallekele",
                "dropoffLat": 7.2831,
                "dropoffLng": 80.7056,
                "weightKg": 3200.0,
                "volumeM3": 11.5,
                "cargoDescription": "Industrial generator and spare parts",
            },
            "candidates": [
                CandidateAgency(
                    agency_id=uuid.uuid4(),
                    name="Peliyagoda Logistics Express",
                    yard_address="Peliyagoda Highway Interchange, Colombo",
                    yard_lat=6.9667,
                    yard_lng=79.8917,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.uuid4(),
                    name="Negombo Haulers Ltd",
                    yard_address="Negombo Colombo Main Road",
                    yard_lat=7.2083,
                    yard_lng=79.8358,
                    available_vehicle_classes=["MiniTruck", "MediumLorry"],
                ),
                CandidateAgency(
                    agency_id=uuid.uuid4(),
                    name="Southern Freight Depot",
                    yard_address="Galle Port Access Rd, Galle",
                    yard_lat=6.0535,
                    yard_lng=80.2210,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
            ],
        },
        {
            "title": "Scenario 2: Perishable Agricultural Produce — Kurunegala to Hambantota",
            "load_context": {
                "pickupAddress": "Kurunegala Central Market",
                "pickupLat": 7.4863,
                "pickupLng": 80.3623,
                "dropoffAddress": "Hambantota International Port",
                "dropoffLat": 6.1245,
                "dropoffLng": 81.1185,
                "weightKg": 8500.0,
                "volumeM3": 28.0,
                "cargoDescription": "Refrigerated export fruit containers",
            },
            "candidates": [
                CandidateAgency(
                    agency_id=uuid.uuid4(),
                    name="Wayamba Heavy Transport",
                    yard_address="Dambulla Road, Kurunegala",
                    yard_lat=7.4950,
                    yard_lng=80.3700,
                    available_vehicle_classes=["ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.uuid4(),
                    name="Colombo Inland Cargo Depot",
                    yard_address="Orugodawatta, Colombo",
                    yard_lat=6.9450,
                    yard_lng=79.8800,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
            ],
        },
    ]


async def run_scenario(scenario: dict):
    print(f"{YELLOW}{BOLD}> Running: {scenario['title']}{RESET}\n")

    ctx = scenario["load_context"]
    candidates = scenario["candidates"]

    load_id = uuid.uuid4()
    run_id = uuid.uuid4()
    shipper_id = uuid.uuid4()

    state = WorkflowState(
        load_id=load_id,
        triggered_by_user_id=shipper_id,
        attempt_no=1,
        workflow_run_id=run_id,
        load_context=ctx,
        candidate_shortlist=candidates,
    )

    settings = get_settings()
    backend_up = await is_backend_running(settings.backend_base_url)

    print(f"{BLUE}[Configuration]{RESET}")
    print(f"  * Load ID: {load_id}")
    print(f"  * Workflow Run ID: {run_id}")
    print(f"  * Cargo: {ctx['cargoDescription']} ({ctx['weightKg']:,.0f} kg, {ctx['volumeM3']} m3)")
    print(f"  * Pickup:  {ctx['pickupAddress']} ({ctx['pickupLat']:.4f}, {ctx['pickupLng']:.4f})")
    print(f"  * Dropoff: {ctx['dropoffAddress']} ({ctx['dropoffLat']:.4f}, {ctx['dropoffLng']:.4f})")
    print(f"  * Shortlisted Candidates from Agent 2: {len(candidates)}")
    for i, c in enumerate(candidates, 1):
        print(f"     {i}. {c.name} -- Yard at ({c.yard_lat:.4f}, {c.yard_lng:.4f}) [{', '.join(c.available_vehicle_classes)}]")
    print(f"  * Backend Service: {'ONLINE (' + settings.backend_base_url + ')' if backend_up else 'STANDALONE MODE (simulated)'}")
    print(f"  * LLM Provider: {settings.llm_provider.upper()} ({settings.gemini_model if settings.llm_provider == 'gemini' else settings.ollama_model})")
    print()

    # If backend is offline, patch the network calls so the demo runs standalone seamlessly
    if not backend_up:
        # Mock pricing estimate following ADR-015 formula: baseFare + dist*ratePerKm + weight*ratePerKg
        async def mock_pricing(req):
            weight = float(ctx["weightKg"])
            dist = float(req.distance_km)
            base_fare = 2500.0
            rate_km = 135.0 if req.suggested_vehicle_class == "ContainerTruck" else 110.0
            rate_kg = 1.75
            price = round(base_fare + (dist * rate_km) + (weight * rate_kg), 2)
            resp = EstimatePricingResponse(
                load_id=req.load_id,
                estimated_price=price,
                distance_km=dist,
                vehicle_class=req.suggested_vehicle_class,
                rate_per_km=rate_km,
                rate_per_kg=rate_kg,
                base_fare=base_fare,
            )
            return resp, {"httpStatusCode": 200, "durationMs": 42}

        with (
            patch("freightlink_agent.agents.matching_pricing.get_price_estimate", side_effect=mock_pricing),
            patch("freightlink_agent.agents.matching_pricing.record_tool_call", new=AsyncMock()),
            patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock(return_value=uuid.uuid4())),
        ):
            print(f"{CYAN}Executing Agent 3 (Matching & Pricing)...{RESET}")
            result = await matching_pricing.run(state)
    else:
        print(f"{CYAN}Executing Agent 3 against live ASP.NET Core backend...{RESET}")
        result = await matching_pricing.run(state)

    print()
    if result.get("failed"):
        print(f"\033[91m[FAIL] Agent 3 Execution Failed: {result.get('failure_reason')}{RESET}\n")
        return

    # Formatted Results Dashboard
    print(f"{GREEN}{BOLD}" + "=" * 70)
    print("                     AGENT 3 MATCH RESULT")
    print("=" * 70 + f"{RESET}")
    print(f"{BOLD}Selected Carrier:{RESET}         {result['selected_agency_name']} ({result['selected_agency_id']})")
    print(f"{BOLD}Recommended Vehicle Class:{RESET} {result['suggested_vehicle_class']}")
    print(f"{BOLD}Positioning ETA to Pickup:{RESET} {result['eta_minutes']} minutes ({result['positioning_distance_km']} km road distance)")
    print(f"{BOLD}Cargo Transit Distance:{RESET}    {result['cargo_distance_km']} km (Pickup -> Dropoff)")
    print(f"{BOLD}Estimated Total Price:{RESET}     LKR {result['proposed_price']:,.2f}")

    if result.get("pricing_breakdown"):
        bk = result["pricing_breakdown"]
        print(f"\n{BLUE}[ADR-015 Pricing Breakdown]{RESET}")
        print(f"  * Base Fare:       LKR {bk.get('baseFare', 0):,.2f}")
        print(f"  * Distance Rate:   LKR {bk.get('ratePerKm', 0):,.2f}/km x {bk.get('distanceKm', 0):.1f} km")
        print(f"  * Weight Rate:     LKR {bk.get('ratePerKg', 0):,.2f}/kg x {ctx['weightKg']:,.0f} kg")

    print(f"\n{MAGENTA}[Shipper Recommendation Justification (LLM Generated)]{RESET}")
    print("-" * 70)
    print(result.get("selection_justification", ""))
    print("-" * 70)
    print(f"{GREEN}[OK] Audit: ToolCall logs and AgentStep #3 generated successfully.{RESET}\n")



async def main():
    print_banner()
    scenarios = get_sample_scenarios()

    # Check CLI arguments first
    if "--all" in sys.argv or "-a" in sys.argv:
        for s in scenarios:
            await run_scenario(s)
            print("-" * 70 + "\n")
        return

    if "--scenario" in sys.argv:
        try:
            sc_idx = int(sys.argv[sys.argv.index("--scenario") + 1]) - 1
            if 0 <= sc_idx < len(scenarios):
                await run_scenario(scenarios[sc_idx])
                return
        except (ValueError, IndexError):
            pass

    # If stdin is not a tty (automated/batch run), run scenario 1 by default
    if not sys.stdin.isatty():
        await run_scenario(scenarios[0])
        return

    print("Choose scenario to run:")
    for idx, s in enumerate(scenarios, 1):
        print(f"  [{idx}] {s['title']}")
    print("  [3] Run all scenarios sequentially")
    print("  [q] Quit")

    try:
        choice = input(f"\nEnter choice (1-3) [default: 1]: ").strip()
    except EOFError:
        choice = "1"

    if choice.lower() == "q":
        return

    print("\n" + "=" * 70)
    if choice == "2":
        await run_scenario(scenarios[1])
    elif choice == "3":
        for s in scenarios:
            await run_scenario(s)
            print("-" * 70 + "\n")
    else:
        await run_scenario(scenarios[0])



if __name__ == "__main__":
    asyncio.run(main())
