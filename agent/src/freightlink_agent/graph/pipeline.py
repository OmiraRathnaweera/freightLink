"""LangGraph wiring for the Agentic AI pipeline.

Only Agent 1 (Planner) is implemented in this service so far. Agents 2-4
(DomainAnalysis, MatchingPricing, ValidationSafety) do not exist yet -
nothing should be registered as a node on their behalf until each is
actually built; a stub node referencing an empty module is worse than no
node at all, since it fails at graph-build time instead of being an
honest gap.
"""

from langgraph.graph import END, StateGraph

from freightlink_agent.agents import planner, domain_analysis
from freightlink_agent.graph.state import WorkflowState


def build_graph():
    graph = StateGraph(WorkflowState)

    graph.add_node("planner", planner.run)
    graph.add_node("domain_analysis", domain_analysis.run)
    
    graph.set_entry_point("planner")
    
    # Simple sequential execution
    graph.add_edge("planner", "domain_analysis")
    graph.add_edge("domain_analysis", END)

    return graph.compile()


_compiled = None


def get_pipeline():
    global _compiled
    if _compiled is None:
        _compiled = build_graph()
    return _compiled
