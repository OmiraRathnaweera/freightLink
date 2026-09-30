"""Agent 1 - Planner/Coordinator. The only agent implemented in this
service so far.

Creates the AgentWorkflowRun row on the backend (getting back the real
workflow_run_id - never a locally generated placeholder), builds a plan
(objective + an ordered list drawn only from the fixed pipeline stages)
from the incoming load_context via a structured LLM call, and reports
its own step back to the backend. No tool calls, no agency/pricing logic
- that belongs to agents that don't exist yet.
"""

import json
import logging
from typing import Any

from freightlink_agent.core.backend_client import BackendClientError, create_workflow_run
from freightlink_agent.core.llm import get_llm
from freightlink_agent.graph.state import WorkflowState
from freightlink_agent.graph.step_reporter import now, report
from freightlink_agent.schemas.workflow import CreateWorkflowRunRequest

logger = logging.getLogger(__name__)

_STEP_NO = 1
_AGENT_ROLE = "Planner"

_SYSTEM_PROMPT = (
    "You are the Planner agent in a freight-matching pipeline. Given a load's "
    "context, produce three things: "
    "1) a one-sentence internal `objective` describing the pipeline's plan; "
    "2) a short, conversational `shipper_message` written directly to the shipper who "
    "posted this load, in first person (e.g. 'This load is 3,200kg, so it needs about "
    "11.5 m3 of space - a MediumLorry is the right fit. I'm now finding the most "
    "suitable agency for you.'). Ground it in the load's actual weight, volume, and the "
    "vehicle class those imply, and end by saying you're now finding a suitable agency. "
    "Never state a weight, volume, or other number you were not given in the load context; "
    "3) `steps`, selected in order, only from these fixed pipeline stages: 'Evaluate "
    "candidate agencies', 'Select agency via routing', 'Validate and get shipper "
    "approval', 'Notify agency'. Do not invent steps outside this list."
)


def _valid_lat(v: Any) -> bool:
    return isinstance(v, (int, float)) and -90.0 <= v <= 90.0


def _valid_lng(v: Any) -> bool:
    return isinstance(v, (int, float)) and -180.0 <= v <= 180.0


