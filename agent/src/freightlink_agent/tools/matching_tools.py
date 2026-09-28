"""LLM-invokable tool wrappers for Agent 3's genuine tool-calling loop.

Unlike tools/routing.py and tools/pricing.py (the raw HTTP-calling functions), the two
`@tool`-decorated functions built here are what the LLM itself actually sees and chooses
to invoke - their docstrings are the tool descriptions the model reads. Each wrapper still
delegates to the real routing/pricing functions, still persists a ToolCall audit row with
the literal ck_toolcall_allowlist-approved tool_name ("get_route_and_eta" / "estimate_price"
- the Python function names below are deliberately different from those persisted names),
and stores the real, typed response object in a per-run ledger keyed by a fresh tool_call_id.

Grounding rule (the deterministic-vs-LLM boundary this whole service enforces): the LLM
only ever gets a short text summary plus a tool_call_id back - never a bare number it could
casually restate wrong. Its final decision cites tool_call_ids; the caller (agents/
matching_pricing.py) looks the real numbers up from the ledger by those ids and never
trusts anything the LLM typed itself for a distance, ETA, or price.
"""

import json
import logging
from typing import Any, Literal
from uuid import UUID, uuid4

from langchain_core.tools import tool

from freightlink_agent.core.backend_client import BackendClientError, record_tool_call
from freightlink_agent.graph.step_reporter import now
from freightlink_agent.schemas.enums import VehicleClass
from freightlink_agent.schemas.matching import (
    CandidateAgency,
    CreateToolCallRequest,
    EstimatePricingRequest,
    RouteAndEtaRequest,
)
from freightlink_agent.tools.pricing import get_price_estimate
from freightlink_agent.tools.routing import get_route_and_eta

logger = logging.getLogger(__name__)


async def _persist_tool_call(
    *,
    workflow_run_id: UUID,
    tool_calls_audit: list[dict[str, Any]],
    attempt_counters: dict[str, int],
    tool_call_id: str,
    tool_name: str,
    request_payload: Any,
    response_payload: Any,
    success: bool,
    http_status_code: int | None,
    duration_ms: int | None,
    error_message: str | None,
) -> None:
    attempt_counters[tool_name] = attempt_counters.get(tool_name, 0) + 1
    attempt_no = attempt_counters[tool_name]
    req_json = json.dumps(request_payload, default=str)
    res_json = json.dumps(response_payload, default=str)

    tool_calls_audit.append({
        "toolCallId": tool_call_id,
        "toolName": tool_name,
        "attemptNo": attempt_no,
        "requestJson": req_json,
        "responseJson": res_json,
        "success": success,
        "httpStatusCode": http_status_code,
        "durationMs": duration_ms,
        "errorMessage": error_message,
        "calledAt": now().isoformat(),
    })

    try:
        await record_tool_call(
            workflow_run_id,
            CreateToolCallRequest(
                tool_name=tool_name,
                attempt_no=attempt_no,
                request_json=req_json,
                response_json=res_json,
                success=success,
                http_status_code=http_status_code,
                duration_ms=duration_ms,
                error_message=error_message,
                called_at=now(),
            ),
        )
    except BackendClientError:
        pass


