"""Agent 2 — Domain Analysis.

Owner: Bandara (Component B).
Evaluates candidate carrier agencies pushed by the backend (already pre-filtered to
AgencyStatus.Active with an available vehicle and an active driver, per ADR-020 - Agent 2
has zero direct database access, per ADR-009, so it can only re-check what's actually in
that payload) against real fleet capacity and proximity, and outputs an eligible shortlist
(up to 5) for Agent 3.

Safe-failure rule: if zero eligible agencies are found, safe-fails immediately to avoid running Agent 3/4.
"""

import logging
import math
from typing import Any
from uuid import UUID

from freightlink_agent.core.backend_client import BackendClientError, record_match_candidates
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report
from freightlink_agent.schemas.callback import MatchCandidateRequest
from freightlink_agent.schemas.matching import CandidateAgency

logger = logging.getLogger(__name__)

_STEP_NO = 2
_AGENT_ROLE = "DomainAnalysis"

# Conservative per-class capacity ceiling (kg), used only when a candidate's
# available_vehicles list has no concrete capacityKg to check directly - the real
# backend payload always includes it (every Active agency has >=1 Available vehicle),
# so this only matters for callers/tests that supply availableVehicleClasses alone.
_CLASS_CAPACITY_CEILING_KG: dict[str, float] = {
    "MiniTruck": 1500.0,
    "MediumLorry": 10000.0,
    "ContainerTruck": float("inf"),
}


def _haversine_km(lat1: float, lng1: float, lat2: float, lng2: float) -> float:
    r = 6371.0
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    d_phi = math.radians(lat2 - lat1)
    d_lambda = math.radians(lng2 - lng1)
    a = math.sin(d_phi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(d_lambda / 2) ** 2
    return r * 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))


def _has_capacity(cand: CandidateAgency, weight_kg: float, volume_m3: float) -> bool:
    """Real fleet-capacity check against the load's actual weight/volume - not just a
    single ContainerTruck-over-10000kg special case (the narrower check this replaces)."""
    if cand.available_vehicles:
        return any(
            float(v.get("capacityKg") or v.get("capacity_kg") or 0.0) >= weight_kg
            and float(v.get("volumeM3") or v.get("volume_m3") or 0.0) >= volume_m3
            for v in cand.available_vehicles
        )
    return any(
        _CLASS_CAPACITY_CEILING_KG.get(vc, 0.0) >= weight_kg for vc in cand.available_vehicle_classes
    )


async def run(state: WorkflowState) -> dict[str, Any]:
    started = now()
    steps = list(state.steps)
    candidates_list: list[dict[str, Any]] = list(state.candidates)
    workflow_run_id = state.workflow_run_id

    ctx = state.load_context or {}
    weight_kg = float(ctx.get("weightKg") or ctx.get("weight_kg") or 1000.0)
    volume_m3 = float(ctx.get("volumeM3") or ctx.get("volume_m3") or 0.0)
    pickup_lat = float(ctx.get("pickupLat") or ctx.get("pickup_lat") or 0.0)
    pickup_lng = float(ctx.get("pickupLng") or ctx.get("pickup_lng") or 0.0)

    # Candidates can be provided in state.candidate_shortlist or in load_context
    raw_candidates = list(state.candidate_shortlist)
    if not raw_candidates:
        context_candidates = ctx.get("candidateAgencies") or ctx.get("candidates") or []
        parsed: list[CandidateAgency] = []
        for c in context_candidates:
            if isinstance(c, CandidateAgency):
                parsed.append(c)
            elif isinstance(c, dict):
                agency_id_raw = c.get("agencyId") or c.get("agency_id")
                if not agency_id_raw:
                    # Never fabricate an agency id for missing data - a made-up UUID
                    # would never match a real Agency row and would fail the FK
                    # constraint when persisted via record_match_candidates below.
                    logger.warning("Skipping candidate with no agencyId: %r", c)
                    continue
                try:
                    agency_id = UUID(str(agency_id_raw))
                except ValueError:
                    logger.warning("Skipping candidate with malformed agencyId: %r", agency_id_raw)
                    continue
                parsed.append(
                    CandidateAgency(
                        agency_id=agency_id,
                        name=str(c.get("name") or "Carrier"),
                        yard_lat=float(c.get("yardLat") or c.get("yard_lat") or pickup_lat),
                        yard_lng=float(c.get("yardLng") or c.get("yard_lng") or pickup_lng),
                        yard_address=str(c.get("yardAddress") or c.get("yard_address") or ""),
                        available_vehicle_classes=c.get("availableVehicleClasses")
                        or c.get("available_vehicle_classes")
                        or [],
                        available_vehicles=c.get("availableVehicles") or c.get("available_vehicles") or [],
                        active_drivers=c.get("activeDrivers") or c.get("active_drivers") or [],
                    )
                )
        raw_candidates = parsed

    # Rank by proximity (yard -> pickup) first, so `rank` is meaningful for every
    # candidate regardless of eligibility, and the eligible shortlist is naturally
    # nearest-first once filtered.
    raw_candidates.sort(key=lambda c: _haversine_km(pickup_lat, pickup_lng, c.yard_lat, c.yard_lng))

    input_data = {
        "loadId": str(state.load_id),
        "weightKg": weight_kg,
        "volumeM3": volume_m3,
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
    match_candidate_requests: list[MatchCandidateRequest] = []

    for rank, cand in enumerate(raw_candidates, start=1):
        is_eligible = _has_capacity(cand, weight_kg, volume_m3)
        rejection_reason = (
            None
            if is_eligible
            else f"Fleet lacks a vehicle with >= {weight_kg}kg and >= {volume_m3}m3 capacity"
        )
        eligibility_score = round(max(0.0, 100.0 - (rank - 1) * 10.0), 2) if is_eligible else 0.0

        cand_record = {
            "matchCandidateId": None,
            "agencyId": str(cand.agency_id),
            "agencyName": cand.name,
            "rank": rank,
            "eligible": is_eligible,
            "eligibilityScore": eligibility_score,
            "rejectionReason": rejection_reason,
            "evaluatedAt": now().isoformat(),
        }
        candidates_list.append(cand_record)
        match_candidate_requests.append(
            MatchCandidateRequest(
                agency_id=cand.agency_id,
                rank=rank,
                eligible=is_eligible,
                eligibility_score=eligibility_score,
                rejection_reason=rejection_reason,
            )
        )

        if is_eligible:
            eligible_shortlist.append(cand)

    if workflow_run_id and match_candidate_requests:
        try:
            await record_match_candidates(workflow_run_id, match_candidate_requests)
        except BackendClientError:
            logger.warning("Failed to record match candidates for run %s", workflow_run_id)

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

    # Pass forward up to top 5 eligible candidates (already nearest-first)
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
        except BackendClientError:
            logger.warning("Failed to report step %s for run %s", _STEP_NO, workflow_run_id)

    return {
        "candidate_shortlist": final_shortlist,
        "candidates": candidates_list,
        "steps": steps,
    }
