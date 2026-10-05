#!/usr/bin/env python3
"""Local runner for the FreightLink test dashboard (testing/dashboard/index.html).

A browser page cannot start `dotnet`, `flutter`, `pytest` or `vitest`, so this tiny server (Python
standard library only, no installs) does it on request and feeds the results to the page.

    python3 testing/dashboard/server.py            # then open http://127.0.0.1:8765
    python3 testing/dashboard/server.py --port 9000

Security: binds to 127.0.0.1 only, runs only the fixed commands below (the browser can pick a suite
name from an allow-list, nothing else) and rejects cross-origin POSTs. It never reads any .env file.

Running a suite overwrites that suite's files in testing/evidence/ (that is the point: the evidence
folder then matches what the dashboard shows).
"""
import argparse
import gzip
import json
import os
import shutil
import subprocess
import sys
import threading
import time
import xml.etree.ElementTree as ET
from collections import deque
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EV = ROOT / "testing" / "evidence"
HERE = Path(__file__).resolve().parent

SUITES = {
    "backend": dict(
        label="Backend API (xUnit)", cwd=ROOT / "backend", env={"TESTCONTAINERS_RYUK_DISABLED": "true"},
        cmd=["dotnet", "test", "backend.sln", "--configuration", "Release", "--collect:XPlat Code Coverage",
             "--results-directory", str(EV / "backend" / "raw"), "--logger", "trx;LogFileName=backend.trx"],
    ),
    "frontend": dict(
        label="React web (Vitest)", cwd=ROOT / "frontend", env={},
        cmd=["npx", "vitest", "run", "--reporter=default", "--reporter=junit", f"--outputFile.junit={EV / 'frontend' / 'junit.xml'}"],
    ),
    "mobile": dict(
        label="Flutter mobile", cwd=ROOT / "mobile" / "freightlink_mobile", env={},
        cmd=["flutter", "test", "--coverage", "--file-reporter", f"json:{EV / 'mobile' / 'flutter-tests.json'}"],
    ),
    "agent": dict(
        label="Agentic AI (pytest)", cwd=ROOT / "agent", env={},
        cmd=["uv", "run", "pytest", "tests", f"--junitxml={EV / 'agent' / 'junit.xml'}", "-q"],
    ),
    "k6": dict(
        label="Performance (k6)", cwd=ROOT, env={}, cmd=["bash", str(ROOT / "testing" / "scripts" / "run-nfr.sh"), "k6"], nfr=True,
    ),
    "zap": dict(
        label="Security scan (ZAP)", cwd=ROOT, env={}, cmd=["bash", str(ROOT / "testing" / "scripts" / "run-nfr.sh"), "zap"], nfr=True,
    ),
}
TEST_SUITES = ["backend", "frontend", "mobile", "agent"]

# ---------------------------------------------------------------- run state
LOCK = threading.Lock()
STATE = {sid: dict(status="idle", log=deque(maxlen=600), started=None, elapsed=0.0) for sid in SUITES}
QUEUE = []          # suite ids waiting
CURRENT = {"id": None}
WORKER = {"thread": None}


def log(sid, line):
    STATE[sid]["log"].append(line.rstrip("\n"))


def meta_path(sid):
    return EV / sid / "run-meta.json"


def post_backend():
    raw = EV / "backend" / "raw"
    cov = next(raw.glob("**/coverage.cobertura.xml"), None)
    if cov:
        with open(cov, "rb") as src, gzip.open(EV / "backend" / "coverage.cobertura.xml.gz", "wb", 9) as dst:
            shutil.copyfileobj(src, dst)
    for child in raw.iterdir():
        if child.is_dir():
            shutil.rmtree(child, ignore_errors=True)


def post_mobile():
    lcov = SUITES["mobile"]["cwd"] / "coverage" / "lcov.info"
    if lcov.exists():
        shutil.copy(lcov, EV / "mobile" / "lcov.info")


