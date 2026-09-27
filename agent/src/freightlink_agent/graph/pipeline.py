"""LangGraph wiring for the FreightLink Agentic AI pipeline.

Wires Agents 1 -> 2 -> 3 -> 4 into a single state graph with safe-failure short-circuits:
- Agent 1 (Planner): Malformed load payload -> stop, nothing delegated.
- Agent 2 (DomainAnalysis): Zero eligible agencies -> stop, do not run Agent 3/4.
- Agent 3 (MatchingPricing): All 5 routing lookups fail or pricing missing -> stop before Agent 4.
- Agent 4 (ValidationSafety): Evaluates safety, compliance, pricing bounds, and vehicle capacity.

P0 consolidation note (see plans/01-python-service-consolidation.md): this file previously
had two spliced graph definitions - a 2-node planner-only stub and this 4-node graph. The
4-node graph is the only one kept, since it's the one every other agent module and the
existing test suite (tests/test_pipeline_integration.py) already targets.
"""

from langgraph.graph import END, StateGraph

from freightlink_agent.agents import domain_analysis, matching_pricing, planner, validation_safety
from freightlink_agent.graph.state import WorkflowState


def _check_planner_short_circuit(state: WorkflowState) -> str:
    if state.failed:
        return END
    return "domain_analysis"


def _check_domain_analysis_short_circuit(state: WorkflowState) -> str:
    if state.failed:
        return END
    return "matching_pricing"


def _check_matching_pricing_short_circuit(state: WorkflowState) -> str:
    if state.failed:
        return END
    return "validation_safety"


def build_graph():
    graph = StateGraph(WorkflowState)

    graph.add_node("planner", planner.run)
    graph.add_node("domain_analysis", domain_analysis.run)
    graph.add_node("matching_pricing", matching_pricing.run)
    graph.add_node("validation_safety", validation_safety.run)

    graph.set_entry_point("planner")

    graph.add_conditional_edges(
        "planner",
        _check_planner_short_circuit,
        {"domain_analysis": "domain_analysis", END: END},
    )
    graph.add_conditional_edges(
        "domain_analysis",
        _check_domain_analysis_short_circuit,
        {"matching_pricing": "matching_pricing", END: END},
    )
    graph.add_conditional_edges(
        "matching_pricing",
        _check_matching_pricing_short_circuit,
        {"validation_safety": "validation_safety", END: END},
    )
    graph.add_edge("validation_safety", END)

    return graph.compile()


_compiled = None


def get_pipeline():
    global _compiled
    if _compiled is None:
        _compiled = build_graph()
    return _compiled
