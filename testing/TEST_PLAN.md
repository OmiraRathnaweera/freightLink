# Test Plan - FreightLink (SE3090 Assignment 2)

Version for submission: 2 Oct 2026. Branch: `test/a2-testing-evidence`.

## 1. Scope and objectives

We test the **same FreightLink system built for Assignment 1** - ASP.NET Core 8 Web API, PostgreSQL, React web app, Flutter mobile app and the Python LangGraph agentic AI service. No new application was built.

Objectives:

1. Show every selected technical area is tested with an appropriate automated tool, using normal, invalid, boundary and failure cases.
2. Prove one complete business workflow across the components (post load -> four AI agent steps -> Shipper approval -> Agency acceptance -> Trip) on a real PostgreSQL database.
3. Measure performance under concurrent load and scan for security weaknesses with tools, then interpret the results.
4. Record defects, fix the important ones and retest, with raw tool output kept as evidence.

Out of scope: real LLM output quality (the LLM is mocked/faked in automated tests), real e-mail/Cloudinary/OpenRouteService calls, production infrastructure (Caddy/Azure).

## 2. Quality risks and the areas that cover them

| Risk | Why it matters | Covered by |
|---|---|---|
| Unauthorised access / role escalation | Shippers, agencies, drivers and admins see different data | Area A + F2 (TC-A-08/09, TC-F-01..14) |
| Wrong business state (loads, invoices, trips) | Money and cargo | Area A (TC-A-05..11), E |
| Database integrity / migrations | Data loss, startup failure | Area B (TC-B-01..06) |
| AI recommends or approves unsafely | Agent must never act without Shipper approval | Area G (TC-G-01..14), E (TC-E-03/04) |
| Prompt injection via cargo text | Attacker-controlled free text reaches the LLM | TC-G-07/08 |
| Client forms accept bad input / show fake data | UX and trust | Areas C and D |
| Slow or failing under load | Many concurrent shippers | Area F1 |
| Backend outage | Must fail loudly, not fabricate results | TC-G-09, TC-C-08 |

## 3. Testing areas, tools and responsibilities

"Responsible" is taken from `git log` on the test paths **before** this branch (commits touching each path). The team must confirm or change these before submission; each member has to be able to run and explain their own tests in the viva.

| Area | What is tested | Tool / framework | Test location | Commits by author (pre-branch) |
|---|---|---|---|---|
| A Backend/API | Unit, service logic, validation, controllers, auth and role authorization, API integration | xUnit, `WebApplicationFactory`, EF Core InMemory, Postman collection | `backend/FreightLink.Api.Tests` (`Unit/`, `Integration/`) | Nuwantha 58, OmiraRathnaweera 16, Nethmi Balasooriya 9, AdeeshaHimal 5 |
| B Database | Unique constraints, FK rollback, migrations, startup seeding on **real PostgreSQL** | xUnit + Testcontainers.PostgreSql | `Integration/DatabaseConstraintTests`, `MigrationTests`, `TransactionRollbackTests`, `EndToEnd/...Postgres...` | (same as A) |
| C React web | Components, form validation, protected/public routes, API hooks, loading/error/empty states | Vitest, React Testing Library, jsdom | `frontend/src/**/tests` | Nuwantha 70, OmiraRathnaweera 8, AdeeshaHimal 8, Nethmi Balasooriya 4 |
| D Flutter | Unit, widget, form validation, navigation, API-error handling | `flutter_test`, mocktail | `mobile/freightlink_mobile/test` | Nuwantha 23, OmiraRathnaweera 8, AdeeshaHimal 1 |
| E Integration/E2E | Complete workflow on PostgreSQL; approval-gate and ownership negatives; cross-platform auth contract | xUnit + Testcontainers | `backend/.../EndToEnd`, `CrossPlatformAuthConsistencyTests`, Flutter/React auth-consistency tests | Nuwantha (+ branch work) |
| F1 Performance | Load 10->50->100 VUs on loads API; agent callback persistence latency | k6 (Docker) | `backend/PerformanceTests` | Nuwantha |
| F2 Security | Authenticated OWASP ZAP API scan; auth bypass, token tampering, role escalation, mass assignment, injection, internal key, hardening headers | OWASP ZAP (Docker), xUnit | `Integration/SecurityTests.cs`, `testing/scripts/run-nfr.sh` | branch work |
| G Agentic AI | Approval enforcement, tool allow-list, boundaries, prompt injection, fail-closed validation, contracts, safe failure | pytest, httpx ASGI transport, unittest.mock | `agent/tests` | Nuwantha 11, OmiraRathnaweera 5, Nethmi Balasooriya 1 |

