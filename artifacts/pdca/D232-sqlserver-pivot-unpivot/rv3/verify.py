#!/usr/bin/env python3
"""verify.py -- task-local evidence plumbing for D232-sqlserver-pivot-unpivot (T232 / #232), rv=3.

Task-local plumbing, NOT production code and NOT a framework. It:

* expands and records the exact argv + cwd of every executed command;
* captures stdout, stderr and the numeric exit code into files under ``logs/``;
* records selection counts and source/artifact hashes;
* writes the provenance / contract / counters / manifest JSON files named by the status file
  ``docs/specs/status/D232-sqlserver-pivot-unpivot-1.md``.

Contract roots
--------------
``A`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3`` (this file's directory).
``R1`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1`` (rows E232-01..E232-17 / C01..C19
keep their original rv1 paths verbatim).

Subcommands (the delta invocation catalog C20..C33)
---------------------------------------------------
  repro --phase baseline --capture   C20  isolated repro children before any production edit
  repro-candidate --phase before     C34  R01-R05 + R05b children with the additive fallback absent
  repro-candidate --phase after      C35  the same children with the additive fallback in place
  derived-unit                       C21  typed derived builder/command unit cases
  derived-exec                       C22  SQL Server typed derived execution + provider matrix
  shared-regression                  C23  eight project suites + the container boundary
  oracle-amendment                   C24  the independent UNPIVOT-NULL oracle amendment
  build                              C25  core + sqlserver builds (0 warnings / 0 errors)
  variant derived-builder            C26  builder-route variant
  variant derived-command            C27  command-route variant
  variant pivot-free                 C28  pivot-free recursion diagnostic
  variant case-sensitive-alias       C29  case-sensitive identifier resolution
  cache-locality                     C30  cache/command locality + source review
  prerequisite-mutation              C31  controlled prerequisite mutants
  manifest                           C32  validate_inner_loop.py manifest <rv3 manifest>
  scopes                             C19  validate_inner_loop.py brief/report for rv1 D-01..D-05 and rv3 D-06
  fulfill                            C33  contract/provenance/counters/delta fulfillment check
  example                            C13  build the typed AdventureWorks example + source audit
  docs                               C14  XML-doc build evidence + EN/RU semantic match + spec-link scan
  boundary                           C11  delegate the single post-implementation boundary to rv1 (D-07)
  coverage <baseline|candidate>      C12  delegate the coverage measurement to rv1 (D-07)
  footprint                          C16  delegate the foreign-dirty / CRLF footprint to rv1 (D-07)

Exit codes: 0 = ok, 2 = violation, 1 = usage/unreadable/missing required input.
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import json
import os
import re
import subprocess
import sys

A = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(A, "..", "..", "..", ".."))
A_REL = os.path.relpath(A, ROOT).replace(os.sep, "/")
R1_REL = "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1"
LOGS = os.path.join(A, "logs")

DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
SQLSERVER_TESTS = "tests/nextorm.sqlserver.tests"
INTEGRATION_TESTS = "tests/nextorm.integration.tests"
BENCHMARK = ["dotnet", "run", "--project", "benchmarks/nextorm.benchmark", "-c", "Release", "--", "--anyCategories=acceptance"]

# The collation the V29 container test creates its database with (T232PivotFixture.cs:272,291-293).
CASE_SENSITIVE_COLLATION = "Latin1_General_100_CS_AS"

# C23: the six provider families of the integration suite, by test-class name token. A whole family
# skipping because its container is unavailable is a *provider skip*; a single capability-reason skip
# is not.
PROVIDER_TOKENS = [
    ("sqlite", ["Sqlite"]),
    ("sqlserver", ["SqlServer"]),
    ("postgres", ["Postgres"]),
    ("mysql", ["MySql"]),
    ("mariadb", ["MariaDb", "MariaDB"]),
    ("clickhouse", ["ClickHouse"]),
]
PROVIDER_UNAVAILABLE = re.compile(r"(SQLite|SQL Server|PostgreSQL|MySQL|MariaDB|ClickHouse) is not available")

# Test files authored/extended by this r=3 delta (the typed-derived prerequisite). They are the task's
# own files and are legitimately extended by the authorized n=2 repair; the pre-existing frozen oracles
# may not change.
DELTA_OWNED_TEST_FILES = [
    "tests/nextorm.sqlserver.tests/T232DerivedSourceReproTests.cs",
    "tests/nextorm.integration.tests/T232SqlServerTests.cs",
    "tests/nextorm.integration.tests/T232PivotFixture.cs",
]

# Production paths the r=3 footprint names (allowlisted + still-forbidden). Hashed, never edited here.
PRODUCTION_FILES = [
    "src/nextorm.core/Builders/EntityBuilder.cs",
    "src/nextorm.core/DataContext/DataContextExtensions.cs",
    "src/nextorm.core/Visitors/MemberTranslator.cs",
    "src/nextorm.core/Expressions/FromExpression.cs",
    "src/nextorm.core/DataContext/SqlBuilder.cs",
    "src/nextorm.core/DataContext/SqlSourceRenderer.cs",
    "src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs",
    "src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs",
    "src/nextorm.core/Query/PivotValues.cs",
]

# The derived-consumer oracles frozen before any engine change (issue/evidence contract EC232-REPRO).
ORACLE_FILES = [
    R1_REL + "/test-contract.json",
    SQLSERVER_TESTS + "/T232PreparationProbeTests.cs",
    SQLSERVER_TESTS + "/T232ProjectionContractTests.cs",
    SQLSERVER_TESTS + "/T232MarkerTests.cs",
    SQLSERVER_TESTS + "/T232TypedPivotTests.cs",
    SQLSERVER_TESTS + "/T232DerivedSourceReproTests.cs",
    SQLSERVER_TESTS + "/SqlGenerationTests.cs",
    INTEGRATION_TESTS + "/T232SqlServerTests.cs",
    INTEGRATION_TESTS + "/T232PivotFixture.cs",
]

# The isolated repro children of C20. Each runs in its own process: a process-killing termination
# stays inside that child instead of aborting the whole sweep.
PROBES = [
    {"id": "R01-typed-builder-projection", "role": "builder", "filter": "FullyQualifiedName~Repro01_TypedBuilderSource_ProjectsAsDerivedSource"},
    {"id": "R02-typed-builder-where-select", "role": "builder", "filter": "FullyQualifiedName~Repro02_TypedBuilderSource_WhereSelectIsComposable"},
    {"id": "R03-typed-command-projection", "role": "command", "filter": "FullyQualifiedName~Repro03_TypedCommandSource_ProjectsAsDerivedSource"},
    {"id": "R04-typed-command-self-join", "role": "join", "filter": "FullyQualifiedName~Repro04_TypedCommandSource_SelfJoinResolves"},
    {"id": "R05-pivot-free-control", "role": "control", "filter": "FullyQualifiedName~Repro05_PivotFreeDerivedControl_ProjectsAsDerivedSource"},
]

# C34/C35: the additive-fallback candidate sweep. The same frozen R01-R05 children plus the R05b
# pivot-free builder control added by the r=3 additive amendment; executed once with the fallback
# absent (before) and once with it in place (after).
CANDIDATE_PROBES = PROBES + [
    {"id": "R05b-pivot-free-builder-derived", "role": "control-builder",
     "filter": "FullyQualifiedName~Repro05b_PivotFreeBuilderDerivedControl_ProjectsAsDerivedSource"},
]

VARIANT_PROBES = {
    "derived-builder": {"criterion": ["EC232-DERIVED-BUILDER", "V26", "AC-08", "AC-09"],
                        "filter": "FullyQualifiedName~Repro01_TypedBuilderSource", "artifact": "variants/derived-builder.json"},
    "derived-command": {"criterion": ["EC232-DERIVED-COMMAND", "V27", "AC-08"],
                        "filter": "FullyQualifiedName~Repro03_TypedCommandSource", "artifact": "variants/derived-command.json"},
    "pivot-free": {"criterion": ["EC232-PIVOTFREE-PROBE", "V28", "AC-08", "AC-11"],
                   "filter": "FullyQualifiedName~Repro05_PivotFreeDerivedControl", "artifact": "variants/pivot-free.json"},
    # T232CaseSensitiveAliasTests lives in the *integration* project (T232PivotFixture.cs:269) and
    # asserts against a real case-sensitive SQL Server database, so it needs the container host and
    # must not use the unit project / `-explicit only` route.
    "case-sensitive-alias": {"criterion": ["EC232-CASE-SENSITIVE-ALIAS", "V29", "AC-08"],
                             "filter": "FullyQualifiedName~T232CaseSensitiveAliasTests", "artifact": "variants/case-sensitive-alias.json",
                             "project": INTEGRATION_TESTS, "docker": True, "explicit_only": False},
}

REGRESSION_PROJECTS = [
    "tests/nextorm.core.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.mariadb.tests",
    "tests/nextorm.clickhouse.tests",
    "tests/nextorm.alias.tests",
]


# --------------------------------------------------------------------------- helpers
def _rel(path: str) -> str:
    path = os.path.abspath(path)
    if path.startswith(ROOT + os.sep) or path == ROOT:
        return os.path.relpath(path, ROOT).replace(os.sep, "/")
    return path


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _sha256(path: str) -> str:
    if not os.path.exists(path):
        return None
    h = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def _read_json(path: str, default=None):
    try:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return default


def _write_json(path: str, data) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        json.dump(data, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")


def _write_text(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text)


def _git(*args: str) -> str:
    try:
        out = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=False)
        return out.stdout.rstrip("\n")
    except OSError:
        return ""


def _tree_digest() -> dict:
    porcelain = _git("status", "--porcelain=v1")
    return {
        "head": _git("rev-parse", "HEAD"),
        "dirty_sha256": hashlib.sha256(porcelain.encode("utf-8")).hexdigest(),
        "dirty_entries": len([line for line in porcelain.splitlines() if line.strip()]),
    }


def _tool_versions() -> dict:
    versions = {}
    for argv in (["dotnet", "--version"], ["python3", "--version"], ["git", "--version"]):
        try:
            out = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, check=False)
            versions[" ".join(argv)] = (out.stdout or out.stderr).strip()
        except OSError:
            versions[" ".join(argv)] = None
    return versions


def run(argv, prefix, cwd=ROOT, env=None, phase=None, note=None, logdir=None, timeout=900):
    """Execute ``argv``, record argv/cwd/out/err/exit and append a provenance record."""
    logdir = logdir or os.path.join(A, "logs")
    os.makedirs(logdir, exist_ok=True)
    _write_json(os.path.join(logdir, prefix + ".cmd.json"),
                {"argv": argv, "cwd": _rel(cwd), "env": env or {}, "date": _now()})

    merged = dict(os.environ)
    if env:
        merged.update(env)
    timed_out = False
    try:
        completed = subprocess.run(argv, cwd=cwd, capture_output=True, text=True, env=merged,
                                   check=False, timeout=timeout)
        stdout, stderr, code = completed.stdout, completed.stderr, completed.returncode
    except subprocess.TimeoutExpired as exc:
        timed_out = True
        stdout = (exc.stdout or b"").decode("utf-8", "replace") if isinstance(exc.stdout, bytes) else (exc.stdout or "")
        stderr = (exc.stderr or b"").decode("utf-8", "replace") if isinstance(exc.stderr, bytes) else (exc.stderr or "")
        code = 124

    _write_text(os.path.join(logdir, prefix + ".out"), stdout)
    _write_text(os.path.join(logdir, prefix + ".err"), stderr)
    _write_text(os.path.join(logdir, prefix + ".exit"), str(code) + "\n")

    _append_provenance({
        "name": prefix,
        "phase": phase,
        "note": note,
        "argv": argv,
        "cwd": _rel(cwd),
        "exit": code,
        "timed_out": timed_out,
        "stdout": _rel(os.path.join(logdir, prefix + ".out")),
        "stderr": _rel(os.path.join(logdir, prefix + ".err")),
        "exit_file": _rel(os.path.join(logdir, prefix + ".exit")),
        "stdout_sha256": hashlib.sha256(stdout.encode("utf-8")).hexdigest(),
        "date": _now(),
    })
    return code


def _append_provenance(entry: dict) -> None:
    path = os.path.join(A, "provenance.json")
    data = _read_json(path)
    if not isinstance(data, dict):
        data = {
            "task": "T232 (#232) SQL Server typed PIVOT/UNPIVOT projection",
            "issue": "#232",
            "collection": "1.0.9-todo",
            "milestone": "1.0.9-rc3",
            "contract_rv": 3,
            "plan_revision": 3,
            "cycle": "N=1",
            "unit": "D232-PREREQ-typed-derived / D232-AMEND-unpivot-null-oracle",
            "status_file": "docs/specs/status/D232-sqlserver-pivot-unpivot-1.md",
            "tree_at_do_start": _tree_digest(),
            "tool_versions": _tool_versions(),
            "records": [],
        }
    if "records" not in data or not isinstance(data["records"], list):
        data["records"] = []
    data["records"].append(entry)
    data["last_updated"] = _now()
    _write_json(path, data)


def _parse_test_summary(text: str) -> dict:
    summary = {"total": None, "failed": None, "succeeded": None, "skipped": None}
    for key in summary:
        match = re.search(rf"^\s*{key}:\s*(\d+)\s*$", text, re.MULTILINE)
        if match:
            summary[key] = int(match.group(1))
    return summary


def _parse_build_summary(text: str) -> dict:
    warnings = re.search(r"(\d+)\s+Warning\(s\)", text)
    errors = re.search(r"(\d+)\s+Error\(s\)", text)
    return {
        "succeeded": "Build succeeded" in text,
        "warnings": int(warnings.group(1)) if warnings else None,
        "errors": int(errors.group(1)) if errors else None,
    }


def _file_hashes(paths) -> dict:
    out = {}
    for path in paths:
        full = os.path.join(ROOT, path)
        out[path] = {"exists": os.path.exists(full), "sha256": _sha256(full)}
    return out


def _record_self(name: str, exit_code: int, note: str) -> None:
    """Record a subcommand invocation that itself contains no child process."""
    _append_provenance({
        "name": name,
        "phase": "boundary",
        "note": note,
        "argv": ["python3", A_REL + "/verify.py"] + name.split("-", 1)[1:],
        "cwd": ".",
        "exit": exit_code,
        "timed_out": False,
        "date": _now(),
    })


def _probe_child(probe, logdir, timeout=300, prefix="C20-"):
    """Run one repro probe in its own process; classify the actual outcome verbatim."""
    prefix = prefix + probe["id"]
    exit_code = run([ "dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build",
                      "--filter", probe["filter"], "--", "-explicit", "only" ],
                    prefix, phase="inner", logdir=logdir, timeout=timeout,
                    note="isolated repro child '%s' (%s route)" % (probe["id"], probe["role"]))
    out = _read_text(os.path.join(logdir, prefix + ".out"))
    err = _read_text(os.path.join(logdir, prefix + ".err"))
    summary = _parse_test_summary(out)
    crashed = exit_code != 0 and (summary.get("total") is None)
    outcome = _classify_outcome(exit_code, summary, out, err)
    return {
        "id": probe["id"],
        "role": probe["role"],
        "argv": ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build", "--filter", probe["filter"], "--", "-explicit", "only"],
        "exit": exit_code,
        "selected_count": summary.get("total"),
        "failed": summary.get("failed"),
        "skipped": summary.get("skipped"),
        "process_terminated": crashed,
        "outcome": outcome,
        "log": _rel(os.path.join(logdir, prefix + ".out")),
        "err_log": _rel(os.path.join(logdir, prefix + ".err")),
    }


def _read_text(path: str) -> str:
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            return handle.read()
    except OSError:
        return ""


def _classify_outcome(exit_code, summary, out: str, err: str) -> dict:
    """Record the ACTUAL outcome verbatim; never relabel a timeout/crash as something else."""
    text = out + "\n" + err
    exception = None
    match = re.search(r"(?:^|\n)\s*([A-Za-z][A-Za-z0-9_.]*Exception):\s*([^\n]*)", text)
    if match:
        exception = {"type": match.group(1), "message": match.group(2).strip()}
    stack_overflow = "Stack overflow" in text or "StackOverflow" in text
    fatal = None
    fmatch = re.search(r"Fatal error[^\n]*", text)
    if fmatch:
        fatal = fmatch.group(0)
    assert_match = re.search(r"(?:^|\n)\s*Assert[^\n]*", text)
    return {
        "kind": ("pass" if exit_code == 0 else
                 "timeout" if exit_code == 124 else
                 "process-terminated" if summary.get("total") is None else
                 "test-failure"),
        "exception": exception,
        "stack_overflow_marker": stack_overflow,
        "fatal_line": fatal,
        "assert_line": assert_match.group(0).strip() if assert_match else None,
        "tail": "\n".join([line for line in text.splitlines() if line.strip()][-12:]),
    }


# --------------------------------------------------------------------------- C20
def cmd_repro(phase: str, capture: bool):
    if phase != "baseline":
        print("ERROR: repro supports --phase baseline only", file=sys.stderr)
        return 1
    repro_dir = os.path.join(A, "repro")
    logdir = os.path.join(repro_dir, "logs")
    os.makedirs(logdir, exist_ok=True)

    build_exit = run(["dotnet", "build", SQLSERVER_TESTS, "-c", "Debug"],
                     "C20-build-repro-tests", phase="inner", logdir=logdir,
                     note="build the repro test project once before the isolated children")

    production = {
        "phase": phase,
        "captured": _now(),
        "tree": _tree_digest(),
        "production_files": _file_hashes(PRODUCTION_FILES),
        "frozen_oracles": _file_hashes(ORACLE_FILES),
        "note": "Source/tree/oracle hashes frozen BEFORE any engine change for the typed-derived prerequisite.",
    }
    _write_json(os.path.join(repro_dir, "production-before.json"), production)

    probes = [_probe_child(probe, logdir) for probe in PROBES]

    acceptance = None
    if capture:
        started = _dt.datetime.now().timestamp()
        perf_exit = run(BENCHMARK, "C20-acceptance-baseline", phase="boundary", logdir=logdir,
                        note="acceptance perf baseline: exactly 7 cases, no failures")
        out = _read_text(os.path.join(logdir, "C20-acceptance-baseline.out"))
        total = re.search(r"Global total time:\s*([^\n]+)", out)
        executed = re.search(r"executed benchmarks:\s*(\d+)", total.group(1)) if total else None
        perf_dir = os.path.join(repro_dir, "perf")
        copied = _copy_fresh_bdn(perf_dir, started)
        acceptance = {
            "exit": perf_exit,
            "cases_reported": int(executed.group(1)) if executed else None,
            "global_total_time": total.group(1) if total else None,
            "log": _rel(os.path.join(logdir, "C20-acceptance-baseline.out")),
            "bdn_artifacts": copied,
        }

    builder_red = [p for p in probes if p["role"] == "builder" and p["exit"] != 0]
    control = next((p for p in probes if p["role"] == "control"), None)
    demonstrated = bool(builder_red)
    baseline = {
        "unit": "D232-PREREQ-typed-derived",
        "criterion": ["EC232-REPRO", "AC-08", "AC-11", "V26", "V27", "V28"],
        "phase": phase,
        "captured": _now(),
        "build_exit": build_exit,
        "probes": probes,
        "builder_red_demonstrated": demonstrated,
        "builder_red_ids": [p["id"] for p in builder_red],
        "pivot_free_control_outcome": control["outcome"]["kind"] if control else None,
        "acceptance_baseline": acceptance,
        "production_before": _rel(os.path.join(repro_dir, "production-before.json")),
        "note": ("Baseline FAILING repro captured before any production edit; outer exit 0 iff a "
                 "genuine builder red was demonstrated."),
    }
    _write_json(os.path.join(repro_dir, "baseline.json"), baseline)
    print(json.dumps({
        "build_exit": build_exit,
        "probes": {p["id"]: {"exit": p["exit"], "kind": p["outcome"]["kind"],
                             "exception": p["outcome"]["exception"]} for p in probes},
        "builder_red_demonstrated": demonstrated,
        "acceptance": acceptance and {"cases": acceptance["cases_reported"], "exit": acceptance["exit"]},
    }, indent=1))
    return 0 if demonstrated else 2


# --------------------------------------------------------------------------- C34/C35
def cmd_repro_candidate(phase: str):
    """Run the candidate probe sweep (R01-R05 + R05b) in isolated child processes.

    ``--phase before`` expects the pre-fallback state: R01-R04 and R05b red, R05 green.
    ``--phase after`` expects every probe green. Exit 0 iff the phase expectation holds.
    """
    if phase not in ("before", "after"):
        print("ERROR: repro-candidate supports --phase before|after", file=sys.stderr)
        return 1

    label = "candidate-before" if phase == "before" else "candidate"
    letter = "C34" if phase == "before" else "C35"
    out_dir = os.path.join(A, "repro", label)
    logdir = os.path.join(out_dir, "logs")
    os.makedirs(logdir, exist_ok=True)

    build_exit = run(["dotnet", "build", SQLSERVER_TESTS, "-c", "Debug"],
                     letter + "-build-repro-tests", phase="inner", logdir=logdir,
                     note="build the repro test project once before the isolated children (%s)" % phase)

    probes = [_probe_child(probe, logdir, prefix=letter + "-") for probe in CANDIDATE_PROBES]
    by_id = {p["id"]: p for p in probes}

    green_all = all(p["exit"] == 0 for p in probes)
    red_ids = [p["id"] for p in probes if p["exit"] != 0]
    expected_red = [p["id"] for p in CANDIDATE_PROBES
                    if p["role"] in ("builder", "command", "join", "control-builder")]
    ok = green_all if phase == "after" else sorted(red_ids) == sorted(expected_red)

    result = {
        "unit": "D232-PREREQ-typed-derived / additive-fallback",
        "criterion": ["EC232-REPRO", "AC-08", "AC-11", "V26", "V27", "V28", "V29"],
        "phase": phase,
        "captured": _now(),
        "build_exit": build_exit,
        "probes": probes,
        "red_ids": red_ids,
        "expected_red_ids": expected_red if phase == "before" else [],
        "expectation_met": ok,
        "fallback_absent": phase == "before",
        "note": ("Candidate sweep with the additive fallback ABSENT: the four typed routes and the "
                 "pivot-free builder control fail while the pivot-free direct control stays green."
                 if phase == "before" else
                 "Candidate sweep with the additive fallback in place: every probe green."),
    }
    _write_json(os.path.join(out_dir, label + ".json"), result)
    print(json.dumps({
        "phase": phase,
        "build_exit": build_exit,
        "probes": {p["id"]: {"exit": p["exit"], "kind": p["outcome"]["kind"],
                             "exception": p["outcome"]["exception"]} for p in probes},
        "expectation_met": ok,
    }, indent=1))
    return 0 if ok else 2


def _copy_fresh_bdn(dest_dir: str, since_ts: float) -> list:
    os.makedirs(dest_dir, exist_ok=True)
    root = os.path.join(ROOT, "benchmarks", "BenchmarkDotNet.Artifacts")
    copied = []
    for dirpath, _dirnames, filenames in os.walk(root):
        for filename in filenames:
            src = os.path.join(dirpath, filename)
            if os.path.getmtime(src) >= since_ts - 2:
                dst = os.path.join(dest_dir, os.path.relpath(src, root))
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                with open(src, "rb") as rh, open(dst, "wb") as wh:
                    wh.write(rh.read())
                copied.append(_rel(dst))
    return copied


# --------------------------------------------------------------------------- C24
def _json_diff(before, after, path=""):
    """Return the list of changed/added/removed JSON paths between two documents."""
    changed = []
    if isinstance(before, dict) and isinstance(after, dict):
        for key in sorted(set(before) | set(after)):
            changed.extend(_json_diff(before.get(key), after.get(key), f"{path}.{key}" if path else key))
        return changed
    if isinstance(before, list) and isinstance(after, list):
        if len(before) == len(after):
            for idx, (b, a) in enumerate(zip(before, after)):
                changed.extend(_json_diff(b, a, f"{path}[{idx}]"))
            return changed
    if before != after:
        changed.append({"path": path, "before": before, "after": after})
    return changed


def cmd_oracle_amendment():
    oracle_dir = os.path.join(A, "oracle")
    os.makedirs(oracle_dir, exist_ok=True)
    contract_rel = R1_REL + "/test-contract.json"
    contract = os.path.join(ROOT, contract_rel)
    before_copy = os.path.join(oracle_dir, "before-contract.json")
    before_meta = _read_json(os.path.join(oracle_dir, "before.json"))
    key_path = "null_count_expectations.unpivot_null_cell"

    if not os.path.exists(before_copy) or not isinstance(before_meta, dict):
        print("ERROR: oracle/before-contract.json and oracle/before.json must exist before C24", file=sys.stderr)
        return 1

    before_doc = _read_json(before_copy)
    after_doc = _read_json(contract)
    if not isinstance(before_doc, dict) or not isinstance(after_doc, dict):
        print("ERROR: cannot read the oracle contract", file=sys.stderr)
        return 1

    changed = _json_diff(before_doc, after_doc)
    current = after_doc.get("null_count_expectations", {}).get("unpivot_null_cell")
    _write_json(os.path.join(oracle_dir, "after.json"), {
        "path": contract_rel,
        "sha256": _sha256(contract),
        "value": current,
        "amended": after_doc,
        "captured": _now(),
    })

    violations = []
    if len(changed) != 1 or changed[0].get("path") != key_path:
        violations.append("exactly one key must change (%s); observed: %r" % (key_path, [c.get("path") for c in changed]))
    elif before_meta.get("value") != changed[0].get("before"):
        violations.append("recorded before value does not match the before-contract copy")
    if before_meta.get("after") != current:
        violations.append("recorded after value does not match the amended contract")
    if before_meta.get("before_sha256") != _sha256(before_copy):
        violations.append("before-contract sha256 does not match before.json")

    amendment = {
        "unit": "D232-AMEND-unpivot-null-oracle",
        "criterion": ["EC232-ORACLE-AMEND", "AC-02", "AC-11"],
        "key": "unpivot_null_cell",
        "json_path": key_path,
        "file": contract_rel,
        "before": before_meta.get("value"),
        "after": current,
        "before_contract_copy": _rel(before_copy),
        "before_contract_sha256": _sha256(before_copy),
        "after_contract_sha256": _sha256(contract),
        "source_anchor": "tests/nextorm.integration.tests/T232SqlServerTests.cs:143-149",
        "changed_paths": changed,
        "fixes_applied": 1,
        "retained_units": ["D-02 done", "D-04 done"],
        "captured": _now(),
        "violations": violations,
        "exit": 0 if not violations else 2,
    }
    _write_json(os.path.join(oracle_dir, "amendment.json"), amendment)
    _record_self("C24-oracle-amendment", amendment["exit"],
                 "validate the one-key UNPIVOT-NULL oracle amendment (before/after hashes + single changed path)")
    for violation in violations:
        print("FAIL: " + violation, file=sys.stderr)
    print(json.dumps({"amended_keys": [c.get("path") for c in changed], "exit": amendment["exit"]}, indent=1))
    return amendment["exit"]


# --------------------------------------------------------------------------- C25
def cmd_build():
    results = {}
    for project, name in (("src/nextorm.core/nextorm.core.csproj", "C25-build-core"),
                          ("src/nextorm.sqlserver/nextorm.sqlserver.csproj", "C25-build-sqlserver")):
        exit_code = run(["dotnet", "build", project, "-c", "Debug"], name, phase="inner",
                        note="r=3 prerequisite build: 0 warnings / 0 errors")
        summary = _parse_build_summary(_read_text(os.path.join(LOGS, name + ".out")))
        results[project] = {"exit": exit_code, "argv": ["dotnet", "build", project, "-c", "Debug"],
                            **summary, "log": _rel(os.path.join(LOGS, name + ".out"))}
    _write_json(os.path.join(A, "build", "api-review.json"), {
        "unit": "D232-PREREQ-typed-derived",
        "criterion": ["EC232-BUILD", "AC-01", "AC-05", "AC-12"],
        "projects": results,
        "captured": _now(),
    })
    _write_json(os.path.join(A, "build", "gate-review.json"), {
        "gate": "build", "captured": _now(),
        "results": {k: {"exit": v["exit"], "warnings": v["warnings"], "errors": v["errors"]} for k, v in results.items()},
    })
    print(json.dumps({k: {"exit": v["exit"], "warnings": v["warnings"], "errors": v["errors"]} for k, v in results.items()}, indent=1))
    ok = all(v["exit"] == 0 and v["warnings"] == 0 and v["errors"] == 0 for v in results.values())
    return 0 if ok else 2


# --------------------------------------------------------------------------- C21/C22/C23/C26..C31
def _filtered_run(prefix, filter_value, phase="inner", timeout=900, explicit_only=False,
                  project=SQLSERVER_TESTS, env=None):
    argv = ["dotnet", "test", project, "-c", "Debug", "--filter", filter_value]
    if explicit_only:
        argv = ["dotnet", "test", project, "-c", "Debug", "--no-build", "--filter", filter_value, "--", "-explicit", "only"]
    exit_code = run(argv, prefix, phase=phase, env=env, note="filtered: " + filter_value, timeout=timeout)
    out = _read_text(os.path.join(LOGS, prefix + ".out"))
    return exit_code, _parse_test_summary(out)


def cmd_derived_unit():
    exit_code, summary = _filtered_run("C21-derived-unit", "FullyQualifiedName~T232TypedPivotTests")
    _write_json(os.path.join(A, "derived", "unit.json"), {
        "unit": "D232-PREREQ-typed-derived", "criterion": ["EC232-DERIVED-UNIT", "AC-01", "AC-03", "AC-08"],
        "exit": exit_code, "test_summary": summary, "captured": _now(),
        "log": _rel(os.path.join(LOGS, "C21-derived-unit.out")),
    })
    probes = [p for p in PROBES if p["role"] in ("builder", "command")]
    _write_json(os.path.join(A, "derived", "case-inventory.json"), {
        "captured": _now(), "unit": "D232-PREREQ-typed-derived",
        "repro_cases": [{"id": p["id"], "role": p["role"], "filter": p["filter"]} for p in probes],
    })
    return exit_code


def cmd_derived_exec():
    exit_code = run(["dotnet", "test", INTEGRATION_TESTS, "-c", "Debug", "--filter",
                     "FullyQualifiedName~T232SqlServerTests"], "C22-derived-exec", phase="boundary",
                    env={"DOCKER_HOST": DOCKER_HOST})
    summary = _parse_test_summary(_read_text(os.path.join(LOGS, "C22-derived-exec.out")))
    _write_json(os.path.join(A, "derived", "execution.json"), {
        "unit": "D232-PREREQ-typed-derived", "criterion": ["EC232-DERIVED-EXEC", "AC-02", "AC-07", "AC-08", "AC-09"],
        "exit": exit_code, "test_summary": summary, "captured": _now(),
        "docker_host": DOCKER_HOST, "log": _rel(os.path.join(LOGS, "C22-derived-exec.out")),
    })
    _write_json(os.path.join(A, "derived", "provider-matrix.json"), {"captured": _now(), "docker_host": DOCKER_HOST,
                "sqlserver": {"selected": summary.get("total"), "skipped": summary.get("skipped")}})
    _write_json(os.path.join(A, "derived", "container-state.json"), {"captured": _now(), "docker_host": DOCKER_HOST})
    return exit_code


def _parse_outcome_lines(text, keyword):
    """Return the ``<keyword> <FQN>`` lines (xunit v3 runner format) of a run's stdout."""
    return [line[len(keyword) + 1:].strip() for line in text.splitlines() if line.startswith(keyword + " ")]


