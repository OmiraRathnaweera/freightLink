# API Contract / OpenAPI Skeleton
## FreightLink / FreightMatch LK — SE3090 Assignment 1

**Jira:** `Y3S01-15` · Epic: Foundation & Shared Infrastructure (`Y3S01-1`) · Sprint 1 (1–7 Aug 2026)
**Owner:** Ratnaweera O.V. (Team Leader) — Component C, Agent 3
**Repository:** https://github.com/OmiraRathnaweera/freightLink
**Status:** Draft — Sprint 1 skeleton, **Rev. 2**. Component owners fill in request/response schema detail as their controllers are implemented (Sprint 2 onward).

### Change Log

| Rev | Date | Change |
|---|---|---|
| 1 | 7 Aug 2026 | Initial Sprint 1 skeleton |
| 2 | 9 Aug 2026 | Corrected against architecture review: fixed `Load`/`Assignment`/`Trip` status enums, added missing `Assignment` list/detail/decline endpoints (Component C), corrected all `/workflows/*` approval endpoints from `Admin` to `Shipper (own load)` per **ADR-016**, corrected the 409 conflict example (previously implied competitive bidding, contradicting **ADR-017**), flagged `disputes/resolve` role as provisional pending team decision, added explicit JSON casing + ownership-guard conventions |

---

## 1. Purpose & Scope

This document is the shared API contract for FreightLink, established in Sprint 1 before backend implementation begins (Sprint 2). It exists so that:

- React and Flutter frontend work can start against an agreed shape, even before controllers are implemented.
- All four component owners build consistent, predictable endpoints (naming, auth, pagination, error format).
- The Agentic AI subsystem's trigger/status/approval touchpoints are clear from day one.

It covers every endpoint referenced in the project README (Section 5) and the ADRs, organized by component and by the four user roles (Shipper, Agency Staff, Driver, Admin). Full request/response JSON Schemas are intentionally left as **skeleton placeholders** — each component owner completes their own section as Sprint 2–3 CRUD work lands, so this file stays living documentation rather than a one-off Sprint 1 snapshot.

**Not in scope here:** the internal contract between ASP.NET Core and the Python Agentic AI service — that's an internal service-to-service call, not part of the public API surface (README Section 2, integration rule). Only the ASP.NET Core endpoints that **trigger** and **surface** the workflow are documented below.

---

## 2. API Conventions

