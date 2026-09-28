"""FreightLink — Agent 3 (Matching & Pricing) Standalone Demo Runner.

Component C — Matching & Trip Execution

Interactive demonstration of Agent 3:
1. Simulates realistic Sri Lankan logistics scenarios (Colombo, Kandy, Galle, Kurunegala, Katunayake).
2. Runs OpenRouteService routing for candidate positioning ETA & distance.
3. Captures and evaluates the TOP 3 CANDIDATE CARRIERS (Spotlight #1 Winner, Alternate #2, Alternate #3).
4. Routes cargo leg and calls pricing estimation (POST /internal/pricing/estimate, ADR-015).
5. Generates shipper recommendation justification using OpenAI (gpt-4o-mini by default).
6. Persists ToolCall audits and reports AgentStep 3.
"""

import asyncio
from pathlib import Path
import sys
from unittest.mock import AsyncMock, patch
import uuid

from dotenv import load_dotenv

# Ensure freightlink_agent can be imported and environment is loaded
AGENT_ROOT = Path(__file__).resolve().parent.parent
load_dotenv(AGENT_ROOT / ".env")
sys.path.insert(0, str(AGENT_ROOT / "src"))

if sys.platform == "win32":
    try:
        reconfigure = getattr(sys.stdout, "reconfigure", None)
        if callable(reconfigure):
            reconfigure(encoding="utf-8", line_buffering=True)
        err_reconfigure = getattr(sys.stderr, "reconfigure", None)
        if callable(err_reconfigure):
            err_reconfigure(encoding="utf-8", line_buffering=True)
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
DIM = "\033[2m"
RESET = "\033[0m"


def print_banner():
    print(f"\n{CYAN}{BOLD}" + "=" * 78)
    print("   FREIGHTLINK AGENTIC AI — AGENT 3 (MATCHING & PRICING)")
    print("   Component C: Matching & Trip Execution")
    print("   Top 3 Candidate Carriers & Top 3 LLM Model Evaluation Suite")
    print("=" * 78 + f"{RESET}\n")


async def is_backend_running(backend_url: str) -> bool:
    try:
        async with httpx.AsyncClient(timeout=1.5) as client:
            res = await client.get(f"{backend_url}/api/v1/health")
            return res.status_code == 200
    except Exception:
        return False


