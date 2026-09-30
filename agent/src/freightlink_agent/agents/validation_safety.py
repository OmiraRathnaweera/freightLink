"""Agent 4 — Validation & Safety (Component D, Owner: Balasooriya).

Architectural Guardrails (Non-Negotiable):
1. Zero Database Access: The Python service has NO database driver, ORM, connection string,
   or direct database access. All reads and writes must pass through internal ASP.NET Core endpoints
   guarded by the X-Internal-Api-Key HTTP header.
2. Deterministic vs. LLM Boundary:
   - Deterministic (Plain Python Code): Sanity checks, validation rules, threshold checks,
     and price-deviation arithmetic. The LLM must NEVER compute a number, evaluate a math formula,
     or decide pass/fail validation logic.
   - LLM Reasoning: Explaining the validation results in plain language for the Shipper and writing
     the personalized job proposal email copy for the agency.
3. LLM Provider:
   - Use OpenAI (gpt-4o-mini by default) via LangChain as the only LLM provider (ADR-008 addendum #2).
   - No Gemini, no Ollama, no other fallback provider - if OpenAI fails, fall back to a
     deterministic, template-based copy (see core/llm.py's _fallback_validation_copy).
   - The agent MUST execute a real LLM call (university rubric requirement).
4. Approval Gate & Authority:
   - Every single run—including every automatic retry—must unconditionally pause for human approval (AwaitingApproval).
   - The approving authority is always the Shipper, never the Admin.
5. No Agency Bidding (ADR-017):
   - On approval, the agency receives a Job Proposal with the fixed price computed by Agent 3.
   - Do NOT ask the agency to bid, negotiate, or provide a quote.
6. Persistence Pattern (Channel 1):
   - Persist Agent 4's execution step incrementally via POST /internal/agent-workflow-runs/{workflow_run_id}/steps
     before or at the pause. Never batch writes to the end of the workflow.
"""

import json
import logging
from typing import Any
from uuid import UUID, uuid4

from freightlink_agent.core.backend_client import BackendClientError
from freightlink_agent.core.llm import get_llm
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report

logger = logging.getLogger(__name__)

_STEP_NO = 4
_AGENT_ROLE = "ValidationSafety"

_SYSTEM_PROMPT = (
    "You are Agent 4 (Validation & Safety, Component D, Owner: Balasooriya) for FreightLink, "
    "a freight-matching platform in Sri Lanka.\n\n"
    "Your responsibility is twofold:\n"
    "1. VALIDATION SUMMARY (FOR THE SHIPPER):\n"
    "   Explain the validation findings and safety checks in plain, reassuring, and professional language "
    "   specifically for the SHIPPER. Detail why the selected agency, vehicle, driver, and route were approved. "
    "   Summarize the proposed price (in LKR) and budget comparison. Clearly inform the Shipper that the match "
    "   is paused awaiting their explicit approval.\n"
    "2. JOB PROPOSAL EMAIL (FOR THE MATCHED AGENCY):\n"
    "   Draft a personalized Job Proposal email (subject and body) to the matched agency.\n\n"
    "STRICT ARCHITECTURAL DIRECTIVE (ADR-017 - NO AGENCY BIDDING):\n"
    "The email MUST be a formal Job Proposal offering the freight assignment at the fixed price already "
    "calculated by the platform. You MUST NOT ask the agency to bid, negotiate, submit a quote, or name a price. "
    "It is a firm dispatch proposal at a pre-calculated, confirmed compensation amount."
)