| Convention | Decision |
|---|---|
| **Base URL** | `/api/v1` (all endpoints below are relative to this) |
| **Format** | JSON request/response bodies; `Content-Type: application/json` |
| **JSON field casing** | `camelCase` for all request/response field names (ASP.NET Core's default `System.Text.Json` casing) — applies to every DTO, including the placeholder schemas added by each owner |
| **Auth** | JWT Bearer token — `Authorization: Bearer <accessToken>` (ADR-011) |
| **Access token lifetime** | 5 minutes |
| **Refresh token lifetime** | 30 days, exchanged at `POST /auth/refresh` |
| **Clients** | React (Admin, Shipper primary) and Flutter (Shipper-lightweight, Agency Staff, Driver) consume identical endpoints — no client-specific API |
| **Third-party calls** | OpenRouteService and PayHere are called **only** from the backend — never exposed as pass-through endpoints |
| **Ownership guards** | A role in the "Roles" column is necessary but not sufficient. Anywhere a table says `(own)` — e.g. `Shipper (own)`, `AgencyStaff (own)` — the controller must also verify the authenticated user is actually the owner/party-of-record on that specific resource (e.g. `load.ShipperId == currentUserId`), not merely that they hold the right role. This applies most critically to the `/workflows/*` approval endpoints (Section 4.6) per **ADR-016**. |

### 2.1 Standard success envelope

Single-resource responses return the resource directly. List responses use a paging envelope:

```json
{
  "items": [ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

### 2.2 Standard error envelope

All non-2xx responses share one shape, so both clients can handle errors generically:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Human-readable summary",
    "details": [
      { "field": "weightKg", "issue": "must be greater than 0" }
    ]
  }
}
```

| Status | Meaning | Example |
|---|---|---|
| `400` | Validation / business-rule failure | Missing Proof-of-Pickup on transition to `PickedUp` (ADR-004) |
| `401` | Missing/expired/invalid access token | |
| `403` | Authenticated but role not permitted, or role permitted but not the resource owner | Driver calling an Admin-only endpoint; a Shipper attempting to approve a workflow run on a load that isn't theirs (ADR-016) |
| `404` | Resource not found | |
| `409` | Concurrency conflict | Double-submit on `POST /assignments/{loadId}/accept` or `.../decline` — e.g. two staff members at the *same* agency submitting the decision at the same time, or a decision submitted after the retry-cap/timeout already closed the proposal out. (**Not** two different agencies competing for the same load — ADR-017 removed competitive bidding, so only one agency ever holds an active proposal for a given load at a time.) |
| `422` | Semantically invalid state transition | e.g. cancelling an already-`Delivered` load |
| `500` | Unhandled server error | |

### 2.3 Pagination & filtering (list endpoints)

Query parameters, applied consistently across all `GET` list endpoints:

```
?page=1&pageSize=20&sortBy=createdAt&sortDir=desc&search=&status=
```

### 2.4 Roles referenced below

`Shipper` · `AgencyStaff` · `Driver` · `Admin` — see README Section 3. Every endpoint table states which role(s) may call it; `Any (authenticated)` means any logged-in role. See the **Ownership guards** row above for what `(own)` means in practice.

---

## 3. Domain Status Enums (referenced by the endpoints below)

| Entity | Workflow |
|---|---|
| `Load` | `Draft → Posted → Matched → ProposalSent → Assigned → InTransit → Delivered → Closed` |
| `Agency` | `Pending → Verified → Active → Suspended` |
| `Assignment` | `Proposed → Accepted` / `Declined` (ADR-017) |
| `Trip` (created only once `Assignment` is `Accepted`) | `Assigned → PickedUp → InTransit → Delivered` |
| `Invoice` | `Draft → Issued → PaymentPending → Paid` / `Failed` |
| `Dispute` | `Raised → UnderReview → Resolved` |
| `AgentWorkflowRun` | Planned by Agent 1 → runs Agents 2–4 → `PendingApproval` → `Approved` / `Rejected` / `Revise` |

> **Corrected in Rev. 2:** `Load` was previously missing `ProposalSent` and `Assigned`. `Assignment` and `Trip` were previously merged into a single row, which hid the two-stage lifecycle ADR-017 requires — they are now separate entities with separate state machines, matching README Section 5 (Component A "CRUD + workflow" row and Component C "CRUD + workflow" row).

---

## 4. Endpoint Inventory

### 4.1 Auth & Identity — Foundation (shared)

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/auth/register` | Register a new user (role-specific payload) | Public |
| POST | `/auth/login` | Issue access + refresh token | Public |
| POST | `/auth/refresh` | Exchange a valid refresh token for a new access token | Public (valid refresh token) |
| POST | `/auth/logout` | Revoke the current refresh token | Any (authenticated) |
| GET | `/auth/me` | Current user profile + role | Any (authenticated) |

> **Open design note (non-blocking):** the approved Figma UI has two separate public registration entry points (Shipper, Agency). Confirm before Sprint 2 whether `/auth/register` stays a single endpoint with a role-discriminated payload, or splits into `/auth/register/shipper` and `/auth/register/agency`. Either is fine architecturally — pick one so both clients build against the same assumption.

### 4.2 Component A — Load Management (Owner: Dias H.N.P.K.)

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/loads` | Create a load (full form — React; quick-post — Flutter) | Shipper |
| GET | `/loads` | Search/filter/sort/paginate loads | Shipper (own), Admin (all) |
| GET | `/loads/{id}` | Load detail incl. status timeline and current `workflowRunId` (if any) | Shipper (own), Admin |
| PUT | `/loads/{id}` | Edit a load (only while `Draft`/`Posted`) | Shipper (own) |
| DELETE | `/loads/{id}` | Cancel a load | Shipper (own), Admin |
| POST | `/loads/{id}/estimate` | Price estimate — `baseFare + distanceKm×ratePerKm + weightKg×ratePerKg` (haversine distance) | Shipper |
| GET | `/loads/{id}/status-history` | Full status timeline | Shipper (own), Admin |
| POST | `/loads/{id}/files` | Upload a document/photo (`Manifest`, `Invoice`, `CargoPhoto`, `Other`) | Shipper (own) |
| GET | `/loads/{id}/files` | List attached files | Shipper (own), Admin |
| DELETE | `/loads/{id}/files/{fileId}` | Remove an attached file | Shipper (own) |

> **Improvement applied:** `GET /loads/{id}` now explicitly returns the current `workflowRunId` (when a match run exists for the load), so the Shipper's React approval console can navigate straight to `GET /workflows/{id}` without needing to already know the workflow ID out-of-band.

### 4.3 Component B — Agency & Fleet Management (Owner: D.B.A.H.W. Bandara)

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/agencies` | Register an agency, incl. `yardLat`/`yardLng` (ADR-002) | AgencyStaff |
| GET | `/agencies` | List/search agencies | Admin |
| GET | `/agencies/{id}` | Agency profile | AgencyStaff (own), Admin |
| PUT | `/agencies/{id}` | Update agency profile | AgencyStaff (own) |
| POST | `/agencies/{id}/verify` | Approve/verify an agency (`Pending → Verified`) | Admin |
| POST | `/agencies/{id}/suspend` | Suspend an agency | Admin |
| GET | `/agencies/expiring-compliance` | Agencies with compliance docs expiring soon (business op) | Admin |
| POST | `/agencies/{id}/vehicles` | Add a vehicle to the fleet | AgencyStaff (own) |
| GET | `/agencies/{id}/vehicles` | List an agency's fleet | AgencyStaff (own), Admin |
| PUT | `/vehicles/{id}` | Update vehicle details | AgencyStaff (own) |
| POST | `/agencies/{id}/drivers` | Onboard a driver (creates a Driver user account) | AgencyStaff (own) |
| GET | `/agencies/{id}/drivers` | List an agency's drivers | AgencyStaff (own), Admin |
| POST | `/agencies/{id}/compliance-docs` | Upload a compliance document (camera capture) | AgencyStaff (own) |
| GET | `/agencies/{id}/compliance-docs` | List/review compliance docs | AgencyStaff (own), Admin |
| PUT | `/agencies/{id}/availability` | Update fleet availability | AgencyStaff (own) |

> **Open item (non-blocking):** no endpoint currently covers `Verified → Active`. Confirm with the team whether this is an explicit Admin action (e.g. `POST /agencies/{id}/activate`) or automatic once the agency has ≥1 vehicle and ≥1 driver on file — either is reasonable, but it should be a stated decision, not an implicit gap.

### 4.4 Component C — Matching & Trip Execution (Owner: Ratnaweera O.V.)

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/assignments` | **Job proposal inbox** — list assignments for the caller's agency, filterable by `status` (e.g. `Proposed`) | AgencyStaff (own agency) |
| GET | `/assignments/{id}` | Assignment detail — recommended agency, price, ETA, load summary (needed *before* a `Trip` exists, i.e. while still `Proposed`) | AgencyStaff (own agency), Shipper (own load), Admin |
| POST | `/assignments/{loadId}/accept` | Agency accepts a proposed load (concurrency-safe) | AgencyStaff (own agency) |
| POST | `/assignments/{loadId}/decline` | Agency declines a proposed load — triggers Component A's `IEmailService` + Agent 1 re-trigger, capped at 3 attempts (ADR-018) | AgencyStaff (own agency) |
| POST | `/assignments/{assignmentId}/assign` | Assign a driver + vehicle to an accepted load | AgencyStaff (own agency) |
| GET | `/trips` | List trips (dashboard) | AgencyStaff, Driver (own), Admin |
| GET | `/trips/{id}` | Trip detail incl. evidence + timeline | AgencyStaff, Driver (own), Shipper (own load), Admin |
| POST | `/trips/{id}/status` | Advance trip status (`Assigned → PickedUp → InTransit → Delivered`); **hard-blocked** without required `TripEvidence` (ADR-004) | AgencyStaff, Driver |
| POST | `/trips/{id}/evidence` | Upload Proof of Pickup / Proof of Delivery (camera) | AgencyStaff (`PickupProof`), Driver (`DeliveryProof`) |
| GET | `/trips/{id}/evidence` | List evidence for a trip | AgencyStaff, Driver (own), Admin |

> **Corrected in Rev. 2:**
> - Added `GET /assignments` and `GET /assignments/{id}` — README explicitly names the "job proposal inbox" as a Flutter/Component C feature (Section 5), but no endpoint previously existed for Agency Staff to view a pending proposal before deciding.
> - Added `POST /assignments/{loadId}/decline` — required by ADR-017's two-outcome `Assignment` lifecycle and by ADR-018's entire auto-retry/email workflow, neither of which is implementable without a decline endpoint.
> - Renamed the `assign` endpoint's path param from `{id}` to `{assignmentId}`, for consistency with the sibling `{loadId}` param used on `accept`/`decline`.

### 4.5 Component D — Billing & Admin Oversight (Owner: Balasooriya B.K.N.N.)

| Method | Path | Description | Roles |
|---|---|---|---|
| GET | `/invoices` | List invoices (financial dashboard) | Shipper (own), AgencyStaff (own), Admin |
| GET | `/invoices/{id}` | Invoice detail incl. payment status | Shipper (own), Admin |
| POST | `/invoices/{id}/checkout` | Create a PayHere sandbox checkout session (business op) | Shipper (own) |
| POST | `/webhooks/payment` | PayHere gateway callback — signature-verified, updates `paymentStatus` | Public (gateway only, signature-verified) |
| POST | `/disputes` | Raise a dispute | Shipper, AgencyStaff |
| GET | `/disputes` | List disputes | Admin, Shipper (own), AgencyStaff (own) |
| GET | `/disputes/{id}` | Dispute detail | Admin, Shipper (own), AgencyStaff (own) |
| POST | `/disputes/{id}/resolve` | Resolve a dispute (`UnderReview → Resolved`) | **Admin — provisional, pending team decision (README Section 10, #9)** |
| GET | `/admin/summary` | Admin financial/operational summary | Admin |

> **Flagged in Rev. 2:** `disputes/{id}/resolve`'s role is currently assumed to be `Admin`, but README Section 10 explicitly lists this as an **unresolved** open decision — "whether Admin retains dispute resolution responsibility." Confirm with the team before Component D's individual report is finalized; if resolution moves elsewhere, only this one row needs to change.

### 4.6 Agentic AI Workflow — Cross-Component

**Owners:** all four agents contribute a step (`AgentStep`); Component D owns Agent 4's computation and the `ApprovalDecision` write logic; **the approval console (UI) lives in Component A's React app** (ADR-016) — Component D does not own shipper-facing screens, only the backend write endpoint.

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/workflows/match` | Trigger the 4-agent pipeline for a posted load (called internally when a `Load` becomes `Posted`) | System (internal, triggered by Component A) |
| GET | `/workflows/{id}` | Workflow run status, current step, plan (Agent 1) | **Shipper (own load)**, Admin |
| GET | `/workflows/{id}/steps` | Per-agent input/output log (`AgentStep`) | **Shipper (own load)**, Admin |
| GET | `/workflows/pending-approval` | Queue of runs paused for approval | **Shipper (own, filtered to their loads)**, Admin (system-wide view for analytics) |
| POST | `/workflows/{id}/approve` | Approve — finalizes proposal, sends job proposal to the recommended agency | **Shipper (own load only)** |
| POST | `/workflows/{id}/reject` | Reject — safe-failure path | **Shipper (own load only)** |
| POST | `/workflows/{id}/revise` | Request revision — re-runs from Agent 3 | **Shipper (own load only)** |

> **Corrected in Rev. 2 — this was the most significant fix.** Every approval-facing endpoint in this section was previously scoped to `Admin`. Per **ADR-016**: *"The Shipper — not the Admin — reviews Agent 4's proposed agency assignment and approves, rejects, or requests revision... Admin has no involvement in per-load matching decisions."* As originally drafted, this section would have let an Admin approve matches while blocking the Shipper from approving their own loads — the exact scenario ADR-016 exists to prevent. Each endpoint must enforce the ownership guard from Section 2 (`load.ShipperId == currentUserId`), not just the `Shipper` role in isolation — this is called out directly in ADR-016's own "Negative consequences" section.
>
> `Admin` is retained on the two `GET` list/detail endpoints only, for system-wide analytics visibility (per README's narrowed Admin scope) — Admin still has **no** write access to `approve`/`reject`/`revise`.

---

## 5. OpenAPI 3.0 Skeleton

The following is the Sprint 1 OpenAPI skeleton — structure, tags, security scheme, and path stubs are fixed; each component owner fills in the `requestBody` / `responses` schema blocks for their own tag as their controller is implemented (Sprint 2–3). Save as `openapi.yaml` alongside this document once schemas are filled in, and it can be served directly from Swagger/Swashbuckle in the ASP.NET Core project (verified live in `Y3S01-123`).

Role restrictions are documented in the tables above (Section 4); this skeleton also carries them as a non-standard `x-allowed-roles` extension on the security-sensitive paths added/corrected in Rev. 2, so the role contract is visible directly in the machine-readable spec, not only in prose.

```yaml
openapi: 3.0.3
info:
  title: FreightLink API
  description: >
    FreightMatch LK — freight-matching platform API (SE3090 Assignment 1).
    Consumed identically by the React (Admin / Shipper) and Flutter
    (Shipper-lightweight, Agency Staff, Driver) clients.
  version: 0.2.0-sprint1-skeleton-rev2
servers:
  - url: /api/v1
    description: Relative base path (host resolved per environment)

tags:
  - name: Auth
  - name: Loads            # Component A — Dias H.N.P.K.
  - name: Agencies          # Component B — D.B.A.H.W. Bandara
  - name: Assignments       # Component C — Ratnaweera O.V.
  - name: Trips             # Component C — Ratnaweera O.V.
  - name: Billing           # Component D — Balasooriya B.K.N.N.
  - name: Workflows         # Agentic AI — all four agents

security:
  - bearerAuth: []

paths:
  /auth/login:
    post:
      tags: [Auth]
      summary: Authenticate and issue access + refresh tokens
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/LoginRequest'
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/AuthTokens'
        '401':
          $ref: '#/components/responses/Unauthorized'

  /auth/register:
    post:
      tags: [Auth]
      summary: Register a new user (role-specific payload)
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/RegisterRequest'
      responses:
        '201': { description: Created }
        '400': { $ref: '#/components/responses/ValidationError' }

  /auth/refresh:
    post:
      tags: [Auth]
      summary: Exchange a refresh token for a new access token
      security: []
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/AuthTokens'

  /auth/me:
    get:
      tags: [Auth]
      summary: Current user profile + role
      responses:
        '200': { description: OK }

  /loads:
    post:
      tags: [Loads]
      summary: Create a load
      responses:
        '201': { description: Created }
        '400': { $ref: '#/components/responses/ValidationError' }
    get:
      tags: [Loads]
      summary: Search/filter/sort/paginate loads
      parameters:
        - $ref: '#/components/parameters/Page'
        - $ref: '#/components/parameters/PageSize'
        - $ref: '#/components/parameters/Status'
      responses:
        '200': { description: OK }

  /loads/{id}:
    parameters:
      - $ref: '#/components/parameters/IdPathParam'
    get:
      tags: [Loads]
      summary: Load detail incl. status timeline and current workflowRunId
      responses:
        '200': { description: OK }
        '404': { $ref: '#/components/responses/NotFound' }
    put:
      tags: [Loads]
      summary: Edit a load (Draft/Posted only)
      responses:
        '200': { description: OK }
    delete:
      tags: [Loads]
      summary: Cancel a load
      responses:
        '204': { description: No Content }

  /loads/{id}/estimate:
    post:
      tags: [Loads]
      summary: Price estimate (baseFare + distanceKm*ratePerKm + weightKg*ratePerKg)
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PriceEstimate'

  /loads/{id}/files:
    parameters:
      - $ref: '#/components/parameters/IdPathParam'
    post:
      tags: [Loads]
      summary: Upload a load document/photo
      responses:
        '201': { description: Created }
    get:
      tags: [Loads]
      summary: List attached files
      responses:
        '200': { description: OK }

  /agencies:
    post:
      tags: [Agencies]
      summary: Register an agency (incl. yardLat/yardLng)
      responses:
        '201': { description: Created }
    get:
      tags: [Agencies]
      summary: List/search agencies
      responses:
        '200': { description: OK }

  /agencies/{id}/verify:
    post:
      tags: [Agencies]
      summary: Approve/verify an agency
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }

  /agencies/expiring-compliance:
    get:
      tags: [Agencies]
      summary: Agencies with compliance docs expiring soon
      responses:
        '200': { description: OK }

  /assignments:
    get:
      tags: [Assignments]
      summary: Job proposal inbox — assignments for the caller's agency
      x-allowed-roles: [AgencyStaff]
      parameters:
        - $ref: '#/components/parameters/Page'
        - $ref: '#/components/parameters/PageSize'
        - $ref: '#/components/parameters/Status'
      responses:
        '200': { description: OK }

  /assignments/{id}:
    get:
      tags: [Assignments]
      summary: Assignment detail (recommended agency, price, ETA, load summary)
      x-allowed-roles: [AgencyStaff, Shipper, Admin]
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }
        '404': { $ref: '#/components/responses/NotFound' }

  /assignments/{loadId}/accept:
    post:
      tags: [Assignments]
      summary: Agency accepts a proposed load (concurrency-safe)
      x-allowed-roles: [AgencyStaff]
      parameters:
        - name: loadId
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200': { description: OK }
        '409': { $ref: '#/components/responses/Conflict' }

  /assignments/{loadId}/decline:
    post:
      tags: [Assignments]
      summary: Agency declines a proposed load (triggers email + Agent 1 re-trigger, ADR-018)
      x-allowed-roles: [AgencyStaff]
      parameters:
        - name: loadId
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200': { description: OK }
        '409': { $ref: '#/components/responses/Conflict' }

  /assignments/{assignmentId}/assign:
    post:
      tags: [Assignments]
      summary: Assign a driver + vehicle to an accepted load
      x-allowed-roles: [AgencyStaff]
      parameters:
        - name: assignmentId
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200': { description: OK }

  /trips/{id}/status:
    post:
      tags: [Trips]
      summary: Advance trip status (hard-blocked without required evidence)
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }
        '400':
          description: Missing required TripEvidence for this transition
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorEnvelope'

  /trips/{id}/evidence:
    post:
      tags: [Trips]
      summary: Upload Proof of Pickup / Proof of Delivery
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '201': { description: Created }

  /invoices/{id}/checkout:
    post:
      tags: [Billing]
      summary: Create a PayHere sandbox checkout session
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }

  /webhooks/payment:
    post:
      tags: [Billing]
      summary: PayHere gateway callback (signature-verified)
      security: []
      responses:
        '200': { description: Acknowledged }
        '400': { description: Invalid signature }

  /disputes:
    post:
      tags: [Billing]
      summary: Raise a dispute
      responses:
        '201': { description: Created }
    get:
      tags: [Billing]
      summary: List disputes
      responses:
        '200': { description: OK }

  /disputes/{id}/resolve:
    post:
      tags: [Billing]
      summary: Resolve a dispute (role provisional — README Section 10, #9)
      x-allowed-roles: [Admin]
      x-open-decision: "Dispute-resolution ownership not yet confirmed by the team — see README Section 10, decision #9"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }

  /workflows/match:
    post:
      tags: [Workflows]
      summary: Trigger the 4-agent matching pipeline for a posted load
      x-allowed-roles: [System]
      responses:
        '202': { description: Accepted — workflow run started }

  /workflows/{id}:
    get:
      tags: [Workflows]
      summary: Workflow run status, current step, plan (Agent 1)
      x-allowed-roles: [Shipper, Admin]
      x-ownership-note: "Shipper access requires load.ShipperId == currentUserId (ADR-016)"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }
        '403': { $ref: '#/components/responses/Unauthorized' }

  /workflows/pending-approval:
    get:
      tags: [Workflows]
      summary: Runs paused for approval (Shipper — own loads; Admin — system-wide)
      x-allowed-roles: [Shipper, Admin]
      responses:
        '200': { description: OK }

  /workflows/{id}/approve:
    post:
      tags: [Workflows]
      summary: Approve — sends job proposal to the recommended agency (ADR-016 — Shipper only)
      x-allowed-roles: [Shipper]
      x-ownership-note: "Requires load.ShipperId == currentUserId (ADR-016)"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }
        '403': { $ref: '#/components/responses/Unauthorized' }

  /workflows/{id}/reject:
    post:
      tags: [Workflows]
      summary: Reject — safe-failure path (ADR-016 — Shipper only)
      x-allowed-roles: [Shipper]
      x-ownership-note: "Requires load.ShipperId == currentUserId (ADR-016)"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }
        '403': { $ref: '#/components/responses/Unauthorized' }

  /workflows/{id}/revise:
    post:
      tags: [Workflows]
      summary: Request revision — re-runs from Agent 3 (ADR-016 — Shipper only)
      x-allowed-roles: [Shipper]
      x-ownership-note: "Requires load.ShipperId == currentUserId (ADR-016)"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      responses:
        '200': { description: OK }
        '403': { $ref: '#/components/responses/Unauthorized' }