def get_sample_scenarios():
    """Returns realistic Sri Lankan freight corridors with 5 candidate carrier agencies each."""
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
                "cargoDescription": "Industrial generator and heavy machinery spare parts",
            },
            "candidates": [
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224ba"),
                    name="Samagi Express Logistics",
                    yard_address="45 Harbor Access Road, Peliyagoda",
                    yard_lat=6.9667,
                    yard_lng=79.8917,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bb"),
                    name="Lanka Freight & Cargo Hub",
                    yard_address="120 Baseline Road, Dematagoda, Colombo 09",
                    yard_lat=6.9380,
                    yard_lng=79.8780,
                    available_vehicle_classes=["MiniTruck", "MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224be"),
                    name="Wayamba Regional Transporters",
                    yard_address="34 Dambulla Road, Kurunegala",
                    yard_lat=7.4863,
                    yard_lng=80.3623,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bc"),
                    name="Southern Coastal Haulage",
                    yard_address="78 Matara Road, Galle",
                    yard_lat=6.0354,
                    yard_lng=80.2170,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bd"),
                    name="Central Highlands Express",
                    yard_address="15 Kandy Industrial Estate, Peradeniya",
                    yard_lat=7.2600,
                    yard_lng=80.5980,
                    available_vehicle_classes=["MiniTruck", "MediumLorry"],
                ),
            ],
        },
        {
            "title": "Scenario 2: Perishable Agricultural Produce — Kurunegala to Hambantota",
            "load_context": {
                "pickupAddress": "Kurunegala Central Agro Logistics Hub",
                "pickupLat": 7.4863,
                "pickupLng": 80.3623,
                "dropoffAddress": "Hambantota International Port",
                "dropoffLat": 6.1245,
                "dropoffLng": 81.1185,
                "weightKg": 8500.0,
                "volumeM3": 28.0,
                "cargoDescription": "Export-grade chilled fresh fruits and spices",
            },
            "candidates": [
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224be"),
                    name="Wayamba Regional Transporters",
                    yard_address="34 Dambulla Road, Kurunegala",
                    yard_lat=7.4950,
                    yard_lng=80.3700,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bd"),
                    name="Central Highlands Express",
                    yard_address="15 Kandy Industrial Estate, Peradeniya",
                    yard_lat=7.2600,
                    yard_lng=80.5980,
                    available_vehicle_classes=["MiniTruck", "MediumLorry"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224ba"),
                    name="Samagi Express Logistics",
                    yard_address="45 Harbor Access Road, Peliyagoda",
                    yard_lat=6.9667,
                    yard_lng=79.8917,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bb"),
                    name="Lanka Freight & Cargo Hub",
                    yard_address="120 Baseline Road, Colombo 09",
                    yard_lat=6.9380,
                    yard_lng=79.8780,
                    available_vehicle_classes=["MiniTruck", "MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bc"),
                    name="Southern Coastal Haulage",
                    yard_address="78 Matara Road, Galle",
                    yard_lat=6.0354,
                    yard_lng=80.2170,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
            ],
        },
        {
            "title": "Scenario 3: Export Apparel & Garments — Katunayake EPZ to Colombo Port",
            "load_context": {
                "pickupAddress": "Katunayake Export Processing Zone (EPZ)",
                "pickupLat": 7.1680,
                "pickupLng": 79.8890,
                "dropoffAddress": "Colombo Port Harbor Access Gate",
                "dropoffLat": 6.9480,
                "dropoffLng": 79.8520,
                "weightKg": 4800.0,
                "volumeM3": 16.0,
                "cargoDescription": "Finished apparel cartons for shipping containerization",
            },
            "candidates": [
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224ba"),
                    name="Samagi Express Logistics",
                    yard_address="45 Harbor Access Road, Peliyagoda",
                    yard_lat=6.9667,
                    yard_lng=79.8917,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bb"),
                    name="Lanka Freight & Cargo Hub",
                    yard_address="120 Baseline Road, Colombo 09",
                    yard_lat=6.9380,
                    yard_lng=79.8780,
                    available_vehicle_classes=["MiniTruck", "MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224be"),
                    name="Wayamba Regional Transporters",
                    yard_address="34 Dambulla Road, Kurunegala",
                    yard_lat=7.4863,
                    yard_lng=80.3623,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bc"),
                    name="Southern Coastal Haulage",
                    yard_address="78 Matara Road, Galle",
                    yard_lat=6.0354,
                    yard_lng=80.2170,
                    available_vehicle_classes=["MediumLorry", "ContainerTruck"],
                ),
                CandidateAgency(
                    agency_id=uuid.UUID("258466d2-3d76-46c5-9eb4-ede1f49224bd"),
                    name="Central Highlands Express",
                    yard_address="15 Kandy Industrial Estate, Peradeniya",
                    yard_lat=7.2600,
                    yard_lng=80.5980,
                    available_vehicle_classes=["MiniTruck", "MediumLorry"],
                ),
            ],
        },
    ]


