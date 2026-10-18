#!/usr/bin/env python3
"""T247 (#247) JSON streaming performance gate driver.

Planned source of the T247 evidence contract (rv=2).  It is *stdlib-only* and deterministic: no
network access, no third-party imports, and every subcommand writes machine-readable JSON plus a
raw log under ``<evidence-root>/<subcommand>/`` and exits ``0`` ONLY on a complete, valid result.

What it decides
---------------
* ``baseline`` / ``phase`` / ``final`` (aliases ``B`` / ``P`` / ``F``) run BenchmarkDotNet rounds
  (``NEXTORM_BENCH_FULL=1``, full ``Job.Default``, the established ``InProcessEmitToolchain``) and
  capture the raw BDN exports per round.  Two full rounds, each with its own dedicated BDN output
  directory; each round fingerprints ``HEAD`` + ``git status --porcelain`` + a scoped source-state
  fingerprint *before* the block and refuses to record numbers if it moved.
* ``compare`` (alias ``C``) is pure computation over ``baseline/`` + ``final/`` + ``phase/`` and
  decides the literal thresholds of R247-01, R247-12, R247-02 and R247-09.  Rounds are never
  averaged into a pass; a discordant pair (one round pass, one fail) is ``inconclusive``.
* ``phases`` attributes the measured compile-only spike, the steady writer and the public total,
  and reports an explicitly labelled **un-attributed residual** (never relabelled "preparation").
* ``lowering`` runs the provider SQL-lowering / native-eligibility filtered tests.
* ``boundary`` runs the affected non-integration suites (six projects); ``coverage`` collects
  coverage *during* that sweep (not an extra sweep).
* ``fault-checks`` seeds three faults, proves each is killed and that the tests pass after
  restoration.
* ``gate`` wraps ``iteration14_gate.py`` and adds the three-bucket JSON timing/allocation gate.
* ``acceptance`` runs two paired rounds of the documented 7-case acceptance set.
* ``audit``, ``docs`` and ``preflight`` are the provenance / documentation / prerequisite gates.

Lock protocol (recorded, not resolved)
-------------------------------------
The plan text names ``/tmp/opencode/nextorm-bdn.lock``; ``docs/specs/status/collection-1.0.9-rc3-open-3.md:45``
pins ``/tmp/opencode/nextorm-bdn-host.lock`` as canonical and states it supersedes every per-plan
lock name; only the ``-host`` variant exists on disk (the T244/D243/D246 lanes used it).  The driver
therefore takes the OUTER lock as ``flock -x /tmp/opencode/nextorm-bdn-host.lock`` (strictly safer:
it also excludes a foreign host-lock holder) and never nests a second lock.  The driver does NOT
flock itself -- the caller wraps it -- but it detects an overlap (a foreign
``dotnet build|test|run``, BDN/testhost, or a coverage collector running inside its block, or a
``HEAD``/fingerprint move) and invalidates the round with an explicit reason code.

Exit codes
----------
0  complete valid result / all literal thresholds pass
1  a literal threshold failed
2  input error: missing, malformed, zero-matched or mismatched evidence
3  inconclusive: a discordant round pair (one round pass, one fail)
4  round invalidated: overlap / lock not detected / source state moved
5  prerequisite unresolved (missing CLI/DB/fault specs/budget rows)

Usage
-----
    flock -x /tmp/opencode/nextorm-bdn-host.lock \\
        python3 .pdca/scripts/perf/t247_json_gate.py baseline --rounds 2 --evidence-root .pdca/T247/rv2
    python3 .pdca/scripts/perf/t247_json_gate.py compare --evidence-root .pdca/T247/rv2
"""
from __future__ import annotations

import argparse
import csv
import fnmatch
import hashlib
import json
import math
import os
import re
import shutil
import statistics
import subprocess
import sys
import time
import types
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

TOOL = "t247_json_gate"
TASK = "T247"
ISSUE = 247

REPO_ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EVIDENCE_ROOT = REPO_ROOT / ".pdca" / "T247" / "rv2"

BENCH_PROJECT = "benchmarks/nextorm.benchmark"
BENCH_SRC_DIR = REPO_ROOT / "benchmarks" / "nextorm.benchmark"
ITERATION14_GATE = REPO_ROOT / ".pdca" / "scripts" / "perf" / "iteration14_gate.py"
ITERATION14_BUDGETS = REPO_ROOT / ".pdca" / "scripts" / "perf" / "iteration14-budgets.json"
MAIN_COVERAGE_SETTINGS = REPO_ROOT / "coverage.settings.xml"
INTEGRATION_COVERAGE_SETTINGS = REPO_ROOT / "coverage.integration.settings.xml"
SEED_DB = REPO_ROOT / "benchmarks" / "nextorm.benchmark" / "data" / "test.db"
PUBLIC_REPORT = REPO_ROOT / "docs" / "specs" / "performance" / "T247-json-streaming-report.md"

# ------------------------------------------------------------------------------------------------
# Lock
# ------------------------------------------------------------------------------------------------
CANONICAL_LOCK = Path("/tmp/opencode/nextorm-bdn-host.lock")
PLAN_LOCK_NAME = Path("/tmp/opencode/nextorm-bdn.lock")

LOCK_NOTICE = (
    "plan text names /tmp/opencode/nextorm-bdn.lock; "
    "docs/specs/status/collection-1.0.9-rc3-open-3.md:45 pins /tmp/opencode/nextorm-bdn-host.lock as "
    "canonical and states it supersedes every per-plan lock name; only the -host variant exists on "
    "disk (T244/D243/D246 lanes used it). The driver takes the OUTER lock as "
    "`flock -x /tmp/opencode/nextorm-bdn-host.lock` (strictly safer: it excludes a foreign "
    "host-lock holder) and MUST NOT nest a second lock. The driver does not flock itself - the "
    "caller wraps it - and invalidates a round on overlap or a HEAD/fingerprint move."
)

# ------------------------------------------------------------------------------------------------
# Exit codes and reason codes
# ------------------------------------------------------------------------------------------------
EXIT_OK = 0
EXIT_GATE_FAIL = 1
EXIT_INPUT = 2
EXIT_INCONCLUSIVE = 3
EXIT_OVERLAP = 4
EXIT_PREREQ = 5

# ------------------------------------------------------------------------------------------------
# Pinned thresholds -- immutable, never configurable from the CLI.
# ------------------------------------------------------------------------------------------------
THRESHOLDS = types.MappingProxyType(
    {
        "r247_01_ratio_1k_max": 1.2,
        "r247_01_ratio_10k_max": 1.0,
        "r247_01_alloc_10k_max_bytes": 446095,  # conservatively converts 435.64 KiB
        "r247_12_100row_fraction_max": 0.50,
        "r247_12_intercept_fraction_max": 0.50,
        "r247_09_acceptance_cases": 7,
        "r247_09_acceptance_max_wall_s": 240,
        "r247_09_acceptance_case_ratio_max": 1.20,
        "main_line_coverage_min": 85.0,
        "main_branch_coverage_min": 75.0,
        "integration_line_coverage_min": 65.0,
        "integration_branch_coverage_min": 75.0,
        "integration_baseline_line": 74.8,
        "integration_baseline_branch": 64.8,
    }
)

# ------------------------------------------------------------------------------------------------
# Benchmark filters, arms and case lists
# ------------------------------------------------------------------------------------------------
PUBLIC_JSON_FILTERS = ("*SqliteBenchmarkWriteJson*",)
FIXED_COST_FILTERS = ("*SqliteBenchmarkJsonFixedCost*",)
PHASE_FILTERS = ("*SqliteBenchmarkJsonStreamPhase2*", "*JsonStreamPhase2WriterBenchmark*")

#: One round runs each tagged invocation; ``compare`` consumes their union.
BASELINE_INVOCATIONS = (
    ("public-json", PUBLIC_JSON_FILTERS),
    ("fixed-cost", FIXED_COST_FILTERS),
)
PHASE_INVOCATIONS = (
    ("phase2", PHASE_FILTERS),
    ("fixed-cost", FIXED_COST_FILTERS),
)

WRITE_JSON_CLASS = "SqliteBenchmarkWriteJson"
FIXED_COST_CLASS = "SqliteBenchmarkJsonFixedCost"
PUBLIC_TOTAL_ARM = "Nextorm_WriteJson_3Col"
COMPILE_ONLY_ARM = "Nextorm_CompileOnly_3Col"
STEADY_WRITER_ARM = "Nextorm_SteadyWriter_3Col"
CONTROL_ARM = "Nextorm_ToList_Dto_Json"
DTO_STREAM_ARM = "WriteJson_Array_Dto"
LINQ2DB_ARM = "Linq2Db_ToList_Json"

#: E247-02: 3 row counts x 11 control-inclusive arms of SqliteBenchmarkWriteJson = 33 cases.
EXPECTED_PUBLIC_JSON_CASES = 33

R247_01_ROWS = (1000, 10000, 100000)
SMALL_ROW_ROWS = (1, 10, 100)