components:
  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT

  parameters:
    IdPathParam:
      name: id
      in: path
      required: true
      schema: { type: string, format: uuid }
    Page:
      name: page
      in: query
      schema: { type: integer, default: 1 }
    PageSize:
      name: pageSize
      in: query
      schema: { type: integer, default: 20 }
    Status:
      name: status
      in: query
      schema: { type: string }

  responses:
    NotFound:
      description: Resource not found
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ErrorEnvelope' }
    Unauthorized:
      description: Missing/expired/invalid access token, or authenticated but not the resource owner
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ErrorEnvelope' }
    ValidationError:
      description: Validation / business-rule failure
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ErrorEnvelope' }
    Conflict:
      description: Concurrency conflict
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ErrorEnvelope' }

  schemas:
    ErrorEnvelope:
      type: object
      properties:
        error:
          type: object
          properties:
            code: { type: string }
            message: { type: string }
            details:
              type: array
              items:
                type: object
                properties:
                  field: { type: string }
                  issue: { type: string }

    LoginRequest:
      type: object
      required: [email, password]
      properties:
        email: { type: string, format: email }
        password: { type: string, format: password }

    RegisterRequest:
      type: object
      description: >
        Skeleton placeholder. Confirm before Sprint 2 whether this single schema carries
        a discriminating `role` field (Shipper | AgencyStaff | Admin) with role-specific
        optional properties, or whether registration splits into separate endpoints/schemas
        per the two dedicated Figma entry points (Shipper, Agency) — see Section 4.1 note.
      required: [email, password, role]
      properties:
        email: { type: string, format: email }
        password: { type: string, format: password }
        role: { type: string, enum: [Shipper, AgencyStaff, Admin] }

    AuthTokens:
      type: object
      properties:
        accessToken: { type: string }
        refreshToken: { type: string }
        expiresIn: { type: integer, example: 300 }

    PriceEstimate:
      type: object
      properties:
        estimatedPrice: { type: number, format: double }
        distanceKm: { type: number, format: double }
        baseFare: { type: number, format: double }
        ratePerKm: { type: number, format: double }
        ratePerKg: { type: number, format: double }

    # --- Skeleton only below this line ---
    # Each owner defines their entity schemas here as their controllers land:
    #   Load, LoadStatusHistory, File            -> Dias H.N.P.K.       (Component A)
    #   Agency, Vehicle, Driver, ComplianceDoc    -> D.B.A.H.W. Bandara  (Component B)
    #   Assignment, Trip, TripEvent, TripEvidence -> Ratnaweera O.V.     (Component C)
    #   Invoice, Dispute                          -> Balasooriya B.K.N.N. (Component D)
