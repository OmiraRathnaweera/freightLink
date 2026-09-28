from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


from pathlib import Path

_ENV_FILE = Path(__file__).resolve().parents[3] / ".env"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=str(_ENV_FILE) if _ENV_FILE.exists() else ".env",
        extra="ignore",
    )

    # This service's own bind info
    agent_api_base_url: str = "http://localhost:8001"
    agent_service_port: int = 8001
    log_level: str = "INFO"

    # LLM provider (ADR-008 addendum #2: OpenAI is the only LLM provider - supersedes the
    # earlier NVIDIA NIM -> Gemini history, see ADR-008 for the full story. No Gemini, no
    # Ollama, no other fallback provider remains anywhere in this service; if OpenAI is
    # unreachable, each caller falls back to its own deterministic template copy instead.)
    openai_api_key: str | None = None
    openai_model: str = "gpt-4o-mini"
    # Hard per-run cap on OpenAI calls, independent of any provider-side rate limit - a bug
    # that loops/retries the pipeline must not be able to run up an unbounded bill during
    # development (plans/03-openai-migration.md §5). Each agent's LLM call increments a
    # per-process counter in llm.py; exceeding this raises rather than calling the API again.
    openai_max_calls_per_process: int = 200

    # Routing tool (OpenRouteService, ADR-012)
    openrouteservice_api_key: str | None = None
    openrouteservice_base_url: str = "https://api.openrouteservice.org"

    # Inbound auth: checked on POST /workflows/run (X-Internal-Api-Key header)
    # No default — must come from .env; an empty-string fallback would make
    # the auth check silently accept an empty header.
    shared_secret: str = Field(alias="SHARED_SECRET")

    # Outbound: calls into the ASP.NET Core backend
    # (POST /internal/agent-workflow-runs, POST /internal/agent-workflow-runs/{id}/steps)
    backend_base_url: str = "http://localhost:5159"
    internal_api_key: str = Field(alias="INTERNAL_API_KEY")


@lru_cache
def get_settings() -> Settings:
    return Settings()
