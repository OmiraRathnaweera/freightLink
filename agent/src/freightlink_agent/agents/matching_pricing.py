"""Agent 3 — Matching & Pricing.

Owner: Ratnaweera (Component C).
Tools: get_route_and_eta, get_price_estimate.

The LLM itself decides which candidate agency to route, when to price it, and which one
to select - it is genuinely the tool-usage agent, not a narrator called after a Python
loop already picked the winner (see tools/matching_tools.py and
AgentLLM.run_tool_calling_selection). The actual distance/ETA/price numbers that get
persisted always come from the tool results themselves (looked up from a ledger by the
tool_call_id the model cites), never from the model restating a number in its own words.

If OpenAI is unreachable, the per-process call cap is hit, or the model's final decision
cites an id that isn't in the ledger (or points at a failed result), this falls back to
_deterministic_fallback_selection - today's original procedural algorithm (route every
candidate, pick the fastest ETA, price the cargo leg) - so a total LLM outage degrades to
already-tested behavior rather than crashing or fabricating a result.
"""

import json
import logging
from typing import Any
from uuid import UUID, uuid4

from freightlink_agent.core.backend_client import BackendClientError, record_tool_call
from freightlink_agent.core.llm import ToolCallingSelectionOutput, get_llm
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report
from freightlink_agent.schemas.enums import VehicleClass
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    CreateToolCallRequest,
    EstimatePricingRequest,
    EstimatePricingResponse,
    RouteAndEtaRequest,
    RouteAndEtaResponse,
)
from freightlink_agent.tools.matching_tools import build_matching_tools
from freightlink_agent.tools.pricing import get_price_estimate
from freightlink_agent.tools.routing import get_route_and_eta

logger = logging.getLogger(__name__)

_STEP_NO = 3
_AGENT_ROLE = "MatchingPricing"

# Bounds on Agent 3's tool-calling loop (AgentLLM.run_tool_calling_selection): enough turns
# to check several candidates' positioning ETA, route one cargo leg, and price a winner,
# without letting a confused model loop unboundedly or blow the OpenAI cost cap.
MAX_TOOL_ITERATIONS = 6
MAX_TOOL_CALLS = 8

_TOOL_SELECTION_SYSTEM_PROMPT = (
    "You are the Matching & Pricing AI Agent in the FreightLink logistics platform. You "
    "have two tools: get_route_and_eta_for_candidate and estimate_price_for_load. Use them "
    "yourself to decide which candidate agency is the best fit for this load - do not "
    "guess or skip straight to a decision without calling tools."
    "\n\n"
    "Suggested process: for each candidate agency given to you, call "
    "get_route_and_eta_for_candidate(leg='positioning') to see how quickly it can reach "
    "pickup. For the candidate(s) you are seriously considering, call "
    "get_route_and_eta_for_candidate(leg='cargo') once (the cargo leg is the same distance "
    "for every candidate, but you must still name the candidate you are evaluating), then "
    "call estimate_price_for_load citing that cargo tool_call_id to get a real price. "
    "Prefer agencies with a fast positioning ETA, a suitable vehicle class, and fleet/"
    "driver readiness."
    "\n\n"
    "CRITICAL GROUNDING RULES: "
    "1. Never invent a distance, ETA, or price - every number must come from a tool "
    "result, and you must cite its exact tool_call_id in your final decision. "
    "2. Never invent mock carriers, vehicles, or drivers - reference only the real "
    "candidates, fleet vehicles, and drivers given to you in the candidates list. "
    "3. In headline/detailed_reasoning, explain why you picked this agency: positioning "
    "ETA, distance, price, and fleet/driver readiness. "
    "4. Keep the tone concise, authoritative, and professional for enterprise freight "
    "logistics."
)


