# API Contract / OpenAPI Skeleton
## FreightLink / FreightMatch LK — SE3090 Assignment 1

**Jira:** `Y3S01-15` · Epic: Foundation & Shared Infrastructure (`Y3S01-1`) · Sprint 1 (1–7 Aug 2026)
**Owner:** Ratnaweera O.V. (Team Leader) — Component C, Agent 3
**Repository:** https://github.com/OmiraRathnaweera/freightLink
**Status:** Draft — Sprint 1 skeleton, **Rev. 9**. Component owners fill in request/response schema detail as their controllers are implemented (Sprint 2 onward).

### Change Log

| Rev | Date | Change |
|---|---|---|
| 1 | 7 Aug 2026 | Initial Sprint 1 skeleton |
| 2 | 9 Aug 2026 | Corrected against architecture review: fixed `Load`/`Assignment`/`Trip` status enums, added missing `Assignment` list/detail/decline endpoints (Component C), corrected all `/workflows/*` approval endpoints from `Admin` to `Shipper (own load)` per **ADR-016**, corrected the 409 conflict example (previously implied competitive bidding, contradicting **ADR-017**), flagged `disputes/resolve` role as provisional pending team decision, added explicit JSON casing + ownership-guard conventions |
| 3 | 9 Aug 2026 | Auth layer implemented: resolved the Section 4.1 open design note by splitting `/auth/register` into `/auth/register/shipper` and `/auth/register/agency` (Driver is not publicly self-registered; Admin has no public registration and is seeded on startup instead); added the previously-missing `/auth/logout` row (`Any (authenticated)`, revokes the caller's own refresh token); clarified that `POST /auth/login` returns **only** `accessToken`/`refreshToken`, never a full user profile; added corresponding YAML skeleton paths |
| 4 | 11 Aug 2026 | Component A Load Management (create/read-one/read-list/edit/cancel) implemented: `POST/GET /loads`, `GET/PUT /loads/{id}`, `POST /loads/{id}/cancel`. Cancellation is now `POST /loads/{id}/cancel`, not `DELETE /loads/{id}` — per **ADR-019** (no hard deletes; cancellation is a status transition recorded in `LoadStatusHistory`), and is **Shipper (own) only** — Admin is role-gated out of edit/cancel entirely, narrower than this doc's earlier "Shipper (own), Admin" assumption for the delete row. Added the `403 LOAD_NOT_OWNED` / `422 INVALID_LOAD_STATUS_TRANSITION` error codes and filled in the `Loads` request/response schemas (Section 5). `estimate`/`status-history`/`files` sub-resources remain unimplemented. |
| 4 | 12 Aug 2026 | Shared File Upload/Delete implemented: added Section 4.7 (`/files/*`, backed by Cloudinary) — component-agnostic infrastructure for Component A (Load files) and Component C (TripEvidence) to build on, not tied to either yet. Scoped to `Shipper`, `AgencyStaff`, `Driver` only — **`Admin` explicitly excluded**, since uploading/deleting a file is an operational action taken by the party producing it, not an oversight action. Only image files (JPG/PNG/GIF/WEBP/BMP/HEIC/TIFF) and PDF are accepted, max 10 MB each. Added corresponding YAML skeleton paths and schemas. |
| 5 | 13 Aug 2026 | Load cancellation changed from `POST /loads/{id}/cancel` to `PATCH /loads/{id}/cancel` — `PATCH` matches its actual semantics (a partial state-transition update), reserving `POST` for resource creation. No change to auth, ownership, or request/response shape. |
| 6 | 13 Aug 2026 | Load Management hardening: `PUT /loads/{id}` and `PATCH /loads/{id}/cancel` now genuinely use `409 LOAD_CONCURRENCY_CONFLICT` (Postgres `xmin` optimistic concurrency), superseding the earlier claim that no Load endpoint used `409`; `POST /loads` retries internally on a `ReferenceCode` collision before returning `409 LOAD_REFERENCE_CODE_CONFLICT`; `POST /loads`/`PUT /loads/{id}` reject identical pickup/dropoff coordinates with `400 LOAD_PICKUP_DROPOFF_IDENTICAL`; `GET /loads` rejects an out-of-range `page`/`pageSize` combination with `400 LOAD_PAGE_OUT_OF_RANGE` and now paginates deterministically (ties broken by `loadId`) and case-insensitive `search` matching is index-backed (`pg_trgm`). No path, role, or response-shape changes. |
| 7 | 15 Aug 2026 | Component A file attachments implemented: `POST/GET /loads/{id}/files` and `DELETE /loads/{id}/files/{fileId}` link an already-uploaded file (from `POST /files/single`) to a load — never re-implements Cloudinary upload/delete, only the `LoadFile` metadata linkage. Also closes a documentation gap (no functional change): `GET /loads` has always supported `search`, `sortBy`, `sortDir`, `shipperUserId`, `createdFrom`, `createdTo` query params since Rev. 4/6, but only `page`/`pageSize`/`status` were documented until now — added the missing `components/parameters` entries and wired them into the path. |
| 8 | 17 Aug 2026 | `LoadResponse`/`LoadListItem` enriched with `shipperName` (server-resolved from `User.FullName`, joined via the existing `Load.ShipperUser` navigation — no new `/users/{id}` endpoint was added or is planned) so Admin views of loads owned by other Shippers can show a display name instead of only a raw `shipperUserId`. `LoadListItem` also gained `shipperUserId` itself (previously detail-only on `LoadResponse`) so the frontend's id-based fallback label works on list rows too, not just the single-load view. `GET /loads/{id}` also now returns the load's full `LoadStatusHistory` audit trail as `statusHistory` (newest first) directly on the response — this **supersedes** the `GET /loads/{id}/status-history` row below, which will not be built as a separate endpoint. `POST /loads`, `PUT /loads/{id}`, and `PATCH /loads/{id}/cancel` responses leave `statusHistory` as an empty array (the caller already knows the single transition it just made). Also documents the frontend side for the first time: the React app's Load Management screens (`frontend/src/features/loads`) and full auth flow (`frontend/src/features/auth`) are now wired against the real backend — `POST/GET /loads`, `GET/PUT /loads/{id}`, `PATCH /loads/{id}/cancel`, the `/loads/{id}/files` attach flow, and all of Section 4.1 (`/auth/*`) — via TanStack Query + Axios (see `frontend/docs/load-management-api.md`). No frontend work exists yet against Sections 4.3–4.6 (Agencies, Assignments/Trips, Billing, Workflows), which also remain unimplemented on the backend. |
| 9 | 19 Aug 2026 | `PATCH /loads/{id}/cancel` **retired and replaced** by `PATCH /loads/{id}/status` — a single endpoint for every Shipper-initiated load status change, closing the gap that a `Draft` load created without `postImmediately` had no way to later become `Posted`. Request body changes from `CancelLoadRequest` (`{ reason }`) to `ChangeLoadStatusRequest` (`{ status, reason }`); `status` must be `Posted` (publish, no reason) or `Cancelled` (reason required, same `400 LOAD_CANCEL_REASON_REQUIRED` as before) — any other value, including `Matched`/`InTransit`/`Delivered`/`Closed`, is rejected `422 INVALID_LOAD_STATUS_TRANSITION` even though those are legal transitions in `LoadStatusTransitionRules`' graph, since they're reached only by internal processes (the AI matching workflow, trip events), never by this Shipper-facing endpoint. Same role/ownership/concurrency behavior as the old `.../cancel` endpoint it replaces. Frontend's `cancelLoad`/`useCancelLoadMutation` (`loadsApi.js`) now call the new endpoint internally, unchanged externally; new `publishLoad`/`usePublishLoadMutation` added alongside, not yet wired to any UI control. |

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
>
> **Note (Rev. 4):** now that Component A is implemented, the `Load` row above no longer matches the actual `LoadStatus` enum (`Entities/Enums/LoadStatus.cs`): `Draft → Posted → Matched → InTransit → Delivered → Closed`, with `Cancelled` as a side-branch reachable from `Draft`/`Posted`/`Matched` (enforced by `Common/Domain/LoadStatusTransitionRules.cs`) — it does not include `ProposalSent`/`Assigned` as `Load` statuses; those concepts live on `Assignment.Status` instead (Section 4.4/ADR-017). Per this project's `CLAUDE.md`, the C# enum is the source of truth for exact status values — this table is left as historical Sprint 1 intent rather than rewritten, since fully reconciling it against `Assignment`'s lifecycle is Component C's task, not part of this pass.

---

## 4. Endpoint Inventory

### 4.1 Auth & Identity — Foundation (shared)

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/auth/register/shipper` | Register a new Shipper (self-service) | Public |
| POST | `/auth/register/agency` | Register a new Agency org + its first Agency Staff user, atomically (self-service) | Public |
| POST | `/auth/login` | Shared login for all roles — returns **only** `{ accessToken, refreshToken }`, never the user profile | Public |
| POST | `/auth/refresh` | Exchange a valid refresh token for a new access + refresh token pair (rotates the old one) | Public (valid refresh token) |
| POST | `/auth/logout` | Revoke a refresh token belonging to the caller | Any (authenticated) |
| GET | `/auth/me` | Current user profile + role, read from the access token | Any (authenticated) |

> **Resolved in Rev. 3:** the open design note below was decided — `/auth/register` is split into `/auth/register/shipper` and `/auth/register/agency`, matching the two public entry points in the approved Figma UI. `Driver` accounts are not publicly self-registered (created by Agency Staff via a future Component B endpoint). `Admin` has no public registration endpoint at all — a default Admin is seeded automatically on API startup from `ADMIN_USER_EMAIL`/`ADMIN_USER_PASSWORD` env vars.

### 4.2 Component A — Load Management (Owner: Dias H.N.P.K.)

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/loads` | Create a load (full form — React; quick-post — Flutter) | Shipper |
| GET | `/loads` | Search/filter/sort/paginate loads | Shipper (own), Admin (all) |
| GET | `/loads/{id}` | Load detail incl. full status-change history (`statusHistory`, newest first) and current `workflowRunId` (if any) | Shipper (own), Admin |
| PUT | `/loads/{id}` | Edit a load (only while `Draft`/`Posted`) | Shipper (own) |
| PATCH | `/loads/{id}/status` | Change a load's status — publish (`Posted`) or cancel (`Cancelled`); every other status is set only by internal processes (Rev. 9, supersedes `.../cancel`) | Shipper (own) |
| POST | `/loads/{id}/estimate` | Price estimate — `baseFare + distanceKm×ratePerKm + weightKg×ratePerKg` (haversine distance) | Shipper *(not yet implemented)* |
| ~~GET~~ | ~~`/loads/{id}/status-history`~~ | **Superseded, Rev. 8** — never built as a separate endpoint; the full timeline now rides along on `GET /loads/{id}`'s `statusHistory` field instead | — |
| POST | `/loads/{id}/files` | Attach an already-uploaded file (`POST /files/single`) to a load, classified `Manifest`/`Invoice`/`CargoPhoto`/`Other` | Shipper (own) |
| GET | `/loads/{id}/files` | List attached files | Shipper (own), Admin |
| DELETE | `/loads/{id}/files/{fileId}` | Detach a file (removes only the `LoadFile` link; the underlying upload is untouched) | Shipper (own) |

> **Improvement applied:** `GET /loads/{id}` now explicitly returns the current `workflowRunId` (when a match run exists for the load), so the Shipper's React approval console can navigate straight to `GET /workflows/{id}` without needing to already know the workflow ID out-of-band. As of Rev. 4 the field exists on `LoadResponse` but is always `null` — populating it requires joining `AgentWorkflowRun`/`Assignment`, which lands with Section 4.6.
>
> **Implemented in Rev. 4:** the first five rows (`POST /loads`, `GET /loads`, `GET /loads/{id}`, `PUT /loads/{id}`, `PATCH /loads/{id}/cancel`) are live in `LoadsController`/`LoadService`. Two corrections against the earlier skeleton: (1) cancellation is a dedicated status-transition route, not `DELETE /loads/{id}` — per **ADR-019**, every entity's lifecycle end is a status transition, not a `DELETE`; (2) cancellation is **Shipper (own) only** — Admin is role-gated out of both `PUT` and `PATCH .../cancel` (`403`, never reaches an ownership check), narrower than this table's earlier "Shipper (own), Admin" assumption on the old delete row.
>
> **Resolved in Rev. 5:** cancellation moved from `POST /loads/{id}/cancel` to `PATCH /loads/{id}/cancel` — `PATCH` matches its actual semantics (a partial update to `status`/reason), reserving `POST` for resource creation.
>
> **Ownership/status errors:** a Shipper accessing a load they don't own gets `403 LOAD_NOT_OWNED` (not `404`) — existence and ownership are checked as separate steps. Editing/cancelling a load whose current status doesn't permit it returns `422 INVALID_LOAD_STATUS_TRANSITION`, not `409`.
>
> **Resolved in Rev. 6:** `PUT /loads/{id}` and `PATCH /loads/{id}/cancel` now do carry a genuine `409 LOAD_CONCURRENCY_CONFLICT` case — a lost-update race caught via `Load`'s Postgres `xmin` optimistic-concurrency token when two requests load the same row and both attempt to save. Superseded the earlier claim (Rev. 4/5) that no Load endpoint uses `409` yet. `POST /loads` also gained a bounded internal retry on the pre-existing `409 LOAD_REFERENCE_CODE_CONFLICT` case (a `ReferenceCode` collision) rather than failing the request on the first collision — the response shape is unchanged, just less likely to occur. `POST /loads` and `PUT /loads/{id}` also now reject pickup/dropoff coordinates that are identical with `400 LOAD_PICKUP_DROPOFF_IDENTICAL` (mirrors the DB's `ck_load_distinct_points` CHECK), and `GET /loads` rejects a `page`/`pageSize` combination whose offset would overflow with `400 LOAD_PAGE_OUT_OF_RANGE`.
>
> **Implemented in Rev. 7:** `POST/GET /loads/{id}/files` and `DELETE /loads/{id}/files/{fileId}` are live in the new `LoadFilesController`/`LoadFileService`. Attach only ever references an already-uploaded file by its Cloudinary `publicId` (obtained from `POST /files/single` beforehand) plus a `fileType` classification — it never re-accepts url/size/contentType from the client, since those already live durably on the `UploadedFile` row created by that prior upload call. Attach can fail with `404 LOAD_NOT_FOUND` (load), `403 LOAD_NOT_OWNED` (not the load's Shipper), `404 LOAD_FILE_UPLOAD_NOT_FOUND` (unknown `publicId`), `403 FILE_NOT_OWNED` (the upload isn't the caller's), or `409 FILE_IN_USE` (the upload is already attached elsewhere — the same code `DELETE /files/{publicId}` already used the other direction). List is Shipper (own) or Admin (any); detach is Shipper (own) only, `404 LOAD_FILE_NOT_FOUND` if the attachment id doesn't exist under that load, `204` on success. Detach removes only the `LoadFile` link — the underlying upload stays deletable afterward via the existing `DELETE /files/{publicId}` once nothing attaches to it.
>
> **Documentation gap closed in Rev. 7 (no functional change):** `GET /loads` has supported `search`, `sortBy`, `sortDir`, `shipperUserId`, `createdFrom`, `createdTo` query params since Rev. 4/6, but only `page`/`pageSize`/`status` were ever added to this doc's `components/parameters`/path — the other six are now documented too.
>
> **Implemented in Rev. 8:** `LoadResponse` and `LoadListItem` both gained `shipperName` — resolved server-side from `User.FullName` via the existing `Load.ShipperUser` navigation (an `Include` on the single-row fetches, a JOIN on the list query — no N+1), so an Admin viewing loads across multiple Shippers sees a display name rather than only a raw `shipperUserId`. Falls back to `"Unknown"` if the owning user row can't be resolved. This is a **read-side enrichment only** — no `/users/{id}` lookup endpoint or general user-directory API was added, matching the smallest-change decision recorded for this feature. `LoadListItem` also carries `shipperUserId` alongside `shipperName` (it was previously detail-only, on `LoadResponse`) — the frontend's shared `formatShipperName(shipperName, shipperUserId)` fallback needs both on the same row to render an id-based label when a name can't be resolved, and that fallback was dead code on the list view until this field existed there too. Separately, `GET /loads/{id}` now also returns `statusHistory` (the load's full `LoadStatusHistory` trail, newest first, via a second `Include` on the same query) — this fulfills the `status-history` row above, which is retired as a would-be separate endpoint rather than built. `POST /loads`, `PUT /loads/{id}`, and `PATCH /loads/{id}/cancel` all still return `LoadResponse`, but leave `statusHistory` as `[]`, since a caller of those three already knows the one transition it just triggered.

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

