import os
import sys
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

os.environ.setdefault("SHARED_SECRET", "test-shared-secret")
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-api-key")

from freightlink_agent.core.llm import AgentLLM, PlanOutput

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
    plan_result = PlanOutput(objective="Test objective", steps=["Evaluate candidate agencies"])

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
    plan_result = PlanOutput(objective="First call objective", steps=["Notify agency"])

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
