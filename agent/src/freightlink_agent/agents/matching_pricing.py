"""Agent 3 — Matching & Pricing.

Owner: Ratnaweera (Component C).
Tools: get_route_and_eta, get_price_estimate.

Selects the best agency using real routing data, prices the job using the cargo leg
distance, generates a natural-language recommendation justification for the Shipper via
Gemini/Ollama, records ToolCall audit records, and reports step 3 back to the backend.
"""

import json
import logging
from typing import Any
from uuid import UUID, uuid4

from freightlink_agent.core.backend_client import BackendClientError, record_tool_call
from freightlink_agent.core.llm import get_llm
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report
from freightlink_agent.schemas.enums import VehicleClass
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    CreateToolCallRequest,
    EstimatePricingRequest,
    RouteAndEtaRequest,
    RouteAndEtaResponse,
)
from freightlink_agent.tools.pricing import get_price_estimate
from freightlink_agent.tools.routing import get_route_and_eta

logger = logging.getLogger(__name__)

_STEP_NO = 3
_AGENT_ROLE = "MatchingPricing"

_SYSTEM_PROMPT = (
    "You are the Matching & Pricing agent in a freight-matching platform. "
    "Given the load details, candidate comparisons, real-world route metrics, and pricing breakdown, "
    "provide a concise, natural-language recommendation for the Shipper. "
    "Highlight why this agency was chosen (e.g. fastest ETA to pickup, fleet readiness) and explain "
    "the price estimate. Ground your response strictly in the provided data — do not invent facts."
)


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

    # Step 1: Route positioning leg (Yard -> Pickup) for up to top 5 candidates
    shortlist_to_eval = candidates[:5]
    routed_candidates: list[tuple[CandidateAgency, RouteAndEtaResponse]] = []

    for idx, candidate in enumerate(shortlist_to_eval, start=1):
        req = RouteAndEtaRequest(
            origin_lat=candidate.yard_lat,
            origin_lng=candidate.yard_lng,
            destination_lat=pickup_lat,
            destination_lng=pickup_lng,
        )
        route_res, telemetry = await get_route_and_eta(req)

        # In-memory ToolCall audit record
        req_json = json.dumps(telemetry.get("request"), default=str)
        res_json = json.dumps(route_res.model_dump(by_alias=True), default=str)
        tool_call_dict = {
            "toolCallId": str(uuid4()),
            "toolName": "get_route_and_eta",
            "attemptNo": idx,
            "requestJson": req_json,
            "responseJson": res_json,
            "success": route_res.success,
            "httpStatusCode": telemetry.get("httpStatusCode"),
            "durationMs": telemetry.get("durationMs"),
            "errorMessage": route_res.error_message,
            "calledAt": now().isoformat(),
        }
        tool_calls.append(tool_call_dict)

        # Attempt reporting to backend if online
        try:
            tool_call_req = CreateToolCallRequest(
                agent_step_id=workflow_run_id,
                tool_name="get_route_and_eta",
                attempt_no=idx,
                request_json=req_json,
                response_json=res_json,
                success=route_res.success,
                http_status_code=telemetry.get("httpStatusCode"),
                duration_ms=telemetry.get("durationMs"),
                error_message=route_res.error_message,
                called_at=now(),
            )
            await record_tool_call(workflow_run_id, tool_call_req)
        except BackendClientError:
            pass

        if route_res.success and route_res.eta_minutes is not None:
            routed_candidates.append((candidate, route_res))
        else:
            logger.warning("Routing failed for candidate %s: %s", candidate.name, route_res.error_message)

    if not routed_candidates:
        msg = "All candidate agency routing lookups failed"
        logger.error(msg)
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
            pass
        return {"failed": True, "failure_reason": msg, "tool_calls": tool_calls, "steps": steps}

    # Step 2: Rank routed candidates by ETA & distance (top 5)
    sorted_routed = sorted(
        routed_candidates,
        key=lambda pair: (pair[1].eta_minutes or 999999, pair[1].distance_km or 999999),
    )
    ranked_five = [
        {
            "agencyId": str(cand.agency_id),
            "etaMinutes": r.eta_minutes or 0,
            "distanceKm": r.distance_km or 0.0,
        }
        for cand, r in sorted_routed[:5]
    ]

    winner, winner_route = sorted_routed[0]
    suggested_vehicle_class = _determine_vehicle_class(
        weight_kg=weight_kg,
        volume_m3=volume_m3,
        available_classes=winner.available_vehicle_classes,
    )

    # Step 3: Route cargo leg (Pickup -> Dropoff) & calculate pricing
    # Critical (ADR-015 addendum): Pricing uses the cargo leg distance, not the yard->pickup positioning leg!
    cargo_req = RouteAndEtaRequest(
        origin_lat=pickup_lat,
        origin_lng=pickup_lng,
        destination_lat=dropoff_lat,
        destination_lng=dropoff_lng,
    )
    cargo_route, cargo_telemetry = await get_route_and_eta(cargo_req)

    cargo_req_json = json.dumps(cargo_telemetry.get("request"), default=str)
    cargo_res_json = json.dumps(cargo_route.model_dump(by_alias=True), default=str)
    cargo_tool_call_dict = {
        "toolCallId": str(uuid4()),
        "toolName": "get_route_and_eta",
        "attemptNo": len(shortlist_to_eval) + 1,
        "requestJson": cargo_req_json,
        "responseJson": cargo_res_json,
        "success": cargo_route.success,
        "httpStatusCode": cargo_telemetry.get("httpStatusCode"),
        "durationMs": cargo_telemetry.get("durationMs"),
        "errorMessage": cargo_route.error_message,
        "calledAt": now().isoformat(),
    }
    tool_calls.append(cargo_tool_call_dict)

    try:
        await record_tool_call(
            workflow_run_id,
            CreateToolCallRequest(
                agent_step_id=workflow_run_id,
                tool_name="get_route_and_eta",
                attempt_no=len(shortlist_to_eval) + 1,
                request_json=cargo_req_json,
                response_json=cargo_res_json,
                success=cargo_route.success,
                http_status_code=cargo_telemetry.get("httpStatusCode"),
                duration_ms=cargo_telemetry.get("durationMs"),
                error_message=cargo_route.error_message,
                called_at=now(),
            ),
        )
    except BackendClientError:
        pass

    if not cargo_route.success or cargo_route.distance_km is None or cargo_route.distance_km <= 0:
        msg = f"Cargo leg routing lookup failed: {cargo_route.error_message}"
        logger.error(msg)
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
            pass
        return {
            "failed": True,
            "failure_reason": msg,
            "tool_calls": tool_calls,
            "ranked_five": ranked_five,
            "steps": steps,
        }

    cargo_distance_km = cargo_route.distance_km

    # Call backend pricing estimator endpoint (POST /internal/pricing/estimate)
    pricing_req = EstimatePricingRequest(
        load_id=state.load_id,
        suggested_vehicle_class=suggested_vehicle_class,
        distance_km=cargo_distance_km,
    )
    pricing_res, pricing_telemetry = await get_price_estimate(pricing_req)

    pricing_req_json = json.dumps(pricing_req.model_dump(by_alias=True), default=str)
    pricing_res_json = json.dumps(pricing_res.model_dump(by_alias=True) if pricing_res else {}, default=str)
    pricing_tool_call_dict = {
        "toolCallId": str(uuid4()),
        "toolName": "estimate_price",
        "attemptNo": 1,
        "requestJson": pricing_req_json,
        "responseJson": pricing_res_json,
        "success": pricing_res is not None,
        "httpStatusCode": pricing_telemetry.get("httpStatusCode"),
        "durationMs": pricing_telemetry.get("durationMs"),
        "errorMessage": pricing_telemetry.get("error"),
        "calledAt": now().isoformat(),
    }
    tool_calls.append(pricing_tool_call_dict)

    if not pricing_res:
        msg = f"Pricing estimation failed: {pricing_telemetry.get('error')}"
        logger.error(msg)
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
            pass
        return {
            "failed": True,
            "failure_reason": msg,
            "tool_calls": tool_calls,
            "ranked_five": ranked_five,
            "steps": steps,
        }

    # Step 4: LLM justification call (Gemini 2.5 Flash / Ollama)
    llm_context = {
        "load": {
            "weightKg": weight_kg,
            "volumeM3": volume_m3,
            "cargoDescription": cargo_desc,
            "cargoDistanceKm": cargo_distance_km,
        },
        "selectedAgency": {
            "agencyId": str(winner.agency_id),
            "name": winner.name,
            "yardAddress": winner.yard_address,
            "etaMinutes": winner_route.eta_minutes,
            "positioningDistanceKm": winner_route.distance_km,
            "suggestedVehicleClass": suggested_vehicle_class,
        },
        "pricing": pricing_res.model_dump(by_alias=True),
        "otherCandidatesEvaluated": [
            {
                "name": cand.name,
                "etaMinutes": r.eta_minutes,
                "positioningDistanceKm": r.distance_km,
            }
            for cand, r in routed_candidates
            if cand.agency_id != winner.agency_id
        ],
    }

    try:
        llm = get_llm()
        llm_out = await llm.justify_selection(
            system_prompt=_SYSTEM_PROMPT,
            context=llm_context,
        )
        headline = llm_out.get("headline", "")
        detailed = llm_out.get("detailed_reasoning", "")
        justification = f"{headline}\n\n{detailed}".strip()
    except Exception as exc:  # noqa: BLE001
        logger.warning("LLM selection justification failed (%s); using deterministic explanation", exc)
        justification = (
            f"Recommended: {winner.name} — nearest available carrier with suitable {suggested_vehicle_class} "
            f"capacity (ETA {winner_route.eta_minutes} min to pickup, {winner_route.distance_km} km positioning). "
            f"Cargo transit distance: {cargo_distance_km} km. Estimated price: LKR {pricing_res.estimated_price:,.2f}."
        )

    output_data = {
        "selectedAgencyId": str(winner.agency_id),
        "selectedAgencyName": winner.name,
        "suggestedVehicleClass": suggested_vehicle_class,
        "etaMinutes": winner_route.eta_minutes,
        "positioningDistanceKm": winner_route.distance_km,
        "cargoDistanceKm": cargo_distance_km,
        "proposedPrice": float(pricing_res.estimated_price),
        "pricingBreakdown": pricing_res.model_dump(by_alias=True),
        "selectionJustification": justification,
    }

    # vehicleClass integer mapping: 0=MiniTruck, 1=MediumLorry, 2=ContainerTruck
    v_class_map = {"MiniTruck": 0, "MediumLorry": 1, "ContainerTruck": 2}
    most_suitable = {
        "agencyId": str(winner.agency_id),
        "estimatedPrice": float(pricing_res.estimated_price),
        "distanceKm": float(cargo_distance_km),
        "vehicleClass": v_class_map.get(suggested_vehicle_class, 0),
    }

    # Step 5: Report step 3 outcome to backend
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
        "ranked_five": ranked_five,
        "most_suitable": most_suitable,
        "tool_calls": tool_calls,
        "steps": steps,
    }