#: The documented seven-case acceptance set (docs/specs/performance/acceptance-benchmarks.md).
ACCEPTANCE_CASES = (
    ("InMemoryBenchmarkAggregates", "Nextorm_Count"),
    ("InMemoryBenchmarkGroupBy", "Nextorm_GroupByCount"),
    ("SqliteBenchmarkAny", "Nextorm_Cached"),
    ("SqliteBenchmarkCachedPlan", "Prepared_ToList"),
    ("SqliteBenchmarkCachedPlan", "Cached_ToList"),
    ("SqliteBenchmarkCachedPlan", "Cached_PlanOnly_Param"),
    ("SqliteBenchmarkWhere", "Nextorm_Cached_ToListAsync"),
)
ACCEPTANCE_FILTERS = tuple("*%s.%s" % (cls, method) for cls, method in ACCEPTANCE_CASES)
#: Known contradiction: the code has EIGHT acceptance-category matches while the docs pin seven.
ACCEPTANCE_CATEGORY_EXTRA_MATCHES = {
    "SqlServerBufferedNumericAcceptanceBenchmark": (
        "class-level [BenchmarkCategory(\"acceptance\")] (benchmarks/nextorm.benchmark/acceptance/"
        "SqlServerBufferedNumericAcceptanceBenchmark.cs:61); not part of the documented seven and "
        "excluded by the explicit seven-case filter list. Recorded as a fact, not silently dropped "
        "or counted as an eighth acceptance case."
    )
}

T247_BUDGET_ROW_IDS = ("Json_1k_WriteJson", "Json_10k_WriteJson", "Json_100k_WriteJson")

#: Affected non-integration suites (six projects) and the provider SQL-lowering suites.
BOUNDARY_PROJECTS = (
    "tests/nextorm.core.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.clickhouse.tests",
)
LOWERING_PROJECTS = (
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.clickhouse.tests",
)
LOWERING_FILTER = "FullyQualifiedName~Json"

# ------------------------------------------------------------------------------------------------
# Source-state fingerprint
# ------------------------------------------------------------------------------------------------
SOURCE_STATE_PATHSPEC = (
    "src",
    "benchmarks/nextorm.benchmark",
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json",
    "nextorm.slnx",
    ".editorconfig",
)
MEASUREMENT_OUTPUT_PREFIXES = (
    "benchmarks/BenchmarkDotNet.Artifacts/",
    "artifacts/",
    "TestResults/",
    ".pdca/",
)
MEASUREMENT_OUTPUT_SEGMENTS = ("bin", "obj")

_PORCELAIN_UNESCAPES = {
    "a": "\a", "b": "\b", "f": "\f", "n": "\n", "r": "\r", "t": "\t", "v": "\v",
    "\\": "\\", '"': '"',
}

# ------------------------------------------------------------------------------------------------
# Process overlap detection
# ------------------------------------------------------------------------------------------------
CONFLICT_MARKERS = (
    "nextorm.benchmark",
    "BenchmarkDotNet",
    "dotnet-coverage",
    "testhost",
    "vstest.console",
)
CONFLICT_VERBS = ("build", "test", "run", "msbuild", "restore", "pack", "publish")


class InputError(Exception):
    """Missing/malformed/zero-matched evidence (exit 2)."""


class PrereqError(Exception):
    """An unresolved prerequisite (exit 5)."""


class RoundInvalid(Exception):
    """A measurement round was invalidated (exit 4)."""


# ================================================================================================
# Small helpers
# ================================================================================================
def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_text(text: str) -> str:
    return sha256_bytes(text.encode("utf-8"))


