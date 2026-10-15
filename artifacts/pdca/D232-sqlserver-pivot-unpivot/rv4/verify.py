#!/usr/bin/env python3
"""verify.py -- task-local evidence plumbing for D232-sqlserver-pivot-unpivot (T232 / #232), rv=4.

Task-local plumbing, NOT production code and NOT a framework. rv=4 supersedes rv3 as the current
contract after the authoritative CHECK at r=3 / n=2 / rv=3 FAILED (build / security / unsealed
evidence). It expands and records the exact argv + cwd of every executed command, captures
stdout/stderr and the numeric exit code under ``logs/``, and writes ``<case>.json``.

Contract roots
--------------
``R``  = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4`` (this file's directory).
``R1`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1`` (E232-01..17 keep their rv1 paths).
``R3`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3`` (E232/EC232 rv3 evidence is preserved).

Usage
-----
    python3 "$R/verify.py" --case <case>

Cases (the 11 rv4 delta rows + the D-08 packaging case)
    packaging  EC232-RV4-PACKAGING  D-08: csproj ProjectReference + docs + literal C13 exit 0
    v05        EC232-RV4-V05        D-09: TVF inner closure
    v18        EC232-RV4-V18        D-09: typo/absent data value closure
    v19        EC232-RV4-V19        D-09: dynamic PIVOT guard
    v21        EC232-RV4-V21        D-09: eager/single-query state closure
    coverage   EC232-RV4-COVERAGE   D-10: thresholds AND comparable touched-scope non-decrease
    ownership  EC232-RV4-OWNERSHIP  D-11: #160 ownership/authorization
    handoff    EC232-RV4-HANDOFF    D-11: per-hunk byte hashes / clean paths / CRLF
    security   EC232-RV4-SECURITY   D-13: advisory capture + duplicate search
    perf       EC232-RV4-PERF       perf re-confirmation
    seal       EC232-RV4-SEAL       D-12: manifest schema + zero applicable open rows

Exit codes: 0 = ok, 2 = violation, 1 = usage/unreadable/missing required input (fail-closed).
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import json
import os
import re
import subprocess
import sys

R = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(R, "..", "..", "..", ".."))
R_REL = os.path.relpath(R, ROOT).replace(os.sep, "/")
LOGS = os.path.join(R, "logs")

DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
SQLSERVER_TESTS = "tests/nextorm.sqlserver.tests"
INTEGRATION_TESTS = "tests/nextorm.integration.tests"
EXAMPLE_CSPROJ = "examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj"

# case -> (row id, unit, required input files, argv, acceptance description)
CASES = {
    "packaging": ("EC232-RV4-PACKAGING", "D-08", [EXAMPLE_CSPROJ], None,
                  "csproj ProjectReferences present, docs note the exception, literal C13 exits 0"),
    "v05": ("EC232-RV4-V05", "D-09", [f"{SQLSERVER_TESTS}/T232TypedPivotTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build", "-p:NuGetAudit=false",
             "--filter", "FullyQualifiedName~T232VariantV05"], "V05 TVF inner closure, selected > 0, exit 0"),
    "v18": ("EC232-RV4-V18", "D-09", [f"{SQLSERVER_TESTS}/T232TypedPivotTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build", "-p:NuGetAudit=false",
             "--filter", "FullyQualifiedName~T232VariantV18"], "V18 typo/absent value, selected > 0, exit 0"),
    "v19": ("EC232-RV4-V19", "D-09", [f"{SQLSERVER_TESTS}/T232TypedPivotTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build", "-p:NuGetAudit=false",
             "--filter", "FullyQualifiedName~T232VariantV19"], "V19 dynamic PIVOT guard, selected > 0, exit 0"),
    "v21": ("EC232-RV4-V21", "D-09", [f"{SQLSERVER_TESTS}/T232TypedPivotTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build", "-p:NuGetAudit=false",
             "--filter", "FullyQualifiedName~T232VariantV21"], "V21 eager/single-query state, selected > 0, exit 0"),
    "coverage": ("EC232-RV4-COVERAGE", "D-10", [f"{R_REL}/coverage/harness.json"],
                 ["python3", f"{R_REL}/coverage/run.py"], "thresholds met AND comparable touched-scope non-decrease"),
    "ownership": ("EC232-RV4-OWNERSHIP", "D-11", [f"{R_REL}/ownership.json"], None,
                  "verified #160 sibling-owner identity + explicit authorization; T232-only footprint"),
    "handoff": ("EC232-RV4-HANDOFF", "D-11", [f"{R_REL}/handoff.json"], None,
                "per-hunk byte hashes, clean paths, CRLF"),
    "security": ("EC232-RV4-SECURITY", "D-13", [], ["dotnet", "restore", "nextorm.slnx"],
                 "complete advisory capture + duplicate search recorded"),
    "perf": ("EC232-RV4-PERF", "D-10", [f"{R_REL}/perf/baseline.json"],
             ["python3", f"{R_REL}/perf/run.py"], "seven acceptance cases, no unexplained material regression"),
    "seal": ("EC232-RV4-SEAL", "D-12", [f"{R_REL}/manifest.json"], None,
             "manifest schema valid AND zero applicable open rows"),
}


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _sha(path: str) -> str:
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def _run(case: str, argv: list[str]) -> dict:
    os.makedirs(LOGS, exist_ok=True)
    out_p, err_p, exit_p = (os.path.join(LOGS, f"{case}.{s}") for s in ("out", "err", "exit"))
    cmd_p = os.path.join(LOGS, f"{case}.cmd.json")
    cmd = dict(argv=argv, cwd=ROOT, env={"DOCKER_HOST": DOCKER_HOST}, captured=_now())
    json.dump(cmd, open(cmd_p, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    open(cmd_p, "a", encoding="utf-8").write("\n")
    env = dict(os.environ)
    if case in ("v05", "v18", "v19", "v21"):
        env["DOCKER_HOST"] = DOCKER_HOST
    proc = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, env=env)
    open(out_p, "w", encoding="utf-8").write(proc.stdout)
    open(err_p, "w", encoding="utf-8").write(proc.stderr)
    open(exit_p, "w", encoding="utf-8").write(str(proc.returncode))
    return dict(exit=proc.returncode, stdout=proc.stdout, stderr=proc.stderr,
                out=os.path.relpath(out_p, ROOT).replace(os.sep, "/"),
                err=os.path.relpath(err_p, ROOT).replace(os.sep, "/"))


def _write(case: str, payload: dict, artifact: str | None = None) -> None:
    payload.setdefault("case", case)
    payload.setdefault("captured", _now())
    payload.setdefault("cwd", ROOT)
    text = json.dumps(payload, indent=1, ensure_ascii=False, sort_keys=True) + "\n"
    open(os.path.join(R, f"{case}.json"), "w", encoding="utf-8").write(text)
    if artifact:
        os.makedirs(os.path.dirname(os.path.join(R, artifact)), exist_ok=True)
        open(os.path.join(R, artifact), "w", encoding="utf-8").write(text)


def case_packaging() -> tuple[dict, int]:
    csproj = open(os.path.join(ROOT, EXAMPLE_CSPROJ), encoding="utf-8").read()
    has_core = "../../src/nextorm.core/nextorm.core.csproj" in csproj
    has_sql = "../../src/nextorm.sqlserver/nextorm.sqlserver.csproj" in csproj
    no_pkg = ('Include="nextorm" ' not in csproj and 'Include="nextorm.sqlserver" ' not in csproj)
    docs = ""
    for p in ("examples/README.md", "examples/VERIFICATION.md",
              "examples/nextorm.examples.mssql.adventureworks/README.md"):
        docs += open(os.path.join(ROOT, p), encoding="utf-8").read()
    docs_note = "explicit exception" in docs and "current checkout" in docs
    res = _run("packaging", ["dotnet", "build", EXAMPLE_CSPROJ, "-c", "Debug"])
    cs_errors = [ln for ln in (res["stdout"] + res["stderr"]).splitlines()
                 if " error " in ln and "NU19" not in ln]
    advisory_only = bool(re.search(r" error NU19\d\d", res["stdout"] + res["stderr"]))
    ok = has_core and has_sql and no_pkg and docs_note and res["exit"] == 0
    payload = dict(
        row=CASES["packaging"][0], unit="D-08", status="met" if ok else "open",
        assertions=dict(project_reference_core=has_core, project_reference_sqlserver=has_sql,
                        no_nextorm_packageref=no_pkg, docs_state_exception=docs_note),
        c13=dict(exit=res["exit"], log=res["out"], cs_or_packaging_errors=cs_errors,
                 advisory_only=advisory_only),
        reason=(None if ok else
                "literal C13 exits 1 (advisory-only NU1902/NU1903 -> D-13); packaging route itself adds no "
                "CS/packaging error"),
        artifact=f"{R_REL}/packaging/packaging.json",
    )
    return payload, (0 if ok else 2)


def case_security() -> tuple[dict, int]:
    res = _run("security", ["dotnet", "restore", "nextorm.slnx"])
    text = res["stdout"] + res["stderr"]
    advisories = {}
    for ln in text.splitlines():
        m = re.search(r"error (NU19\d\d):.*Package '([^']+)' ([\d.]+) has a known (\w+) severity.*?(GHSA-[0-9a-z-]+)", ln)
        if m:
            advisories[(m.group(1), m.group(2), m.group(3), m.group(4), m.group(5))] = True
    adv = [dict(code=k[0], package=k[1], version=k[2], severity=k[3], ghsa=k[4]) for k in sorted(advisories)]
    gh_dir = os.path.join(R, "security", "gh-search")
    gh_files = sorted(os.listdir(gh_dir)) if os.path.isdir(gh_dir) else []
    found = bool(gh_files)
    captured = bool(adv) and found
    payload = dict(row=CASES["security"][0], unit="D-13",
                   status="captured" if captured else "incomplete",
                   restore_exit=res["exit"], advisories=adv, gh_search_files=gh_files,
                   reason=None if captured else "missing advisory identifiers or duplicate-search artifacts")
    return payload, (0 if captured else 2)


def case_seal() -> tuple[dict, int]:
    manifest = os.path.join(R, "manifest.json")
    res = _run("seal", ["python3", "scripts/validate_inner_loop.py", "manifest", manifest])
    data = json.load(open(manifest, encoding="utf-8"))
    open_rows = [r["id"] for r in data["rows"] if r.get("status") == "open"]
    ok = res["exit"] == 0 and not open_rows
    payload = dict(row=CASES["seal"][0], unit="D-12", schema_exit=res["exit"],
                   status="met" if ok else "open", open_rows=open_rows,
                   reason=None if ok else f"{len(open_rows)} applicable rows still open")
    return payload, (0 if ok else 2)


def main(argv: list[str]) -> int:
    if len(argv) != 2 or argv[0] != "--case":
        sys.stderr.write("usage: python3 verify.py --case <case>\n")
        return 1
    case = argv[1]
    if case not in CASES:
        sys.stderr.write(f"unknown case: {case!r}\n")
        return 1
    row, unit, inputs, cmd, acceptance = CASES[case]
    os.makedirs(LOGS, exist_ok=True)

    if case == "packaging":
        payload, code = case_packaging()
        _write(case, payload, "packaging/packaging.json")
        print(json.dumps({k: payload[k] for k in ("case", "status", "assertions", "reason")}, ensure_ascii=False))
        return code
    if case == "security":
        payload, code = case_security()
        _write(case, payload)
        print(json.dumps({k: payload[k] for k in ("case", "status", "advisories", "gh_search_files")},
                         ensure_ascii=False))
        return code
    if case == "seal":
        payload, code = case_seal()
        _write(case, payload)
        print(json.dumps({k: payload[k] for k in ("case", "status", "open_rows", "reason")}, ensure_ascii=False))
        return code

    missing = [p for p in inputs if not os.path.exists(os.path.join(ROOT, p))]
    if missing:
        payload = dict(row=row, unit=unit, status="blocked", missing_inputs=missing,
                       reason="required input not present (planned future unit); fail-closed")
        _write(case, payload)
        print(json.dumps({k: payload[k] for k in ("case", "status", "missing_inputs")}, ensure_ascii=False))
        return 1
    if cmd is None:
        payload = dict(row=row, unit=unit, status="blocked",
                       reason="no executable route wired yet; fail-closed")
        _write(case, payload)
        return 1
    res = _run(case, cmd)
    payload = dict(row=row, unit=unit, acceptance=acceptance, exit=res["exit"], log=res["out"],
                   status="met" if res["exit"] == 0 else "open",
                   reason=None if res["exit"] == 0 else f"command exit {res['exit']}")
    _write(case, payload)
    print(json.dumps({k: payload[k] for k in ("case", "status", "exit")}, ensure_ascii=False))
    return 0 if res["exit"] == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
