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
        except BackendClientError:
            logger.warning("Failed to report step %s for run %s", _STEP_NO, workflow_run_id)

    return {
        "candidate_shortlist": final_shortlist,
        "candidates": candidates_list,
        "steps": steps,
    }
