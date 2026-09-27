"""Agent 2 - Domain Analysis.

Evaluates every candidate agency against a deterministic eligibility filter
(status, compliance, capacity), computes a rough haversine distance rank,
selects the top 5, and uses the LLM to explain the choices.
"""

import math
import logging
from datetime import date

from freightlink_agent.core.llm import get_llm
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report
from freightlink_agent.schemas.domain import EvaluatedAgency, RankedCandidate
from freightlink_agent.core.backend_client import get_active_agencies, create_match_candidate
from freightlink_agent.schemas.callback import CreateMatchCandidateRequest
"""Agent 2 — Domain Analysis.

Owner: Bandara (Component B).
Evaluates candidate carrier agencies against compliance, fleet capacity, and proximity,
and outputs an eligible shortlist (up to 5) for Agent 3.

Safe-failure rule: if zero eligible agencies are found, safe-fails immediately to avoid running Agent 3/4.
"""

import logging
from typing import Any
from uuid import UUID, uuid4

from freightlink_agent.core.backend_client import BackendClientError
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report
from freightlink_agent.schemas.matching import CandidateAgency

logger = logging.getLogger(__name__)

_STEP_NO = 2
_AGENT_ROLE = "DomainAnalysis"

_SYSTEM_PROMPT = (
    "You are the Domain Analysis agent. You have deterministically filtered a list of "
    "trucking agencies based on status (must be Active), compliance (must not be expired), "
    "and capacity (must have a vehicle large enough for the load). You then picked the "
    "top 5 closest eligible agencies.\n\n"
    "Explain in plain language why the top 5 were chosen and why others were excluded. "
    "Do not invent facts, just summarize the filtering results."
)


