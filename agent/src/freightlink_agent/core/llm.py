"""LLM access for Agent 1 (Planner) - the only agent implemented so far.

ADR-008 addendum: Gemini (free tier) primary, Ollama fallback (covers the
network-dependency risk of a hosted API during a live demo). LLM_PROVIDER
picks the primary provider; when it's Gemini, a failed call additionally
falls back to Ollama rather than propagating straight away. Always uses
structured output (a typed Pydantic schema), never free-form text parsing.
"""

import json
import logging
from functools import lru_cache
from typing import Any, Literal

from pydantic import BaseModel

from freightlink_agent.core.config import get_settings

logger = logging.getLogger(__name__)

# The pipeline is a fixed sequence (ADR-007); the Planner's job is to write
# an objective and select/order among these stages, never invent new ones.
PipelineStage = Literal[
    "Evaluate candidate agencies",
    "Select agency via routing",
    "Validate and get shipper approval",
    "Notify agency",
]


class PlanOutput(BaseModel):
    objective: str
    steps: list[PipelineStage]


class SelectionJustificationOutput(BaseModel):
    headline: str
    detailed_reasoning: str


class AgentLLM:
    def __init__(self) -> None:
        self._settings = get_settings()

    def _gemini(self):
        from langchain_google_genai import ChatGoogleGenerativeAI

        return ChatGoogleGenerativeAI(
            model=self._settings.gemini_model,
            google_api_key=self._settings.gemini_api_key,
        )

    def _ollama(self):
        from langchain_ollama import ChatOllama

        return ChatOllama(model=self._settings.ollama_model, base_url=self._settings.ollama_base_url)

    async def plan(self, system_prompt: str, load_context: dict[str, Any]) -> dict:
        messages = [
            ("system", system_prompt),
            ("human", json.dumps(load_context, default=str)),
        ]

        if self._settings.llm_provider == "ollama":
            model = self._ollama().with_structured_output(PlanOutput)
            result: PlanOutput = await model.ainvoke(messages)  # type: ignore[assignment]
            return result.model_dump()

        try:
            model = self._gemini().with_structured_output(PlanOutput)
            result = await model.ainvoke(messages)  # type: ignore[assignment]
        except Exception:
            logger.warning("Primary LLM (Gemini) failed, falling back to Ollama", exc_info=True)
            model = self._ollama().with_structured_output(PlanOutput)
            result = await model.ainvoke(messages)  # type: ignore[assignment]

        return result.model_dump()

    async def justify_selection(self, system_prompt: str, context: dict[str, Any]) -> dict:
        messages = [
            ("system", system_prompt),
            ("human", json.dumps(context, default=str)),
        ]

        if self._settings.llm_provider == "ollama":
            model = self._ollama().with_structured_output(SelectionJustificationOutput)
            result: SelectionJustificationOutput = await model.ainvoke(messages)  # type: ignore[assignment]
            return result.model_dump()

        try:
            model = self._gemini().with_structured_output(SelectionJustificationOutput)
            result = await model.ainvoke(messages)  # type: ignore[assignment]
        except Exception:
            logger.warning("Primary LLM (Gemini) failed for selection justification, falling back to Ollama", exc_info=True)
            model = self._ollama().with_structured_output(SelectionJustificationOutput)
            result = await model.ainvoke(messages)  # type: ignore[assignment]

        return result.model_dump()



@lru_cache
def get_llm() -> AgentLLM:
    return AgentLLM()