### Non-functional justification

Performance and security are mandatory and are done with tools (k6, ZAP). We chose **not** to add accessibility, usability or compatibility testing in this round: the system's highest risks are authorization, data integrity and unsafe AI behaviour, and the remaining time was better spent on those. Reliability/recovery is covered in part by the safe-failure tests (TC-G-09/10, TC-C-08, TC-D-07/08).

## 4. Test design

Each area includes the four case types the brief asks for. Examples: **normal** (valid login, valid load), **invalid** (weak passwords, SQL/XSS strings, junk Authorization header), **boundary** (pickup window reversed, vehicle weight limits, `distance_km` 0 / 0.001, expired token), **failure** (wrong role, other shipper's load, backend down, DB FK violation, injected prompts). The curated list is in `TEST_CASES.md`; every executed test is in `evidence/test-register.csv`.

## 5. Environment

| Item | Value |
|---|---|
| Machine | macOS 27.0.1 (arm64), Docker 29.7.2 |
| .NET / Node / Flutter / Python | SDK 9.0.316 (project pins 8.0.x via `global.json`) / 24.18.1 / 3.47.2 / 3.12.13 (uv) |
| Database for DB tests | `postgres:16-alpine` via Testcontainers (ephemeral) |
| Database for other API tests | EF Core InMemory (fast); DB-specific behaviour is deliberately tested only on PostgreSQL |
| Performance / security target | API published from the working tree, run against a throwaway `postgres:16-alpine`, started by `testing/scripts/run-nfr.sh` |
| Tool versions | k6 v2.3.0, OWASP ZAP 2.17.0 |
| Secrets | None used. The runner uses throwaway values and never reads the real `.env`. |

CI (`.github/workflows/*-ci.yml`) runs all four suites on push and pull request and now uploads test results/coverage as artifacts.

## 6. Schedule

| Date | Activity | State |
|---|---|---|
| 19-29 Sep | Existing unit/integration suites written (see commit history) | Done |
| 30 Sep - 1 Oct | Postgres/migration/transaction/E2E tests, k6 scripts | Done |
| 2 Oct | Security tests, ZAP scan, Postgres E2E workflow, agent safety tests, defect fixing and retest, evidence and documents | Done (this branch) |
| 3-4 Oct | Each member rehearses and extends their own tests; assemble PDF report; confirm AI declaration | To do |
| 5 Oct | Submit on CourseWeb | - |

## 7. Entry / exit criteria

- Entry: stack builds; Docker running; dependencies restored.
- Exit: all automated tests pass; no open High/Medium defect; performance thresholds met; ZAP shows no High/Medium alerts; evidence regenerated and `build_test_docs.py` exits 0.

## 8. Traceability matrix (feature / risk -> test cases)

| Feature / risk | Test cases |
|---|---|
| Authentication & sessions | TC-A-01..04, TC-A-12, TC-D-04/05, TC-F-01..06 |
| Role & ownership authorization | TC-A-08/09, TC-C-01..03, TC-D-02/03, TC-E-04, TC-F-07..09 |
| Load lifecycle & validation | TC-A-05..07, TC-C-04..07, TC-D-06/07 |
| Invoicing / pricing | TC-A-10/11, TC-C-08 |
| Database integrity & migrations | TC-B-01..06 |
| End-to-end freight workflow | TC-E-01..05 |
| Human approval of AI matches | TC-C-09/10, TC-E-03, TC-G-01..03 |
| Agent tool safety & prompt injection | TC-G-04..08 |
| Safe failure / recovery | TC-G-09..14, TC-C-08, TC-D-08 |
| Injection / data exposure | TC-F-10..13 |
| Service-to-service security | TC-F-12, TC-F-15 |
| Performance | `evidence/k6` (two scripts) |
| Scan-found issues | DEF-001..006 in `DEFECT_REPORT.md` |

## 9. Known limitations (stated honestly)

- The "E2E" workflow is an API-level test on real PostgreSQL. The Python agent's callbacks are replayed against the real internal endpoints; **no browser (Playwright) or mobile `integration_test` runs exist**, and the LLM is never called.
- React API calls are mocked at module level, not with MSW.
- The performance run is on a laptop against a local container, so absolute numbers are indicative only. The agent latency script measures backend persistence, not LLM time.
- ZAP ran an API scan from the OpenAPI document as a Shipper; other roles' endpoints received 403 and were only lightly exercised.
- Backend branch coverage is about 52% (line coverage about 92%).
- Individual contribution evidence is uneven (see commit counts above).
