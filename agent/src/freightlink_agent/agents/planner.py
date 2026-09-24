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
    "context, write a one-sentence objective and select, in order, only from "
    "these fixed pipeline stages: 'Evaluate candidate agencies', 'Select agency "
    "via routing', 'Validate and get shipper approval', 'Notify agency'. Do not "
    "invent steps outside this list, and do not invent data you were not given."
)


async def run(state: WorkflowState) -> dict:
    started = now()

    try:
        created = await create_workflow_run(
            CreateWorkflowRunRequest(
                load_id=state.load_id,
                triggered_by_user_id=state.triggered_by_user_id,
                attempt_no=state.attempt_no,
            )
        )
    except BackendClientError as exc:
        logger.exception("Could not create AgentWorkflowRun for load %s", state.load_id)
        return {"failed": True, "failure_reason": f"create_workflow_run_failed: {exc}"}

    workflow_run_id = created.workflow_run_id
    input_data = {"loadId": str(state.load_id), "loadContext": state.load_context}

    try:
        llm = get_llm()
        plan = await llm.plan(system_prompt=_SYSTEM_PROMPT, load_context=state.load_context)
        objective = plan["objective"]
        plan_json_dict = plan
    except Exception as exc:  # noqa: BLE001 - last-resort, the run is otherwise unobserved
        logger.exception("Planner LLM call failed for run %s", workflow_run_id)
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
            logger.exception("Also failed to report step %s failure for run %s", _STEP_NO, workflow_run_id)
        return {
            "workflow_run_id": workflow_run_id,
            "failed": True,
            "failure_reason": f"planner_llm_failed: {exc}",
        }

    plan_json = json.dumps(plan_json_dict)

    try:
        await report(
            workflow_run_id=workflow_run_id,
            step_no=_STEP_NO,
            agent_role=_AGENT_ROLE,
            status="Succeeded",
            started_at=started,
            input_data=input_data,
            output_data=plan_json_dict,
        )
    except BackendClientError as exc:
        logger.exception("Could not report step %s success for run %s", _STEP_NO, workflow_run_id)
        return {
            "workflow_run_id": workflow_run_id,
            "objective": objective,
            "plan_json": plan_json,
            "failed": True,
            "failure_reason": f"report_step_failed: {exc}",
        }

    return {
        "workflow_run_id": workflow_run_id,
        "objective": objective,
        "plan_json": plan_json,
    }