def run_one(sid):
    spec = SUITES[sid]
    st = STATE[sid]
    st.update(status="running", started=time.time(), elapsed=0.0)
    st["log"].clear()
    (EV / sid).mkdir(parents=True, exist_ok=True)
    if sid == "backend":
        shutil.rmtree(EV / "backend" / "raw", ignore_errors=True)
    log(sid, "$ " + " ".join(spec["cmd"]))
    env = dict(os.environ, **spec["env"])
    started_iso = datetime.now(timezone.utc).isoformat(timespec="seconds")
    t0 = time.time()
    code = -1
    try:
        proc = subprocess.Popen(spec["cmd"], cwd=spec["cwd"], env=env, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True, bufsize=1)
        console = open(EV / sid / "console.log", "w") if not spec.get("nfr") else None
        for line in proc.stdout:
            log(sid, line)
            if console:
                console.write(line)
            st["elapsed"] = time.time() - t0
        code = proc.wait()
        if console:
            console.close()
    except FileNotFoundError as exc:
        log(sid, f"ERROR: command not found: {exc.filename}. Start the dashboard from a terminal where this tool works.")
    except Exception as exc:  # noqa: BLE001 - surface any failure in the log pane
        log(sid, f"ERROR: {exc}")
    duration = time.time() - t0
    try:
        {"backend": post_backend, "mobile": post_mobile}.get(sid, lambda: None)()
    except Exception as exc:  # noqa: BLE001
        log(sid, f"post-processing warning: {exc}")
    meta_path(sid).write_text(json.dumps(dict(started_at=started_iso, duration_s=round(duration, 2),
                                              exit_code=code, command=" ".join(spec["cmd"]))))
    st.update(status="done" if code == 0 else "failed", elapsed=duration)
    log(sid, f"--- finished in {duration:.1f}s with exit code {code}")


def worker():
    while True:
        with LOCK:
            if not QUEUE:
                CURRENT["id"] = None
                WORKER["thread"] = None
                return
            sid = QUEUE.pop(0)
            CURRENT["id"] = sid
        run_one(sid)


def enqueue(ids):
    with LOCK:
        for sid in ids:
            if sid not in QUEUE and CURRENT["id"] != sid:
                QUEUE.append(sid)
                STATE[sid]["status"] = "queued"
        if WORKER["thread"] is None:
            t = threading.Thread(target=worker, daemon=True)
            WORKER["thread"] = t
            t.start()


# ---------------------------------------------------------------- result parsing
def to_sec(hms):
    h, m, s = hms.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def cut(text, n=1500):
    return (text or "").strip()[:n]


def parse_backend():
    f = EV / "backend" / "raw" / "backend.trx"
    if not f.exists():
        return None
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(f).getroot()
    defs = {d.get("id"): d.find("t:TestMethod", ns).get("className") for d in root.iterfind(".//t:UnitTest", ns)}
    tests = []
    for r in root.iterfind(".//t:UnitTestResult", ns):
        name = r.get("testName")
        cls = (defs.get(r.get("testId")) or "").replace("FreightLink.Api.Tests.", "")
        short = name.replace("FreightLink.Api.Tests.", "")
        if short.startswith(cls + "."):
            short = short[len(cls) + 1:]
        msg = ""
        m = r.find(".//t:ErrorInfo/t:Message", ns)
        if m is not None:
            msg = cut(m.text)
        tests.append([short, cls, r.get("outcome"), to_sec(r.get("duration", "0:0:0")), msg])
    times = root.find("t:Times", ns)
    dur = None
    if times is not None:
        try:
            a = datetime.fromisoformat(times.get("start")[:26])
            b = datetime.fromisoformat(times.get("finish")[:26])
            dur = (b - a).total_seconds()
        except ValueError:
            pass
    return tests, dur, f.stat().st_mtime