def evaluate_deterministic_rules(state: WorkflowState) -> tuple[bool, list[str], float | None, list[dict[str, Any]]]:
    """Pure deterministic sanity checks, validation rules, threshold checks, and price-deviation arithmetic.

    The LLM NEVER computes a number, evaluates a math formula, or decides pass/fail validation logic.
    """
    flags: list[str] = []
    checks: list[dict[str, Any]] = []

    ctx = state.load_context or {}
    weight_kg = float(ctx.get("weightKg") or ctx.get("weight_kg") or 0.0)
    volume_m3 = float(ctx.get("volumeM3") or ctx.get("volume_m3") or 0.0)

    selected_agency_id = state.selected_agency_id
    selected_agency_name = state.selected_agency_name or "Selected Agency"
    proposed_price = state.proposed_price
    cargo_distance_km = state.cargo_distance_km
    eta_minutes = state.eta_minutes
    vehicle_class = str(state.suggested_vehicle_class or ctx.get("suggestedVehicleClass") or "")

    # Check 1: Carrier eligibility & presence. Fails closed on a missing/empty shortlist -
    # a run reaching Agent 4 without Agent 2's shortlist threaded through cannot actually
    # prove the selected agency was ever screened, so it must not pass by default
    # (previously `not eligible_agency_ids` made this pass whenever the shortlist was
    # simply absent, the exact false-pass the audit flagged as most safety-critical).
    eligible_agency_ids = {c.agency_id for c in state.candidate_shortlist} if state.candidate_shortlist else set()
    carrier_passed = (
        selected_agency_id is not None
        and bool(eligible_agency_ids)
        and selected_agency_id in eligible_agency_ids
    )
    if not carrier_passed:
        flags.append(f"CARRIER_ELIGIBILITY_FAILED: Agency {selected_agency_id} not eligible.")
    checks.append({
        "name": "carrier_eligibility",
        "passed": carrier_passed,
        "details": (
            f"Carrier '{selected_agency_name}' ({selected_agency_id}) verified active and eligible."
            if carrier_passed
            else f"Carrier {selected_agency_id} not found in eligible shortlist."
        ),
    })

    # Check 2: Price bounds sanity
    price_passed = proposed_price is not None and proposed_price > 0.0
    if not price_passed:
        flags.append(f"INVALID_PRICE: Proposed price must be positive (got {proposed_price}).")
    checks.append({
        "name": "price_bounds",
        "passed": price_passed,
        "details": (
            f"Proposed price LKR {proposed_price:,.2f} is positive and within acceptable system bounds."
            if price_passed
            else f"Invalid proposed price: {proposed_price}."
        ),
    })

    # Check 3: Routing & ETA sanity. A missing cargo_distance_km must fail this check, not
    # pass it - the previous `cargo_distance_km is None or ...` let a run with no distance
    # data at all sail through (the audit's "missing cargo distance may pass routing
    # sanity" finding).
    route_passed = (
        cargo_distance_km is not None
        and cargo_distance_km > 0.0
        and eta_minutes is not None
        and eta_minutes > 0
    )
    if not route_passed:
        flags.append(f"INVALID_ROUTING: Distance or transit ETA invalid (distance={cargo_distance_km}, eta={eta_minutes}).")
    elif eta_minutes is not None and eta_minutes > 10080:  # > 7 days transit in Sri Lanka
        flags.append("ETA_EXCEEDS_NORMAL_THRESHOLD: Estimated transit time exceeds 7 days.")
    checks.append({
        "name": "routing_sanity",
        "passed": route_passed,
        "details": (
            f"Cargo distance {cargo_distance_km or 'N/A'} km and ETA {eta_minutes} min verified valid."
            if route_passed
            else f"Invalid routing metrics: distance={cargo_distance_km}, eta={eta_minutes}."
        ),
    })

    # Check 4: Vehicle capacity & class sanity
    capacity_passed = True
    capacity_reason = f"Vehicle class '{vehicle_class}' accommodates {weight_kg} kg / {volume_m3} m3."
    if not vehicle_class:
        capacity_passed = False
        capacity_reason = "No suggested vehicle class specified."
        flags.append("MISSING_VEHICLE_CLASS: Suggested vehicle class is required.")
    elif weight_kg > 45000.0:
        capacity_passed = False
        capacity_reason = f"Load weight {weight_kg} kg exceeds 45,000 kg highway safety regulations."
        flags.append("WEIGHT_EXCEEDS_MAX_LEGAL_LIMIT: Cargo weight exceeds legal limits.")
    elif weight_kg > 10000.0 and vehicle_class not in ("ContainerTruck", "MultiAxleTruck", "HeavyTruck"):
        capacity_passed = False
        capacity_reason = f"Load weight {weight_kg} kg exceeds {vehicle_class} capacity limits."
        flags.append(f"VEHICLE_OVERWEIGHT: Load weight {weight_kg} kg exceeds {vehicle_class} capacity.")
    elif weight_kg > 5000.0 and vehicle_class in ("MiniTruck", "Van"):
        capacity_passed = False
        capacity_reason = f"Load weight {weight_kg} kg exceeds {vehicle_class} capacity limits."
        flags.append(f"VEHICLE_OVERWEIGHT: Load weight {weight_kg} kg exceeds {vehicle_class} capacity.")

    checks.append({
        "name": "vehicle_capacity",
        "passed": capacity_passed,
        "details": capacity_reason,
    })

    # Check 5: Active Licensed Driver Compliance. A missing assignment must fail, not pass -
    # the previous `True if not assigned_driver else ...` treated "nothing assigned" as
    # automatically compliant (the audit's "missing driver/vehicle assignments pass their
    # checks" finding).
    assigned_driver = state.assigned_driver
    driver_passed = bool(assigned_driver) and bool(
        assigned_driver.get("driverId") or assigned_driver.get("name")
    )
    if not driver_passed:
        flags.append("DRIVER_COMPLIANCE_FAILED: No verified driver assigned.")
    checks.append({
        "name": "driver_compliance",
        "passed": driver_passed,
        "details": (
            f"Driver '{assigned_driver.get('name', 'Active Commercial Driver')}' verified licensed."
            if driver_passed
            else "No assigned driver found - cannot verify licensed driver compliance."
        ),
    })

    # Check 6: Verified Fleet Vehicle. Same fail-closed fix as Check 5.
    assigned_vehicle = state.assigned_vehicle
    vehicle_passed = bool(assigned_vehicle) and bool(
        assigned_vehicle.get("vehicleId") or assigned_vehicle.get("registrationNo")
    )
    if not vehicle_passed:
        flags.append("VEHICLE_VERIFICATION_FAILED: No verified fleet vehicle assigned.")
    checks.append({
        "name": "vehicle_verification",
        "passed": vehicle_passed,
        "details": (
            f"Fleet vehicle '{assigned_vehicle.get('registrationNo', 'Verified Plate')}' verified available."
            if vehicle_passed
            else "No assigned fleet vehicle found - cannot verify vehicle availability."
        ),
    })

    # Price-deviation check: deliberately dropped (plans/02-contracts-and-agent-fixes.md §5 /
    # 00-master-plan.md open question #1). The audit confirmed Load creation has no
    # shipperBudget/budget field anywhere in the normal payload, so this arithmetic was
    # always silently a no-op in production - not a real check anyone could rely on. Rather
    # than add a new shipper-budget field across load creation (backend + both clients) to
    # give it something genuine to compare against, the check is removed; the field is kept
    # (always None) since nothing downstream reads it as anything but optional.
    price_deviation_percent: float | None = None

    critical_check_names = {
        "carrier_eligibility",
        "price_bounds",
        "routing_sanity",
        "vehicle_capacity",
        "driver_compliance",
        "vehicle_verification",
    }
    critical_failed = any(not c["passed"] for c in checks if c["name"] in critical_check_names)
    is_valid = not critical_failed

    return is_valid, flags, price_deviation_percent, checks


