# FreightMatch LK — Freight & Logistics Matching Platform
### SE3090 – Software Engineering Frameworks | Assignment 1 | Group Project

> **Academic project notice:** This system is built strictly to satisfy the SE3090 Assignment 1 marking rubric within an 8-week academic timeline. It is intentionally scoped down from a full commercial product — every design decision below is chosen for rubric coverage, viva-defensibility, and reliability during a live demo, not real-world completeness.

---

## 1. Project Overview

**Domain:** A freight-matching platform for the Sri Lankan market, inspired by Uber Freight but adapted to local market structure. Shippers post loads; the Agentic AI subsystem is triggered immediately and recommends the single best-fit freight **agency** (fleet-owning company); the **shipper** reviews and approves/rejects the recommendation; on approval, the system sends that agency a **job proposal**, which the agency accepts or declines before the assignment is finalized. Admins operate independently of this workflow — verifying agency KYC/compliance and monitoring system-wide analytics.

**Why "agencies" instead of independent drivers:** Sri Lanka's freight market is dominated by agencies/companies operating vehicle fleets from fixed yards/depots — not independent owner-operators as in the US/UK Uber Freight model. The system reflects this reality, which also meaningfully simplifies the technical scope (see Section 5).

**Group size:** 4 students. Each student owns exactly one primary business component end-to-end (backend, database, React, Flutter, tests, and one distinct role in the shared Agentic AI subsystem).

**Team & Component Ownership:**

| Student | Component | Agentic AI Role |
|---|---|---|
| Dias H.N.P.K. | A — Load Management | Agent 1 — Planner/Coordinator |
| D.B.A.H.W. Bandara | B — Agency & Fleet Management | Agent 2 — Domain Analysis |
| Ratnaweera O.V. (**Team Leader**) | C — Matching & Trip Execution | Agent 3 — Matching & Pricing |
| Balasooriya B.K.N.N. | D — Billing & Admin Oversight | Agent 4 — Validation & Safety |

**Group leader:** Ratnaweera O.V. — responsible for the single Course Web submission.
**Repository:** https://github.com/OmiraRathnaweera/freightLink

---

## 2. Mandatory Technology Stack