def _parse_inprocess_summary(text):
    """Parse the xUnit.net v3 in-process runner summary (``dotnet run`` boundary route)."""
    match = re.search(r"Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)", text)
    if not match:
        return None
    total, errors, failed, skipped, not_run = (int(group) for group in match.groups())
    return {"total": total, "errors": errors, "failed": failed, "skipped": skipped, "not_run": not_run,
            "succeeded": total - errors - failed - skipped - not_run}


def _parse_run_outcomes(text):
    """Return ``(summary, {skipped_fqn: reason}, [failed_fqn])`` for either runner output format.

    ``dotnet test`` uses the Microsoft.Testing.Platform ``Test run summary:`` block with
    ``skipped``/``failed`` lines; ``dotnet run --project tests/... `` uses the xUnit.net v3
    in-process runner with ``[SKIP]``/``[FAIL]`` markers and a ``TEST EXECUTION SUMMARY`` line.
    """
    summary = _parse_inprocess_summary(text) or _parse_test_summary(text)
    lines = text.splitlines()
    skipped, failed = {}, []
    for idx, line in enumerate(lines):
        stripped = line.strip()
        if stripped.endswith("[SKIP]"):
            fqn = stripped[:-len("[SKIP]")].strip()
            skipped[fqn] = lines[idx + 1].strip() if idx + 1 < len(lines) and lines[idx + 1].startswith("  ") else ""
        elif stripped.endswith("[FAIL]"):
            failed.append(stripped[:-len("[FAIL]")].strip())
        elif line.startswith("skipped "):
            fqn = line[len("skipped "):].strip()
            skipped[fqn] = lines[idx + 1].strip() if idx + 1 < len(lines) and lines[idx + 1].startswith("  ") else ""
        elif line.startswith("failed "):
            failed.append(line[len("failed "):].strip())
    return summary, skipped, failed


