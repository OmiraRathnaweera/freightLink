# Architecture Decision Records (ADR)
## FreightLink / FreightMatch LK

**Module:** SE3090 – Software Engineering Frameworks | Assignment 1
**Programme:** BSc (Hons) in Information Technology, Specializing in SE/AI | Year 3, Semester 1, 2026
**Repository:** https://github.com/OmiraRathnaweera/freightLink

### Group Members & Component Ownership

| Student | Primary Component | Agentic AI Role |
|---|---|---|
| Dias H.N.P.K. | Component A — Load Management | Agent 1 — Planner/Coordinator |
| D.B.A.H.W. Bandara | Component B — Agency & Fleet Management | Agent 2 — Domain Analysis |
| Ratnaweera O.V. (**Team Leader**) | Component C — Matching & Trip Execution | Agent 3 — Matching & Pricing |
| Balasooriya B.K.N.N. | Component D — Billing & Admin Oversight | Agent 4 — Validation & Safety |

---

## About This Document

An Architecture Decision Record (ADR) captures the context, the decision made, and the consequences of a significant technical or architectural choice at the point it was made. As introduced in Lecture 01 of SE3090 and required by Section 14.2 of the Assignment 1 specification, this document is the group's primary **written evidence for Learning Outcome 4** ("Select appropriate frameworks, tools, and agentic AI-assisted approaches to meet specific project and industry requirements").

Each ADR below records one decision the FreightLink team made while designing and building the platform. These records will be referenced directly during the viva — every team member should be able to explain the context, the decision, and the trade-offs of any ADR related to their owned component.

All decisions recorded here reflect the current, agreed state of the project as described in the project README.

---

## ADR-001: Carrier Model = Agency (Not Independent Drivers)

**Status:** Accepted
**Date:** August 2026

### Context
Uber Freight, the platform that inspired this project, is built around independent owner-operator drivers accepting individual loads. However, the Sri Lankan freight market is structured differently: freight movement is dominated by agencies/companies that own and manage vehicle fleets from fixed yards or depots, rather than independent owner-operators.

### Decision
The system models the "Carrier" role as an **Agency** — a company entity that owns/manages a fleet of vehicles and employs drivers — rather than modelling independent driver-owned trucks as the primary matching unit.

### Consequences
**Positive**
- Reflects the actual structure of the Sri Lankan freight market, giving the project genuine domain credibility.
- Simplifies the technical scope: matching and eligibility logic operates at the agency level (compliance, fleet capacity) rather than needing to model thousands of independent, loosely-regulated individual operators.
- Creates a clean two-tier user structure within Component B (Agency Staff who manage the fleet, and Drivers who execute trips), which naturally supports the requirement for distinct user roles.

**Negative**
- Diverges from the literal Uber Freight reference model, which must be explained clearly in the viva and README as a deliberate localisation decision, not a misunderstanding of the brief.
- Individual owner-operators (a real segment of the market) are out of scope for this version of the system.

### Alternatives Considered
- **Independent owner-operator model (literal Uber Freight replica):** Rejected — poorly matches local market structure and would complicate compliance/eligibility logic without added marking benefit.
- **Hybrid model supporting both agencies and independent drivers:** Rejected — adds schema and matching-logic complexity with no rubric requirement to justify it within an 8-week timeline.

---

## ADR-002: Single Yard per Agency (No Multi-Yard Support)

**Status:** Accepted
**Date:** August 2026

### Context
Once the Agency carrier model (ADR-001) was adopted, a decision was needed on how many physical dispatch locations ("yards") each agency could have, since this directly determines the origin coordinate used by the Agentic AI's routing/ETA tool call and by the matching workflow generally.

### Decision
Each Agency has exactly **one fixed yard**, stored as `yardLat` / `yardLng` fields on the `Agency` entity. This system deliberately does **not** support multiple yards per agency.

### Consequences
**Positive**
- Matches market reality: the majority of small and medium freight agencies in Sri Lanka operate from a single fixed yard or depot, so the model represents the typical case rather than an edge case.
- Keeps the Agentic AI matching workflow simple, deterministic, and reliable — Agent 3's `get_route_and_eta` tool call always has one unambiguous origin, which is important for a reproducible, low-risk live demonstration.
- Avoids complexity that would not earn additional marks: multiple yards would require a new `AgencyYard` table, yard-selection logic inside the matching agent, and potentially multiple OpenRouteService calls per candidate agency — none of which is required by the marking scheme, and all of which increases the surface area for bugs during evaluation.
- Protects the 8-week timeline and viva-defensibility: a single, well-understood data point per agency is easy for the Component B owner to explain, test, and modify live under viva questioning.

**Negative**
- Does not represent larger agencies that genuinely operate from multiple depots — a known and accepted simplification.

### Alternatives Considered
- **One-to-many `AgencyYard` table with yard-selection logic in Agent 2/3:** Rejected for this assignment — noted explicitly as a documented future extension, not attempted now, to protect timeline and demo reliability.

### Future Extension (Out of Current Scope)
If extended beyond this assignment, a separate `AgencyYard` table (one Agency → many Yards) could be introduced, with the Domain Analysis Agent (Agent 2) selecting the nearest or most suitable yard for each match rather than assuming a single fixed location.

---

## ADR-003: Shipper's Primary Interface is React (Flutter is Lightweight for Shippers)

**Status:** Accepted
**Date:** August 2026

### Context
The specification requires React to primarily support administrative/staff/dashboard functions and Flutter to primarily support user-facing/operational workflows, while also requiring "meaningful and different purposes" for each client. Most freight shippers in the Sri Lankan market are businesses rather than individuals, which affects how they are likely to interact with the system day-to-day.

### Decision
**React is the primary interface for the Shipper role.** Full load management, history, and document handling live in React (create/edit/cancel, dashboards, filtering, document attachments). Flutter's shipper-facing screens are deliberately kept lightweight: status viewing, notifications, and a simplified quick-post-load action.

### Consequences
**Positive**
- Matches how business shippers actually work — desk-based, document-heavy, multi-load management is naturally suited to a web console rather than a phone.
- Still satisfies the mandatory cross-platform workflow pattern, because the simplified quick-post-load action is deliberately retained in Flutter (see below), preserving the required Flutter → API → PostgreSQL → Agentic AI → React (approval) → Flutter (status) pattern.
- Role-based navigation allows the Admin to reuse the same underlying React screens (all-loads dashboard, cancel/flag) scoped by permission — one codebase, two role-gated views, which is efficient and easy to defend in the viva.

**Negative**
- Slightly departs from a strict reading of "Flutter = primary user-facing client" for this specific role; this is documented and justified rather than left implicit, to pre-empt viva questioning.

### Alternatives Considered
- **Flutter as the shipper's primary/only interface, mirroring Driver/Agency Staff:** Rejected — would not reflect realistic B2B shipper behaviour and would push document-heavy workflows onto a small screen with no marking benefit.
- **Full feature parity between React and Flutter for Shippers:** Rejected — unnecessary duplication of effort within the 8-week timeline; the "quick-post-load" action in Flutter is sufficient to preserve the cross-platform requirement.

---

## ADR-004: Proof of Pickup + Proof of Delivery as Mandatory Camera Evidence

**Status:** Accepted
**Date:** August 2026

### Context
Component C requires a meaningful device feature and a clean example of deterministic, testable business-rule validation. The trip lifecycle (`Assigned → PickedUp → InTransit → Delivered`) needed a real-world control point that could not simply be "clicked through" without evidence, to make the status workflow credible and worth the marks available for business logic.