async def run(state: WorkflowState) -> dict[str, Any]:
    """Executes Agent 4 — Validation & Safety.

    - Performs deterministic sanity checks, validation rules, and price-deviation arithmetic.
    - Executes real LLM reasoning call (OpenAI only, deterministic template fallback) for Shipper
      validation summary and personalized Job Proposal email copy (ADR-017: No Bidding).
    - Incrementally persists step report via POST /internal/agent-workflow-runs/{id}/steps (Channel 1).
    - Unconditionally pauses for human approval by the Shipper (status: AwaitingApproval).
    """
    started = now()
    steps = list(state.steps)
    workflow_run_id = state.workflow_run_id or uuid4()

    # 1. Deterministic Validation (Pure Python Code)
    is_valid, validation_flags, price_deviation_percent, checks = evaluate_deterministic_rules(state)

    # 2. Status & Approval Gate (Rule 4)
    # Every single run—including every automatic retry—must unconditionally pause for human approval.
    # Authority is always the Shipper.
    status = "AwaitingApproval" if is_valid else "Failed"
    recommendation = "Approve" if is_valid else "Reject"

    # Context for LLM Reasoning
    selected_agency_name = state.selected_agency_name or "Selected Carrier Agency"
    proposed_price = state.proposed_price or 0.0
    vehicle_class = str(state.suggested_vehicle_class or "Standard Freight")
    eta_minutes = state.eta_minutes or 0

    llm_context: dict[str, Any] = {
        "load_id": str(state.load_id),
        "load_context": state.load_context,
        "selected_agency_id": str(state.selected_agency_id) if state.selected_agency_id else None,
        "selected_agency_name": selected_agency_name,
        "eta_minutes": eta_minutes,
        "proposed_price": proposed_price,
        "suggested_vehicle_class": vehicle_class,
        "is_valid": is_valid,
        "validation_flags": validation_flags,
        "price_deviation_percent": price_deviation_percent,
        "checks": checks,
    }

    # 3. LLM Reasoning Call (OpenAI only, deterministic template fallback on failure)
    llm = get_llm()
    llm_provenance: dict[str, Any] | None = None
    try:
        llm_output = await llm.generate_validation_summary_and_proposal(
            system_prompt=_SYSTEM_PROMPT,
            context=llm_context,
        )
        validation_summary = llm_output["validation_summary"]
        proposal_email_subject = llm_output["proposal_email_subject"]
        proposal_email_body = llm_output["proposal_email_body"]
        llm_provenance = getattr(llm, "last_call_meta", None)
    except Exception as exc:
        logger.exception("LLM call in Agent 4 failed, generating structured fallback")
        fallback = llm._fallback_validation_copy(llm_context)
        validation_summary = fallback["validation_summary"]
        proposal_email_subject = fallback["proposal_email_subject"]
        proposal_email_body = fallback["proposal_email_body"]

    explanation = validation_summary if is_valid else f"Validation failed: {'; '.join(validation_flags)}"

    validation = {
        "checks": checks,
        "explanation": explanation,
        "recommendation": recommendation,
        "isValid": is_valid,
        "validationFlags": validation_flags,
        "priceDeviationPercent": price_deviation_percent,
        "validationSummary": validation_summary,
        "proposalEmailSubject": proposal_email_subject,
        "proposalEmailBody": proposal_email_body,
        "status": status,
        "llmProvenance": llm_provenance,
    }

    input_data = {
        "loadId": str(state.load_id),
        "selectedAgencyId": str(state.selected_agency_id) if state.selected_agency_id else None,
        "selectedAgencyName": selected_agency_name,
        "proposedPrice": proposed_price,
        "cargoDistanceKm": state.cargo_distance_km,
        "positioningDistanceKm": state.positioning_distance_km,
        "etaMinutes": eta_minutes,
        "vehicleClass": vehicle_class,
        "assignedVehicle": state.assigned_vehicle,
        "assignedDriver": state.assigned_driver,
        "loadContext": state.load_context,
    }

    # 4. Persistence Pattern (Channel 1): Incremental step report to internal ASP.NET endpoint,
    # done *before* recording this step as Succeeded in the returned state. A run must never
    # report AwaitingApproval/Succeeded when its own AgentStep row was never actually persisted -
    # that's precisely the fabricated-success problem this pipeline exists to avoid. If the report
    # call fails, this run fails too, even if validation itself passed
    # (plans/02-contracts-and-agent-fixes.md §1.5 - Agent 4's "step-report failure is logged and
    # ignored" pattern, same fix as Agent 1's).
    report_failed = False
    if state.workflow_run_id:
        try:
            await report(
                workflow_run_id=UUID(str(state.workflow_run_id)),
                step_no=_STEP_NO,
                agent_role=_AGENT_ROLE,
                status="Succeeded" if is_valid else "Failed",
                started_at=started,
                input_data=input_data,
                output_data=validation,
                error_message=None if is_valid else explanation,
            )
            logger.info("Persisted Agent 4 step %s for workflow %s", _STEP_NO, state.workflow_run_id)
        except BackendClientError as exc:
            logger.error("Failed to report step %s for run %s: %s", _STEP_NO, state.workflow_run_id, exc)
            report_failed = True

    final_is_valid = is_valid and not report_failed
    final_status = "Failed" if report_failed else status
    final_explanation = (
        f"report_step_failed: could not persist Agent 4's step" if report_failed else explanation
    )

    # 5. Consolidated step record
    steps.append({
        "stepNo": _STEP_NO,
        "agentRole": _AGENT_ROLE,
        "status": "Succeeded" if final_is_valid else "Failed",
        "inputJson": input_data,
        "outputJson": validation,
        "errorMessage": None if final_is_valid else final_explanation,
        "startedAt": started.isoformat(),
        "completedAt": now().isoformat(),
    })

    return {
        "is_valid": final_is_valid,
        "validation_flags": validation_flags,
        "price_deviation_percent": price_deviation_percent,
        "validation_summary": validation_summary,
        "status": final_status,
        "proposal_email_subject": proposal_email_subject,
        "proposal_email_body": proposal_email_body,
        "validation": validation,
        "steps": steps,
        "failed": not final_is_valid,
        "failure_reason": None if final_is_valid else final_explanation,
    }
