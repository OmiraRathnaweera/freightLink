#!/usr/bin/env bash
# Assignment 2 - non-functional evidence runner (area F1 performance, area F2 security).
#
# Starts a THROWAWAY stack (disposable Postgres container + the API published from this repo),
# then runs the k6 load scripts and an authenticated OWASP ZAP API scan against it, saving all raw
# output under testing/evidence/{k6,zap}. It never reads or needs your real .env: the API is started
# from a temporary working directory (Program.cs only loads ".env" from the current directory) with
# explicit throwaway settings.
#
# Requirements: Docker, .NET 8 SDK, curl, python3.
# Usage:  testing/scripts/run-nfr.sh [k6|zap|all]      (default: all)
set -euo pipefail

WHAT="${1:-all}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
EVID="$ROOT/testing/evidence"
WORK="$(mktemp -d)"
PG_NAME="fl-nfr-pg"
PG_PORT=15432
API_PORT=5188
API_URL="http://localhost:${API_PORT}"
DOCKER_API_URL="http://host.docker.internal:${API_PORT}"   # how containers (k6, ZAP) reach the host
INTERNAL_KEY="nfr-internal-key-$(date +%s)"
API_PID=""

cleanup() {
  [[ -n "$API_PID" ]] && kill "$API_PID" 2>/dev/null || true
  docker rm -f "$PG_NAME" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

mkdir -p "$EVID/k6" "$EVID/zap"

echo "==> Starting disposable Postgres on :${PG_PORT}"
docker rm -f "$PG_NAME" >/dev/null 2>&1 || true
docker run -d --name "$PG_NAME" -e POSTGRES_PASSWORD=nfrpass -e POSTGRES_DB=freightlink_nfr \
  -p "${PG_PORT}:5432" postgres:16-alpine >/dev/null
until docker exec "$PG_NAME" pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done

echo "==> Publishing API"
dotnet publish "$ROOT/backend/FreightLink.Api.csproj" -c Release -o "$WORK/publish" >/dev/null
rm -f "$WORK/publish/appsettings.Development.json"   # never carry a local dev settings file into the run

echo "==> Starting API on ${API_URL}"
(
  cd "$WORK"   # no .env here, so only the settings below are used
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://0.0.0.0:${API_PORT}" \
  CONNECTIONSTRINGS__DEFAULTCONNECTION="Host=localhost;Port=${PG_PORT};Database=freightlink_nfr;Username=postgres;Password=nfrpass" \
  JWT__ISSUER=FreightLinkApi JWT__AUDIENCE=FreightLinkClient \
  JWT__KEY="nfr-throwaway-signing-key-that-is-long-enough-1234567890" \
  JWT__ACCESSTOKENMINUTES=60 JWT__REFRESHTOKENDAYS=1 \
  ADMIN_USER_EMAIL="nfr-admin@example.com" ADMIN_USER_PASSWORD='NfrAdm1n$ecret' \
  INTERNAL_API_KEY="$INTERNAL_KEY" \
  EMAIL__ENABLED=false EMAIL__REQUIREEMAILVERIFICATION=false \
  CORS_ORIGINS="http://localhost:5173" \
  exec dotnet "$WORK/publish/FreightLink.Api.dll" >"$WORK/api.log" 2>&1
) &
API_PID=$!
for _ in $(seq 1 60); do
  curl -fs "${API_URL}/health" >/dev/null 2>&1 && break
  sleep 1
done
curl -fs "${API_URL}/health" >/dev/null || { echo "API failed to start:"; tail -30 "$WORK/api.log"; exit 1; }
echo "    API healthy."

run_k6() {
  echo "==> k6: loads API (10/50/100 VUs)"
  docker run --rm -v "$ROOT/backend/PerformanceTests:/scripts:ro" -v "$EVID/k6:/out" \
    -e API_BASE_URL="$DOCKER_API_URL" grafana/k6 run \
    --summary-export=/out/loads-api-summary.json /scripts/loads-api.perf.js \
    2>&1 | tee "$EVID/k6/loads-api-console.log" || echo "    (k6 loads-api reported failed thresholds - see log)"

  echo "==> k6: agent workflow persistence latency"
  docker run --rm -v "$ROOT/backend/PerformanceTests:/scripts:ro" -v "$EVID/k6:/out" \
    -e API_BASE_URL="$DOCKER_API_URL" -e INTERNAL_API_KEY="$INTERNAL_KEY" grafana/k6 run \
    --summary-export=/out/agent-workflow-summary.json /scripts/agent-workflow-latency.perf.js \
    2>&1 | tee "$EVID/k6/agent-workflow-console.log" || echo "    (k6 agent-workflow reported failed thresholds - see log)"
}

run_zap() {
  echo "==> ZAP: registering a Shipper so the scan runs authenticated"
  EMAIL="zap-shipper-$(date +%s)@example.com"
  curl -fs -X POST "${API_URL}/api/v1/auth/register/shipper" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$EMAIL\",\"password\":\"Sup3r\$ecret1\",\"fullName\":\"ZAP Shipper\",\"companyName\":\"ZAP Co\",\"billingAddress\":\"1 Test Lane, Colombo\"}" >/dev/null
  TOKEN="$(curl -fs -X POST "${API_URL}/api/v1/auth/login" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$EMAIL\",\"password\":\"Sup3r\$ecret1\"}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["accessToken"])')"

  echo "==> ZAP: API scan from the OpenAPI document (authenticated as Shipper)"
  chmod 777 "$EVID/zap"
  docker run --rm -v "$EVID/zap:/zap/wrk:rw" -t ghcr.io/zaproxy/zaproxy:stable zap-api-scan.py \
    -t "${DOCKER_API_URL}/swagger/v1/swagger.json" -f openapi -O "$DOCKER_API_URL" \
    -r zap-report.html -J zap-report.json -w zap-report.md -T 10 -I \
    -z "-config replacer.full_list(0).description=auth -config replacer.full_list(0).enabled=true -config replacer.full_list(0).matchtype=REQ_HEADER -config replacer.full_list(0).matchstr=Authorization -config replacer.full_list(0).regex=false -config replacer.full_list(0).replacement='Bearer ${TOKEN}'" \
    2>&1 | tee "$EVID/zap/zap-console.log" || true
}

case "$WHAT" in
  k6)  run_k6 ;;
  zap) run_zap ;;
  all) run_k6; run_zap ;;
  *)   echo "usage: $0 [k6|zap|all]"; exit 2 ;;
esac
echo "==> Done. Evidence in $EVID/k6 and $EVID/zap"