| Layer | Technology |
|---|---|
| Backend | C# / ASP.NET Core Web API |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Web frontend | React (functional components, hooks, router) |
| Mobile frontend | Flutter / Dart |
| Agentic AI | LangGraph-style sequential multi-agent workflow with one conditional branch (the Admin's Approve/Reject/Revise decision) |
| Version control / CI | GitHub + GitHub Actions (backend build + test on every push/PR to main) |

**Integration rule:** React and Flutter communicate **only** with the ASP.NET Core API. The Agentic AI subsystem is called internally by ASP.NET Core and is never called directly by either client.

### Deployment Architecture (resolved)

| Component | Host | Notes |
|---|---|---|
| ASP.NET Core API | AWS free-tier EC2 or Azure free VPS | Dockerized |
| React | Same VPS (Dockerized, served via nginx) | Configured to call the deployed API |
| PostgreSQL | AWS free-tier EC2 or Azure free VPS | Dockerized, migrations run on startup |
| Agentic AI | Separate **Python service** (FastAPI + LangGraph), Dockerized | Called internally by ASP.NET Core only — never by React/Flutter directly. Per the spec's own allowance, this may run alongside the API or locally with documented setup/startup instructions, whichever is most reliable for the live demo. |

Splitting the Agentic AI subsystem into its own Python service (rather than shoehorning LangGraph-equivalent logic into C#) is itself a clean ADR entry — justify it as "best tool for the job" while keeping the mandatory backend rule intact (Python service is internal-only, called by ASP.NET Core).

---

## 3. User Roles

| Role | Primary client | Responsibilities |
|---|---|---|
| **Shipper** | React (primary — see Section 4) / Flutter (quick actions) | Posts loads, tracks status, views invoices, raises disputes, **reviews and approves/rejects the AI-recommended agency match** (approval sends a job proposal to the agency — see Section 4) |
| **Agency Staff (Dispatcher)** | Flutter | Manages fleet/compliance, reviews and accepts/declines AI-recommended job proposals sent to the agency, assigns a driver/vehicle to an accepted load |
| **Driver** | Flutter | Distinct login from Agency Staff; views assigned trips, updates trip status, captures proof of delivery |
| **Admin** | React | Verifies agency KYC/compliance (Component B), monitors system-wide analytics, and adjudicates disputes. **Does not** approve/reject AI-proposed matches — see Section 4 for the resolved decision. |

This gives the system four roles in total, exceeding the spec's minimum of three, with each role scoped narrowly enough to make role-based authorization straightforward to implement and easy to defend in the viva.

*(Note: this table previously listed Flutter as the Shipper's primary client, which conflicted with Section 4's decision that React is primary. Corrected here.)*

---

## 4. Domain Model — Key Decisions

- **Carrier = Agency.** A Carrier in this system is always a company that owns/manages a fleet, not an individual driver.
- **Shippers are primarily businesses, not individuals.** Most freight shippers in Sri Lanka are companies. React — not Flutter — is therefore the *primary* interface for the Shipper role: full load management, history, document handling, and the **AI match approval console** all live in React. Flutter's shipper-facing screens are deliberately lightweight: status viewing, notifications, and a simplified quick-post-load action (retained specifically to preserve the required cross-platform workflow pattern — see Section 8).
- **Proof of Pickup, alongside Proof of Delivery.** When a vehicle is dispatched from the yard, Agency Staff capture a photo as pickup evidence; the Driver later captures Proof of Delivery at the destination. Both are stored as `TripEvidence` records under Component C (see Section 5), and both transitions are hard-blocked at the API level without the required evidence.
- **Driver has a distinct login, separate from Agency Staff.** Agency Staff (dispatchers) manage the fleet and accept loads on the agency's behalf; once a load is accepted, they assign a specific driver and vehicle. The Driver then logs in under their own account to view the assigned trip, update its status, and capture proof of delivery.
- **Fixed yard coordinates.** Each Agency has one fixed yard location (`yardLat`, `yardLng`) stored in the database. This is the coordinate used for all matching and ETA calculations — no live GPS tracking of individual vehicles/drivers.
- **No real-time location tracking.** Trip status updates are driver/agency-triggered snapshots, not continuous GPS streaming. This keeps the mobile app simple and makes the AI demo 100% reproducible during evaluation.
- **Real payment gateway (updated decision).** Component D now integrates **PayHere** (sandbox/test mode) for actual invoice payment — see Section 6. This is a scope *addition*, not required by the rubric (one integration already satisfies Section 11), taken on deliberately for domain depth and fits the system's Sri Lanka-first framing.
- **AI match approval sits with the Shipper, not the Admin (updated decision).** The shipper reviews Agent 4's proposed agency assignment (price deviation shown as decision-support context) and approves or rejects it — it's their cargo and cost, so approval authority sits with them rather than a platform overseer. This narrows Admin's role to two functions only: agency KYC/verification (Component B) and system-wide analytics. **Cross-component note:** the recommendation is *computed* by Agents 3–4 (Component C/D), but *displayed and actioned* on the shipper's React console (Component A) — document this shared contract explicitly in both components' individual reports, the same way the Shipper/Agency registration DTO overlap is documented.
- **No agency bidding — AI recommends one agency, agency confirms via a job proposal (updated decision).** The system does not run a competitive bidding process. The Agentic AI pipeline is triggered immediately when the shipper posts a load, evaluates all eligible agencies (Agent 2), and recommends a single best-fit candidate (Agent 3). Once the **shipper approves**, the backend creates an `Assignment` in a `Proposed` state and sends it to the recommended agency as a job proposal — the agency then **accepts or declines** it (Flutter, Component C) before anything is finalized. This is a second, genuine human decision point on the other client, strengthening the required cross-platform workflow (Section 8) beyond a single approval step. Removing bidding also **simplifies scope**: no bid entity, no competitive-ranking-of-bids logic, no bid-expiry handling.
  - `Assignment` state machine: `Proposed → Accepted` / `Declined`.
  - `Trip` (created only once `Assignment` is `Accepted`): `Assigned → PickedUp → InTransit → Delivered`.
  - **On decline (updated decision — auto-cascade with a retry cap):** the system emails the shipper immediately ("this agency declined, we're finding another"), then **automatically re-triggers** Agents 2–4 to find a new candidate, excluding any agency that already declined this specific load. The new run still pauses unconditionally for shipper approval (Section 7's human-in-the-loop rule is unaffected — auto-retry only automates *re-running the pipeline*, never *skipping approval*). Once the new recommendation is ready, the shipper is emailed a second time ("new match found, please review"). **Retry cap:** capped at **3 automatic attempts per load** (tunable) — beyond that, the system stops auto-retrying, records a safe failure, flags the load for manual review, and emails the shipper that no automatic match was found. The cap exists specifically so a load with no genuinely available agencies fails safely instead of looping indefinitely or spamming the shipper.
- **Three third-party integrations for the whole system:** OpenRouteService (routing/distance/ETA — reused as the Agentic AI's tool-use call, required, satisfies the rubric's minimum), a payment gateway (invoice payment, additional depth on Component D), and a transactional email API (decline/new-match/no-match notifications, additional depth — see Section 6).

### Single Yard per Agency

Each Agency has exactly **one fixed yard** (`yardLat`, `yardLng`). This version of the system deliberately does **not** support multiple yards per agency.

**Reasons:**

1. **Matches market reality.** The majority of small and medium freight agencies in Sri Lanka operate from a single fixed yard or depot, so this models the typical case rather than an edge case.
2. **Keeps the Agentic AI matching workflow simple, deterministic, and reliable.** A single, fixed coordinate per agency means Agent 3's `get_route_and_eta` call always has one unambiguous origin — important for a reproducible, low-risk live demonstration.
3. **Avoids complexity that would not earn additional marks.** Supporting multiple yards would require a new `AgencyYard` table, yard-selection logic inside the matching agent, and potentially multiple OpenRouteService calls per candidate agency — none of which is required by the marking scheme, and all of which increases the surface area for bugs during evaluation.
4. **Protects the 8-week timeline and viva-defensibility.** A single, well-understood data point per agency is easy for Student B (owner of Component B and the `Agency` entity) to explain, test, and modify live under viva questioning.

This decision will be recorded as **ADR-00X: Single Yard per Agency** in the group's Architecture Decision Record (Section 14.2 of the spec), alongside the existing state-management, Agentic AI framework, database schema, and deployment platform entries.

**Future extension (out of current scope):** if the system were extended beyond this assignment, a separate `AgencyYard` table (one Agency → many Yards) could be introduced, with the Domain Analysis Agent selecting the nearest or most suitable yard for each match rather than assuming a single fixed location.

---

## 5. The Four Components

Each component is balanced in size: 2–3 owned entities, 4+ API endpoints, one business-specific operation beyond CRUD, one status workflow, dedicated React and Flutter screens, and one distinct Agentic AI role.

### Component A — Load Management (Shipper side) — Owner: Dias H.N.P.K.
| | |
|---|---|
| **Backend** | `LoadsController`: create/edit/cancel load; search/filter/sort/paginate; `POST /loads/{id}/estimate` (price estimator — business op beyond CRUD). `LoadFilesController` (or nested route): upload/list/delete files attached to a load. Owns the shared `IEmailService` (transactional email — Section 6) and the retry-cap check used by Agent 1 on re-trigger (Section 7). |
| **DB entities** | `Load`, `LoadStatusHistory`, `File` |
| **React — primary shipper interface** | Full load management console: create/edit/cancel load (rich form), my-loads dashboard (search/filter/sort/pagination), load detail with status timeline, history, and attached documents (manifest, invoice, other). Also hosts the **AI match approval screen**: shows Agent 4's proposed agency, ETA, price, and deviation % from the shipper's own estimate, with Approve/Reject/Revise actions (decision written to Component D's `ApprovalDecision` table via a role-guarded endpoint — see Section 7). Role-based navigation exposes an Admin view on the same underlying screens (all-loads dashboard, cancel/flag), scoped by permissions — one codebase, two role-gated views. |
| **Flutter — lightweight shipper interface** | Quick-post-load form (simplified, date/time picker for pickup window, optional **cargo photo** at posting via camera/image picker), my-loads list (read-focused), load status timeline, push notifications on status change |
| **CRUD + workflow** | `Draft → Posted → Matched → ProposalSent → Assigned → InTransit → Delivered → Closed`. On agency decline, status reverts from `ProposalSent` to `Matched` and the matching workflow **auto-retriggers** (capped at 3 attempts) — see Section 4 and Section 7. |
| **Agentic AI role** | **Agent 1 — Planner/Coordinator**: receives the match objective, builds the structured multi-step plan, delegates to Agents 2–4, owns overall workflow status |

> **Compliance note:** the Flutter quick-post-load action is kept deliberately, even though React is the shipper's primary tool day-to-day. The assignment's required cross-platform workflow pattern must begin in one client and be reviewed/approved in the other. Keeping load-posting possible in Flutter preserves the Flutter → API → PostgreSQL → Agentic AI → React (**shipper approval**) → Flutter (status) pattern used for the officially demonstrated workflow (Section 8).

**`File` entity detail:**

| Field | Notes |
|---|---|
| `id` | Primary key |
| `loadId` | FK → `Load` (one Load has many Files) |
| `fileName` | Original file name |
| `fileUrl` | Stored file reference |
| `fileType` | Enum: `Manifest`, `Invoice`, `CargoPhoto`, `Other` |
| `uploadedByUserId` | FK → `User` |
| `uploadedAt` | Timestamp |

A dedicated, related table rather than attachment fields on `Load` itself — normalizes cleanly (a load can have any number of documents or photos) and gives Component A its own small, explainable one-to-many relationship for the ER diagram and viva. Same widget/upload endpoint pattern as Component C's `TripEvidence` capture — good code-reuse talking point across components.

**Price estimation formula (resolved):**

`estimatedPrice = baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)`

- `distanceKm` here is the straight-line (haversine) distance between the load's pickup and destination coordinates — computed internally, no external API call needed for this rough, pre-matching estimate.
- `baseFare`, `ratePerKm`, `ratePerKg` are shared configuration constants (not a new DB entity — a small config/appsettings value), reused by both this endpoint and Agent 3's pricing calculation below, so both sides of the later approval comparison are computed consistently.
- Exact constant values are an implementation detail to tune during Week 2–3, not an architectural decision.

### Component B — Agency & Fleet Management — Owner: D.B.A.H.W. Bandara
| | |
|---|---|
| **Backend** | `AgenciesController`: register agency (incl. yard lat/lng), manage vehicles/drivers, `GET /agencies/expiring-compliance` (business op beyond CRUD) |
| **DB entities** | `Agency`, `Vehicle`, `Driver` (linked to its own User account for login), `ComplianceDoc` |
| **React (admin)** | Agency verification queue, compliance-doc review, approve/suspend agency |
| **Flutter (agency staff)** | Agency profile + fleet setup, driver onboarding (creates a driver login), compliance doc upload (camera), availability management |
| **CRUD + workflow** | Agency: `Pending → Verified → Active → Suspended` |
| **Agentic AI role** | **Agent 2 — Carrier/Domain Analysis**: given a load's requirements, queries agency/compliance/fleet data and returns a ranked list of eligible candidate agencies |

### Component C — Matching & Trip Execution — Owner: Ratnaweera O.V. (Team Leader)
| | |
|---|---|
| **Backend** | `AssignmentsController` / `TripsController`: create proposal on shipper approval (concurrency-safe), agency accept/decline endpoints (decline calls into Component A's `IEmailService` and triggers Agent 1's re-run — cross-component call, document in both reports), trip status transitions, evidence upload endpoints (pickup + delivery), plus backend-only `IRouteService` wrapping OpenRouteService |
| **DB entities** | `Assignment`, `Trip`, `TripEvent`, `TripEvidence` |
| **React (admin)** | Active trips dashboard, trip detail with pickup/delivery evidence photos, status/timeline monitor |
| **Flutter** | Agency Staff: **job proposal inbox** (accept/decline AI-recommended assignments sent after shipper approval), assign a driver/vehicle once accepted, capture **Proof of Pickup** at dispatch from the yard (camera). Driver (own login): view assigned trip, update trip status (location snapshot), capture **Proof of Delivery** on completion (camera) |
| **CRUD + workflow** | `Assignment`: `Proposed → Accepted` / `Declined` (agency's own decision — see Section 4). `Trip` (created only once `Assignment` is `Accepted`): `Assigned → PickedUp → InTransit → Delivered`. Hard-blocked business rule: the API **rejects** (e.g. `400 Bad Request`) a transition to `PickedUp` unless a Proof-of-Pickup record is attached, and rejects a transition to `Delivered` unless a Proof-of-Delivery record is attached — a clean, testable deterministic-validation case, not just a warning. |
| **Agentic AI role** | **Agent 3 — Matching & Pricing (tool-use agent)**: takes the top candidate agency from Agent 2, calls the allow-listed `get_route_and_eta` tool using the agency's **yard coordinates** as origin and the load's pickup location as destination, then computes the proposed price using the same `baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)` formula as Component A's estimate — here using the real ORS-routed `distanceKm` instead of the straight-line one — and produces the structured assignment proposal (created as `Assignment.Status = Proposed` once the shipper approves) |

**`TripEvidence` entity detail:**

| Field | Notes |
|---|---|
| `id` | Primary key |
| `tripId` | FK → `Trip` |
| `evidenceType` | Enum: `PickupProof`, `DeliveryProof` |
| `photoUrl` | Uploaded image reference |
| `capturedByUserId` | FK → `User` — Agency Staff for `PickupProof`, the assigned Driver for `DeliveryProof` |
| `capturedAt` | Timestamp |
| `latitude` / `longitude` | Optional — reuses the same location-snapshot mechanic already used for trip status updates |

Pickup and delivery evidence share one entity distinguished by `evidenceType`, so the same capture widget and upload endpoint are reused for both — low implementation cost for a second genuinely business-meaningful camera moment.

### Component D — Billing & Admin Oversight — Owner: Balasooriya B.K.N.N.
| | |
|---|---|
| **Backend** | `InvoicesController` / `DisputesController`: auto-generate invoice on delivery, `POST /invoices/{id}/checkout` (creates a gateway checkout session — business op beyond CRUD), `POST /webhooks/payment` (gateway callback — verifies signature, updates payment status), dispute raise/resolve, admin summary endpoints |
| **DB entities** | `Invoice`, `Dispute` |
| **React (admin)** | Financial dashboard, invoice list showing real gateway-driven payment status, dispute resolution screen, system-wide analytics view |
| **Flutter (shipper/agency)** | Invoice view with a **Pay Now** action (opens gateway checkout, returns to app on completion), raise-a-dispute form, notifications |
| **CRUD + workflow** | Invoice: `Draft → Issued → PaymentPending → Paid` / `Failed` (driven by the payment gateway's webhook, not simulated); Dispute: `Raised → UnderReview → Resolved` |
| **Agentic AI role** | **Agent 4 — Validation/Safety**: applies deterministic checks (compliance still valid, price within sane bounds, tool call succeeded) to Agent 3's proposal, calculates the **% deviation** between Agent 3's proposed price and the shipper's own estimate (Component A), and **always** pauses the workflow for human approval — resolved on the **shipper's** React console (Component A), which displays the deviation percentage as decision-support context alongside Approve/Reject/Revise. The approval endpoint itself still lives in this component's workflow controller (writes to `ApprovalDecision`), guarded to accept the decision only from the load's own shipper — a cross-component API contract with Component A, document it explicitly in both individual reports |

**`Invoice` payment fields (updated):**

| Field | Notes |
|---|---|
| `paymentGatewayRef` | Transaction/session ID returned by the gateway |
| `paymentStatus` | Enum: `Pending`, `Paid`, `Failed`, `Refunded` |
| `paidAt` | Timestamp, nullable until payment confirmed |

The webhook endpoint (signature verification, idempotency handling, safe-failure logging on invalid/failed callbacks) mirrors the same failure-handling discipline already established for the OpenRouteService tool call — a consistent, easy-to-explain pattern across both integrations for the viva.

---

## 6. Third-Party Integrations

| Integration | Used for | Called from | Notes |
|---|---|---|---|
| **OpenRouteService** | Distance + ETA calculation between an agency's yard and a load's pickup location | ASP.NET Core backend only (never React/Flutter directly) | Free tier, worldwide coverage including Sri Lanka. Doubles as the Agentic AI's allow-listed tool. API key stored in environment variables / .NET user-secrets only — never committed to Git. |
| **PayHere (sandbox)** | Invoice payment on Component D: create checkout session, confirm payment via webhook | ASP.NET Core backend only (never React/Flutter directly) | Sandbox/test mode only, no real money. Merchant ID + secret stored in environment variables / .NET user-secrets only — never committed to Git. Webhook (notify URL) signature verified (MD5 hash check) before trusting any payment-status update. |
| **Transactional email API** (e.g. Brevo or Resend, free tier) | Shipper notifications on the auto-retry loop: agency declined, new match found, no auto-match found (Section 7) | ASP.NET Core backend only, via a shared `IEmailService` — never React/Flutter directly | Free tier (hundreds of emails/day, well above demo needs). Owned/implemented in Component A (already owns shipper notifications and Agent 1's workflow-status role); called cross-component from Component C's decline endpoint — document this contract in both individual reports, same as the approval-endpoint contract in Section 5. API key stored in environment variables / .NET user-secrets only. **Demo-reliability note:** consider a sandbox/logging mode (e.g. Mailtrap, or logging the email payload instead of sending) as a network-independent fallback for the live viva, the same way Ollama backs up the hosted LLM. |

**Three external integrations now.** OpenRouteService alone already satisfies the rubric's minimum of one; the payment gateway and the email API are additional depth, not a compliance requirement. Location tracking remains simulated (Section 4) — that simplification is unaffected by this decision.

---

## 7. Agentic AI Subsystem — Complete Workflow

**Framework:** LangGraph-style sequential pipeline with one conditional branch — the branch now sits at the Shipper's decision (Approve vs. Reject/Revise), since human approval itself is unconditional on every run.

**Trigger:** Shipper posts a load (React or Flutter, Component A) → backend calls `POST /workflows/match` with the load ID, immediately — no bidding or waiting period.

```
Agent 1 — Planner/Coordinator (Dias H.N.P.K.)
   │  Builds structured multi-step plan, delegates to Agent 2
   ▼
Agent 2 — Carrier/Domain Analysis (D.B.A.H.W. Bandara)
   │  Queries agency + compliance + fleet data
   │  Returns ranked list of eligible candidate agencies
   ▼
Agent 3 — Matching & Pricing / tool-use (Ratnaweera O.V.)
   │  Calls allow-listed tool: get_route_and_eta(
   │      origin = candidate agency's yard lat/lng,
   │      destination = load's pickup lat/lng)
   │  Computes proposed price + ETA from distance/duration
   │  Produces structured assignment proposal
   ▼
Agent 4 — Validation/Safety (Balasooriya B.K.N.N.)
   │  Deterministic checks: compliance valid? price within bounds? tool call succeeded?
   │  Calculates % deviation between proposed price and shipper's own estimate
   │  (decision-support context — not a gating condition)
   ▼
PAUSE workflow — every run always stops here for approval, unconditionally
   │
   ▼
Shipper approves / rejects / revises
(React approval console, Component A — displayed here, computed by Agents 3–4 /
 Component C–D; decision writes to Component D's ApprovalDecision table)
   │
   ├── Approve ────► Assignment created (Status = Proposed), job proposal
   │                 sent to the recommended agency (Component C)
   │                    │
   │                    ▼
   │                 Agency Staff accepts / declines (Flutter, Component C)
   │                    │
   │                    ├── Accept  ──► Assignment → Accepted, Trip created,
   │                    │               shipper + agency see updated status
   │                    │               → Auditable Result (Success)
   │                    │
   │                    └── Decline ──► Assignment → Declined
   │                                    Email #1 to shipper: "agency declined"
   │                                       │
   │                                       ▼
   │                                    Retry cap reached? (max 3 auto-attempts/load)
   │                                       │
   │                                       ├── No  ──► Re-run Agents 2–4, excluding
   │                                       │           declined agencies ─ ↻ loops
   │                                       │           back to "PAUSE workflow" above
   │                                       │           (still requires shipper approval —
   │                                       │           Email #2: "new match found")
   │                                       │
   │                                       └── Yes ──► Safe failure: load flagged for
   │                                                   manual review, Email: "no auto-
   │                                                   match found, please check the app"
   │                                                   → Safe Failure (Recorded)
   │
   └── Reject/Revise ────► Safe failure: rejection recorded, no proposal sent,
                            load reopened for re-matching
                            → Safe Failure (Recorded)
```

### Tool contract — `get_route_and_eta`

| | |
|---|---|
| **Input (validated)** | `origin_lat, origin_lng, destination_lat, destination_lng` — all required, range-checked before calling |
| **Output (structured)** | `{ distance_km: number, duration_minutes: number, summary: string }` |
| **Access** | Called only by Agent 3, server-side only |
| **Failure handling** | Timeout → retry once with backoff → on repeated failure, return `toolCallSuccess: false`; Agent 4 treats this as "cannot validate — hold for manual review," never a silent guessed fallback |
| **Rate limits** | Free-tier ORS limits are per-minute; workflow is triggered per-load on demand, not in a bulk/live-tracking loop, so limits are not a practical constraint |

### Shared workflow state (PostgreSQL)

| Table | Purpose | Written by |
|---|---|---|
| `AgentWorkflowRun` | Workflow ID, objective, plan, overall status, **attempt number** (for the decline-retry cap — see Section 7) | Agent 1 (Dias H.N.P.K.) |
| `AgentStep` | Per-agent input/output log, one row per agent per run | All four students, one row each |
| `ToolCall` | OpenRouteService call input/output, success/failure | Agent 3 (Ratnaweera O.V.) |
| `ApprovalDecision` | Decided-by, action taken, reason | Agent 4 computes the proposal (Balasooriya B.K.N.N.); **Shipper** submits the decision via Component A's console, written through Component D's role-guarded endpoint |

Each student can point directly to the database rows their own agent produced — clean, individually attributable viva evidence.

### Rubric compliance — minimum assessed workflow

| Requirement (Section 9.1) | Satisfied by |
|---|---|
| Domain objective → structured multi-step plan | Agent 1 |
| Delegation to distinct agent roles | Agents 1→2→3→4, each with a distinct contract |
| Allow-listed tool, validated inputs, structured outputs | Agent 3 + `get_route_and_eta` |
| Persisted workflow state | `AgentWorkflowRun`, `AgentStep`, `ToolCall`, `ApprovalDecision` |
| Deterministic validation | Agent 4's business-rule checks |
| Human approval on a high-impact action | Every run unconditionally pauses before a job proposal is sent to an agency, resolved on the shipper's React console (Component A), backed by Component D's approval endpoint |
| Auditable result or safe, recorded failure | Assignment accepted by the agency (full trace, Trip created), or a recorded shipper rejection, agency decline (with bounded auto-retry, capped at 3 attempts), or tool failure |

---

## 8. Required Cross-Platform Demo Workflow

At least one workflow must begin in one client, pass through the full stack, require approval in the other client, and return status to the initiating user. This system's primary demo workflow now has **two** human decision points across both clients, which is a stronger demonstration than a single approval step:

```
1. React/Flutter    Shipper posts a load                          (Component A)
2. ASP.NET Core     Authenticates, validates, applies rules
3. PostgreSQL       Stores load + audit fields
4. Agentic AI       4-agent pipeline plans, matches, prices, validates — triggered immediately, no bidding
5. React            Shipper reviews the recommendation, approves/rejects/revises  (Component A console; decision recorded via Component D)
6. ASP.NET Core     On approval, creates Assignment (Proposed) and sends a job proposal to the recommended agency
7. Flutter          Agency Staff accepts or declines the proposal              (Component C)
8. Shared status    Backend finalizes Assignment + creates Trip on accept (or reopens the load on decline); shipper and agency both see updated status
```

**Secondary (supporting) workflow — pickup confirmation:** Agency Staff capture Proof of Pickup at dispatch (Flutter, Component C) → backend validates and stores the evidence, unblocking the transition to `PickedUp` → React admin monitor and the shipper's Flutter status view both reflect the update. This is a useful additional demonstration of full-stack sync during the demo, though the assignment's *officially assessed* end-to-end workflow remains the AI-matching-approval-proposal one above.

---

## 9. Deliberate Scope Simplifications

These are intentional, documented decisions to protect the 8-week timeline — not omissions.

1. **Carrier = Agency, fixed yard coordinates** — no per-vehicle/per-driver live GPS; matching uses one fixed location per agency.
2. **No live GPS tracking** — trip location updates are driver/agency-triggered snapshots, not continuous streaming.
3. **OpenRouteService reused for both the UI and the Agentic AI's tool-use**, rather than a separate matching-related integration per component.

*(Previously simplified out, now added back by team decision: the payment gateway — see Sections 4 and 6. This is an intentional scope **addition**, not required by the rubric, so it's worth re-checking timeline impact in Week 6–7 if the team feels stretched.)*

---

## 10. Open Decisions — To Be Confirmed by the Team

| # | Decision | Status |
|---|---|---|
| 1 | Student name → Component (A/B/C/D) assignment | ✅ Resolved — see Team & Component Ownership table in Section 1 |
| 2 | LLM access: paid API key vs. free local model (e.g. Ollama) for the agents | ✅ Resolved — **NVIDIA NIM** (`build.nvidia.com`), free tier: no credit card, OpenAI-compatible endpoint, ~40 requests/minute (well above what a single 4-agent workflow run needs). Integrated into LangGraph via the official `langchain-nvidia-ai-endpoints` package (`ChatNVIDIA`) or a plain OpenAI-compatible client pointed at NVIDIA's base URL. Satisfies the spec's "no-cost services" rule (Section 14) without needing a paid OpenAI/Anthropic key. **Ollama kept as a locally-tested fallback** to cover the network-dependency risk of a hosted API during the live viva demo. |
| 3 | Deployment targets | ✅ Resolved — AWS free EC2 / Azure free VPS, Dockerized, for API + React + PostgreSQL; Agentic AI as a separate Dockerized Python service. See "Deployment Architecture" in Section 2. |
| 4 | Pricing formula | ✅ Resolved — distance + weight based (`baseFare + distanceKm×ratePerKm + weightKg×ratePerKg`). See Component A and Agent 3 in Section 5. |
| 5 | Approval trigger | ✅ Resolved — Agent 4 always pauses for approval on every run; the >X% deviation from the shipper's own estimate is calculated and shown to the Admin as decision-support context, not as a gating condition. See Section 7. |
| 6 | State management | ✅ Resolved — React: Redux Toolkit + Context API used together (Redux Toolkit for global/shared state such as auth and agent-workflow monitoring; Context for smaller, localized component trees). Flutter: **Provider** — recommended for its low learning curve, wide documentation, and easier explainability under viva pressure; happy to swap to Riverpod or Bloc if the team prefers. |
| 7 | Auth model | ✅ Resolved — JWT **access token (5 min)** + **refresh token (30 days)**. |
| 8 | GitHub repository + nominated group leader | ✅ Resolved — Leader: Ratnaweera O.V. Repo: https://github.com/OmiraRathnaweera/freightLink |
| 9 | Who approves the AI-recommended agency match: Admin or Shipper | ✅ Resolved — **Shipper** approves/rejects/revises (their cargo, their cost). Admin retains agency KYC/verification (Component B), system-wide analytics, and dispute adjudication; Admin has no AI-match approval authority. See ADR-016. |
| 10 | Competitive bidding vs. AI-recommended single agency + proposal | ✅ Resolved — **no bidding.** The AI is triggered immediately on posting, recommends one best-fit agency, the shipper approves, and the system sends that agency a job proposal which it accepts/declines. See Sections 4, 5 (Component C), 7, 8. **On decline:** ✅ Resolved — **automatic cascade**, capped at 3 attempts/load, with email notifications to the shipper at each decline and each new match (Sections 4, 6, 7). Beyond the cap, the load is flagged for manual review rather than retrying indefinitely. *(The retry-cap number itself — 3 — is a tunable implementation detail, not an architectural constraint; adjust freely during Week 5–6 build-out.)* |

*(Also resolved earlier: Driver has a distinct Flutter login separate from Agency Staff — see Sections 3 and 4.)*

---

## 11. 8-Week Delivery Plan

| Week | Focus | Key deliverables |
|---|---|---|
| 1 | Setup & design | Repo + CI skeleton, ER diagram, API contracts, Agentic AI architecture, ADR drafts, role/auth design |
| 2 | Backend core | Auth, EF Core models/migrations for all 4 components, basic CRUD live |
| 3 | Business logic + client shells | Full component logic per owner; React admin shell; Flutter shell + auth |
| 4 | Client features | React admin views complete; Flutter operational flows complete per component |
| 5 | Agentic AI build | All 4 agents implemented, OpenRouteService tool integration, state persistence, validation |
| 6 | Integration | Approval UI in React, status sync to Flutter, full cross-platform workflow working end-to-end |
| 7 | Testing + deployment | Unit/integration/E2E/agent-evaluation/performance tests, CI green, deploy all components |
| 8 | Docs + rehearsal | README, ADRs finalized, consolidated report, AI logs/reflections, demo video, viva dry-run |

**Highest-risk items:** agent orchestration slipping late (mitigate by building a stub pipeline as early as Week 3) and mobile camera/upload permissions eating time late (test on a real device by Week 4).

---

## 12. Marking Rubric Alignment Snapshot

| Rubric area | How this plan addresses it |
|---|---|
| 4 primary components (Section 3) | Components A–D, one per student, full-stack ownership each |
| ≥3 user roles (Section 4.1) | Shipper, Agency Staff, Driver, Admin (four roles, exceeding the minimum) |
| ≥4 distinct agents (Section 9.1) | Agents 1–4, distinct contracts, tools, and DB writes |
| Human-in-the-loop approval (Section 9.1) | Unconditional pause on every run in Agent 4, resolved on the shipper's React console (Component A), backed by Component D's approval endpoint |
| ≥1 third-party integration (Section 11) | OpenRouteService (backend-only, required) + PayHere sandbox (backend-only, additional depth on Component D) |
| Cross-platform workflow (Section 10) | Flutter → API → DB → Agentic AI → React → shared status (Section 8 above) |
| Meaningful device feature(s) (Section 8) | Camera: cargo photo at posting (Component A), Proof of Pickup (Agency Staff) + Proof of Delivery (Driver) (Component C), compliance-doc upload (Component B). Date/time picker (Component A). Notifications (Component D). |
| ADR (Section 14.2) | State management (React/Flutter), Agentic AI framework, DB schema for agent state, deployment platform, and the agency-vs-driver domain decision are all ADR-worthy entries |

---

## 13. Documentation Checklist (per Final Student Checklist, Section 20)

- [ ] All 4 primary components completed, one per student
- [ ] JWT auth + role-based authorization
- [ ] React and Flutter working through the shared API
- [ ] 4 specialized agents with controlled tools and structured state
- [ ] Validation, observability, human approval implemented
- [ ] OpenRouteService integration completed and documented
- [ ] GitHub Actions CI building and running tests
- [ ] React, ASP.NET Core, PostgreSQL deployed; Flutter APK generated
- [ ] Git contribution visible for every member
- [ ] Demonstration and viva prepared with no external AI use
- [ ] ADRs completed with justified framework/architecture decisions
- [ ] One consolidated report: Group Report + all Individual Reports + diagrams + links
- [ ] AI usage declared (individual logs + group declaration); no secrets committed to GitHub
- [ ] Contribution statements, AI logs, group declaration, individual reflections included
