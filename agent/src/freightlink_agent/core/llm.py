"""LLM access for the four agents - ADR-008 addendum: Gemini (free tier)
primary, Ollama fallback (covers the network-dependency risk of a hosted
API during a live demo). Every call uses structured output (a typed
Pydantic schema per call site), never free-form text parsing.
"""

import json
import logging
from functools import lru_cache
from typing import Any, TypeVar

from pydantic import BaseModel

from freightlink_agent.core.config import get_settings

logger = logging.getLogger(__name__)

T = TypeVar("T", bound=BaseModel)


class PlanOutput(BaseModel):
    objective: str
    steps: list[str]


class DomainAnalysisOutput(BaseModel):
    explanation: str
    """Plain-language explanation of why the top candidates were chosen and
    why others were excluded - layered on top of the deterministic
    eligibility filter, never a replacement for it."""


class MatchingPricingOutput(BaseModel):
    explanation: str
    """Justifies the #1 (highlighted) pick to the shipper, referencing the
    real ETA and price Agent 3 computed."""


class ValidationOutput(BaseModel):
    explanation: str
    recommendation: str


class AgentLLM:
    def __init__(self) -> None:
        settings = get_settings()
        self._settings = settings

    def _primary(self):
        from langchain_google_genai import ChatGoogleGenerativeAI

        return ChatGoogleGenerativeAI(
            model=self._settings.llm_model,
            google_api_key=self._settings.gemini_api_key or self._settings.llm_api_key,
        )

    def _fallback(self):
        from langchain_ollama import ChatOllama

        return ChatOllama(model=self._settings.ollama_model)

    async def _structured(self, schema: type[T], system_prompt: str, context: dict[str, Any]) -> T:
        messages = [
            ("system", system_prompt),
            ("human", json.dumps(context, default=str)),
        ]
        try:
            model = self._primary().with_structured_output(schema)
            return await model.ainvoke(messages)  # type: ignore[return-value]
        except Exception:
            logger.warning("Primary LLM (Gemini) failed, falling back to Ollama", exc_info=True)
            model = self._fallback().with_structured_output(schema)
            return await model.ainvoke(messages)  # type: ignore[return-value]

    async def plan(self, system_prompt: str, context: dict[str, Any]) -> dict:
        result = await self._structured(PlanOutput, system_prompt, context)
        return result.model_dump()

    async def explain_domain_analysis(self, system_prompt: str, context: dict[str, Any]) -> dict:
        result = await self._structured(DomainAnalysisOutput, system_prompt, context)
        return result.model_dump()

    async def explain_matching_pricing(self, system_prompt: str, context: dict[str, Any]) -> dict:
        result = await self._structured(MatchingPricingOutput, system_prompt, context)
        return result.model_dump()

    async def explain_validation(self, system_prompt: str, context: dict[str, Any]) -> dict:
        result = await self._structured(ValidationOutput, system_prompt, context)
        return result.model_dump()


@lru_cache
def get_llm() -> AgentLLM:
    return AgentLLM()