def sha256_file(path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_json(path, label: str):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise InputError("cannot read %s %s: %s" % (label, path, exc)) from exc


def write_text(path, text: str) -> None:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_json(path, payload) -> None:
    write_text(path, json.dumps(payload, indent=2, ensure_ascii=False, sort_keys=False) + "\n")


def run_capture(argv, cwd=None, timeout=None):
    """Run *argv*, returning ``(exit_code|None, stdout_text)``; never raises on a nonzero exit."""
    try:
        completed = subprocess.run(
            [str(a) for a in argv],
            cwd=str(cwd) if cwd is not None else None,
            capture_output=True,
            text=True,
            timeout=timeout,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        return None, str(exc)
    return completed.returncode, (completed.stdout or "") + (completed.stderr or "")


def git_capture(args, repo):
    return run_capture(["git", *args], cwd=repo)


def as_number(value):
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    return None


# ================================================================================================
# Fingerprint
# ================================================================================================
def _unquote_porcelain_path(text: str) -> str:
    if len(text) < 2 or not text.startswith('"') or not text.endswith('"'):
        return text
    body = text[1:-1]
    out, index = [], 0
    while index < len(body):
        char = body[index]
        if char != "\\":
            out.append(char)
            index += 1
            continue
        if index + 1 >= len(body):
            out.append(char)
            break
        escaped = body[index + 1]
        octal = body[index + 1:index + 4]
        if escaped in _PORCELAIN_UNESCAPES:
            out.append(_PORCELAIN_UNESCAPES[escaped])
            index += 2
        elif len(octal) == 3 and octal.isdigit():
            out.append(chr(int(octal, 8)))
            index += 4
        else:
            out.append(escaped)
            index += 2
    return "".join(out)


def _porcelain_entry_paths(line: str):
    body = line[3:] if len(line) > 3 else ""
    parts = body.split(" -> ") if " -> " in body else [body]
    return [
        _unquote_porcelain_path(part).replace("\\", "/").rstrip("/") for part in parts
    ]


def _is_measurement_output(path: str) -> bool:
    if path.startswith(MEASUREMENT_OUTPUT_PREFIXES):
        return True
    return any(segment in MEASUREMENT_OUTPUT_SEGMENTS for segment in path.split("/"))


def _is_measurement_entry(line: str) -> bool:
    paths = _porcelain_entry_paths(line)
    return bool(paths) and all(_is_measurement_output(path) for path in paths)


def source_fingerprint(repo, pathspec=SOURCE_STATE_PATHSPEC) -> dict:
    """Fingerprint the compiled inputs that can change the benchmark binary/behaviour."""
    repo = Path(repo)
    code, head = git_capture(["rev-parse", "HEAD"], repo)
    if code != 0 or not head:
        raise InputError("cannot resolve git HEAD in %s: %s" % (repo, head))
    code_s, status = git_capture(
        ["status", "--porcelain", "--untracked-files=all", "--", *pathspec], repo
    )
    if code_s != 0:
        raise InputError("cannot read git status in %s: %s" % (repo, status))
    entries = [
        line for line in status.splitlines() if line.strip() and not _is_measurement_entry(line)
    ]
    return {
        "head": head.splitlines()[0].strip(),
        "porcelain_sha256": sha256_text("\n".join(entries)),
        "dirty_paths": len(entries),
        "pathspec": list(pathspec),
    }


# ================================================================================================
# Overlap detection and inherited lock
# ================================================================================================
def processes_snapshot():
    code, output = run_capture(["ps", "-eo", "pid,ppid,args"])
    if code != 0:
        raise InputError("cannot snapshot processes: %s" % output)
    rows = []
    for line in output.splitlines()[1:]:
        parts = line.split(None, 2)
        if len(parts) < 3:
            continue
        try:
            pid, ppid = int(parts[0]), int(parts[1])
        except ValueError:
            continue
        rows.append({"pid": pid, "ppid": ppid, "args": parts[2]})
    return rows


def process_tree_pids(snapshot) -> set:
    """Our own pid, its ancestors, and every descendant (so our own child is never an overlap)."""
    by_pid = {row["pid"]: row for row in snapshot}
    tree = set()
    pid = os.getpid()
    while pid and pid not in tree:
        tree.add(pid)
        row = by_pid.get(pid)
        pid = row["ppid"] if row else 0
    changed = True
    while changed:
        changed = False
        for row in snapshot:
            if row["ppid"] in tree and row["pid"] not in tree:
                tree.add(row["pid"])
                changed = True
    return tree


def is_conflicting(args: str) -> bool:
    if any(marker in args for marker in CONFLICT_MARKERS):
        return True
    if "dotnet" in args:
        tokens = args.split()
        for index, token in enumerate(tokens):
            if token.endswith("dotnet") or token == "dotnet":
                if index + 1 < len(tokens) and tokens[index + 1] in CONFLICT_VERBS:
                    return True
    return False


def conflicting_processes(snapshot):
    tree = process_tree_pids(snapshot)
    conflicts = []
    for row in snapshot:
        if row["pid"] in tree:
            continue
        if is_conflicting(row["args"]):
            conflicts.append(row)
    return conflicts


def inherited_lock_fd(path):
    """Return the fd of an outer ``flock -x`` wrapper holding *path*, or None."""
    target = str(Path(path))
    fd_dir = Path("/proc/self/fd")
    if not fd_dir.is_dir():
        return None
    for entry in fd_dir.iterdir():
        try:
            descriptor = int(entry.name)
        except ValueError:
            continue
        try:
            if os.readlink(entry) == target:
                return descriptor
        except OSError:
            continue
    return None


def lock_record():
    fd = inherited_lock_fd(CANONICAL_LOCK)
    return {
        "canonical_path": str(CANONICAL_LOCK),
        "plan_text_name": str(PLAN_LOCK_NAME),
        "canonical_exists": CANONICAL_LOCK.exists(),
        "mode": "inherited flock -x" if fd is not None else "not-detected",
        "inherited_fd": fd,
        "notice": LOCK_NOTICE,
    }


# ================================================================================================
# BDN report parsing
# ================================================================================================
_JOB_SEGMENT_RE = re.compile(r"^([A-Za-z0-9_]+)\s*\(")
_TOOLCHAIN_RE = re.compile(r"Toolchain=([A-Za-z0-9_.]+)")
_PARAM_ROWS_RE = re.compile(r"\b(?:Rows|RowCount)=(\d+)")
_REPORT_SUFFIXES = ("-report-full.json", "-report-full-compressed.json", "-report.json")


def job_identity(display: str) -> dict:
    display = display or ""
    segment = display.split(": ", 1)[1] if ": " in display else ""
    match = _JOB_SEGMENT_RE.match(segment)
    job = match.group(1) if match else ""
    toolchain_match = _TOOLCHAIN_RE.search(segment)
    toolchain = toolchain_match.group(1) if toolchain_match else "Default"
    return {
        "job": job,
        "toolchain": toolchain,
        "identity": "%s/%s" % (job, toolchain) if job else "?/%s" % toolchain,
    }


def rows_from_parameters(text):
    if not text:
        return None
    match = _PARAM_ROWS_RE.search(str(text))
    return int(match.group(1)) if match else None


def parse_bdn_json(path):
    data = read_json(path, "BDN report")
    benchmarks = data.get("Benchmarks") if isinstance(data, dict) else None
    if not isinstance(benchmarks, list):
        raise InputError("BDN report %s has no Benchmarks array" % path)
    records = []
    for entry in benchmarks:
        if not isinstance(entry, dict):
            raise InputError("BDN report %s has a non-object benchmark entry" % path)
        short_type = entry.get("Type")
        method = entry.get("Method")
        if not short_type or not method:
            raise InputError("BDN report %s has an entry without Type/Method" % path)
        namespace = entry.get("Namespace") or ""
        memory = entry.get("Memory") if isinstance(entry.get("Memory"), dict) else {}
        stats = entry.get("Statistics") if isinstance(entry.get("Statistics"), dict) else {}
        display = entry.get("DisplayInfo") or ""
        records.append(
            {
                "type": "%s.%s" % (namespace, short_type) if namespace else short_type,
                "short_type": short_type,
                "method": method,
                "display": display,
                "job": job_identity(display)["job"],
                "toolchain": job_identity(display)["toolchain"],
                "identity": job_identity(display)["identity"],
                "mean_ns": as_number(stats.get("Mean")),
                "allocated_b": as_number(memory.get("BytesAllocatedPerOperation")),
                "rows": rows_from_parameters(entry.get("Parameters")),
                "state": entry.get("State"),
                "source": str(path),
            }
        )
    return records


def parse_bdn_csv(path):
    """Cross-check the ``-report.csv`` exporter into the same record shape."""
    records = []
    try:
        with open(path, "r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            headers = reader.fieldnames or []
            for required in ("Method", "Mean", "Allocated"):
                if required not in headers:
                    raise InputError("BDN csv %s has no %r column" % (path, required))
            for row in reader:
                method = (row.get("Method") or "").strip()
                if not method:
                    continue
                records.append(
                    {
                        "method": method,
                        "mean_text": row.get("Mean"),
                        "allocated_text": row.get("Allocated"),
                        "source": str(path),
                    }
                )
    except OSError as exc:
        raise InputError("cannot read BDN csv %s: %s" % (path, exc)) from exc
    return records


def discover_bdn_reports(results_dir):
    results_dir = Path(results_dir)
    if not results_dir.is_dir():
        return []
    reports, seen = [], set()
    candidates = []
    for suffix in _REPORT_SUFFIXES:
        candidates.extend(sorted(results_dir.rglob("*%s" % suffix)))
    for candidate in candidates:
        stem = candidate.name.split("-report")[0]
        if stem in seen:
            continue
        seen.add(stem)
        reports.append(candidate)
    return reports


def case_key(record):
    return (record["short_type"], record["method"], record["rows"])


def index_cases(records):
    index = {}
    for record in records:
        key = case_key(record)
        if key in index:
            raise InputError(
                "duplicate case %s.%s Rows=%s (%s and %s)"
                % (key[0], key[1], key[2], record["source"], index[key]["source"])
            )
        index[key] = record
    return index


def get_case(index, cls, method, rows, where):
    key = (cls, method, rows)
    record = index.get(key)
    if record is None:
        raise InputError("%s: missing case %s.%s Rows=%s" % (where, cls, method, rows))
    return record


def case_mean(index, cls, method, rows, where):
    record = get_case(index, cls, method, rows, where)
    mean = as_number(record.get("mean_ns"))
    if mean is None or mean <= 0.0:
        raise InputError(
            "%s: NA/non-positive Mean for %s.%s Rows=%s" % (where, cls, method, rows)
        )
    return mean


def case_alloc(index, cls, method, rows, where):
    record = get_case(index, cls, method, rows, where)
    allocated = as_number(record.get("allocated_b"))
    if allocated is None or allocated < 0:
        raise InputError(
            "%s: NA allocation for %s.%s Rows=%s (missing is never zero)"
            % (where, cls, method, rows)
        )
    return allocated


# ================================================================================================
# Payload plumbing
# ================================================================================================
def base_payload(sub, root, repo, extra=None) -> dict:
    payload = {
        "tool": TOOL,
        "task": TASK,
        "issue": ISSUE,
        "subcommand": sub,
        "generated_utc": utc_now(),
        "repo": str(repo),
        "evidence_root": str(root),
        "output_dir": str(Path(root) / sub),
        "source_fingerprint": source_fingerprint(repo),
        "children": [],
        "discovered_case_count": 0,
        "executed_case_count": 0,
        "reason_code": "ok",
        "status": "pass",
    }
    if extra:
        payload.update(extra)
    return payload


def emit(sub, root, payload, log_lines):
    out_dir = Path(root) / sub
    out_dir.mkdir(parents=True, exist_ok=True)
    payload["output_dir"] = str(out_dir)
    payload["reason_code"] = payload.get("reason_code", "ok")
    write_json(out_dir / ("%s.json" % sub), payload)
    write_text(out_dir / ("%s.log" % sub), "\n".join(log_lines) + ("\n" if log_lines else ""))


def log_child(children, argv, exit_code, log_path, discovered, executed):
    children.append(
        {
            "argv": [str(a) for a in argv],
            "exit_code": exit_code,
            "log": str(log_path),
            "discovered_case_count": discovered,
            "executed_case_count": executed,
        }
    )


# ================================================================================================
# BDN rounds (baseline / phase / final)
# ================================================================================================
def benchmark_argv(filters, artifacts_dir):
    return [
        "dotnet",
        "run",
        "--project",
        BENCH_PROJECT,
        "-c",
        "Release",
        "--",
        "--filter",
        *filters,
        "--exporters",
        "json",
        "csv",
        "--artifacts",
        str(artifacts_dir),
    ]


def child_environment():
    env = dict(os.environ)
    env["NEXTORM_BENCH_FULL"] = "1"
    return env


def run_round(sub, round_index, invocations, root, repo, timeout):
    """Run every tagged invocation of one round and return its validity record."""
    round_dir = Path(root) / sub / ("round-%d" % round_index)
    round_dir.mkdir(parents=True, exist_ok=True)
    fingerprint_before = source_fingerprint(repo)
    lock = lock_record()
    snapshot_before = processes_snapshot()
    conflicts_before = conflicting_processes(snapshot_before)
    reasons = []
    children = []
    cases = []
    records = []
    env = child_environment()
    started = utc_now()
    clock = time.monotonic()
    for tag, filters in invocations:
        artifacts_dir = round_dir / tag
        artifacts_dir.mkdir(parents=True, exist_ok=True)
        argv = benchmark_argv(filters, artifacts_dir)
        console = round_dir / ("%s.console.log" % tag)
        with open(console, "wb") as log:
            try:
                completed = subprocess.run(
                    argv, cwd=str(repo), env=env, stdout=log, stderr=subprocess.STDOUT,
                    timeout=timeout,
                )
                exit_code = completed.returncode
            except subprocess.TimeoutExpired:
                log.write(b"\ninvocation exceeded the timeout; child killed\n")
                exit_code = None
            except OSError as exc:
                log.write(("\nlaunch failed: %s\n" % exc).encode("utf-8"))
                exit_code = None
        results_dir = artifacts_dir / "results"
        reports = discover_bdn_reports(results_dir)
        round_cases = []
        for report in reports:
            for record in parse_bdn_json(report):
                record["tag"] = tag
                record["round"] = "round-%d" % round_index
                round_cases.append(record)
        executed = len([r for r in round_cases if as_number(r.get("mean_ns")) is not None])
        log_child(children, argv, exit_code, console, len(round_cases), executed)
        if exit_code != 0:
            reasons.append("%s: child exited %r (see %s)" % (tag, exit_code, console.name))
        if not round_cases:
            reasons.append("%s: zero matched cases" % tag)
        records.append(
            {
                "tag": tag,
                "filters": list(filters),
                "argv": argv,
                "exit_code": exit_code,
                "artifacts_dir": str(artifacts_dir),
                "results_dir": str(results_dir),
                "console_log": str(console),
                "reports": [str(p) for p in reports],
                "discovered_case_count": len(round_cases),
                "executed_case_count": executed,
            }
        )
        cases.extend(round_cases)
    fingerprint_after = source_fingerprint(repo)
    conflicts_after = conflicting_processes(processes_snapshot())
    if lock["mode"] != "inherited flock -x":
        reasons.append("outer-lock-not-detected: %s" % CANONICAL_LOCK)
    if conflicts_before:
        reasons.append(
            "overlap before the block: "
            + "; ".join("pid %s %s" % (r["pid"], r["args"][:120]) for r in conflicts_before)
        )
    if conflicts_after:
        reasons.append(
            "overlap after the block: "
            + "; ".join("pid %s %s" % (r["pid"], r["args"][:120]) for r in conflicts_after)
        )
    if fingerprint_before != fingerprint_after:
        reasons.append("source state moved during the block")
    valid = not reasons
    if not valid:
        cases = []  # never record numbers from an invalidated round
    round_record = {
        "round": "round-%d" % round_index,
        "subcommand": sub,
        "valid": valid,
        "reasons": reasons,
        "started_utc": started,
        "finished_utc": utc_now(),
        "duration_s": round(time.monotonic() - clock, 3),
        "fingerprint_before": fingerprint_before,
        "fingerprint_after": fingerprint_after,
        "lock": lock,
        "children": children,
        "invocations": records,
        "cases": cases,
        "discovered_case_count": len(cases),
        "executed_case_count": len([c for c in cases if as_number(c.get("mean_ns")) is not None]),
    }
    write_json(round_dir / "round.json", round_record)
    return round_record


def run_rounds(sub, args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    invocations = PHASE_INVOCATIONS if sub == "phase" else BASELINE_INVOCATIONS
    log_lines = []
    rounds = []
    for index in range(1, int(args.rounds) + 1):
        record = run_round(sub, index, invocations, root, repo, args.timeout)
        rounds.append(record)
        for child in record["children"]:
            log_lines.append(
                "+ %s  -> exit %r (round %s, %d case(s))"
                % (" ".join(child["argv"]), child["exit_code"], record["round"], child["discovered_case_count"])
            )
        log_lines.extend("  round %s INVALID: %s" % (record["round"], r) for r in record["reasons"])
    payload = base_payload(sub, root, repo)
    payload["rounds"] = [
        {
            "round": r["round"],
            "valid": r["valid"],
            "reasons": r["reasons"],
            "discovered_case_count": r["discovered_case_count"],
            "executed_case_count": r["executed_case_count"],
            "round_json": str(Path(root) / sub / r["round"] / "round.json"),
        }
        for r in rounds
    ]
    payload["children"] = [c for r in rounds for c in r["children"]]
    payload["discovered_case_count"] = sum(r["discovered_case_count"] for r in rounds)
    payload["executed_case_count"] = sum(r["executed_case_count"] for r in rounds)
    payload["expected_case_count"] = EXPECTED_PUBLIC_JSON_CASES if sub in ("baseline", "final") else None
    payload["lock"] = rounds[0]["lock"] if rounds else lock_record()
    payload["notice"] = LOCK_NOTICE
    invalid = [r for r in rounds if not r["valid"]]
    if invalid:
        payload["status"] = "invalid"
        payload["reason_code"] = "round-invalid"
        emit(sub, root, payload, log_lines)
        for record in invalid:
            for reason in record["reasons"]:
                print("  INVALID %s: %s" % (record["round"], reason), file=sys.stderr)
        return EXIT_OVERLAP
    if payload["discovered_case_count"] == 0:
        payload["status"] = "input-error"
        payload["reason_code"] = "zero-matched-cases"
        emit(sub, root, payload, log_lines)
        return EXIT_INPUT
    payload["status"] = "pass"
    emit(sub, root, payload, log_lines)
    print("%s: PASS (%d case(s) over %d round(s))" % (sub, payload["discovered_case_count"], len(rounds)))
    return EXIT_OK


def load_rounds(root, sub):
    """Load the valid rounds of one phase directory, keyed by round id."""
    phase_dir = Path(root) / sub
    if not phase_dir.is_dir():
        raise InputError("phase directory does not exist: %s" % phase_dir)
    rounds = {}
    for child in sorted(phase_dir.iterdir()):
        if not child.is_dir() or not re.match(r"^round-\d+$", child.name):
            continue
        round_json = child / "round.json"
        if not round_json.exists():
            raise InputError("missing %s (round validity is never assumed)" % round_json)
        record = read_json(round_json, "round")
        if not isinstance(record, dict) or not isinstance(record.get("valid"), bool):
            raise InputError("malformed %s" % round_json)
        if record["valid"]:
            rounds[child.name] = record
    if not rounds:
        raise InputError("no valid round under %s" % phase_dir)
    return rounds


def load_case_index(record):
    return index_cases(record.get("cases") or [])


# ================================================================================================
# compare (pure computation)
# ================================================================================================
def linear_fit(points):
    """Least-squares fit ``T(rows) = a + b*rows``; returns a, b and the residuals."""
    if len(points) < 2:
        raise InputError("linear fit needs at least 2 points")
    xs = [float(p[0]) for p in points]
    ys = [float(p[1]) for p in points]
    mean_x = statistics.fmean(xs)
    mean_y = statistics.fmean(ys)
    sxx = sum((x - mean_x) ** 2 for x in xs)
    if sxx == 0.0:
        raise InputError("linear fit: degenerate x values")
    sxy = sum((x - mean_x) * (y - mean_y) for x, y in zip(xs, ys))
    b = sxy / sxx
    a = mean_y - b * mean_x
    residuals = [y - (a + b * x) for x, y in zip(xs, ys)]
    return {"a": a, "b": b, "residuals": residuals, "points": [[p[0], p[1]] for p in points]}


def _check(check_id, kind, threshold, per_round, detail=None):
    values = list(per_round.values())
    if all(v["pass"] for v in values):
        verdict = "pass"
    elif not any(v["pass"] for v in values):
        verdict = "fail"
    else:
        verdict = "inconclusive"
    return {
        "id": check_id,
        "kind": kind,
        "threshold": threshold,
        "rounds": per_round,
        "verdict": verdict,
        "detail": detail,
    }


def _ratio_check(index_b, index_f, cls_num, method_num, cls_den, method_den, rows, round_id, threshold):
    where = "round %s" % round_id
    numerator = case_mean(index_f, cls_num, method_num, rows, where)
    denominator = case_mean(index_f, cls_den, method_den, rows, where)
    if denominator <= 0:
        raise InputError("%s: non-positive denominator for Rows=%s" % (where, rows))
    value = numerator / denominator
    return {
        "value": value,
        "threshold": threshold,
        "pass": (value <= threshold) if threshold is not None else None,
        "numerator_ns": numerator,
        "denominator_ns": denominator,
    }


def evaluate_compare(root, repo):
    baseline = load_rounds(root, "baseline")
    final = load_rounds(root, "final")
    phase = load_rounds(root, "phase")
    if set(baseline) != set(final):
        raise InputError(
            "baseline/final round sets differ: %s vs %s" % (sorted(baseline), sorted(final))
        )
    if len(baseline) < 2:
        raise InputError("fewer than 2 paired rounds: %d (the literal thresholds need both)" % len(baseline))
    round_ids = sorted(baseline)

    index_b = {rid: load_case_index(baseline[rid]) for rid in round_ids}
    index_f = {rid: load_case_index(final[rid]) for rid in round_ids}

    checks = []

    # R247-01: same-run ratio and allocation, per round.
    for label, rows, threshold in (
        ("1k", 1000, THRESHOLDS["r247_01_ratio_1k_max"]),
        ("10k", 10000, THRESHOLDS["r247_01_ratio_10k_max"]),
    ):
        per_round = {}
        for rid in round_ids:
            per_round[rid] = _ratio_check(
                index_b[rid], index_f[rid], WRITE_JSON_CLASS, DTO_STREAM_ARM,
                WRITE_JSON_CLASS, LINQ2DB_ARM, rows, rid, threshold,
            )
        checks.append(_check("R247-01-%s-ratio" % label, "ratio", threshold, per_round))

    per_round = {}
    for rid in round_ids:
        allocated = case_alloc(index_f[rid], WRITE_JSON_CLASS, DTO_STREAM_ARM, 10000, "round %s" % rid)
        threshold = THRESHOLDS["r247_01_alloc_10k_max_bytes"]
        per_round[rid] = {
            "value": allocated,
            "threshold": threshold,
            "pass": allocated <= threshold,
        }
    checks.append(_check("R247-01-10k-alloc", "allocation", THRESHOLDS["r247_01_alloc_10k_max_bytes"], per_round))

    # 100k: the final ratio must not exceed its paired pre-change same-round ratio.
    per_round = {}
    for rid in round_ids:
        baseline_ratio = _ratio_check(
            index_b[rid], index_b[rid], WRITE_JSON_CLASS, DTO_STREAM_ARM,
            WRITE_JSON_CLASS, LINQ2DB_ARM, 100000, rid, None,
        )["value"]
        final_ratio = _ratio_check(
            index_b[rid], index_f[rid], WRITE_JSON_CLASS, DTO_STREAM_ARM,
            WRITE_JSON_CLASS, LINQ2DB_ARM, 100000, rid, None,
        )["value"]
        per_round[rid] = {
            "value": final_ratio,
            "paired_baseline_ratio": baseline_ratio,
            "threshold": baseline_ratio,
            "pass": final_ratio <= baseline_ratio,
        }
    checks.append(_check("R247-01-100k-paired-ratio", "paired-ratio", None, per_round))

    # R247-02: byte-equality control outside timing (setup-time marker, never inside the timed region).
    byte_rounds = {}
    for rid in round_ids:
        markers = {}
        for invocation in baseline[rid].get("invocations", []):
            console = Path(invocation.get("console_log", ""))
            if not console.exists():
                continue
            for line in console.read_text(encoding="utf-8", errors="replace").splitlines():
                if "R247_02_BYTE_CONTROL" in line:
                    equal = "equal=true" in line
                    rows_match = _PARAM_ROWS_RE.search(line)
                    markers[rows_match.group(1) if rows_match else "?"] = equal
        present = bool(markers)
        byte_rounds[rid] = {
            "value": markers,
            "threshold": True,
            "pass": present and all(markers.values()),
            "detail": "no R247_02_BYTE_CONTROL marker" if not present else "markers present",
        }
    checks.append(_check("R247-02-byte-control", "byte-control", True, byte_rounds))

    # R247-12: small-row mean and intercept fit (fixed-cost 3Col).
    small_row_rounds = {}
    fit_rounds = {}
    for rid in round_ids:
        b100 = case_mean(index_b[rid], FIXED_COST_CLASS, PUBLIC_TOTAL_ARM, 100, "round %s baseline" % rid)
        f100 = case_mean(index_f[rid], FIXED_COST_CLASS, PUBLIC_TOTAL_ARM, 100, "round %s final" % rid)
        fraction = f100 / b100 if b100 > 0 else math.inf
        small_row_rounds[rid] = {
            "baseline_mean_ns": b100,
            "final_mean_ns": f100,
            "value": fraction,
            "threshold": THRESHOLDS["r247_12_100row_fraction_max"],
            "pass": fraction <= THRESHOLDS["r247_12_100row_fraction_max"],
        }
        b_points = [
            (rows, case_mean(index_b[rid], FIXED_COST_CLASS, PUBLIC_TOTAL_ARM, rows, "round %s baseline" % rid))
            for rows in SMALL_ROW_ROWS
        ]
        f_points = [
            (rows, case_mean(index_f[rid], FIXED_COST_CLASS, PUBLIC_TOTAL_ARM, rows, "round %s final" % rid))
            for rows in SMALL_ROW_ROWS
        ]
        fit_b = linear_fit(b_points)
        fit_f = linear_fit(f_points)
        positive = fit_b["a"] > 0.0 and fit_f["a"] > 0.0
        ratio = fit_f["a"] / fit_b["a"] if fit_b["a"] > 0.0 else math.inf
        passed = positive and ratio <= THRESHOLDS["r247_12_intercept_fraction_max"]
        fit_rounds[rid] = {
            "baseline": fit_b,
            "final": fit_f,
            "both_intercepts_positive": positive,
            "a_ratio": ratio,
            "threshold": THRESHOLDS["r247_12_intercept_fraction_max"],
            "pass": passed,
        }
    checks.append(
        _check("R247-12-100row", "small-row", THRESHOLDS["r247_12_100row_fraction_max"], small_row_rounds)
    )
    checks.append(
        _check("R247-12-intercept", "intercept", THRESHOLDS["r247_12_intercept_fraction_max"], fit_rounds)
    )

    # R247-09: the seven-case acceptance inventory (produced by the `acceptance` subcommand).
    acceptance_path = Path(root) / "acceptance" / "acceptance.json"
    if not acceptance_path.exists():
        raise InputError(
            "missing %s: the R247-09 seven-case acceptance evidence is required" % acceptance_path
        )
    acceptance = read_json(acceptance_path, "acceptance")
    acceptance_rounds = {}
    for entry in acceptance.get("rounds", []):
        rid = entry.get("round")
        cases = entry.get("cases") or []
        count_ok = len(cases) == THRESHOLDS["r247_09_acceptance_cases"]
        ratios_ok = all(c.get("pass") for c in cases) and bool(cases)
        acceptance_rounds[rid] = {
            "case_count": len(cases),
            "value": len(cases),
            "threshold": THRESHOLDS["r247_09_acceptance_cases"],
            "pass": count_ok and ratios_ok,
        }
    if not acceptance_rounds:
        raise InputError("acceptance evidence has no rounds")
    checks.append(
        _check("R247-09-acceptance", "acceptance", THRESHOLDS["r247_09_acceptance_cases"], acceptance_rounds)
    )

    verdicts = [c["verdict"] for c in checks]
    if "fail" in verdicts:
        status, reason = "fail", "gate-fail"
    elif "inconclusive" in verdicts:
        status, reason = "inconclusive", "inconclusive-discordant-rounds"
    else:
        status, reason = "pass", "ok"
    return {
        "status": status,
        "reason_code": reason,
        "rounds": round_ids,
        "checks": checks,
        "small_row": small_row_rounds,
        "intercept_fit": fit_rounds,
        "acceptance": {"path": str(acceptance_path), "rounds": acceptance_rounds},
    }


def cmd_compare(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "compare"
    try:
        result = evaluate_compare(root, repo)
    except InputError as exc:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "input-error", "error": str(exc), "checks": []})
        emit(sub, root, payload, ["input error: %s" % exc])
        print("compare: input error: %s" % exc, file=sys.stderr)
        return EXIT_INPUT
    payload = base_payload(sub, root, repo)
    payload.update(result)
    payload["thresholds"] = dict(THRESHOLDS)
    payload["children"] = []
    payload["discovered_case_count"] = len(result["rounds"])
    payload["executed_case_count"] = len(result["rounds"])
    out_dir = root / sub
    emit(sub, root, payload, [
        "compare: status=%s reason=%s rounds=%s" % (result["status"], result["reason_code"], result["rounds"]),
    ])
    write_json(out_dir / "small-row.json", {"rounds": result["small_row"], "threshold": THRESHOLDS["r247_12_100row_fraction_max"]})
    write_json(out_dir / "intercept-fit.json", {"rounds": result["intercept_fit"], "threshold": THRESHOLDS["r247_12_intercept_fraction_max"], "model": "T(rows)=a+b*rows"})
    write_json(out_dir / "comparison.json", payload)
    for check in result["checks"]:
        if check["verdict"] != "pass":
            print("  %s: %s" % (check["id"], check["verdict"]), file=sys.stderr)
    if result["status"] == "pass":
        print("compare: PASS (%d check(s))" % len(result["checks"]))
        return EXIT_OK
    if result["status"] == "inconclusive":
        print("compare: INCONCLUSIVE (discordant round pair)", file=sys.stderr)
        return EXIT_INCONCLUSIVE
    print("compare: FAIL (%d check(s))" % len(result["checks"]), file=sys.stderr)
    return EXIT_GATE_FAIL


# ================================================================================================
# phases (attribution)
# ================================================================================================
def cmd_phases(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "phases"
    log_lines = []
    try:
        phase = load_rounds(root, "phase")
    except InputError as exc:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "phase-rounds-missing", "error": str(exc)})
        emit(sub, root, payload, ["input error: %s" % exc])
        return EXIT_INPUT
    round_ids = sorted(phase)
    compile_only, steady, public_total, residual = {}, {}, {}, {}
    for rid in round_ids:
        index = load_case_index(phase[rid])
        try:
            compile_only[rid] = case_mean(index, FIXED_COST_CLASS, COMPILE_ONLY_ARM, 100, "round %s" % rid)
            steady[rid] = case_mean(index, FIXED_COST_CLASS, STEADY_WRITER_ARM, 100, "round %s" % rid)
            public_total[rid] = case_mean(index, FIXED_COST_CLASS, PUBLIC_TOTAL_ARM, 100, "round %s" % rid)
        except InputError as exc:
            payload = base_payload(sub, root, repo)
            payload.update({"status": "input-error", "reason_code": "compile-arm-missing", "error": str(exc)})
            emit(sub, root, payload, ["input error: %s" % exc])
            return EXIT_INPUT
        residual[rid] = public_total[rid] - compile_only[rid]
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass",
            "reason_code": "ok",
            "rounds": round_ids,
            "measured_compile": {"arm": COMPILE_ONLY_ARM, "mean_ns": compile_only},
            "steady_writer": {"arm": STEADY_WRITER_ARM, "mean_ns": steady},
            "public_total": {"arm": PUBLIC_TOTAL_ARM, "mean_ns": public_total},
            "unattributed_residual": {
                "formula": "public_total - measured_compile (3Col, Rows=100)",
                "mean_ns": residual,
                "label": (
                    "un-attributed residual: potentially clone/prepare/shape and measurement-boundary "
                    "differences; it is NOT an independently measured preparation phase"
                ),
            },
            "attribution_note": (
                "compile-only (setup-built shape, warm reusable entry compiled once before measurement), "
                "steady writer (precompiled writer driven over a hand-written record) and public total "
                "(ordinary preparation included) are measured separately; the residual is reported as "
                "un-attributed and is never relabelled 'preparation'."
            ),
        }
    )
    payload["discovered_case_count"] = len(round_ids)
    payload["executed_case_count"] = len(round_ids)
    log_lines.append("phases: compile-only %s steady %s total %s residual %s" % (compile_only, steady, public_total, residual))
    emit(sub, root, payload, log_lines)
    write_json(Path(root) / sub / "phase-attribution.json", payload)
    print("phases: PASS (compile-only/steady/public/residual over %d round(s))" % len(round_ids))
    return EXIT_OK