### 4.7 Files — Shared File Upload/Delete (shared infrastructure)

**Status: Implemented (Rev. 4).** Component-agnostic upload/delete capability backed by Cloudinary, with its own controller at `/files` rather than being embedded in any one component's controller — Component A (`/loads/{id}/files`, Section 4.2) and Component C (`TripEvidence`, Section 4.4) are each expected to call this internally (storing the returned `publicId`) once their own attachment flows land, instead of each hand-rolling Cloudinary SDK calls. There is no per-resource ownership concept here, since this controller has no notion of which business entity a file belongs to — that link is made by whichever component persists the returned `publicId`.

| Method | Path | Description | Roles |
|---|---|---|---|
| POST | `/files/single` | Upload a single file (multipart/form-data) | Shipper, AgencyStaff, Driver |
| DELETE | `/files/{publicId}` | Delete a single file by its Cloudinary public id — idempotent, never 404 (`deleted: false` for an already-gone id) | Shipper, AgencyStaff, Driver |

> **Role note:** `Admin` is deliberately excluded from every endpoint in this section — uploading or deleting a file is an operational action taken by whichever party is producing the evidence/document (Shipper cargo photos, AgencyStaff compliance docs, Driver delivery proof), not an Admin oversight action. This mirrors the ownership-guard philosophy in Section 2: a role alone isn't sufficient reason to grant access, and here it's insufficient reason even the other way — `Admin`'s broad system-wide role does **not** extend to this shared infrastructure.
>
> Only image files (`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`, `.heic`, `.heif`, `.tif`, `.tiff`) and `.pdf` are accepted (allowlist, not a blocklist), max 10 MB each — everything else is rejected with `400 BLOCKED_FILE_TYPE`.

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
  version: 0.8.0-sprint2-loadstatushistory-rev8
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
  - name: Files             # Shared infrastructure — Dias H.N.P.K.

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

  /auth/register/shipper:
    post:
      tags: [Auth]
      summary: Register a new Shipper (self-service)
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/RegisterShipperRequest'
      responses:
        '201': { description: Created }
        '400': { $ref: '#/components/responses/ValidationError' }
        '409': { $ref: '#/components/responses/Conflict' }

  /auth/register/agency:
    post:
      tags: [Auth]
      summary: Register a new Agency org + its first Agency Staff user, atomically (self-service)
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/RegisterAgencyRequest'
      responses:
        '201': { description: Created }
        '400': { $ref: '#/components/responses/ValidationError' }
        '409': { $ref: '#/components/responses/Conflict' }

  /auth/refresh:
    post:
      tags: [Auth]
      summary: Exchange a refresh token for a new access + refresh token pair (rotates the old one)
      security: []
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/AuthTokens'
        '401': { $ref: '#/components/responses/Unauthorized' }

  /auth/logout:
    post:
      tags: [Auth]
      summary: Revoke a refresh token belonging to the caller
      responses:
        '204': { description: No Content }
        '401': { $ref: '#/components/responses/Unauthorized' }

  /auth/me:
    get:
      tags: [Auth]
      summary: Current user profile + role
      responses:
        '200': { description: OK }
        '401': { $ref: '#/components/responses/Unauthorized' }

  /loads:
    post:
      tags: [Loads]
      summary: Create a load
      x-allowed-roles: [Shipper]
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateLoadRequest'
      responses:
        '201':
          description: Created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/LoadResponse'
        '400': { $ref: '#/components/responses/ValidationError' }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '409': { $ref: '#/components/responses/Conflict' }
    get:
      tags: [Loads]
      summary: Search/filter/sort/paginate loads
      x-allowed-roles: [Shipper, Admin]
      x-ownership-note: "Shipper is always scoped to their own loads regardless of query filters; Admin sees every load"
      parameters:
        - $ref: '#/components/parameters/Page'
        - $ref: '#/components/parameters/PageSize'
        - $ref: '#/components/parameters/Status'
        - $ref: '#/components/parameters/Search'
        - $ref: '#/components/parameters/SortBy'
        - $ref: '#/components/parameters/SortDir'
        - $ref: '#/components/parameters/ShipperUserId'
        - $ref: '#/components/parameters/CreatedFrom'
        - $ref: '#/components/parameters/CreatedTo'
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PagedLoadResponse'
        '400': { $ref: '#/components/responses/ValidationError' }
        '401': { $ref: '#/components/responses/Unauthorized' }

  /loads/{id}:
    parameters:
      - $ref: '#/components/parameters/IdPathParam'
    get:
      tags: [Loads]
      summary: Load detail incl. full statusHistory (newest first) and current workflowRunId
      x-allowed-roles: [Shipper, Admin]
      x-ownership-note: "Shipper must own the load (403 LOAD_NOT_OWNED otherwise); Admin may fetch any load"
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/LoadResponse'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }
    put:
      tags: [Loads]
      summary: Edit a load (Draft/Posted only)
      x-allowed-roles: [Shipper]
      x-ownership-note: "Shipper must own the load (403 LOAD_NOT_OWNED); Admin is role-gated out entirely (403), never reaches an ownership check"
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateLoadRequest'
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/LoadResponse'
        '400': { $ref: '#/components/responses/ValidationError' }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }
        '409': { $ref: '#/components/responses/Conflict' }
        '422': { $ref: '#/components/responses/UnprocessableEntity' }

  /loads/{id}/status:
    patch:
      tags: [Loads]
      summary: Change a load's status — publish (Posted) or cancel (Cancelled); never a hard delete (ADR-019)
      description: >
        The single endpoint for every Shipper-initiated load status change (Rev. 9, supersedes the
        retired PATCH /loads/{id}/cancel). status must be Posted or Cancelled — every other value,
        including Matched/InTransit/Delivered/Closed, is rejected 422 INVALID_LOAD_STATUS_TRANSITION
        even though those are legal transitions in LoadStatusTransitionRules' graph, since they're
        reached only by internal processes (the AI matching workflow, trip events), never by this
        Shipper-facing endpoint.
      x-allowed-roles: [Shipper]
      x-ownership-note: "Shipper must own the load (403 LOAD_NOT_OWNED); Admin is role-gated out entirely (403), never reaches an ownership check"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/ChangeLoadStatusRequest'
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/LoadResponse'
        '400': { $ref: '#/components/responses/ValidationError' }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }
        '409': { $ref: '#/components/responses/Conflict' }
        '422': { $ref: '#/components/responses/UnprocessableEntity' }

  # --- Not yet implemented (Component A, planned) ---
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

  # --- Implemented Rev. 7 (Component A) ---
  /loads/{id}/files:
    parameters:
      - $ref: '#/components/parameters/IdPathParam'
    post:
      tags: [Loads]
      summary: Attach an already-uploaded file (see POST /files/single) to this load
      x-allowed-roles: [Shipper]
      x-ownership-note: "Requires load.ShipperId == currentUserId (403 LOAD_NOT_OWNED otherwise)"
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/AttachLoadFileRequest'
      responses:
        '201':
          description: Created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/LoadFileResponse'
        '400': { $ref: '#/components/responses/ValidationError' }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }
        '409': { $ref: '#/components/responses/Conflict' }
    get:
      tags: [Loads]
      summary: List a load's attached files
      x-allowed-roles: [Shipper, Admin]
      x-ownership-note: "Shipper must own the load (403 LOAD_NOT_OWNED otherwise); Admin may list any load's files"
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                type: array
                items: { $ref: '#/components/schemas/LoadFileResponse' }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }

  /loads/{id}/files/{fileId}:
    delete:
      tags: [Loads]
      summary: Detach a file from this load (removes only the LoadFile link, not the underlying upload)
      x-allowed-roles: [Shipper]
      x-ownership-note: "Requires load.ShipperId == currentUserId (403 LOAD_NOT_OWNED otherwise)"
      parameters:
        - $ref: '#/components/parameters/IdPathParam'
        - name: fileId
          in: path
          required: true
          description: The LoadFile attachment's own id (not the underlying upload's id)
          schema: { type: string, format: uuid }
      responses:
        '204': { description: No Content }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }

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

  /files/single:
    post:
      tags: [Files]
      summary: Upload a single file (image or PDF, max 10 MB)
      x-allowed-roles: [Shipper, AgencyStaff, Driver]
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              properties:
                file: { type: string, format: binary }
      responses:
        '201':
          description: Created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/FileUploadResult'
        '400': { $ref: '#/components/responses/ValidationError' }
        '403': { $ref: '#/components/responses/Unauthorized' }

  /files/{publicId}:
    delete:
      tags: [Files]
      summary: Delete a single file by public id (idempotent — never 404)
      x-allowed-roles: [Shipper, AgencyStaff, Driver]
      parameters:
        - name: publicId
          in: path
          required: true
          description: Cloudinary public id; may itself contain '/' characters
          schema: { type: string }
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/FileDeleteResult'
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
    Search:
      name: search
      in: query
      schema: { type: string }
      description: Case-insensitive substring match against cargoDescription, referenceCode, pickupAddress, dropoffAddress.
    SortBy:
      name: sortBy
      in: query
      schema: { type: string, enum: [createdAt, pickupWindowStart, weightKg], default: createdAt }
      description: Unrecognized values fall back to createdAt.
    SortDir:
      name: sortDir
      in: query
      schema: { type: string, enum: [asc, desc], default: desc }
    ShipperUserId:
      name: shipperUserId
      in: query
      schema: { type: string, format: uuid }
      description: Admin-only narrowing filter; ignored for a non-Admin caller, who is always scoped to their own loads regardless of this value.
    CreatedFrom:
      name: createdFrom
      in: query
      schema: { type: string, format: date-time }
      description: Inclusive lower bound on createdAt.
    CreatedTo:
      name: createdTo
      in: query
      schema: { type: string, format: date-time }
      description: Inclusive upper bound on createdAt.

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
    UnprocessableEntity:
      description: Semantically invalid state transition (e.g. editing/cancelling a load in a status that doesn't allow it)
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

    RegisterShipperRequest:
      type: object
      required: [email, password, fullName, companyName, billingAddress]
      properties:
        email: { type: string, format: email }
        password: { type: string, format: password }
        fullName: { type: string }
        phoneE164: { type: string }
        companyName: { type: string }
        businessRegNo: { type: string }
        billingAddress: { type: string }

    RegisterAgencyRequest:
      type: object
      description: >
        Creates the Agency org and its first Agency Staff user (the caller) atomically —
        AgencyStaff.agencyId is a required foreign key, so there is no schema-safe way to
        register an Agency Staff account without also creating the Agency row it belongs to.
      required: [email, password, fullName, agencyName, businessRegNo, yardAddress, yardLat, yardLng]
      properties:
        email: { type: string, format: email }
        password: { type: string, format: password }
        fullName: { type: string }
        phoneE164: { type: string }
        jobTitle: { type: string }
        agencyName: { type: string }
        businessRegNo: { type: string }
        yardAddress: { type: string }
        yardLat: { type: number, format: double }
        yardLng: { type: number, format: double }

    AuthTokens:
      type: object
      description: Returned by /auth/login and /auth/refresh — intentionally only these two fields, never the user profile.
      properties:
        accessToken: { type: string }
        refreshToken: { type: string }

    PriceEstimate:
      type: object
      properties:
        estimatedPrice: { type: number, format: double }
        distanceKm: { type: number, format: double }
        baseFare: { type: number, format: double }
        ratePerKm: { type: number, format: double }
        ratePerKg: { type: number, format: double }

    CreateLoadRequest:
      type: object
      description: POST /loads body. Server-assigns shipperUserId (from the JWT), referenceCode, and status — none are client-supplied.
      required: [cargoDescription, weightKg, volumeM3, pickupAddress, pickupLat, pickupLng, dropoffAddress, dropoffLat, dropoffLng, pickupWindowStart, pickupWindowEnd]
      properties:
        cargoDescription: { type: string, minLength: 3, maxLength: 1000 }
        weightKg: { type: number, format: double, exclusiveMinimum: 0, description: "kilograms; must be > 0 (mirrors ck_load_weight)" }
        volumeM3: { type: number, format: double, exclusiveMinimum: 0, description: "cubic meters; must be > 0 (mirrors ck_load_volume)" }
        pickupAddress: { type: string, minLength: 5, maxLength: 500 }
        pickupLat: { type: number, format: double, minimum: -90, maximum: 90 }
        pickupLng: { type: number, format: double, minimum: -180, maximum: 180 }
        dropoffAddress: { type: string, minLength: 5, maxLength: 500 }
        dropoffLat: { type: number, format: double, minimum: -90, maximum: 90 }
        dropoffLng: { type: number, format: double, minimum: -180, maximum: 180, description: "pickup/dropoff coordinates must not be identical (400 LOAD_PICKUP_DROPOFF_IDENTICAL otherwise, mirrors ck_load_distinct_points)" }
        pickupWindowStart: { type: string, format: date-time }
        pickupWindowEnd: { type: string, format: date-time, description: "must be after pickupWindowStart (400 INVALID_PICKUP_WINDOW otherwise, mirrors ck_load_window)" }
        postImmediately:
          type: boolean
          default: false
          description: "true creates the load directly as Posted instead of Draft (the 'quick-post' flow). No backing column on Load itself."

    UpdateLoadRequest:
      type: object
      description: >
        PUT /loads/{id} body. Same content fields as CreateLoadRequest minus postImmediately — this
        endpoint never changes status. Only accepted while the load is Draft or Posted (422
        INVALID_LOAD_STATUS_TRANSITION otherwise).
      required: [cargoDescription, weightKg, volumeM3, pickupAddress, pickupLat, pickupLng, dropoffAddress, dropoffLat, dropoffLng, pickupWindowStart, pickupWindowEnd]
      properties:
        cargoDescription: { type: string, minLength: 3, maxLength: 1000 }
        weightKg: { type: number, format: double, exclusiveMinimum: 0 }
        volumeM3: { type: number, format: double, exclusiveMinimum: 0 }
        pickupAddress: { type: string, minLength: 5, maxLength: 500 }
        pickupLat: { type: number, format: double, minimum: -90, maximum: 90 }
        pickupLng: { type: number, format: double, minimum: -180, maximum: 180 }
        dropoffAddress: { type: string, minLength: 5, maxLength: 500 }
        dropoffLat: { type: number, format: double, minimum: -90, maximum: 90 }
        dropoffLng: { type: number, format: double, minimum: -180, maximum: 180, description: "pickup/dropoff coordinates must not be identical (400 LOAD_PICKUP_DROPOFF_IDENTICAL otherwise, mirrors ck_load_distinct_points)" }
        pickupWindowStart: { type: string, format: date-time }
        pickupWindowEnd: { type: string, format: date-time }

    ChangeLoadStatusRequest:
      type: object
      description: >
        PATCH /loads/{id}/status body (Rev. 9). status is required and must be Posted or Cancelled —
        any other value is rejected 422 INVALID_LOAD_STATUS_TRANSITION before reason is even
        considered. reason is optional at the schema level but enforced as required by the service
        when status is Cancelled, before it writes the LoadStatusHistory row (400
        LOAD_CANCEL_REASON_REQUIRED if missing/blank) — mirrors LoadStatusHistory's own
        ck_lsh_cancel_reason CHECK. Ignored when status is Posted.
      required: [status]
      properties:
        status: { type: string, enum: [Posted, Cancelled] }
        reason: { type: string, maxLength: 500, nullable: true }

    LoadResponse:
      type: object
      description: Full single-resource response for POST /loads, GET /loads/{id}, PUT /loads/{id}, and PATCH /loads/{id}/status.
      properties:
        loadId: { type: string, format: uuid }
        shipperUserId: { type: string, format: uuid }
        shipperName: { type: string, description: "Resolved server-side from User.FullName; falls back to \"Unknown\" if the owning user can't be resolved. No /users/{id} endpoint exists — this is the only way either client learns a load owner's display name." }
        referenceCode: { type: string, description: "Server-generated, unique (uq_load_reference)" }
        cargoDescription: { type: string }
        weightKg: { type: number, format: double }
        volumeM3: { type: number, format: double }
        pickupAddress: { type: string }
        pickupLat: { type: number, format: double }
        pickupLng: { type: number, format: double }
        dropoffAddress: { type: string }
        dropoffLat: { type: number, format: double }
        dropoffLng: { type: number, format: double }
        pickupWindowStart: { type: string, format: date-time }
        pickupWindowEnd: { type: string, format: date-time }
        estimatedPrice: { type: number, format: double, nullable: true, description: "Set only via the not-yet-implemented POST /loads/{id}/estimate" }
        status: { type: string, description: "Draft | Posted | Matched | InTransit | Delivered | Closed | Cancelled" }
        workflowRunId: { type: string, format: uuid, nullable: true, description: "Always null until Section 4.6's AgentWorkflowRun join is implemented" }
        createdAt: { type: string, format: date-time }
        updatedAt: { type: string, format: date-time }
        statusHistory:
          type: array
          description: >
            Full LoadStatusHistory audit trail, newest first. Only populated by GET /loads/{id} — the
            POST/PUT/PATCH .../status responses that also return LoadResponse leave this as an empty
            array, since the caller already knows the single transition it just made.
          items: { $ref: '#/components/schemas/LoadStatusHistoryResponse' }

    LoadStatusHistoryResponse:
      type: object
      description: One recorded LoadStatus transition, as included in LoadResponse.statusHistory.
      properties:
        loadStatusHistoryId: { type: string, format: uuid }
        fromStatus: { type: string, nullable: true, description: "Null only for the load's very first status row" }
        toStatus: { type: string }
        reason: { type: string, nullable: true, description: "Set only for transitions that require one (e.g. cancellation)" }
        changedByUserId: { type: string, format: uuid }
        changedAt: { type: string, format: date-time }

    LoadListItem:
      type: object
      description: Lightweight row shape used inside PagedLoadResponse.items — omits lat/lng, volume, and workflowRunId (present on LoadResponse only).
      properties:
        loadId: { type: string, format: uuid }
        shipperUserId: { type: string, format: uuid, description: "Kept on the list row (not detail-only) so a client-side fallback label can identify the owner by id when shipperName falls back to \"Unknown\"" }
        shipperName: { type: string, description: "Resolved server-side from User.FullName; falls back to \"Unknown\" if the owning user can't be resolved" }
        referenceCode: { type: string }
        cargoDescription: { type: string }
        weightKg: { type: number, format: double }
        pickupAddress: { type: string }
        dropoffAddress: { type: string }
        pickupWindowStart: { type: string, format: date-time }
        pickupWindowEnd: { type: string, format: date-time }
        estimatedPrice: { type: number, format: double, nullable: true }
        status: { type: string }
        createdAt: { type: string, format: date-time }

    PagedLoadResponse:
      type: object
      description: GET /loads response — the generic Section 2.1 paging envelope, specialized to LoadListItem.
      properties:
        items:
          type: array
          items: { $ref: '#/components/schemas/LoadListItem' }
        page: { type: integer }
        pageSize: { type: integer }
        totalItems: { type: integer }
        totalPages: { type: integer }

    AttachLoadFileRequest:
      type: object
      description: >
        POST /loads/{id}/files body. References an already-uploaded file by its Cloudinary publicId
        (from POST /files/single) — never re-accepts url/size/contentType, since those are already
        durably stored on the UploadedFile row created by that prior upload call.
      required: [publicId, fileType]
      properties:
        publicId: { type: string, description: "Cloudinary public id, as returned by POST /files/single" }
        fileType: { type: string, enum: [Manifest, Invoice, CargoPhoto, Other] }

    LoadFileResponse:
      type: object
      description: Returned by POST /loads/{id}/files and as an item of GET /loads/{id}/files. Combines the LoadFile link with its joined UploadedFile storage details.
      properties:
        fileId: { type: string, format: uuid, description: "The LoadFile link's own id (used as the {fileId} path param for DELETE)" }
        loadId: { type: string, format: uuid }
        fileType: { type: string, enum: [Manifest, Invoice, CargoPhoto, Other] }
        attachedAt: { type: string, format: date-time }
        publicId: { type: string }
        secureUrl: { type: string, format: uri }
        format: { type: string, nullable: true }
        bytes: { type: integer, format: int64 }
        contentType: { type: string }
        originalFileName: { type: string, nullable: true }

    FileUploadResult:
      type: object
      description: Returned by POST /files/single.
      properties:
        publicId: { type: string }
        secureUrl: { type: string, format: uri }
        format: { type: string, nullable: true, description: "Cloudinary does not always detect a format for raw-resource-type uploads" }
        bytes: { type: integer, format: int64 }
        resourceType: { type: string }
        contentType: { type: string, description: "MIME type reported by the uploading client (e.g. image/jpeg, application/pdf)" }
        originalFileName: { type: string, nullable: true }

    FileDeleteResult:
      type: object
      description: Returned by DELETE /files/{publicId}. Deletion is idempotent — an already-gone publicId still returns 200 with deleted:false, never a 404.
      properties:
        publicId: { type: string }
        deleted: { type: boolean }
        detail: { type: string }

    # --- Skeleton only below this line ---
    # Each owner defines their entity schemas here as their controllers land:
    #   Agency, Vehicle, Driver, ComplianceDoc    -> D.B.A.H.W. Bandara  (Component B)
    #   Assignment, Trip, TripEvent, TripEvidence -> Ratnaweera O.V.     (Component C)
    #   Invoice, Dispute                          -> Balasooriya B.K.N.N. (Component D)
```

---

## 6. Ownership & Next Steps

| Section to complete | Owner | Target sprint |
|---|---|---|
| ~~`Load` schema + create/read-one/read-list/edit/cancel request/response bodies~~ — done Rev. 4 (`LoadsController`/`LoadService`); ~~`files` endpoints~~ — done Rev. 7 (`LoadFilesController`/`LoadFileService`). `LoadStatusHistory` schema + `estimate`/`status-history` endpoints still open | Dias H.N.P.K. | Sprint 2–3 (`Y3S01-25`–`44`) |
| `Agency`, `Vehicle`, `Driver`, `ComplianceDoc` schemas + confirm `Verified→Active` trigger | D.B.A.H.W. Bandara | Sprint 2–3 (`Y3S01-29`–`32`, `73`) |
| `Assignment`, `Trip`, `TripEvent`, `TripEvidence` schemas + new list/detail/decline endpoints (Section 4.4) | Ratnaweera O.V. | Sprint 2–4 (`Y3S01-33`–`35`, `74`) |
| `Invoice`, `Dispute` schemas + webhook payload; confirm `disputes/resolve` ownership (README §10 #9) | Balasooriya B.K.N.N. | Sprint 2–5 (`Y3S01-36`–`38`, `81`, `93`) |
| `Workflows` request/response schemas (`AgentWorkflowRun`, `AgentStep`, `ApprovalDecision`); implement the `load.ShipperId == currentUserId` ownership guard on `approve`/`reject`/`revise` per ADR-016 | All four (agent owners) | Sprint 5 (`Y3S01-90`–`92`, `95`–`97`) |
| Role-based authorization scheme wired to every endpoint above, including ownership guards flagged with `(own)` | Ratnaweera O.V. | Sprint 1 (`Y3S01-19`), enforced per-controller Sprint 2+ |
| ~~Confirm `/auth/register` single-vs-split design~~ — resolved Rev. 3, split into `/auth/register/shipper` + `/auth/register/agency`; Auth layer (JWT issuance/rotation, admin seed) implemented | Dias H.N.P.K. (owns Auth foundation alongside Component A) | Done |
| ~~Files: shared upload/delete infrastructure~~ — resolved Rev. 4, `/files/*` implemented (Cloudinary-backed, Shipper/AgencyStaff/Driver only). ~~Component A attachment flow~~ — resolved Rev. 7, `/loads/{id}/files*` implemented on top of it. Component C (`TripEvidence`) still needs to wire its own attachment flow | Dias H.N.P.K. (shared infra + Component A); Ratnaweera O.V. (Component C, still open) | Infra + Component A done; Component C Sprint 2–4 |
| Publish live Swagger/OpenAPI UI from the ASP.NET Core project | Ratnaweera O.V. | Verified Sprint 7 (`Y3S01-123`) |

This document is the Sprint 1 deliverable for `Y3S01-15`. Update it whenever an endpoint's path, method, or role requirement changes — it is the single source of truth both clients (React, Flutter) build against.
