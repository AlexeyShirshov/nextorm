#!/usr/bin/env python3
""".pdca/scripts/perf/t244_evidence.py — T244 (#244) warm-CTE measurement evidence harness.

Scope
-----
This is a *measurement-only* tool (no product code, no benchmark edits).  It keeps the
raw BenchmarkDotNet (BDN) data, records the exact invocations, and computes the
acceptance evidence required by ``docs/specs/status/D244-warm-cte-1.md`` §3/§4/§7/§8/§14:

``measure``   runs the three pinned invocations of one round under the canonical host
              lock ``/tmp/opencode/nextorm-bdn-host.lock``, inventories
              ``benchmarks/BenchmarkDotNet.Artifacts`` before/after each invocation and
              stages **only that invocation's new outputs** into
              ``<output>/<n>-<tag>/``.  Writes ``provenance.json``,
              ``round-validity.json`` and ``commands.log``.  A round is invalidated
              (exit 2, everything still recorded) for wrong source/build/DB hash, mixed
              artifacts, external overlap, wrong environment, a missing job/reference or
              malformed output — never for a merely unfavorable valid result.
``evaluate``  pairs same scenario/class/job/round NextORM and linq2db arms, applies the
              per-class logical-operation divisor (FairCached /10, WarmDecompose /100)
              and the tier (final <= 1.00, checkpoint <= 1.20), checks the prepared
              ``A_*`` controls (<= 1.03x), the zero-byte ``*_Warm_Reused`` rows and the
              +20 % warm-timing band.  Emits ``evaluation.json`` + ``ratios.csv``.
              Exit 0 = requested tier fully passed, 2 = numeric requirement unmet,
              1 = malformed/missing evidence.
``attribute`` decomposes one WarmDecompose round per CTE family into
              Construct / Prepare_NoHash / Prepare_Hash / Warm_PlanOnly / Warm_Reused
              (PlanKey) / RawHit / end-to-end, ranks the eligible non-frozen components
              and checks the unattributed residual bound.  Emits ``attribution.json`` +
              ``attribution.md``.
``docs-check`` verifies that the §9 docs mapping is consistent with the measured
              acceptance tier (no final/success claim for a checkpoint).
``invariants`` hashes the frozen regions (``CteHoister.cs``, frozen recipe /
              ``CaptureRawShape`` file, ``QueryPlanner.cs``) and the owner WIP markers
              declared by the execution map, and fails closed on any change.

Divisors are carried per benchmark type in ``CLASS_DIVISORS`` and are never applied
indiscriminately.  ``WarmDecompose`` divides the exported bytes by 100
(``SqliteBenchmarkWarmDecompose.cs`` loops ``Iterations = 100``); both Fair classes
divide by 10 (``Iterations = 10``).  The divisor is additionally pinned by a checked-in
source contract (class ``Iterations`` constant and no ``OperationsPerInvoke``) — an
unverified logical-operation count is a hard rejection, never an assumption.

The module is stdlib-only and unittest-friendly: every command is exposed as
``main(argv) -> int`` and the parsers/counters are pure functions over JSON paths.
"""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import json
import math
import os
import platform
import re
import shlex
import shutil
import statistics
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

# ------------------------------------------------------------------------------------------------
# Pinned contract
# ------------------------------------------------------------------------------------------------
SCENARIOS = ("Cte", "RecursiveCte", "Except", "Intersect", "Distinct")
CTE_SCENARIOS = ("Cte", "RecursiveCte")
FAMILIES = ("Cte", "RecursiveCte")

FAIR_CACHED_TYPE = "SqliteBenchmarkFeaturesFairCached"
FAIR_TYPE = "SqliteBenchmarkFeaturesFair"
WARM_TYPE = "SqliteBenchmarkWarmDecompose"

#: Per-class logical operations per BDN invocation.  Never apply one divisor globally.
CLASS_DIVISORS = {
    FAIR_CACHED_TYPE: 10,
    FAIR_TYPE: 10,
    WARM_TYPE: 100,
}

NEXTORM_CACHED_PREFIX = "B_Nextorm_Cached_"
LINQ_PREFIX = "B_Linq2Db_"
PREPARED_PREFIX = "A_Nextorm_Prepared_"

FINAL_RATIO = 1.00
CHECKPOINT_RATIO = 1.20
PREPARED_TOLERANCE = 1.03
WARM_TIME_TOLERANCE = 1.20
WARM_REUSED_BYTES = 0
EXPECTED_OPERATIONS_PER_INVOKE = 1

RESIDUAL_ABS_BYTES = 128.0
RESIDUAL_FRACTION = 0.10
ELIGIBLE_ABS_BYTES = 64.0
ELIGIBLE_FRACTION = 0.10
NEGATIVE_RESIDUAL_FRACTION = 0.02
NEGATIVE_RESIDUAL_ABS_BYTES = 16.0

#: Components that may not be optimized in T244 (frozen recipe / frozen preparation).
FROZEN_COMPONENTS = ("RawHit", "Prepare_NoHash_Delta", "Prepare_Hash_Delta")
#: Non-frozen candidate components and the D-unit that owns each.
CANDIDATE_COMPONENTS = {"Construct": "D244-02", "PlanKey": "D244-03"}

LOCK_PATH = Path("/tmp/opencode/nextorm-bdn-host.lock")
ARTIFACTS_REL = Path("benchmarks/BenchmarkDotNet.Artifacts")
BENCH_PROJECT_REL = Path("benchmarks/nextorm.benchmark")
DEFAULT_REPO = Path(__file__).resolve().parents[3]
DEFAULT_MIN_ROUNDS = 2

INVOCATIONS = (
    {
        "tag": "faircached",
        "expected_class": FAIR_CACHED_TYPE,
        "filter": "*SqliteBenchmarkFeaturesFairCached.B_*",
    },
    {
        "tag": "fair-prepared",
        "expected_class": FAIR_TYPE,
        "filter": "*SqliteBenchmarkFeaturesFair.A_*",
    },
    {
        "tag": "warm-decompose",
        "expected_class": WARM_TYPE,
        "filter": "*SqliteBenchmarkWarmDecompose*",
    },
)

WARM_METHODS = (
    "Construct",
    "Prepare_NoHash",
    "Prepare_Hash",
    "Warm_PlanOnly",
    "Warm_Reused",
)

#: §9 documents that must stay consistent with the measured tier.  The token is the
#: minimum content the mapping requires so a missing/renamed page cannot pass silently.
DOC_MAPPING = (
    ("docs/comparisons/feature-benchmarks-2026-10-17.md", "cte"),
    ("docs/comparisons/benchmarks.md", "cte"),
    ("docs/specs/performance/acceptance-benchmarks.md", "cte"),
    ("docs/specs/performance/benchmark-report.md", "cte"),
    ("docs/specs/status/D244-warm-cte-1.md", "d244"),
)

#: A final/success closure claim about T244/#244.  Negated occurrences
#: ("must not mark T244 ... done") are not claims and are ignored.
FINAL_CLAIM_PATTERNS = (
    re.compile(r"\bfinal target(?:\s+is)?\s+(?:met|reached|achieved)\b", re.I),
    re.compile(r"\bfinal\s*[:=]\s*(?:pass|passed|success|achieved|met)\b", re.I),
    re.compile(r"\bT244\b[^\n]{0,80}?\b(?:done|completed?|success(?:ful)?)\b", re.I),
    re.compile(r"#244\b[^\n]{0,80}?\b(?:done|completed?|success(?:ful)?)\b", re.I),
    re.compile(r"\bacceptance tier\s*[:=]\s*`?final\b", re.I),
    re.compile(r"\btier\s*[:=]\s*`?final\b", re.I),
)
CHECKPOINT_CLAIM_PATTERNS = (
    re.compile(r"\bacceptance tier\s*[:=]\s*`?checkpoint\b", re.I),
    re.compile(r"\btier\s*[:=]\s*`?checkpoint\b", re.I),
)
_NEGATION_RE = re.compile(r"\b(?:not|never|no|cannot|must\s+not|isn't|is\s+not)\b", re.I)

#: File descriptors this process opened for the measurement lock (never "inherited").
_OWN_LOCK_FDS: set = set()

#: Frozen regions that must be declared by the execution map (§10 forbidden edits).
MANDATORY_FROZEN = (
    "src/nextorm.core/Builders/CteHoister.cs",
    "src/nextorm.core/DataContext/QueryPlanner.cs",
    "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
)
#: Owner WIP markers that must survive (957fcfe7).
MANDATORY_WIP_MARKERS = ("HasEverPrepared", "ShapeValuesStable")

EXIT_OK = 0
EXIT_MALFORMED = 1
EXIT_NUMERIC = 2

_TOOLCHAIN_RE = re.compile(r"Toolchain=([A-Za-z0-9_.]+)")
_JOB_SEGMENT_RE = re.compile(r"^\s*([A-Za-z][A-Za-z0-9]*)\s*\(")
_CLASS_RE = re.compile(r"\bclass\s+([A-Za-z_]\w*)")
_ITERATIONS_CONST_RE = re.compile(r"\bconst\s+int\s+Iterations\s*=\s*(\d+)\s*;")
_BENCHMARK_ATTR_RE = re.compile(r"\[Benchmark\b[^\]]*\]")
_OPERATIONS_PER_INVOKE_RE = re.compile(r"OperationsPerInvoke\s*=\s*([^,)\]]+)")
_PUBLIC_METHOD_RE = re.compile(
    r"\s*(?:\[[^\]]*\]\s*)*public\s+[^\n;{=]*?\b(?P<name>[A-Za-z_]\w*)\s*\("
)


class Reject(Exception):
    """Malformed/missing evidence, or a refused measurement (never a fabricated pass)."""


class MeasureRefused(Reject):
    """The round may not start (lock busy, conflicting workload, missing prerequisites)."""


# ------------------------------------------------------------------------------------------------
# Small utilities
# ------------------------------------------------------------------------------------------------
def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def is_number(value) -> bool:
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and math.isfinite(float(value))
    )


def is_integral(value) -> bool:
    if isinstance(value, bool):
        return False
    if isinstance(value, int):
        return True
    return isinstance(value, float) and value.is_integer()


def as_bytes(value):
    """Return *value* as an integral byte count, or ``None`` (NA/null/negative)."""
    if not is_integral(value):
        return None
    number = int(value)
    return number if number >= 0 else None


