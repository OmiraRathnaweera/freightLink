# FreightLink (FreightMatch LK)

An agentic AI freight-matching platform for Sri Lanka. A shipper posts a load, a four-agent AI pipeline recommends the best-fit freight agency with a route, ETA and price, the shipper approves, the agency accepts, a driver completes the trip with photo evidence, and the agency invoices the shipper.

**Module:** SE3090 Software Engineering Frameworks · Assignment 1 (group project) · Year 3 Semester 1, 2026

## Contents

1. [Project overview](#1-project-overview): business problem, user roles, features, technology justification
2. [Architecture and design](#2-architecture-and-design): system architecture, Agentic AI architecture, database design, repository structure
3. [Setup](#3-setup): installation, environment variables, database setup, startup for all components
4. [Using and verifying the system](#4-using-and-verifying-the-system): API documentation, test instructions, deployment, live URLs, test accounts
5. [Team and project reflection](#5-team-and-project-reflection): individual contributions, challenges, security considerations, AI usage declaration

---

## 1. Project overview

### 1.1 Business problem

Road freight in Sri Lanka is mostly arranged through phone calls, brokers and personal contacts. That leaves four gaps:

- **Finding a carrier.** Shippers have no easy way to find an available, compliant carrier or to compare options.
- **Pricing.** There is no published freight rate card, so shippers cannot tell whether a quote is fair.
- **Trust and compliance.** Licences, insurance and permits are checked informally.
- **Evidence.** Nobody holds shared proof of pickup, delivery or payment, so disputes are hard to settle.

FreightLink matches shippers with verified freight agencies, prices the job with a transparent formula, and records evidence at every step.

### 1.2 User roles

| Role | Primary app | What they do |
| --- | --- | --- |
| **Shipper** | Web (React) | Posts and tracks loads, approves or rejects the AI recommendation, uploads proof of payment, raises disputes. Registers on the web app only. |
| **Agency Staff** | Web and mobile | Registers the agency, uploads compliance documents, manages vehicles and drivers, accepts or declines proposals, assigns trips, issues invoices and confirms payment. |
| **Driver** | Mobile (Flutter) | Created by agency staff (a temporary password is emailed). Runs the assigned trip and uploads pickup and delivery photo evidence. |
| **Admin** | Web (React) | Verifies agencies and their documents, maintains pricing reference data, resolves disputes, reads platform analytics. An admin cannot approve AI matches. |

### 1.3 Features

The system has four components, each owned by one team member and each with its own AI agent role.

| | Component | Main features | Agentic AI role |
| --- | --- | --- | --- |
| A | **Load Management** | Create, edit, publish, cancel and track loads. Attach cargo documents. Trigger matching. Confirm, reject or revise the AI recommendation. | Agent 1: Planner/Coordinator |
| B | **Agency and Fleet Management** | Agency registration and admin verification, compliance documents, vehicles, drivers, suspension. | Agent 2: Domain Analysis |
| C | **Matching and Trip Execution** | Ranked agency recommendation with route and price, agency accept or decline with automatic retry (maximum 3 attempts), trip stages with mandatory photo evidence. | Agent 3: Matching and Pricing |
| D | **Billing and Admin Oversight** | Invoices, manual payment proof and confirmation, disputes, pricing configuration, admin analytics. | Agent 4: Validation and Safety |

Each component has its own sequence diagram, shown in [section 2.1](#21-system-architecture).

### 1.4 Technology justification

| Technology | Why it was chosen |
| --- | --- |
| **ASP.NET Core 8 Web API** | A single, typed API shared by the web and mobile clients. Built-in JWT auth, role-based authorisation, dependency injection and strong tooling for EF Core and testing. |
| **PostgreSQL 16 + EF Core (Npgsql)** | Relational integrity for loads, assignments, trips and invoices. `pg_trgm` indexes support fast text search. Migrations keep the schema versioned. |
| **React 19 + Vite** | Shippers and admins work at a desk, so a rich web app is the primary interface. Redux Toolkit, TanStack Query and Tailwind keep state and styling organised. |
| **Flutter** | One codebase for the mobile app that drivers and agency staff use in the field. |
| **Python FastAPI + LangGraph** | LangGraph models the four-agent pipeline as an explicit state graph with safe-failure short-circuits. FastAPI keeps the service small. It is internal-only and the API is its sole caller. |
| **OpenAI `gpt-4o-mini`** | The cheapest model that reliably supports structured output. Every agent has a deterministic fallback, so a model outage never blocks the platform. |
| **OpenRouteService** | Free routing, distance and ETA data for Sri Lanka. |
| **Cloudinary** | Hosted storage for cargo photos, compliance documents, trip evidence and payment proofs. |
| **Docker Compose + Caddy** | One command starts the whole backend. Caddy provides automatic HTTPS. |
| **GitHub Actions** | Automated build and test on every push for each component. |

---

## 2. Architecture and design

### 2.1 System architecture

![High-level system architecture](https://dl.dropbox.com/scl/fi/b96m8olmhb9236e98skbq/FreightLink-High-Level-System-Architecture-as-implemented.png?rlkey=5yhjsotkohi39toh8wmycd6m0&st=ht5jbtie&dl=0)

- Only React and Flutter call the API. The agent service has no public port and is called only by the API.
- The database is on an internal Docker network with no published port.
- Only Caddy's ports 80 and 443 are public.

#### Component sequence diagrams

Each component has its own sequence diagram. Click an image to view it at full size.

**Component A: Load Management** (Agent 1, Planner)

![Component A: Load Management sequence diagram](https://dl.dropbox.com/scl/fi/bpzs4esbxxjr5g4l5xsxj/Component-A-Load-Management-Sequence-as-implemented.png?rlkey=8e3mxoufb0kfps3zt61g1asxj&st=rra7kgwh&dl=0)

**Component B: Agency and Fleet Management** (Agent 2, Domain Analysis)

![Component B: Agency and Fleet Management sequence diagram](https://dl.dropbox.com/scl/fi/vjn66qp35oa9kphegbe6q/Component-B-Agency-and-Fleet-Management-Sequence-as-implemented.png?rlkey=4m2re22nui4wmnqoybov2q2oj&st=1lllxvds&dl=0)

**Component C: Matching and Trip Execution** (Agent 3, Matching and Pricing)

![Component C: Matching and Trip Execution sequence diagram](https://dl.dropbox.com/scl/fi/s1ww7jezcr5lpuor5lv56/Component-C-Matching-and-Trip-Execution-Sequence-as-implemented.png?rlkey=1dka7exqgjxcko1e6lbcv2x37&st=7u484m07&dl=0)

**Component D: Billing and Admin Oversight** (Agent 4, Validation and Safety)

![Component D: Billing and Admin Oversight sequence diagram](https://dl.dropbox.com/scl/fi/5c2mpkl4u70ti6ckxce5m/Component-D-Billing-and-Admin-Oversight-Sequence-as-implemented.png?rlkey=6rp6561saoek6d07574rymhcb&st=fqntf6x3&dl=0)



### 2.2 Agentic AI architecture

The pipeline is a **role-based sequential multi-agent system** built with LangGraph (`agent/src/freightlink_agent/graph/pipeline.py`). Four specialised agents share one persisted `WorkflowState`, and each step is reported back to the API and stored for audit.

![AI Agent architecture diagram](https://dl.dropbox.com/scl/fi/6n8unqc0zbjkigc8pagk6/FreightLink-Agentic-Matching-Workflow-as-implemented-1.png?rlkey=31l6gyofn63yw9q7o6pnxe34k&st=ixuu1qfw&dl=0)

| Agent | Responsibility |
| --- | --- |
| **1. Planner** | Validates the load payload, asks the LLM for an objective, a fixed list of pipeline stages and a friendly message to the shipper. |
| **2. Domain Analysis** | Takes the candidate agencies the API pre-filtered (Active, with an available vehicle and an active driver), ranks them by distance from yard to pickup, and keeps those whose fleet has enough weight and volume capacity (top 5). Stops the run with `zero_eligible_agencies` if none qualify. |
| **3. Matching and Pricing** | Uses routing and pricing tools to measure each shortlisted agency's ETA to pickup, selects the best fit (fastest positioning ETA, then distance), routes the cargo leg and prices it with `baseFare + distanceKm × ratePerKm + weightKg × ratePerKg`. Returns a ranked top five. |
| **4. Validation and Safety** | Deterministic checks (positive price, valid route, legal weight and vehicle-class limits) and an LLM-written validation summary and proposal email. |

Design rules:

- **Deterministic tools do the maths and hard checks.** The LLM plans, selects among tool results and explains. It must cite tool results and may not invent a number.
- **Human approval is unconditional.** The shipper approves every recommendation, and the agency independently accepts or declines (ADR-013, ADR-016, ADR-017). A decline triggers an automatic re-match, up to 3 attempts (ADR-018). Admins cannot approve matches.
- **Fallbacks.** Without an OpenAI or OpenRouteService key, or when a call fails, each agent uses deterministic template copy or a simulated route. A per-process cap (`OPENAI_MAX_CALLS_PER_PROCESS`) limits model spend.
- **Internal only.** The agent service is reachable only from the API (ADR-009). The API stores every run, step, tool call and candidate (`AgentWorkflowRun`, `AgentStep`, `ToolCall`, `MatchCandidate`, `ApprovalDecision`).

#### Agentic matching sequence

![Agentic matching sequence diagram](https://dl.dropboxusercontent.com/scl/fi/bmcaued398gbyz4qjzbal/agentic-matching-sequence.png?rlkey=nhbnuzut3cimqxlezwg0zibcf&st=e7n7w4d9&dl=0)

### 2.3 Database design

PostgreSQL 16, accessed with EF Core (Npgsql). Migrations are applied by the `migrate` service. Enums are stored as strings (ADR-020), and the `pg_trgm` extension backs search on loads. The entity-relationship diagram is below.

| Area | Main tables / entities |
| --- | --- |
| Identity | `User`, `RefreshToken`, `ShipperProfile` |
| Loads (A) | `Load`, `LoadFile`, `LoadStatusHistory`, `UploadedFile`, `LoadProposal` |
| Agency and fleet (B) | `Agency`, `AgencyStaff`, `AgencyStatusHistory`, `ComplianceDoc`, `Vehicle`, `Driver` |
| Matching and trips (C) | `Assignment`, `AssignmentResponse`, `AssignmentActionToken`, `MatchCandidate`, `Trip`, `TripEvent`, `TripEvidence` |
| Agent audit (C) | `AgentWorkflowRun`, `AgentStep`, `ToolCall`, `ApprovalDecision` |
| Billing and oversight (D) | `Invoice`, `InvoiceLineItem`, `Dispute`, `DisputeResolution`, `Notification` |
| Pricing configuration (D) | `FuelPriceRate`, `VehicleClassEfficiency`, `PricingFormulaConfig` |

Status machines (allowed transitions are table-driven in `backend/Common/Domain`):

- **Load:** Draft, Posted, Matched, InTransit, Delivered, Closed (Cancelled from Draft, Posted or Matched)
- **Trip:** Assigned, PickedUp, InTransit, Delivered (or Cancelled)
- **Invoice:** Draft, Issued, PaymentPending, Paid (or Void, Failed)
- **Dispute:** Raised, UnderReview, Resolved
- **Agency:** Pending, Verified, Active, Suspended (reversible)

![Entity-relationship diagram](https://dl.dropboxusercontent.com/scl/fi/rkbaenqkckud94swtqbtl/entity-relationship.png?rlkey=2gwvu8qphja8argo7w1hbeo1b&st=3f9320my&dl=0)

### 2.4 Repository structure

```text
freightLink/
├── .github/workflows/        CI pipelines: agent-ci, backend-ci, frontend-ci, mobile-ci
├── backend/                  ASP.NET Core 8 Web API (FreightLink.Api)
│   ├── Controllers/          thin HTTP layer: reads the JWT role, calls a service
│   ├── Services/             business rules, ownership checks, status transitions
│   │   └── Interfaces/
│   ├── Entities/             EF Core entities (and Enums/)
│   ├── DTOs/                 request and response contracts, grouped by module
│   ├── Data/                 AppDbContext and Configurations/ (one per entity)
│   ├── Migrations/           EF Core migrations
│   ├── Common/               Domain/ (status rules), Errors/, Exceptions/, Filters/,
│   │                         Options/, Security/, Validation/, Email/
│   ├── Middleware/           global exception handling (error envelope)
│   ├── FreightLink.Api.Tests/    xUnit: Unit/, Integration/, EndToEnd/
│   ├── PerformanceTests/     k6 scripts (loads API, agent workflow latency)
│   ├── Program.cs            startup, DI, JWT, CORS, seeding
│   ├── Dockerfile
│   └── .env.example
├── frontend/                 React 19 + Vite web app
│   ├── src/
│   │   ├── features/         agencies, agent-workflows, analytics, assignmentActions, auth,
│   │   │                     billing, disputes, loads, marketing, pricingConfig, trips
│   │   │                     (features use api/, components/, pages/, lib/, tests/ as needed)
│   │   ├── components/       shared UI
│   │   ├── hooks/ layouts/ lib/ routes/ store/ test/ assets/
│   │   └── App.jsx, main.jsx, index.css
│   ├── public/
│   ├── vercel.json, vite.config.js, package.json
│   └── .env.example
├── mobile/freightlink_mobile/    Flutter app
│   ├── lib/
│   │   ├── core/             constants, models, network, routing, storage, theme, utils
│   │   ├── features/         agencies, auth, billing, disputes, loads, notifications, trips
│   │   ├── shared/widgets/
│   │   └── main.dart
│   ├── test/
│   ├── android/ ios/ web/ linux/ macos/ windows/    platform folders
│   └── pubspec.yaml, .env.example
├── agent/                    Python FastAPI + LangGraph agent service
│   ├── src/freightlink_agent/
│   │   ├── agents/           planner, domain_analysis, matching_pricing, validation_safety
│   │   ├── graph/            pipeline (LangGraph wiring), state, step_reporter
│   │   ├── tools/            matching_tools, pricing, routing
│   │   ├── core/             config, llm (OpenAI + fallbacks), backend_client
│   │   ├── schemas/
│   │   └── main.py           FastAPI app
│   ├── tests/                Unit/, Integration/, EndToEnd/, Performance/ (pytest)
│   ├── scripts/              demo_matching.py
│   ├── Dockerfile, pyproject.toml, uv.lock
│   └── .env.example
├── deploy/
│   └── Caddyfile             HTTPS reverse proxy configuration
├── testing/                  test plan, test cases, execution summary, defect report,
│   │                         AI usage declaration
│   ├── dashboard/            local page that runs the suites
│   ├── evidence/             raw test, coverage, k6 and ZAP output
│   └── scripts/              run-nfr.sh, build_test_docs.py
├── postman/                  collection, Local and Azure environments, samples/
├── compose.yaml              Docker Compose stack (db, agent, migrate, api, caddy)
├── .env.example              variables for compose.yaml
├── pyrefly.toml
└── README.md
```

---

## 3. Setup

### 3.1 Prerequisites

| Tool | Needed for |
| --- | --- |
| Docker with Compose v2 | Running the whole backend stack, and the test database |
| .NET SDK 8 | Running or testing the API locally |
| Node.js 22+ | Web app |
| Python 3.12 and [uv](https://docs.astral.sh/uv/) | Agent service |
| Flutter (stable) | Mobile app |

### 3.2 Environment variables

There is one `.env.example` per component. Copy each to `.env` and fill it in. **Never commit a real `.env`.**

| Component | Template | Key variables |
| --- | --- | --- |
| Whole Docker stack (the file used on the server) | `.env.example` | `POSTGRES_PASSWORD`, `JWT_KEY`, `INTERNAL_API_KEY`, `AGENT_SHARED_SECRET`, `ADMIN_USER_EMAIL`, `ADMIN_USER_PASSWORD` (all required); `API_DOMAIN`, `CORS_ORIGINS`, `FRONTEND_BASE_URL`; optional `OPENAI_API_KEY`, `OPENROUTESERVICE_API_KEY`, `CLOUDINARY_*`, `EMAIL_*` |
| Backend API (local `dotnet run`) | `backend/.env.example` | `CONNECTIONSTRINGS__DEFAULTCONNECTION`, `JWT__*`, `INTERNAL_API_KEY`, `AGENT_API_BASE_URL`, `AGENT_SERVICE_API_KEY`, `CORS_ORIGINS`, `CLOUDINARY__*`, `OPENROUTESERVICE__*`, `EMAIL__*`, `ADMIN_USER_*` |
| Agent service (local) | `agent/.env.example` | `SHARED_SECRET`, `INTERNAL_API_KEY`, `BACKEND_BASE_URL`, `OPENAI_API_KEY`, `OPENAI_MODEL`, `OPENAI_MAX_CALLS_PER_PROCESS`, `OPENROUTESERVICE_*`, `LOG_LEVEL` |
| React web app | `frontend/.env.example` | `VITE_API_BASE_URL` (must include `/api/v1`) |
| Flutter mobile app | `mobile/freightlink_mobile/.env.example` | `API_BASE_URL`, `APP_NAME` (passed with `--dart-define`) |
| PostgreSQL | root `.env` | `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` |

Three secrets must match across services:

- `INTERNAL_API_KEY` is the same on the API and the agent.
- `AGENT_SHARED_SECRET` is the API's `AGENT_SERVICE_API_KEY` and the agent's `SHARED_SECRET`.
- The `JWT_KEY` must be at least 32 bytes.

### 3.3 Database setup

- **Docker stack (recommended).** Nothing to do by hand. The `migrate` service applies all EF Core migrations before the API starts, and the API seeds the admin account, demo agencies and default pricing data on startup.
- **Local development without Docker.** Start a PostgreSQL 16 container and point `CONNECTIONSTRINGS__DEFAULTCONNECTION` at it. Outside Production the API applies pending migrations automatically at startup.

```sh
docker run -d --name freightlink-pg -p 5432:5432 \
  -e POSTGRES_USER=freightlink -e POSTGRES_PASSWORD=<a password> -e POSTGRES_DB=freightlink \
  postgres:16-alpine
```

To work with migrations by hand: `cd backend && dotnet ef migrations add <Name>` and `dotnet ef database update`.

### 3.4 Start every component

**Option 1: all backend services with Docker (one command)**

```sh
cp .env.example .env    # then edit .env
docker compose up -d --build
docker compose ps
```

See [section 4.3](#43-deployment-instructions) for the full deployment notes. The React app and the Flutter app are started separately, as below.

**Option 2: each component locally**

| Component | Commands | Runs on |
| --- | --- | --- |
| Backend API | `cd backend && cp .env.example .env && dotnet restore && dotnet run` | `http://localhost:5159` |
| Agent service | `cd agent && cp .env.example .env && uv sync && uv run uvicorn freightlink_agent.main:app --port 8001 --reload` | `http://localhost:8001` |
| React web app | `cd frontend && cp .env.example .env && npm install && npm run dev` | `http://localhost:5173` |
| Flutter mobile app | `cd mobile/freightlink_mobile && flutter pub get && flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5159/api/v1` | emulator or device |

Notes:

- Start PostgreSQL first, then the agent, then the API.
- For the Android emulator use `10.0.2.2` to reach the host. For the iOS simulator or desktop use `http://localhost:5159/api/v1`.
- Add the web app's origin (for example `http://localhost:5173`) to `CORS_ORIGINS`.

---

## 4. Using and verifying the system

### 4.1 API documentation

| Resource | Location |
| --- | --- |
| Live Swagger UI | https://freightlink.centralindia.cloudapp.azure.com/swagger |
| OpenAPI JSON | https://freightlink.centralindia.cloudapp.azure.com/swagger/v1/swagger.json |
| API contract (conventions, resources, error envelope) | [`docs/api-contract-openapi-skeleton.md`](docs/api-contract-openapi-skeleton.md) |
| Postman collection and environments | [`postman/`](postman) |

Conventions: base path `/api/v1`, camelCase JSON, JWT bearer authentication, paged lists, and one error envelope for every failure: `{ "error": { "code", "message", "details" } }`. The agent calls back through key-protected `/internal/...` endpoints that are not meant for clients.

### 4.2 Test instructions

| Suite | Folder | Command |
| --- | --- | --- |
| Backend (xUnit: unit, integration, end-to-end, security) | `backend/` | `dotnet test backend.sln --configuration Release` (Docker must be running, because Testcontainers starts PostgreSQL) |
| React web app (Vitest) | `frontend/` | `npm install` then `npm test` |
| Agent (pytest) | `agent/` | `uv run pytest tests` |
| Flutter mobile app | `mobile/freightlink_mobile/` | `flutter test` |
| Performance (k6) and security (OWASP ZAP) | repository root | `testing/scripts/run-nfr.sh` (or `run-nfr.sh k6` / `run-nfr.sh zap`). It starts a throwaway database and API and never uses your `.env`. |

Run a single backend class with `dotnet test --filter "FullyQualifiedName~LoadServiceTests"`. Always run tests on your own machine, not on the production server.

The test plan, curated test cases, execution summary, defect report and raw evidence are under [`testing/`](testing). The dashboard `python3 testing/dashboard/server.py` runs the suites from one web page.

### 4.3 Deployment instructions

#### Configuration

Everything is configured through one file. Copy [`.env.example`](.env.example) to `.env` in the repository root and edit it; Docker Compose reads it automatically. The stack needs `POSTGRES_PASSWORD`, `JWT_KEY` (at least 32 UTF-8 bytes), `INTERNAL_API_KEY`, `AGENT_SHARED_SECRET`, `ADMIN_USER_EMAIL`, and `ADMIN_USER_PASSWORD`; Compose refuses to start with a clear message if any is missing. A fresh database needs the seeded administrator for its pricing records. Generate the PostgreSQL password from hexadecimal characters so it can be inserted safely into the Npgsql connection string. Compose maps the two internal keys to both services; you do not need to configure them twice. Never commit `.env` or put credentials in application images.

Optional OpenAI and OpenRouteService keys enable live model and routing calls. The agent retains its deterministic fallbacks when those keys are absent. Cloudinary credentials (`CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET`) are required for file uploads (compliance documents, cargo photos, trip evidence, payment proofs): the API has no local-storage fallback, and every upload returns HTTP 500 while they are unset. Email and required email verification are disabled by default so local accounts can sign in. For production email verification, enable both, supply the SMTP values in `.env.example`, and set `FRONTEND_BASE_URL` to the Vercel URL.

#### Deploy (one command)

The database, Python agent, API and Caddy (HTTPS) start together. Startup order is enforced by Compose: `db` and `agent` become healthy, the one-shot `migrate` service applies all EF Core migrations and exits, then `api` starts and must pass its `/health` check before `caddy` starts. If a migration fails, the API is not started.

```sh
cp .env.example .env    # then edit .env
docker compose up -d --build
docker compose ps
```

Repeat `docker compose up -d --build` for every later deployment; migrations run again automatically (already-applied ones are skipped). Check migration output with `docker compose logs migrate`.

The API is also on `http://localhost:5159` (loopback only) and allows the frontend origins in `CORS_ORIGINS`. The agent and database are reachable only on their Docker networks. To inspect PostgreSQL, use `docker compose exec db psql -U freightlink -d freightlink` (adjust the user/database if you changed their defaults). `docker compose down` stops the stack while retaining data. Removing Docker volumes deletes database records, local uploads, and Caddy certificates, so avoid `down --volumes` when you need that data.

For local development set `API_DOMAIN=localhost` (Caddy then uses a locally-signed certificate), or point a locally running frontend at `VITE_API_BASE_URL=http://localhost:5159/api/v1` and add its origin to `CORS_ORIGINS`. If ports 80/443 are busy, set `HTTP_PORT`/`HTTPS_PORT`.

#### Azure VPS and Vercel

1. The VM's Azure DNS name (`freightlink.centralindia.cloudapp.azure.com`) is the API hostname; no extra DNS setup is needed. In the Azure network security group (and host firewall) allow inbound TCP 80 and 443 and, optionally, UDP 443 for HTTP/3. Port 80 must stay open because Caddy uses it to obtain and renew the certificate. Only Caddy needs public ports; port 5159 is bound to VM loopback.
2. In `.env`, set the required secrets plus `API_DOMAIN` to that hostname, and `CORS_ORIGINS` and `FRONTEND_BASE_URL` to the exact HTTPS Vercel frontend origin. Caddy acquires and renews the TLS certificate and redirects public HTTP to HTTPS.
3. Run `docker compose up -d --build`, then `curl https://freightlink.centralindia.cloudapp.azure.com/health` with your actual hostname.
4. In Vercel, set `VITE_API_BASE_URL=https://freightlink.centralindia.cloudapp.azure.com/api/v1` for the frontend project and redeploy it. Include the `/api/v1` suffix. If using a Vercel custom domain, use its exact origin for `CORS_ORIGINS`; add additional origins as a comma-separated list only when needed.

Swagger remains enabled in the Compose deployment even with `ASPNETCORE_ENVIRONMENT=Production`. Share `https://freightlink.centralindia.cloudapp.azure.com/swagger` as the university documentation URL; the OpenAPI JSON is at `https://freightlink.centralindia.cloudapp.azure.com/swagger/v1/swagger.json`. Set `SWAGGER_ENABLED=false` only if that public documentation is no longer required.

For a PostgreSQL backup, run `docker compose exec -T db pg_dump -U freightlink -d freightlink -Fc > freightlink.dump` (adjust credentials if changed) and store the dump separately from the VPS. The named volumes `postgres_data`, `api_uploads`, `caddy_data`, and `caddy_config` survive container recreation. The API serves plain HTTP only inside Docker and on VPS loopback; Caddy is the public HTTPS entry point.

#### Containers and firewall

| Service | Image | Port | Public? |
| --- | --- | --- | --- |
| `caddy` | `caddy:2-alpine` | 80, 443 (TCP), 443 (UDP) | Yes |
| `api` | built from `backend/Dockerfile` (`dotnet/aspnet:8.0`) | 8080, published only on `127.0.0.1:5159` | No |
| `agent` | built from `agent/Dockerfile` (`python:3.12-slim`) | 8001 | No |
| `db` | `postgres:16-alpine` | 5432 | No |
| `migrate` | same image as `api` | none (one-shot job) | No |

### 4.4 Live URLs

| What | URL |
| --- | --- |
| API base | https://freightlink.centralindia.cloudapp.azure.com/api/v1 |
| Health check | https://freightlink.centralindia.cloudapp.azure.com/health |
| Swagger UI | https://freightlink.centralindia.cloudapp.azure.com/swagger |
| Web app (Vercel) | https://freight-link-six.vercel.app/ |
| Source repository | https://github.com/OmiraRathnaweera/freightLink |

### 4.5 Test accounts (Prod Acc)

The API seeds these accounts on startup so each role can be tried without manual setup.

| Role | Email | Password | Notes |
| --- | --- | --- | --- |
| Admin | `it24101003@my.sliit.lk` | `Password@123` | Created on first start. There is no public admin registration. |
| Shipper | `hnpkdias@gmail.com` | `Qwe123##123` | - |
| Agency Staff | `pasindhukavishan@gmail.com` | `Qwe123##123` | - |
| Driver | `nuwanthadias@gmail.com` | `Qwe123##123` | Drivers can log in only through the mobile app. |


---

### Test accounts (Dev Acc)

The API seeds these accounts on startup so each role can be tried without manual setup.

| Role | Email | Password | Notes |
| --- | --- | --- | --- |
| Admin | the value of `ADMIN_USER_EMAIL` on the server | the value of `ADMIN_USER_PASSWORD` on the server | Created on first start. There is no public admin registration. |
---

## 5. Team and project reflection

### 5.1 Individual contributions

Ownership follows the component assignment. The "also contributed" column is taken from the Git history (`git shortlog` and per-folder history). **Each member should confirm and refine their own row before submission.**

| Member | Component and AI agent | Main work | Also contributed |
| --- | --- | --- | --- |
| Dias H.N.P.K. | A: Load Management, Agent 1 (Planner) | Load API and service, load lifecycle and status history, cargo document attachment, load pages in the React app, the Planner agent | Most of the React web app and Flutter app screens across all components, Docker Compose and Caddy deployment, the Azure VPS and Vercel setup, the test suites and the documentation set |
| D.B.A.H.W. Bandara | B: Agency and Fleet, Agent 2 (Domain Analysis) | Agency registration and verification, compliance documents, vehicles and drivers, agency status rules, the Domain Analysis agent | Agency and fleet screens on web and mobile |
| Ratnaweera O.V. (Team Leader) | C: Matching and Trip Execution, Agent 3 (Matching and Pricing) | Assignment and proposal flow, retry logic, trip execution and evidence, the agent pipeline, routing and pricing tools | Trip screens on web and mobile, the agent workflow console |
| Balasooriya B.K.N.N. | D: Billing and Admin Oversight, Agent 4 (Validation and Safety) | Invoices and manual payment proof, disputes, pricing configuration, admin analytics, validation and safety checks | Billing and dispute screens, request and response DTOs, backend tests |

Commit totals per author: `git shortlog -sne --all`.

### 5.2 Challenges

- **Changing the LLM provider.** The original NVIDIA NIM choice was replaced by Gemini and finally by OpenAI (ADR-008). Every agent therefore got a deterministic fallback, and a per-process call cap was added to control cost.
- **Keeping the AI accountable.** The first design had an admin approving workflow runs. It was replaced with unconditional shipper approval and an independent agency accept or decline (ADR-013, ADR-016, ADR-017). The old admin route now always returns 403.
- **Payment gateway.** Online payment (PayHere) was dropped in favour of manual payment proof and agency confirmation (ADR-021), which fits how local freight is paid.
- **Documents drifting from code.** Early design documents and the code diverged as the system grew. A role and feature audit and an implementation baseline were written, and where they disagree the code is the source of truth.
- **Network isolation in Docker.** The database and the agent had to stay unreachable from the internet while Caddy and the API remained reachable. This was solved with separate internal and public Docker networks.
- **Reliable agent audit trail.** Agent tool calls are stored against a unique `(step, tool name, attempt)` constraint, so the fallback path had to continue attempt numbers instead of restarting them.
- **Making failures visible.** An AI run that stopped early (for example no agency with a big enough vehicle) used to show only a spinner. The UI now explains the reason and offers a retry.
- **Testing found real defects.** The test and security pass found six defects (for example unauthenticated seed endpoints and a missing `X-Content-Type-Options` header). Five are fixed and retested. See `testing/DEFECT_REPORT.md`.

### 5.3 Security considerations

| Area | What is in place |
| --- | --- |
| Authentication | JWT bearer tokens (HS256, key of at least 32 bytes, the API refuses to start otherwise) with persisted refresh tokens that are rotated. |
| Authorisation | Four roles enforced with `[Authorize(Roles = ...)]` and again in the services, which check ownership (a shipper sees only their own loads, an agency only its own trips and invoices). |
| Service-to-service | The agent calls only `/internal/...` endpoints, protected by an `X-Internal-Api-Key` header compared in constant time. The agent in turn accepts only requests carrying the shared secret. |
| Network | HTTPS through Caddy with automatic certificates. The database and agent have no published ports. The database network is internal. |
| Secrets | Everything comes from environment variables. `.env` is git-ignored, and Compose refuses to start when a required secret is missing. |
| Input and files | Request validation on every DTO. Uploads accept images and PDFs only. CORS lists exact origins, with no wildcard. |
| Error handling | One error envelope. Unhandled exceptions return a generic 500 without internals. |
| Business safety | Mandatory pickup and delivery evidence (422 without it), status-transition tables, a human approval step, and a cap on automatic re-matching. |
| AI safety | The LLM may not invent numbers (every figure must come from a tool result), a hard cap on OpenAI calls, and deterministic validation in Agent 4. |
| Verification | A security test class plus an authenticated OWASP ZAP scan with no High or Medium alerts after the fixes (see `testing/`). |

**Known limitations:**

- Swagger is public on the deployed server unless `SWAGGER_ENABLED=false`.
- The seeded demo accounts share one password (see section 4.5).
- Some low-severity ZAP alerts remain.
- JWT lifetimes: the design target is 5 minutes (access) and 30 days (refresh), while the Compose default is 15 minutes and 7 days.