def haversine_distance(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    R = 6371.0  # Earth radius in kilometers
    dlat = math.radians(lat2 - lat1)
    dlon = math.radians(lon2 - lon1)
    a = (
        math.sin(dlat / 2) ** 2
        + math.cos(math.radians(lat1)) * math.cos(math.radians(lat2)) * math.sin(dlon / 2) ** 2
    )
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return R * c


async def run(state: WorkflowState) -> dict:
    started = now()

    # 1. Deterministic Filtering
    load = state.load_context
    weight_kg = float(load.get("weightKg", 0))
    volume_m3 = float(load.get("volumeM3", 0))
    pickup_lat = float(load.get("pickupLat", 0))
    pickup_lng = float(load.get("pickupLng", 0))

    today = date.today()

    evaluated = []
    eligible = []

    try:
        candidate_agencies = await get_active_agencies()
    except Exception as exc:
        logger.exception("Failed to fetch active agencies")
        return {"failed": True, "failure_reason": f"fetch_agencies_failed: {exc}"}

    for agency in candidate_agencies:
        reasons = []

        # Rule 1: Status
        if agency.status != "Active":
            reasons.append("Status is not Active")

        # Rule 2: Compliance
        if not agency.compliance or agency.compliance.expiry_date < today:
            reasons.append("Compliance documentation is missing or expired")

        # Rule 3: Capacity
        has_capacity = False
        for vc in agency.fleet:
            if vc.max_payload_kg >= weight_kg and vc.max_volume_m3 >= volume_m3:
                has_capacity = True
                break

        if not has_capacity:
            reasons.append(f"No vehicle in fleet has >= {weight_kg}kg and >= {volume_m3}m3 capacity")

        passed = len(reasons) == 0
        evaluated.append(
            EvaluatedAgency(
                agency_id=agency.id,
                passed=passed,
                rejection_reasons=reasons,
            )
        )

        if passed:
            dist = haversine_distance(pickup_lat, pickup_lng, agency.yard_lat, agency.yard_lng)
            eligible.append((dist, agency.id))

    # 2. Ranking
    eligible.sort(key=lambda x: x[0])
    top_5 = [RankedCandidate(agency_id=aid, haversine_distance_km=d) for d, aid in eligible[:5]]

    if state.workflow_run_id:
        try:
            for e in evaluated:
                # Find rank score if it's in the top 5
                rank_score = next((r.haversine_distance_km for r in top_5 if r.agency_id == e.agency_id), None)
                req = CreateMatchCandidateRequest(
                    agency_id=e.agency_id,
                    is_eligible=e.passed,
                    rejection_reason="; ".join(e.rejection_reasons) if e.rejection_reasons else None,
                    rank_score=rank_score
                )
                await create_match_candidate(state.workflow_run_id, req)
        except Exception as exc:
            logger.exception("Could not create match candidates")
            return {"failed": True, "failure_reason": f"create_match_candidate_failed: {exc}"}

    # Safe failure
    if not top_5:
        logger.warning("Agent 2 safe failure: 0 eligible agencies found for run %s", state.workflow_run_id)
        if state.workflow_run_id:
            try:
                await report(
                    workflow_run_id=state.workflow_run_id,
                    step_no=_STEP_NO,
                    agent_role=_AGENT_ROLE,
                    status="Succeeded", # Execution succeeded, but matched 0
                    started_at=started,
                    input_data={"candidateAgenciesCount": len(candidate_agencies)},
                    output_data={"evaluated_agencies": [e.model_dump() for e in evaluated]},
                )
            except Exception as exc:
                logger.exception("Could not report step %s safe failure", _STEP_NO)
                
        return {
            "evaluated_agencies": evaluated,
            "shortlisted_agencies": [],
            "failed": True,
            "failure_reason": "Zero eligible agencies after filtering."
        }

    # 3. LLM Explanation
    llm = get_llm()
    data_for_llm = {
        "top_5_agencies": [c.model_dump() for c in top_5],
        "evaluated_agencies": [e.model_dump() for e in evaluated]
    }

    try:
        explanation_result = await llm.domain_analysis_explain(
            system_prompt=_SYSTEM_PROMPT, data=data_for_llm
        )
        explanation = explanation_result["explanation"]
    except Exception as exc:
        logger.exception("Domain Analysis LLM call failed")
        return {"failed": True, "failure_reason": f"domain_analysis_llm_failed: {exc}"}

    # 4. Report Step
    input_data = {
        "candidateAgenciesCount": len(candidate_agencies),
        "loadContext": load,
    }
    output_data = {
        "top5": [c.model_dump() for c in top_5],
        "explanation": explanation,
    }

    if state.workflow_run_id:
        try:
            await report(
                workflow_run_id=state.workflow_run_id,

async def run(state: WorkflowState) -> dict[str, Any]:
    started = now()
    steps = list(state.steps)
    candidates_list: list[dict[str, Any]] = list(state.candidates)
    workflow_run_id = state.workflow_run_id

    ctx = state.load_context or {}
    weight_kg = float(ctx.get("weightKg") or ctx.get("weight_kg") or 1000.0)
    pickup_lat = float(ctx.get("pickupLat") or ctx.get("pickup_lat") or 0.0)
    pickup_lng = float(ctx.get("pickupLng") or ctx.get("pickup_lng") or 0.0)

    # Candidates can be provided in state.candidate_shortlist or in load_context
    raw_candidates = state.candidate_shortlist
    if not raw_candidates:
        context_candidates = ctx.get("candidateAgencies") or ctx.get("candidates") or []
        parsed: list[CandidateAgency] = []
        for c in context_candidates:
            if isinstance(c, CandidateAgency):
                parsed.append(c)
            elif isinstance(c, dict):
                parsed.append(
                    CandidateAgency(
                        agency_id=UUID(str(c.get("agencyId") or c.get("agency_id") or uuid4())),
                        name=str(c.get("name") or "Carrier"),
                        yard_lat=float(c.get("yardLat") or c.get("yard_lat") or pickup_lat),
                        yard_lng=float(c.get("yardLng") or c.get("yard_lng") or pickup_lng),
                        yard_address=str(c.get("yardAddress") or c.get("yard_address") or ""),
                        available_vehicle_classes=c.get("availableVehicleClasses")
                        or c.get("available_vehicle_classes")
                        or ["MediumLorry"],
                        available_vehicles=c.get("availableVehicles") or c.get("available_vehicles") or [],
                        active_drivers=c.get("activeDrivers") or c.get("active_drivers") or [],
                    )
                )
        raw_candidates = parsed

    input_data = {
        "loadId": str(state.load_id),
        "weightKg": weight_kg,
        "pickup": {"lat": pickup_lat, "lng": pickup_lng},
        "candidatesEvaluatedCount": len(raw_candidates),
        "candidates": [
            {
                "agencyId": str(c.agency_id),
                "name": c.name,
                "yardAddress": c.yard_address,
                "availableVehicleClasses": c.available_vehicle_classes,
                "availableVehicles": c.available_vehicles,
                "activeDrivers": c.active_drivers,
            }
            for c in raw_candidates
        ],
    }

    eligible_shortlist: list[CandidateAgency] = []

    for rank, cand in enumerate(raw_candidates, start=1):
        # Deterministic eligibility check: active carrier with valid capacity
        is_eligible = True
        rejection_reason = None

        if weight_kg > 10000.0 and "ContainerTruck" not in cand.available_vehicle_classes:
            is_eligible = False
            rejection_reason = "Fleet lacks required heavy ContainerTruck capacity"

        cand_record = {
            "matchCandidateId": str(uuid4()),
            "agencyId": str(cand.agency_id),
            "agencyName": cand.name,
            "rank": rank,
            "eligible": is_eligible,
            "eligibilityScore": round(max(0.0, 100.0 - (rank - 1) * 10.0), 2) if is_eligible else 0.0,
            "rejectionReason": rejection_reason,
            "evaluatedAt": now().isoformat(),
        }
        candidates_list.append(cand_record)

        if is_eligible:
            eligible_shortlist.append(cand)

    # Safe-failure short-circuit: zero eligible agencies -> stop, don't run Agent 3/4
    if not eligible_shortlist:
        msg = "zero_eligible_agencies"
        logger.warning("Agent 2 short-circuit: %s", msg)
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
        if workflow_run_id:
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
                logger.warning("Failed to report step %s for run %s", _STEP_NO, workflow_run_id)
        return {
            "failed": True,
            "failure_reason": msg,
            "candidates": candidates_list,
            "steps": steps,
        }

    # Pass forward up to top 5 eligible candidates
    final_shortlist = eligible_shortlist[:5]

    output_data = {
        "shortlistCount": len(final_shortlist),
        "eligibleCount": len(eligible_shortlist),
        "shortlist": [
            {
                "agencyId": str(c.agency_id),
                "name": c.name,
                "yardAddress": c.yard_address,
                "availableVehicleClasses": c.available_vehicle_classes,
                "availableVehicles": c.available_vehicles,
                "activeDrivers": c.active_drivers,
            }
            for c in final_shortlist
        ],
        "candidates": candidates_list,
    }

    steps.append({
        "stepNo": _STEP_NO,
        "agentRole": _AGENT_ROLE,
        "status": "Succeeded",
        "inputJson": input_data,
        "outputJson": output_data,
        "startedAt": started.isoformat(),
        "completedAt": now().isoformat(),
    })

    if workflow_run_id:
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
        except Exception as exc:
            logger.exception("Could not report step %s success", _STEP_NO)
            return {"failed": True, "failure_reason": f"report_step_failed: {exc}"}

    return {
        "evaluated_agencies": evaluated,
        "shortlisted_agencies": top_5,
        "domain_analysis_explanation": explanation,
        except BackendClientError:
            logger.warning("Failed to report step %s for run %s", _STEP_NO, workflow_run_id)

    return {
        "candidate_shortlist": final_shortlist,
        "candidates": candidates_list,
        "steps": steps,
    }
