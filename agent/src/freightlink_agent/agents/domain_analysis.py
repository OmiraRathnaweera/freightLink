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
    }
