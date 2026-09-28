"""Agent 3 — Matching & Pricing.

Owner: Ratnaweera (Component C).
Tools: get_route_and_eta, get_price_estimate.

Selects the best agency using real routing data, prices the job using the cargo leg
distance, generates a natural-language recommendation justification for the Shipper via
OpenAI, records ToolCall audit records, and reports step 3 back to the backend.
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
    EstimatePricingResponse,
    RouteAndEtaRequest,
    RouteAndEtaResponse,
)
from freightlink_agent.tools.pricing import get_price_estimate
from freightlink_agent.tools.routing import get_route_and_eta

logger = logging.getLogger(__name__)

_STEP_NO = 3
_AGENT_ROLE = "MatchingPricing"

_SYSTEM_PROMPT = (
    "You are the Matching & Pricing AI Agent in the FreightLink logistics platform. "
    "Given the load specifications, candidate comparison metrics, positioning route, pricing breakdown, "
    "and the selected carrier's real database fleet vehicle and licensed driver, "
    "provide a professional, natural-language recommendation for the Shipper. "
    "CRITICAL GROUNDING RULES: "
    "1. Never invent mock carriers, mock vehicles, or mock drivers. Use ONLY the real carrier, assigned fleet vehicle "
    "(with actual registration plate), and assigned licensed driver provided in the context. "
    "2. Explicitly cite the carrier name, yard location, assigned vehicle registration plate, and licensed driver name. "
    "3. State why this agency was selected (fastest ETA to pickup, distance, fleet readiness). "
    "4. Explain the price estimate (LKR) with distance and cargo weight factors clearly and transparently. "
    "5. Keep the tone concise, authoritative, and professional for enterprise freight logistics."
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
        err_msg = route_res.error_message
        if not route_res.success:
            if not err_msg:
                err_msg = "hold for review: routing lookup failed after retry"
            elif "hold for review" not in err_msg.lower():
                err_msg = f"hold for review: {err_msg}"
            res_json = json.dumps({"status": "hold for review", "error": err_msg}, default=str)
        else:
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
            "errorMessage": err_msg if not route_res.success else None,
            "calledAt": now().isoformat(),
        }
        tool_calls.append(tool_call_dict)

        # Attempt reporting to backend if online
        try:
            tool_call_req = CreateToolCallRequest(
                tool_name="get_route_and_eta",
                attempt_no=idx,
                request_json=req_json,
                response_json=res_json,
                success=route_res.success,
                http_status_code=telemetry.get("httpStatusCode"),
                duration_ms=telemetry.get("durationMs"),
                error_message=err_msg if not route_res.success else None,
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
        msg = "hold for review: all candidate agency routing lookups failed after retry"
        logger.error(msg)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": input_data,
            "outputJson": {"status": "hold for review", "reason": msg},
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
    cargo_err_msg = cargo_route.error_message
    if not cargo_route.success:
        if not cargo_err_msg:
            cargo_err_msg = "hold for review: cargo routing lookup failed after retry"
        elif "hold for review" not in cargo_err_msg.lower():
            cargo_err_msg = f"hold for review: {cargo_err_msg}"
        cargo_res_json = json.dumps({"status": "hold for review", "error": cargo_err_msg}, default=str)
    else:
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
        "errorMessage": cargo_err_msg if not cargo_route.success else None,
        "calledAt": now().isoformat(),
    }
    tool_calls.append(cargo_tool_call_dict)

    try:
        await record_tool_call(
            workflow_run_id,
            CreateToolCallRequest(
                tool_name="get_route_and_eta",
                attempt_no=len(shortlist_to_eval) + 1,
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
        msg = f"hold for review: cargo leg routing lookup failed: {cargo_err_msg}"
        logger.error(msg)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": input_data,
            "outputJson": {"status": "hold for review", "reason": msg},
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
    pricing_err_msg = pricing_telemetry.get("error")
    if not pricing_res:
        if not pricing_err_msg:
            pricing_err_msg = "hold for review: pricing estimation failed after retry"
        elif "hold for review" not in pricing_err_msg.lower():
            pricing_err_msg = f"hold for review: {pricing_err_msg}"
        pricing_res_json = json.dumps({"status": "hold for review", "error": pricing_err_msg}, default=str)
    else:
        pricing_res_json = json.dumps(pricing_res.model_dump(by_alias=True), default=str)

    pricing_tool_call_dict = {
        "toolCallId": str(uuid4()),
        "toolName": "estimate_price",
        "attemptNo": 1,
        "requestJson": pricing_req_json,
        "responseJson": pricing_res_json,
        "success": pricing_res is not None,
        "httpStatusCode": pricing_telemetry.get("httpStatusCode"),
        "durationMs": pricing_telemetry.get("durationMs"),
        "errorMessage": pricing_err_msg if not pricing_res else None,
        "calledAt": now().isoformat(),
    }
    tool_calls.append(pricing_tool_call_dict)

    try:
        await record_tool_call(
            workflow_run_id,
            CreateToolCallRequest(
                tool_name="estimate_price",
                attempt_no=1,
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
    # this run cleanly rather than fabricate a price. The previous fallback here also
    # referenced VehicleClass.MiniTruck as if VehicleClass were an enum with attributes -
    # it's a Literal type alias, so that comparison always raised AttributeError before
    # it could even construct its (separately shape-mismatched) fallback response.
    if not pricing_res:
        msg = f"hold for review: pricing estimation failed: {pricing_err_msg}"
        logger.error(msg)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": input_data,
            "outputJson": {"status": "hold for review", "reason": msg},
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

    # Step 4: LLM justification call (OpenAI)
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
            "assignedVehicle": assigned_vehicle,
            "assignedDriver": assigned_driver,
            "availableVehicles": winner.available_vehicles,
            "activeDrivers": winner.active_drivers,
        },
        "pricing": pricing_res.model_dump(by_alias=True),
        "otherCandidatesEvaluated": [
            {
                "name": cand.name,
                "etaMinutes": r.eta_minutes,
                "positioningDistanceKm": r.distance_km,
                "availableVehiclesCount": len(cand.available_vehicles),
                "activeDriversCount": len(cand.active_drivers),
            }
            for cand, r in routed_candidates
            if cand.agency_id != winner.agency_id
        ],
    }

    llm_provenance: dict[str, Any] | None = None
    try:
        llm = get_llm()
        llm_out = await llm.justify_selection(
            system_prompt=_SYSTEM_PROMPT,
            context=llm_context,
        )
        headline = llm_out.get("headline", "")
        detailed = llm_out.get("detailed_reasoning", "")
        justification = f"{headline}\n\n{detailed}".strip()
        llm_provenance = getattr(llm, "last_call_meta", None)
    except Exception as exc:  # noqa: BLE001
        logger.warning("LLM selection justification failed (%s); using deterministic explanation", exc)
        veh_reg = (
            assigned_vehicle.get("registrationNo") or assigned_vehicle.get("registration_no")
            if assigned_vehicle
            else "Verified Fleet Vehicle"
        )
        dr_name = assigned_driver.get("name") if assigned_driver else "Licensed Carrier Driver"
        justification = (
            f"Recommended Carrier: {winner.name} (Yard: {winner.yard_address or 'Hub'}). "
            f"Assigned Vehicle: {veh_reg} ({suggested_vehicle_class}) with Driver: {dr_name}. "
            f"Nearest available carrier with ETA {winner_route.eta_minutes} min to pickup ({winner_route.distance_km} km positioning). "
            f"Cargo transit distance: {cargo_distance_km} km. Estimated price: LKR {pricing_res.estimated_price:,.2f}."
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
        "assigned_vehicle": assigned_vehicle,
        "assigned_driver": assigned_driver,
        "ranked_five": ranked_five,
        "most_suitable": most_suitable,
        "tool_calls": tool_calls,
        "steps": steps,
    }