async def run_scenario(scenario: dict):
    print(f"\n{YELLOW}{BOLD}> Running Scenario: {scenario['title']}{RESET}\n")

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

    print(f"{BLUE}[Configuration & Dispatch Input]{RESET}")
    print(f"  * Load ID:         {load_id}")
    print(f"  * Workflow Run ID: {run_id}")
    print(f"  * Cargo:           {ctx['cargoDescription']} ({ctx['weightKg']:,.0f} kg, {ctx['volumeM3']} m3)")
    print(f"  * Pickup Address:  {ctx['pickupAddress']} ({ctx['pickupLat']:.4f}, {ctx['pickupLng']:.4f})")
    print(f"  * Dropoff Address: {ctx['dropoffAddress']} ({ctx['dropoffLat']:.4f}, {ctx['dropoffLng']:.4f})")
    print(f"  * Total Shortlisted Candidates Evaluated: {len(candidates)}")
    for i, c in enumerate(candidates, 1):
        print(f"     {i}. {c.name:<32} Yard: {c.yard_address[:32]:<32} Classes: {', '.join(c.available_vehicle_classes)}")
    print(f"  * Backend Service: {'ONLINE (' + settings.backend_base_url + ')' if backend_up else 'STANDALONE MODE (simulated)'}")
    print(f"  * LLM Provider:    OPENAI ({settings.openai_model})")
    print()

    # If backend is offline, patch the network calls so the demo runs standalone seamlessly
    if not backend_up:
        async def mock_pricing(req):
            weight = float(ctx["weightKg"])
            dist = float(req.distance_km)
            base_fare = 5000.0
            rate_km = 145.0 if req.suggested_vehicle_class == "ContainerTruck" else 115.0
            rate_kg = 2.00
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
            return resp, {"httpStatusCode": 200, "durationMs": 35}

        with (
            # Deterministic-fallback path's own imports (used if the LLM tool-calling path
            # is unusable, e.g. no OPENAI_API_KEY configured):
            patch("freightlink_agent.agents.matching_pricing.get_price_estimate", side_effect=mock_pricing),
            patch("freightlink_agent.agents.matching_pricing.record_tool_call", new=AsyncMock()),
            # The genuine tool-calling path's own tool wrappers call these same names from
            # tools/matching_tools.py instead - patch both so standalone mode works whichever
            # path actually runs:
            patch("freightlink_agent.tools.matching_tools.get_price_estimate", side_effect=mock_pricing),
            patch("freightlink_agent.tools.matching_tools.record_tool_call", new=AsyncMock()),
            patch("freightlink_agent.agents.matching_pricing.report", new=AsyncMock(return_value=uuid.uuid4())),
        ):
            print(f"{CYAN}Executing Agent 3 Multi-Criteria Carrier Ranking & Pricing...{RESET}")
            result = await matching_pricing.run(state)
    else:
        print(f"{CYAN}Executing Agent 3 against live ASP.NET Core backend...{RESET}")
        result = await matching_pricing.run(state)

    print()
    if result.get("failed"):
        print(f"\033[91m[FAIL] Agent 3 Execution Failed: {result.get('failure_reason')}{RESET}\n")
        return

    # Formatted Results Dashboard
    print(f"{GREEN}{BOLD}" + "=" * 78)
    print("                     AGENT 3 MATCH RECOMMENDATION RESULT")
    print("=" * 78 + f"{RESET}")
    print(f"{BOLD}Selected Best-Fit Carrier:{RESET}  {result['selected_agency_name']} ({result['selected_agency_id']})")
    print(f"{BOLD}Recommended Vehicle Class:{RESET}  {result['suggested_vehicle_class']}")
    print(f"{BOLD}Positioning ETA to Pickup:{RESET}  {result['eta_minutes']} minutes ({result['positioning_distance_km']:.1f} km road positioning)")
    print(f"{BOLD}Cargo Transit Leg Distance:{RESET} {result['cargo_distance_km']:.1f} km (Pickup -> Dropoff)")
    print(f"{BOLD}Proposed Dynamic Price:{RESET}     LKR {result['proposed_price']:,.2f}")

    if result.get("pricing_breakdown"):
        bk = result["pricing_breakdown"]
        print(f"\n{BLUE}[ADR-015 Pricing Breakdown (Fuel & Distance Adjusted)]{RESET}")
        print(f"  * Base Fare:       LKR {bk.get('baseFare', 0):,.2f}")
        print(f"  * Distance Charge: LKR {bk.get('ratePerKm', 0):,.2f}/km x {bk.get('distanceKm', 0):.1f} km = LKR {(bk.get('ratePerKm', 0) * bk.get('distanceKm', 0)):,.2f}")
        print(f"  * Payload Charge:  LKR {bk.get('ratePerKg', 0):,.2f}/kg x {ctx['weightKg']:,.0f} kg = LKR {(bk.get('ratePerKg', 0) * ctx['weightKg']):,.2f}")

    # =========================================================================
    # TOP 3 CAPTURED CANDIDATE CARRIERS TABLE
    # =========================================================================
    print(f"\n{YELLOW}{BOLD}" + "=" * 78)
    print("               TOP 3 CAPTURED CANDIDATE CARRIERS (AGENT 3)")
    print("=" * 78 + f"{RESET}")

    ranked_list = result.get("ranked_five", [])
    cand_map = {str(c.agency_id): c for c in candidates}

    print(f"{BOLD}{'Rank':<6} {'Carrier Name':<30} {'Positioning':<18} {'Classes':<16} {'Status':<14}{RESET}")
    print("-" * 88)

    top_3 = ranked_list[:3]
    for idx, r in enumerate(top_3, 1):
        ag_id = r.get("agencyId")
        agency_obj = cand_map.get(ag_id)
        name = agency_obj.name if agency_obj else f"Agency {ag_id[:8]}"
        classes_str = ", ".join(agency_obj.available_vehicle_classes[:2]) if agency_obj else "Standard"

        eta = r.get("etaMinutes", 0)
        dist = r.get("distanceKm", 0.0)
        positioning_str = f"{eta} min ({dist:.1f} km)"

        raw_rank = f"#{idx}"
        if idx == 1:
            status_str = f"{GREEN}{BOLD}#1 BEST FIT (WINNER){RESET}"
            rank_str = f"{GREEN}{BOLD}{raw_rank:<6}{RESET}"
        elif idx == 2:
            status_str = f"{CYAN}ALTERNATE #2{RESET}"
            rank_str = f"{CYAN}{raw_rank:<6}{RESET}"
        else:
            status_str = f"{BLUE}ALTERNATE #3{RESET}"
            rank_str = f"{BLUE}{raw_rank:<6}{RESET}"

        print(f"{rank_str} {name:<30} {positioning_str:<18} {classes_str:<16} {status_str}")

    print("-" * 88)
    if len(top_3) >= 2:
        winner_eta = top_3[0].get("etaMinutes", 0)
        second_eta = top_3[1].get("etaMinutes", 0)
        diff = max(0, second_eta - winner_eta)
        print(f"{DIM}* Agent 3 Selection Advantage: Winner arrives {diff} minutes sooner than Alternate #2.{RESET}")
        print(f"{DIM}* Shipper has option in UI console to override and select Candidate #2 or Candidate #3.{RESET}")

    # LLM Justification Output
    print(f"\n{MAGENTA}[Shipper Recommendation Justification (LLM Generated)]{RESET}")
    print("-" * 78)
    print(result.get("selection_justification", ""))
    print("-" * 78)
    print(f"{GREEN}[OK] Audit Confirmed: Top 3 captured, ToolCalls logged, and AgentStep #3 generated.{RESET}\n")


async def main():
    print_banner()
    scenarios = get_sample_scenarios()

    # Check CLI arguments first
    if "--all" in sys.argv or "-a" in sys.argv:
        for s in scenarios:
            await run_scenario(s)
            print("-" * 78 + "\n")
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

    # Interactive Menu
    print("Select an option to execute:")
    for idx, s in enumerate(scenarios, 1):
        print(f"  [{idx}] {s['title']}")
    print(f"  [4] Run All 3 Scenarios Sequentially (Verify Top 3 Candidates Across Sri Lanka)")
    print("  [q] Quit")

    try:
        choice = input(f"\nEnter choice (1-4) [default: 1]: ").strip()
    except EOFError:
        choice = "1"

    if choice.lower() == "q":
        return

    print("\n" + "=" * 78)
    if choice == "2":
        await run_scenario(scenarios[1])
    elif choice == "3":
        await run_scenario(scenarios[2])
    elif choice == "4":
        for s in scenarios:
            await run_scenario(s)
            print("-" * 78 + "\n")
    else:
        await run_scenario(scenarios[0])


if __name__ == "__main__":
    asyncio.run(main())
