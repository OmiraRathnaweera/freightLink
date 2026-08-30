from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # This service's own bind info
    agent_api_base_url: str = "http://localhost:8001"
    agent_service_port: int = 8001
    log_level: str = "INFO"

    # LLM provider (ADR-008 addendum: Gemini free tier primary, Ollama fallback)
    gemini_api_key: str | None = None
    llm_api_key: str | None = None
    llm_model: str = "gemini-2.5-flash"
    ollama_model: str = "llama3.2"

    # Inbound auth: checked on POST /workflows/start (X-Internal-Api-Key header)
    # No default — must come from .env; an empty-string fallback would make
    # the auth check silently accept an empty header.
    shared_secret: str = Field(alias="SHARED_SECRET")

    # Outbound: calls into the ASP.NET Core backend
    # (POST /internal/pricing/estimate, POST /internal/workflows/{runId}/steps)
    backend_base_url: str = "http://localhost:5159"
    backend_internal_api_key: str

    # Outbound: direct OpenRouteService calls (get_route_and_eta tool)
    openrouteservice_api_key: str
    openrouteservice_base_url: str = "https://api.openrouteservice.org"


@lru_cache
def get_settings() -> Settings:
    return Settings()