class _SelectionFailed(Exception):
    """Raised by _deterministic_fallback_selection when the underlying routing/pricing
    tools themselves are broken - a real pipeline failure, not something to fall back
    further from. ranked_five carries whatever partial comparison data exists so far, for
    the same visibility the original inline failure paths used to return."""

    def __init__(self, message: str, ranked_five: list[dict[str, Any]] | None = None) -> None:
        super().__init__(message)
        self.message = message
        self.ranked_five = ranked_five


def _determine_vehicle_class(
    weight_kg: float,
    volume_m3: float,
    available_classes: list[VehicleClass] | None,
) -> VehicleClass:
    """Deterministically resolves the appropriate vehicle class tier based on weight and volume (ADR-019)."""
    if weight_kg <= 1000.0 and volume_m3 <= 5.0:
        preferred: VehicleClass = "MiniTruck"
    elif weight_kg <= 5000.0 and volume_m3 <= 20.0:
        preferred = "MediumLorry"
    else:
        preferred = "ContainerTruck"

    if not available_classes:
        return preferred

    # If the preferred class is directly available, use it
    if preferred in available_classes:
        return preferred

    # Hierarchy: MiniTruck < MediumLorry < ContainerTruck
    hierarchy: list[VehicleClass] = ["MiniTruck", "MediumLorry", "ContainerTruck"]
    preferred_idx = hierarchy.index(preferred)
    # Find the smallest available class that can accommodate the load
    for cls in hierarchy[preferred_idx:]:
        if cls in available_classes:
            return cls

    # Fallback to the first available class
    return available_classes[0]


def _build_ranked_five(
    candidates_by_id: dict[str, CandidateAgency],
    ledger: dict[str, dict[str, Any]],
) -> list[dict[str, Any]]:
    """Builds the candidate comparison list from whichever positioning-leg tool results
    actually landed in the ledger - only candidates the LLM (or the deterministic fallback)
    actually routed appear here."""
    best_by_candidate: dict[str, RouteAndEtaResponse] = {}
    for entry in ledger.values():
        if entry.get("type") == "route" and entry.get("leg") == "positioning" and entry["result"].success:
            best_by_candidate[entry["candidateAgencyId"]] = entry["result"]

    pairs = [(candidates_by_id[cid], route) for cid, route in best_by_candidate.items() if cid in candidates_by_id]
    pairs.sort(key=lambda pair: (pair[1].eta_minutes or 999999, pair[1].distance_km or 999999))
    return [
        {
            "agencyId": str(cand.agency_id),
            "etaMinutes": route.eta_minutes or 0,
            "distanceKm": route.distance_km or 0.0,
            "isSimulatedRoute": route.is_simulated,
        }
        for cand, route in pairs[:5]
    ]


def _extract_selection_from_ledger(
    decision: ToolCallingSelectionOutput,
    candidates_by_id: dict[str, CandidateAgency],
    ledger: dict[str, dict[str, Any]],
    vehicle_class_by_candidate: dict[str, VehicleClass],
) -> dict[str, Any]:
    """Validates the LLM's cited tool_call_ids against the real ledger and pulls every
    numeric field from there - never from the decision's own text. Raises ValueError if any
    citation is missing, malformed, or points at a failed result; the caller treats that
    exactly like a total LLM failure and falls back to the deterministic algorithm."""
    winner = candidates_by_id.get(decision.selected_candidate_agency_id)
    if winner is None:
        raise ValueError(f"decision cites unknown candidate_agency_id '{decision.selected_candidate_agency_id}'")

    positioning_entry = ledger.get(decision.selected_positioning_tool_call_id)
    if (
        positioning_entry is None
        or positioning_entry.get("type") != "route"
        or positioning_entry.get("leg") != "positioning"
        or positioning_entry.get("candidateAgencyId") != decision.selected_candidate_agency_id
        or not positioning_entry["result"].success
    ):
        raise ValueError("decision cites an invalid or failed positioning tool_call_id")

    cargo_entry = ledger.get(decision.selected_cargo_tool_call_id)
    if (
        cargo_entry is None
        or cargo_entry.get("type") != "route"
        or cargo_entry.get("leg") != "cargo"
        or not cargo_entry["result"].success
    ):
        raise ValueError("decision cites an invalid or failed cargo tool_call_id")

    pricing_entry = ledger.get(decision.selected_pricing_tool_call_id)
    if pricing_entry is None or pricing_entry.get("type") != "pricing" or pricing_entry["result"] is None:
        raise ValueError("decision cites an invalid or failed pricing tool_call_id")

    justification = f"{decision.headline}\n\n{decision.detailed_reasoning}".strip()

    return {
        "winner": winner,
        "winner_route": positioning_entry["result"],
        "cargo_route": cargo_entry["result"],
        "pricing_res": pricing_entry["result"],
        "suggested_vehicle_class": vehicle_class_by_candidate.get(decision.selected_candidate_agency_id, "MediumLorry"),
        "justification": justification,
        "ranked_five": _build_ranked_five(candidates_by_id, ledger),
    }


