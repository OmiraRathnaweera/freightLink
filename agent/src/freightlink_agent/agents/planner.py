"""Agent 1 - Planner/Coordinator.

Builds the Objective/PlanJson from the incoming Load + eligible-agency
count. No tool calls. Structured output only (with_structured_output-style
LLM call), not a wrapper around fixed logic - but the plan's *shape* is
fixed (this is a sequential pipeline, ADR-007), so the LLM's job here is a
genuine planning/summarization task, not routing.
"""

import json

from freightlink_agent.core.llm import get_llm
from freightlink_agent.graph.state import PipelineState
from freightlink_agent.graph.step_reporter import now, report, reported_step

_STEP_NO = 1
_AGENT_ROLE = "Planner"

_SYSTEM_PROMPT = (
    "You are the Planner agent in a freight-matching pipeline. Given a load's "
    "details and the number of eligible candidate agencies already assembled, "
    "write a one-sentence objective and a short ordered list of the steps the "
    "downstream agents will take. Be concise and factual - do not invent data "
    "you were not given."
)


@reported_step(_STEP_NO, _AGENT_ROLE)
async def run(state: PipelineState) -> dict:
    started = now()
    request = state["request"]
    load = request.load

    if request.resume_from_step_no is not None and request.prior_plan_json is not None:
        # Shipper-requested revise (/workflows/{id}/revise): replay Agent 1's
        # prior output rather than re-planning from scratch.
        await report(
            run_id=state["run_id"],
            step_no=_STEP_NO,
            agent_role=_AGENT_ROLE,
            status="Succeeded",
            started_at=started,
            input_data={"resumed": True, "loadId": str(load.load_id)},
            output_data={"note": "Replayed from prior run (revise)"},
        )
        return {"objective": "", "plan_json": request.prior_plan_json}

    input_data = {
        "loadId": str(load.load_id),
        "weightKg": str(load.weight_kg),
        "volumeM3": str(load.volume_m3),
        "eligibleAgencyCount": len(request.eligible_agencies),
        "preselectedVehicleClass": request.preselected_vehicle_class,
    }

    llm = get_llm()
    plan = await llm.plan(
        system_prompt=_SYSTEM_PROMPT,
        context=input_data,
    )
    objective = plan["objective"]
    plan_json = json.dumps(plan)

    await report(
        run_id=state["run_id"],
        step_no=_STEP_NO,
        agent_role=_AGENT_ROLE,
        status="Succeeded",
        started_at=started,
        input_data=input_data,
        output_data=plan,
    )

    return {"objective": objective, "plan_json": plan_json}
