import os
import sys
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

os.environ.setdefault("SHARED_SECRET", "test-shared-secret")
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-api-key")

from freightlink_agent.core.llm import AgentLLM, PlanOutput, ToolCallingSelectionOutput

_MESSAGES = [("system", "prompt"), ("human", "{}")]


def _settings(**overrides) -> MagicMock:
    defaults = dict(
        openai_model="gpt-4o-mini",
        openai_api_key="sk-test",
        openai_max_calls_per_process=200,
    )
    defaults.update(overrides)
    return MagicMock(**defaults)


def _mock_chat_model(result):
    structured = MagicMock()
    structured.ainvoke = AsyncMock(return_value=result)
    chat_model = MagicMock()
    chat_model.with_structured_output = MagicMock(return_value=structured)
    return chat_model


@pytest.mark.anyio
async def test_openai_success_records_provenance():
    plan_result = PlanOutput(objective="Test objective", shipper_message="Hi shipper", steps=["Evaluate candidate agencies"])

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings()):
        llm = AgentLLM()
        with patch.object(llm, "_openai", return_value=_mock_chat_model(plan_result)) as mock_openai:
            result = await llm.plan(system_prompt="p", load_context={})

    mock_openai.assert_called_once()
    assert result["objective"] == "Test objective"
    assert llm.last_call_meta == {"provider": "openai", "model": "gpt-4o-mini", "usedFallback": False}


@pytest.mark.anyio
async def test_openai_failure_uses_deterministic_fallback_not_another_provider():
    """No Gemini, no Ollama, no other LLM provider - OpenAI is the only one this service
    calls. If it fails, the caller's own deterministic template copy is the only fallback."""
    failing_openai = MagicMock()
    failing_openai.with_structured_output = MagicMock(
        return_value=MagicMock(ainvoke=AsyncMock(side_effect=RuntimeError("openai down")))
    )

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings()):
        llm = AgentLLM()
        with patch.object(llm, "_openai", return_value=failing_openai) as mock_openai:
            result = await llm.plan(system_prompt="p", load_context={})

    mock_openai.assert_called_once()
    # Deterministic rule-based plan, not a crash and not a second provider attempt
    assert result["objective"]
    assert isinstance(result["steps"], list)
    assert llm.last_call_meta == {"provider": "deterministic_fallback", "model": None, "usedFallback": True}


def test_agent_llm_has_no_gemini_or_ollama_provider_methods():
    """Regression guard: Gemini and Ollama support must not silently creep back in."""
    llm_instance_methods = dir(AgentLLM)
    assert not any("gemini" in name.lower() for name in llm_instance_methods)
    assert not any("ollama" in name.lower() for name in llm_instance_methods)


@pytest.mark.anyio
async def test_openai_call_cap_enforced_falls_back_without_calling_openai_again():
    """Cost guard (plans/03-openai-migration.md §5): once the per-process cap is hit, no
    further OpenAI calls are attempted - subsequent calls go straight to the deterministic
    fallback, not to any other LLM provider (there isn't one)."""
    plan_result = PlanOutput(objective="First call objective", shipper_message="Hi shipper", steps=["Notify agency"])

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings(openai_max_calls_per_process=1)):
        llm = AgentLLM()
        with patch.object(llm, "_openai", return_value=_mock_chat_model(plan_result)) as mock_openai:
            first = await llm.plan(system_prompt="p", load_context={})  # consumes the single allowed call
            second = await llm.plan(system_prompt="p", load_context={})  # cap now reached

    assert mock_openai.call_count == 1
    assert first["objective"] == "First call objective"
    # Second call never reached OpenAI - straight to the deterministic fallback
    assert second["objective"]
    assert llm.last_call_meta == {"provider": "deterministic_fallback", "model": None, "usedFallback": True}


@pytest.mark.anyio
async def test_tool_calling_loop_is_bounded_never_hangs():
    """Agent 3's tool-calling loop (run_tool_calling_selection) must stop at
    max_iterations even against a model stand-in that always requests another tool call -
    it must never hang, and must never exceed the OpenAI cost cap."""
    from langchain_core.messages import AIMessage
    from langchain_core.tools import tool

    call_count = {"n": 0}

    @tool
    async def noop_tool(value: str) -> str:
        """A no-op test tool."""
        call_count["n"] += 1
        return f"ok: {value}"

    tool_calling_model = MagicMock()
    tool_calling_model.ainvoke = AsyncMock(
        return_value=AIMessage(
            content="",
            tool_calls=[{"name": "noop_tool", "args": {"value": "x"}, "id": "call-1", "type": "tool_call"}],
        )
    )

    final_decision = ToolCallingSelectionOutput(
        selected_candidate_agency_id="a1",
        selected_positioning_tool_call_id="p1",
        selected_cargo_tool_call_id="c1",
        selected_pricing_tool_call_id="pr1",
        headline="h",
        detailed_reasoning="d",
    )
    structured_model = MagicMock()
    structured_model.ainvoke = AsyncMock(return_value=final_decision)

    chat_model = MagicMock()
    chat_model.bind_tools = MagicMock(return_value=tool_calling_model)
    chat_model.with_structured_output = MagicMock(return_value=structured_model)

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings(openai_max_calls_per_process=100)):
        llm = AgentLLM()
        with patch.object(llm, "_openai", return_value=chat_model):
            result = await llm.run_tool_calling_selection(
                system_prompt="p",
                context={},
                tools=[noop_tool],
                max_iterations=3,
                max_tool_calls=10,
            )

    assert result == final_decision
    # 3 bounded tool-calling turns + 1 final decision turn, never unbounded
    assert tool_calling_model.ainvoke.await_count == 3
    assert call_count["n"] == 3
    structured_model.ainvoke.assert_awaited_once()


@pytest.mark.anyio
async def test_tool_call_budget_stops_further_tool_execution():
    """Once max_tool_calls is hit mid-loop, further requested tool calls must not execute -
    the model is just told the budget is exhausted and asked to decide."""
    from langchain_core.messages import AIMessage
    from langchain_core.tools import tool

    call_count = {"n": 0}

    @tool
    async def noop_tool(value: str) -> str:
        """A no-op test tool."""
        call_count["n"] += 1
        return f"ok: {value}"

    # A single turn requesting 5 tool calls at once, budget is only 2
    tool_calling_model = MagicMock()
    tool_calling_model.ainvoke = AsyncMock(
        return_value=AIMessage(
            content="",
            tool_calls=[
                {"name": "noop_tool", "args": {"value": str(i)}, "id": f"call-{i}", "type": "tool_call"}
                for i in range(5)
            ],
        )
    )

    final_decision = ToolCallingSelectionOutput(
        selected_candidate_agency_id="a1",
        selected_positioning_tool_call_id="p1",
        selected_cargo_tool_call_id="c1",
        selected_pricing_tool_call_id="pr1",
        headline="h",
        detailed_reasoning="d",
    )
    structured_model = MagicMock()
    structured_model.ainvoke = AsyncMock(return_value=final_decision)

    chat_model = MagicMock()
    chat_model.bind_tools = MagicMock(return_value=tool_calling_model)
    chat_model.with_structured_output = MagicMock(return_value=structured_model)

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings(openai_max_calls_per_process=100)):
        llm = AgentLLM()
        with patch.object(llm, "_openai", return_value=chat_model):
            await llm.run_tool_calling_selection(
                system_prompt="p",
                context={},
                tools=[noop_tool],
                max_iterations=3,
                max_tool_calls=2,
            )

    # Only the first 2 of the 5 requested tool calls actually executed
    assert call_count["n"] == 2