def _build_deterministic_justification(
    *,
    winner: CandidateAgency,
    winner_route: RouteAndEtaResponse,
    cargo_distance_km: float,
    pricing_res: EstimatePricingResponse,
    suggested_vehicle_class: str,
    assigned_vehicle: dict[str, Any] | None,
    assigned_driver: dict[str, Any] | None,
) -> str:
    veh_reg = (
        (assigned_vehicle.get("registrationNo") or assigned_vehicle.get("registration_no"))
        if assigned_vehicle
        else "Verified Fleet Vehicle"
    )
    dr_name = assigned_driver.get("name") if assigned_driver else "Licensed Carrier Driver"
    return (
        f"Recommended Carrier: {winner.name} (Yard: {winner.yard_address or 'Hub'}). "
        f"Assigned Vehicle: {veh_reg} ({suggested_vehicle_class}) with Driver: {dr_name}. "
        f"Nearest available carrier with ETA {winner_route.eta_minutes} min to pickup ({winner_route.distance_km} km positioning). "
        f"Cargo transit distance: {cargo_distance_km} km. Estimated price: LKR {pricing_res.estimated_price:,.2f}."
    )


async def _deterministic_fallback_selection(
    *,
    shortlist_to_eval: list[CandidateAgency],
    load_id: UUID,
    pickup_lat: float,
    pickup_lng: float,
    dropoff_lat: float,
    dropoff_lng: float,
    workflow_run_id: UUID,
    weight_kg: float,
    volume_m3: float,
    tool_calls: list[dict[str, Any]],
    attempt_counters: dict[str, int],
) -> dict[str, Any]:
    """Today's original, fully procedural selection algorithm (no LLM): routes every
    shortlisted candidate, picks the fastest positioning ETA, routes the cargo leg, and
    prices it. Used whenever the genuine LLM tool-calling path is unusable, so a total LLM
    outage degrades to this already-tested behavior instead of failing the run outright.
    Raises _SelectionFailed (a real "hold for review" failure) if routing/pricing
    themselves are broken - that must stop the pipeline either way.

    attempt_counters is the SAME per-tool-name counter dict the (possibly already-run)
    LLM tool-calling path used via build_matching_tools - continuing it here, rather than
    restarting each tool's attempt numbering at 1, is required: both paths record ToolCall
    rows against the very same AgentStep, and the backend's uq_toolcall_attempt constraint
    is unique on (AgentStepId, ToolName, AttemptNo). Restarting at 1 collides with attempt
    numbers the LLM path already persisted whenever it made real tool calls before its
    final decision turned out to be unusable - every single one of this fallback's own
    tool calls would then fail to persist (silently, since record_tool_call failures are
    non-fatal), losing the audit trail for the run that actually mattered.
    """
    routed_candidates: list[tuple[CandidateAgency, RouteAndEtaResponse]] = []

    for candidate in shortlist_to_eval:
        attempt_counters["get_route_and_eta"] = attempt_counters.get("get_route_and_eta", 0) + 1
        attempt_no = attempt_counters["get_route_and_eta"]
        req = RouteAndEtaRequest(
            origin_lat=candidate.yard_lat,
            origin_lng=candidate.yard_lng,
            destination_lat=pickup_lat,
            destination_lng=pickup_lng,
        )
        route_res, telemetry = await get_route_and_eta(req)

        req_json = json.dumps(telemetry.get("request"), default=str)
        err_msg = route_res.error_message
        if not route_res.success:
            if not err_msg:
                err_msg = "hold for review: routing lookup failed after retry"
            elif "hold for review" not in err_msg.lower():
                err_msg = f"hold for review: {err_msg}"
            res_json = json.dumps({"status": "hold for review", "error": err_msg}, default=str)
        else:
            res_json = json.dumps(route_res.model_dump(by_alias=True), default=str)

        tool_calls.append({
            "toolCallId": str(uuid4()),
            "toolName": "get_route_and_eta",
            "attemptNo": attempt_no,
            "requestJson": req_json,
            "responseJson": res_json,
            "success": route_res.success,
            "httpStatusCode": telemetry.get("httpStatusCode"),
            "durationMs": telemetry.get("durationMs"),
            "errorMessage": err_msg if not route_res.success else None,
            "calledAt": now().isoformat(),
        })
        try:
            await record_tool_call(
                workflow_run_id,
                CreateToolCallRequest(
                    tool_name="get_route_and_eta",
                    attempt_no=attempt_no,
                    request_json=req_json,
                    response_json=res_json,
                    success=route_res.success,
                    http_status_code=telemetry.get("httpStatusCode"),
                    duration_ms=telemetry.get("durationMs"),
                    error_message=err_msg if not route_res.success else None,
                    called_at=now(),
                ),
            )
        except BackendClientError:
            pass

        if route_res.success and route_res.eta_minutes is not None:
            routed_candidates.append((candidate, route_res))
        else:
            logger.warning("Routing failed for candidate %s: %s", candidate.name, route_res.error_message)

    if not routed_candidates:
        raise _SelectionFailed("hold for review: all candidate agency routing lookups failed after retry")

    sorted_routed = sorted(
        routed_candidates,
        key=lambda pair: (pair[1].eta_minutes or 999999, pair[1].distance_km or 999999),
    )
    ranked_five = [
        {
            "agencyId": str(cand.agency_id),
            "etaMinutes": r.eta_minutes or 0,
            "distanceKm": r.distance_km or 0.0,
            "isSimulatedRoute": r.is_simulated,
        }
        for cand, r in sorted_routed[:5]
    ]

    winner, winner_route = sorted_routed[0]
    suggested_vehicle_class = _determine_vehicle_class(
        weight_kg=weight_kg,
        volume_m3=volume_m3,
        available_classes=winner.available_vehicle_classes,
    )

    cargo_req = RouteAndEtaRequest(
        origin_lat=pickup_lat,
        origin_lng=pickup_lng,
        destination_lat=dropoff_lat,
        destination_lng=dropoff_lng,
    )
    cargo_route, cargo_telemetry = await get_route_and_eta(cargo_req)

    attempt_counters["get_route_and_eta"] = attempt_counters.get("get_route_and_eta", 0) + 1
    cargo_attempt_no = attempt_counters["get_route_and_eta"]

    cargo_req_json = json.dumps(cargo_telemetry.get("request"), default=str)
    cargo_err_msg = cargo_route.error_message
    if not cargo_route.success:
        if not cargo_err_msg:
            cargo_err_msg = "hold for review: cargo routing lookup failed after retry"
        elif "hold for review" not in cargo_err_msg.lower():
            cargo_err_msg = f"hold for review: {cargo_err_msg}"
        cargo_res_json = json.dumps({"status": "hold for review", "error": cargo_err_msg}, default=str)
    else:
        cargo_res_json = json.dumps(cargo_route.model_dump(by_alias=True), default=str)

    tool_calls.append({
        "toolCallId": str(uuid4()),
        "toolName": "get_route_and_eta",
        "attemptNo": cargo_attempt_no,
        "requestJson": cargo_req_json,
        "responseJson": cargo_res_json,
        "success": cargo_route.success,
        "httpStatusCode": cargo_telemetry.get("httpStatusCode"),
        "durationMs": cargo_telemetry.get("durationMs"),
        "errorMessage": cargo_err_msg if not cargo_route.success else None,
        "calledAt": now().isoformat(),
    })
    try:
        await record_tool_call(
            workflow_run_id,
            CreateToolCallRequest(
                tool_name="get_route_and_eta",
                attempt_no=cargo_attempt_no,
                request_json=cargo_req_json,
                response_json=cargo_res_json,
                success=cargo_route.success,
                http_status_code=cargo_telemetry.get("httpStatusCode"),
                duration_ms=cargo_telemetry.get("durationMs"),
                error_message=cargo_err_msg if not cargo_route.success else None,
                called_at=now(),
            ),
        )
    except BackendClientError:
        pass

    if not cargo_route.success or cargo_route.distance_km is None or cargo_route.distance_km <= 0:
        raise _SelectionFailed(
            f"hold for review: cargo leg routing lookup failed: {cargo_err_msg}",
            ranked_five=ranked_five,
        )

    cargo_distance_km = cargo_route.distance_km

    pricing_req = EstimatePricingRequest(
        load_id=load_id,
        suggested_vehicle_class=suggested_vehicle_class,
        distance_km=cargo_distance_km,
    )
    pricing_res, pricing_telemetry = await get_price_estimate(pricing_req)

    attempt_counters["estimate_price"] = attempt_counters.get("estimate_price", 0) + 1
    pricing_attempt_no = attempt_counters["estimate_price"]

    pricing_req_json = json.dumps(pricing_req.model_dump(by_alias=True), default=str)
    pricing_err_msg = pricing_telemetry.get("error")
    if not pricing_res:
        if not pricing_err_msg:
            pricing_err_msg = "hold for review: pricing estimation failed after retry"
        elif "hold for review" not in pricing_err_msg.lower():
            pricing_err_msg = f"hold for review: {pricing_err_msg}"
        pricing_res_json = json.dumps({"status": "hold for review", "error": pricing_err_msg}, default=str)
    else:
        pricing_res_json = json.dumps(pricing_res.model_dump(by_alias=True), default=str)

    tool_calls.append({
        "toolCallId": str(uuid4()),
        "toolName": "estimate_price",
        "attemptNo": pricing_attempt_no,
        "requestJson": pricing_req_json,
        "responseJson": pricing_res_json,
        "success": pricing_res is not None,
        "httpStatusCode": pricing_telemetry.get("httpStatusCode"),
        "durationMs": pricing_telemetry.get("durationMs"),
        "errorMessage": pricing_err_msg if not pricing_res else None,
        "calledAt": now().isoformat(),
    })
    try:
        await record_tool_call(
            workflow_run_id,
            CreateToolCallRequest(
                tool_name="estimate_price",
                attempt_no=pricing_attempt_no,
                request_json=pricing_req_json,
                response_json=pricing_res_json,
                success=pricing_res is not None,
                http_status_code=pricing_telemetry.get("httpStatusCode"),
                duration_ms=pricing_telemetry.get("durationMs"),
                error_message=pricing_err_msg if not pricing_res else None,
                called_at=now(),
            ),
        )
    except BackendClientError:
        pass

    # Pricing is a hard business number, never an LLM/local guess (deterministic-vs-LLM
    # boundary, see plans/00-master-plan.md §6.5): if the backend estimator fails, fail
    # this run cleanly rather than fabricate a price.
    if not pricing_res:
        raise _SelectionFailed(
            f"hold for review: pricing estimation failed: {pricing_err_msg}",
            ranked_five=ranked_five,
        )

    return {
        "winner": winner,
        "winner_route": winner_route,
        "cargo_route": cargo_route,
        "pricing_res": pricing_res,
        "suggested_vehicle_class": suggested_vehicle_class,
        "justification": None,
        "ranked_five": ranked_five,
    }


