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
        llm_provider="openai",
        openai_model="gpt-4o-mini",
        openai_api_key="sk-test",
        openai_max_calls_per_process=200,
        gemini_model="gemini-2.5-flash",
        gemini_api_key="gm-test",
        ollama_model="llama3.2",
        ollama_base_url="http://localhost:11434",
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
async def test_openai_primary_success_records_provenance():
    plan_result = PlanOutput(objective="Test objective", steps=["Evaluate candidate agencies"])

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings()):
        llm = AgentLLM()
        with patch.object(llm, "_openai", return_value=_mock_chat_model(plan_result)) as mock_openai:
            result = await llm.plan(system_prompt="p", load_context={})

    mock_openai.assert_called_once()
    assert result["objective"] == "Test objective"
    assert llm.last_call_meta == {"provider": "openai", "model": "gpt-4o-mini", "usedFallback": False}


@pytest.mark.anyio
async def test_openai_failure_falls_back_to_ollama_and_records_fallback():
    plan_result = PlanOutput(objective="Fallback objective", steps=["Notify agency"])
    failing_openai = MagicMock()
    failing_openai.with_structured_output = MagicMock(
        return_value=MagicMock(ainvoke=AsyncMock(side_effect=RuntimeError("openai down")))
    )

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings()):
        llm = AgentLLM()
        with (
            patch.object(llm, "_openai", return_value=failing_openai),
            patch.object(llm, "_ollama", return_value=_mock_chat_model(plan_result)) as mock_ollama,
        ):
            result = await llm.plan(system_prompt="p", load_context={})

    mock_ollama.assert_called_once()
    assert result["objective"] == "Fallback objective"
    assert llm.last_call_meta == {"provider": "ollama", "model": "llama3.2", "usedFallback": True}


@pytest.mark.anyio
async def test_gemini_provider_selected_when_configured():
    plan_result = PlanOutput(objective="Gemini objective", steps=["Notify agency"])

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings(llm_provider="gemini")):
        llm = AgentLLM()
        with patch.object(llm, "_gemini", return_value=_mock_chat_model(plan_result)) as mock_gemini:
            result = await llm.plan(system_prompt="p", load_context={})

    mock_gemini.assert_called()
    assert result["objective"] == "Gemini objective"
    assert llm.last_call_meta["provider"] == "gemini"
    assert llm.last_call_meta["usedFallback"] is False


@pytest.mark.anyio
async def test_ollama_only_provider_never_calls_openai_or_gemini():
    plan_result = PlanOutput(objective="Ollama objective", steps=["Notify agency"])

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings(llm_provider="ollama")):
        llm = AgentLLM()
        with (
            patch.object(llm, "_ollama", return_value=_mock_chat_model(plan_result)) as mock_ollama,
            patch.object(llm, "_openai") as mock_openai,
            patch.object(llm, "_gemini") as mock_gemini,
        ):
            result = await llm.plan(system_prompt="p", load_context={})

    mock_ollama.assert_called_once()
    mock_openai.assert_not_called()
    mock_gemini.assert_not_called()
    assert result["objective"] == "Ollama objective"
    assert llm.last_call_meta == {"provider": "ollama", "model": "llama3.2", "usedFallback": False}


@pytest.mark.anyio
async def test_all_providers_fail_uses_deterministic_fallback_and_records_it():
    failing = MagicMock()
    failing.with_structured_output = MagicMock(
        return_value=MagicMock(ainvoke=AsyncMock(side_effect=RuntimeError("down")))
    )

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings()):
        llm = AgentLLM()
        with (
            patch.object(llm, "_openai", return_value=failing),
            patch.object(llm, "_ollama", return_value=failing),
        ):
            result = await llm.plan(system_prompt="p", load_context={})

    # Deterministic rule-based plan, not a crash
    assert result["objective"]
    assert isinstance(result["steps"], list)
    assert llm.last_call_meta == {"provider": "deterministic_fallback", "model": None, "usedFallback": True}


@pytest.mark.anyio
async def test_openai_call_cap_enforced_falls_back_without_calling_openai_again():
    """Cost guard (plans/03-openai-migration.md §5): once the per-process cap is hit, no
    further OpenAI calls are attempted - subsequent calls go straight to the fallback."""
    plan_result = PlanOutput(objective="Ollama objective", steps=["Notify agency"])

    with patch("freightlink_agent.core.llm.get_settings", return_value=_settings(openai_max_calls_per_process=1)):
        llm = AgentLLM()
        with (
            patch.object(llm, "_openai", return_value=_mock_chat_model(plan_result)) as mock_openai,
            patch.object(llm, "_ollama", return_value=_mock_chat_model(plan_result)) as mock_ollama,
        ):
            await llm.plan(system_prompt="p", load_context={})  # consumes the single allowed call
            await llm.plan(system_prompt="p", load_context={})  # cap now reached

    assert mock_openai.call_count == 1
    assert mock_ollama.call_count == 1
    assert llm.last_call_meta["provider"] == "ollama"
    assert llm.last_call_meta["usedFallback"] is True
