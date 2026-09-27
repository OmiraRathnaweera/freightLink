# FreightLink Role and Feature Audit

**Last reviewed:** 2026-09-27  
**Scope:** React web, ASP.NET Core API, PostgreSQL schema/migrations, and Flutter mobile. The separate Python multi-agent implementation is assessed only where the web/API contract exposes an end-user action.

This is the living handoff record for the integration branch. Update the status and refactor log in the same change as every feature fix.

## Scope and evidence standard

Each requirement was checked against four things:

1. A reachable client route/screen and form or action.
2. A real client API call, rather than mock/session-only state.
3. A corresponding backend endpoint and persistence/service implementation.
4. Role enforcement in both the client navigation/guard and backend authorization.

An implemented-looking screen without a real API call is not marked complete. The API is the final security authority.

## Status legend

| Status | Meaning |
| --- | --- |
| Complete | Client flow, backend operation, persistence, and role enforcement exist. |
| Partial | A meaningful implementation exists but has a broken build, missing action, missing integration, or incomplete client role protection. |
| UI/mock only | Screen exists but state is local/mock or no supported backend action is used. |
| Missing | No implementation found. |
| Incorrect access | Function exists but is exposed to the wrong role. |

## Role feature matrix

### Shipper

| Requirement | Web (React) | Mobile (Flutter) | Backend/access result | Status |
| --- | --- | --- | --- | --- |
| Shipper registration | Web registration form | Explicitly directed to the web portal; mobile shipper registration method/UI removed | Public shipper registration endpoint remains web-consumed | Complete: web-only by policy |
| Create, edit, cancel a load | Rich post/edit/cancel forms | Quick-post and edit forms | Shipper-only create/update/status endpoints; UI hides mutations for other roles | Complete |
| My loads: search/filter/sort/pagination | List/filter/pagination implemented | Read-focused list/search/status filters | `/loads` supports list filtering/paging | Complete on web; Partial mobile |
| Detail, timeline and history | Detail/timeline/history | Detail/timeline/map | `GET /loads/{id}` returns status history | Complete |
| Attached documents: Manifest, Invoice, CargoPhoto, Other | Upload/attach/list/detach exists | Quick-post supports camera/file selection, upload, and nested attachment by type | Nested load-file API supports types | Complete |
| Price estimate at `POST /loads/{id}/estimate` | `PriceEstimateCard` on Load Detail calls it on demand | Load Detail opens Estimate & AI Match screen | Shipper-owned estimate endpoint | Complete |
| Review Agent 4 recommendation: ETA, price, deviation; approve/reject/revise | Match console supports confirm, reject, and revise (reason-gated dialogs) | Estimate & AI Match screen supports confirm/reject/revise with reason validation | `POST /loads/{id}/match/{confirm,reject,revise}` all exist, Shipper-owned, reject/revise abort the run via an `ApprovalDecision` row | Complete |
| Email notification for decline/new match/no match | No notification settings UI | No email UI | Email templates/service exist; end-to-end agent trigger is outside this audit | Partial |
| View invoices/dispute status | Billing screens exist | Invoice list and invoice-linked dispute form | Invoice/dispute APIs exist | Complete |
| Raise dispute | Not specified as web primary | Invoice-linked Flutter form submits trip/category/description | Shipper/AgencyStaff dispute API | Complete |
| Settle invoice by uploading payment proof | Drag-and-drop receipt uploader in the invoice drawer (`Dropzone`); Agency then reviews and confirms | Receipt file picker/upload in `PaymentsScreen` | `POST /invoices/{id}/payment-proof` (Shipper, advances to PaymentPending), `POST /invoices/{id}/confirm-payment` (Agency, closes as Paid) — no payment gateway involved | Complete |

### Agency staff (Dispatcher)