async def run(state: WorkflowState) -> dict[str, Any]:
    started = now()
    steps = list(state.steps)
    tool_calls = list(state.tool_calls)
    workflow_run_id = state.workflow_run_id or uuid4()

    # Extract coordinates and load details
    ctx = state.load_context or {}
    pickup_lat = float(ctx.get("pickupLat") or ctx.get("pickup_lat") or 0.0)
    pickup_lng = float(ctx.get("pickupLng") or ctx.get("pickup_lng") or 0.0)
    dropoff_lat = float(ctx.get("dropoffLat") or ctx.get("dropoff_lat") or 0.0)
    dropoff_lng = float(ctx.get("dropoffLng") or ctx.get("dropoff_lng") or 0.0)
    weight_kg = float(ctx.get("weightKg") or ctx.get("weight_kg") or 1000.0)
    volume_m3 = float(ctx.get("volumeM3") or ctx.get("volume_m3") or 1.0)
    cargo_desc = str(ctx.get("cargoDescription") or ctx.get("cargo_description") or "")

    input_data = {
        "loadId": str(state.load_id),
        "workflowRunId": str(workflow_run_id),
        "candidatesCount": len(state.candidate_shortlist),
        "pickup": {"lat": pickup_lat, "lng": pickup_lng},
        "dropoff": {"lat": dropoff_lat, "lng": dropoff_lng},
        "weightKg": weight_kg,
        "volumeM3": volume_m3,
        "cargoDescription": cargo_desc,
        "candidateAgencies": [
            {
                "agencyId": str(c.agency_id),
                "name": c.name,
                "yardAddress": c.yard_address,
                "availableVehicleClasses": c.available_vehicle_classes,
            }
            for c in state.candidate_shortlist
        ],
    }

    candidates = state.candidate_shortlist
    if not candidates:
        msg = "No eligible candidate agencies available in shortlist"
        logger.warning(msg)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": input_data,
            "outputJson": None,
            "errorMessage": msg,
            "startedAt": started.isoformat(),
            "completedAt": now().isoformat(),
        })
        try:
            await report(
                workflow_run_id=workflow_run_id,
                step_no=_STEP_NO,
                agent_role=_AGENT_ROLE,
                status="Failed",
                started_at=started,
                input_data=input_data,
                error_message=msg,
            )
        except BackendClientError:
            logger.exception("Failed to report step failure")
        return {"failed": True, "failure_reason": msg, "tool_calls": tool_calls, "steps": steps}

    # Up to the top 5 candidates from Agent 2's shortlist are evaluated (capped to preserve
    # API quotas and the OpenAI tool-call budget).
    shortlist_to_eval = candidates[:5]
    candidates_by_id = {str(c.agency_id): c for c in shortlist_to_eval}
    # Vehicle class is a pure capacity lookup with no judgment call (ADR-019) - given to the
    # LLM as a fact per candidate, never left for it to "decide" or exposed as a tool.
    vehicle_class_by_candidate: dict[str, VehicleClass] = {
        cid: _determine_vehicle_class(weight_kg, volume_m3, c.available_vehicle_classes)
        for cid, c in candidates_by_id.items()
    }

    ledger: dict[str, dict[str, Any]] = {}
    # Shared across the LLM tool-calling path (below) AND _deterministic_fallback_selection
    # if it runs afterward - both record ToolCall rows against the same AgentStep, and the
    # backend's uq_toolcall_attempt constraint is unique on (AgentStepId, ToolName,
    # AttemptNo), so attempt numbering must continue rather than restart at 1.
    attempt_counters: dict[str, int] = {}
    tools = build_matching_tools(
        candidates_by_id=candidates_by_id,
        pickup=(pickup_lat, pickup_lng),
        dropoff=(dropoff_lat, dropoff_lng),
        load_id=state.load_id,
        workflow_run_id=workflow_run_id,
        vehicle_class_by_candidate=vehicle_class_by_candidate,
        ledger=ledger,
        tool_calls_audit=tool_calls,
        attempt_counters=attempt_counters,
    )

    llm_context = {
        "load": {
            "weightKg": weight_kg,
            "volumeM3": volume_m3,
            "cargoDescription": cargo_desc,
        },
        "candidates": [
            {
                "agencyId": cid,
                "name": c.name,
                "yardAddress": c.yard_address,
                "suggestedVehicleClass": vehicle_class_by_candidate[cid],
                "availableVehicles": c.available_vehicles,
                "activeDrivers": c.active_drivers,
            }
            for cid, c in candidates_by_id.items()
        ],
    }

    selection: dict[str, Any] | None = None
    llm_provenance: dict[str, Any] | None = None
    try:
        llm = get_llm()
        decision = await llm.run_tool_calling_selection(
            system_prompt=_TOOL_SELECTION_SYSTEM_PROMPT,
            context=llm_context,
            tools=tools,
            max_iterations=MAX_TOOL_ITERATIONS,
            max_tool_calls=MAX_TOOL_CALLS,
        )
        selection = _extract_selection_from_ledger(decision, candidates_by_id, ledger, vehicle_class_by_candidate)
        llm_provenance = getattr(llm, "last_call_meta", None)
    except Exception as exc:  # noqa: BLE001
        logger.warning(
            "LLM tool-calling selection unusable (%s); falling back to deterministic selection", exc
        )
        selection = None

    if selection is None:
        try:
            selection = await _deterministic_fallback_selection(
                shortlist_to_eval=shortlist_to_eval,
                load_id=state.load_id,
                pickup_lat=pickup_lat,
                pickup_lng=pickup_lng,
                dropoff_lat=dropoff_lat,
                dropoff_lng=dropoff_lng,
                workflow_run_id=workflow_run_id,
                weight_kg=weight_kg,
                volume_m3=volume_m3,
                tool_calls=tool_calls,
                attempt_counters=attempt_counters,
            )
        except _SelectionFailed as failure:
            logger.error(failure.message)
            steps.append({
                "stepNo": _STEP_NO,
                "agentRole": _AGENT_ROLE,
                "status": "Failed",
                "inputJson": input_data,
                "outputJson": {"status": "hold for review", "reason": failure.message},
                "errorMessage": failure.message,
                "startedAt": started.isoformat(),
                "completedAt": now().isoformat(),
            })
            try:
                await report(
                    workflow_run_id=workflow_run_id,
                    step_no=_STEP_NO,
                    agent_role=_AGENT_ROLE,
                    status="Failed",
                    started_at=started,
                    input_data=input_data,
                    error_message=failure.message,
                )
            except BackendClientError:
                pass
            failure_result: dict[str, Any] = {
                "failed": True,
                "failure_reason": failure.message,
                "tool_calls": tool_calls,
                "steps": steps,
            }
            if failure.ranked_five is not None:
                failure_result["ranked_five"] = failure.ranked_five
            return failure_result
        llm_provenance = {"provider": "deterministic_fallback", "model": None, "usedFallback": True}

    winner: CandidateAgency = selection["winner"]
    winner_route: RouteAndEtaResponse = selection["winner_route"]
    cargo_route: RouteAndEtaResponse = selection["cargo_route"]
    cargo_distance_km = cargo_route.distance_km
    pricing_res: EstimatePricingResponse = selection["pricing_res"]
    suggested_vehicle_class = selection["suggested_vehicle_class"]
    ranked_five = selection["ranked_five"]

    # Resolve real assigned fleet vehicle and licensed driver from database entities
    assigned_vehicle = None
    if winner.available_vehicles:
        for v in winner.available_vehicles:
            v_cap = float(v.get("capacityKg") or v.get("capacity_kg") or 0.0)
            v_cls = "ContainerTruck" if v_cap > 10000.0 else ("MediumLorry" if v_cap > 1500.0 else "MiniTruck")
            if v_cls == suggested_vehicle_class:
                assigned_vehicle = v
                break
        if not assigned_vehicle:
            assigned_vehicle = winner.available_vehicles[0]

    assigned_driver = winner.active_drivers[0] if winner.active_drivers else None

    justification = selection.get("justification") or _build_deterministic_justification(
        winner=winner,
        winner_route=winner_route,
        cargo_distance_km=cargo_distance_km,
        pricing_res=pricing_res,
        suggested_vehicle_class=suggested_vehicle_class,
        assigned_vehicle=assigned_vehicle,
        assigned_driver=assigned_driver,
    )

    output_data = {
        "selectedAgencyId": str(winner.agency_id),
        "selectedAgencyName": winner.name,
        "suggestedVehicleClass": suggested_vehicle_class,
        "etaMinutes": winner_route.eta_minutes,
        "positioningDistanceKm": winner_route.distance_km,
        "positioningRouteSimulated": winner_route.is_simulated,
        "cargoDistanceKm": cargo_distance_km,
        "cargoRouteSimulated": cargo_route.is_simulated,
        "proposedPrice": float(pricing_res.estimated_price),
        "pricingBreakdown": pricing_res.model_dump(by_alias=True),
        "selectionJustification": justification,
        "assignedVehicle": assigned_vehicle,
        "assignedDriver": assigned_driver,
        "rankedCandidates": ranked_five,
        "llmProvenance": llm_provenance,
    }

    # vehicleClass integer mapping: 0=MiniTruck, 1=MediumLorry, 2=ContainerTruck
    v_class_map = {"MiniTruck": 0, "MediumLorry": 1, "ContainerTruck": 2}
    most_suitable = {
        "agencyId": str(winner.agency_id),
        "estimatedPrice": float(pricing_res.estimated_price),
        "distanceKm": float(cargo_distance_km),
        "cargoRouteSimulated": cargo_route.is_simulated,
        "vehicleClass": v_class_map.get(suggested_vehicle_class, 0),
        "vehicleId": str(assigned_vehicle["vehicleId"]) if assigned_vehicle and "vehicleId" in assigned_vehicle else None,
        "registrationNo": assigned_vehicle.get("registrationNo") if assigned_vehicle else None,
        "driverId": str(assigned_driver["driverId"]) if assigned_driver and "driverId" in assigned_driver else None,
        "driverName": assigned_driver.get("name") if assigned_driver else None,
    }

    # Report step 3 outcome to backend
    steps.append({
        "stepNo": _STEP_NO,
        "agentRole": _AGENT_ROLE,
        "status": "Succeeded",
        "inputJson": input_data,
        "outputJson": output_data,
        "startedAt": started.isoformat(),
        "completedAt": now().isoformat(),
    })

    try:
        await report(
            workflow_run_id=workflow_run_id,
            step_no=_STEP_NO,
            agent_role=_AGENT_ROLE,
            status="Succeeded",
            started_at=started,
            input_data=input_data,
            output_data=output_data,
        )
    except BackendClientError:
        logger.warning("Failed to report step %s success for run %s", _STEP_NO, workflow_run_id)

    return {
        "selected_agency_id": winner.agency_id,
        "selected_agency_name": winner.name,
        "suggested_vehicle_class": suggested_vehicle_class,
        "eta_minutes": winner_route.eta_minutes,
        "positioning_distance_km": winner_route.distance_km,
        "cargo_distance_km": cargo_distance_km,
        "proposed_price": float(pricing_res.estimated_price),
        "pricing_breakdown": pricing_res.model_dump(by_alias=True),
        "selection_justification": justification,
        "assigned_vehicle": assigned_vehicle,
        "assigned_driver": assigned_driver,
        "ranked_five": ranked_five,
        "most_suitable": most_suitable,
        "tool_calls": tool_calls,
        "steps": steps,
    }