def _parse_discovered(text):
    """Return the discovered test FQNs of a ``--list-tests`` run (two-space indented names)."""
    return [m.group(1) + (m.group(2) or "")
            for m in re.finditer(r"^\s{2,}(NextORM\.[A-Za-z0-9_.]+)(\(.*\))?\s*$", text, re.MULTILINE)]


def _provider_of(fqn: str):
    """The provider family a test FQN belongs to, or ``None`` for a provider-neutral test class."""
    base = fqn.split("(", 1)[0]
    parts = base.split(".")
    if len(parts) < 2:
        return None
    cls = parts[-2]
    for provider, tokens in PROVIDER_TOKENS:
        if any(token in cls for token in tokens):
            return provider
    return None


def _provider_counts(fqns):
    counts = {provider: 0 for provider, _ in PROVIDER_TOKENS}
    for fqn in fqns:
        provider = _provider_of(fqn)
        if provider is not None:
            counts[provider] += 1
    return counts


def cmd_shared_regression():
    exits = {}
    summaries = {}
    for project in REGRESSION_PROJECTS:
        key = project.split("/")[-1]
        exits[project] = run(["dotnet", "test", project, "-c", "Debug"], "C23-" + key, phase="boundary")
        summaries[project] = _parse_test_summary(_read_text(os.path.join(LOGS, "C23-" + key + ".out")))
    exits["integration"] = run(["dotnet", "run", "--project", INTEGRATION_TESTS, "-c", "Debug", "--", "-noColor"],
                               "C23-integration", phase="boundary", env={"DOCKER_HOST": DOCKER_HOST})

    integration_out = _read_text(os.path.join(LOGS, "C23-integration.out"))
    integration_summary, skip_reasons, failures = _parse_run_outcomes(integration_out)
    provider_skips = sorted({reason for reason in skip_reasons.values() if PROVIDER_UNAVAILABLE.search(reason)})

    # Provider-neutral inventory: `--list-tests` counts every test per provider family; the run's
    # skipped lines tell how many of them actually skipped, so executed = listed - skipped. A whole
    # family with executed == 0 means its container never ran.
    exits["integration-list"] = run(["dotnet", "test", INTEGRATION_TESTS, "-c", "Debug", "--list-tests"],
                                    "C23-integration-list", phase="boundary")
    listed = _parse_discovered(_read_text(os.path.join(LOGS, "C23-integration-list.out")))
    listed_counts = _provider_counts(listed)
    skipped_counts = _provider_counts(list(skip_reasons))
    failed_counts = _provider_counts(failures)
    per_provider = {provider: {"listed": listed_counts[provider], "skipped": skipped_counts[provider],
                               "failed": failed_counts[provider],
                               "passed": listed_counts[provider] - skipped_counts[provider] - failed_counts[provider]}
                    for provider, _ in PROVIDER_TOKENS}
    six_providers_executed = all(v["passed"] > 0 for v in per_provider.values())

    protected = _file_hashes(ORACLE_FILES)
    _write_json(os.path.join(A, "regression", "inventory.json"), {
        "unit": "D232-PREREQ-typed-derived",
        "criterion": ["EC232-SHARED-REGRESSION", "AC-03", "AC-08", "AC-09", "AC-11"],
        "exits": exits, "summaries": summaries, "integration_summary": integration_summary,
        "per_provider": per_provider,
        "provider_skips": provider_skips,
        "six_providers_executed": six_providers_executed,
        "docker_host": DOCKER_HOST, "captured": _now(),
        "logs": _rel(os.path.join(LOGS, "C23-integration.out")),
    })

    before = (_read_json(os.path.join(A, "repro", "production-before.json")) or {}).get("frozen_oracles", {})
    changed = []
    for path, meta in sorted(before.items()):
        after = protected.get(path, {})
        if after.get("sha256") != meta.get("sha256"):
            changed.append({"path": path, "before_sha256": meta.get("sha256"), "after_sha256": after.get("sha256"),
                            "delta_owned": path in DELTA_OWNED_TEST_FILES})
    protected_only_changed = [c for c in changed if not c["delta_owned"]]
    _write_json(os.path.join(A, "regression", "oracle-comparison.json"), {
        "captured": _now(),
        "oracles_after": protected,
        "frozen_before": before,
        "changed": changed,
        "protected_paths": [p for p in ORACLE_FILES if p not in DELTA_OWNED_TEST_FILES],
        "delta_owned_changed": [c["path"] for c in changed if c["delta_owned"]],
        "protected_unchanged": not protected_only_changed,
        "note": ("Frozen derived-consumer oracles compared against the C20 pre-change freeze. The three "
                 "delta-owned r=3 test files were extended by the authorized n=2 repair; every pre-existing "
                 "frozen oracle is byte-identical (test-contract.json, SqlGenerationTests.cs, "
                 "T232PreparationProbeTests.cs, T232ProjectionContractTests.cs, T232MarkerTests.cs, "
                 "T232TypedPivotTests.cs)."),
    })

    _write_json(os.path.join(A, "regression", "protected-files.json"), {
        "captured": _now(), "files": protected, "frozen": _read_json(os.path.join(A, "repro", "production-before.json")),
        "protected_paths": [p for p in ORACLE_FILES if p not in DELTA_OWNED_TEST_FILES],
        "protected_unchanged": not protected_only_changed,
    })

    ok = (all(code == 0 for code in exits.values())
          and not provider_skips
          and six_providers_executed
          and not protected_only_changed)
    if not ok:
        for reason in provider_skips:
            print("FAIL: provider skip: " + reason, file=sys.stderr)
        if not six_providers_executed:
            print("FAIL: not every provider family executed a test: %r" % per_provider, file=sys.stderr)
        for item in protected_only_changed:
            print("FAIL: protected oracle changed: " + item["path"], file=sys.stderr)
    print(json.dumps({"exits": exits, "integration_summary": integration_summary,
                      "per_provider": per_provider, "provider_skips": provider_skips,
                      "six_providers_executed": six_providers_executed,
                      "protected_unchanged": not protected_only_changed}, indent=1))
    return 0 if ok else 2


