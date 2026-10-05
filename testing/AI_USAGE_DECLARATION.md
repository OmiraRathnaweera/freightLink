# AI Usage Declaration (DRAFT - team must review and complete)

> This draft describes what actually happened in the repository. Each member must add their own AI usage for their own work and re-map the headings to the module's CLEAR template if it differs. Do not submit work you cannot explain or reproduce in the viva.

## Tool

Claude Code (Anthropic), run in the terminal against this repository on 2 Oct 2026.

## What the AI did

| Item | AI contribution | Human verification expected |
|---|---|---|
| `SecurityTests.cs` (52 security cases) | Drafted test cases and code | Run the suite; read each test; confirm each failure was real before the fix (`evidence/defects/*BEFORE*.log`) |
| `LoadToTripWorkflowPostgresEndToEndTests.cs` | Drafted E2E workflow on Testcontainers; two initial assumptions were wrong and were corrected after running | Re-run; explain the flow |
| `agent/tests/Unit/test_safety_gates.py` | Drafted approval, allow-list, boundary and safe-failure tests; mutation-checked the approval gate | Re-run; explain why each assertion matters |
| Fixes for DEF-001..004, DEF-006 | Proposed and applied the code changes | Review the diff (`git log --grep=DEF-`) |
| `run-nfr.sh`, `build_test_docs.py`, CI artifact upload | Wrote scripts | Run them; read the output |
| Documents in `testing/` | Drafted from the real tool output | Check numbers against `evidence/`; edit wording |
| Existing test suites (written before this branch) | Not part of this declaration - authors must declare their own AI use | - |

## Limitations the team must acknowledge

- AI-generated tests were verified by running them against the real system, but passing tests do not prove the absence of defects.
- Responsibilities and individual contributions in the Test Plan are taken from Git history, not from this AI work.

## Verification statement

(Each member signs here after reproducing at least one test: name, test run, date.)
