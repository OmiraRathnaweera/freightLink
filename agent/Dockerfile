FROM ghcr.io/astral-sh/uv:0.12.1 AS uv

FROM python:3.12-slim AS runtime
COPY --from=uv /uv /uvx /bin/
WORKDIR /app
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    UV_NO_DEV=1 \
    PATH="/app/.venv/bin:$PATH"

COPY pyproject.toml uv.lock README.md ./
RUN uv sync --frozen --no-install-project --no-dev
COPY src ./src
RUN uv sync --frozen --no-dev && useradd --system --uid 10001 --create-home agent && chown -R agent:agent /app

USER agent
EXPOSE 8001
CMD ["uvicorn", "freightlink_agent.main:app", "--host", "0.0.0.0", "--port", "8001"]