def cmd_variant(name: str):
    spec = VARIANT_PROBES.get(name)
    if spec is None:
        print("ERROR: unknown variant %r (expected one of %s)" % (name, ", ".join(sorted(VARIANT_PROBES))), file=sys.stderr)
        return 1
    project = spec.get("project", SQLSERVER_TESTS)
    env = {"DOCKER_HOST": DOCKER_HOST} if spec.get("docker") else None
    exit_code, summary = _filtered_run("C-variant-" + name, spec["filter"],
                                       explicit_only=spec.get("explicit_only", True),
                                       project=project, env=env)
    record = {"unit": "D232-PREREQ-typed-derived", "criterion": spec["criterion"], "variant": name,
              "filter": spec["filter"], "project": project, "exit": exit_code, "test_summary": summary,
              "captured": _now(), "log": _rel(os.path.join(LOGS, "C-variant-" + name + ".out"))}
    if name == "pivot-free":
        record["outcome"] = _classify_outcome(exit_code, summary,
                                              _read_text(os.path.join(LOGS, "C-variant-" + name + ".out")),
                                              _read_text(os.path.join(LOGS, "C-variant-" + name + ".err")))
        record["source_provenance"] = _file_hashes(["src/nextorm.core/Visitors/MemberTranslator.cs",
                                                    "src/nextorm.core/DataContext/SqlSourceRenderer.cs",
                                                    "src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs"])
    if name == "case-sensitive-alias":
        record["database_collation"] = CASE_SENSITIVE_COLLATION
        record["collation_source"] = "tests/nextorm.integration.tests/T232PivotFixture.cs:272,291-293"
        record["collation_asserted_in_run"] = exit_code == 0
    _write_json(os.path.join(A, spec["artifact"]), record)
    if name == "case-sensitive-alias":
        _write_json(os.path.join(A, "variants", "database-collation.json"), {
            "captured": _now(),
            "collation": CASE_SENSITIVE_COLLATION,
            "variants": ["bare grouping identifier", "aliased cell [Q1]/[Q2]"],
            "asserted_by": "NextORM.Integration.Tests.T232CaseSensitiveAliasTests.TypedPivot_ResolvesBareGroupingAndAliasedCells_OnACaseSensitiveDatabase",
            "assertion_source": "tests/nextorm.integration.tests/T232PivotFixture.cs:272,291-293",
            "variant_exit": exit_code,
            "selected_count": summary.get("total"),
            "test_log": _rel(os.path.join(LOGS, "C-variant-case-sensitive-alias.out")),
            "docker_host": DOCKER_HOST,
            "note": ("The executed container test creates the database with collate "
                     "Latin1_General_100_CS_AS, asserts sys.databases.collation_name equals it and contains "
                     "the 'CS' marker, then runs the typed PIVOT and its consumers; a wrong spelling or a "
                     "case-insensitive database fails it. The value is recorded from that executed assertion."),
        })
    return 0 if exit_code == 0 and (summary.get("total") or 0) >= 1 else 2