### Decision
Both a **Proof of Pickup** (captured by Agency Staff when dispatching a vehicle from the yard) and a **Proof of Delivery** (captured by the Driver on completion) are required. Both are stored as `TripEvidence` records (Component C), distinguished by an `evidenceType` enum (`PickupProof` / `DeliveryProof`). The API **hard-blocks** the relevant status transition (`400 Bad Request`) unless the corresponding evidence record exists.

### Consequences
**Positive**
- Produces a clean, testable deterministic-validation case for the backend test suite — not just a UI warning, but an enforced business rule at the API layer.
- Gives Component C two genuinely business-meaningful camera moments (satisfying the "meaningful device feature" requirement) while reusing a single entity and upload pattern for both, keeping implementation cost low.
- Mirrors the same reusable capture/upload pattern used for Component A's `File` entity — a good, easy-to-explain code-reuse talking point for the viva.

**Negative**
- Adds a hard dependency between the mobile camera feature and trip-status progression; if camera/upload permissions fail during the live demo, the trip cannot progress. Mitigated by testing on a real device early (Week 4, per the delivery plan).

### Alternatives Considered
- **Delivery Proof only, no Pickup Proof:** Rejected — a single evidence point is a weaker validation story and gives Component C only one camera moment instead of two.
- **Soft warning instead of a hard API block:** Rejected — a hard-blocked transition is a stronger, more defensible example of deterministic business-rule enforcement for the rubric's "Data Operations" and "Component Design and Business Logic" criteria.

---

## ADR-005: React State Management — Redux Toolkit + Context API

**Status:** Accepted
**Date:** August 2026

### Context
The specification requires a "justified state-management approach" for React (Section 7) and lists this as one of the four mandatory ADR topics (Section 14.2). The React application needs to manage genuinely global/shared state (authenticated user, role, Agentic AI workflow monitoring data visible across screens) alongside smaller, localised UI state within individual component trees.