# ================================================================================================
# lowering
# ================================================================================================
def cmd_lowering(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "lowering"
    children, log_lines, projects = [], [], []
    exit_code = EXIT_OK
    for project in LOWERING_PROJECTS:
        argv = ["dotnet", "test", project, "-c", "Debug", "--filter", LOWERING_FILTER]
        console = Path(root) / sub / (Path(project).name + ".log")
        console.parent.mkdir(parents=True, exist_ok=True)
        with open(console, "wb") as log:
            try:
                completed = subprocess.run(argv, cwd=str(repo), stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
                code = completed.returncode
            except (OSError, subprocess.TimeoutExpired) as exc:
                log.write(("\n%s\n" % exc).encode("utf-8"))
                code = None
        text = console.read_text(encoding="utf-8", errors="replace") if console.exists() else ""
        selected = _parse_selected_count(text)
        projects.append({"project": project, "exit_code": code, "selected_count": selected, "log": str(console)})
        log_child(children, argv, code, console, selected, selected)
        log_lines.append("+ %s -> exit %r selected %s" % (" ".join(argv), code, selected))
        if code != 0 or selected == 0:
            exit_code = EXIT_INPUT
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if exit_code == EXIT_OK else "input-error",
            "reason_code": "ok" if exit_code == EXIT_OK else "lowering-missing-or-zero",
            "provider_inventory": projects,
            "filter": LOWERING_FILTER,
        }
    )
    payload["children"] = children
    payload["discovered_case_count"] = sum(p["selected_count"] for p in projects)
    payload["executed_case_count"] = payload["discovered_case_count"]
    emit(sub, root, payload, log_lines)
    print("lowering: %s (%d provider project(s))" % (payload["status"], len(projects)))
    return exit_code