def build_matching_tools(
    *,
    candidates_by_id: dict[str, CandidateAgency],
    pickup: tuple[float, float],
    dropoff: tuple[float, float],
    load_id: UUID,
    workflow_run_id: UUID,
    vehicle_class_by_candidate: dict[str, VehicleClass],
    ledger: dict[str, dict[str, Any]],
    tool_calls_audit: list[dict[str, Any]],
    attempt_counters: dict[str, int],
) -> list[Any]:
    """Builds the two tools for one Agent 3 run, closing over that run's own candidates,
    coordinates, and ledger so concurrent runs never share mutable state.

    attempt_counters is owned by the caller (agents/matching_pricing.py), not created here:
    if the LLM tool-calling path turns out to be unusable, the caller's deterministic
    fallback records ToolCall rows against this same AgentStep too, and must continue this
    same per-tool-name counter rather than restart at 1 - the backend's uq_toolcall_attempt
    constraint is unique on (AgentStepId, ToolName, AttemptNo)."""

    @tool
    async def get_route_and_eta_for_candidate(candidate_agency_id: str, leg: Literal["positioning", "cargo"]) -> str:
        """Looks up the real driving distance (km) and ETA (minutes) for one leg of this
        shipment, for one candidate agency.

        leg="positioning": from that candidate's yard to the load's pickup point - use this
        to compare how quickly each candidate can reach the pickup.
        leg="cargo": from the load's pickup point to its dropoff point (the actual freight
        movement). This leg's distance is the same regardless of which candidate you pick,
        but you must still call it (naming the candidate you are currently evaluating)
        before you can estimate a price for that candidate.

        On success, the result includes a tool_call_id. You MUST cite that exact
        tool_call_id in your final decision for every distance/ETA you rely on - never
        restate the number itself in your own words, since only a cited id is trusted.
        On failure, disqualify that candidate for that use - never invent a distance or ETA.
        """
        candidate = candidates_by_id.get(candidate_agency_id)
        if candidate is None:
            return (
                f"Error: unknown candidate_agency_id '{candidate_agency_id}'. Use one of the "
                f"candidate agency ids given to you in the candidates list."
            )

        if leg == "positioning":
            origin_lat, origin_lng = candidate.yard_lat, candidate.yard_lng
            dest_lat, dest_lng = pickup
        else:
            origin_lat, origin_lng = pickup
            dest_lat, dest_lng = dropoff

        req = RouteAndEtaRequest(
            origin_lat=origin_lat,
            origin_lng=origin_lng,
            destination_lat=dest_lat,
            destination_lng=dest_lng,
        )
        route_res, telemetry = await get_route_and_eta(req)

        tool_call_id = str(uuid4())
        ledger[tool_call_id] = {
            "type": "route",
            "leg": leg,
            "candidateAgencyId": candidate_agency_id,
            "result": route_res,
        }

        err_msg = route_res.error_message
        if not route_res.success:
            if not err_msg:
                err_msg = "hold for review: routing lookup failed after retry"
            elif "hold for review" not in err_msg.lower():
                err_msg = f"hold for review: {err_msg}"
            response_payload: Any = {"status": "hold for review", "error": err_msg}
        else:
            response_payload = route_res.model_dump(by_alias=True)

        await _persist_tool_call(
            workflow_run_id=workflow_run_id,
            tool_calls_audit=tool_calls_audit,
            attempt_counters=attempt_counters,
            tool_call_id=tool_call_id,
            tool_name="get_route_and_eta",
            request_payload=telemetry.get("request"),
            response_payload=response_payload,
            success=route_res.success,
            http_status_code=telemetry.get("httpStatusCode"),
            duration_ms=telemetry.get("durationMs"),
            error_message=err_msg if not route_res.success else None,
        )

        if not route_res.success:
            return f"tool_call_id={tool_call_id}. FAILED: {err_msg}. Do not invent a distance or ETA for this leg."

        sim_note = (
            " (SIMULATED estimate - no live routing API available; still the real number to use)"
            if route_res.is_simulated
            else ""
        )
        return (
            f"tool_call_id={tool_call_id}. distance_km={route_res.distance_km}, "
            f"eta_minutes={route_res.eta_minutes}{sim_note}. Cite tool_call_id='{tool_call_id}' "
            f"in your final decision if you rely on this result."
        )

    @tool
    async def estimate_price_for_load(candidate_agency_id: str, cargo_route_tool_call_id: str) -> str:
        """Computes the fixed shipment price for one candidate agency via the backend
        pricing service (ADR-015: base fare + cargo distance x rate/km + weight x rate/kg)
        - never compute or guess a price yourself.

        You must call get_route_and_eta_for_candidate(leg="cargo") for this candidate
        first, and pass its returned tool_call_id here as cargo_route_tool_call_id: the
        real cargo distance is looked up from that result, never from a number you type.
        The vehicle class is resolved automatically from this candidate's fleet - you do
        not choose it. On success, the result includes a tool_call_id you must cite in
        your final decision. On failure, disqualify this candidate - never invent a price.
        """
        candidate = candidates_by_id.get(candidate_agency_id)
        if candidate is None:
            return f"Error: unknown candidate_agency_id '{candidate_agency_id}'."

        cargo_entry = ledger.get(cargo_route_tool_call_id)
        if (
            cargo_entry is None
            or cargo_entry.get("type") != "route"
            or cargo_entry.get("leg") != "cargo"
            or not cargo_entry["result"].success
        ):
            return (
                f"Error: cargo_route_tool_call_id '{cargo_route_tool_call_id}' does not refer to a "
                f"successful cargo-leg routing result. Call get_route_and_eta_for_candidate("
                f"candidate_agency_id='{candidate_agency_id}', leg='cargo') first and use its "
                f"returned tool_call_id."
            )

        cargo_distance_km = cargo_entry["result"].distance_km
        suggested_vehicle_class = vehicle_class_by_candidate.get(candidate_agency_id, "MediumLorry")

        pricing_req = EstimatePricingRequest(
            load_id=load_id,
            suggested_vehicle_class=suggested_vehicle_class,
            distance_km=cargo_distance_km,
        )
        pricing_res, telemetry = await get_price_estimate(pricing_req)

        tool_call_id = str(uuid4())
        ledger[tool_call_id] = {
            "type": "pricing",
            "candidateAgencyId": candidate_agency_id,
            "result": pricing_res,
        }

        err_msg = telemetry.get("error")
        if not pricing_res:
            if not err_msg:
                err_msg = "hold for review: pricing estimation failed after retry"
            elif "hold for review" not in err_msg.lower():
                err_msg = f"hold for review: {err_msg}"
            response_payload = {"status": "hold for review", "error": err_msg}
        else:
            response_payload = pricing_res.model_dump(by_alias=True)

        await _persist_tool_call(
            workflow_run_id=workflow_run_id,
            tool_calls_audit=tool_calls_audit,
            attempt_counters=attempt_counters,
            tool_call_id=tool_call_id,
            tool_name="estimate_price",
            request_payload=pricing_req.model_dump(by_alias=True),
            response_payload=response_payload,
            success=pricing_res is not None,
            http_status_code=telemetry.get("httpStatusCode"),
            duration_ms=telemetry.get("durationMs"),
            error_message=err_msg if not pricing_res else None,
        )

        if not pricing_res:
            return f"tool_call_id={tool_call_id}. FAILED: {err_msg}. Do not invent a price for this candidate."

        return (
            f"tool_call_id={tool_call_id}. estimated_price_lkr={pricing_res.estimated_price}, "
            f"vehicle_class={suggested_vehicle_class}. Cite tool_call_id='{tool_call_id}' in "
            f"your final decision if you rely on this result."
        )

    return [get_route_and_eta_for_candidate, estimate_price_for_load]
