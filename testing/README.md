# Testing (SE3090 Assignment 2)

| File | Purpose |
|---|---|
| `TEST_PLAN.md` | Scope, objectives, areas, tools, environment, responsibilities, schedule, traceability |
| `TEST_CASES.md` | Test case document (generated; actual result and status looked up from tool output) |
| `TEST_EXECUTION_SUMMARY.md` | Executed / passed / failed, coverage, k6, ZAP, defects, conclusion (generated) |
| `DEFECT_REPORT.md` | Defects with severity, steps, evidence, fix and retest result (generated from `defects.json`) |
| `AI_USAGE_DECLARATION.md` | AI assistance declaration (draft for the team to confirm) |
| `evidence/` | Raw tool output: TRX/JUnit/JSON test results, Cobertura coverage, k6 summaries, ZAP reports, before/after defect logs, `test-register.csv` |
| `scripts/run-nfr.sh` | Starts a throwaway API + PostgreSQL and runs k6 and ZAP |
| `scripts/build_test_docs.py` | Rebuilds the generated documents from `evidence/` |

## Dashboard (run tests and see results/times in one page)

```bash
python3 testing/dashboard/server.py        # then open http://127.0.0.1:8765
```

One page with Run buttons for the backend, React, Flutter and agent suites (plus k6 and ZAP), live console output, pass/fail counts, wall-clock and per-test times, slowest tests, coverage, k6/ZAP results and the defect list. It needs no installs (Python standard library only), binds to localhost only and runs fixed commands. Running a suite overwrites that suite's files in `evidence/`, so the evidence always matches the screen. Use it for screenshots; press Run all, then `python3 testing/scripts/build_test_docs.py` to refresh the generated documents.

## Run everything (command line)

Prerequisites: Docker, .NET SDK 8+, Node 22+, Flutter stable, [uv](https://docs.astral.sh/uv/), Python 3.

```bash
# 1. Backend (xUnit; Testcontainers starts PostgreSQL, so Docker must be running)
cd backend
TESTCONTAINERS_RYUK_DISABLED=true dotnet test backend.sln --configuration Release \
  --collect:"XPlat Code Coverage" --results-directory ../testing/evidence/backend/raw \
  --logger "trx;LogFileName=backend.trx"

# (gzip the large coverage report for storage: gzip -9 -c <path>/coverage.cobertura.xml > ../testing/evidence/backend/coverage.cobertura.xml.gz)

# 2. Frontend (Vitest)
cd ../frontend
npx vitest run --reporter=default --reporter=junit --outputFile.junit=../testing/evidence/frontend/junit.xml

# 3. Agent (pytest)
cd ../agent
uv run pytest tests --junitxml=../testing/evidence/agent/junit.xml

# 4. Mobile (Flutter)
cd ../mobile/freightlink_mobile
flutter test --coverage --file-reporter json:../../testing/evidence/mobile/flutter-tests.json

# 5. Performance (k6) and security (ZAP) against a throwaway stack
cd ../..
testing/scripts/run-nfr.sh          # or: run-nfr.sh k6   /   run-nfr.sh zap

# 6. Regenerate the documents from the evidence (exits non-zero on any mismatch)
python3 testing/scripts/build_test_docs.py
```

The NFR runner never reads your `.env`; it starts the API from a temporary directory with throwaway settings. Run a single class with `dotnet test --filter "FullyQualifiedName~SecurityTests"`.

## Consistency guarantees

`build_test_docs.py` fails if a curated test case or a defect's retest test is missing from, or failed in, the real tool output. Counts in `TEST_EXECUTION_SUMMARY.md` are parsed from the raw files in `evidence/`, not typed.
