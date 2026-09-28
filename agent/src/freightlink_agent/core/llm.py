"""LLM access shared by Agents 1, 3, and 4.

ADR-008 addendum #2: OpenAI (gpt-4o-mini by default) is the only LLM provider this
service calls - no Gemini, no Ollama, no other fallback provider. Always uses structured
output (a typed Pydantic schema), never free-form text parsing. If OpenAI is unreachable
or the per-process call cap is hit, each caller falls back to its own deterministic,
template-based copy (see plan()/justify_selection()/generate_validation_summary_and_proposal()
below) rather than trying a second LLM provider.

Every call records which provider and model actually responded, and whether the
deterministic fallback was used, on `self.last_call_meta` - callers read this immediately
after awaiting a call and attach it to their own AgentStep's outputJson, so a "Succeeded"
step carries real, checkable proof a model call happened (plans/03-openai-migration.md
§4). This is an attribute rather than a return-value tuple specifically so it doesn't
change the existing call signatures every agent (and every existing test's mock) already
depends on.

Agent 2 (DomainAnalysis) does not call the LLM - its output is a
deterministic eligibility filter (see agents/domain_analysis.py).
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
    shipper_message: str
    steps: list[PipelineStage]


class SelectionJustificationOutput(BaseModel):
    headline: str
    detailed_reasoning: str


class ValidationLLMOutput(BaseModel):
    validation_summary: str
    proposal_email_subject: str
    proposal_email_body: str


class AgentLLM:
    def __init__(self) -> None:
        self._settings = get_settings()
        self.last_call_meta: dict[str, Any] = {}
        self._openai_call_count = 0

    def _openai(self, model_name: str | None = None):
        from langchain_openai import ChatOpenAI

        return ChatOpenAI(
            model=model_name or self._settings.openai_model,
            api_key=self._settings.openai_api_key,
            timeout=15.0,
            max_retries=1,
        )

    def _record_meta(self, provider: str, model: str | None, *, used_fallback: bool) -> None:
        self.last_call_meta = {"provider": provider, "model": model, "usedFallback": used_fallback}

    async def _invoke_structured(self, schema: type[BaseModel], messages: list[tuple[str, str]], label: str):
        """Calls OpenAI - the only LLM provider this service has. Raises if the
        per-process call cap is already hit, or if the OpenAI call itself fails; callers
        catch this and fall back to their own deterministic, template-based copy, and are
        responsible for recording the "deterministic_fallback" provenance themselves,
        since only they know that fallback's shape.
        """
        if self._openai_call_count >= self._settings.openai_max_calls_per_process:
            raise RuntimeError(
                f"OpenAI per-process call cap ({self._settings.openai_max_calls_per_process}) "
                f"reached; refusing further calls for {label}"
            )

        self._openai_call_count += 1
        model = self._openai().with_structured_output(schema)
        result = await model.ainvoke(messages)
        self._record_meta("openai", self._settings.openai_model, used_fallback=False)
        return result

    async def plan(self, system_prompt: str, load_context: dict[str, Any]) -> dict:
        messages = [
            ("system", system_prompt),
            ("human", json.dumps(load_context, default=str)),
        ]
        try:
            result: PlanOutput = await self._invoke_structured(PlanOutput, messages, "plan")
            return result.model_dump()
        except Exception:
            logger.warning("OpenAI call failed for plan, using deterministic rule-based plan", exc_info=True)
            self._record_meta("deterministic_fallback", None, used_fallback=True)
            return {
                "objective": "Evaluate candidate agencies, select optimal carrier via routing, validate safety compliance, and confirm dispatch.",
                "shipper_message": (
                    "I'm reviewing your load's weight and volume to pick a suitable vehicle class, "
                    "then finding the most suitable agency for you."
                ),
                "steps": [
                    "Evaluate candidate agencies",
                    "Select agency via routing",
                    "Validate and get shipper approval",
                    "Notify agency",
                ],
            }

    async def justify_selection(self, system_prompt: str, context: dict[str, Any]) -> dict:
        messages = [
            ("system", system_prompt),
            ("human", json.dumps(context, default=str)),
        ]
        try:
            result: SelectionJustificationOutput = await self._invoke_structured(
                SelectionJustificationOutput, messages, "justify_selection"
            )
            return result.model_dump()
        except Exception:
            logger.warning("OpenAI call failed for justify_selection, using deterministic explanation", exc_info=True)
            self._record_meta("deterministic_fallback", None, used_fallback=True)
            selected_agency = context.get("selectedAgency") if isinstance(context.get("selectedAgency"), dict) else {}
            agency_name = selected_agency.get("name") or context.get("agency_name") or context.get("agencyName") or "Recommended Carrier"
            yard_address = selected_agency.get("yardAddress") or "Verified Yard"
            assigned_vehicle = selected_agency.get("assignedVehicle") or {}
            assigned_driver = selected_agency.get("assignedDriver") or {}
            reg_no = assigned_vehicle.get("registrationNo") or assigned_vehicle.get("registration_no") or "Assigned Fleet Vehicle"
            driver_name = assigned_driver.get("name") or "Assigned Licensed Driver"
            eta = selected_agency.get("etaMinutes") or context.get("eta_minutes") or context.get("etaMinutes") or "optimal"
            dist = selected_agency.get("positioningDistanceKm") or "direct"
            return {
                "headline": f"Recommended Carrier: {agency_name} (Yard: {yard_address})",
                "detailed_reasoning": (
                    f"Selected {agency_name} based on real database fleet availability: assigned vehicle {reg_no} and driver {driver_name}. "
                    f"This carrier provides the fastest positioning ETA ({eta} mins, {dist} km) to pickup, full payload capability, and verified safety compliance."
                ),
            }

    async def generate_validation_summary_and_proposal(
        self,
        system_prompt: str,
        context: dict[str, Any],
    ) -> dict[str, str]:
        """Executes LLM reasoning for Agent 4:
        1. Explains validation findings in plain language for the Shipper.
        2. Drafts personalized Job Proposal email copy offering the job at the fixed price
           (ADR-017: No agency bidding or negotiation).
        """
        messages = [
            ("system", system_prompt),
            ("human", json.dumps(context, default=str)),
        ]
        try:
            result: ValidationLLMOutput = await self._invoke_structured(
                ValidationLLMOutput, messages, "generate_validation_summary_and_proposal"
            )
            return result.model_dump()
        except Exception:
            logger.warning("OpenAI call failed for validation proposal, using template fallback", exc_info=True)
            self._record_meta("deterministic_fallback", None, used_fallback=True)
            return self._fallback_validation_copy(context)

    def _fallback_validation_copy(self, context: dict[str, Any]) -> dict[str, str]:
        """Deterministic copy fallback used when all LLM providers are unreachable."""
        agency = context.get("selected_agency_name") or "Selected Agency"
        price = context.get("proposed_price") or 0.0
        vehicle = context.get("suggested_vehicle_class") or "Standard Freight"
        eta = context.get("eta_minutes") or 0
        deviation = context.get("price_deviation_percent")
        is_valid = context.get("is_valid", True)
        flags = context.get("validation_flags", [])

        load = context.get("load_context", {})
        pickup = load.get("pickup", "Origin")
        dropoff = load.get("dropoff", "Destination")
        cargo = load.get("cargo_description", "General Freight")
        weight = load.get("weight_kg") or load.get("weightKg") or "N/A"

        if is_valid:
            budget_note = ""
            if deviation is not None:
                if deviation > 0:
                    budget_note = f" Note: The price is {deviation:+.1f}% compared to your target budget."
                elif deviation < 0:
                    budget_note = f" Great news: The price is {abs(deviation):.1f}% under your target budget."
                else:
                    budget_note = " The price perfectly matches your target budget."

            summary = (
                f"Validation checks passed successfully. We have matched your shipment with {agency} "
                f"using a {vehicle} vehicle (Estimated transit: {eta} mins). The agreed fixed price is "
                f"LKR {price:,.2f}.{budget_note} This match is now held in 'AwaitingApproval' pending "
                f"your authorization. Once approved, the official Job Proposal will be dispatched to the agency."
            )
        else:
            summary = (
                f"Validation encountered safety or platform issues: {', '.join(flags)}. "
                f"The proposed match with {agency} at LKR {price:,.2f} cannot proceed until these issues are resolved."
            )

        email_subject = f"FreightLink Job Proposal: {pickup} to {dropoff} ({vehicle})"
        email_body = (
            f"Dear {agency} Dispatch Team,\n\n"
            f"FreightLink is pleased to offer you a confirmed freight dispatch opportunity:\n\n"
            f"• Route: {pickup} -> {dropoff}\n"
            f"• Cargo: {cargo} ({weight} kg)\n"
            f"• Required Vehicle: {vehicle}\n"
            f"• Estimated Transit Time: {eta} minutes\n"
            f"• Fixed Compensation: LKR {price:,.2f}\n\n"
            f"Please note that this is a direct Job Proposal with fixed compensation pre-approved by the shipper. "
            f"No bidding or rate negotiation is required. To accept this job and confirm vehicle assignment, "
            f"please log into your FreightLink Agency Portal.\n\n"
            f"Best regards,\nFreightLink Automated Logistics Dispatch"
        )

        return {
            "validation_summary": summary,
            "proposal_email_subject": email_subject,
            "proposal_email_body": email_body,
        }


@lru_cache
def get_llm() -> AgentLLM:
    return AgentLLM()