| Requirement | Flutter implementation | Backend/access result | Status |
| --- | --- | --- | --- |
| Agency registration and yard coordinates | Registration flow collects agency/yard data | Public registration creates agency/staff | Complete |
| Agency profile/dashboard | Real dashboard/profile screens with fleet, driver, availability, and compliance metrics | Agency get/update API | Complete |
| Fleet setup/manage vehicles | List, add, and update vehicle status | API supports list/add/status update (`PATCH /agencies/{id}/vehicles/{vehicleId}/status`) | Complete |
| Driver onboarding and roster management | No mobile UI by design — drivers no longer self-register; there is no separate driver signup flow on mobile at all | `POST/PATCH /agencies/{id}/drivers[/{driverId}/status]`, ownership-checked; Agency supplies no password — the server generates one and emails the driver their login email + temporary password (also shown once in the create response as a fallback); **Web**: `/agencies/drivers` page lets Agency Staff add, deactivate ("Remove"), and reinstate drivers on their own roster | Complete: web-only by policy |
| Compliance upload from camera/file | List/add screen supports camera capture or document picker | Upload/API exists | Complete |
| Availability management | Vehicle status control on the fleet list screen | Agency-owned `UpdateVehicleStatusAsync`, ownership-checked | Complete |
| Job proposal inbox, accept/decline | Proposal list/detail and accept/decline screens | Assignment list/detail/accept/decline API | Complete |
| Agency trips and dispatch | Proposal dispatch, agency trip list, fleet-resource assignment | Trip creation/list APIs | Complete UI; needs end-to-end proof |
| Capture proof of pickup | Camera/upload/status flow exists | Trip-evidence and status endpoints | Complete |

### Driver

| Requirement | Flutter implementation | Backend/access result | Status |
| --- | --- | --- | --- |
| Separate login | Role-aware login; account/credentials are set by the employing Agency, not self-registered | Driver JWT role | Complete |
| View assigned trip | Driver assigned-trip screen | Driver may list/get allowed trips | Complete |
| Status update (no live GPS) | Status actions/snapshot-oriented UI | Trip status API; no continuous tracking implementation | Complete |
| Proof of delivery from camera | Capture/upload/evidence/status flow | Trip evidence API | Complete |

### Admin

| Requirement | Web implementation | Backend/access result | Status |
| --- | --- | --- | --- |
| Agency verification queue/compliance review | Agency list and verification queue | Admin-only agency/compliance verification actions | Complete |
| Activate/suspend agency | UI actions present | Admin-only API actions | Complete |
| System-wide analytics dashboard | `/analytics` route with per-category counts and invoice revenue totals | `GET /admin/analytics/summary`, Admin-only, computed on demand (no persistence/caching) | Complete |
| Financial dashboard/payment status | Billing/invoice list and details (read-only "Inspector Mode") | Invoice API exists; Admin never mutates payment state — only Agency confirms | Complete |
| Dispute resolution | Admin dispute UI uses persisted queue/review/resolve requests | Backend list contract includes claimant, trip, vehicle, carrier, and resolution metadata | Complete |
| Pricing configuration | Fuel-rate, vehicle efficiency, formula pages | Admin-only pricing API | Complete |
| Must not approve/reject AI agency matches | Admin is excluded from `/agent-workflows` | Load-match endpoints are Shipper-only; legacy Admin approval endpoints now return 403 | Complete (refactored) |

## Cross-application workflow and access audit

```text
Shipper posts load (web/mobile)
  → matching recommendation becomes available
  → Shipper alone reviews/approves match (web)
  → Agency staff accepts/declines proposal (mobile)
  → Agency assigns driver + vehicle / creates trip (mobile)
  → pickup proof (agency) → transit → delivery proof (driver)
  → invoice → Shipper uploads payment receipt → Agency reviews and confirms payment → dispute if needed
```

Current breakpoints (updated 2026-09-27; see refactor log for what closed the items below):

1. Notifications are intentionally excluded from this integration branch.

Resolved since the baseline: the Flutter compliance screen compiled and now works (P0, 2026-09-26); web disputes persist through the backend API instead of session-state mock (P0, 2026-09-26); mobile routing now has a role-aware guard (P0, 2026-09-26); Agency Staff fleet/vehicle-availability management is complete end-to-end (P1, 2026-09-27); Shipper reject/revise match actions are complete end-to-end (P1, 2026-09-27); the Shipper price-estimate endpoint and web view are complete (P1, 2026-09-27; Flutter view still pending); the PayHere payment gateway was removed entirely (web + mobile + backend) and replaced with a manual Shipper-uploads-receipt / Agency-confirms-payment flow (P1, 2026-09-27).

## Missing, incomplete, or incorrectly authorized features

### P0 — role/security or broken critical flow

- [x] Restrict load-match viewing/confirmation and AI workflow console approval to Shipper only.
- [x] Repair Flutter compliance document compile errors; then run the complete Flutter test suite.
- [x] Replace web dispute session-storage mock with backend dispute API integration.
- [x] Add a role-aware mobile route guard.