def _parse_selected_count(text):
    for pattern in (
        r"executed benchmarks:\s*(\d+)",
        r"total:\s*\d+\s+failed:\s*\d+\s+succeeded:\s*(\d+)",
        r"Passed!.*?-\s*Failed:\s*\d+,\s*Passed:\s*(\d+)",
        r"Test Run Successful.*?Total tests:\s*(\d+)",
    ):
        match = re.search(pattern, text)
        if match:
            return int(match.group(1))
    return 0


# ================================================================================================
# boundary + coverage
# ================================================================================================
def cmd_boundary(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "boundary"
    if args.scope != "nonintegration":
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "unsupported-scope", "error": args.scope})
        emit(sub, root, payload, ["unsupported --scope %s" % args.scope])
        return EXIT_INPUT
    children, log_lines, totals = [], [], []
    exit_code = EXIT_OK
    coverage_dir = Path(root) / sub / "coverage"
    coverage_dir.mkdir(parents=True, exist_ok=True)
    for project in BOUNDARY_PROJECTS:
        cobertura = coverage_dir / (Path(project).name + ".cobertura.xml")
        inner = " ".join(["dotnet", "test", project, "-c", "Debug"])
        argv = [
            "dotnet", "tool", "run", "dotnet-coverage", "collect",
            "-s", str(MAIN_COVERAGE_SETTINGS), "-f", "cobertura",
            "-o", str(cobertura), inner,
        ]
        console = Path(root) / sub / (Path(project).name + ".log")
        with open(console, "wb") as log:
            try:
                completed = subprocess.run(argv, cwd=str(repo), stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
                code = completed.returncode
            except (OSError, subprocess.TimeoutExpired) as exc:
                log.write(("\n%s\n" % exc).encode("utf-8"))
                code = None
        text = console.read_text(encoding="utf-8", errors="replace") if console.exists() else ""
        selected = _parse_selected_count(text)
        totals.append({"project": project, "exit_code": code, "selected_count": selected, "coverage": str(cobertura), "log": str(console)})
        log_child(children, argv, code, console, selected, selected)
        log_lines.append("+ %s -> exit %r selected %s" % (inner, code, selected))
        if code != 0 or selected == 0:
            exit_code = EXIT_INPUT
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if exit_code == EXIT_OK else "input-error",
            "reason_code": "ok" if exit_code == EXIT_OK else "boundary-failure",
            "scope": args.scope,
            "projects": totals,
            "coverage_dir": str(coverage_dir),
        }
    )
    payload["children"] = children
    payload["discovered_case_count"] = sum(t["selected_count"] for t in totals)
    payload["executed_case_count"] = payload["discovered_case_count"]
    emit(sub, root, payload, log_lines)
    print("boundary: %s (%d project(s), %d case(s))" % (payload["status"], len(totals), payload["discovered_case_count"]))
    return exit_code