### Decision
Use **Redux Toolkit** for global/shared state — authentication, role-based navigation, and Agentic AI workflow monitoring/approval state — combined with **React Context** for smaller, localized state within specific component trees (e.g. a multi-step form's internal state).

### Consequences
**Positive**
- Redux Toolkit's centralised store and DevTools make it straightforward to demonstrate and explain global state (e.g. current approval queue, auth state) live in the viva.
- Avoids Context-only "prop drilling" pain for state that many unrelated components need (auth, role, workflow status).
- Using Context for genuinely local state avoids unnecessary Redux boilerplate everywhere, keeping the codebase easier to navigate for four different owners.

**Negative**
- Running two state-management approaches side by side adds a small amount of conceptual overhead; the team must be disciplined about which state belongs where, and this rule must be documented for consistency across all four components.

### Alternatives Considered
- **Context API only:** Rejected — becomes unwieldy for frequently-updated global state such as live agent workflow monitoring across multiple screens.
- **Zustand:** Considered as a lighter-weight alternative; not selected because Redux Toolkit's structure and DevTools are more familiar to the team and better support explaining state flow under viva questioning.

---

## ADR-006: Flutter State Management — Provider

**Status:** Accepted
**Date:** August 2026

### Context
The specification requires a "justified state-management approach" for Flutter (Section 8) and lists this as one of the four mandatory ADR topics (Section 14.2). Four students with varying levels of Flutter experience need to work independently on their own component's screens while keeping the approach consistent and explainable.

### Decision
Use **Provider** as the Flutter state-management approach across all components.

### Consequences
**Positive**
- Low learning curve and wide documentation, reducing onboarding risk for team members with less Flutter experience.
- Easier to explain and defend under viva pressure than more complex alternatives, directly supporting the "student can explain, modify, debug" requirement embedded throughout the rubric.
- Sufficient for the scope of this application (auth state, form state, workflow/status state) without introducing unnecessary architectural complexity.

**Negative**
- Less powerful/structured than alternatives such as Bloc or Riverpod for very large applications — considered an acceptable trade-off given the bounded scope of this academic project.

### Alternatives Considered
- **Riverpod:** A viable, more modern alternative; not selected as the default due to a steeper learning curve, though the team keeps it as a fallback if a component owner prefers it.
- **Bloc:** Rejected as the default — more boilerplate and a steeper learning curve than justified for this project's scope and timeline.

---

## ADR-007: Agentic AI Framework & Orchestration — LangGraph-Style Sequential Multi-Agent Pipeline with One Conditional Branch

**Status:** Accepted
**Date:** August 2026

### Context
Section 9 of the specification requires a multi-step, multi-agent Agentic AI workflow — not a chatbot or single-prompt system — with at least four distinct agents, planning and delegation, controlled tool use, persisted shared state, deterministic validation, and a human-in-the-loop approval gate. LangGraph is explicitly named in the spec as the framework used in labs.

### Decision
Implement the Agentic AI subsystem as a **LangGraph-style sequential pipeline with one conditional branch**. Four distinct agents run in sequence (Agent 1 → Agent 2 → Agent 3 → Agent 4), and the single conditional branch sits at the Shipper's decision point (Approve vs. Reject/Revise), since human approval itself is invoked unconditionally on every run. *(Approval authority was assigned to the Shipper, not the Admin, in ADR-016. The same sequential-with-one-branch pipeline may be automatically re-invoked more than once per load under the bounded retry rule in ADR-018 — each invocation is still an independent, self-contained run of this same pattern, not additional branching within a single run.)*

### Consequences
**Positive**
- A sequential pipeline with one branch is simple to reason about, test, and demonstrate reliably during a live evaluation — directly protecting the highest-weighted individual criterion (Individual Agentic AI Contribution, 12 marks) and the group Integrated Architecture/Agent Orchestration criterion (10 marks).
- Each agent maps cleanly onto one student's owned component (Planner→Component A, Domain Analysis→Component B, Matching/Pricing→Component C, Validation/Safety→Component D), giving every student individually attributable, viva-defensible ownership of one pipeline stage.
- Satisfies the "distinct agent" definition in Section 9.1 of the spec: each agent has an identifiable responsibility, a defined input/output contract, controlled tool permissions, and visible participation in the workflow.

**Negative**
- A purely sequential design is less flexible than a fully graph-based, multi-branch orchestration; this is an accepted trade-off, since the spec's minimum acceptance criteria do not require complex branching — only one defined high-impact approval gate.

### Alternatives Considered
- **Fully branching/graph-based orchestration with multiple conditional paths:** Rejected — adds orchestration complexity with no corresponding rubric requirement, and increases the risk of an unreliable live demo.
- **Microsoft Agent Framework / LlamaIndex agents / Google ADK:** Considered as spec-permitted alternatives; LangGraph was selected because it is the framework already used in labs, minimising ramp-up risk within the 8-week timeline.

---

## ADR-008: LLM Provider — NVIDIA NIM (Free Tier)

**Status:** Accepted
**Date:** August 2026

### Context
The Agentic AI subsystem requires LLM access for at least four agents. Section 14 of the specification requires that the assignment be completable using institution-provided or no-cost services, with no paid subscriptions required. The team needed a reliable, free, OpenAI-compatible LLM endpoint suitable for a live, reproducible demo.

### Decision
Use **NVIDIA NIM** (`build.nvidia.com`) as the primary LLM provider — a free tier requiring no credit card, exposing an OpenAI-compatible endpoint, with a rate limit (~40 requests/minute) comfortably above what a single 4-agent workflow run needs. Integration is via the official `langchain-nvidia-ai-endpoints` package (`ChatNVIDIA`) or a plain OpenAI-compatible client pointed at NVIDIA's base URL. **Ollama (local model) is kept as a tested fallback** to cover the network-dependency risk of a hosted API during the live viva demo.

### Consequences
**Positive**
- Fully satisfies the spec's no-cost-services requirement without needing a paid OpenAI/Anthropic API key.
- OpenAI-compatible interface means it integrates cleanly into LangGraph with minimal custom code.
- The Ollama fallback directly mitigates the single highest-risk failure mode of a live agentic demo — losing internet/API access at the wrong moment — by allowing a same-day switch to a fully local model.

**Negative**
- Dependence on a third-party free-tier service carries some risk of policy changes, downtime, or rate-limit surprises; mitigated by the documented Ollama fallback and by the spec's own allowance for reporting confirmed service outages to the evaluator.
- Free-tier hosted models may be less capable than top-tier paid models; acceptable given the workflow's structured, tool-assisted, deterministically-validated design does not depend on very high raw model capability.

### Alternatives Considered
- **Paid OpenAI/Anthropic API key:** Rejected — not required by the spec and adds unnecessary cost/dependency for a student project.
- **Ollama as the sole/primary provider:** Rejected as primary (kept as fallback only) — local model quality and setup consistency across four students' machines was judged less reliable than a shared hosted free-tier endpoint for day-to-day development.

---

## ADR-009: Agentic AI Runs as a Separate Python Service (Called Only by ASP.NET Core)

**Status:** Accepted
**Date:** August 2026

### Context
The specification's mandatory backend rule (Section 2) states that where a Python Agentic AI service is used, it must operate as an internal service called by ASP.NET Core and must never be called directly by either client application. LangGraph, the team's chosen orchestration framework, is a Python-native library.

### Decision
Implement the Agentic AI subsystem as a **separate Python service (FastAPI + LangGraph)**, Dockerized, called **internally only** by the ASP.NET Core backend. React and Flutter never call this service directly — all workflow triggers, status checks, and approval actions pass through ASP.NET Core endpoints.

### Consequences
**Positive**
- "Best tool for the job": LangGraph and its Python ecosystem are used natively rather than being reimplemented or shoehorned into C#, reducing development risk and increasing agent-code quality.
- Fully compliant with the spec's mandatory backend rule — a clean, explicit architectural boundary that is simple to demonstrate and defend in the viva.
- Keeps ASP.NET Core as the single authoritative application layer for auth, business rules, and audit logging, exactly as required by Section 5 of the spec.

**Negative**
- Introduces a second runtime/service to deploy and keep running alongside the .NET API and PostgreSQL, adding a small amount of operational complexity (mitigated by Dockerizing all components together — see ADR-014).
- Requires a well-defined internal API contract between ASP.NET Core and the Python service, which must be documented and kept in sync.

### Alternatives Considered
- **Reimplement the multi-agent orchestration directly in C#:** Rejected — LangGraph is a mature, lab-taught Python framework; replicating its orchestration patterns natively in C# would consume timeline with no additional marking benefit and higher implementation risk.
- **Expose the Python service directly to React/Flutter:** Rejected outright — explicitly disallowed by the specification's mandatory backend rule.

---

## ADR-010: Database Schema Strategy for Agentic AI Workflow State

**Status:** Accepted
**Date:** August 2026

### Context
Section 9.1 of the specification requires the workflow ID, objective, plan, completed steps, tool results, validation results, errors, approval status, and final outcome to be persisted in structured, durable storage. This is also one of the four mandatory ADR topics (Section 14.2). The team also needed each student to have individually attributable, row-level database evidence for their own agent's contribution.

### Decision
Persist Agentic AI workflow state in PostgreSQL using four dedicated tables, each with a clear writer:

| Table | Purpose | Written by |
|---|---|---|
| `AgentWorkflowRun` | Workflow ID, objective, plan, overall status, attempt number (bounded retry — see ADR-018) | Agent 1 (Planner) |
| `AgentStep` | Per-agent input/output log, one row per agent per run | All four agents, one row each |
| `ToolCall` | OpenRouteService call input/output, success/failure | Agent 3 (Matching/Pricing) |
| `ApprovalDecision` | Decided-by, action taken, reason | Agent 4 computes the proposal; **Shipper** action, submitted via Component A's console, written through a role-guarded Component D endpoint (see ADR-016) |

### Consequences
**Positive**
- Directly satisfies the spec's shared-state persistence requirement with a clean, explainable relational structure.
- Gives each student a specific table (or specific rows within `AgentStep`) they personally write to — strong, unambiguous, individually attributable database evidence for the viva.
- Normalized structure (rather than one large JSON blob) makes it straightforward to query, test, and display execution summaries in the React approval console.

**Negative**
- Four related tables require careful foreign-key design and coordinated migrations across all four owners' EF Core models; must be planned early (Week 1–2) to avoid integration conflicts.

### Alternatives Considered
- **Single JSON/JSONB "workflow state blob" column:** Rejected — harder to query, harder to attribute row-level evidence to individual students, and less aligned with the relational, normalized approach expected elsewhere in the database design.
- **Separate database per agent/service:** Rejected — unnecessary complexity for this scope; a single shared PostgreSQL database keeps the integrated-system rule (Section 1) simple to satisfy and demonstrate.

---

## ADR-011: Authentication Model — JWT Access Token (5 min) + Refresh Token (30 days)

**Status:** Accepted
**Date:** August 2026

### Context
The specification requires JWT authentication and role-based authorization across both React and Flutter clients, sharing the same identity and permissions (Section 1, integrated-system rule). Four distinct roles (Shipper, Agency Staff, Driver, Admin) must be supported consistently across both clients.

### Decision
Use **JWT-based authentication** with a short-lived **access token (5 minutes)** and a longer-lived **refresh token (30 days)**, issued by the ASP.NET Core backend and consumed identically by both React and Flutter.

### Consequences
**Positive**
- Short-lived access tokens limit the exposure window if a token is compromised, satisfying the spec's security requirements (Section 5) with a well-understood, industry-standard pattern.
- A 30-day refresh token keeps the mobile experience (Flutter — Driver/Agency Staff) usable without requiring very frequent re-logins, which matters for operational users in the field.
- One shared identity/token scheme across both clients directly satisfies the mandatory integrated-system rule and gives a single, consistent security story to present in the viva.

**Negative**
- Requires implementing and testing a full refresh-token flow (secure storage, rotation, revocation on both clients), which is additional implementation effort compared to a single long-lived token.
- Secure token storage on Flutter (secure storage APIs) must be implemented correctly to avoid weakening the security benefit of short-lived access tokens.

### Alternatives Considered
- **Single long-lived JWT with no refresh token:** Rejected — weaker security posture, and directly contradicts the "secure configuration" and "protected endpoints" expectations in Section 5 of the spec.
- **Session-based (cookie) authentication:** Rejected — less natural fit for a Flutter mobile client and for a stateless REST API consumed by two independent client types.

---

## ADR-012: Third-Party Integrations — OpenRouteService + PayHere Sandbox + Transactional Email

**Status:** Accepted
**Date:** August 2026

### Context
Section 11 of the specification requires at least one meaningful third-party API integration, routed through the ASP.NET Core backend, with credentials protected and failure/timeout handling in place. The Agentic AI's Matching & Pricing agent also needed a real routing/distance data source to make its tool-use step genuine rather than simulated. Once the job-proposal and bounded-retry design was adopted (ADR-017, ADR-018), the shipper also needed to be proactively notified at several points in that process rather than having to keep re-checking the app.

### Decision
Integrate **three** third-party services, all called exclusively from the ASP.NET Core backend (never directly by React or Flutter):
1. **OpenRouteService** — distance and ETA calculation between an agency's yard and a load's pickup location; this call **doubles as the Agentic AI's allow-listed tool** (`get_route_and_eta`), used by Agent 3.
2. **PayHere (sandbox/test mode)** — invoice payment on Component D: checkout session creation and webhook-driven payment confirmation.
3. **Transactional email API** (e.g. Brevo or Resend, free tier) — shipper notifications on the decline/retry loop introduced in ADR-018 (agency declined, new match found, no auto-match found), sent via a shared `IEmailService` owned by Component A.

### Consequences
**Positive**
- OpenRouteService alone already satisfies the rubric's minimum third-party integration requirement; reusing the same call for both the UI-facing price estimate and the Agentic AI's tool-use maximises marking coverage from one integration effort.
- PayHere and the email API are both deliberate scope **additions** (not required by the rubric) that add real domain depth — PayHere to Component D's billing story, email to the reliability/UX of the decline-retry loop — while remaining free-tier/sandbox only, no real cost.
- All three integrations share a consistent, easy-to-explain failure-handling pattern (timeout/retry, signature verification for the webhook, safe-failure logging), which is a strong, reusable viva talking point across multiple components.
- Credentials for all three services are stored only in environment variables / .NET user-secrets, never committed to Git, satisfying the spec's credential-protection requirement.

**Negative**
- Three live third-party integrations (rather than one) increases the number of external failure points to handle and test, and adds a small amount of additional risk during the live demo. Mitigated by the documented allowance in the spec to report a confirmed external-service outage to the evaluator with evidence, and by a documented sandbox/logging fallback mode for `IEmailService` during the live viva (parallel to the Ollama LLM fallback in ADR-008), so declining a proposal on stage doesn't require real email delivery to succeed.
- Free-tier ORS rate limits apply per-minute; judged not to be a practical constraint since the workflow is triggered per-load on demand rather than in a bulk or live-tracking loop.
- The email integration is owned by Component A but triggered from a Component C endpoint (agency decline) — a cross-component call that must be documented in both students' individual reports, consistent with the other cross-component contracts already flagged in the README.

### Alternatives Considered
- **OpenRouteService only, no payment gateway or email (fully simulated invoicing and notifications):** This was the original, more conservative scope; the team upgraded to real sandbox/free-tier integrations as intentional depth additions once time permitted, while explicitly flagging each for re-evaluation if the timeline becomes tight.
- **Mapbox instead of OpenRouteService:** Considered as an equally spec-permitted alternative; OpenRouteService was selected for its free-tier terms and adequate coverage of Sri Lanka.
- **In-app/push notifications only, no email, for the decline-retry loop:** Considered; email was preferred because it reaches the shipper even when the app isn't open, which matters for a multi-attempt retry process that may take some time to resolve.

---

## ADR-013: Human Approval is Unconditional on Every Agentic Workflow Run

**Status:** Accepted
**Date:** August 2026

### Context
Section 9.1 of the specification requires that "at least one clearly defined high-impact action must pause until an authorized user approves, rejects or requests revision." The team needed to decide whether this pause should be conditional (e.g. only triggered above a price-deviation threshold) or unconditional (triggered on every single run). *(Which specific human holds this authority — Admin or Shipper — was a separate question, resolved in ADR-016; this ADR concerns only whether the pause itself is conditional or unconditional, and remains unaffected by that later refinement.)*

### Decision
The workflow **always** pauses for Shipper approval after Agent 4's validation step, on **every** run, with no exceptions. Agent 4 still calculates the percentage deviation between its proposed price and the shipper's own estimate, but this figure is surfaced to the Shipper purely as **decision-support context**, not as a gating condition that determines whether approval is required. *(This unconditional rule is unaffected by the bounded auto-retry introduced in ADR-018: each automatic re-invocation of the pipeline after an agency decline is itself a fresh run, and each fresh run pauses for Shipper approval again — the retry loop never skips this gate. The agency's own accept/decline of a job proposal, introduced in ADR-017, is a separate downstream business-consent step, not a second instance of this AI-approval gate.)*

### Consequences
**Positive**
- Removes any ambiguity about whether the mandatory human-in-the-loop requirement has been met — every single assignment proposal, without exception, passes through an authorized human before being finalized, which is unambiguous, reliable evidence for the "Human approval" row of Section 9.1 and for the live demo checklist (Section 17.1).
- Simplifies the conditional logic in the pipeline to a single, well-defined branch (see ADR-007), reducing orchestration risk during the live evaluation.
- The price-deviation percentage still gives the Shipper genuinely useful decision-support information, so the design does not sacrifice UX or realism for the sake of simplicity.

**Negative**
- In a real production system, requiring human approval on every single match would not scale operationally; this is explicitly accepted as an academic-project trade-off, prioritising demo reliability and unambiguous rubric compliance over production efficiency.

### Alternatives Considered
- **Conditional approval (only above an X% price-deviation threshold):** Rejected — introduces a risk that, depending on the exact data used in the live demo, the "high-impact action" might not actually trigger the approval pause, which would directly jeopardise the single most safety-critical rubric requirement in Section 9.1.

---

## ADR-014: Deployment Approach — Dockerized on Free-Tier VPS, Deferred to Later Sprints

**Status:** Accepted
**Date:** August 2026

### Context
Section 14 of the specification requires the ASP.NET Core API, PostgreSQL, and React to be deployed with working live/health URLs, and requires that this be achievable using institution-provided or no-cost services. This is also one of the four mandatory ADR topics (Section 14.2). The team's sprint plan needed to decide when, in an 8-week timeline, deployment work should happen relative to feature development.

### Decision
Deploy the full stack using **Docker containers** on a **free-tier cloud VPS (AWS free-tier EC2 or Azure free VPS)**:
- ASP.NET Core API — Dockerized, deployed to the VPS.
- React — Dockerized, served via nginx on the same VPS, configured to call the deployed API.
- PostgreSQL — Dockerized on the same VPS, with migrations run on startup.
- Agentic AI (Python/FastAPI/LangGraph) — Dockerized as a separate internal-only service (see ADR-009), which may run alongside the API on the same VPS or locally with documented setup/startup instructions, whichever is most reliable for the live demo.

Deployment and Dockerization work is deliberately scheduled for **Sprint 7 (12–18 Sep 2026)**, after all backend, database, React, Flutter, and Agentic AI functionality is substantially complete, rather than attempted continuously from Week 1.

### Consequences
**Positive**
- Free-tier hosting fully satisfies the spec's no-cost-services requirement.
- Dockerizing all components consistently (API, React, PostgreSQL, Agentic AI) gives a single, repeatable, easy-to-explain deployment story for the viva and README setup instructions.
- Deferring deployment to Sprint 7 avoids the common student-project failure mode of repeatedly re-deploying an unstable, half-finished system; by Sprint 7, the team deploys a feature-complete, already-tested system once, reducing wasted effort.
- Keeping the Agentic AI service's exact deployment location flexible (same VPS or local, as documented) protects the live demo against a single point of failure.

**Negative**
- Concentrating deployment work into one sprint (Sprint 7) creates schedule risk if unexpected infrastructure issues arise late; mitigated by having CI (GitHub Actions) and Dockerfiles prepared incrementally beforehand, and by leaving Sprint 8 free for final regression checks and fixes.
- Free-tier VPS resources are limited; the team must monitor resource usage (especially running the LLM-calling Agentic AI service) to avoid hitting free-tier limits close to submission.

### Alternatives Considered
- **Deploy incrementally from Week 1 alongside every feature:** Rejected as the primary strategy — would consume significant time on infrastructure work before there is a stable system worth deploying, at the cost of feature development time in a tight 8-week window.
- **Platform-as-a-Service (e.g. Render, Railway) instead of a raw VPS:** Considered; free-tier VPS with Docker was preferred for giving the team full, transparent control over all four components' startup order and environment configuration, which is easier to document and defend in the viva than a managed platform's abstracted behaviour.

---

## ADR-015: Pricing Formula — `baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)`

**Status:** Accepted
**Date:** August 2026

### Context
Two separate parts of the system need a price figure: Component A's shipper-facing price estimator (`POST /loads/{id}/estimate`) and Agent 3's proposed assignment price within the Agentic AI pipeline. This is also one of the four mandatory ADR topics (Section 14.2), and the two prices needed to be comparable so that Agent 4's price-deviation check (ADR-013) is meaningful.

### Decision
Use a single, shared pricing formula across both use cases:

```
estimatedPrice = baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg)
```

- Component A's estimate uses a straight-line (haversine) `distanceKm`, computed internally with no external API call, appropriate for a rough, pre-matching estimate.
- Agent 3's pricing uses the real, ORS-routed `distanceKm` (from the `get_route_and_eta` tool call), giving a more accurate figure once an actual candidate agency and route are known.
- `baseFare`, `ratePerKm`, and `ratePerKg` were originally scoped as shared configuration constants (application settings), not a separate database entity, reused by both calculations so that the two prices remain consistent and genuinely comparable. **This was subsequently refined by ADR-019**, which moves the sourcing and storage of these constants (and introduces weight-tiered `ratePerKm` variation) into two Admin-managed reference tables, without changing the formula itself or the requirement that both calculations stay consistent.
- **Addendum (post-acceptance):** Agent 3's pricing tool call (`get_price_estimate`, calling the internal `/internal/pricing/calculate` endpoint — see ADR-009's internal-service boundary) uses `distanceKm = route(pickup → dropoff)` via OpenRouteService — the same cargo leg Component A's haversine estimate approximates, just routed instead of straight-line. The agency's yard→pickup leg (returned separately by the `get_route_and_eta` tool call, used only for the shipper-facing ETA) is **not** included in the price calculation. This is a deliberate, documented simplification: real deadhead/positioning cost is excluded, so that both prices remain computed over the same distance concept and Agent 4's price-deviation check stays a genuine like-for-like comparison, per this ADR's original intent.

### Consequences
**Positive**
- A single shared formula (with two different distance inputs) is simple to implement, simple to test, and simple to explain twice — once for Component A's owner and once for Component C's owner — without duplicating business logic in two different forms.
- Because both prices are computed the same way, the percentage deviation Agent 4 calculates between the shipper's estimate and Agent 3's proposal is a meaningful, non-arbitrary comparison rather than comparing two unrelated pricing models.
- Using haversine distance for the pre-matching estimate avoids an unnecessary external API call at load-posting time, when no agency has been matched yet and a real route cannot be computed.

**Negative**
- A linear formula based on distance and weight is a simplification of real-world freight pricing (which can depend on vehicle type, cargo type, fuel prices, seasonal demand, etc.); this is an accepted, documented simplification appropriate to the academic scope.
- Exact constant values (`baseFare`, `ratePerKm`, `ratePerKg`) are treated as an implementation/tuning detail, not an architectural decision, and may be adjusted without requiring a new ADR — see ADR-019 for how these values are now sourced, stored, and kept auditable.
- Excluding the yard→pickup deadhead leg from pricing (see addendum above) means the price does not reflect the agency's full vehicle-occupied trip time, only the loaded cargo leg — an accepted simplification, not an oversight.

### Alternatives Considered
- **Two separate, unrelated pricing formulas for the estimate vs. the agent's proposal:** Rejected — would make Agent 4's price-deviation validation meaningless, since it would be comparing two structurally different calculations rather than the same formula applied to better or worse distance data.
- **Flat-rate pricing (no distance/weight sensitivity):** Rejected — too simplistic to be a credible "business-specific operation beyond CRUD" for Component A, and would give Agent 3's pricing step little genuine work to do.
- **Pricing Agent 3's proposal on the yard→pickup leg (or yard→pickup + pickup→dropoff combined):** Rejected — pricing only the positioning leg would exclude the actual cargo movement from the price entirely; pricing the full combined trip would break the "same distance concept" comparability with Component A's estimate and complicate Agent 4's validation logic, for a realism gain judged not worth the added complexity at this scope.

---

## ADR-016: Approval Authority — Shipper, Not Admin

**Status:** Accepted
**Date:** August 2026

### Context
The original design (see the "Admin" wording still present in early drafts of ADR-007, ADR-010, and ADR-013 before this ADR) had the Admin reviewing and approving the AI's recommended agency match, mirroring a generic "platform overseer approves everything" pattern. On review, the team reconsidered who actually has the standing to make this call: it is the shipper's cargo and the shipper's money at stake in each match, not the platform operator's.

### Decision
The **Shipper** — not the Admin — reviews Agent 4's proposed agency assignment and approves, rejects, or requests revision. This happens on the Shipper's own React console (Component A), which displays the recommended agency, ETA, price, and the price-deviation percentage as decision-support context. The **Admin's** role is narrowed to two functions only: **agency KYC/compliance verification** (Component B) and **system-wide analytics**. Admin has no involvement in per-load matching decisions. *(ADR-019 subsequently adds a third, narrow Admin function — maintaining the pricing-configuration reference data — which does not involve per-load matching decisions and is consistent with this ADR's separation-of-concerns intent; see ADR-019 for the reasoning.)*