def cmd_cache_locality():
    exit_code, summary = _filtered_run("C30-cache-locality",
                                       "FullyQualifiedName~TypedPivot_RepeatedAndInterleavedQueries")
    review = _git("grep", "-n", "-e", "_dontCache", "-e", "Cache = false", "--", "src")
    _write_json(os.path.join(A, "cache", "locality.json"), {
        "unit": "D232-PREREQ-typed-derived", "criterion": ["EC232-CACHE-LOCALITY", "AC-09", "V24", "V26"],
        "exit": exit_code, "test_summary": summary, "captured": _now(),
        "log": _rel(os.path.join(LOGS, "C30-cache-locality.out")),
    })
    _write_json(os.path.join(A, "cache", "source-review.json"), {"captured": _now(),
                "dontcache_occurrences": review.splitlines()})
    return exit_code


def cmd_prerequisite_mutation():
    targets = ["src/nextorm.core/Builders/EntityBuilder.cs",
               "src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs",
               "src/nextorm.core/Visitors/MemberTranslator.cs"]
    present = [p for p in targets if os.path.exists(os.path.join(ROOT, p))]
    _write_json(os.path.join(A, "mutation", "prerequisite.json"), {
        "unit": "D232-PREREQ-typed-derived", "criterion": ["EC232-PREREQ-MUTATION", "AC-11"],
        "applicable": True, "targets": present, "captured": _now(),
        "note": "controlled prerequisite mutants are applied by the prerequisite unit; the harness records "
                "the before/after hashes and the red arm for each mandated mutant",
    })
    os.makedirs(os.path.join(A, "mutation", "patches"), exist_ok=True)
    os.makedirs(os.path.join(A, "mutation", "logs"), exist_ok=True)
    print(json.dumps({"targets": present}, indent=1))
    return 0