def parse_junit(path, strip=""):
    if not path.exists():
        return None
    root = ET.parse(path).getroot()
    tests = []
    for tc in root.iter("testcase"):
        outcome = "Passed"
        msg = ""
        bad = tc.find("failure") if tc.find("failure") is not None else tc.find("error")
        if bad is not None:
            outcome, msg = "Failed", cut(bad.get("message") or bad.text)
        elif tc.find("skipped") is not None:
            outcome = "NotExecuted"
        cls = tc.get("classname", "")
        if strip:
            cls = cls.replace(strip, "")
        tests.append([tc.get("name"), cls, outcome, float(tc.get("time") or 0), msg])
    suites = [root] if root.tag == "testsuite" else list(root.iter("testsuite"))
    times = [float(x.get("time")) for x in suites if x.get("time")]
    dur = (float(root.get("time")) if root.tag == "testsuites" and root.get("time") else sum(times)) or None
    return tests, dur, path.stat().st_mtime


def parse_flutter():
    path = EV / "mobile" / "flutter-tests.json"
    if not path.exists():
        return None
    names, starts, tests, errors, total_ms = {}, {}, [], {}, 0
    for line in path.read_text().splitlines():
        try:
            e = json.loads(line)
        except json.JSONDecodeError:
            continue
        t = e.get("type")
        if t == "testStart" and e["test"].get("url"):
            names[e["test"]["id"]] = (e["test"]["name"], e["test"]["url"].rsplit("/test/", 1)[-1])
            starts[e["test"]["id"]] = e["time"]
        elif t == "error":
            errors[e["testID"]] = cut(e.get("error"))
        elif t == "testDone" and not e.get("hidden") and e["testID"] in names:
            n, u = names[e["testID"]]
            o = "NotExecuted" if e.get("skipped") else ("Passed" if e["result"] == "success" else "Failed")
            tests.append([n, u, o, (e["time"] - starts[e["testID"]]) / 1000.0, errors.get(e["testID"], "")])
        elif t == "done":
            total_ms = e["time"]
    return tests, total_ms / 1000.0 or None, path.stat().st_mtime


def backend_coverage():
    f = EV / "backend" / "coverage.cobertura.xml.gz"
    if not f.exists():
        return None
    import re
    with gzip.open(f, "rt") as fh:
        head = fh.read(600)
    return dict(line=float(re.search(r'line-rate="([0-9.]+)"', head).group(1)) * 100,
                branch=float(re.search(r'branch-rate="([0-9.]+)"', head).group(1)) * 100)


def suite_result(sid):
    spec = SUITES[sid]
    parsed = {"backend": parse_backend, "frontend": lambda: parse_junit(EV / "frontend" / "junit.xml"),
              "mobile": parse_flutter, "agent": lambda: parse_junit(EV / "agent" / "junit.xml")}[sid]()
    out = dict(id=sid, label=spec["label"], has_results=parsed is not None)
    mp = meta_path(sid)
    meta = json.loads(mp.read_text()) if mp.exists() else None
    out["meta"] = meta
    if not parsed:
        return out
    tests, tool_dur, mtime = parsed
    passed = sum(1 for t in tests if t[2] == "Passed")
    failed = sum(1 for t in tests if t[2] == "Failed")
    out.update(total=len(tests), passed=passed, failed=failed, skipped=len(tests) - passed - failed,
               tool_duration_s=tool_dur, sum_test_s=round(sum(t[3] for t in tests), 3),
               finished_at=datetime.fromtimestamp(mtime).isoformat(timespec="seconds"), tests=tests)
    if sid == "backend":
        out["coverage"] = backend_coverage()
    return out


