# FreightLink

The API, PostgreSQL database, and Python agent run together with Docker Compose. The React frontend is deployed separately on Vercel. For VPS deployment, Caddy provides HTTPS for the public API; the database and agent have no published ports.

## Configuration

Use [`.env.example`](.env.example) as the reference for host variables. Export values from your shell or deployment secret store before running Compose. The stack needs `POSTGRES_PASSWORD`, `JWT_KEY` (at least 32 UTF-8 bytes), `INTERNAL_API_KEY`, `AGENT_SHARED_SECRET`, `ADMIN_USER_EMAIL`, and `ADMIN_USER_PASSWORD`. A fresh database needs the seeded administrator for its pricing records. Generate the PostgreSQL password from hexadecimal characters so it can be inserted safely into the Npgsql connection string. Compose maps the two internal keys to both services; you do not need to configure them twice. Do not commit credentials or put them in application images.

Optional OpenAI and OpenRouteService keys enable live model and routing calls. The agent retains its deterministic fallbacks when those keys are absent. Cloudinary is optional; when uploads fall back to local storage, files persist in the `api_uploads` Docker volume. Email and required email verification are disabled by default so local accounts can sign in. For production email verification, enable both, supply the SMTP values in `.env.example`, and set `FRONTEND_BASE_URL` to the Vercel URL.

## Local development

From the repository root, with Docker running and the six required values exported:

```sh
docker compose build api agent
docker compose up -d db agent api
curl http://localhost:5159/health
docker compose ps
```

The API is available on `http://localhost:5159` and allows the local frontend origins `http://localhost:5173` and `http://localhost:3000` by default. The agent and database are reachable only on their Docker networks. The API applies pending EF Core migrations automatically in Development. To inspect PostgreSQL, use `docker compose exec db psql -U freightlink -d freightlink` (adjust the user/database if you changed their defaults).

Set `VITE_API_BASE_URL=http://localhost:5159/api/v1` for a locally running frontend. `docker compose down` stops the stack while retaining data. Removing Docker volumes deletes database records, local uploads, and Caddy certificates, so avoid `down --volumes` when you need that data.

## Azure VPS and Vercel

1. Point an API hostname, such as `api.example.com`, to the VPS with DNS. Open inbound TCP 80 and 443 and UDP 443 in the Azure network security group and host firewall. Only Caddy needs public ports; port 5159 is bound to VPS loopback.
2. Export the required secrets and set `ASPNETCORE_ENVIRONMENT=Production`, `API_DOMAIN` to that hostname, `CORS_ORIGINS` to the exact HTTPS Vercel frontend origin, and `FRONTEND_BASE_URL` to the same frontend URL. Configure email and external service keys as needed. Caddy acquires and renews the TLS certificate and redirects public HTTP to HTTPS.
3. Build the images, start dependencies, run the explicit migration job, then start the API and Caddy:

   ```sh
   docker compose build api agent
   docker compose up -d db agent
   docker compose run --rm migrate
   docker compose --profile production up -d api caddy
   curl https://api.example.com/health
   ```

   Use your actual API hostname for the final command. Repeat the build, migration, and final `up` steps for later deployments. A failed migration must be fixed before updating the API. Production startup does not apply migrations automatically.
4. In Vercel, set `VITE_API_BASE_URL=https://api.example.com/api/v1` for the frontend project and redeploy it. Include the `/api/v1` suffix. If using a Vercel custom domain, use its exact origin for `CORS_ORIGINS`; add additional origins as a comma-separated list only when needed.

Swagger remains enabled in the Compose deployment even with `ASPNETCORE_ENVIRONMENT=Production`. Share `https://api.example.com/swagger` as the university documentation URL; the OpenAPI JSON is at `https://api.example.com/swagger/v1/swagger.json`. Set `SWAGGER_ENABLED=false` only if that public documentation is no longer required.

For a PostgreSQL backup, run `docker compose exec -T db pg_dump -U freightlink -d freightlink -Fc > freightlink.dump` (adjust credentials if changed) and store the dump separately from the VPS. The named volumes `postgres_data`, `api_uploads`, `caddy_data`, and `caddy_config` survive container recreation. The API serves plain HTTP only inside Docker and on VPS loopback; Caddy is the public HTTPS entry point.