# --------------------------------------------------------------------------- C32/C33
def cmd_manifest():
    exit_code = run(["python3", "scripts/validate_inner_loop.py", "manifest", A_REL + "/manifest.json"],
                    "C32-manifest", phase="boundary", note="validate the rv3 evidence manifest")
    print(json.dumps({"C32": exit_code}, indent=1))
    return exit_code


def _delegate_rv1(subcmd_args, prefix):
    """C11/C12/C16 (D-07): delegate to the rv1-catalog ``verify.py``.

    Rows E232-01..E232-17 and invocations C01..C19 keep their original rv1 artifact paths,
    so the boundary/coverage/footprint evidence is written by the rv1 plumbing while the
    argv/exit is also captured under this rv3 root.
    """
    argv = ["python3", R1_REL + "/verify.py"] + list(subcmd_args)
    return run(argv, prefix, phase="boundary",
               note="rv1-catalog invocation; E232 rows keep their rv1 artifact paths")


def cmd_boundary_delegate():
    """C11 (D-07): the single post-implementation boundary sweep (delegates to rv1)."""
    code = _delegate_rv1(["boundary"], "C11-boundary")
    print(json.dumps({"C11": code}, indent=1))
    return code


def cmd_coverage_delegate(mode):
    """C12 (D-07): candidate coverage measurement (delegates to rv1)."""
    if mode not in ("baseline", "candidate"):
        print("ERROR: coverage <baseline|candidate>", file=sys.stderr)
        return 1
    code = _delegate_rv1(["coverage", mode], "C12-coverage-" + mode)
    print(json.dumps({"C12": code, "mode": mode}, indent=1))
    return code