def sha256_file(path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def write_json(path, payload) -> Path:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8", newline="\r\n")
    return path


def read_json(path, label="JSON"):
    path = Path(path)
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise Reject(f"cannot read {label} {path}: {exc}") from exc


def run_capture(argv, cwd=None, timeout=180):
    try:
        completed = subprocess.run(
            argv,
            cwd=str(cwd) if cwd is not None else None,
            capture_output=True,
            text=True,
            timeout=timeout,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        return None, str(exc)
    return completed.returncode, (completed.stdout or "").strip()


# ------------------------------------------------------------------------------------------------
# BDN report parsing
# ------------------------------------------------------------------------------------------------
def job_identity(display: str) -> dict:
    """Split a BDN ``DisplayInfo`` into job name + toolchain identity.

    ``"T.M: ShortRun(Toolchain=InProcessEmitToolchain, ...)"`` -> job ``ShortRun``,
    toolchain ``InProcessEmitToolchain``, identity ``ShortRun/InProcessEmitToolchain``.
    A missing toolchain segment is the default (out-of-process) toolchain.
    """
    display = display or ""
    segment = display.split(": ", 1)[1] if ": " in display else ""
    match = _JOB_SEGMENT_RE.match(segment)
    job = match.group(1) if match else ""
    toolchain_match = _TOOLCHAIN_RE.search(segment)
    toolchain = toolchain_match.group(1) if toolchain_match else "Default"
    return {
        "job": job,
        "toolchain": toolchain,
        "identity": f"{job}/{toolchain}" if job else f"?/{toolchain}",
    }


def parse_report(path) -> list:
    """Parse one BDN JSON export into normalised records."""
    data = read_json(path, "BDN report")
    benchmarks = data.get("Benchmarks") if isinstance(data, dict) else None
    if not isinstance(benchmarks, list):
        raise Reject(f"BDN report {path} has no Benchmarks array")
    records = []
    for entry in benchmarks:
        if not isinstance(entry, dict):
            raise Reject(f"BDN report {path} has a non-object benchmark entry")
        short_type = entry.get("Type")
        method = entry.get("Method")
        if not short_type or not method:
            raise Reject(f"BDN report {path} has an entry without Type/Method")
        namespace = entry.get("Namespace") or ""
        memory = entry.get("Memory") if isinstance(entry.get("Memory"), dict) else {}
        statistics_ = (
            entry.get("Statistics") if isinstance(entry.get("Statistics"), dict) else {}
        )
        display = entry.get("DisplayInfo") or ""
        identity = job_identity(display)
        records.append(
            {
                "type": f"{namespace}.{short_type}" if namespace else short_type,
                "short_type": short_type,
                "method": method,
                "display": display,
                "job": identity["job"],
                "toolchain": identity["toolchain"],
                "identity": identity["identity"],
                "allocated": memory.get("BytesAllocatedPerOperation"),
                "mean": statistics_.get("Mean"),
                "opi": entry.get("OperationsPerInvoke"),
                "source": str(path),
            }
        )
    return records


def discover_reports(path) -> list:
    """Find BDN JSON exports under *path*, de-duplicating compressed/plain pairs."""
    path = Path(path)
    if path.is_file():
        return [path]
    if not path.is_dir():
        raise Reject(f"evidence path does not exist: {path}")
    compressed = sorted(path.rglob("*-report-full-compressed.json"))
    plain = sorted(path.rglob("*-report-full.json"))
    reports, seen = [], set()
    for candidate in compressed + plain:
        stem = candidate.name.split("-report-")[0]
        if stem in seen:
            continue
        seen.add(stem)
        reports.append(candidate)
    if not reports:
        reports = sorted(path.rglob("*-report.json"))
    return reports


def load_records(paths) -> list:
    records = []
    for report in paths:
        records.extend(parse_report(report))
    return records


def require_short_run(record, label=""):
    if record["job"] != "ShortRun":
        raise Reject(
            f"missing/wrong job for {label or record['method']}: "
            f"job={record['job']!r} (the pinned invocation is --job short)"
        )


def require_operation_count(record):
    opi = record.get("opi")
    if opi is None:
        return
    if not is_integral(opi) or int(opi) != EXPECTED_OPERATIONS_PER_INVOKE:
        raise Reject(
            f"unexpected OperationsPerInvoke={opi!r} for {record['method']} "
            f"({record['source']}); the pinned contract is "
            f"OperationsPerInvoke={EXPECTED_OPERATIONS_PER_INVOKE}"
        )


def require_numbers(record, label=""):
    label = label or f"{record['method']}/{record['identity']}"
    allocated = as_bytes(record.get("allocated"))
    if allocated is None:
        raise Reject(
            f"NA/null allocation for {label} ({record['source']}); "
            "missing allocation data is never zero"
        )
    mean = record.get("mean")
    if not is_number(mean) or float(mean) <= 0.0:
        raise Reject(f"NA/null mean for {label} ({record['source']})")
    return allocated, float(mean)


def index_records(records) -> dict:
    """Index by (type, method, identity); a duplicate is a hard rejection."""
    index = {}
    for record in records:
        key = (record["type"], record["method"], record["identity"])
        if key in index:
            raise Reject(
                f"duplicate result for {record['type']}.{record['method']}/"
                f"{record['identity']} ({record['source']} and "
                f"{index[key]['source']})"
            )
        index[key] = record
    return index


def measured_classes(records) -> set:
    return {record["short_type"] for record in records}


def verify_divisor_contract(repo, classes) -> dict:
    """Pin each class divisor to the checked-in benchmark source (fail closed).

    The BDN JSON export has no ``OperationsPerInvoke`` scaling factor, so the divisor is
    only trustworthy if the class constant ``Iterations`` equals it and every measured
    ``[Benchmark]`` method leaves ``OperationsPerInvoke`` at 1.
    """
    repo = Path(repo)
    src_dir = repo / BENCH_PROJECT_REL
    contract = {}
    for short_type in sorted(classes):
        if short_type not in CLASS_DIVISORS:
            raise Reject(
                f"unverified logical operation count: unknown benchmark class "
                f"{short_type!r}"
            )
        path = None
        for candidate in sorted(src_dir.rglob("*.cs")):
            try:
                text = candidate.read_text(encoding="utf-8")
            except OSError as exc:
                raise Reject(f"cannot read benchmark source {candidate}: {exc}") from exc
            class_match = _CLASS_RE.search(text)
            if class_match and class_match.group(1) == short_type:
                path = candidate
                break
        if path is None:
            raise Reject(
                f"unverified logical operation count: benchmark class {short_type} "
                f"not found under {src_dir}"
            )
        text = path.read_text(encoding="utf-8")
        iterations_match = _ITERATIONS_CONST_RE.search(text)
        if iterations_match is None:
            raise Reject(
                f"unverified logical operation count: {short_type} declares no "
                "const int Iterations"
            )
        iterations = int(iterations_match.group(1))
        divisor = CLASS_DIVISORS[short_type]
        if iterations != divisor:
            raise Reject(
                f"unverified logical operation count: {short_type} Iterations="
                f"{iterations} but CLASS_DIVISORS pins {divisor}"
            )
        for attr in _BENCHMARK_ATTR_RE.finditer(text):
            opi_match = _OPERATIONS_PER_INVOKE_RE.search(attr.group(0))
            if opi_match is not None:
                value = opi_match.group(1).strip()
                if value not in ("1", "1u", "1L"):
                    raise Reject(
                        f"unexpected OperationsPerInvoke={value} declared in "
                        f"{short_type} source contract"
                    )
        contract[short_type] = {
            "divisor": divisor,
            "iterations": iterations,
            "source": str(path.relative_to(repo)) if path.is_relative_to(repo) else str(path),
            "source_sha256": sha256_file(path),
        }
    return contract


# ------------------------------------------------------------------------------------------------
# measure — host lock, process overlap, artifact inventory/staging, provenance
# ------------------------------------------------------------------------------------------------
def processes_snapshot() -> list:
    code, output = run_capture(["ps", "-eo", "pid,ppid,args"])
    if code != 0:
        raise Reject(f"cannot snapshot processes: {output}")
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


def own_process_chain(snapshot) -> set:
    """Return the pid chain of the current process (self + ancestors)."""
    by_pid = {row["pid"]: row for row in snapshot}
    chain, pid = set(), os.getpid()
    while pid and pid not in chain:
        chain.add(pid)
        row = by_pid.get(pid)
        pid = row["ppid"] if row else 0
    return chain


def conflicting_processes(snapshot) -> list:
    """Unrelated benchmark/BDN workloads (everything outside our own process chain)."""
    chain = own_process_chain(snapshot)
    conflicts = []
    for row in snapshot:
        if row["pid"] in chain:
            continue
        args = row["args"]
        if "nextorm.benchmark" in args or "BenchmarkDotNet" in args:
            conflicts.append(row)
    return conflicts


def inherited_lock_fd(path) -> int | None:
    """Detect a lock inherited from an outer ``flock -x`` wrapper (same open description).

    File descriptors this process opened itself are tracked in ``_OWN_LOCK_FDS`` and are
    never mistaken for an inherited wrapper lock (a second acquisition must block/fail).
    """
    target = str(Path(path))
    fd_dir = Path("/proc/self/fd")
    if not fd_dir.is_dir():
        return None
    for entry in fd_dir.iterdir():
        try:
            descriptor = int(entry.name)
        except ValueError:
            continue
        if descriptor in _OWN_LOCK_FDS:
            continue
        try:
            if os.readlink(entry) == target:
                return descriptor
        except OSError:
            continue
    return None


class RoundLock:
    """Exclusive host measurement lock, held for the whole round."""

    def __init__(self, path, handle=None, inherited_fd=None, acquired_at=None, inherited=False):
        self.path = Path(path)
        self.handle = handle
        self.inherited_fd = inherited_fd
        self.acquired_at = acquired_at
        self.inherited = inherited
        self.released_at = None
        self.mode = "flock(LOCK_EX)" if not inherited else "inherited flock -x"

    def record(self) -> dict:
        return {
            "path": str(self.path),
            "mode": self.mode,
            "inherited": self.inherited,
            "acquired_at": self.acquired_at,
            "released_at": self.released_at,
        }

    def release(self):
        if self.released_at is not None:
            return
        # An inherited flock belongs to the outer wrapper: never unlock it from here.
        if self.handle is not None:
            descriptor = self.handle.fileno()
            try:
                fcntl.flock(descriptor, fcntl.LOCK_UN)
            finally:
                _OWN_LOCK_FDS.discard(descriptor)
                self.handle.close()
                self.handle = None
        self.released_at = utc_now()


def acquire_lock(path=LOCK_PATH, timeout=0.0) -> RoundLock:
    """Acquire the canonical lock exactly once (inherited wrapper or own LOCK_EX)."""
    path = Path(path)
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        raise MeasureRefused(f"cannot create lock directory {path.parent}: {exc}") from exc
    inherited_fd = inherited_lock_fd(path)
    if inherited_fd is not None:
        return RoundLock(path, inherited_fd=inherited_fd, acquired_at=utc_now(), inherited=True)
    try:
        handle = open(path, "a+")
    except OSError as exc:
        raise MeasureRefused(f"cannot open measurement lock {path}: {exc}") from exc
    _OWN_LOCK_FDS.add(handle.fileno())
    deadline = time.monotonic() + max(0.0, timeout)
    while True:
        try:
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            if time.monotonic() >= deadline:
                handle.close()
                raise MeasureRefused(
                    f"measurement lock {path} is held by another workload "
                    "(flock -x acquisition timed out)"
                )
            time.sleep(0.2)
            continue
        return RoundLock(path, handle=handle, acquired_at=utc_now())


def resolve_db(repo) -> dict | None:
    """Mirror ``BenchDb.Resolve`` so the recorded database is the measured one."""
    repo = Path(repo)
    env_path = os.environ.get("NEXTORM_BENCH_DB")
    if env_path:
        path, source = Path(env_path), "NEXTORM_BENCH_DB"
    elif Path("/tmp/nextorm-bench/test.db").exists():
        path, source = Path("/tmp/nextorm-bench/test.db"), "wsl-fallback"
    else:
        path, source = repo / "data" / "test.db", "cwd-fallback"
    if not path.exists():
        return {"path": str(path), "source": source, "sha256": None, "size": 0}
    return {
        "path": str(path),
        "source": source,
        "sha256": sha256_file(path),
        "size": path.stat().st_size,
    }


def benchmark_assembly(repo) -> dict | None:
    bin_dir = Path(repo) / BENCH_PROJECT_REL / "bin"
    if not bin_dir.is_dir():
        return None
    candidates = sorted(bin_dir.glob("*/Release/*/nextorm.benchmark.dll"))
    if not candidates:
        return None
    path = max(candidates, key=lambda item: item.stat().st_mtime_ns)
    return {
        "path": str(path.relative_to(Path(repo))),
        "sha256": sha256_file(path),
        "mtime_ns": path.stat().st_mtime_ns,
    }


def dotnet_info() -> dict:
    code, version = run_capture(["dotnet", "--version"])
    code_r, runtimes = run_capture(["dotnet", "--list-runtimes"])
    return {
        "sdk_version": version if code == 0 else None,
        "run_version": version if code != 0 and code is not None else None,
        "runtimes": runtimes.splitlines() if code_r == 0 else None,
    }


def host_identity() -> dict:
    uname = platform.uname()
    identity = {
        "node": uname.node,
        "machine": uname.machine,
        "system": uname.system,
        "release": uname.release,
        "python": platform.python_version(),
        "cpu_count": os.cpu_count(),
    }
    os_release = Path("/etc/os-release")
    if os_release.exists():
        for line in os_release.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("PRETTY_NAME="):
                identity["os"] = line.split("=", 1)[1].strip().strip('"')
                break
    cpuinfo = Path("/proc/cpuinfo")
    if cpuinfo.exists():
        for line in cpuinfo.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.lower().startswith("model name"):
                identity["cpu"] = line.split(":", 1)[1].strip()
                break
    return identity


def source_state(repo) -> dict:
    code, head = run_capture(["git", "rev-parse", "HEAD"], cwd=repo)
    if code != 0 or not head:
        raise Reject(f"cannot resolve git HEAD in {repo}: {head}")
    code_s, status = run_capture(["git", "status", "--porcelain"], cwd=repo)
    if code_s != 0:
        raise Reject(f"cannot read git status in {repo}: {status}")
    entries = [line for line in status.splitlines() if line.strip()]
    return {
        "head": head.splitlines()[0].strip(),
        "porcelain_sha256": sha256_text(status),
        "dirty_paths": len(entries),
    }


def inventory_artifacts(root) -> dict:
    """Path -> {size, mtime_ns, sha256} for every file under the artifact root."""
    root = Path(root)
    inventory = {}
    if not root.is_dir():
        return inventory
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        stat = path.stat()
        rel = str(path.relative_to(root))
        inventory[rel] = {
            "size": stat.st_size,
            "mtime_ns": stat.st_mtime_ns,
            "sha256": sha256_file(path),
        }
    return inventory


def changed_outputs(before, after) -> list:
    """New or rewritten files only — an untouched older JSON is never an output."""
    changed = []
    for rel, meta in after.items():
        previous = before.get(rel)
        if previous is None:
            changed.append(rel)
            continue
        if (
            previous["mtime_ns"] != meta["mtime_ns"]
            or previous["size"] != meta["size"]
            or previous["sha256"] != meta["sha256"]
        ):
            changed.append(rel)
    return sorted(changed)


def stage_invocation_artifacts(artifact_root, before, staged_dir) -> tuple:
    """Copy exactly the invocation's new outputs; never pick up a stale JSON."""
    artifact_root = Path(artifact_root)
    staged_dir = Path(staged_dir)
    after = inventory_artifacts(artifact_root)
    new_files = changed_outputs(before, after)
    staged = []
    for rel in new_files:
        source = artifact_root / rel
        destination = staged_dir / rel
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        source_hash = sha256_file(source)
        if source_hash != after[rel]["sha256"]:
            raise Reject(f"artifact changed while copying: {source}")
        if sha256_file(destination) != source_hash:
            raise Reject(f"staged artifact hash mismatch: {destination}")
        staged.append({"rel": rel, "sha256": source_hash, "size": after[rel]["size"]})
    stale = sorted(rel for rel in before if rel in after and rel not in new_files)
    return staged, after, stale


def expected_arms(short_type) -> tuple:
    if short_type == FAIR_CACHED_TYPE:
        arms = []
        for scenario in SCENARIOS:
            arms.append(f"{NEXTORM_CACHED_PREFIX}{scenario}")
            arms.append(f"{LINQ_PREFIX}{scenario}")
        return tuple(arms)
    if short_type == FAIR_TYPE:
        return (PREPARED_PREFIX,)
    if short_type == WARM_TYPE:
        arms = []
        for family in FAMILIES:
            for suffix in WARM_METHODS:
                arms.append(f"{family}_{suffix}")
        return tuple(arms)
    return ()


def validate_staged_report(staged_dir, expected_class) -> dict:
    """Fail closed on a missing/mixed/malformed report for one invocation."""
    staged_dir = Path(staged_dir)
    reports = discover_reports(staged_dir)
    if not reports:
        raise Reject(f"no staged BDN JSON report for {expected_class} under {staged_dir}")
    records = []
    for report in reports:
        report_records = parse_report(report)
        classes = {record["short_type"] for record in report_records}
        foreign = classes - {expected_class}
        if foreign:
            raise Reject(
                f"mixed artifacts: {report.name} carries {sorted(foreign)} but the "
                f"invocation measured {expected_class}"
            )
        if expected_class not in classes:
            raise Reject(
                f"staged report {report.name} does not contain {expected_class}"
            )
        records.extend(report_records)
    index_records(records)
    for record in records:
        require_operation_count(record)
        if record["job"] != "ShortRun":
            raise Reject(
                f"missing job for {record['method']}: expected ShortRun, got "
                f"{record['job']!r}"
            )
    methods = {record["method"] for record in records}
    if expected_class == FAIR_TYPE:
        prepared = sorted(m for m in methods if m.startswith(PREPARED_PREFIX))
        if not prepared:
            raise Reject(f"no {PREPARED_PREFIX}* control arm in the staged report")
    else:
        missing = [arm for arm in expected_arms(expected_class) if arm not in methods]
        if missing:
            raise Reject(
                f"missing reference/arm(s) in {expected_class}: {', '.join(missing)}"
            )
    identities = sorted({record["identity"] for record in records})
    return {
        "reports": [str(path.relative_to(staged_dir)) for path in reports],
        "methods": len(methods),
        "identities": identities,
        "records_sha256": sha256_text(
            json.dumps(
                sorted(
                    [r["type"], r["method"], r["identity"], str(r["allocated"]), str(r["mean"])]
                    for r in records
                )
            )
        ),
    }


def benchmark_argv(filter_pattern) -> list:
    return [
        "dotnet",
        "run",
        "--project",
        "benchmarks/nextorm.benchmark",
        "-c",
        "Release",
        "--",
        "--filter",
        filter_pattern,
        "--job",
        "short",
        "--exporters",
        "json",
    ]


def child_environment() -> tuple:
    """The exact measured environment: ``env -u NEXTORM_BENCH_FULL``."""
    env = dict(os.environ)
    removed = []
    if "NEXTORM_BENCH_FULL" in env:
        removed.append("NEXTORM_BENCH_FULL")
        del env["NEXTORM_BENCH_FULL"]
    recorded = {
        key: env[key]
        for key in ("NEXTORM_BENCH_DB", "DOTNET_CLI_TELEMETRY_OPTOUT", "COMPlus_ReadyToRun")
        if key in env
    }
    return env, {
        "removed": removed,
        "pinned": {"NEXTORM_BENCH_FULL": "unset"},
        "recorded": recorded,
        "NEXTORM_BENCH_DB": env.get("NEXTORM_BENCH_DB"),
    }


def command_line(argv, repo) -> str:
    return "cd {} && env -u NEXTORM_BENCH_FULL {}".format(
        shlex.quote(str(repo)), " ".join(shlex.quote(str(arg)) for arg in argv)
    )


def collect_invocation(repo, artifact_root, staged_dir, argv, env, timeout=None) -> dict:
    """Run one pinned invocation (bounded by ``timeout``) and stage only its new artifacts."""
    staged_dir = Path(staged_dir)
    staged_dir.mkdir(parents=True, exist_ok=True)
    before = inventory_artifacts(artifact_root)
    source_before = source_state(repo)
    assembly_before = benchmark_assembly(repo)
    db_before = resolve_db(repo)
    processes_before = processes_snapshot()
    conflicts_before = conflicting_processes(processes_before)
    started_at = utc_now()
    clock = time.monotonic()
    log_path = staged_dir / "console.log"
    timed_out = False
    with open(log_path, "wb") as log:
        try:
            completed = subprocess.run(
                [str(arg) for arg in argv],
                cwd=str(repo),
                env=env,
                stdout=log,
                stderr=subprocess.STDOUT,
                timeout=timeout,
            )
            exit_code = completed.returncode
        except subprocess.TimeoutExpired:
            timed_out = True
            exit_code = None
            log.write(
                "\ninvocation exceeded the {} s timeout; child killed\n".format(
                    timeout
                ).encode("utf-8")
            )
        except OSError as exc:
            log.write(f"\nlaunch failed: {exc}\n".encode("utf-8"))
            exit_code = None
    duration = round(time.monotonic() - clock, 3)
    ended_at = utc_now()
    source_after = source_state(repo)
    assembly_after = benchmark_assembly(repo)
    db_after = resolve_db(repo)
    processes_after = processes_snapshot()
    conflicts_after = conflicting_processes(processes_after)
    staged, inventory_after, stale = stage_invocation_artifacts(
        artifact_root, before, staged_dir
    )
    return {
        "argv": [str(arg) for arg in argv],
        "command_line": command_line(argv, repo),
        "cwd": str(repo),
        "started_at": started_at,
        "ended_at": ended_at,
        "duration_s": duration,
        "exit_code": exit_code,
        "timed_out": timed_out,
        "timeout_s": timeout,
        "stdout_log": str(log_path.relative_to(staged_dir)),
        "source_state_before": source_before,
        "source_state_after": source_after,
        "assembly_before": assembly_before,
        "assembly_after": assembly_after,
        "db_before": db_before,
        "db_after": db_after,
        "processes_before": processes_before,
        "processes_after": processes_after,
        "conflicts_before": conflicts_before,
        "conflicts_after": conflicts_after,
        "staged_artifacts": staged,
        "stale_artifacts": stale,
        "inventory_size_after": len(inventory_after),
    }


def measure(args) -> int:
    repo = Path(args.repo).resolve()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    artifact_root = repo / ARTIFACTS_REL
    record = {
        "tool": "t244_evidence/measure",
        "task": "T244",
        "issue": 244,
        "phase": args.phase,
        "round": args.round,
        "repo": str(repo),
        "artifact_root": str(artifact_root),
        "output": str(output),
        "started_at": utc_now(),
        "finished_at": None,
        "environment": {"host": host_identity(), "dotnet": dotnet_info()},
        "lock": {},
        "invocations": [],
        "source_state_start": None,
        "source_state_end": None,
        "validity": {"valid": True, "reasons": []},
    }
    reasons = record["validity"]["reasons"]
    commands = []
    lock = None

    def finish(code):
        record["finished_at"] = utc_now()
        record["validity"]["valid"] = not reasons
        if lock is not None:
            lock.release()
            record["lock"] = lock.record()
        write_json(output / "provenance.json", record)
        write_json(output / "round-validity.json", {
            "phase": args.phase,
            "round": args.round,
            "valid": not reasons,
            "reasons": list(reasons),
            "checked_at": record["finished_at"],
        })
        (output / "commands.log").write_text(
            "\n".join(commands) + ("\n" if commands else ""), encoding="utf-8", newline="\r\n"
        )
        return code

    try:
        lock = acquire_lock(LOCK_PATH, timeout=args.lock_timeout)
    except MeasureRefused as exc:
        reasons.append(str(exc))
        record["lock"] = {"path": str(LOCK_PATH), "mode": "unacquired", "error": str(exc)}
        return finish(EXIT_NUMERIC)
    record["lock"] = lock.record()

    initial_snapshot = processes_snapshot()
    initial_conflicts = conflicting_processes(initial_snapshot)
    record["processes_initial"] = initial_snapshot
    record["conflicts_initial"] = initial_conflicts
    if initial_conflicts:
        reasons.append(
            "external overlap: unrelated benchmark workload running: "
            + "; ".join(f"pid {row['pid']}: {row['args'][:160]}" for row in initial_conflicts)
        )
        return finish(EXIT_NUMERIC)

    record["source_state_start"] = source_state(repo)
    db = resolve_db(repo)
    record["db"] = db
    if db["sha256"] is None:
        reasons.append(f"database missing: {db['path']}")
        return finish(EXIT_NUMERIC)
    assembly = benchmark_assembly(repo)
    record["assembly"] = assembly
    if assembly is None:
        reasons.append("benchmark assembly missing: no Release build of nextorm.benchmark")
        return finish(EXIT_NUMERIC)

    env, env_record = child_environment()
    record["env"] = env_record
    if "NEXTORM_BENCH_FULL" in env:
        reasons.append("wrong environment: NEXTORM_BENCH_FULL survived into the child env")

    head = record["source_state_start"]
    invocation_timeout = getattr(args, "invocation_timeout", None)
    for number, invocation in enumerate(INVOCATIONS, start=1):
        tag = invocation["tag"]
        staged_dir = output / f"{number}-{tag}"
        argv = benchmark_argv(invocation["filter"])
        commands.append(command_line(argv, repo))
        try:
            collected = collect_invocation(
                repo, artifact_root, staged_dir, argv, env, timeout=invocation_timeout
            )
        except Reject as exc:
            reasons.append(f"{tag}: {exc}")
            collected = {"tag": tag, "error": str(exc)}
        collected["n"] = number
        collected["tag"] = tag
        collected["filter"] = invocation["filter"]
        collected["expected_class"] = invocation["expected_class"]
        collected["environment"] = record["environment"]
        collected["lock"] = record["lock"]
        record["invocations"].append(collected)
        if "error" in collected:
            continue

        if collected.get("timed_out"):
            reasons.append(
                f"{tag}: invocation exceeded the {invocation_timeout} s timeout (see console.log)"
            )
        if collected["exit_code"] != 0:
            reasons.append(
                f"{tag}: invocation exited {collected['exit_code']!r} (see console.log)"
            )
        if collected["source_state_before"] != collected["source_state_after"]:
            reasons.append(f"{tag}: source state changed during the invocation")
        if collected["source_state_before"] != head:
            reasons.append(f"{tag}: source state differs from the round start")
        if collected["assembly_before"] != collected["assembly_after"]:
            reasons.append(f"{tag}: benchmark assembly changed during the invocation")
        if collected["assembly_before"] != assembly:
            reasons.append(f"{tag}: benchmark assembly differs from the round build")
        if collected["db_before"] != collected["db_after"]:
            reasons.append(f"{tag}: database changed during the invocation")
        if collected["conflicts_after"]:
            reasons.append(
                f"{tag}: external overlap after the invocation: "
                + "; ".join(row["args"][:120] for row in collected["conflicts_after"])
            )
        if not collected["staged_artifacts"]:
            reasons.append(f"{tag}: no new artifacts — the run produced no evidence")
            continue
        try:
            summary = validate_staged_report(staged_dir, invocation["expected_class"])
        except Reject as exc:
            reasons.append(f"{tag}: {exc}")
            continue
        collected["report_summary"] = summary

    record["source_state_end"] = source_state(repo)
    if record["source_state_end"] != head:
        reasons.append("source state changed during the round (no product edits in a round)")
    final_snapshot = processes_snapshot()
    record["processes_final"] = final_snapshot
    record["conflicts_final"] = conflicting_processes(final_snapshot)
    if record["conflicts_final"]:
        reasons.append("external overlap at round end")
    record["commands_log"] = str(output / "commands.log")
    return finish(EXIT_OK if not reasons else EXIT_NUMERIC)


# ------------------------------------------------------------------------------------------------
# evaluate — same-round pairing, divisors, tier, controls, warm timing
# ------------------------------------------------------------------------------------------------
_ROUND_DIR_RE = re.compile(r"^round-0*(\d+)$")


def discover_rounds(root):
    """Resolve a phase directory (``baseline/``, ``candidate/``) into round dirs."""
    root = Path(root)
    if not root.is_dir():
        raise Reject(f"phase directory does not exist: {root}")
    children = [p for p in sorted(root.iterdir()) if p.is_dir() and _ROUND_DIR_RE.match(p.name)]
    if children:
        children.sort(key=lambda p: int(_ROUND_DIR_RE.match(p.name).group(1)))
        return children
    if (root / "round-validity.json").exists() or (root / "provenance.json").exists():
        return [root]
    raise Reject(f"no round directories under {root}")


def load_round(round_dir) -> dict:
    round_dir = Path(round_dir)
    validity_path = round_dir / "round-validity.json"
    if not validity_path.exists():
        raise Reject(
            f"missing round-validity.json in {round_dir} (round validity is never assumed)"
        )
    validity = read_json(validity_path, "round validity")
    if not isinstance(validity, dict) or not isinstance(validity.get("valid"), bool):
        raise Reject(f"malformed round-validity.json in {round_dir}")
    record = {
        "id": round_dir.name,
        "dir": str(round_dir),
        "valid": validity["valid"],
        "reasons": validity.get("reasons") or [],
        "records": [],
        "provenance": None,
    }
    provenance = round_dir / "provenance.json"
    if provenance.exists():
        record["provenance"] = read_json(provenance, "round provenance")
    if record["valid"]:
        reports = discover_reports(round_dir)
        if not reports:
            raise Reject(f"no BDN JSON report under the valid round {round_dir}")
        record["records"] = load_records(reports)
    return record


def phase_records(phase_root, min_rounds) -> tuple:
    rounds = [load_round(round_dir) for round_dir in discover_rounds(phase_root)]
    valid = [round_ for round_ in rounds if round_["valid"]]
    invalid = [
        {"id": round_["id"], "reasons": round_["reasons"]}
        for round_ in rounds
        if not round_["valid"]
    ]
    if len(valid) < min_rounds:
        raise Reject(
            f"{phase_root}: {len(valid)} valid round(s), {min_rounds} required "
            f"({len(invalid)} invalid round(s) retained)"
        )
    if valid:
        provenance = [round_["provenance"] for round_ in valid if round_["provenance"]]
        if provenance and len(provenance) != len(valid):
            raise Reject(f"{phase_root}: provenance missing from a valid round")
        if provenance:
            sources = {
                json.dumps(
                    round_["provenance"].get("source_state_start"), sort_keys=True
                )
                for round_ in valid
            }
            if len(sources) != 1:
                raise Reject(
                    f"{phase_root}: valid rounds were measured on different source states"
                )
    return valid, invalid


def safe_ratio(numerator, denominator):
    if denominator > 0:
        return numerator / denominator
    return 1.0 if numerator == 0 else None


def median(values):
    return statistics.median(values)


def tier_for(ratio):
    """Per-row tier from its worst allocation ratio (worst job, never the mean)."""
    if ratio is None:
        return "malformed"
    if ratio <= FINAL_RATIO:
        return "final"
    if ratio <= CHECKPOINT_RATIO:
        return "checkpoint"
    return "fail"


def timing_rule(ratios, band=WARM_TIME_TOLERANCE):
    """§3.3 repeat rule: median of three only when two pairs straddle the band."""
    ordered = list(ratios)
    if not ordered:
        return {"ok": None, "rule": "missing", "median": None}
    if len(ordered) >= 3:
        first_three = ordered[:3]
        values = [ratio for _, ratio in first_three]
        straddles = any(value <= band for value in values) and any(
            value > band for value in values
        )
        if straddles:
            value = median(values)
            return {"ok": value <= band, "rule": "median-of-three", "median": value}
        return {"ok": all(value <= band for value in values), "rule": "every-round", "median": None}
    values = [ratio for _, ratio in ordered]
    if all(value <= band for value in values):
        return {"ok": True, "rule": "every-round", "median": None}
    if all(value > band for value in values):
        return {"ok": False, "rule": "every-round", "median": None}
    return {"ok": None, "rule": "needs-third-round", "median": None}


def compute_phase(phase_root, min_rounds, repo):
    rounds, invalid = phase_records(phase_root, min_rounds)
    all_records = [record for round_ in rounds for record in round_["records"]]
    for round_ in rounds:
        # Duplicates are per round: the same arm in two rounds is expected and paired.
        index_records(round_["records"])
    for record in all_records:
        require_operation_count(record)
    contract = verify_divisor_contract(repo, measured_classes(all_records))
    return {
        "root": str(phase_root),
        "rounds": rounds,
        "invalid_rounds": invalid,
        "records": all_records,
        "contract": contract,
    }


def build_ratio_evidence(evaluation, tier):
    """Populate ``evaluation`` with rows, controls, reused rows and timing rows."""
    tier = tier or "final"
    threshold = FINAL_RATIO if tier == "final" else CHECKPOINT_RATIO
    baseline = evaluation["_baseline"]
    candidate = evaluation["_candidate"]

    def index_for(phase):
        index = {}
        for round_ in phase["rounds"]:
            per_round = {}
            for record in round_["records"]:
                key = (record["type"], record["method"], record["identity"])
                per_round[key] = record
            index[round_["id"]] = per_round
        return index

    baseline_index = index_for(baseline)
    candidate_index = index_for(candidate)

    def baseline_means(short_type, method, identity):
        values = []
        for round_id, per_round in baseline_index.items():
            record = per_round.get((short_type, method, identity))
            if record is None:
                continue
            _, mean = require_numbers(record, f"{method}/{identity} baseline")
            values.append(mean)
        return values

    def baseline_allocations(short_type, method, identity):
        values = []
        for round_id, per_round in baseline_index.items():
            record = per_round.get((short_type, method, identity))
            if record is None:
                continue
            allocated, _ = require_numbers(record, f"{method}/{identity} baseline")
            values.append(allocated)
        return values

    rows = []
    failures = []
    for scenario in SCENARIOS:
        nextorm_method = f"{NEXTORM_CACHED_PREFIX}{scenario}"
        reference_method = f"{LINQ_PREFIX}{scenario}"
        divisor = CLASS_DIVISORS[FAIR_CACHED_TYPE]
        ratio_rounds = []
        for round_id, per_round in candidate_index.items():
            candidate_records = [
                record
                for (_, method, identity), record in per_round.items()
                if method == nextorm_method
            ]
            if not candidate_records:
                raise Reject(f"missing NextORM arm {nextorm_method} in candidate {round_id}")
            for nextorm in sorted(candidate_records, key=lambda r: r["identity"]):
                identity = nextorm["identity"]
                require_short_run(nextorm, nextorm_method)
                reference = per_round.get(
                    (nextorm["type"], reference_method, identity)
                )
                if reference is None:
                    raise Reject(
                        f"missing comparator {reference_method}/{identity} for "
                        f"{nextorm_method} in candidate {round_id}"
                    )
                require_short_run(reference, reference_method)
                nextorm_bytes, nextorm_mean = require_numbers(nextorm)
                reference_bytes, reference_mean = require_numbers(reference)
                if reference_bytes == 0:
                    raise Reject(
                        f"zero comparator denominator {reference_method}/{identity} "
                        f"in candidate {round_id}"
                    )
                job_baseline = [
                    value / divisor
                    for value in baseline_allocations(
                        nextorm["type"], nextorm_method, identity
                    )
                ]
                if not job_baseline:
                    raise Reject(
                        f"missing matched pre-T244 baseline for {nextorm_method}/"
                        f"{identity} in the baseline phase"
                    )
                nextorm_per_query = nextorm_bytes / divisor
                reference_per_query = reference_bytes / divisor
                ratio = nextorm_per_query / reference_per_query
                ratio_rounds.append(
                    {
                        "round": round_id,
                        "job": identity,
                        "nextorm_bytes_per_query": nextorm_per_query,
                        "linq2db_bytes_per_query": reference_per_query,
                        "allocation_ratio": ratio,
                        "nextorm_mean": nextorm_mean,
                        "linq2db_mean": reference_mean,
                        "time_ratio_vs_reference": nextorm_mean / reference_mean,
                        "baseline_bytes_per_query": min(job_baseline),
                    }
                )
        worst = max(entry["allocation_ratio"] for entry in ratio_rounds)
        row = {
            "requirement": "R244-01" if scenario == "Cte" else (
                "R244-02" if scenario == "RecursiveCte" else "R244-07"
            ),
            "class": FAIR_CACHED_TYPE,
            "scenario": scenario,
            "divisor": divisor,
            "job": ratio_rounds[0]["job"] if ratio_rounds else None,
            "rounds": ratio_rounds,
            "worst_allocation_ratio": worst,
            "tier_status": tier_for(worst),
            "baseline_bytes_per_query": min(
                entry["baseline_bytes_per_query"] for entry in ratio_rounds
            ),
        }
        if scenario in CTE_SCENARIOS:
            if worst > threshold:
                failures.append(
                    f"{row['requirement']}: {scenario} allocation ratio {worst:.4f} > "
                    f"{threshold:.2f} ({tier}) in at least one candidate round/job"
                )
        else:
            regressions = [
                entry
                for entry in ratio_rounds
                if entry["nextorm_bytes_per_query"] > entry["baseline_bytes_per_query"]
            ]
            row["regression_vs_baseline"] = max(
                (
                    entry["nextorm_bytes_per_query"] - entry["baseline_bytes_per_query"]
                    for entry in ratio_rounds
                ),
                default=0.0,
            )
            for entry in regressions:
                failures.append(
                    f"R244-07: {scenario}/{entry['job']} candidate allocation "
                    f"{entry['nextorm_bytes_per_query']:.2f} B/query is above the "
                    f"pre-T244 baseline {entry['baseline_bytes_per_query']:.2f} B/query "
                    f"({entry['round']})"
                )
        rows.append(row)

    prepared = []
    baseline_prepared = {}
    for round_ in baseline["rounds"]:
        for record in round_["records"]:
            if not record["method"].startswith(PREPARED_PREFIX):
                continue
            key = (record["method"], record["identity"])
            allocated, mean = require_numbers(
                record, f"{record['method']}/{record['identity']} baseline"
            )
            baseline_prepared.setdefault(key, []).append((allocated, mean))
    candidate_prepared = {}
    for round_ in candidate["rounds"]:
        for record in round_["records"]:
            if not record["method"].startswith(PREPARED_PREFIX):
                continue
            key = (round_["id"], record["method"], record["identity"])
            candidate_prepared[key] = record
    missing_baseline = sorted(
        {(method, identity) for (_, method, identity) in candidate_prepared}
        - set(baseline_prepared)
    )
    if missing_baseline:
        raise Reject(
            "prepared control without a matched baseline: "
            + ", ".join(f"{method}/{identity}" for method, identity in missing_baseline)
        )
    omitted = sorted(
        set(baseline_prepared)
        - {(method, identity) for (_, method, identity) in candidate_prepared}
    )
    if omitted:
        raise Reject(
            "omitted prepared control (present in baseline, absent in candidate): "
            + ", ".join(f"{method}/{identity}" for method, identity in omitted)
        )
    for key in sorted(candidate_prepared):
        round_id, method, identity = key
        record = candidate_prepared[key]
        prepared_baseline_allocs = [entry[0] for entry in baseline_prepared[(method, identity)]]
        prepared_baseline_means = [entry[1] for entry in baseline_prepared[(method, identity)]]
        allocation, mean = require_numbers(record, f"{method}/{identity} candidate")
        alloc_ratio = safe_ratio(allocation, median(prepared_baseline_allocs))
        time_ratio = safe_ratio(mean, median(prepared_baseline_means))
        ok = (
            alloc_ratio is not None
            and time_ratio is not None
            and alloc_ratio <= PREPARED_TOLERANCE
            and time_ratio <= PREPARED_TOLERANCE
        )
        prepared.append(
            {
                "method": method,
                "job": identity,
                "round": round_id,
                "allocation_ratio": alloc_ratio,
                "time_ratio": time_ratio,
                "tolerance": PREPARED_TOLERANCE,
                "ok": ok,
            }
        )
        if not ok:
            failures.append(
                f"R244-06: prepared control {method}/{identity} regressed in {round_id} "
                f"(alloc {alloc_ratio}, time {time_ratio} > {PREPARED_TOLERANCE})"
            )

    reused_rows = []
    timing_ratios = {}
    for scenario in CTE_SCENARIOS:
        for round_ in candidate["rounds"]:
            for record in round_["records"]:
                if record["short_type"] != WARM_TYPE:
                    continue
                if record["method"] == f"{scenario}_Warm_Reused":
                    allocated, _ = require_numbers(
                        record, f"{record['method']}/{record['identity']}"
                    )
                    ok = allocated == WARM_REUSED_BYTES
                    reused_rows.append(
                        {
                            "family": scenario,
                            "job": record["identity"],
                            "round": round_["id"],
                            "bytes": allocated,
                            "bytes_per_query": allocated / CLASS_DIVISORS[WARM_TYPE],
                            "ok": ok,
                        }
                    )
                    if not ok:
                        failures.append(
                            f"R244-05: {scenario}_Warm_Reused/{record['identity']} is "
                            f"{allocated} B ({round_['id']}), expected exactly 0 B"
                        )
        timing_methods = (
            ("WarmDecompose", WARM_TYPE, f"{scenario}_Warm_PlanOnly"),
            ("FairCached", FAIR_CACHED_TYPE, f"{NEXTORM_CACHED_PREFIX}{scenario}"),
        )
        for arm, short_type, method in timing_methods:
            for round_ in candidate["rounds"]:
                for record in round_["records"]:
                    if record["short_type"] != short_type or record["method"] != method:
                        continue
                    baseline_values = baseline_means(
                        record["type"], method, record["identity"]
                    )
                    if not baseline_values:
                        raise Reject(
                            f"missing matched pre-T244 baseline for {method}/"
                            f"{record['identity']}"
                        )
                    _, mean = require_numbers(record, f"{method}/{record['identity']}")
                    timing_ratios.setdefault(
                        (scenario, arm, record["identity"]), []
                    ).append((round_["id"], mean / median(baseline_values)))
    warm_timing = []
    for (family, arm, identity), ratios in sorted(timing_ratios.items()):
        verdict = timing_rule(ratios)
        warm_timing.append(
            {
                "family": family,
                "arm": arm,
                "job": identity,
                "rounds": [{"round": rid, "ratio": value} for rid, value in ratios],
                "rule": verdict["rule"],
                "median": verdict["median"],
                "ok": verdict["ok"],
                "band": WARM_TIME_TOLERANCE,
            }
        )
        if verdict["ok"] is None:
            failures.append(
                f"R244-04: {family} {arm} timing inconclusive "
                f"({verdict['rule']}); a third paired round is required"
            )
        elif not verdict["ok"]:
            failures.append(
                f"R244-04: {family} {arm} timing ratio "
                f"{max(value for _, value in ratios):.4f} > "
                f"+{int(WARM_TIME_TOLERANCE * 100) - 100}% of the pre-T244 baseline"
            )

    return {
        "rows": rows,
        "prepared_controls": prepared,
        "warm_reused": reused_rows,
        "timing": warm_timing,
        "failures": failures,
    }


def evaluate(args) -> int:
    output = Path(args.output)
    tier = args.tier
    payload = {
        "tool": "t244_evidence/evaluate",
        "task": "T244",
        "issue": 244,
        "generated_at": utc_now(),
        "tier_requested": tier,
        "baseline": str(args.baseline),
        "candidate": str(args.candidate),
        "source_contract": {},
        "phase": {},
        "rows": [],
        "prepared_controls": [],
        "warm_reused": [],
        "timing": [],
        "failures": [],
        "unresolved_final": [],
        "tier_status": None,
        "exit_code": EXIT_MALFORMED,
        "status": "malformed",
    }
    try:
        if args.min_rounds < 1:
            raise Reject("--min-rounds must be >= 1 (a round is never optional)")
        repo = Path(args.repo).resolve() if args.repo else DEFAULT_REPO
        baseline = compute_phase(args.baseline, args.min_rounds, repo)
        candidate = compute_phase(args.candidate, args.min_rounds, repo)
        for phase in (baseline, candidate):
            for round_ in phase["rounds"]:
                for record in round_["records"]:
                    if record["short_type"] not in CLASS_DIVISORS:
                        continue
                    method = record["method"]
                    participating = (
                        method.startswith(PREPARED_PREFIX)
                        or method.startswith(NEXTORM_CACHED_PREFIX)
                        or method.startswith(LINQ_PREFIX)
                        or method in expected_arms(record["short_type"])
                    )
                    if participating:
                        require_numbers(record, f"{method}/{record['identity']}")
                        require_short_run(record, method)
        payload["source_contract"] = candidate["contract"]
        payload["phase"] = {
            "baseline": {
                "root": baseline["root"],
                "valid_rounds": [round_["id"] for round_ in baseline["rounds"]],
                "invalid_rounds": baseline["invalid_rounds"],
            },
            "candidate": {
                "root": candidate["root"],
                "valid_rounds": [round_["id"] for round_ in candidate["rounds"]],
                "invalid_rounds": candidate["invalid_rounds"],
            },
        }
        payload["_baseline"] = baseline
        payload["_candidate"] = candidate
        evidence = build_ratio_evidence(payload, tier)
        payload.update(evidence)
        statuses = [row["tier_status"] for row in payload["rows"] if row["scenario"] in CTE_SCENARIOS]
        if any(status == "malformed" for status in statuses):
            raise Reject("malformed tier status")
        if any(status == "fail" for status in statuses):
            payload["tier_status"] = "fail"
        elif any(status == "checkpoint" for status in statuses):
            payload["tier_status"] = "checkpoint"
        else:
            payload["tier_status"] = "final"
        if payload["tier_status"] != "final":
            payload["unresolved_final"] = [
                row["requirement"]
                for row in payload["rows"]
                if row["scenario"] in CTE_SCENARIOS and row["worst_allocation_ratio"] > FINAL_RATIO
            ]
        numeric_ok = not payload["failures"]
        if tier == "final":
            numeric_ok = numeric_ok and payload["tier_status"] == "final"
        else:
            numeric_ok = numeric_ok and payload["tier_status"] in ("checkpoint", "final")
        payload["exit_code"] = EXIT_OK if numeric_ok else EXIT_NUMERIC
        payload["status"] = "passed" if numeric_ok else "unmet"
    except Reject as exc:
        payload["status"] = "malformed"
        payload["exit_code"] = EXIT_MALFORMED
        payload["error"] = str(exc)
    payload.pop("_baseline", None)
    payload.pop("_candidate", None)
    write_json(output, payload)
    write_ratios_csv(output, payload)
    print(f"evaluate: {payload['status']} tier={payload['tier_requested']} "
          f"measured={payload.get('tier_status')} -> {output}")
    if payload["status"] == "malformed":
        print(f"ERROR: {payload.get('error')}", file=sys.stderr)
    return payload["exit_code"]


def write_ratios_csv(output, payload) -> Path:
    import csv

    destination = Path(output).parent / "ratios.csv"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with open(destination, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "criterion",
                "class",
                "scenario",
                "job",
                "round",
                "divisor",
                "nextorm_bytes_per_query",
                "reference_bytes_per_query",
                "allocation_ratio",
                "baseline_reference",
                "time_ratio_vs_reference",
                "tier_status",
            ]
        )
        for row in payload.get("rows", []):
            for entry in row.get("rounds", []):
                writer.writerow(
                    [
                        row.get("requirement"),
                        row.get("class"),
                        row.get("scenario"),
                        entry.get("job"),
                        entry.get("round"),
                        row.get("divisor"),
                        f"{entry.get('nextorm_bytes_per_query'):.2f}"
                        if entry.get("nextorm_bytes_per_query") is not None else "",
                        f"{entry.get('linq2db_bytes_per_query'):.2f}"
                        if entry.get("linq2db_bytes_per_query") is not None else "",
                        f"{entry.get('allocation_ratio'):.6f}"
                        if entry.get("allocation_ratio") is not None else "",
                        f"{entry.get('baseline_bytes_per_query'):.2f}"
                        if entry.get("baseline_bytes_per_query") is not None else "",
                        f"{entry.get('time_ratio_vs_reference'):.6f}"
                        if entry.get("time_ratio_vs_reference") is not None else "",
                        row.get("tier_status"),
                    ]
                )
        for row in payload.get("prepared_controls", []):
            writer.writerow(
                [
                    "R244-06",
                    FAIR_TYPE,
                    row["method"],
                    row["job"],
                    row["round"],
                    CLASS_DIVISORS[FAIR_TYPE],
                    "",
                    "",
                    f"{row['allocation_ratio']:.6f}" if row["allocation_ratio"] is not None else "",
                    "",
                    f"{row['time_ratio']:.6f}" if row["time_ratio"] is not None else "",
                    "ok" if row["ok"] else "fail",
                ]
            )
        for row in payload.get("warm_reused", []):
            writer.writerow(
                [
                    "R244-05",
                    WARM_TYPE,
                    row["family"],
                    row["job"],
                    row["round"],
                    CLASS_DIVISORS[WARM_TYPE],
                    f"{row['bytes_per_query']:.2f}",
                    "",
                    "",
                    "",
                    "",
                    "ok" if row["ok"] else "fail",
                ]
            )
    return destination


# ------------------------------------------------------------------------------------------------
# attribute — phase table, ranking, residual bound
# ------------------------------------------------------------------------------------------------
def load_raw_hits(round_dir):
    """Verified raw hit/miss counts, when the round actually carries them.

    A probe is authoritative only when it is recorded: the round's
    ``raw-hits.json`` / ``raw-hit-counts.json`` or a ``RawHitCounts`` object in the
    BDN export.  Absence is reported as ``raw-hit-count: unavailable`` — hit counts
    are never inferred from other rows.
    """
    round_dir = Path(round_dir)
    found = None
    candidates = [
        round_dir / "raw-hits.json",
        round_dir / "raw-hit-counts.json",
        round_dir.parent / "raw-hits.json",
    ]
    for candidate in candidates:
        if candidate.exists():
            found = read_json(candidate, "raw-hit counts")
            break
    if found is None:
        try:
            reports = discover_reports(round_dir)
        except Reject:
            reports = []
        for report in reports:
            data = read_json(report, "BDN report")
            raw = data.get("RawHitCounts") if isinstance(data, dict) else None
            if isinstance(raw, dict):
                found = raw
                break
    if found is None:
        return {"available": False, "families": {}, "note": "raw-hit-count: unavailable"}
    if not isinstance(found, dict):
        raise Reject("raw-hit counts must be an object keyed by CTE family")
    families = {}
    for family in FAMILIES:
        entry = found.get(family)
        if not isinstance(entry, dict):
            continue
        hits, misses = entry.get("hits"), entry.get("misses")
        if not is_integral(hits) or not is_integral(misses) or hits < 0 or misses < 0:
            raise Reject(f"malformed raw hit/miss count for {family}: {entry!r}")
        families[family] = {"hits": int(hits), "misses": int(misses)}
    if not families:
        raise Reject("raw-hit counts present but carry no CTE family")
    return {"available": True, "families": families}


def reference_bytes(round_dir, family, evaluation_path, identity):
    """Same-round linq2db B/query for *family* (FairCached, /10).

    Only the round directory and its immediate sibling invocation directories are searched:
    an ancestor walk could silently pick up another round's evidence.
    """
    divisor = CLASS_DIVISORS[FAIR_CACHED_TYPE]
    round_dir = Path(round_dir)
    roots = [round_dir]
    if round_dir.parent.is_dir():
        roots.extend(
            child for child in sorted(round_dir.parent.iterdir()) if child.is_dir()
        )
    seen = set()
    for root in roots:
        if root in seen or not root.is_dir():
            continue
        seen.add(root)
        try:
            reports = discover_reports(root)
        except Reject:
            continue
        for report in reports:
            for record in parse_report(report):
                if (
                    record["short_type"] == FAIR_CACHED_TYPE
                    and record["method"] == f"{LINQ_PREFIX}{family}"
                    and record["identity"] == identity
                ):
                    allocated, _ = require_numbers(
                        record, f"{record['method']}/{record['identity']}"
                    )
                    return {
                        "bytes_per_query": allocated / divisor,
                        "source": str(report),
                        "job": identity,
                    }
    if evaluation_path:
        payload = read_json(evaluation_path, "evaluation")
        for row in payload.get("rows") or []:
            if row.get("scenario") != family:
                continue
            for entry in row.get("rounds") or []:
                if entry.get("job") != identity:
                    continue
                value = entry.get("linq2db_bytes_per_query")
                if is_number(value):
                    return {
                        "bytes_per_query": float(value),
                        "source": str(evaluation_path),
                        "job": identity,
                    }
    return None


def attribute(args) -> int:
    output = Path(args.output)
    markdown = output.with_suffix(".md")
    round_dir = Path(args.round)
    payload = {
        "tool": "t244_evidence/attribute",
        "task": "T244",
        "issue": 244,
        "generated_at": utc_now(),
        "round": str(round_dir),
        "divisor": CLASS_DIVISORS[WARM_TYPE],
        "families": [],
        "raw_hits": {},
        "ranking": [],
        "residual": {},
        "status": "malformed",
        "exit_code": EXIT_MALFORMED,
    }
    try:
        reports = discover_reports(round_dir)
        records = [
            record for record in load_records(reports) if record["short_type"] == WARM_TYPE
        ]
        if not records:
            raise Reject(f"no {WARM_TYPE} results under {round_dir}")
        for record in records:
            require_operation_count(record)
            require_short_run(record, record["method"])
        index_records(records)
        by_method = {}
        for record in records:
            by_method.setdefault(record["method"], {})[record["identity"]] = record
        identities = sorted({record["identity"] for record in records})
        raw_hits = load_raw_hits(round_dir)
        payload["raw_hits"] = raw_hits
        divisor = CLASS_DIVISORS[WARM_TYPE]
        residual_ok = True
        for family in FAMILIES:
            for identity in identities:
                methods = {}
                for suffix in WARM_METHODS:
                    method = f"{family}_{suffix}"
                    record = by_method.get(method, {}).get(identity)
                    if record is None:
                        raise Reject(f"missing phase {method}/{identity}")
                    allocated, mean = require_numbers(record, f"{method}/{identity}")
                    methods[suffix] = {
                        "bytes_per_query": allocated / divisor,
                        "mean": mean,
                        "source": record["source"],
                    }
                end_to_end = methods["Warm_PlanOnly"]["bytes_per_query"]
                prepare_no_hash = methods["Prepare_NoHash"]["bytes_per_query"]
                prepare_hash = methods["Prepare_Hash"]["bytes_per_query"]
                plan_key = methods["Warm_Reused"]["bytes_per_query"]
                construct = methods["Construct"]["bytes_per_query"]
                # Ordering guard: the inclusive preparation arms must genuinely include the
                # construction arm.  A violation means the phase rows were mixed or a phase
                # was counted twice, never a valid decomposition.
                if construct > prepare_no_hash or prepare_no_hash > prepare_hash:
                    raise Reject(
                        f"inconsistent phase ordering for {family}/{identity}: "
                        f"Construct {construct:.2f} <= Prepare_NoHash {prepare_no_hash:.2f} "
                        f"<= Prepare_Hash {prepare_hash:.2f} does not hold"
                    )
                # Only correctly nested phases are subtracted: construction (measured, nested
                # in end-to-end) and the already-prepared arm's plan-key/lookup cost.  The
                # preparation/hash arms are NOT nested in the warm path (the raw recipe hit
                # skips full preparation) and subtracting them would double-count.
                signed_residual = end_to_end - construct - plan_key
                if signed_residual < -max(
                    NEGATIVE_RESIDUAL_ABS_BYTES, NEGATIVE_RESIDUAL_FRACTION * end_to_end
                ):
                    raise Reject(
                        f"inconsistent nesting for {family}/{identity}: end-to-end "
                        f"{end_to_end:.2f} B/query is smaller than the correctly nested "
                        f"components (Construct {construct:.2f} + PlanKey {plan_key:.2f})"
                    )
                residual = max(0.0, signed_residual)
                bound = max(RESIDUAL_ABS_BYTES, RESIDUAL_FRACTION * end_to_end)
                bound_ok = residual <= bound
                residual_ok = residual_ok and bound_ok
                reference = reference_bytes(round_dir, family, args.evaluation, identity)
                if reference is None:
                    raise Reject(
                        f"missing same-round linq2db reference for {family}/{identity}"
                    )
                linq_bytes = reference["bytes_per_query"]
                gap = max(0.0, end_to_end - linq_bytes)
                phases = [
                    {"phase": "Construct", "bytes_per_query": construct,
                     "kind": "measured", "inclusive": False, "nested_in_warm": True},
                    {"phase": "Prepare_NoHash", "bytes_per_query": prepare_no_hash,
                     "kind": "measured", "inclusive": True, "nested_in_warm": False},
                    {"phase": "Prepare_NoHash_Delta",
                     "bytes_per_query": prepare_no_hash - construct,
                     "kind": "derived", "inclusive": False, "nested_in_warm": False},
                    {"phase": "Prepare_Hash", "bytes_per_query": prepare_hash,
                     "kind": "measured", "inclusive": True, "nested_in_warm": False},
                    {"phase": "Prepare_Hash_Delta",
                     "bytes_per_query": prepare_hash - prepare_no_hash,
                     "kind": "derived", "inclusive": False, "nested_in_warm": False},
                    {"phase": "PlanKey", "bytes_per_query": plan_key,
                     "kind": "measured", "inclusive": False, "nested_in_warm": True},
                    {"phase": "Warm_PlanOnly", "bytes_per_query": end_to_end,
                     "kind": "measured", "inclusive": True, "nested_in_warm": True},
                    {"phase": "RawHit", "bytes_per_query": residual,
                     "kind": "derived", "inclusive": False, "nested_in_warm": True},
                    {"phase": "end-to-end", "bytes_per_query": end_to_end,
                     "kind": "measured", "inclusive": True, "nested_in_warm": True},
                ]
                for phase in phases:
                    phase["percent_of_total"] = (
                        100.0 * phase["bytes_per_query"] / end_to_end
                        if end_to_end > 0 else None
                    )
                    phase["excess_vs_linq2db"] = max(
                        0.0, phase["bytes_per_query"] - linq_bytes
                    )
                    phase["frozen"] = phase["phase"] in FROZEN_COMPONENTS
                eligible = []
                for phase in phases:
                    if phase["phase"] not in CANDIDATE_COMPONENTS:
                        continue
                    removable = max(0.0, phase["bytes_per_query"])
                    is_eligible = (
                        removable >= ELIGIBLE_ABS_BYTES
                        or removable >= ELIGIBLE_FRACTION * gap
                    )
                    if is_eligible:
                        eligible.append(
                            {
                                "component": phase["phase"],
                                "family": family,
                                "job": identity,
                                "removable_bytes_per_query": removable,
                                "unit": CANDIDATE_COMPONENTS[phase["phase"]],
                                "gap_bytes_per_query": gap,
                                "frozen": False,
                            }
                        )
                eligible.sort(key=lambda row: row["removable_bytes_per_query"], reverse=True)
                payload["ranking"].extend(eligible)
                payload["families"].append(
                    {
                        "family": family,
                        "job": identity,
                        "phases": phases,
                        "end_to_end_bytes_per_query": end_to_end,
                        "linq2db_bytes_per_query": linq_bytes,
                        "linq2db_source": reference["source"],
                        "gap_bytes_per_query": gap,
                        "residual_bytes_per_query": residual,
                        "residual_signed": signed_residual,
                        "residual_bound": bound,
                        "residual_ok": bound_ok,
                        "warm_path_excludes_full_preparation": prepare_hash > end_to_end,
                        "accounting": {
                            "subtracted": ["Construct", "PlanKey"],
                            "excluded_as_not_nested": [
                                "Prepare_NoHash",
                                "Prepare_NoHash_Delta",
                                "Prepare_Hash",
                                "Prepare_Hash_Delta",
                            ],
                            "inclusive_labelled": [
                                "Prepare_NoHash",
                                "Prepare_Hash",
                                "Warm_PlanOnly",
                                "end-to-end",
                            ],
                            "rule": "residual = end-to-end - Construct - PlanKey "
                                    "(only correctly nested phases, never double-counted)",
                        },
                        "raw_hit_count": (
                            raw_hits["families"].get(family, {})
                            if raw_hits["available"] else "unavailable"
                        ),
                    }
                )
        payload["ranking"].sort(key=lambda row: row["removable_bytes_per_query"], reverse=True)
        payload["residual"] = {
            "ok": residual_ok,
            "flag": None if residual_ok else "bounded-re-gather-required",
            "formula": "end-to-end - Construct - PlanKey",
            "bound": "max(128 B/query, 10% of measured warm bytes)",
            "components": sorted(FROZEN_COMPONENTS),
        }
        payload["status"] = "bounded" if residual_ok else "bounded-re-gather-required"
        payload["exit_code"] = EXIT_OK if residual_ok else EXIT_NUMERIC
    except Reject as exc:
        payload["status"] = "malformed"
        payload["exit_code"] = EXIT_MALFORMED
        payload["error"] = str(exc)
    write_json(output, payload)
    write_attribution_markdown(markdown, payload)
    print(f"attribute: {payload['status']} -> {output}")
    if payload["status"] == "malformed":
        print(f"ERROR: {payload.get('error')}", file=sys.stderr)
    return payload["exit_code"]


def write_attribution_markdown(path, payload) -> Path:
    lines = [
        "# T244 warm-CTE attribution",
        "",
        f"- round: `{payload.get('round')}`",
        f"- WarmDecompose divisor: {payload.get('divisor')}",
        f"- status: **{payload.get('status')}**",
        "- residual bound check: "
        + (
            "failed (" + payload["residual"]["flag"] + ")"
            if payload.get("residual", {}).get("flag")
            else "ok"
        ),
        "",
    ]
    for family in payload.get("families", []):
        lines.append(f"## {family['family']} / {family['job']}")
        lines.append("")
        lines.append("| phase | B/query | % of warm | excess vs linq2db | kind | nested in warm | frozen |")
        lines.append("|---|---|---|---|---|---|---|")
        for phase in family["phases"]:
            percent = phase["percent_of_total"]
            lines.append(
                "| {phase} | {bytes:.2f} | {percent} | {excess:.2f} | {kind}{incl} | {nested} | {frozen} |".format(
                    phase=phase["phase"],
                    bytes=phase["bytes_per_query"],
                    percent=f"{percent:.2f}%" if percent is not None else "NA",
                    excess=phase["excess_vs_linq2db"],
                    kind=phase["kind"],
                    incl=" (inclusive)" if phase["inclusive"] else "",
                    nested="yes" if phase.get("nested_in_warm") else "no",
                    frozen="yes" if phase["frozen"] else "no",
                )
            )
        lines.append("")
        lines.append(
            f"end-to-end {family['end_to_end_bytes_per_query']:.2f} B/query vs linq2db "
            f"{family['linq2db_bytes_per_query']:.2f} B/query; residual "
            f"{family['residual_bytes_per_query']:.2f} B/query "
            f"(signed {family['residual_signed']:.2f}, bound "
            f"{family['residual_bound']:.2f}); raw-hit-count: "
            f"{family['raw_hit_count']}"
        )
        lines.append("")
    lines.append("## Eligible non-frozen ranking")
    lines.append("")
    if not payload.get("ranking"):
        lines.append("_No eligible non-frozen component._")
    for index, row in enumerate(payload.get("ranking", []), start=1):
        lines.append(
            f"{index}. {row['family']}/{row['job']} — {row['component']}: "
            f"{row['removable_bytes_per_query']:.2f} B/query removable "
            f"(gap {row['gap_bytes_per_query']:.2f}) → {row['unit']}"
        )
    lines.append("")
    Path(path).write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\r\n")
    return Path(path)


# ------------------------------------------------------------------------------------------------
# docs-check — tier wording vs §9 docs mapping
# ------------------------------------------------------------------------------------------------
def scan_claims(text, patterns):
    hits = []
    for pattern in patterns:
        for match in pattern.finditer(text):
            prefix = text[max(0, match.start() - 60):match.start()]
            if _NEGATION_RE.search(prefix):
                continue
            hits.append({"pattern": pattern.pattern, "match": match.group(0)})
    return hits


def docs_check(args) -> int:
    repo = Path(args.repo).resolve()
    evaluation_path = Path(args.evaluation)
    payload = {
        "tool": "t244_evidence/docs-check",
        "task": "T244",
        "generated_at": utc_now(),
        "repo": str(repo),
        "evaluation": str(evaluation_path),
        "tier": None,
        "documents": [],
        "claim_hits": [],
        "consistent": False,
        "exit_code": EXIT_MALFORMED,
        "status": "malformed",
    }
    mapping_payload = {
        "tool": "t244_evidence/docs-check",
        "generated_at": payload["generated_at"],
        "tier": None,
        "mapping": [],
    }
    destination = evaluation_path.parent
    try:
        evaluation = read_json(evaluation_path, "evaluation")
        tier = evaluation.get("tier_status")
        if tier not in ("final", "checkpoint", "fail"):
            raise Reject(
                f"evaluation has no measured tier (tier_status={tier!r}); docs cannot be "
                "checked against an unmeasured acceptance"
            )
        payload["tier"] = tier
        mapping_payload["tier"] = tier
        forbidden = FINAL_CLAIM_PATTERNS if tier != "final" else CHECKPOINT_CLAIM_PATTERNS
        for rel, token in DOC_MAPPING:
            path = repo / rel
            entry = {
                "path": rel,
                "required_token": token,
                "exists": path.exists(),
                "token_present": False,
                "claims": [],
            }
            if not path.exists():
                payload["documents"].append(entry)
                raise Reject(f"docs mapping target missing: {rel}")
            text = path.read_text(encoding="utf-8", errors="replace")
            entry["token_present"] = token.lower() in text.lower()
            entry["claims"] = scan_claims(text, forbidden)
            payload["documents"].append(entry)
            if not entry["token_present"]:
                raise Reject(f"docs mapping target {rel} does not mention {token!r}")
        payload["claim_hits"] = [
            {"path": entry["path"], "claims": entry["claims"]}
            for entry in payload["documents"]
            if entry["claims"]
        ]
        if payload["claim_hits"]:
            payload["status"] = "inconsistent"
            payload["exit_code"] = EXIT_NUMERIC
            payload["consistent"] = False
        else:
            payload["status"] = "consistent"
            payload["exit_code"] = EXIT_OK
            payload["consistent"] = True
    except Reject as exc:
        payload["status"] = "malformed"
        payload["exit_code"] = EXIT_MALFORMED
        payload["error"] = str(exc)
        print(f"ERROR: {exc}", file=sys.stderr)
    mapping_payload["mapping"] = payload["documents"]
    write_json(destination / "docs-check.json", payload)
    write_json(destination / "docs-mapping.json", mapping_payload)
    print(f"docs-check: {payload['status']} tier={payload.get('tier')}")
    return payload["exit_code"]


# ------------------------------------------------------------------------------------------------
# invariants — frozen regions and owner WIP markers
# ------------------------------------------------------------------------------------------------
def invariants(args) -> int:
    repo = Path(args.repo).resolve()
    output = Path(args.output)
    payload = {
        "tool": "t244_evidence/invariants",
        "task": "T244",
        "generated_at": utc_now(),
        "repo": str(repo),
        "execution_map": str(args.execution_map),
        "frozen_regions": [],
        "wip_markers": [],
        "changed": [],
        "missing": [],
        "exit_code": EXIT_MALFORMED,
        "status": "malformed",
    }
    try:
        execution_map = read_json(args.execution_map, "execution map")
        if not isinstance(execution_map, dict):
            raise Reject("execution map must be an object")
        regions = execution_map.get("frozen_regions")
        if not isinstance(regions, list) or not regions:
            raise Reject("execution map declares no frozen_regions")
        declared = {}
        for region in regions:
            if not isinstance(region, dict) or not region.get("path") or not region.get("sha256"):
                raise Reject(f"malformed frozen region entry: {region!r}")
            if region["path"] in declared:
                raise Reject(f"duplicate frozen region entry: {region['path']}")
            declared[region["path"]] = region["sha256"]
        absent = [path for path in MANDATORY_FROZEN if path not in declared]
        if absent:
            raise Reject(
                "execution map omits mandatory frozen region(s): " + ", ".join(absent)
            )
        for path, expected in sorted(declared.items()):
            file_path = repo / path
            entry = {"path": path, "expected_sha256": expected, "present": file_path.exists()}
            if not file_path.exists():
                entry["status"] = "missing"
                payload["missing"].append(path)
                payload["frozen_regions"].append(entry)
                continue
            actual = sha256_file(file_path)
            entry["actual_sha256"] = actual
            entry["status"] = "unchanged" if actual == expected else "changed"
            if actual != expected:
                payload["changed"].append(path)
            payload["frozen_regions"].append(entry)
        markers = execution_map.get("wip_markers")
        if markers is None:
            markers = []
        if not isinstance(markers, list):
            raise Reject("execution map wip_markers must be a list")
        marker_names = set()
        for marker in markers:
            if not isinstance(marker, dict) or not marker.get("path") or not marker.get("marker"):
                raise Reject(f"malformed wip marker entry: {marker!r}")
            marker_names.add(marker["marker"])
            file_path = repo / marker["path"]
            entry = {
                "path": marker["path"],
                "marker": marker["marker"],
                "present": file_path.exists(),
            }
            if not file_path.exists():
                entry["status"] = "missing"
                payload["missing"].append(marker["path"])
                payload["wip_markers"].append(entry)
                continue
            actual = sha256_file(file_path)
            entry["actual_sha256"] = actual
            entry["expected_sha256"] = marker.get("sha256")
            text = file_path.read_text(encoding="utf-8", errors="replace")
            entry["marker_present"] = marker["marker"] in text
            hash_ok = marker.get("sha256") in (None, actual)
            entry["status"] = (
                "unchanged" if hash_ok and entry["marker_present"] else "changed"
            )
            if entry["status"] == "changed":
                payload["changed"].append(marker["path"])
            payload["wip_markers"].append(entry)
        absent_markers = [
            name for name in MANDATORY_WIP_MARKERS if name not in marker_names
        ]
        if absent_markers:
            raise Reject(
                "execution map omits mandatory WIP marker(s): " + ", ".join(absent_markers)
            )
        if payload["missing"]:
            payload["status"] = "missing"
            payload["exit_code"] = EXIT_MALFORMED
        elif payload["changed"]:
            payload["status"] = "changed"
            payload["exit_code"] = EXIT_NUMERIC
        else:
            payload["status"] = "preserved"
            payload["exit_code"] = EXIT_OK
    except Reject as exc:
        payload["status"] = "malformed"
        payload["exit_code"] = EXIT_MALFORMED
        payload["error"] = str(exc)
        print(f"ERROR: {exc}", file=sys.stderr)
    write_json(output, payload)
    print(f"invariants: {payload['status']} -> {output}")
    return payload["exit_code"]


# ------------------------------------------------------------------------------------------------
# CLI
# ------------------------------------------------------------------------------------------------
def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="t244_evidence.py",
        description=(
            "T244 (#244) warm-CTE measurement evidence harness: measure rounds, evaluate the "
            "same-round allocation ratios and tiers, attribute the WarmDecompose phases, check "
            "docs wording against the measured tier, and verify the frozen regions."
        ),
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    measure_parser = subparsers.add_parser(
        "measure",
        help="run one pinned round under the canonical host lock and stage its artifacts",
        description=(
            "Run the three pinned invocations of one round under "
            "/tmp/opencode/nextorm-bdn-host.lock and stage only each invocation's new "
            "artifacts. Exit 0 valid, 2 invalid/refused (everything recorded), 1 usage error."
        ),
    )
    measure_parser.add_argument("--repo", default=".", help="repository root (default: .)")
    measure_parser.add_argument(
        "--phase", required=True, choices=("baseline", "candidate"), help="measured phase"
    )
    measure_parser.add_argument("--round", required=True, type=int, help="round number")
    measure_parser.add_argument("--output", required=True, help="round output directory")
    measure_parser.add_argument(
        "--lock-timeout",
        type=float,
        default=0.0,
        help="seconds to wait for the host lock (default: 0 = fail immediately)",
    )
    measure_parser.add_argument(
        "--invocation-timeout",
        type=float,
        default=3600.0,
        help="per-invocation wall-clock limit in seconds (default: 3600)",
    )

    evaluate_parser = subparsers.add_parser(
        "evaluate",
        help="pair same-round arms, apply per-class divisors and the requested tier",
        description=(
            "Compare baseline and candidate phase directories. Exit 0 = requested tier fully "
            "passed, 2 = numeric requirement unmet, 1 = malformed/missing evidence."
        ),
    )
    evaluate_parser.add_argument("--baseline", required=True, help="baseline phase directory")
    evaluate_parser.add_argument("--candidate", required=True, help="candidate phase directory")
    evaluate_parser.add_argument("--output", required=True, help="evaluation.json path")
    evaluate_parser.add_argument(
        "--tier",
        choices=("final", "checkpoint"),
        default="final",
        help="requested acceptance tier (default: final)",
    )
    evaluate_parser.add_argument(
        "--min-rounds", type=int, default=DEFAULT_MIN_ROUNDS,
        help="minimum number of valid rounds per phase (default: 2)",
    )
    evaluate_parser.add_argument(
        "--repo", default=None, help="repository root for the divisor source contract"
    )

    attribute_parser = subparsers.add_parser(
        "attribute",
        help="decompose one WarmDecompose round and rank eligible components",
        description=(
            "Build the per-family phase table, the unattributed residual bound and the "
            "eligible non-frozen ranking. Exit 0 bounded, 2 bounded-re-gather-required, "
            "1 malformed/missing evidence."
        ),
    )
    attribute_parser.add_argument("--round", required=True, help="round directory")
    attribute_parser.add_argument("--output", required=True, help="attribution.json path")
    attribute_parser.add_argument(
        "--evaluation", default=None, help="evaluation.json fallback for the linq2db reference"
    )

    docs_parser = subparsers.add_parser(
        "docs-check",
        help="verify the docs mapping matches the measured acceptance tier",
        description=(
            "Fail when the measured tier is checkpoint but the §9 docs claim final/success "
            "closure. Writes docs-check.json and docs-mapping.json."
        ),
    )
    docs_parser.add_argument("--repo", default=".", help="repository root")
    docs_parser.add_argument("--evaluation", required=True, help="evaluation.json path")

    invariants_parser = subparsers.add_parser(
        "invariants",
        help="hash frozen regions and owner WIP markers against the execution map",
        description=(
            "Fail closed on any change to the mandatory frozen regions or the declared "
            "owner WIP markers. Exit 0 preserved, 2 changed, 1 malformed/missing."
        ),
    )
    invariants_parser.add_argument("--repo", default=".", help="repository root")
    invariants_parser.add_argument("--execution-map", required=True, help="execution-map.json")
    invariants_parser.add_argument("--output", required=True, help="invariants.json path")

    return parser


def main(argv=None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        if args.command == "measure":
            return measure(args)
        if args.command == "evaluate":
            return evaluate(args)
        if args.command == "attribute":
            return attribute(args)
        if args.command == "docs-check":
            return docs_check(args)
        if args.command == "invariants":
            return invariants(args)
    except Reject as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return EXIT_MALFORMED
    except KeyboardInterrupt:
        return 130
    parser.error(f"unknown command {args.command!r}")
    return EXIT_MALFORMED


if __name__ == "__main__":
    sys.exit(main())