### Consequences
**Positive**
- Matches real incentive structure: the party bearing the cost and cargo risk is the one deciding whether to accept a proposed match, which is a more defensible business-logic story for the viva than a generic platform-overseer model.
- Turns Admin into a genuine trust-and-integrity layer (who is allowed on the platform, how the platform is performing overall) rather than an operational bottleneck approving every individual match — a cleaner separation of concerns across the four components.
- Keeps the approval UI and the load data it depends on in the same place (Component A's React console), avoiding an unnecessary cross-client round-trip just to display context the Shipper already has open.

**Negative**
- The approval endpoint must now be guarded to accept a decision only from the specific load's own shipper (not just "any authenticated user with the Shipper role"), which is a small but necessary additional authorization check beyond simple role-based access control.
- Unlike a small, predictable Admin user population, the live demo now depends on the correct Shipper account being logged in and responsive at the exact moment the recommendation is ready — the demo script must have that session open in advance.
- Narrows Component D's ("Billing & Admin Oversight") scope, since the approval console it previously owned moves to Component A. This is judged an acceptable, even positive, simplification, since Component D retains full ownership of Agent 4's computation, the `ApprovalDecision` write endpoint, invoicing, and (pending confirmation) dispute resolution.

### Alternatives Considered
- **Admin approves (original design):** Rejected — a weaker mirror of real-world incentives, and unnecessarily routes every single match through a staff account that has no direct stake in the outcome.
- **Dual approval (both Shipper and Admin must approve):** Rejected — adds friction and an extra state to test and demo, with no rubric requirement to justify it within the 8-week timeline.

### Open Follow-On Question
Whether Admin's narrowed "KYC + analytics only" remit also removes dispute resolution from Component D, or whether disputes remain with Admin as a distinct function unrelated to per-load matching, is not yet resolved by the team — flagged for confirmation before Component D's individual report is finalized.

---

## ADR-017: No Competitive Bidding — Single AI-Recommended Agency, Confirmed via Job Proposal

**Status:** Accepted
**Date:** August 2026

### Context
An earlier version of the matching design considered a competitive-bidding model: agencies would bid on a posted load, and the Agentic AI would evaluate the pool of bids. On review, the team judged this added a whole additional subsystem (a `Bid` entity, competitive-ranking-of-bids logic, bid-expiry handling) without a corresponding rubric requirement, and that it delayed the AI's involvement until after a bidding window closed rather than triggering it immediately.

### Decision
The system does **not** run a competitive bidding process. The Agentic AI pipeline is triggered **immediately** when the shipper posts a load. Agent 2 evaluates **all** eligible agencies directly (compliance, fleet capacity, proximity) and Agent 3 computes price/ETA for the single best-fit candidate — the AI recommends **one** agency, not a ranked pool for the shipper to pick from. Once the shipper approves (ADR-016), the backend creates an `Assignment` in a `Proposed` state and sends it to the recommended agency as a **job proposal**. The agency then independently **accepts or declines** the proposal (Flutter, Component C) before anything is finalized:
- `Assignment` state machine: `Proposed → Accepted` / `Declined`.
- `Trip` (created only once `Assignment` is `Accepted`): `Assigned → PickedUp → InTransit → Delivered`.

### Consequences
**Positive**
- Removes an entire competitive-bidding subsystem from scope — no bid entity, no bid-ranking logic, no bid-expiry handling — reducing implementation risk within the 8-week timeline.
- Creates a genuine **second** human decision point on the other client (the agency's own accept/decline in Flutter), which is a materially stronger demonstration of the required cross-platform workflow (Section 8) than a single shipper-side approval alone.
- Fits Component C's existing "Matching & Trip Execution" naming and its pre-existing `Assignment`/`Trip` entities with minimal structural change — the component already anticipated an agency-side accept action.
- Reinforces that the AI's recommendation is genuinely a *recommendation*, not an automatic assignment — a clean, easy-to-explain distinction for the viva between what the AI decides and what still requires two independent human confirmations (shipper, then agency).

**Negative**
- Introduces a two-stage `Assignment` lifecycle (`Proposed → Accepted`/`Declined`) rather than a single-step assignment, which is one more state to implement, test, and demonstrate correctly.
- The AI's top-ranked agency is not guaranteed to actually execute the job until it independently confirms — this must be narrated clearly in the demo so the proposal-then-confirmation flow reads as intentional design, not an incomplete assignment.
- Requires a corresponding decision on what happens if the agency declines — addressed separately in ADR-018.

### Alternatives Considered
- **Competitive bidding (agencies bid, AI picks the best bid):** Rejected — adds bid-entity, bid-ranking, and bid-expiry complexity with no rubric requirement, and delays AI involvement until after a bidding window closes.
- **AI recommendation directly finalizes the assignment, no agency confirmation:** Rejected — removes the agency's own agency in the process and produces a weaker cross-platform demonstration with only one human decision point instead of two.

---

## ADR-018: Automatic Retry with a Capped Attempt Limit, and Shipper Email Notifications, on Agency Decline

**Status:** Accepted
**Date:** August 2026

### Context
Once ADR-017 introduced an independent agency accept/decline step, the team needed to decide what happens when an agency **declines** a job proposal: fail outright, require the shipper or Admin to manually re-trigger matching, or retry automatically. The team also needed to decide whether and how the shipper is kept informed throughout, since a decline-and-retry process could otherwise leave them checking the app repeatedly with no signal that anything had changed.

### Decision
On decline, the system:
1. Marks the `Assignment` `Declined` and **emails the shipper immediately** ("this agency declined, we're finding another").
2. **Automatically re-triggers** Agents 2–4 to find a new candidate, with Agent 2's query excluding any agency that has already declined this specific load.
3. The new run still **pauses unconditionally for shipper approval** (ADR-013's rule applies to every run, including automatic re-invocations — it is never skipped).
4. Once a new recommendation is ready, **emails the shipper a second time** ("new match found, please review").
5. This is **capped at 3 automatic attempts per load** (a tunable implementation constant, not an architectural constraint). Beyond the cap, the system stops auto-retrying, records a **safe failure**, flags the load for manual review, and sends a third email variant ("no automatic match found, please check the app").

`AgentWorkflowRun` gains an **attempt number** field to implement the cap (see ADR-010). Email delivery uses the transactional email integration added in ADR-012, via a shared `IEmailService` owned by Component A and called cross-component from Component C's decline endpoint.

### Consequences
**Positive**
- Keeps the shipper informed at every state change without requiring them to poll the app — a genuine UX improvement that also gives Component A's owner additional individually-attributable technical depth (the retry-cap logic and the shared email service).
- The retry cap gives the system a clean, bounded "safe failure" terminus for a load with no genuinely available agencies, directly satisfying Section 9.1's "auditable result or safe, recorded failure" requirement for this failure mode specifically — an unbounded retry would instead risk looping indefinitely, which is both a live-demo hazard and a weaker rubric story.
- Because each automatic re-invocation still passes through Agent 4's unconditional approval pause (ADR-013), the bounded-retry design cannot be used to bypass human oversight, even accidentally.

**Negative**
- Repeatedly declining a proposal during the live demo (e.g. to showcase the retry loop) will send multiple real emails unless a sandbox/logging fallback mode is used for `IEmailService` during the viva — documented as a demo-reliability mitigation in ADR-012, parallel to the Ollama LLM fallback in ADR-008.
- The cross-component call from Component C's decline endpoint into Component A's `IEmailService` and retry-check logic must be documented explicitly in both students' individual reports, following the same pattern already used for the Shipper/Agency registration DTO overlap and the approval-endpoint contract.
- The exact retry-cap number (3) is a judgement call rather than a derived constant; the team accepts this as a reasonable, adjustable default rather than treating it as load-bearing architecture.

### Alternatives Considered
- **Manual re-trigger only (shipper or Admin must manually re-run matching after a decline):** This was the team's initial, more conservative default; superseded by this ADR once the team judged that bounded automatic retry was worth the added complexity for a materially better shipper experience.
- **Unbounded automatic retry (no cap):** Rejected — risks an infinite loop if no agency will accept a given load, which is a live-demo reliability hazard and does not produce a clean, terminating "safe failure" outcome.
- **No shipper notification during the retry process (silent retry):** Rejected — would leave the shipper unaware anything happened until they happened to check the app, undermining the point of having a responsive, human-in-the-loop system at all.

---

## ADR-019: Admin-Managed Pricing Configuration — Fuel Price & Vehicle-Class Efficiency Reference Tables

**Status:** Accepted
**Date:** August 2026

### Context
ADR-015 established the shared pricing formula and deliberately left `baseFare`, `ratePerKm`, and `ratePerKg` as tunable configuration constants, "not an architectural decision." In preparing to defend these constants at the project review board, the team recognised two gaps this left open:

1. **No sourced, citable answer to "where did these numbers come from."** Unlike Uber Freight — which predicts price using a machine-learning model trained on years of proprietary shipment data — this project has no equivalent training data, and reproducing that approach was judged infeasible and inappropriate for the academic scope. A plain hardcoded constant, however, gives an equally weak answer under questioning: there was no way to show a reviewer *where the number came from* or *when it was last verified*.
2. **A single flat `ratePerKm` misprices different vehicle classes.** Research into general trucking fuel-efficiency data showed a roughly 3–4× difference in fuel consumption between a mini truck (~11 km/L) and a container/trailer truck (~2.6–3.3 km/L). A flat rate would significantly under-price container-truck jobs and over-price mini-truck jobs.

The team also checked directly for a free API to source live Sri Lankan diesel/petrol prices or freight rates. **None exists**: Sri Lanka's Ceylon Petroleum Corporation (CPC) publishes prices only as static HTML with no API; the one commercial fuel-price API found covering Sri Lanka (GlobalPetrolPrices.com) is a paid product; and freight/lorry-hire pricing in Sri Lanka is market-negotiated per job with no published rate card at all, unlike the regulated, published tuk-tuk/taxi fare structure. A live external fetch was also judged undesirable even if one existed, for the same demo-reliability reasons already documented for the LLM (ADR-008) and email (ADR-012) fallbacks: CPC prices change only monthly at most, so a live fetch buys negligible accuracy over a periodically-updated, cited constant, while introducing a real live-demo failure point.

### Decision
Introduce two new, Admin-only-managed reference tables to hold the pricing input data, while keeping the ADR-015 formula itself unchanged:

| Table | Purpose |
|---|---|
| `FuelPriceRate` | Current and historical `PricePerLitre` by `FuelType`, each row citing its `Source` (e.g. "CPC official announcement, dated") |
| `VehicleClassEfficiency` | Fuel consumption (`FuelConsumptionLPer100Km`) and a `Load.WeightKg`/`VolumeM3` payload band (`MinPayloadKg`–`MaxPayloadKg`) per `VehicleClass` tier, used to derive a weight-tiered `ratePerKm` instead of one flat value |

Design details:
- **`FuelType` and `VehicleClass` are native Postgres enums**, not lookup tables — consistent with the existing `UserRole`/`LoadStatus` convention (small, closed, structural sets; see also ADR-020, which generalises this reasoning across the whole schema). Only the genuinely tunable numeric market data (price, payload bounds, consumption figures) lives in a table, since enums cannot hold editable numeric values.
- **Versioning is append-only**: editing a rate means inserting a new row with a later `EffectiveFrom`; the prior row is left untouched and becomes historical. The "current" value is the latest `EffectiveFrom` row that has not been soft-deleted.
- **Soft delete, not hard delete**: a `BEFORE DELETE` trigger intercepts any `DELETE` and instead sets `DeletedAt` / `DeletedByUserId`, consistent with the project's existing no-hard-delete rule (enforced elsewhere via `BEFORE DELETE` triggers and `ON DELETE RESTRICT`). Unlike other status-bearing entities, no separate `*StatusHistory` table is introduced for these two tables, since reference data of this kind has no meaningful multi-stage lifecycle to log beyond "current vs. superseded vs. deleted."
- **`VehicleClassEfficiency` is deliberately decoupled from `Vehicle.VehicleType`** (Component B). The actual `Vehicle` used for a load is not selected until `Trip` creation, which happens after `Assignment` is `Accepted` — by which point Agent 3 has already computed the proposed price. `Load.WeightKg`/`VolumeM3` are used instead as the earliest available proxy for the vehicle class the job will likely need.
- **Auditability without new coupling to the agentic tables**: Agent 3's pricing service snapshots the actual `PricePerLitre` and `FuelConsumptionLPer100Km` values it used into `AgentStep.InputJson` (already `jsonb`, per ADR-010) at calculation time. This preserves a full point-in-time audit trail for any historical run even after the underlying config row is later superseded or soft-deleted, without adding any new foreign key onto the Agentic AI tables.
- **Admin-only CRUD**, extending ADR-016's narrowed Admin scope (KYC verification, system-wide analytics) with a third, similarly non-matching-related function: maintaining system pricing-configuration reference data.
- **Initial data sourcing methodology**: a documented, cost-based derivation (CPC's official cited diesel price ÷ a fuel-efficiency figure per vehicle class, plus driver/maintenance/margin allowances) cross-checked against real market quotes obtained directly from comparable local operators (e.g. PickMe Truck, independent lorry-hire services), rather than any single external pricing API.

### Consequences
**Positive**
- Gives the team a directly defensible, citable, dated, and auditable answer to "where did `baseFare`/`ratePerKm`/`ratePerKg` come from" under review-board questioning, rather than an unexplained hardcoded number.
- Lets `ratePerKm` vary realistically by load-weight tier, reflecting genuine fuel-efficiency differences between vehicle classes, without requiring a machine-learning pricing model or any training data the team does not have.
- Admin can update these values through the application as CPC revises fuel prices, with no code redeploy required — a genuine, demonstrable operational-maturity story for the viva.
- Fully consistent with existing project conventions rather than introducing arbitrary new patterns: the enum-vs-table split follows the same reasoning already established for `UserRole`; the soft-delete trigger follows the same no-hard-delete principle used everywhere else in the schema; and the `AgentStep.InputJson` snapshot reuses an existing audit mechanism instead of adding new schema coupling.
- Avoids the live-demo fragility of fetching pricing data from an external service at request time, matching the same reliability reasoning already applied to the LLM and email integrations.

**Negative**
- Two new tables, an Admin CRUD endpoint pair, and role-guard tests are additive scope not in the original sprint plan; the team has deliberately timed this to avoid displacing the higher-priority Sprint 3 Agentic AI catch-up work, rather than treating it as urgent.
- Expands Admin's role beyond ADR-016's original "KYC + analytics only" wording with a third function; this must be explained consistently as a deliberate, narrow addition (system configuration, not matching authority) if raised in the viva, rather than left as unexplained scope creep.
- No authoritative Sri Lanka fuel-price or freight-rate API exists (confirmed by direct search); the initial `FuelPriceRate` and `VehicleClassEfficiency` values are therefore a cost-based derivation and manually-collected market quotes, dated to when they were gathered, requiring periodic manual re-verification by Admin rather than automatic refresh. This is accepted as a reasonable, documented limitation rather than a gap the team failed to investigate.
- Introduces a `DeletedAt`/`DeletedByUserId` soft-delete pattern not used elsewhere in the schema (other entities use Status enums plus a separate `*StatusHistory` table instead); justified specifically for this case because the reference data has no meaningful multi-stage lifecycle that would warrant a full history table.

### Alternatives Considered
- **Leave `baseFare`/`ratePerKm`/`ratePerKg` as plain hardcoded application-config constants (original ADR-015 scope, unchanged):** Rejected as insufficient on its own — gives no admin editability without a redeploy, and no queryable, timestamped, sourced answer to "where did this number come from" under review-board questioning.
- **Fetch fuel price and/or freight pricing live from a third-party API at calculation time:** Rejected — no free, official Sri Lanka fuel-price API exists (confirmed by direct search), and even a paid or unofficial one would introduce a live external dependency that could fail during the live demo, for the same reliability reasons already documented in ADR-008 and ADR-012.
- **Free-text `VehicleType`/`FuelType` columns instead of enums:** Rejected for consistency — the project already established native Postgres enums over lookup tables for exactly this kind of small, closed categorical set (see `UserRole`, `LoadStatus`, and ADR-020).
- **Key pricing tiers directly off `Vehicle.VehicleType`:** Rejected — the actual `Vehicle` is not selected until `Trip` creation, which happens after `Assignment` is `Accepted` and after Agent 3 has already computed the proposed price; `Load.WeightKg`/`VolumeM3` bands are used instead as the earliest available proxy.
- **A full `*StatusHistory` table mirroring `AgencyStatusHistory`/`LoadStatusHistory`:** Rejected as unnecessary for this case — these two tables are pure reference data with no multi-stage business lifecycle; the simpler `EffectiveFrom` versioning plus `DeletedAt` soft-delete satisfies the no-hard-delete rule without the added complexity of a parallel history table.

---

## ADR-020: Enumerated Value Sets as Native Enum Types, Not Lookup Tables

**Status:** Accepted
**Date:** August 2026

### Context
The schema contains roughly two dozen closed value sets — user roles, the status chain of every major entity, evidence types, agent roles, approval decisions, notification categories. Two questions had to be settled together: how these are stored, and where their authoritative definition lives.

The storage question surfaced first with `User.Role`. A `Role` lookup table with a foreign key from `User` is the conventional relational answer and was the schema's original design. On review the team found the usual justification for it does not hold here: a lookup table is required by Third Normal Form only if *role metadata* (display name, description, permission set) is stored, since that metadata depends on the role rather than on the user. FreightLink stores no such metadata — authorization is expressed through `[Authorize(Roles = ...)]` attributes in code, not database rows — so a single atomic value on `User` introduces no transitive dependency and the table earns nothing.

The definition question surfaced separately: value sets were being invented ad hoc in C# as each component was built, with no single place to check them, which risks the four components drifting apart on spelling and on which terminal states exist.

### Decision
Every closed value set is stored as a **PostgreSQL native enum type** (or, where a project constraint prevents that, a `text` column with an equivalent `CHECK` constraint), mapped to a C# enum with `HasConversion<string>()` in EF Core so values remain human-readable in `psql` during the live demo. No lookup tables are created for value sets.

The authoritative definition of every enum lives in a single **Enum Inventory** document (`docs/enum-inventory.md`), grouped by owning component. Adding or renaming a value is a change to that document first, then a migration. `FuelType` and `VehicleClass`, introduced by ADR-019, follow this same convention.

Two substantive points were settled while producing the inventory:

- **`AgencyStatus` distinguishes `Verified` from `Active`.** `Verified` means the Admin has approved the agency's KYC/compliance documents; `Active` means verified *and* currently accepting jobs (at least one available vehicle, at least one active driver, availability switched on). `Verified ↔ Active` is the transition the Flutter availability-management screen writes, in both directions; movement into and out of `Suspended` is Admin-only. **Agent 2's eligibility query filters on `Active` only** — this is the reason the distinction had to be resolved rather than left implicit.
- **Terminal states were missing from four documented chains.** `LoadStatus.Cancelled`, `AssignmentStatus.Cancelled`, `TripStatus.Cancelled`, and `InvoiceStatus.Void` are all produced by endpoints that already exist and are required by mandatory edge case 2, but none appeared in the status chains as originally written. All four are now part of their respective enums.

### Consequences
**Positive**
- The database rejects an invalid value exactly as a foreign key would have, so dropping the lookup table costs nothing in integrity while removing a join from every user query.
- Enum members and database values are the same strings, so `[Authorize(Roles = "AgencyStaff")]` lines up with what is stored, with no mapping layer in between and nothing to get out of sync.
- A single inventory document gives the four component owners one place to check a value set before using it, and gives the viva a written artefact to point at — a value set that exists only in C# is one an evaluator cannot inspect.
- Resolving `Verified` vs `Active` removes a genuine ambiguity in Agent 2's eligibility filter that would otherwise have been settled silently, and differently, by whoever implemented it first.

**Negative**
- Adding a value to a PostgreSQL enum type requires a migration (`ALTER TYPE ... ADD VALUE`) rather than an `INSERT`, so value sets are less convenient to extend at runtime than lookup-table rows. Judged appropriate here precisely because these sets should not change casually — each value implies branching logic in code.
- The design assumes **exactly one role per user**. If a user ever needs two roles simultaneously (an owner-dispatcher who also drives), `User.Role` must be replaced by a `UserRole` junction table. This is recorded explicitly as a correctness boundary, not a preference, so that the constraint is a known one rather than a surprise.
- Enum members and the inventory document can drift if the document is not updated alongside a migration; the team treats the document as the first step of any enum change rather than as documentation written afterwards.

### Alternatives Considered
- **`Role` lookup table with a foreign key from `User` (original design):** Rejected — required by 3NF only if role metadata is stored, which it is not; adds a table and a join without adding information.
- **Plain `text`/`varchar` status columns with no constraint:** Rejected outright — permits `'shipper'`, `'Shipper '`, and `'SHIPPER'` to coexist as distinct values, which silently breaks every status filter and eligibility query in the system.
- **Integer-backed enums (EF Core's default mapping):** Rejected — stores `2` where a human reading the table needs `Active`, which makes both debugging and live demonstration of data changes materially harder for no benefit.
- **A single generic `Lookup` table holding all value sets:** Rejected — a polymorphic key/value table defeats type safety entirely and would require every join to filter on a category discriminator.

---

## Summary of Decisions

| ADR No. | Title | Status |
|---|---|---|
| ADR-001 | Carrier model = Agency (not independent drivers) | Accepted |
| ADR-002 | Single Yard per Agency (no multi-yard support) | Accepted |
| ADR-003 | Shipper's primary interface is React (Flutter lightweight for shippers) | Accepted |
| ADR-004 | Proof of Pickup + Proof of Delivery as mandatory camera evidence | Accepted |
| ADR-005 | React state management — Redux Toolkit + Context API | Accepted |
| ADR-006 | Flutter state management — Provider | Accepted |
| ADR-007 | Agentic AI framework & orchestration — LangGraph-style sequential pipeline with one conditional branch | Accepted |
| ADR-008 | LLM provider — NVIDIA NIM (free tier), Ollama fallback | Accepted |
| ADR-009 | Agentic AI runs as a separate Python service (called only by ASP.NET Core) | Accepted |
| ADR-010 | Database schema strategy for Agentic AI workflow state | Accepted |
| ADR-011 | Authentication model — JWT Access Token (5 min) + Refresh Token (30 days) | Accepted |
| ADR-012 | Third-party integrations — OpenRouteService + PayHere sandbox + transactional email | Accepted |
| ADR-013 | Human approval is unconditional on every agentic workflow run | Accepted |
| ADR-014 | Deployment approach — Dockerized on free-tier VPS, deferred to later sprints | Accepted |
| ADR-015 | Pricing formula — baseFare + distance × ratePerKm + weight × ratePerKg | Accepted |
| ADR-016 | Approval authority — Shipper, not Admin | Accepted |
| ADR-017 | No competitive bidding — single AI-recommended agency, confirmed via job proposal | Accepted |
| ADR-018 | Automatic retry with a capped attempt limit, and Shipper email notifications, on agency decline | Accepted |
| ADR-019 | Admin-managed pricing configuration — fuel price & vehicle-class efficiency reference tables | Accepted |
| ADR-020 | Enumerated value sets as native enum types, not lookup tables | Accepted |

---

*These Architecture Decision Records will be referenced directly during the final viva as the group's primary written evidence for Learning Outcome 4 (LO4). Each team member should be prepared to explain, in their own words, the context and consequences of any ADR relevant to their owned component (Sections 5–8) and their individual Agentic AI contribution (Section 9).*