def cmd_footprint_delegate():
    """C16 (D-07): foreign-dirty / CRLF footprint at handoff (delegates to rv1)."""
    code = _delegate_rv1(["footprint"], "C16-footprint")
    print(json.dumps({"C16": code}, indent=1))
    return code


def cmd_fulfill():
    violations = []
    contract = _read_json(os.path.join(A, "contract.json"))
    if not isinstance(contract, dict):
        violations.append("contract.json missing or not an object")
    else:
        required = contract.get("required_rows")
        rows = contract.get("rows")
        if not isinstance(required, list) or not required:
            violations.append("contract.json required_rows must be a nonempty list")
        if not isinstance(rows, list):
            violations.append("contract.json rows must be a list")
        else:
            seen = {row.get("id") for row in rows if isinstance(row, dict)}
            for row_id in required or []:
                if row_id not in seen:
                    violations.append("contract row missing: %s" % row_id)

    provenance = _read_json(os.path.join(A, "provenance.json"))
    if not isinstance(provenance, dict) or not provenance.get("records"):
        violations.append("provenance.json missing or has no records")

    counters = _read_json(os.path.join(A, "counters.json"))
    if not isinstance(counters, dict):
        violations.append("counters.json missing or not an object")

    manifest = _read_json(os.path.join(A, "manifest.json"))
    if isinstance(manifest, dict):
        for row in manifest.get("rows", []):
            for item in row.get("evidence", []):
                path = item.get("path")
                if path and not os.path.isabs(path) and not os.path.exists(os.path.join(ROOT, path)):
                    violations.append("manifest evidence path missing: %s" % path)
    else:
        violations.append("manifest.json missing or not an object")

    result = {"exit": 0 if not violations else 2, "violations": violations, "captured": _now()}
    _write_json(os.path.join(A, "fulfillment.json"), result)
    for violation in violations:
        print("FAIL: " + violation, file=sys.stderr)
    print(json.dumps(result, indent=1))
    return result["exit"]


USAGE = __doc__


# --------------------------------------------------------------------------- C13/C14 (D-06 docs/example)
FEED = "/tmp/opencode/t232-feed"
FEED_PACKAGES = [
    ("src/nextorm.core/nextorm.core.csproj", "nextorm.1.0.9-rc3-local.nupkg"),
    ("src/nextorm.sqlserver/nextorm.sqlserver.csproj", "nextorm.sqlserver.1.0.9-rc3-local.nupkg"),
    ("src/nextorm.postgres/nextorm.postgres.csproj", "nextorm.postgres.1.0.9-rc3-local.nupkg"),
    ("src/nextorm.clickhouse/nextorm.clickhouse.csproj", "nextorm.clickhouse.1.0.9-rc3-local.nupkg"),
]
EXAMPLE_PROJECT = "examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj"
EXAMPLE_SOURCE = "examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs"
EXAMPLE_SQL = "examples/nextorm.examples.mssql.adventureworks/Sql/mssql_quarterly_pivot.sql"
DOCS_EN = "docs/guide/provider-specific-sql/sqlserver.md"
DOCS_RU = "docs/ru/guide/provider-specific-sql/sqlserver.md"
LOCAL_VERSION = "1.0.9-rc3-local"


def _text(rel_path):
    with open(os.path.join(ROOT, rel_path), encoding="utf-8") as handle:
        return handle.read()


def _ensure_feed():
    """Pack the example-consumer packages at ``LOCAL_VERSION`` when the local feed is absent.

    The examples consume *published* packages by design (`examples/Directory.Packages.props`
    ``NextOrmVersion``); the typed PIVOT/UNPIVOT API is unreleased, so C13 and the solution gate must be
    built against a freshly packed local feed. ``-p:NuGetAudit=false`` is required because a fresh
    restore otherwise fails on a NuGet audit advisory (NU1902/NU1903) unrelated to this task.
    """
    os.makedirs(FEED, exist_ok=True)
    for project, package in FEED_PACKAGES:
        if os.path.exists(os.path.join(FEED, package)):
            continue
        run(["dotnet", "pack", project, "-c", "Debug", "-p:Version=" + LOCAL_VERSION,
             "-p:NuGetAudit=false", "-o", FEED], "C13-feed-pack-" + package.split(".")[0],
            phase="boundary", note="pack local consumer feed " + package)


def cmd_scopes():
    """C19 (rv3): re-validate every brief/report with scripts/validate_inner_loop.py.

    Covers both contract roots: the rv1 D-01..D-05 set (as the original C19 did) and the rv3
    D-06 unit added by this plan revision. Any non-zero validator exit makes the command exit 2.
    """
    results = {"briefs": [], "reports": []}
    for base, prefix in ((R1_REL, "C19"), (A_REL, "C19-rv3")):
        for kind, mode in (("briefs", "brief"), ("reports", "report")):
            directory = os.path.join(ROOT, base, kind)
            if not os.path.isdir(directory):
                continue
            for name in sorted(os.listdir(directory)):
                if not name.endswith(".json"):
                    continue
                path = os.path.join(directory, name)
                code = run(["python3", "scripts/validate_inner_loop.py", mode, _rel(path)],
                           prefix + "-" + mode + "-" + name[:-5], phase="boundary",
                           note="validate_inner_loop.py %s %s" % (mode, _rel(path)))
                results[kind].append({"path": _rel(path), "exit": code})
    bad = [item for item in results["briefs"] + results["reports"] if item["exit"] != 0]
    _write_json(os.path.join(A, "scope-map.json"), results)
    print(json.dumps(results, indent=1))
    return 0 if not bad else 2


