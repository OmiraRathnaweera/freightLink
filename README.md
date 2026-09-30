# FreightLink

The API, PostgreSQL database, Python agent, and Caddy run together with a single Docker Compose command. The React frontend is deployed separately on Vercel. Caddy provides HTTPS for the public API; the database and agent have no published ports.

## Configuration

Everything is configured through one file. Copy [`.env.example`](.env.example) to `.env` in the repository root and edit it; Docker Compose reads it automatically. The stack needs `POSTGRES_PASSWORD`, `JWT_KEY` (at least 32 UTF-8 bytes), `INTERNAL_API_KEY`, `AGENT_SHARED_SECRET`, `ADMIN_USER_EMAIL`, and `ADMIN_USER_PASSWORD`; Compose refuses to start with a clear message if any is missing. A fresh database needs the seeded administrator for its pricing records. Generate the PostgreSQL password from hexadecimal characters so it can be inserted safely into the Npgsql connection string. Compose maps the two internal keys to both services; you do not need to configure them twice. Never commit `.env` or put credentials in application images.

Optional OpenAI and OpenRouteService keys enable live model and routing calls. The agent retains its deterministic fallbacks when those keys are absent. Cloudinary credentials (`CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET`) are required for file uploads (compliance documents, cargo photos, trip evidence, payment proofs): the API has no local-storage fallback, and every upload returns HTTP 500 while they are unset. Email and required email verification are disabled by default so local accounts can sign in. For production email verification, enable both, supply the SMTP values in `.env.example`, and set `FRONTEND_BASE_URL` to the Vercel URL.

## Deploy (one command)

The database, Python agent, API and Caddy (HTTPS) start together. Startup order is enforced by Compose: `db` and `agent` become healthy, the one-shot `migrate` service applies all EF Core migrations and exits, then `api` starts and must pass its `/health` check before `caddy` starts. If a migration fails, the API is not started.

```sh
cp .env.example .env    # then edit .env
docker compose up -d --build
docker compose ps
```

Repeat `docker compose up -d --build` for every later deployment; migrations run again automatically (already-applied ones are skipped). Check migration output with `docker compose logs migrate`.

The API is also on `http://localhost:5159` (loopback only) and allows the frontend origins in `CORS_ORIGINS`. The agent and database are reachable only on their Docker networks. To inspect PostgreSQL, use `docker compose exec db psql -U freightlink -d freightlink` (adjust the user/database if you changed their defaults). `docker compose down` stops the stack while retaining data. Removing Docker volumes deletes database records, local uploads, and Caddy certificates, so avoid `down --volumes` when you need that data.

For local development set `API_DOMAIN=localhost` (Caddy then uses a locally-signed certificate), or point a locally running frontend at `VITE_API_BASE_URL=http://localhost:5159/api/v1` and add its origin to `CORS_ORIGINS`. If ports 80/443 are busy, set `HTTP_PORT`/`HTTPS_PORT`.

## Azure VPS and Vercel

1. The VM's Azure DNS name (`freightlink.centralindia.cloudapp.azure.com`) is the API hostname; no extra DNS setup is needed. In the Azure network security group (and host firewall) allow inbound TCP 80 and 443 and, optionally, UDP 443 for HTTP/3. Port 80 must stay open because Caddy uses it to obtain and renew the certificate. Only Caddy needs public ports; port 5159 is bound to VM loopback.
2. In `.env`, set the required secrets plus `API_DOMAIN` to that hostname, and `CORS_ORIGINS` and `FRONTEND_BASE_URL` to the exact HTTPS Vercel frontend origin. Caddy acquires and renews the TLS certificate and redirects public HTTP to HTTPS.
3. Run `docker compose up -d --build`, then `curl https://freightlink.centralindia.cloudapp.azure.com/health` with your actual hostname.
4. In Vercel, set `VITE_API_BASE_URL=https://freightlink.centralindia.cloudapp.azure.com/api/v1` for the frontend project and redeploy it. Include the `/api/v1` suffix. If using a Vercel custom domain, use its exact origin for `CORS_ORIGINS`; add additional origins as a comma-separated list only when needed.

Swagger remains enabled in the Compose deployment even with `ASPNETCORE_ENVIRONMENT=Production`. Share `https://freightlink.centralindia.cloudapp.azure.com/swagger` as the university documentation URL; the OpenAPI JSON is at `https://freightlink.centralindia.cloudapp.azure.com/swagger/v1/swagger.json`. Set `SWAGGER_ENABLED=false` only if that public documentation is no longer required.

For a PostgreSQL backup, run `docker compose exec -T db pg_dump -U freightlink -d freightlink -Fc > freightlink.dump` (adjust credentials if changed) and store the dump separately from the VPS. The named volumes `postgres_data`, `api_uploads`, `caddy_data`, and `caddy_config` survive container recreation. The API serves plain HTTP only inside Docker and on VPS loopback; Caddy is the public HTTPS entry point.