```

---

## 6. Ownership & Next Steps

| Section to complete | Owner | Target sprint |
|---|---|---|
| `Load`, `LoadStatusHistory`, `File` schemas + full request/response bodies | Dias H.N.P.K. | Sprint 2–3 (`Y3S01-25`–`44`) |
| `Agency`, `Vehicle`, `Driver`, `ComplianceDoc` schemas + confirm `Verified→Active` trigger | D.B.A.H.W. Bandara | Sprint 2–3 (`Y3S01-29`–`32`, `73`) |
| `Assignment`, `Trip`, `TripEvent`, `TripEvidence` schemas + new list/detail/decline endpoints (Section 4.4) | Ratnaweera O.V. | Sprint 2–4 (`Y3S01-33`–`35`, `74`) |
| `Invoice`, `Dispute` schemas + webhook payload; confirm `disputes/resolve` ownership (README §10 #9) | Balasooriya B.K.N.N. | Sprint 2–5 (`Y3S01-36`–`38`, `81`, `93`) |
| `Workflows` request/response schemas (`AgentWorkflowRun`, `AgentStep`, `ApprovalDecision`); implement the `load.ShipperId == currentUserId` ownership guard on `approve`/`reject`/`revise` per ADR-016 | All four (agent owners) | Sprint 5 (`Y3S01-90`–`92`, `95`–`97`) |
| Role-based authorization scheme wired to every endpoint above, including ownership guards flagged with `(own)` | Ratnaweera O.V. | Sprint 1 (`Y3S01-19`), enforced per-controller Sprint 2+ |
| Confirm `/auth/register` single-vs-split design (Section 4.1 note) | Dias H.N.P.K. (owns Auth foundation alongside Component A) | Sprint 2 |
| Publish live Swagger/OpenAPI UI from the ASP.NET Core project | Ratnaweera O.V. | Verified Sprint 7 (`Y3S01-123`) |

This document is the Sprint 1 deliverable for `Y3S01-15`. Update it whenever an endpoint's path, method, or role requirement changes — it is the single source of truth both clients (React, Flutter) build against.