def _cobertura_rate(path):
    try:
        tree = ET.parse(path)
    except (OSError, ET.ParseError):
        return None
    root = tree.getroot()
    if root is None:
        return None
    line_rate = root.get("line-rate")
    branch_rate = root.get("branch-rate")
    if line_rate is None or branch_rate is None:
        return None
    return {"line": float(line_rate) * 100.0, "branch": float(branch_rate) * 100.0}


def cmd_coverage(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "coverage"
    boundary = Path(root) / "boundary"
    if not boundary.is_dir():
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "boundary-missing", "error": "run boundary first"})
        emit(sub, root, payload, ["boundary artifacts missing; coverage is collected during the boundary sweep"])
        print("coverage: boundary artifacts missing (run boundary first)", file=sys.stderr)
        return EXIT_INPUT
    coverage_dir = boundary / "coverage"
    reports = sorted(coverage_dir.glob("*.cobertura.xml")) if coverage_dir.is_dir() else []
    if not reports:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "coverage-missing", "error": "no cobertura report under %s" % coverage_dir})
        emit(sub, root, payload, ["no cobertura report produced by the boundary sweep"])
        print("coverage: no cobertura report under %s" % coverage_dir, file=sys.stderr)
        return EXIT_INPUT

    # Canonical merge: reportgenerator over every cobertura collected during the boundary sweep.
    children, log_lines = [], []
    report_dir = Path(root) / sub / "report"
    report_dir.mkdir(parents=True, exist_ok=True)
    argv = [
        "dotnet", "tool", "run", "reportgenerator",
        "-reports:" + ";".join(str(p) for p in reports),
        "-targetdir:" + str(report_dir),
        "-reporttypes:TextSummary",
    ]
    console = Path(root) / sub / "reportgenerator.log"
    with open(console, "wb") as log:
        try:
            completed = subprocess.run(argv, cwd=str(repo), stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
            code = completed.returncode
        except (OSError, subprocess.TimeoutExpired) as exc:
            log.write(("\n%s\n" % exc).encode("utf-8"))
            code = None
    log_child(children, argv, code, console, len(reports), len(reports))
    log_lines.append("+ %s -> exit %r" % (" ".join(argv), code))

    merged_line = merged_branch = None
    summary = report_dir / "Summary.txt"
    if summary.exists():
        text = summary.read_text(encoding="utf-8", errors="replace")
        line_match = re.search(r"Line coverage:\s*([\d.]+)\s*%", text)
        branch_match = re.search(r"Branch coverage:\s*([\d.]+)\s*%", text)
        if line_match and branch_match:
            merged_line = float(line_match.group(1))
            merged_branch = float(branch_match.group(1))

    per_project = []
    for report in reports:
        rates = _cobertura_rate(report)
        if rates is None:
            continue
        per_project.append({"report": str(report), "line": rates["line"], "branch": rates["branch"]})
    if merged_line is None and per_project:
        merged_line = statistics.fmean(p["line"] for p in per_project)
        merged_branch = statistics.fmean(p["branch"] for p in per_project)
    main_pass = (
        merged_line is not None
        and merged_branch is not None
        and merged_line >= THRESHOLDS["main_line_coverage_min"]
        and merged_branch >= THRESHOLDS["main_branch_coverage_min"]
    )
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if main_pass else "input-error",
            "reason_code": "ok" if main_pass else "coverage-missing-or-below",
            "merge": "reportgenerator TextSummary" if summary.exists() else "cobertura root average (fallback)",
            "main_gate": {
                "line": merged_line,
                "branch": merged_branch,
                "line_min": THRESHOLDS["main_line_coverage_min"],
                "branch_min": THRESHOLDS["main_branch_coverage_min"],
                "pass": main_pass,
            },
            "integration_gate": {
                "line_min": THRESHOLDS["integration_line_coverage_min"],
                "branch_min": THRESHOLDS["integration_branch_coverage_min"],
                "known_red_baseline": {
                    "line": THRESHOLDS["integration_baseline_line"],
                    "branch": THRESHOLDS["integration_baseline_branch"],
                },
                "note": (
                    "pre-existing red integration baseline (~74.8% line / ~64.8% branch at 65/75), "
                    "never to be chased green here and never called passing; thresholds are never lowered. "
                    "It is measured by coverage.integration.settings.xml, not by this boundary sweep."
                ),
            },
            "per_project": per_project,
        }
    )
    payload["children"] = children
    emit(sub, root, payload, log_lines)
    print("coverage: %s (line %s, branch %s)" % (payload["status"], merged_line, merged_branch))
    return EXIT_OK if main_pass else EXIT_INPUT


# ================================================================================================
# fault-checks
# ================================================================================================
def cmd_fault_checks(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "fault-checks"
    spec_path = Path(root) / sub / "faults.json"
    if not spec_path.exists():
        payload = base_payload(sub, root, repo)
        payload.update(
            {
                "status": "input-error",
                "reason_code": "fault-specs-not-pinned",
                "error": "no %s" % spec_path,
                "instruction": (
                    "pin exactly three fault specs (id/file/find/replace/test_argv) at D247-03/D247-04; "
                    "each fault must be killed and the tests must pass after restoration"
                ),
            }
        )
        emit(sub, root, payload, ["fault specs are not pinned; refusing to fabricate a kill"])
        print("fault-checks: fault specs not pinned at %s" % spec_path, file=sys.stderr)
        return EXIT_PREREQ
    spec = read_json(spec_path, "fault specs")
    faults = spec.get("faults") if isinstance(spec, dict) else None
    if not isinstance(faults, list) or len(faults) != 3:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "fault-count", "error": "expected exactly 3 faults"})
        emit(sub, root, payload, ["fault spec must declare exactly three faults"])
        return EXIT_INPUT
    results, children, log_lines, ok = [], [], [], True
    for fault in faults:
        file_path = repo / fault["file"]
        original = file_path.read_text(encoding="utf-8")
        original_sha = sha256_text(original)
        seeded = original.replace(fault["find"], fault["replace"], 1)
        if seeded == original:
            ok = False
            results.append({"id": fault.get("id"), "killed": False, "restored": False, "error": "find not present"})
            continue
        file_path.write_text(seeded, encoding="utf-8", newline="")
        killed_code = _run_test_argv(fault["test_argv"], repo, root / sub, "kill-%s" % fault.get("id"), args.timeout, children, log_lines)
        file_path.write_text(original, encoding="utf-8", newline="")
        restored_sha = sha256_text(file_path.read_text(encoding="utf-8"))
        restore_code = _run_test_argv(fault["test_argv"], repo, root / sub, "restore-%s" % fault.get("id"), args.timeout, children, log_lines)
        killed = killed_code not in (0, None)
        restored = restore_code == 0 and restored_sha == original_sha
        ok = ok and killed and restored
        results.append({"id": fault.get("id"), "killed": killed, "restored": restored, "original_sha256": original_sha})
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if ok else "input-error",
            "reason_code": "ok" if ok else "fault-not-killed",
            "faults": results,
            "no_stryker_statement": "fault seeding only; no Stryker.NET mutation run in this unit",
        }
    )
    payload["children"] = children
    emit(sub, root, payload, log_lines)
    print("fault-checks: %s (%d fault(s))" % (payload["status"], len(results)))
    return EXIT_OK if ok else EXIT_INPUT