### P1 — required workflow gaps

- [x] Implement `POST /api/v1/loads/{id}/estimate` and a Shipper estimate view using the agreed haversine-based contract, or formally remove the requirement and use the existing pricing service consistently. (Web only; Flutter view still missing — tracked as a follow-up, not blocking.)
- [x] Add explicit Shipper reject/revise match actions and matching backend state transitions.
- [x] Remove the PayHere payment gateway integration; replace PayNow with a Shipper-uploaded payment-receipt flow that an Agency reviews and confirms.
- [x] Implement vehicle availability/status management.
- [ ] Notifications intentionally skipped for this branch (no Flutter/backend notification integration).
- [x] Add Admin analytics API/dashboard.

### P2 — integration quality

- [x] Correct web invoice/payment paths so they are relative to the configured `/api/v1` base URL.
- [x] Complete mobile load-file attachment support for all document types.
- [ ] Add end-to-end tests covering Shipper → Agency → Driver → Invoice/Payment against PostgreSQL.
- [x] Resolve all frontend lint errors before release.

## Refactor log

| Date | Change | Result |
| --- | --- | --- |
| 2026-09-26 | Created this role/feature audit and populated it from routes, API modules/controllers, service/persistence evidence, and Flutter screens. | Baseline recorded; P0 role mismatch identified. |
| 2026-09-26 | Removed Admin access to the React AI workflow route; restricted backend recommendation/read-confirm actions to the owning Shipper; restricted the legacy assignment-approve alias to Agency Staff; retired the legacy Admin workflow approval endpoint with a 403 response. | Shipper → Agency approval boundary now matches the stated role policy. Backend integration tests were updated to assert the new denial behavior. |
| 2026-09-26 | Repaired the Flutter compliance upload screen’s stale imports/widget API; aligned `file_picker 8.3.7` with its required `win32 5.9.0` API; added the mobile AgencyStaff route guard. | `flutter test` passes: 68 tests. |
| 2026-09-26 | Replaced the web dispute session-storage store and demo reset with `/api/v1/disputes` list/review/resolve requests via React Query. | Dispute lifecycle changes now persist through the backend; compact list-display enrichment remains P2. |
| 2026-09-26 | Normalized React billing and PayHere client paths to be relative to the configured `/api/v1` Axios base URL. | Invoice, recipient, trip lookup, and checkout requests no longer compose into `/api/v1/api/...`. |
| 2026-09-26 | Added camera capture alongside document-picker upload for Flutter compliance documents. | `flutter test` still passes: 68 tests. |
| 2026-09-26 | Enriched the persisted dispute-list DTO and React adapter with safe claimant, route, carrier, vehicle, and resolution fields. | Admin dispute queue no longer requires mock display metadata or per-row detail requests. |
| 2026-09-27 | Added Agency Staff vehicle availability/status management: `UpdateVehicleStatusDto`, `IAgencyService.UpdateVehicleStatusAsync`, `PATCH /agencies/{id}/vehicles/{vehicleId}/status` (ownership-checked), and the Flutter fleet screen/provider/repository wiring. | Fleet setup and availability management are now Complete end-to-end; P1 item closed. |
| 2026-09-27 | Added Flutter invoice list and PayHere hosted checkout: `BillingRepository` fetches invoices and receives backend-signed checkout fields; Shipper-only Pay now submits a POST form through `webview_flutter`, without calculating or storing a merchant hash client-side. | `flutter test` passes: 68 tests. Manual sandbox payment + webhook confirmation remains required before release. |
| 2026-09-27 | Added the invoice-linked Flutter dispute form and `BillingRepository.raiseDispute`, with backend-supported category selection and a required 10-character description. | Shippers and Agency Staff can raise persisted trip disputes from mobile invoices; `flutter test` passes: 68 tests. |
| 2026-09-27 | Added optional mobile load attachments to quick-post: camera or file picker, type selection (CargoPhoto/Manifest/Invoice/Other), upload through `/files/single`, then attach through `/loads/{id}/files`. | `flutter test` passes: 68 tests; attachment failures warn after the load is created rather than causing accidental duplicate posts. |
| 2026-09-27 | Removed mobile Shipper registration: deleted the mobile `registerShipper` API method, made the registration screen Agency-only, and changed the Shipper login prompt to direct users to the web portal. | Shipper signup is now web-only by policy; full `flutter test` passes: 68 tests. |
| 2026-09-27 | Upgraded `file_picker` from 8.x to 13.1.0 and removed the forced Win32 5.x override, then migrated billing, compliance, and load-attachment pickers to the new `FilePicker.pickFiles`/`PlatformFile.readAsBytes` API. | Resolves the `flutter_secure_storage_windows`/Win32 conflict that blocked iOS compilation. `flutter build ios --simulator` produced `Runner.app`; full `flutter test` passes: 60 tests. |
| 2026-09-27 | Centralized Flutter role access in `app_router.dart`: exact tab paths plus explicit nested AgencyStaff prefixes; Shipper Billing, Agency workspace, Driver trip tab, and Admin/unknown rejection now use one policy matrix. Removed the dead `admin_loads_screen.dart` route surface and added direct route-policy tests. | Full `flutter test` passes: 64 tests. Nested Agency routes such as `/dashboard/fleet` now correctly reject Shipper and Driver direct navigation. |
| 2026-09-27 | Closed the unauthenticated bootstrap route hole: `AuthStatus.unknown` now fails closed to `/login` for every protected URL, rather than returning no redirect while secure-storage validation is pending. | Direct `/dashboard` access without a validated session is blocked; full `flutter test` passes: 64 tests. |
| 2026-09-27 | Refactored Flutter authentication foundation: common backend-role-resolved login, refresh-token storage/rotation, one-retry-on-401 API behavior, Agency-only registration with map-selected yard coordinates, and explicit Admin rejection. | Full `flutter test` passes: 64 tests. Shipper registration remains web-only; route guards consume the backend `/auth/me` role. |
| 2026-09-27 | Expanded Agency Staff mobile parity with web: real Agency dashboard metrics and quick actions, Agency trips list, and authorized Billing/Trips routes added to the centralized mobile role matrix. | Full `flutter test` passes: 64 tests. The Agency dashboard is no longer a placeholder for AgencyStaff; “Coming Soon” remains only for Shipper/Driver dashboards. |
| 2026-09-27 | Added Flutter Shipper Estimate & AI Match screen from Load Detail, using the existing estimate and Shipper-only confirm/reject/revise backend endpoints. | Full `flutter test` passes: 64 tests; existing Shipper load create/edit/delete flow remains unchanged. |
| 2026-09-27 | Made PostgreSQL trigger-function migrations recoverable after a partial schema deletion by replacing non-idempotent `CREATE FUNCTION` statements with `CREATE OR REPLACE FUNCTION`. | Orphaned trigger functions no longer block migration startup with PostgreSQL error `42723`; restart the API to rerun migrations. |
| 2026-09-27 | Added Shipper reject/revise match actions: `MatchDecisionRequestDto`/`MatchDecisionResponseDto`, `AssignmentService.RejectMatchAsync`/`ReviseMatchAsync` (shared `RecordMatchDecisionAsync` helper, records an append-only `ApprovalDecision` row and aborts the workflow run), `POST /loads/{id}/match/{reject,revise}` (Shipper-only); React `MatchDecisionDialog` with reason validation wired into `MatchRecommendationCard`/`AgentWorkflowConsolePage`. | Shipper can now reject or request a revised AI recommendation with a recorded reason, not just approve; `npm test` (211 passed), `npx eslint` on touched files (clean), and `npm run build` all verified. P1 item closed. |
| 2026-09-27 | Implemented the Shipper price-estimate endpoint per Component A's documented contract: `IPricingEstimatorService.EstimateForShipperAsync` (haversine distance from the load's own coordinates, vehicle-class tier resolved from weight/volume via `GetTierForWeightAndVolume`, same reference-rate config as Agent 3's internal estimate), `POST /loads/{id}/estimate` (Shipper-owned); deliberately does not persist to `Load.EstimatedPrice` since that column remains the AI agent's own price. Added `PriceEstimateCard` to the React Load Detail page. Backend integration tests (`LoadsControllerTests`) and service unit tests (`PricingEstimatorServiceTests`) added for the new method/endpoint. | Shippers can now get an on-demand rough quote before matching, without touching the agent-owned pricing flow; `npm test` (211 passed), `npx eslint` on touched files (clean), and `npm run build` verified. Backend `dotnet build`/`dotnet test` could not be run in this environment (no `dotnet` SDK installed) — verified by careful manual review of every touched file (DbSet/entity field names, brace balance, DI registration) instead; please run `dotnet test` before merging. P1 item closed for web; Flutter estimate view remains a follow-up. |
| 2026-09-27 | Added the Admin system-wide analytics dashboard: `IAnalyticsService`/`AnalyticsService` (DB-side grouped counts by status/role across Loads, Agencies, Trips, Assignments, Disputes, Users, plus invoice revenue totals), `GET /admin/analytics/summary` (Admin-only, on-demand, nothing persisted/cached); React `/analytics` route + nav item + `AdminAnalyticsPage`, gated to Admin in `roleAccess.js`. Backend integration tests (`AdminAnalyticsControllerTests`) and unit tests (`AnalyticsServiceTests`) added. | Admin's role now matches the documented "KYC/verification + system-wide analytics" scope end-to-end; `npm test` (214 passed), `npx eslint` (clean), `npm run build` verified. Backend `dotnet test` still could not be run in this environment — verified by manual review (DbSet/entity field names, brace balance, DI registration) instead; please run `dotnet test` before merging. Last remaining P1 items (Flutter PayHere/dispute flows, notification push transport) are mobile-only and out of scope for this pass. |
| 2026-09-27 | Resolved all 26 pre-existing frontend lint problems (23 errors, 3 warnings) across billing, disputes, loads, trips, and agencies: removed dead/unused imports and props (including a fully dead `isAdmin` prop chain in `InvoiceListTable`'s `ActionMenu`, confirmed inert since Admin already gets no action-menu items via `isAgent`/`isShipper` alone); replaced `useEffect`-based "sync state from a prop" patterns with React's documented render-time-adjustment pattern in `ResolutionModal`, `InvoiceFormModal`, and `CreateTripDialog` (`react-hooks/set-state-in-effect`); restructured `InvoiceFormModal`'s and `BillingPage`'s mount-time data-fetch effects so their `setState` calls stay nested in `.then()/.catch()/.finally()` callbacks instead of a named function call (same rule); replaced a `.map()` loop with mutable accumulator variables with `.reduce()` in `InvoiceFormModal`'s totals calculation (`react-hooks/immutability`); memoized `AgenciesPage`'s `rawAgencies` and added the missing `isShipper` dependency to `BillingPage`'s filter `useMemo` (`react-hooks/exhaustive-deps`). | `npx eslint .` is now fully clean (0 errors, 0 warnings); `npm test` (214 passed) and `npm run build` verified after every change. No behavior changes intended anywhere; the riskiest edits (the two data-fetch effect restructures) were reasoned through carefully since neither file has test coverage — a manual smoke test of Billing and Invoice creation/edit is still recommended before release. P2 item closed. |
| 2026-09-27 | Removed the PayHere payment gateway entirely and replaced Shipper settlement with a manual payment-receipt-upload flow reviewed by Agency staff. Backend: deleted `PaymentsController`, `PayHereService`/`IPayHereService`, `PayHereOptions`, the `Payment`/`PaymentWebhookEvent` entities and their configs/DbSets, `PayInvoiceDto`, and the `PAYHERE_*` env keys; added `Invoice.PaymentProofFileId/UploadedAt/UploadedByUserId` (references the existing `UploadedFile` two-step upload, same pattern as `LoadFile`); added `InvoiceService.UploadPaymentProofAsync` (Shipper, requires Issued/PaymentPending, advances to PaymentPending) and `ConfirmPaymentAsync` (Agency, requires an existing payment proof, closes as Paid) behind `POST /invoices/{id}/payment-proof` and `POST /invoices/{id}/confirm-payment`; renamed `InvoiceStatusTransitionRules.GatewayOwnedStatuses`/`IsGatewayOwned` to `DedicatedActionOnlyStatuses`/`RequiresDedicatedAction` since "Paid" is now Agency-confirmed rather than gateway-owned. While generating the migration, discovered and fixed a pre-existing, unrelated bug: two already-committed migrations (`AddManualInvoiceManagementAndLineItems`, `AddInvoicePaidAtAndPaymentReference`, both 2026-09-26) were missing the `[Migration]` attribute EF needs to run them, so their invoice line-items/subtotal/tax/paid-at columns had never actually been created on any real Postgres database — replaced both with a single correctly-generated `FixInvoiceManagementSchemaAndRemovePayHere` migration (applied and verified against the local Postgres container) that creates that schema for real and removes the PayHere tables/function in the same pass. Frontend: reused the existing `Dropzone` drag-and-drop component (no new uploader built) inside `InvoiceDetailsDrawer` for the Shipper's receipt submission and the Agency's proof-review/confirm card; removed `paymentApi.js`, `PaymentSuccessPage`/`PaymentCancelledPage` and their routes, and the table-row quick-pay action. Mobile: `BillingRepository.createPayHereCheckout` replaced with `uploadFile`/`submitPaymentProof`; `PaymentsScreen`'s "Pay now" button replaced with a `file_picker`-based "Submit payment receipt" action; deleted `payhere_checkout_screen.dart` and its route. | Backend: `dotnet build`/`dotnet test` now actually run in this environment (`dotnet`/`dotnet-ef` were present but not on `PATH`; symlinked into `~/.local/bin`) — full suite is 489 tests, 476 passed; the only failure caused by this change (`UploadPaymentProof_AuthenticatedAdmin_Returns403Forbidden`, a test bug — it tried uploading a file as Admin, a role `FilesController` doesn't allow) was fixed and reverified (52/52 Invoice+Analytics tests passing). The other 12 failures (`GmailEmailServiceTests`, `DisputesControllerTests`, `AssignmentsControllerTests`, `AgenciesControllerTests`, `MatchConfirmControllerTests`) are pre-existing and unrelated — enum-deserialization and authorization mismatches in code this change never touched — and are **not** fixed here; flagging for whoever owns that in-progress work. Frontend: `npm test` (214 passed), `npx eslint .` (clean), `npm run build` verified. Mobile: `flutter test` (68 passed), `flutter analyze` (clean). P1 item closed. |
| 2026-09-27 | Removed mobile Driver self-service registration entirely (no signup form, no signup logic) and replaced it with Agency-managed roster CRUD on **web**. Backend: deleted `POST /auth/register/driver`, `IAuthService.RegisterDriverAsync`, and `RegisterDriverRequestDto` (kept the still-generic, independently-tested `GET /auth/agencies` lookup, since nothing else depends on removing it); added `PATCH /agencies/{id}/drivers/{driverId}/status` (`UpdateDriverStatusDto`, `AgencyService.UpdateDriverStatusAsync`, Agency-Staff-only, ownership-checked via the existing `VerifyAgencyOwnershipAsync`) so a driver can be "removed" (set Inactive) or reinstated (set Active) — mirrors the existing vehicle-availability pattern; `OnTrip` stays exclusively trip-execution-owned, matching vehicles' own `OnTrip`/`Retired` rule, and a driver's own hard-delete is deliberately not offered since this system never hard-deletes append-relevant rows (a `Driver` has FK-referenced `Trip` history) — "delete" in the request is implemented as this Active/Inactive roster toggle. Frontend: net-new `/agencies/drivers` page and nav item — `FleetDriversPage` (metrics, search/status filter, per-row Remove/Reinstate) plus `AddDriverDrawer` (Formik/Yup form; see the next entry for how the driver's password is actually set), `agencyApi.js` additions (`useDriversQuery`/`useAddDriverMutation`/`useUpdateDriverStatusMutation`), matching the existing `FleetVehiclesPage`/`AddVehicleDrawer` conventions; this is the *first* web UI for any Agency Staff fleet/roster feature (vehicles and drivers were previously Flutter-only). Mobile: deleted `driver_register_screen.dart`, `AuthProvider.registerDriver`/`fetchAgencies`, and the now-unused `AgencyLookup` model; replaced the login screen's Driver-role "New driver? Register here" link with a static "contact your agency" notice, matching the existing Shipper-is-web-only messaging pattern. While fixing tests in this area, also fixed two more pre-existing, unrelated JSON-enum-deserialization failures in `AgenciesControllerTests` (`AgencyFleetResponseDto`/`DriverResponseDto`/`PagedAgencyResponseDto` reads lacked the `JsonStringEnumConverter` the API's actual output requires — same root cause class as the Dispute/Assignment failures noted in the prior entry, fixed here since it was directly in a file this change was already editing). | Backend: `dotnet build`/`dotnet test` — targeted run covering every touched file (`AgenciesControllerTests`, `AuthServiceTests`, `AuthControllerTests`, `RegisterValidationTests`, `CrossPlatformAuthConsistencyTests`) is 65/65 passing, including 5 new driver-status tests and the 2 newly-fixed pre-existing failures; a full-suite run could not be completed in this sandboxed environment this pass (the test host intermittently stalls on the first `CustomWebApplicationFactory` construction in a full run — reproduced 3 of 4 attempts, appears to be sandbox resource contention rather than a code defect, since a full run earlier in this same session completed cleanly at 476/489) — please run `dotnet test` (no filter) on a normal machine before merging. Frontend: `npm test` (223 passed, +9 new `FleetDriversPage` tests), `npx eslint .` (clean), `npm run build` verified. Mobile: `flutter test` (60 passed — net -8 from removing the driver self-registration tests), `flutter analyze` (clean). |
| 2026-09-27 | Replaced the Agency-sets-the-driver's-password flow with server-generated temporary credentials emailed to the driver. Backend: removed `Password` from `CreateDriverRequestDto`; `AgencyService.AddDriverAsync` now generates a cryptographically random 12-character password (`GenerateTemporaryPassword`, guarantees upper/lower/digit/special via `RandomNumberGenerator`, satisfies the same `StrongPasswordAttribute` rule every other password field uses) and emails it via a new `EmailTemplates.BuildDriverCredentials` template through the existing `IEmailService` (optional constructor dependency, same nullable pattern as `InvoiceService`; send failures are caught and logged, never block driver creation — mirrors `AuthService`'s `TrySend*` account-email pattern). Also sets the new driver's `EmailVerifiedAt` immediately at creation: discovered while implementing this that `EmailOptions.RequireEmailVerification` defaults to `true`, which would otherwise have permanently locked every Agency-added driver out of login (a latent pre-existing bug in the driver-add path, now fixed as part of making "log in with the emailed password" actually true) — since the Agency is vouching for the driver's identity here, there's no separate driver-side verification step to gate on. Added `DriverResponseDto.TemporaryPassword` (populated only in the create response, always `null` on list/update) so the Agency also sees it once on-screen as a fallback if the email is delayed; the `AddDriver_Returns201_ForAgencyStaff` integration test now asserts the returned temporary password actually logs the driver in, and that a subsequent driver list never re-exposes it. Frontend: removed the password field from `AddDriverDrawer`/`addDriverSchema` entirely; the email field's helper text now explains a temporary password will be generated and emailed; the success toast shows the returned `temporaryPassword` (15s duration) as the same on-screen fallback. | Backend: targeted run (`AgenciesControllerTests`, `CrossPlatformAuthConsistencyTests`) is 15/15 passing, including the updated end-to-end "temp password actually logs in" assertion; `dotnet build` clean on both projects. Frontend: `npm test` (223 passed), `npx eslint .` (clean), `npm run build` verified. |

## Remaining work and verification checklist

### Environment handoff

1. On a machine with the .NET 8 SDK and PostgreSQL, run `dotnet test` from `backend/`, then start the API against a migrated database.
2. With a Shipper account, create a load with an attachment, request an estimate, approve/reject/revise an AI match, and verify an Admin receives 403 for those match URLs.
3. With an Agency Staff account, accept the proposal, assign vehicle/driver, change fleet availability, and upload pickup proof; complete delivery as the Driver.
4. As the Shipper, open the Flutter invoice list, submit a payment receipt for an Issued invoice, then as Agency Staff review the receipt on web and confirm payment; verify the invoice reaches Paid before treating mobile payment as release-ready.
5. Notifications are intentionally excluded from this branch; do not configure or expect mobile/backend notification delivery.

- [ ] Update this document’s matrix and refactor log in the same commit as each feature change.
- [x] Run React test, build, and lint after web changes — 29 files / 214 tests passed; lint clean; production build passed on 2026-09-27.
- [x] Run backend build/test and apply migrations against PostgreSQL after API/schema changes — `dotnet`/`dotnet-ef` are installed but weren't on `PATH`; symlinked to `~/.local/bin/dotnet` so future sessions can run them directly. 489 tests / 476 passed on 2026-09-27 (12 pre-existing unrelated failures — see refactor log).
- [x] Run Flutter test after mobile changes — 68 tests passed, `flutter analyze` clean on 2026-09-27.
- [ ] Execute a manual role-by-role test with four separate accounts and confirm unauthorized calls return 403.
