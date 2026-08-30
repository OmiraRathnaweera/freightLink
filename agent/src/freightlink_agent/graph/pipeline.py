"""LangGraph wiring: planner -> domain_analysis -> matching_pricing ->
validation_safety, with one conditional branch (ADR-007) - it short-circuits
to a failure terminus if Agent 2 finds zero eligible candidates, or if every
routing call in Agent 3's shortlist fails. The shipper-approval fork itself
lives entirely in the backend/human layer, not in this graph - a run either
completes cleanly (Agent 4 reports Succeeded, which flips the backend's
AgentWorkflowRun.Status to AwaitingApproval) or short-circuits to a recorded
safe failure.
"""

from langgraph.graph import END, StateGraph

from freightlink_agent.agents import domain_analysis, matching_pricing, planner, validation_safety
from freightlink_agent.graph.state import PipelineState


def _route_after_planner(state: PipelineState) -> str:
    return "end" if state.get("failed") else "continue"


def _route_after_domain_analysis(state: PipelineState) -> str:
    return "end" if state.get("failed") else "continue"


def _route_after_matching_pricing(state: PipelineState) -> str:
    return "end" if state.get("failed") else "continue"


def build_graph():
    graph = StateGraph(PipelineState)

    graph.add_node("planner", planner.run)
    graph.add_node("domain_analysis", domain_analysis.run)
    graph.add_node("matching_pricing", matching_pricing.run)
    graph.add_node("validation_safety", validation_safety.run)

    graph.set_entry_point("planner")
    graph.add_conditional_edges(
        "planner",
        _route_after_planner,
        {"continue": "domain_analysis", "end": END},
    )

    graph.add_conditional_edges(
        "domain_analysis",
        _route_after_domain_analysis,
        {"continue": "matching_pricing", "end": END},
    )
    graph.add_conditional_edges(
        "matching_pricing",
        _route_after_matching_pricing,
        {"continue": "validation_safety", "end": END},
    )
    graph.add_edge("validation_safety", END)

    return graph.compile()


_compiled = None


def get_pipeline():
    global _compiled
    if _compiled is None:
        _compiled = build_graph()
    return _compiled