async def run(state: WorkflowState) -> dict:
    started = now()
    steps = list(state.steps)

    # Short-circuit check: Agent 1 malformed load payload -> stop, nothing delegated.
    # Checks weight, volume, and all four coordinates (presence + real-world range) -
    # a load ownership check already happens upstream in the backend (the Shipper who
    # triggered this run is verified against Load.ShipperUserId before Python is ever
    # called), so it isn't repeated here.
    ctx = state.load_context or {}
    weight = float(ctx.get("weightKg") or ctx.get("weight_kg") or 0.0)
    volume = float(ctx.get("volumeM3") or ctx.get("volume_m3") or 0.0)
    pickup_lat = ctx.get("pickupLat") or ctx.get("pickup_lat")
    pickup_lng = ctx.get("pickupLng") or ctx.get("pickup_lng")
    dropoff_lat = ctx.get("dropoffLat") or ctx.get("dropoff_lat")
    dropoff_lng = ctx.get("dropoffLng") or ctx.get("dropoff_lng")

    malformed = (
        weight <= 0
        or volume <= 0
        or not _valid_lat(pickup_lat)
        or not _valid_lng(pickup_lng)
        or not _valid_lat(dropoff_lat)
        or not _valid_lng(dropoff_lng)
    )
    if malformed:
        msg = "malformed_load_payload"
        logger.warning("Agent 1 short-circuit: %s", msg)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": {"loadId": str(state.load_id), "loadContext": ctx},
            "outputJson": None,
            "errorMessage": msg,
            "startedAt": started.isoformat(),
            "completedAt": now().isoformat(),
        })
        return {
            "failed": True,
            "failure_reason": msg,
            "steps": steps,
        }

    workflow_run_id = state.workflow_run_id
    if not workflow_run_id:
        try:
            created = await create_workflow_run(
                CreateWorkflowRunRequest(
                    load_id=state.load_id,
                    triggered_by_user_id=state.triggered_by_user_id,
                    attempt_no=state.attempt_no,
                )
            )
            workflow_run_id = created.workflow_run_id
        except BackendClientError as exc:
            # No local placeholder UUID: the backend is the only source of a real
            # workflow_run_id, and every subsequent step report needs a run that
            # actually exists on the backend to attach to. Fail visibly instead.
            msg = f"create_workflow_run_failed: {exc}"
            logger.error("Agent 1 could not create a real AgentWorkflowRun row: %s", msg)
            steps.append({
                "stepNo": _STEP_NO,
                "agentRole": _AGENT_ROLE,
                "status": "Failed",
                "inputJson": {"loadId": str(state.load_id), "loadContext": ctx},
                "outputJson": None,
                "errorMessage": msg,
                "startedAt": started.isoformat(),
                "completedAt": now().isoformat(),
            })
            return {
                "failed": True,
                "failure_reason": msg,
                "steps": steps,
            }

    input_data = {"loadId": str(state.load_id), "loadContext": state.load_context}

    try:
        llm = get_llm()
        plan = await llm.plan(system_prompt=_SYSTEM_PROMPT, load_context=state.load_context)
        objective = plan["objective"]
        plan_json_dict = plan
        # Provenance (plans/03-openai-migration.md §4): which provider/model actually
        # responded, so a Succeeded step is checkable evidence a real LLM call happened.
        # Kept out of plan_json_dict itself since that also becomes plan_json/plan in the
        # return value below, whose documented shape is exactly {objective, steps}.
        llm_provenance = getattr(llm, "last_call_meta", None)
    except Exception as exc:  # noqa: BLE001
        logger.exception("Planner LLM call failed for run %s", workflow_run_id)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": input_data,
            "outputJson": None,
            "errorMessage": f"Unhandled exception: {exc}",
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
                error_message=f"Unhandled exception: {exc}",
            )
        except BackendClientError:
            pass
        return {
            "workflow_run_id": workflow_run_id,
            "failed": True,
            "failure_reason": f"planner_llm_failed: {exc}",
            "steps": steps,
        }

    plan_json = json.dumps(plan_json_dict)
    step_output_data = {**plan_json_dict, "llmProvenance": llm_provenance}

    # Report before marking this step Succeeded in the returned state: a step that
    # claims to have happened but was never actually persisted to the backend is
    # exactly the "audit trail unreliable" problem this run must not produce - if the
    # report call fails, this run fails too, rather than silently continuing as if
    # Agent 1's step were safely recorded (plans/02-contracts-and-agent-fixes.md §1.5).
    try:
        await report(
            workflow_run_id=workflow_run_id,
            step_no=_STEP_NO,
            agent_role=_AGENT_ROLE,
            status="Succeeded",
            started_at=started,
            input_data=input_data,
            output_data=step_output_data,
        )
    except BackendClientError as exc:
        msg = f"report_step_failed: {exc}"
        logger.error("Agent 1 succeeded but could not persist its step: %s", msg)
        steps.append({
            "stepNo": _STEP_NO,
            "agentRole": _AGENT_ROLE,
            "status": "Failed",
            "inputJson": input_data,
            "outputJson": step_output_data,
            "errorMessage": msg,
            "startedAt": started.isoformat(),
            "completedAt": now().isoformat(),
        })
        return {
            "workflow_run_id": workflow_run_id,
            "failed": True,
            "failure_reason": msg,
            "steps": steps,
        }

    steps.append({
        "stepNo": _STEP_NO,
        "agentRole": _AGENT_ROLE,
        "status": "Succeeded",
        "inputJson": input_data,
        "outputJson": step_output_data,
        "startedAt": started.isoformat(),
        "completedAt": now().isoformat(),
    })

    return {
        "workflow_run_id": workflow_run_id,
        "objective": objective,
        "plan_json": plan_json,
        "plan": plan_json_dict,
        "steps": steps,
    }