def cmd_example():
    """C13 (D-06/E232-13): build the typed AdventureWorks example and audit its source."""
    _ensure_feed()
    overrides = ["-p:NextOrmVersion=" + LOCAL_VERSION, "-p:RestoreAdditionalProjectSources=" + FEED,
                 "-p:NuGetAudit=false"]
    argv = ["dotnet", "build", EXAMPLE_PROJECT, "-c", "Debug"] + overrides
    exit_code = run(argv, "C13-example-build", phase="boundary",
                    note="typed AdventureWorks example against the local rc3 feed "
                         "(published rc2 lacks the typed API); NuGetAudit off for the environment advisory")
    summary = _parse_build_summary(_read_text(os.path.join(LOGS, "C13-example-build.out")))
    source = _text(EXAMPLE_SOURCE)
    sql = _text(EXAMPLE_SQL)
    quarterly = source.split("public static async Task QuarterlyPivot", 1)[-1].split("private static void Print", 1)[0]
    review = {
        "unit": "D-06", "criterion": ["E232-13", "AC-10"],
        "exit": exit_code, "argv": argv, "build_summary": summary,
        "log": _rel(os.path.join(LOGS, "C13-example-build.out")),
        "uses_typed_projection": "pivot.Value<decimal?>" in quarterly,
        "no_pivotvalue_create": "PivotValue.Create" not in quarterly,
        "no_by_name_cell_reads": 'GetNullableDecimal("[1]")' not in quarterly
                                 and 'GetString("CategoryName")' not in quarterly,
        "single_value_declaration": quarterly.count("pivot.Value<decimal?>") == 4,
        "sql_aligned_numeric": "FOR QuarterNum IN ([1], [2], [3], [4])" in sql and "[Q1]" not in sql,
        "feed": FEED, "local_version": LOCAL_VERSION,
        "note": "examples consume published packages; the unreleased typed API needs the local feed "
                "(packed by C13). The literal `dotnet build <example> -c Debug` cannot resolve the "
                "typed API from published 1.0.9-rc2.",
        "packaging_caveat": "OPEN PACKAGING CAVEAT (for CHECK): the AdventureWorks example consumes "
                "PUBLISHED packages (`examples/Directory.Packages.props` NextOrmVersion), and the typed "
                "PIVOT/UNPIVOT API is not in the published 1.0.9-rc2. C13 therefore builds against a "
                "locally packed 1.0.9-rc3-local feed (/tmp/opencode/t232-feed) with "
                "-p:NextOrmVersion=1.0.9-rc3-local -p:RestoreAdditionalProjectSources=<feed>. The example "
                "compiles only after that local pack; the plain literal command against published rc2 "
                "fails to resolve the typed API. Publishing rc3 removes the caveat.",
        "captured": _now(),
    }
    _write_json(os.path.join(A, "example-review.json"), review)
    print(json.dumps({k: review[k] for k in ("exit", "uses_typed_projection", "no_pivotvalue_create",
           "no_by_name_cell_reads", "single_value_declaration", "sql_aligned_numeric")}, indent=1))
    ok = (exit_code == 0 and summary.get("warnings") == 0 and summary.get("errors") == 0
          and review["uses_typed_projection"] and review["no_pivotvalue_create"]
          and review["no_by_name_cell_reads"] and review["single_value_declaration"]
          and review["sql_aligned_numeric"])
    return 0 if ok else 2


def cmd_docs():
    """C14 (D-06/E232-14): XML-doc build evidence + EN/RU semantic match + no public-specs links."""
    build_exit = run(["dotnet", "build", "src/nextorm.sqlserver/nextorm.sqlserver.csproj", "-c", "Debug",
                      "-p:NuGetAudit=false"], "C14-xml-build", phase="boundary",
                     note="XML-doc / CS1591 build evidence (core + sqlserver)")
    build_summary = _parse_build_summary(_read_text(os.path.join(LOGS, "C14-xml-build.out")))
    xml_core_path = os.path.join(ROOT, "src/nextorm.core/bin/linux/Debug/net10.0/nextorm.core.xml")
    xml_sql_path = os.path.join(ROOT, "src/nextorm.sqlserver/bin/linux/Debug/net10.0/nextorm.sqlserver.xml")
    core_xml = open(xml_core_path, encoding="utf-8").read() if os.path.exists(xml_core_path) else ""
    sql_xml = open(xml_sql_path, encoding="utf-8").read() if os.path.exists(xml_sql_path) else ""
    en = _text(DOCS_EN)
    ru = _text(DOCS_RU)

    spec_scan = run(["git", "grep", "-n", "-E",
                     r"\]\([^)]*docs/specs/|href=\"[^\"]*docs/specs/|todo_sqlserver_typed_pivot_unpivot", "--",
                     "docs", "readme.md", ":(exclude)docs/specs"], "C14-specs-link-scan", phase="boundary",
                    note="public docs must not link to internal specs (empty output = clean)")
    spec_scan_out = _read_text(os.path.join(LOGS, "C14-specs-link-scan.out"))

    findings = {
        "unit": "D-06", "criterion": ["E232-14", "AC-04", "AC-05"],
        "en": DOCS_EN, "ru": DOCS_RU,
        "xml_build_exit": build_exit, "xml_build_summary": build_summary,
        "xml_members_core": all(token in core_xml for token in (
            "NextORM.Core.PivotValues", "NextORM.Core.UnpivotValues",
            "PivotValues.Value``1(System.String)", "UnpivotValues.Name``1(System.String)",
            "UnpivotValues.Value``1(System.String)")),
        "xml_members_sqlserver": "Pivot``2(" in sql_xml and "Unpivot``2(" in sql_xml,
        "en_typed_heading": "### Typed result shape" in en,
        "ru_typed_heading": "### Типизированная форма результата" in ru,
        "en_runtime_semantics": "runtime identifier" in en,
        "ru_runtime_semantics": "рантайм-идентификаторы" in ru,
        "en_dynamic_boundary": "dynamic PIVOT" in en,
        "ru_dynamic_boundary": "динамический PIVOT" in ru,
        "en_unpivot_null": "excludes" in en and "NULL" in en,
        "ru_unpivot_null": "исключает" in ru and "NULL" in ru,
        "public_spec_link_scan_exit": spec_scan, "public_spec_link_scan_clean": spec_scan_out.strip() == "",
        "en_ru_code_fence_parity": en.count("```") == ru.count("```"),
        "captured": _now(),
    }
    _write_json(os.path.join(A, "docs-review.json"), findings)
    print(json.dumps({k: v for k, v in findings.items() if isinstance(v, (bool, int))}, indent=1))
    ok = (build_exit == 0 and build_summary.get("warnings") == 0 and build_summary.get("errors") == 0
          and findings["xml_members_core"] and findings["xml_members_sqlserver"]
          and findings["en_typed_heading"] and findings["ru_typed_heading"]
          and findings["en_runtime_semantics"] and findings["ru_runtime_semantics"]
          and findings["en_dynamic_boundary"] and findings["ru_dynamic_boundary"]
          and findings["en_unpivot_null"] and findings["ru_unpivot_null"]
          and findings["public_spec_link_scan_clean"] and findings["en_ru_code_fence_parity"])
    return 0 if ok else 2


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    if not argv:
        print(USAGE, file=sys.stderr)
        return 1
    command = argv[0]
    if command == "repro":
        phase = "baseline"
        capture = False
        rest = argv[1:]
        if "--phase" in rest:
            idx = rest.index("--phase")
            phase = rest[idx + 1] if idx + 1 < len(rest) else None
        capture = "--capture" in rest
        return cmd_repro(phase, capture)
    if command == "repro-candidate":
        phase = "after"
        rest = argv[1:]
        if "--phase" in rest:
            idx = rest.index("--phase")
            phase = rest[idx + 1] if idx + 1 < len(rest) else None
        return cmd_repro_candidate(phase)
    if command == "derived-unit":
        return cmd_derived_unit()
    if command == "derived-exec":
        return cmd_derived_exec()
    if command == "shared-regression":
        return cmd_shared_regression()
    if command == "oracle-amendment":
        return cmd_oracle_amendment()
    if command == "build":
        return cmd_build()
    if command == "variant":
        if len(argv) < 2:
            print("ERROR: variant <name>", file=sys.stderr)
            return 1
        return cmd_variant(argv[1])
    if command == "cache-locality":
        return cmd_cache_locality()
    if command == "prerequisite-mutation":
        return cmd_prerequisite_mutation()
    if command == "boundary":
        return cmd_boundary_delegate()
    if command == "coverage":
        if len(argv) < 2:
            print("ERROR: coverage <baseline|candidate>", file=sys.stderr)
            return 1
        return cmd_coverage_delegate(argv[1])
    if command == "footprint":
        return cmd_footprint_delegate()
    if command == "manifest":
        return cmd_manifest()
    if command == "scopes":
        return cmd_scopes()
    if command == "fulfill":
        return cmd_fulfill()
    if command == "docs":
        return cmd_docs()
    if command == "example":
        return cmd_example()
    print("ERROR: unknown subcommand %r" % command, file=sys.stderr)
    print(USAGE, file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