def _run_test_argv(argv, repo, out_dir, tag, timeout, children, log_lines):
    console = Path(out_dir) / ("%s.log" % tag)
    console.parent.mkdir(parents=True, exist_ok=True)
    with open(console, "wb") as log:
        try:
            completed = subprocess.run([str(a) for a in argv], cwd=str(repo), stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
            code = completed.returncode
        except (OSError, subprocess.TimeoutExpired) as exc:
            log.write(("\n%s\n" % exc).encode("utf-8"))
            code = None
    text = console.read_text(encoding="utf-8", errors="replace") if console.exists() else ""
    selected = _parse_selected_count(text)
    log_child(children, argv, code, console, selected, selected)
    log_lines.append("+ %s -> exit %r" % (" ".join(str(a) for a in argv), code))
    return code


# ================================================================================================
# gate
# ================================================================================================
def cmd_gate(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "gate"
    baseline_dir = Path(args.baseline)
    final_dir = Path(args.final)
    log_lines, children = [], []
    if not args.baseline or not args.final:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "missing-baseline-or-final", "error": "--baseline and --final are required"})
        emit(sub, root, payload, ["--baseline and --final are required"])
        return EXIT_PREREQ
    final_rounds = sorted([p for p in final_dir.iterdir() if p.is_dir() and re.match(r"^round-\d+$", p.name)]) if final_dir.is_dir() else []
    if not final_rounds:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "final-rounds-missing", "error": str(final_dir)})
        emit(sub, root, payload, ["no final rounds"])
        print("gate: no final round directories under %s" % final_dir, file=sys.stderr)
        return EXIT_INPUT
    reports_dir = final_rounds[-1] / "fixed-cost" / "results"
    budgets = read_json(ITERATION14_BUDGETS, "iteration14 budgets")
    row_ids = {row.get("id") for row in budgets.get("rows", [])}
    missing_rows = [rid for rid in T247_BUDGET_ROW_IDS if rid not in row_ids]
    argv = ["python3", str(ITERATION14_GATE), "--manifest", str(ITERATION14_BUDGETS), "--reports-dir", str(reports_dir)]
    console = Path(root) / sub / "iteration14_gate.log"
    console.parent.mkdir(parents=True, exist_ok=True)
    with open(console, "wb") as log:
        try:
            completed = subprocess.run(argv, cwd=str(repo), stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
            code = completed.returncode
        except (OSError, subprocess.TimeoutExpired) as exc:
            log.write(("\n%s\n" % exc).encode("utf-8"))
            code = None
    log_child(children, argv, code, console, 0, 0)
    log_lines.append("+ %s -> exit %r" % (" ".join(argv), code))
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "baseline": str(baseline_dir),
            "final": str(final_dir),
            "reports_dir": str(reports_dir),
            "iteration14_gate": {"argv": argv, "exit_code": code, "log": str(console)},
            "t247_budget_rows": {
                "required_ids": list(T247_BUDGET_ROW_IDS),
                "missing_ids": missing_rows,
                "note": (
                    "T247 is the third gate owner and must ADD 3 rows to iteration14-budgets.json without "
                    "relaxing existing rows. The numeric budgets are measurement-derived and are added at the "
                    "freeze step (D247-04), not invented in this pre-change unit."
                ),
            },
        }
    )
    payload["children"] = children
    if missing_rows:
        payload.update({"status": "input-error", "reason_code": "t247-budget-rows-missing", "missing": missing_rows})
        emit(sub, root, payload, log_lines)
        print("gate: missing T247 budget rows %s" % missing_rows, file=sys.stderr)
        return EXIT_PREREQ
    if code != 0:
        payload.update({"status": "fail", "reason_code": "iteration14-gate-failed"})
        emit(sub, root, payload, log_lines)
        return EXIT_GATE_FAIL
    payload.update({"status": "pass", "reason_code": "ok"})
    emit(sub, root, payload, log_lines)
    print("gate: PASS (iteration14 + three-bucket JSON gate)")
    return EXIT_OK


# ================================================================================================
# acceptance
# ================================================================================================
def _acceptance_run(repo, root, round_index, timeout):
    round_dir = Path(root) / "acceptance" / ("round-%d" % round_index)
    artifacts = round_dir / "artifacts"
    artifacts.mkdir(parents=True, exist_ok=True)
    argv = [
        "dotnet", "run", "--project", BENCH_PROJECT, "-c", "Release", "--",
        "--filter", *ACCEPTANCE_FILTERS,
        "--anyCategories=acceptance",
        "--exporters", "json",
        "--artifacts", str(artifacts),
    ]
    console = round_dir / "console.log"
    env = child_environment()
    clock = time.monotonic()
    with open(console, "wb") as log:
        try:
            completed = subprocess.run(argv, cwd=str(repo), env=env, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
            code = completed.returncode
        except (OSError, subprocess.TimeoutExpired) as exc:
            log.write(("\n%s\n" % exc).encode("utf-8"))
            code = None
    wall = time.monotonic() - clock
    cases = {}
    for report in discover_bdn_reports(artifacts / "results"):
        for record in parse_bdn_json(report):
            cases[(record["short_type"], record["method"])] = record
    return {"round": "round-%d" % round_index, "argv": argv, "exit_code": code, "wall_s": wall, "console_log": str(console), "cases": cases}


def cmd_acceptance(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "acceptance"
    baseline_root = Path(args.baseline)
    children, log_lines = [], []
    if not args.baseline:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "missing-baseline", "error": "--baseline is required"})
        emit(sub, root, payload, ["--baseline is required (the paired baseline acceptance run)"])
        return EXIT_PREREQ
    rounds, all_ok = [], True
    for index in range(1, 3):
        run = _acceptance_run(repo, root, index, args.timeout)
        log_child(children, run["argv"], run["exit_code"], run["console_log"], len(run["cases"]), len(run["cases"]))
        log_lines.append("+ %s -> exit %r wall %.1fs cases %d" % (" ".join(run["argv"]), run["exit_code"], run["wall_s"], len(run["cases"])))
        baseline_round_json = baseline_root / ("round-%d" % index) / "acceptance" / "cases.json"
        baseline_cases = read_json(baseline_round_json, "baseline acceptance cases") if baseline_round_json.exists() else {}
        case_rows, round_ok = [], True
        if len(run["cases"]) != THRESHOLDS["r247_09_acceptance_cases"]:
            round_ok = False
        if run["wall_s"] > THRESHOLDS["r247_09_acceptance_max_wall_s"]:
            round_ok = False
        for cls, method in ACCEPTANCE_CASES:
            record = run["cases"].get((cls, method))
            if record is None or record.get("state") == "Failure":
                round_ok = False
                case_rows.append({"class": cls, "method": method, "present": False, "pass": False})
                continue
            baseline_mean = baseline_cases.get("%s.%s" % (cls, method))
            if baseline_mean is None:
                round_ok = False
                case_rows.append({"class": cls, "method": method, "present": True, "baseline_mean_ns": None, "pass": False})
                continue
            ratio = record["mean_ns"] / baseline_mean if baseline_mean else math.inf
            passed = ratio <= THRESHOLDS["r247_09_acceptance_case_ratio_max"]
            round_ok = round_ok and passed
            case_rows.append(
                {
                    "class": cls,
                    "method": method,
                    "present": True,
                    "mean_ns": record["mean_ns"],
                    "baseline_mean_ns": baseline_mean,
                    "ratio": ratio,
                    "threshold": THRESHOLDS["r247_09_acceptance_case_ratio_max"],
                    "pass": passed,
                }
            )
        rounds.append(
            {
                "round": run["round"],
                "case_count": len(run["cases"]),
                "wall_s": run["wall_s"],
                "cases": case_rows,
                "pass": round_ok,
                "baseline_source": str(baseline_round_json),
            }
        )
        all_ok = all_ok and round_ok
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if all_ok else "fail",
            "reason_code": "ok" if all_ok else "acceptance-failed",
            "rounds": rounds,
            "documented_case_count": THRESHOLDS["r247_09_acceptance_cases"],
            "documented_cases": ["%s.%s" % (c, m) for c, m in ACCEPTANCE_CASES],
            "acceptance_category_extra_matches": ACCEPTANCE_CATEGORY_EXTRA_MATCHES,
            "category_count_note": (
                "the code has 8 acceptance-category matches while the docs pin 7; the documented seven are "
                "selected by the explicit case list and the eighth is recorded as a fact, never silently "
                "dropped or counted as an eighth acceptance case"
            ),
            "max_wall_s": THRESHOLDS["r247_09_acceptance_max_wall_s"],
            "case_ratio_max": THRESHOLDS["r247_09_acceptance_case_ratio_max"],
        }
    )
    payload["children"] = children
    payload["discovered_case_count"] = sum(r["case_count"] for r in rounds)
    payload["executed_case_count"] = payload["discovered_case_count"]
    emit(sub, root, payload, log_lines)
    write_json(Path(root) / sub / "acceptance.json", payload)
    print("acceptance: %s (%d round(s), %d case(s))" % (payload["status"], len(rounds), payload["discovered_case_count"]))
    return EXIT_OK if all_ok else EXIT_GATE_FAIL


