from functools import lru_cache
from typing import Literal

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

    # LLM provider (ADR-008 addendum: Gemini free tier primary, Ollama fallback)
    llm_provider: Literal["gemini", "ollama"] = "gemini"
    gemini_api_key: str | None = None
    gemini_model: str = "gemini-2.5-flash"
    ollama_model: str = "llama3.2"
    ollama_base_url: str = "http://localhost:11434"

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