def nfr_result():
    out = {}
    for sid, fname in (("loads", "loads-api-summary.json"), ("agent", "agent-workflow-summary.json")):
        f = EV / "k6" / fname
        if f.exists():
            m = json.loads(f.read_text())["metrics"]
            thr = [[k, e, not bad] for k, v in m.items() for e, bad in (v.get("thresholds") or {}).items()]
            durations = {k: v for k, v in m.items() if k.endswith("duration") and "p(95)" in v}
            out.setdefault("k6", []).append(dict(
                script="loads-api.perf.js" if sid == "loads" else "agent-workflow-latency.perf.js",
                requests=int(m["http_reqs"]["count"]), failed_pct=m["http_req_failed"]["value"] * 100,
                vus_max=int(m["vus_max"]["max"]), iterations=int(m["iterations"]["count"]),
                p95={k: v["p(95)"] for k, v in durations.items() if not k.startswith("iteration")},
                thresholds=thr, finished_at=datetime.fromtimestamp(f.stat().st_mtime).isoformat(timespec="seconds")))
    z = EV / "zap" / "zap-report.json"
    if z.exists():
        d = json.loads(z.read_text())
        alerts = [[a["riskdesc"], a["name"], int(a["count"])] for s in d["site"] for a in s["alerts"]]
        out["zap"] = dict(alerts=alerts, finished_at=datetime.fromtimestamp(z.stat().st_mtime).isoformat(timespec="seconds"),
                          report="/evidence/zap/zap-report.html")
        zc = EV / "zap" / "zap-console.log"
        if zc.exists():
            import re
            m = re.search(r"FAIL-NEW: (\d+).*?WARN-NEW: (\d+).*?PASS: (\d+)", zc.read_text())
            if m:
                out["zap"].update(fail=int(m.group(1)), warn=int(m.group(2)), passed=int(m.group(3)))
    for sid in ("k6", "zap"):
        mp = meta_path(sid)
        if mp.exists():
            out.setdefault("meta", {})[sid] = json.loads(mp.read_text())
    return out


def results():
    defects = []
    dj = ROOT / "testing" / "defects.json"
    if dj.exists():
        defects = [dict(id=d["id"], title=d["title"], severity=d["severity"], status=d["status"], retest=d["retest"])
                   for d in json.loads(dj.read_text())["defects"]]
    return dict(suites=[suite_result(s) for s in TEST_SUITES], nfr=nfr_result(), defects=defects,
                machine=dict(os=sys.platform, python=sys.version.split()[0], host=os.uname().nodename if hasattr(os, "uname") else ""),
                now=datetime.now().isoformat(timespec="seconds"))


def status():
    return dict(current=CURRENT["id"], queue=list(QUEUE), suites={
        sid: dict(status=st["status"], elapsed=(time.time() - st["started"]) if st["status"] == "running" else st["elapsed"],
                  log=list(st["log"])[-250:]) for sid, st in STATE.items()})


# ---------------------------------------------------------------- http
class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):  # keep the terminal quiet
        pass

    def send(self, code, body, ctype="application/json"):
        data = body if isinstance(body, bytes) else body.encode()
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path in ("/", "/index.html"):
            return self.send(200, (HERE / "index.html").read_bytes(), "text/html; charset=utf-8")
        if path == "/api/results":
            return self.send(200, json.dumps(results()))
        if path == "/api/status":
            return self.send(200, json.dumps(status()))
        if path.startswith("/evidence/zap/") and path.count("..") == 0:
            f = EV / "zap" / path.split("/evidence/zap/", 1)[1]
            if f.is_file():
                return self.send(200, f.read_bytes(), "text/html; charset=utf-8" if f.suffix == ".html" else "text/plain")
        self.send(404, json.dumps({"error": "not found"}))

    def do_POST(self):
        origin = self.headers.get("Origin")
        if origin and origin not in (f"http://{self.headers.get('Host')}",):
            return self.send(403, json.dumps({"error": "cross-origin request refused"}))
        if self.path != "/api/run":
            return self.send(404, json.dumps({"error": "not found"}))
        body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0) or b"{}")
        what = body.get("suite")
        ids = TEST_SUITES if what == "all" else [what] if what in SUITES else None
        if ids is None:
            return self.send(400, json.dumps({"error": "unknown suite"}))
        enqueue(ids)
        self.send(202, json.dumps({"queued": ids}))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8765)
    args = ap.parse_args()
    srv = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print(f"FreightLink test dashboard: http://127.0.0.1:{args.port}   (Ctrl+C to stop)")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped")


if __name__ == "__main__":
    main()