# ================================================================================================
# audit
# ================================================================================================
_PUBLIC_DECL_RE = re.compile(r"^\s*(?:public|protected)\s+(?!override\b)(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|virtual\s+|async\s+)*[\w<>,\[\]\?\.\s]+\s+(\w+)\s*[\(<]")


def _public_surface(text):
    surface = set()
    for line in text.splitlines():
        match = _PUBLIC_DECL_RE.match(line)
        if match:
            surface.add(match.group(1))
    return surface


def cmd_audit(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "audit"
    base = args.base
    if not base:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "missing-base", "error": "--base is required"})
        emit(sub, root, payload, ["--base is required"])
        return EXIT_PREREQ
    code, output = git_capture(["diff", "--name-only", base, "--", "src"], repo)
    if code != 0:
        payload = base_payload(sub, root, repo)
        payload.update({"status": "input-error", "reason_code": "git-diff-failed", "error": output})
        emit(sub, root, payload, ["git diff failed: %s" % output])
        return EXIT_INPUT
    changed = [line.strip() for line in output.splitlines() if line.strip()]
    public_delta, crlf_violations, suppressions, skips = [], [], 0, 0
    for rel in changed:
        before = git_capture(["show", "%s:%s" % (base, rel)], repo)[1]
        after_path = repo / rel
        after = after_path.read_text(encoding="utf-8", errors="replace") if after_path.exists() else ""
        added = _public_surface(after) - _public_surface(before)
        removed = _public_surface(before) - _public_surface(after)
        if added or removed:
            public_delta.append({"file": rel, "added": sorted(added), "removed": sorted(removed)})
    code_d, diff_text = git_capture(["diff", base, "--unified=0"], repo)
    for line in diff_text.splitlines():
        if line.startswith("+") and not line.startswith("+++"):
            if "SuppressMessage" in line or "#pragma warning disable" in line or "NoWarn" in line:
                suppressions += 1
            if "Skip=" in line or "Assert.Skip" in line:
                skips += 1
    code_n, names = git_capture(["diff", "--name-only", base], repo)
    for rel in [n.strip() for n in names.splitlines() if n.strip()]:
        if _is_measurement_output(rel):
            continue  # generated BDN reports / build artifacts / evidence are not sources
        path = repo / rel
        if not path.is_file():
            continue
        data = path.read_bytes()
        if re.search(rb"(?<!\r)\n", data):
            crlf_violations.append(rel)
    ok = not public_delta and not crlf_violations and suppressions == 0 and skips == 0
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if ok else "fail",
            "reason_code": "ok" if ok else "audit-violation",
            "base": base,
            "changed_src_files": changed,
            "public_api_delta": public_delta,
            "crlf_violations": crlf_violations,
            "new_suppressions": suppressions,
            "new_skips": skips,
            "footprint": {"changed_files": len([n for n in names.splitlines() if n.strip()])},
        }
    )
    emit(sub, root, payload, ["audit: public_delta=%d crlf=%d suppressions=%d skips=%d" % (len(public_delta), len(crlf_violations), suppressions, skips)])
    print("audit: %s" % payload["status"])
    return EXIT_OK if ok else EXIT_GATE_FAIL


# ================================================================================================
# docs
# ================================================================================================
def cmd_docs(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "docs"
    violations = []
    if not PUBLIC_REPORT.exists():
        violations.append("missing report %s" % PUBLIC_REPORT.relative_to(repo))
    registry = repo / "docs" / "specs" / "performance" / "feature-benchmark-followups-2026-10-17.md"
    if not registry.exists() or "#247" not in registry.read_text(encoding="utf-8", errors="replace"):
        violations.append("registry has no #247 row")
    en_pages = [p for p in (repo / "docs" / "comparisons").glob("feature-benchmarks-*.md")]
    ru_pages = [p for p in (repo / "docs" / "ru" / "comparisons").glob("feature-benchmarks-*.md")]
    if not en_pages or not ru_pages:
        violations.append("EN/RU comparison pages missing")
    public_pages = [
        p for p in (repo / "docs").rglob("*.md")
        if not str(p.relative_to(repo)).replace("\\", "/").startswith("docs/specs/")
    ]
    if (repo / "readme.md").exists():
        public_pages.append(repo / "readme.md")
    for page in public_pages:
        text = page.read_text(encoding="utf-8", errors="replace")
        if re.search(r"\]\([^)]*specs/", text):
            violations.append("public page links to internal specs: %s" % page.relative_to(repo))
    ok = not violations
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if ok else "fail",
            "reason_code": "ok" if ok else "docs-violation",
            "violations": violations,
            "en_pages": [str(p.relative_to(repo)) for p in en_pages],
            "ru_pages": [str(p.relative_to(repo)) for p in ru_pages],
        }
    )
    emit(sub, root, payload, ["docs: %s" % ("ok" if ok else violations)])
    print("docs: %s" % payload["status"])
    return EXIT_OK if ok else EXIT_GATE_FAIL


# ================================================================================================
# preflight
# ================================================================================================
def cmd_preflight(args):
    root = Path(args.evidence_root)
    repo = Path(args.repo).resolve()
    sub = "preflight"
    tools, unresolved = {}, []
    for name, argv in (
        ("dotnet", ["dotnet", "--version"]),
        ("python3", ["python3", "--version"]),
        ("git", ["git", "--version"]),
        ("flock", ["flock", "--version"]),
    ):
        code, output = run_capture(argv, cwd=repo)
        tools[name] = {"argv": argv, "exit_code": code, "output": output.strip()[:200]}
        if code != 0:
            unresolved.append("cli-missing:%s" % name)
    db = {"path": str(SEED_DB), "exists": SEED_DB.exists(), "sha256": sha256_file(SEED_DB) if SEED_DB.exists() else None}
    if not db["exists"]:
        unresolved.append("db-missing:%s" % SEED_DB)
    lease_dir = Path("/tmp/opencode")
    leases = sorted(str(p) for p in lease_dir.glob("*.lock")) if lease_dir.is_dir() else []
    payload = base_payload(sub, root, repo)
    payload.update(
        {
            "status": "pass" if not unresolved else "input-error",
            "reason_code": "ok" if not unresolved else "prerequisites-unresolved",
            "tools": tools,
            "database": db,
            "artifact_lease_inventory": {
                "lock_dir": str(lease_dir),
                "leases": leases,
                "canonical_lock": str(CANONICAL_LOCK),
                "plan_lock_name": str(PLAN_LOCK_NAME),
            },
            "unresolved": unresolved,
            "notice": LOCK_NOTICE,
        }
    )
    emit(sub, root, payload, ["preflight: %s" % ("ok" if not unresolved else unresolved)])
    print("preflight: %s" % payload["status"])
    return EXIT_OK if not unresolved else EXIT_PREREQ


# ================================================================================================
# CLI
# ================================================================================================
ALIASES = {"B": "baseline", "P": "phase", "F": "final", "C": "compare"}


def build_parser():
    parser = argparse.ArgumentParser(
        prog="t247_json_gate.py",
        description="T247 (#247) JSON streaming performance gate driver.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "subcommand",
        choices=(
            "preflight", "baseline", "phase", "final", "compare", "lowering", "boundary",
            "coverage", "fault-checks", "gate", "acceptance", "audit", "docs", "phases",
            "B", "P", "F", "C",
        ),
    )
    parser.add_argument("--evidence-root", default=str(DEFAULT_EVIDENCE_ROOT))
    parser.add_argument("--output", default=None)
    parser.add_argument("--repo", default=str(REPO_ROOT))
    parser.add_argument("--rounds", type=int, default=2)
    parser.add_argument("--scope", default="nonintegration")
    parser.add_argument("--baseline", default=None)
    parser.add_argument("--final", default=None)
    parser.add_argument("--base", default=None)
    parser.add_argument("--timeout", type=int, default=3600)
    return parser


def main(argv=None):
    args = build_parser().parse_args(argv)
    sub = ALIASES.get(args.subcommand, args.subcommand)
    if args.output:
        args.evidence_root = args.output
    try:
        if sub == "preflight":
            return cmd_preflight(args)
        if sub in ("baseline", "phase", "final"):
            return run_rounds(sub, args)
        if sub == "compare":
            return cmd_compare(args)
        if sub == "phases":
            return cmd_phases(args)
        if sub == "lowering":
            return cmd_lowering(args)
        if sub == "boundary":
            return cmd_boundary(args)
        if sub == "coverage":
            return cmd_coverage(args)
        if sub == "fault-checks":
            return cmd_fault_checks(args)
        if sub == "gate":
            return cmd_gate(args)
        if sub == "acceptance":
            return cmd_acceptance(args)
        if sub == "audit":
            return cmd_audit(args)
        if sub == "docs":
            return cmd_docs(args)
    except PrereqError as exc:
        print("%s: prerequisite unresolved: %s" % (sub, exc), file=sys.stderr)
        return EXIT_PREREQ
    except InputError as exc:
        print("%s: input error: %s" % (sub, exc), file=sys.stderr)
        return EXIT_INPUT
    print("unknown subcommand %s" % sub, file=sys.stderr)
    return EXIT_INPUT


if __name__ == "__main__":
    sys.exit(main())
