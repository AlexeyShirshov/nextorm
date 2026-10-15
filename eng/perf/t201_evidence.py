#!/usr/bin/env python3
"""eng/perf/t201_evidence.py — T201 (#201) cached-path parity evidence runner.

Task-specific evidence wrapper for the approved M12 #3 plan (r=2, evidence contract rv1),
unit D201-2.  One entry point per stage of the exact command registry in
``docs/specs/status/D201-cached-parity-1.md`` §"Exact command registry":

    --stage self-test    (W-S)  negative self-tests for the manifest/inventory/pairing rules
    --stage baseline     (W-B)  Release build + B-BASE + A-BASE + G-LIVE + G-REPORT
    --stage tests        (W-T)  core/dialect suites + the container integration run
    --stage candidate    (W-C)  B-CAND + A-CAND + G-LIVE + G-REPORT on the post-edit tree
    --stage parity       (W-P)  frozen ratio/noise decisions over every valid round
    --stage warm-gate    (W-W)  archived gate-evidence provenance + report-only reconciliation
    --stage acceptance   (W-A)  seven active acceptance cases, wall/Mean/Error/Allocated + ratios
    --stage api          (W-API) public-surface (compiler-metadata) baseline/candidate export + diff
    --stage coverage     (W-Q)  native coverage workflow and thresholds
    --stage manifest     (W-M)  sixteen-row manifest + schema validator + local semantic check

Exit codes (every stage):
    0  the stage completed and met its required predicates;
    1  measured failure, or invalid/missing evidence;
    2  inconclusive / operationally blocked (never fabricated results).

This wrapper never changes benchmark query bodies, the LINQ to DB version, prepared status, data
semantics, gate budgets, ``eng/perf/iteration14_gate.py``, ``eng/perf/iteration14-budgets.json`` or
toolchain configuration.  It only runs them and records exactly what they produced.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_ROOT = "artifacts/pdca/T201/rv1"

VALIDATOR = REPO_ROOT / "scripts" / "validate_inner_loop.py"
GATE_REL = "eng/perf/iteration14_gate.py"
BUDGETS_REL = "eng/perf/iteration14-budgets.json"
BDN_RESULTS = REPO_ROOT / "benchmarks" / "BenchmarkDotNet.Artifacts" / "results"
BDN_ARTIFACTS = REPO_ROOT / "benchmarks" / "BenchmarkDotNet.Artifacts"
BENCH_PROJECT = "benchmarks/nextorm.benchmark"
CORE_TESTS = "tests/nextorm.core.tests"
INTEGRATION_PROJECT = "tests/nextorm.integration.tests"

EXIT_OK = 0
EXIT_FAIL = 1
EXIT_INCONCLUSIVE = 2

STAGES = (
    "self-test",
    "baseline",
    "tests",
    "candidate",
    "parity",
    "warm-gate",
    "acceptance",
    "api",
    "coverage",
    "manifest",
)

# --- frozen contract values (docs/specs/status/D201-cached-parity-1.md, rv1) ---------------------
REQUIRED_ROWS = [f"EC201-{index:02d}" for index in range(1, 17)]
CONTRACT_RV = 1
TIME_TOLERANCE = 1.05
CONFIDENCE_MIN = 99.0
LOGICAL_OPS = 100
BDN_OPS_PER_INVOKE = 1
PINNED_CLASSES = (
    "SqliteBenchmarkWarmDecompose",
    "SqliteBenchmarkCachedPlan",
    "SqliteBenchmarkFeaturePlanBuild",
)
TOOLCHAINS = ("Default", "InProcessEmitToolchain")
BROAD_FILTER = "*SqliteBenchmarkFeaturesFairCached.B_*"
EXPORT_ASSEMBLIES = ("nextorm.core", "nextorm.postgres", "nextorm.sqlserver", "nextorm.clickhouse")
API_TEST_FILTER = "FullyQualifiedName~CachedPreparationRecipeTests"
SCENARIOS = ("Cte", "RecursiveCte", "Except", "Intersect", "Distinct")

# Historical same-host acceptance ratios recorded in the approved design
# (docs/specs/performance/m12-3-prepare-command-design.md §"Seven-case acceptance inventory").
HISTORICAL_ACCEPTANCE_RATIOS = {
    "time_cached_over_prepared": 1.87,
    "alloc_cached_over_prepared": 7.42,
}
ACCEPTANCE_GROWTH_THRESHOLD = 0.20

# --- operational budgets: exceed -> exit 2, never invent results ---------------------------------
B_BASE_BUDGET_S = 75 * 60
G_LIVE_BUDGET_S = 30 * 60
DEFAULT_STEP_TIMEOUT_S = 1800

DOCKER_SOCKET = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"

# --- benchmark-project guard ---------------------------------------------------------------------
# BDN's out-of-process (Default) toolchain resolves the benchmark project by searching the current
# directory tree for `nextorm.benchmark.csproj` and fails closed when it finds more than one:
#     System.NotSupportedException: Found more than one matching project file for nextorm.benchmark
# Stale full-repo copies kept as evidence by *other* units (artifacts/pdca/D208/**, D236/**) trip
# this.  The guard never edits those copies: it moves the offending directories out of the tree with
# a byte-preserving rename for the duration of the measurement and always restores them, journaling
# before/after identity.  It is opt-in (fail-closed by default) so the registry commands themselves
# stay exactly as approved.
BENCH_PROJECT_FILE = "nextorm.benchmark.csproj"
QUARANTINE_ENV = "NEXTORM_T201_QUARANTINE"
QUARANTINE_ROOT_ENV = "NEXTORM_T201_QUARANTINE_DIR"
QUARANTINE_DEFAULT_ROOT = "/tmp/opencode/t201-bench-project-quarantine"
WALK_PRUNED = (".git", "bin", "obj", "TestResults", "node_modules", ".vs", ".idea")
PROVIDER_NAMES = ("postgres", "sql server", "sqlserver", "mysql", "mariadb", "clickhouse")
PROVIDER_SKIP_RES = (
    re.compile(r"(?i)\b(" + "|".join(re.escape(n) for n in PROVIDER_NAMES) + r")\b[^\n]{0,120}?\bskip"),
    re.compile(r"(?i)\bskip[^\n]{0,120}?\b(" + "|".join(re.escape(n) for n in PROVIDER_NAMES) + r")\b"),
)
TOTAL_LINE_RE = re.compile(r"^\s*total:\s*(\d+)\s*$", re.MULTILINE)


class EvidenceError(Exception):
    """Measured failure or invalid/missing evidence (exit 1)."""


class Inconclusive(Exception):
    """Operationally blocked, or cannot decide (exit 2)."""


# ------------------------------------------------------------------------------------------------
# Utilities
# ------------------------------------------------------------------------------------------------
def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def log(message: str) -> None:
    print(f"[t201] {message}", flush=True)


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path) -> str:
    return sha256_bytes(Path(path).read_bytes())


def read_json(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path, payload):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return path


def write_text(path, text: str):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def rel(path) -> str:
    try:
        return str(Path(path).resolve().relative_to(REPO_ROOT))
    except ValueError:
        return str(path)


def is_number(value) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def is_int(value) -> bool:
    return isinstance(value, int) and not isinstance(value, bool)


def git_output(*args: str):
    completed = subprocess.run(
        ["git", *args], cwd=str(REPO_ROOT), stdout=subprocess.PIPE, stderr=subprocess.STDOUT
    )
    return completed.returncode, completed.stdout.decode("utf-8", "replace")


def tree_identity() -> dict:
    _, head = git_output("rev-parse", "HEAD")
    _, head_tree = git_output("rev-parse", "HEAD^{tree}")
    _, branch = git_output("rev-parse", "--abbrev-ref", "HEAD")
    _, status = git_output("status", "--porcelain")
    lines = [line for line in status.splitlines() if line.strip()]
    return {
        "branch": branch.strip(),
        "head": head.strip(),
        "head_tree": head_tree.strip(),
        "dirty_entries": len(lines),
        "status_sha256": sha256_bytes(status.encode("utf-8")),
    }


def dir_manifest(directory: Path) -> dict:
    manifest = {}
    directory = Path(directory)
    if not directory.exists():
        return manifest
    for path in sorted(directory.rglob("*")):
        if path.is_file():
            manifest[str(path.relative_to(directory)).replace(os.sep, "/")] = sha256_file(path)
    return manifest


def restore_directory(snapshot: Path, directory: Path) -> None:
    """Make *directory* byte-identical to *snapshot* (extra files removed, modified restored)."""
    if directory.exists():
        shutil.rmtree(directory)
    shutil.copytree(snapshot, directory)


def find_foreign_benchmark_projects() -> list:
    """Every ``nextorm.benchmark.csproj`` under the repository root except the canonical one."""
    canonical = (REPO_ROOT / "benchmarks" / "nextorm.benchmark" / BENCH_PROJECT_FILE).resolve()
    found = []
    for dirpath, dirnames, filenames in os.walk(REPO_ROOT):
        dirnames[:] = [name for name in dirnames if name not in WALK_PRUNED]
        if BENCH_PROJECT_FILE in filenames:
            candidate = (Path(dirpath) / BENCH_PROJECT_FILE).resolve()
            if candidate != canonical:
                found.append(Path(dirpath))
    return sorted(set(found))


def _dir_identity(directory: Path) -> dict:
    files = 0
    total = 0
    for path in directory.rglob("*"):
        if path.is_file():
            files += 1
            total += path.stat().st_size
    return {"files": files, "bytes": total}


class BenchmarkProjectGuard:
    """Fail-closed preflight around BDN's benchmark-project resolution.

    Default: refuse to run and report the offending copies with the exact remedy.
    ``NEXTORM_T201_QUARANTINE=1``: rename them out of the tree, journal their identity, and always
    restore them (a rename inside one filesystem preserves content exactly; the journal is verified).
    """

    def __init__(self, root: Path, phase: str):
        self.root = root
        self.phase = phase
        self.journal_path = root / "instrumentation" / f"benchmark-project-quarantine-{phase}.json"
        self.conflicts = []
        self.moved = []

    def __enter__(self):
        self.conflicts = find_foreign_benchmark_projects()
        enabled = os.environ.get(QUARANTINE_ENV) == "1"
        if not self.conflicts:
            write_json(self.journal_path, {"phase": self.phase, "conflicts": [], "moved": [],
                                           "guard": "no foreign benchmark project copies"})
            return self
        if not enabled:
            write_json(self.journal_path, {
                "phase": self.phase,
                "conflicts": [rel(path) for path in self.conflicts],
                "moved": [],
                "guard": "refused",
            })
            raise Inconclusive(
                "BDN would find more than one benchmark project file under the repository root: "
                + ", ".join(rel(path) for path in self.conflicts)
                + f". Set {QUARANTINE_ENV}=1 to move them out of the tree for the duration of the "
                "run (byte-preserving rename, journaled and always restored)."
            )

        quarantine_root = Path(
            os.environ.get(QUARANTINE_ROOT_ENV, QUARANTINE_DEFAULT_ROOT)
        ) / f"{self.phase}-{int(time.time())}"
        quarantine_root.mkdir(parents=True, exist_ok=True)
        for index, directory in enumerate(self.conflicts):
            project = directory / BENCH_PROJECT_FILE
            before = _dir_identity(directory)
            target = quarantine_root / f"{index}-{directory.name}"
            shutil.move(str(directory), str(target))
            self.moved.append({
                "original": str(directory),
                "quarantine": str(target),
                "relative": rel(directory),
                "csproj_sha256": sha256_file(target / BENCH_PROJECT_FILE),
                "before": before,
                "after": _dir_identity(target),
            })
        write_json(self.journal_path, {"phase": self.phase, "guard": "quarantined (opt-in)",
                                       "conflicts": [rel(path) for path in self.conflicts],
                                       "moved": self.moved})
        log(f"benchmark-project guard: quarantined {len(self.moved)} foreign project cop(ies)")
        return self

    def __exit__(self, exc_type, exc, traceback) -> bool:
        problems = []
        for entry in self.moved:
            original = Path(entry["original"])
            target = Path(entry["quarantine"])
            if original.exists():
                problems.append(f"original path reappeared: {entry['relative']}")
                continue
            if not target.exists():
                problems.append(f"quarantined copy vanished: {entry['quarantine']}")
                continue
            shutil.move(str(target), str(original))
            after = _dir_identity(original)
            if after != entry["before"] or sha256_file(original / BENCH_PROJECT_FILE) != entry["csproj_sha256"]:
                problems.append(f"restored copy differs: {entry['relative']}")
        if self.moved:
            document = read_json(self.journal_path)
            document["restored"] = not problems
            document["restore_problems"] = problems
            document["restored_utc"] = utc_now()
            write_json(self.journal_path, document)
            log(f"benchmark-project guard: restored {len(self.moved)} cop(ies), problems={problems}")
        if problems:
            raise EvidenceError("benchmark-project restore failed: " + "; ".join(problems))
        return False


class Recorder:
    """Owns the run registry: exact commands, exits, wall times, logs and environment."""

    def __init__(self, root: Path):
        self.root = root
        self.registry_path = root / "runs.json"
        self.registry = read_json(self.registry_path) if self.registry_path.exists() else []

    def save(self) -> None:
        write_json(self.registry_path, self.registry)

    def run(
        self,
        stage: str,
        name: str,
        cmd,
        *,
        log_path: Path,
        env_set=None,
        env_unset=(),
        timeout: int = DEFAULT_STEP_TIMEOUT_S,
        cwd: Path = REPO_ROOT,
    ) -> dict:
        argv = [str(token) for token in cmd]
        env = dict(os.environ)
        for key in env_unset:
            env.pop(key, None)
        for key, value in (env_set or {}).items():
            env[key] = str(value)

        run_id = f"{stage}/{name}"
        started = utc_now()
        start = time.monotonic()
        timed_out = False
        output = b""
        exit_code = None
        try:
            completed = subprocess.run(
                argv, cwd=str(cwd), env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                timeout=timeout,
            )
            exit_code = completed.returncode
            output = completed.stdout or b""
        except subprocess.TimeoutExpired as expired:
            timed_out = True
            output = expired.stdout or b""
        wall = time.monotonic() - start

        log_path = Path(log_path)
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_path.write_bytes(output)

        record = {
            "run_id": run_id,
            "stage": stage,
            "name": name,
            "command": argv,
            "cwd": rel(cwd),
            "env_set": {key: str(value) for key, value in (env_set or {}).items()},
            "env_unset": list(env_unset),
            "started_utc": started,
            "exit_code": exit_code,
            "timed_out": timed_out,
            "wall_seconds": round(wall, 3),
            "log": rel(log_path),
            "log_sha256": sha256_file(log_path),
        }
        self.registry = [entry for entry in self.registry if entry.get("run_id") != run_id]
        self.registry.append(record)
        self.save()
        status = "TIMEOUT" if timed_out else f"exit {exit_code}"
        log(f"{run_id}: {status} in {wall:.1f}s -> {record['log']}")
        return record


# ------------------------------------------------------------------------------------------------
# BDN report parsing
# ------------------------------------------------------------------------------------------------
def toolchain_from_display(display: str) -> str:
    match = re.search(r"Toolchain=([A-Za-z0-9_.]+)", display or "")
    return match.group(1) if match else "Default"


def load_bdn_entries(report_dir: Path) -> list:
    """Parse every BDN JSON report under *report_dir* into flat measurement records."""
    report_dir = Path(report_dir)
    if not report_dir.exists():
        return []
    paths = sorted(report_dir.rglob("*-report-full-compressed.json"))
    paths += [p for p in sorted(report_dir.rglob("*-report-full.json")) if p not in paths]
    entries = []
    for path in paths:
        try:
            data = read_json(path)
        except (OSError, ValueError) as exc:
            raise EvidenceError(f"cannot read BDN report {rel(path)}: {exc}") from exc
        benchmarks = data.get("Benchmarks")
        if not isinstance(benchmarks, list):
            raise EvidenceError(f"BDN report {rel(path)} has no Benchmarks array")
        for benchmark in benchmarks:
            namespace = benchmark.get("Namespace") or ""
            short_type = benchmark.get("Type")
            method = benchmark.get("Method")
            if not short_type or not method:
                raise EvidenceError(f"BDN report {rel(path)} has an entry without Type/Method")
            full_type = f"{namespace}.{short_type}" if namespace else short_type
            statistics = benchmark.get("Statistics") or {}
            interval = statistics.get("ConfidenceInterval") or {}
            memory = benchmark.get("Memory") or {}
            entries.append(
                {
                    "full": f"{full_type}.{method}",
                    "type": full_type,
                    "method": method,
                    "mean": statistics.get("Mean"),
                    "standard_error": statistics.get("StandardError"),
                    "ci_low": interval.get("Lower"),
                    "ci_high": interval.get("Upper"),
                    "ci_level": interval.get("Level"),
                    "ci_margin": interval.get("Margin"),
                    "ci_n": interval.get("N"),
                    "allocated": memory.get("BytesAllocatedPerOperation"),
                    "operations_per_invoke": benchmark.get("OperationsPerInvoke"),
                    "toolchain": toolchain_from_display(benchmark.get("DisplayInfo") or ""),
                    "display": benchmark.get("DisplayInfo") or "",
                    "source": rel(path),
                }
            )
    return entries


def index_by_full(entries) -> tuple:
    """(full -> entry, problems) — one measurement per benchmark name."""
    indexed = {}
    problems = []
    for entry in entries:
        if entry["full"] in indexed:
            if indexed[entry["full"]]["toolchain"] == entry["toolchain"]:
                problems.append(f"duplicate result for {entry['full']}/{entry['toolchain']}")
            else:
                problems.append(
                    f"unexpected extra toolchain job for {entry['full']} ({entry['toolchain']})"
                )
            continue
        indexed[entry["full"]] = entry
    return indexed, problems


def index_by_full_toolchain(entries) -> tuple:
    indexed = {}
    problems = []
    for entry in entries:
        key = (entry["full"], entry["toolchain"])
        if key in indexed:
            problems.append(f"duplicate result for {entry['full']}/{entry['toolchain']}")
            continue
        indexed[key] = entry
    return indexed, problems


# ------------------------------------------------------------------------------------------------
# Frozen decision rules — docs/specs/performance/m12-3-prepare-command-design.md
# §"Executable ratio and noise decisions"
# ------------------------------------------------------------------------------------------------
# ------------------------------------------------------------------------------------------------
# Confidence-level verification
# ------------------------------------------------------------------------------------------------
# BDN's JSON exporter carries no percentage confidence field: `ConfidenceInterval.Level` is an
# opaque identifier that is constant (observed: 12) regardless of the sample size, while the
# exported interval itself is a two-sided Student-t interval whose `Margin / StandardError` equals
# the t-multiplier for the 99.9% level (observed 4.2208 / 4.1405 / 4.3180 for df = 13 / 14 / 12).
# The design requires "the same exported confidence level, at least 99%", so this module:
#   1. requires both arms to export the same non-null level identifier, and
#   2. proves the interval is at least a two-sided 99% t-interval arithmetically, and
#   3. records the observed level identifier and the implied confidence in the evidence.
def _betacf(a: float, b: float, x: float, max_iter: int = 300, eps: float = 3e-16) -> float:
    qab = a + b
    qap = a + 1.0
    qam = a - 1.0
    c = 1.0
    d = 1.0 - qab * x / qap
    if abs(d) < 1e-300:
        d = 1e-300
    d = 1.0 / d
    h = d
    for m in range(1, max_iter + 1):
        m2 = 2 * m
        aa = m * (b - m) * x / ((qam + m2) * (a + m2))
        d = 1.0 + aa * d
        c = 1.0 + aa / c
        if abs(d) < 1e-300:
            d = 1e-300
        if abs(c) < 1e-300:
            c = 1e-300
        d = 1.0 / d
        h *= d * c
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
        d = 1.0 + aa * d
        c = 1.0 + aa / c
        if abs(d) < 1e-300:
            d = 1e-300
        if abs(c) < 1e-300:
            c = 1e-300
        d = 1.0 / d
        delta = d * c
        h *= delta
        if abs(delta - 1.0) < eps:
            break
    return h


def _betai(a: float, b: float, x: float) -> float:
    """Regularized incomplete beta function I_x(a, b)."""
    if x <= 0.0:
        return 0.0
    if x >= 1.0:
        return 1.0
    front = math.exp(
        math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b) + a * math.log(x) + b * math.log1p(-x)
    )
    if x < (a + 1.0) / (a + b + 2.0):
        return front * _betacf(a, b, x) / a
    return 1.0 - front * _betacf(b, a, 1.0 - x) / b


def two_sided_t_confidence(t: float, df: int) -> float:
    """P(|T_df| <= t) for a Student-t variable with *df* degrees of freedom."""
    if df <= 0 or not is_number(t) or t <= 0:
        return 0.0
    return 1.0 - _betai(df / 2.0, 0.5, df / (df + t * t))


def student_t_critical(df: int, confidence: float = 0.99) -> float:
    """Two-sided Student-t critical value for *confidence* (bisection on the exact CDF)."""
    if df <= 0:
        return math.inf
    low, high = 0.0, 1000.0
    for _ in range(200):
        middle = (low + high) / 2.0
        if two_sided_t_confidence(middle, df) < confidence:
            low = middle
        else:
            high = middle
    return (low + high) / 2.0


def interval_confidence_check(entry: dict) -> dict:
    """Is this arm's exported interval at least a two-sided 99% t-interval?"""
    level = entry.get("ci_level")
    standard_error = entry.get("standard_error")
    margin = entry.get("ci_margin")
    if margin is None and is_number(entry.get("ci_low")) and is_number(entry.get("ci_high")):
        margin = (entry["ci_high"] - entry["ci_low"]) / 2.0
    n_sample = entry.get("ci_n")
    detail = {"exported_level": level, "margin": margin, "standard_error": standard_error,
              "sample_size": n_sample}
    if is_number(level) and CONFIDENCE_MIN <= float(level) <= 100.0:
        detail["satisfied"] = True
        detail["basis"] = "the exported level is already a percentage >= 99"
        return detail
    if not is_number(margin) or not is_number(standard_error) or standard_error <= 0:
        detail["satisfied"] = False
        detail["reason"] = "missing/invalid interval margin or standard error"
        return detail
    if not is_int(n_sample) or n_sample < 2:
        detail["satisfied"] = False
        detail["reason"] = "missing/invalid interval sample size"
        return detail
    df = n_sample - 1
    critical = student_t_critical(df, CONFIDENCE_MIN / 100.0)
    observed = margin / standard_error
    detail.update({
        "degrees_of_freedom": df,
        "t_multiplier": observed,
        "t_critical_99": critical,
        "implied_confidence": two_sided_t_confidence(observed, df),
        "satisfied": observed + 1e-9 >= critical,
        "basis": "arithmetic: Margin/StandardError vs the two-sided 99% t critical value",
    })
    if not detail["satisfied"]:
        detail["reason"] = "exported interval is narrower than a two-sided 99% interval"
    return detail


def time_decision(nextorm: dict, linq: dict) -> tuple:
    detail = {
        "nextorm_mean": nextorm.get("mean"),
        "linq_mean": linq.get("mean"),
        "nextorm_ci": [nextorm.get("ci_low"), nextorm.get("ci_high")],
        "linq_ci": [linq.get("ci_low"), linq.get("ci_high")],
        "confidence_levels": [nextorm.get("ci_level"), linq.get("ci_level")],
    }
    values = [
        nextorm.get("mean"), linq.get("mean"),
        nextorm.get("ci_low"), nextorm.get("ci_high"),
        linq.get("ci_low"), linq.get("ci_high"),
    ]
    if any(not is_number(value) or value <= 0 for value in values):
        detail["reason"] = "missing/non-positive/non-finite mean or CI bound"
        return "OPEN", detail

    levels = [nextorm.get("ci_level"), linq.get("ci_level")]
    if any(level is None for level in levels):
        detail["reason"] = "missing confidence level"
        return "OPEN", detail
    if levels[0] != levels[1]:
        detail["reason"] = "arms do not export the same confidence level"
        return "OPEN", detail
    checks = [interval_confidence_check(nextorm), interval_confidence_check(linq)]
    detail["confidence_checks"] = checks
    if not all(check["satisfied"] for check in checks):
        detail["reason"] = "at least one arm's exported interval is not verified at >= 99% confidence"
        return "OPEN", detail

    point = nextorm["mean"] / linq["mean"]
    ratio_low = nextorm["ci_low"] / linq["ci_high"]
    ratio_high = nextorm["ci_high"] / linq["ci_low"]
    detail.update({"point": point, "ratio_low": ratio_low, "ratio_high": ratio_high})
    if point <= TIME_TOLERANCE and ratio_high <= TIME_TOLERANCE:
        return "MET", detail
    if point > TIME_TOLERANCE and ratio_low > TIME_TOLERANCE:
        return "FAIL", detail
    return "OPEN", detail


def allocation_decision(nextorm_bytes, linq_bytes) -> tuple:
    detail = {"nextorm_bytes": nextorm_bytes, "linq_bytes": linq_bytes}
    if not is_int(nextorm_bytes) or not is_int(linq_bytes) or nextorm_bytes < 0 or linq_bytes < 0:
        detail["reason"] = "missing/invalid raw allocated bytes"
        return "FAIL", detail
    if linq_bytes == 0:
        return ("MET" if nextorm_bytes == 0 else "FAIL"), detail
    return ("MET" if nextorm_bytes <= linq_bytes else "FAIL"), detail


def combine_rounds(time_decisions, allocation_decisions) -> str:
    """MET only if every round is MET; FAIL if every time round is decisively FAIL or any round
    fails allocations; otherwise OPEN."""
    if not time_decisions:
        return "OPEN"
    if all(decision == "MET" for decision in time_decisions) and all(
        decision == "MET" for decision in allocation_decisions
    ):
        return "MET"
    if all(decision == "FAIL" for decision in time_decisions) or any(
        decision == "FAIL" for decision in allocation_decisions
    ):
        return "FAIL"
    return "OPEN"


# ------------------------------------------------------------------------------------------------
# Inventory / pairing validation (EC201-02..06, 08)
# ------------------------------------------------------------------------------------------------
def category_of(oracle_b_arms: dict) -> dict:
    return {method["name"]: method["category"] for method in oracle_b_arms["methods"]}


def extract_category_b(report_dir: Path, oracle_b_arms: dict) -> dict:
    """Extract the 55-arm selection and the exact ten-arm map from real BDN reports."""
    arms, problems = index_by_full(
        [
            entry
            for entry in load_bdn_entries(report_dir)
            if entry["type"].endswith("SqliteBenchmarkFeaturesFairCached")
            and entry["method"].startswith("B_")
        ]
    )
    expected = {
        f"NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.{method['name']}"
        for method in oracle_b_arms["methods"]
    }
    missing = sorted(expected - set(arms))
    undeclared = sorted(set(arms) - expected)
    if missing:
        problems.append(f"{len(missing)} inventory arms missing from the run: {missing[:6]}")
    if undeclared:
        problems.append(f"{len(undeclared)} undeclared arms selected: {undeclared[:6]}")

    categories = category_of(oracle_b_arms)
    pairs = {}
    for mapping in oracle_b_arms["ten_arm_map"]:
        scenario = mapping["scenario"]
        nextorm = arms.get(mapping["nextorm"]["full"])
        linq = arms.get(mapping["linq2db"]["full"])
        if nextorm is None or linq is None:
            problems.append(f"{scenario}: missing arm(s) in the run")
            continue
        if not nextorm["method"].startswith("B_Nextorm_Cached_"):
            problems.append(f"{scenario}: NextORM arm {nextorm['method']!r} is not a cached NextORM arm")
        if not linq["method"].startswith("B_Linq2Db_"):
            problems.append(f"{scenario}: competitor arm {linq['method']!r} is not a LINQ to DB arm")
        if categories.get(nextorm["method"]) != f"B_{scenario}":
            problems.append(f"{scenario}: NextORM arm category is {categories.get(nextorm['method'])!r}")
        if categories.get(linq["method"]) != f"B_{scenario}":
            problems.append(f"{scenario}: competitor arm category is {categories.get(linq['method'])!r}")
        pairs[scenario] = {"nextorm": nextorm, "linq2db": linq}

    return {
        "source": rel(report_dir),
        "selected_count": len(arms),
        "expected_count": len(expected),
        "reported_source_count": oracle_b_arms["total_b_methods"],
        "pairs": pairs,
        "problems": problems,
    }


def extraction_ok(extraction: dict) -> bool:
    return (
        not extraction["problems"]
        and extraction["selected_count"] == extraction["expected_count"]
        and extraction["expected_count"] == extraction["reported_source_count"]
        and len(extraction["pairs"]) == len(SCENARIOS)
    )


def extract_acceptance(report_dir: Path, oracle_acceptance: dict) -> dict:
    entries, problems = index_by_full(load_bdn_entries(report_dir))
    active = oracle_acceptance["active"]
    cases = []
    missing = []
    for case in active:
        entry = entries.get(case["case"])
        if entry is None:
            missing.append(case["case"])
            continue
        cases.append(
            {
                "case": case["case"],
                "source": case["source"],
                "mean": entry["mean"],
                "standard_error": entry["standard_error"],
                "ci_low": entry["ci_low"],
                "ci_high": entry["ci_high"],
                "allocated": entry["allocated"],
                "toolchain": entry["toolchain"],
                "noisy": (
                    not is_number(entry["standard_error"])
                    or not is_number(entry["mean"])
                    or entry["standard_error"] >= entry["mean"]
                    or (is_number(entry["ci_low"]) and entry["ci_low"] <= 0)
                ),
            }
        )
    problems.extend(f"missing acceptance case: {name}" for name in missing)
    return {
        "source": rel(report_dir),
        "case_count": len(cases),
        "expected_count": len(active),
        "cases": cases,
        "missing": missing,
        "control": oracle_acceptance["control"],
        "selected_names": sorted(entries),
        "problems": problems,
    }


def acceptance_ok(extraction: dict) -> bool:
    return extraction["case_count"] == extraction["expected_count"] == 7 and not extraction["missing"]


def acceptance_ratios(extraction: dict) -> dict:
    by_case = {case["case"]: case for case in extraction["cases"]}
    cached = by_case.get("NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_ToList")
    prepared = by_case.get("NextORM.Benchmark.SqliteBenchmarkCachedPlan.Prepared_ToList")
    if cached is None or prepared is None:
        return {}
    ratios = {}
    if is_number(cached["mean"]) and is_number(prepared["mean"]) and prepared["mean"] > 0:
        ratios["time_cached_over_prepared"] = cached["mean"] / prepared["mean"]
    if is_int(cached["allocated"]) and is_int(prepared["allocated"]) and prepared["allocated"] > 0:
        ratios["alloc_cached_over_prepared"] = cached["allocated"] / prepared["allocated"]
    return ratios


# ------------------------------------------------------------------------------------------------
# Native gate evidence validation (EC201-07)
# ------------------------------------------------------------------------------------------------
def validate_gate_evidence(fresh_dir: Path, inventory: dict, live_exit, report_exit, recorded=None) -> dict:
    """Independent check of the *fresh* pinned-class reports: coverage, zero rows, the 840 B/op
    budget row, the pinned normalization and both toolchains."""
    fresh_dir = Path(fresh_dir)
    problems = []
    if live_exit is None:
        problems.append("gate live run produced no exit code")
    elif live_exit != 0:
        problems.append(f"gate live run exited {live_exit}")
    if report_exit is None:
        problems.append("gate report-only reconciliation produced no exit code")
    elif report_exit != 0:
        problems.append(f"gate report-only reconciliation exited {report_exit}")

    entries, index_problems = index_by_full_toolchain(load_bdn_entries(fresh_dir))
    problems.extend(index_problems)
    if not entries:
        problems.append(f"no fresh pinned-class reports under {rel(fresh_dir)}")

    rows = []
    for row in inventory["rows"]:
        full = f"{row['type']}.{row['method']}"
        for toolchain in TOOLCHAINS:
            match = entries.get((full, toolchain))
            if match is None:
                problems.append(f"missing fresh gate result {row['id']}/{toolchain}")
                continue
            allocated = match["allocated"]
            operations = match["operations_per_invoke"]
            if operations is not None and operations != BDN_OPS_PER_INVOKE:
                problems.append(
                    f"{row['id']}/{toolchain}: OperationsPerInvoke={operations!r} != {BDN_OPS_PER_INVOKE}"
                )
            if not is_int(allocated) or allocated < 0:
                problems.append(f"{row['id']}/{toolchain}: invalid allocation {allocated!r}")
                continue
            bytes_per_op = allocated / inventory["logical_ops_per_invocation"]
            ok = bytes_per_op <= row["max_bytes_per_op"]
            rows.append(
                {
                    "id": row["id"],
                    "toolchain": toolchain,
                    "raw_bytes": allocated,
                    "normalization": inventory["logical_ops_per_invocation"],
                    "bytes_per_op": bytes_per_op,
                    "kind": row["kind"],
                    "budget": row["max_bytes_per_op"],
                    "ok": ok,
                }
            )
            if not ok:
                problems.append(
                    f"over budget {row['id']}/{toolchain}: {bytes_per_op:.2f} > {row['max_bytes_per_op']}"
                )

    for row_id in inventory["zero_rows"]:
        for toolchain in TOOLCHAINS:
            verdict = next((r for r in rows if r["id"] == row_id and r["toolchain"] == toolchain), None)
            if verdict is not None and verdict["raw_bytes"] != 0:
                problems.append(f"zero row {row_id}/{toolchain} is {verdict['raw_bytes']} B, not 0")

    row_ids = {row["id"] for row in inventory["rows"]}
    row_840 = next((row for row in inventory["rows"] if row["id"] == "InAtIn_Inline_Warm_Reused"), None)
    if row_840 is None:
        problems.append("the 840 B/op budget row InAtIn_Inline_Warm_Reused is absent from the inventory")
    elif row_840["kind"] != "budget" or row_840["max_bytes_per_op"] != 840:
        problems.append("InAtIn_Inline_Warm_Reused must stay the 840 B/op budget row, not a zero row")
    if len(inventory["zero_rows"]) != 3:
        problems.append(f"expected exactly 3 zero rows, inventory declares {len(inventory['zero_rows'])}")
    if inventory["logical_ops_per_invocation"] != LOGICAL_OPS:
        problems.append(
            f"normalization divisor is {inventory['logical_ops_per_invocation']}, expected {LOGICAL_OPS}"
        )
    if inventory.get("bdn_operations_per_invoke") != BDN_OPS_PER_INVOKE:
        problems.append(
            f"bdn_operations_per_invoke is {inventory.get('bdn_operations_per_invoke')}, "
            f"expected {BDN_OPS_PER_INVOKE}"
        )

    # A recorded gate-evidence document must declare the pinned normalization and be
    # arithmetically consistent with the raw reports.
    if recorded is not None:
        if recorded.get("normalization") != LOGICAL_OPS:
            problems.append(
                f"recorded gate normalization is {recorded.get('normalization')!r}, expected {LOGICAL_OPS}"
            )
        recorded_rows = recorded.get("rows")
        if not isinstance(recorded_rows, list) or not recorded_rows:
            problems.append("recorded gate evidence has no rows")
        else:
            by_key = {(r.get("id"), r.get("toolchain")): r for r in recorded_rows}
            for verdict in rows:
                other = by_key.get((verdict["id"], verdict["toolchain"]))
                if other is None:
                    problems.append(f"recorded gate evidence lacks {verdict['id']}/{verdict['toolchain']}")
                    continue
                divisor = recorded.get("normalization")
                if not is_int(divisor) or divisor <= 0:
                    continue
                if other.get("raw_bytes") != verdict["raw_bytes"]:
                    problems.append(
                        f"recorded raw bytes differ for {verdict['id']}/{verdict['toolchain']}"
                    )
                expected = verdict["raw_bytes"] / divisor
                if not is_number(other.get("bytes_per_op")) or abs(other["bytes_per_op"] - expected) > 1e-6:
                    problems.append(
                        f"recorded normalization inconsistent for {verdict['id']}/{verdict['toolchain']}"
                    )

    return {
        "fresh_dir": rel(fresh_dir),
        "live_exit": live_exit,
        "report_exit": report_exit,
        "row_count": len(rows),
        "expected_row_count": len(inventory["rows"]) * len(TOOLCHAINS),
        "toolchains": list(TOOLCHAINS),
        "normalization": inventory["logical_ops_per_invocation"],
        "bdn_operations_per_invoke": inventory.get("bdn_operations_per_invoke"),
        "zero_rows": inventory["zero_rows"],
        "budget_840_row": "InAtIn_Inline_Warm_Reused",
        "rows": rows,
        "problems": problems,
    }


def gate_evidence_ok(evidence: dict) -> bool:
    return not evidence["problems"] and evidence["row_count"] == evidence["expected_row_count"]


def load_recorded_gate(fresh_dir: Path):
    """The wrapper's own gate-evidence document for the phase owning *fresh_dir*."""
    candidate = Path(fresh_dir).parent / "gate-evidence.json"
    if candidate.exists():
        try:
            return read_json(candidate)
        except (OSError, ValueError):
            return None
    return None


# ------------------------------------------------------------------------------------------------
# API inventory validation (EC201-12)
# ------------------------------------------------------------------------------------------------
def validate_api_inventory(path) -> dict:
    path = Path(path)
    problems = []
    if not path.exists():
        return {"path": rel(path), "present": False, "problems": [f"missing API export {rel(path)}"]}
    try:
        document = read_json(path)
    except (OSError, ValueError) as exc:
        return {"path": rel(path), "present": True, "problems": [f"unreadable API export: {exc}"]}
    assemblies = document.get("assemblies")
    if not isinstance(assemblies, list) or not assemblies:
        problems.append("API export has no assemblies")
        assemblies = []
    present = [a for a in assemblies if isinstance(a, dict) and a.get("present")]
    if not present:
        problems.append("API export has no present assembly")
    total_types = 0
    for assembly in present:
        types = assembly.get("public_types")
        if not isinstance(types, list) or not types:
            problems.append(f"{assembly.get('name')}: empty public type list")
            continue
        if assembly.get("public_type_count") != len(types):
            problems.append(f"{assembly.get('name')}: public_type_count does not match public_types")
        total_types += len(types)
    digest = document.get("sha256")
    if not isinstance(digest, str) or len(digest) != 64:
        problems.append("API export has no 64-hex sha256 digest")
    if document.get("task") != "T201":
        problems.append("API export task is not T201")
    return {
        "path": rel(path),
        "present": True,
        "label": document.get("label"),
        "assemblies": [a.get("name") for a in present],
        "public_type_count": total_types,
        "sha256": sha256_file(path),
        "inventory_sha256": digest,
        "capture": document.get("capture"),
        "problems": problems,
    }


def api_surface(document: dict) -> dict:
    surface = {}
    for assembly in document.get("assemblies", []):
        if not isinstance(assembly, dict) or not assembly.get("present"):
            continue
        surface[assembly.get("name")] = {
            "version": assembly.get("version"),
            "types": {
                t.get("full_name"): {
                    "type_kind": t.get("type_kind"),
                    "modifiers": t.get("modifiers"),
                    "base_type": t.get("base_type"),
                    "interfaces": t.get("interfaces"),
                    "members": t.get("members"),
                }
                for t in assembly.get("public_types", [])
                if isinstance(t, dict)
            },
        }
    return surface


def api_diff(baseline_path, candidate_path) -> dict:
    base = api_surface(read_json(baseline_path))
    cand = api_surface(read_json(candidate_path))
    added, removed, changed = [], [], []
    for name in sorted(set(base) | set(cand)):
        base_assembly = base.get(name)
        cand_assembly = cand.get(name)
        if base_assembly is None:
            added.append(f"assembly {name}")
            continue
        if cand_assembly is None:
            removed.append(f"assembly {name}")
            continue
        base_types = base_assembly["types"]
        cand_types = cand_assembly["types"]
        for type_name in sorted(set(base_types) | set(cand_types)):
            if type_name not in base_types:
                added.append(type_name)
            elif type_name not in cand_types:
                removed.append(type_name)
            elif base_types[type_name] != cand_types[type_name]:
                changed.append(type_name)
    return {
        "baseline": rel(baseline_path),
        "candidate": rel(candidate_path),
        "added": added,
        "removed": removed,
        "changed": changed,
        "equal": not (added or removed or changed),
    }


# ------------------------------------------------------------------------------------------------
# Typed evidence validators
# ------------------------------------------------------------------------------------------------
def test_selection_count(log_path) -> int:
    log_path = Path(log_path)
    if not log_path.exists():
        return 0
    text = log_path.read_text(encoding="utf-8", errors="replace")
    totals = [int(match) for match in TOTAL_LINE_RE.findall(text)]
    return max(totals) if totals else 0


def validate_tests_evidence(payload_path) -> dict:
    payload_path = Path(payload_path)
    problems = []
    if not payload_path.exists():
        return {"path": rel(payload_path), "problems": [f"missing tests evidence {rel(payload_path)}"]}
    payload = read_json(payload_path)
    if payload.get("exit_code") != 0:
        problems.append(f"tests run exit code {payload.get('exit_code')}")
    skipped = payload.get("skipped_required") or []
    if skipped:
        problems.append(f"required provider(s) skipped: {skipped}")
    if not payload.get("selected_count"):
        problems.append("tests evidence has no selected_count")
    return {"path": rel(payload_path), "skipped_required": skipped, "problems": problems}


def scan_provider_skips(log_path) -> list:
    log_path = Path(log_path)
    if not log_path.exists():
        return []
    text = log_path.read_text(encoding="utf-8", errors="replace")
    found = set()
    for pattern in PROVIDER_SKIP_RES:
        for match in pattern.finditer(text):
            found.add(match.group(1).lower())
    return sorted(found)


def validate_build_log(log_path) -> dict:
    log_path = Path(log_path)
    problems = []
    if not log_path.exists():
        return {"path": rel(log_path), "problems": [f"missing build log {rel(log_path)}"]}
    text = log_path.read_text(encoding="utf-8", errors="replace")
    if "0 Error(s)" not in text:
        problems.append("build log does not report 0 Error(s)")
    if "0 Warning(s)" not in text:
        problems.append("build log does not report 0 Warning(s)")
    if "Build succeeded" not in text:
        problems.append("build log does not report success")
    return {"path": rel(log_path), "problems": problems}


def validate_parity_evidence(payload_path) -> dict:
    payload_path = Path(payload_path)
    if not payload_path.exists():
        return {"problems": [f"missing parity evidence {rel(payload_path)}"]}
    payload = read_json(payload_path)
    scenarios = payload.get("scenarios")
    problems = []
    if not isinstance(scenarios, dict) or sorted(scenarios) != sorted(SCENARIOS):
        return {"problems": [f"parity evidence does not cover exactly {SCENARIOS}"]}
    for scenario in SCENARIOS:
        decision = scenarios[scenario].get("decision") if isinstance(scenarios[scenario], dict) else None
        if decision not in ("MET", "FAIL", "OPEN"):
            problems.append(f"{scenario}: invalid decision {decision!r}")
        elif decision != "MET":
            problems.append(f"{scenario}: decision is {decision}")
    return {"problems": problems, "decisions": {name: scenarios[name].get("decision") for name in SCENARIOS}}


def validate_typed_evidence(kind, path, item) -> list:
    """Dispatch one typed evidence item to its own validator (returns a problem list)."""
    path = Path(path)
    if kind in (None, "file", "review", "report", "discovery", "log"):
        return []
    if not path.is_file():
        return [f"typed evidence {kind!r} must be a file"]
    try:
        if kind == "build":
            return validate_build_log(path)["problems"]
        if kind == "tests":
            return validate_tests_evidence(path)["problems"]
        if kind == "api":
            return validate_api_inventory(path)["problems"]
        if kind == "parity":
            return validate_parity_evidence(path)["problems"]
        if kind == "gate":
            # <root>/<phase>/iteration14/fresh/<report>.json -> <root>
            root = path.parents[3]
            inventory = read_json(root / "oracle" / "iteration14-inventory.json")
            recorded = load_recorded_gate(path.parent)
            evidence = validate_gate_evidence(path.parent, inventory, 0, 0, recorded)
            return evidence["problems"]
        if kind == "b-arms":
            # <root>/oracle/b-arms.json
            root = path.parents[1]
            phase = item.get("phase", "baseline")
            extraction = extract_category_b(root / phase / "category-b" / "results", read_json(path))
            return extraction["problems"] + (
                [] if extraction_ok(extraction) else ["category B selection/pairing check failed"]
            )
        if kind == "acceptance":
            root = path.parents[1]
            phase = item.get("phase", "baseline")
            extraction = extract_acceptance(root / phase / "acceptance" / "results", read_json(path))
            return extraction["problems"] + (
                [] if acceptance_ok(extraction) else ["acceptance inventory is not exactly seven cases"]
            )
    except (EvidenceError, OSError, ValueError, KeyError) as exc:
        return [f"typed evidence {kind!r} unusable: {exc}"]
    return [] if path.stat().st_size > 0 else [f"empty evidence file {rel(path)}"]


# ------------------------------------------------------------------------------------------------
# Manifest semantics — the stricter local T201 PASS check
# ------------------------------------------------------------------------------------------------
def manifest_schema_exit(manifest_path) -> int:
    completed = subprocess.run(
        [sys.executable, str(VALIDATOR), "manifest", str(manifest_path)],
        cwd=str(REPO_ROOT), stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
    )
    return completed.returncode


def validate_manifest_semantics(manifest_path, root: Path) -> dict:
    """Schema validity plus the mandatory semantic checks of the rv1 evidence contract.

    A local PASS requires: schema-valid; ``required_rows`` exactly the frozen sixteen IDs;
    ``contract_rv`` integer 1; rows exactly covering them once each; every ``met`` row with
    nonempty evidence whose path exists; every typed evidence validator passing (including
    recorded exits, run identity, report hashes, pairing, inventories and normalization);
    and no ``open``/``na`` row.
    """
    manifest_path = Path(manifest_path)
    problems = []
    schema_exit = manifest_schema_exit(manifest_path)
    if schema_exit != 0:
        problems.append(f"schema validator exited {schema_exit}")
    try:
        manifest = read_json(manifest_path)
    except (OSError, ValueError) as exc:
        return {"schema_exit": schema_exit, "problems": [f"unreadable manifest: {exc}"], "pass": False}

    contract_rv = manifest.get("contract_rv")
    if not is_int(contract_rv) or contract_rv != CONTRACT_RV:
        problems.append(f"contract_rv must be integer {CONTRACT_RV}, got {contract_rv!r}")

    required = manifest.get("required_rows")
    if not isinstance(required, list) or sorted(required) != sorted(REQUIRED_ROWS):
        problems.append("required_rows does not equal the frozen sixteen IDs")

    rows = manifest.get("rows")
    if not isinstance(rows, list):
        problems.append("rows is not a list")
        return {"schema_exit": schema_exit, "problems": problems, "pass": False}

    registry = []
    if (Path(root) / "runs.json").exists():
        try:
            registry = read_json(Path(root) / "runs.json")
        except (OSError, ValueError):
            problems.append("runs.json is unreadable")
    run_ids = {entry.get("run_id"): entry for entry in registry if isinstance(entry, dict)}

    seen = set()
    for row in rows:
        if not isinstance(row, dict):
            problems.append("row is not an object")
            continue
        row_id = row.get("id")
        if row_id in seen:
            problems.append(f"duplicate row id {row_id!r}")
        seen.add(row_id)
        status = row.get("status")
        if status != "met":
            problems.append(f"{row_id}: status {status!r} forbids PASS (no open/na allowed)")
            continue
        evidence = row.get("evidence")
        if not isinstance(evidence, list) or not evidence:
            problems.append(f"{row_id}: met without evidence")
            continue
        for index, item in enumerate(evidence):
            if not isinstance(item, dict):
                problems.append(f"{row_id} evidence {index}: not an object")
                continue
            kind = item.get("kind")
            raw_path = item.get("path")
            if not isinstance(raw_path, str) or not raw_path:
                problems.append(f"{row_id} evidence {index}: no path")
                continue
            path = Path(raw_path)
            if not path.is_absolute():
                path = REPO_ROOT / path
            if not path.exists():
                problems.append(f"{row_id} evidence {index}: missing path {raw_path!r}")
                continue
            if path.is_file() and path.stat().st_size == 0:
                problems.append(f"{row_id} evidence {index}: empty file {raw_path!r}")
                continue
            expected_sha = item.get("sha256")
            if expected_sha is not None and expected_sha != sha256_file(path):
                problems.append(f"{row_id} evidence {index}: sha256 mismatch (stale report?) {raw_path!r}")
            exit_code = item.get("exit")
            if exit_code is not None and exit_code != 0:
                problems.append(f"{row_id} evidence {index}: recorded exit {exit_code}")
            item_run = item.get("run_id")
            if item_run is not None:
                record = run_ids.get(item_run)
                if record is None:
                    problems.append(f"{row_id} evidence {index}: run_id {item_run!r} is not in runs.json")
                elif record.get("exit_code") != 0:
                    problems.append(
                        f"{row_id} evidence {index}: run {item_run!r} exited {record.get('exit_code')}"
                    )
            problems.extend(
                f"{row_id} evidence {index}: {message}"
                for message in validate_typed_evidence(kind, path, item)
            )

    return {
        "schema_exit": schema_exit,
        "problems": problems,
        "row_ids": sorted(str(row.get("id")) for row in rows),
        "pass": not problems,
    }


# ------------------------------------------------------------------------------------------------
# Fixture writers (used by the negative self-tests)
# ------------------------------------------------------------------------------------------------
def write_bdn_report(directory, full: str, values: dict, *, toolchain: str = "Default"):
    directory = Path(directory)
    namespace, short_type, method = full.rsplit(".", 2)
    display = f"{short_type}.{method}"
    if toolchain != "Default":
        display += f"(Toolchain={toolchain})"
    entry = {
        "Namespace": namespace,
        "Type": short_type,
        "Method": method,
        "DisplayInfo": display,
        "Statistics": {
            "Mean": values.get("mean"),
            "StandardError": values.get("standard_error", 1.0),
            "ConfidenceInterval": {
                "Lower": values.get("ci_low"),
                "Upper": values.get("ci_high"),
                "Level": values.get("ci_level", 99.9),
            },
        },
        "Memory": {"BytesAllocatedPerOperation": values.get("allocated")},
    }
    path = directory / f"{namespace}.{short_type}-report-full-compressed.json"
    document = read_json(path) if path.exists() else {"Title": "T201 fixture", "Benchmarks": []}
    document["Benchmarks"].append(entry)
    deduplicated = {}
    for existing in document["Benchmarks"]:
        key = (existing.get("Type"), existing.get("Method"),
               toolchain_from_display(existing.get("DisplayInfo") or ""))
        deduplicated[key] = existing
    document["Benchmarks"] = list(deduplicated.values())
    write_json(path, document)
    return path


def write_gate_reports(directory, inventory: dict, toolchains, normalization: int):
    """Write one BDN-shaped report per pinned class, carrying every row of that class."""
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    for path in directory.glob("*.json"):
        path.unlink()
    by_type: dict = {}
    for row in inventory["rows"]:
        by_type.setdefault(row["type"], []).append(row)
    for type_name, rows in by_type.items():
        namespace, short_type = type_name.rsplit(".", 1)
        document = {"Title": "T201 gate fixture", "Benchmarks": []}
        for row in rows:
            for toolchain in toolchains:
                display = f"{short_type}.{row['method']}"
                if toolchain != "Default":
                    display += f"(Toolchain={toolchain})"
                document["Benchmarks"].append(
                    {
                        "Namespace": namespace,
                        "Type": short_type,
                        "Method": row["method"],
                        "DisplayInfo": display,
                        "Statistics": {"Mean": 1.0,
                                       "ConfidenceInterval": {"Lower": 1.0, "Upper": 1.0,
                                                              "Level": 99.9}},
                        "Memory": {"BytesAllocatedPerOperation": row["max_bytes_per_op"] * normalization},
                    }
                )
        write_json(directory / f"{type_name}-report-full-compressed.json", document)
    return sorted(directory.glob("*report-full-compressed.json"))


def gate_evidence_document(inventory: dict, toolchains, normalization: int) -> dict:
    rows = []
    for row in inventory["rows"]:
        raw = row["max_bytes_per_op"] * normalization
        for toolchain in toolchains:
            rows.append(
                {
                    "id": row["id"],
                    "toolchain": toolchain,
                    "raw_bytes": raw,
                    "normalization": normalization,
                    "bytes_per_op": raw / normalization,
                    "kind": row["kind"],
                    "budget": row["max_bytes_per_op"],
                    "ok": True,
                }
            )
    return {
        "normalization": normalization,
        "bdn_operations_per_invoke": BDN_OPS_PER_INVOKE,
        "toolchains": list(toolchains),
        "zero_rows": inventory["zero_rows"],
        "budget_840_row": "InAtIn_Inline_Warm_Reused",
        "rows": rows,
        "problems": [],
    }


# ------------------------------------------------------------------------------------------------
# Stages
# ------------------------------------------------------------------------------------------------
def stage_self_test(root: Path, recorder: Recorder) -> int:  # noqa: C901 - explicit fixture matrix
    """W-S: reject or distinguish every case of the §Manifest self-test matrix."""
    fixtures = root / "instrumentation" / "self-test"
    if (fixtures / "root").exists():
        shutil.rmtree(fixtures / "root")
    fixture_root = fixtures / "root"
    fixture_root.mkdir(parents=True, exist_ok=True)

    oracle_b = read_json(root / "oracle" / "b-arms.json")
    oracle_acceptance = read_json(root / "oracle" / "acceptance.json")
    gate_inventory = read_json(root / "oracle" / "iteration14-inventory.json")

    def build_valid_root(directory: Path) -> None:
        """Write a self-consistent artifact tree mirroring the real evidence layout."""
        fixture_values = {"mean": 100.0, "ci_low": 99.0, "ci_high": 101.0, "allocated": 0}
        names = {f"NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.{m['name']}"
                 for m in oracle_b["methods"]}
        for mapping in oracle_b["ten_arm_map"]:
            names.add(mapping["nextorm"]["full"])
            names.add(mapping["linq2db"]["full"])
        for full in sorted(names):
            write_bdn_report(directory / "baseline" / "category-b" / "results", full, fixture_values)
            write_bdn_report(directory / "candidate" / "category-b" / "results", full, fixture_values)

        for case in oracle_acceptance["active"]:
            write_bdn_report(directory / "baseline" / "acceptance" / "results", case["case"], fixture_values)
            write_bdn_report(directory / "candidate" / "acceptance" / "results", case["case"], fixture_values)

        for phase in ("baseline", "candidate"):
            write_gate_reports(directory / phase / "iteration14" / "fresh", gate_inventory,
                               TOOLCHAINS, LOGICAL_OPS)
            write_json(directory / phase / "iteration14" / "gate-evidence.json",
                       gate_evidence_document(gate_inventory, TOOLCHAINS, LOGICAL_OPS))

        for name in ("b-arms.json", "acceptance.json", "iteration14-inventory.json"):
            source = {"b-arms.json": oracle_b, "acceptance.json": oracle_acceptance,
                      "iteration14-inventory.json": gate_inventory}[name]
            write_json(directory / "oracle" / name, source)

        for name in ("design.md", "plan.md", "collection-reconciliation.md"):
            write_text(directory / "reviews" / name, f"# {name}\napproved\n")
        for name in ("ownership-state.md", "shared-route.md", "m9-inventory.md", "siblings.md"):
            write_text(directory / "discovery" / name, f"# {name}\ndiscovery record\n")
        write_text(directory / "build.log", "Build succeeded.\n0 Warning(s)\n0 Error(s)\n")
        write_json(directory / "instrumentation" / "self-test.json", {"cases": [], "failures": []})
        write_json(directory / "tests" / "results.json",
                   {"exit_code": 0, "selected_count": 2400, "skipped_required": []})
        write_json(directory / "coverage" / "coverage.json", {"line": 88.0, "branch": 80.0})
        for name in ("correctness.md", "shared-command.md", "cache-contract.md", "recipe-consumers.md",
                     "temp-tvp.md", "quality.md", "iteration14.json", "acceptance.json",
                     "manifest-validation.md"):
            write_text(directory / "reports" / name, f"# {name}\nrecorded\n")
        write_json(directory / "reports" / "parity.json",
                   {"scenarios": {scenario: {"decision": "MET"} for scenario in SCENARIOS}})
        write_json(directory / "reports" / "acceptance.json", {"case_count": 7})
        write_json(directory / "reports" / "iteration14.json", {"pass": True})
        inventory = {
            "task": "T201",
            "assemblies": [
                {"name": "nextorm.core", "present": True, "public_type_count": 1, "version": "1.0.9.0",
                 "public_types": [{"full_name": "NextORM.Core.QueryPlan", "present": True,
                                   "type_kind": "class", "modifiers": "public", "base_type": None,
                                   "interfaces": [], "members": ["public ctor()"]}]},
            ],
            "sha256": "0" * 64,
        }
        write_json(directory / "api" / "baseline.json", inventory)
        write_json(directory / "api" / "candidate.json", inventory)
        write_json(directory / "api" / "diff.json", {"equal": True})

    def valid_manifest(directory: Path) -> dict:
        write_json(directory / "runs.json", [
            {"run_id": "fixture/gate", "exit_code": 0},
            {"run_id": "fixture/tests", "exit_code": 0},
        ])
        fresh_report = next(
            (directory / "baseline" / "iteration14" / "fresh").glob("*report-full-compressed.json")
        )
        evidence = {
            "EC201-01": [("review", directory / "reviews" / "design.md", None),
                         ("review", directory / "reviews" / "plan.md", None)],
            "EC201-02": [("b-arms", directory / "oracle" / "b-arms.json", None)],
            "EC201-03": [("parity", directory / "reports" / "parity.json", None)],
            "EC201-04": [("parity", directory / "reports" / "parity.json", None)],
            "EC201-05": [("parity", directory / "reports" / "parity.json", None)],
            "EC201-06": [("parity", directory / "reports" / "parity.json", None)],
            "EC201-07": [("gate", fresh_report, "fixture/gate")],
            "EC201-08": [("acceptance", directory / "oracle" / "acceptance.json", None)],
            "EC201-09": [("tests", directory / "tests" / "results.json", "fixture/tests")],
            "EC201-10": [("file", directory / "reports" / "shared-command.md", None)],
            "EC201-11": [("file", directory / "reports" / "temp-tvp.md", None)],
            "EC201-12": [("api", directory / "api" / "baseline.json", None)],
            "EC201-13": [("file", directory / "reports" / "cache-contract.md", None)],
            "EC201-14": [("file", directory / "reports" / "recipe-consumers.md", None)],
            "EC201-15": [("build", directory / "build.log", None),
                         ("file", directory / "instrumentation" / "self-test.json", None)],
            "EC201-16": [("file", directory / "reviews" / "collection-reconciliation.md", None)],
        }
        rows = []
        for row_id in REQUIRED_ROWS:
            items = []
            for kind, path, run_id in evidence[row_id]:
                item = {"kind": kind, "path": rel(path)}
                if run_id:
                    item["run_id"] = run_id
                if kind == "gate":
                    item["sha256"] = sha256_file(path)
                items.append(item)
            rows.append({"id": row_id, "status": "met", "evidence": items})
        return {
            "task": "T201",
            "tree": "self-test-fixture",
            "status_file": "docs/specs/status/D201-cached-parity-1.md",
            "contract_rv": CONTRACT_RV,
            "required_rows": list(REQUIRED_ROWS),
            "rows": rows,
        }

    cases = []

    def evaluate(name: str, directory: Path, expect: str, *, schema_zero: bool, accept: bool = False) -> None:
        semantics = validate_manifest_semantics(directory / "manifest.json", directory)
        rejected = not semantics["pass"]
        if accept:
            ok = semantics["pass"] and semantics["schema_exit"] == 0
        else:
            ok = rejected and (semantics["schema_exit"] == 0) == schema_zero
        cases.append(
            {
                "name": name,
                "expect": expect,
                "schema_exit": semantics["schema_exit"],
                "pass": semantics["pass"],
                "problems": semantics["problems"][:4],
                "ok": ok,
            }
        )

    # ---- positive fixture: a truthful manifest must be accepted --------------------------------
    valid_dir = fixture_root / "valid"
    valid_dir.mkdir(parents=True, exist_ok=True)
    build_valid_root(valid_dir)
    write_json(valid_dir / "manifest.json", valid_manifest(valid_dir))
    evaluate("valid-manifest-accepted", valid_dir, "schema 0 and local PASS", schema_zero=True, accept=True)

    def mutate(name: str, expect: str, mutator, *, schema_zero: bool) -> None:
        directory = fixture_root / name
        directory.mkdir(parents=True, exist_ok=True)
        for entry in sorted(valid_dir.iterdir()):
            if entry.name == "manifest.json":
                continue
            target = directory / entry.name
            if entry.is_dir():
                shutil.copytree(entry, target)
            else:
                shutil.copy2(entry, target)
        document = json.loads((valid_dir / "manifest.json").read_text(encoding="utf-8"))
        prefix = rel(valid_dir)
        for row in document["rows"]:
            for item in row.get("evidence", []):
                if isinstance(item.get("path"), str) and item["path"].startswith(prefix):
                    item["path"] = rel(directory) + item["path"][len(prefix):]
        mutator(directory, document)
        write_json(directory / "manifest.json", document)
        evaluate(name, directory, expect, schema_zero=schema_zero)

    def drop_required_row(_directory, document):
        document["rows"].pop(5)

    def add_undeclared_row(_directory, document):
        document["rows"].append({"id": "EC201-99", "status": "open", "reason": "undeclared"})

    def duplicate_required_id(_directory, document):
        document["required_rows"].append("EC201-01")

    def duplicate_row_id(_directory, document):
        document["rows"].append(json.loads(json.dumps(document["rows"][0])))

    def contract_rv_bool(_directory, document):
        document["contract_rv"] = True

    def contract_rv_string(_directory, document):
        document["contract_rv"] = "1"

    def contract_rv_zero(_directory, document):
        document["contract_rv"] = 0

    def met_empty_evidence(_directory, document):
        document["rows"][0]["evidence"] = []

    def nonexistent_evidence_path(_directory, document):
        document["rows"][9]["evidence"] = [
            {"kind": "file", "path": "artifacts/pdca/T201/rv1/does-not-exist.md"}
        ]

    def open_without_reason(_directory, document):
        document["rows"][3]["status"] = "open"
        document["rows"][3].pop("evidence", None)

    def schema_valid_open(_directory, document):
        document["rows"][3]["status"] = "open"
        document["rows"][3]["reason"] = "missing evidence"

    def schema_valid_na(_directory, document):
        document["rows"][4]["status"] = "na"
        document["rows"][4]["predicate"] = "not applicable"

    def nonzero_command_exit(_directory, document):
        document["rows"][8]["evidence"][0]["exit"] = 3

    def provider_skip(directory, document):
        write_json(directory / "tests" / "results.json",
                   {"exit_code": 0, "selected_count": 2400, "skipped_required": ["postgres"]})

    def stale_gate_run_id(_directory, document):
        document["rows"][6]["evidence"][0]["run_id"] = "stale/old-run"

    def stale_gate_hash(_directory, document):
        document["rows"][6]["evidence"][0]["sha256"] = "f" * 64

    def wrong_toolchain(directory, document):
        fresh = Path(REPO_ROOT / document["rows"][6]["evidence"][0]["path"]).parent
        write_gate_reports(fresh, gate_inventory, ("Default",), LOGICAL_OPS)
        write_json(fresh.parent / "gate-evidence.json",
                   gate_evidence_document(gate_inventory, TOOLCHAINS, LOGICAL_OPS))

    def wrong_allocation_normalization(directory, document):
        fresh = Path(REPO_ROOT / document["rows"][6]["evidence"][0]["path"]).parent
        write_gate_reports(fresh, gate_inventory, TOOLCHAINS, 1)
        write_json(fresh.parent / "gate-evidence.json",
                   gate_evidence_document(gate_inventory, TOOLCHAINS, 1))

    def wrong_b_arm_pairing(directory, document):
        oracle_path = directory / "oracle" / "b-arms.json"
        element = read_json(oracle_path)
        for mapping in element["ten_arm_map"]:
            if mapping["scenario"] == "Cte":
                mapping["linq2db"] = next(
                    m["linq2db"] for m in element["ten_arm_map"] if m["scenario"] == "Distinct"
                )
        write_json(oracle_path, element)

    def wrong_acceptance_inventory(directory, document):
        oracle_path = directory / "oracle" / "acceptance.json"
        element = read_json(oracle_path)
        element["active"] = element["active"][:6]
        write_json(oracle_path, element)

    matrix = (
        ("missing-required-row", "schema rejects (required row absent)", drop_required_row, False),
        ("undeclared-row", "schema rejects (undeclared row id)", add_undeclared_row, False),
        ("duplicate-required-id", "schema rejects (duplicate required_rows entry)",
         duplicate_required_id, False),
        ("duplicate-row-id", "schema rejects (duplicate row id)", duplicate_row_id, False),
        ("contract-rv-bool", "schema rejects (contract_rv is a bool)", contract_rv_bool, False),
        ("contract-rv-string", "schema rejects (contract_rv is a string)", contract_rv_string, False),
        ("contract-rv-zero", "schema rejects (contract_rv is 0)", contract_rv_zero, False),
        ("met-empty-evidence", "schema rejects (met without evidence)", met_empty_evidence, False),
        ("nonexistent-evidence-path", "schema 0, local PASS rejects (path absent)",
         nonexistent_evidence_path, True),
        ("open-without-reason", "schema rejects (open without reason)", open_without_reason, False),
        ("schema-valid-open", "schema 0, local PASS rejects (open forbids PASS)", schema_valid_open, True),
        ("schema-valid-na", "schema 0, local PASS rejects (unconditional row cannot be na)",
         schema_valid_na, True),
        ("nonzero-command-exit", "schema 0, local PASS rejects (recorded exit 3)",
         nonzero_command_exit, True),
        ("provider-skip", "schema 0, local PASS rejects (required provider skipped)",
         provider_skip, True),
        ("stale-gate-run-id", "schema 0, local PASS rejects (report not from the current run)",
         stale_gate_run_id, True),
        ("stale-gate-hash", "schema 0, local PASS rejects (report hash does not match)",
         stale_gate_hash, True),
        ("wrong-toolchain", "schema 0, local PASS rejects (InProcessEmitToolchain absent)",
         wrong_toolchain, True),
        ("wrong-allocation-normalization", "schema 0, local PASS rejects (divisor is not 100)",
         wrong_allocation_normalization, True),
        ("wrong-b-arm-pairing", "schema 0, local PASS rejects (arm pairing/category wrong)",
         wrong_b_arm_pairing, True),
        ("wrong-acceptance-inventory", "schema 0, local PASS rejects (not exactly seven cases)",
         wrong_acceptance_inventory, True),
    )
    for name, expect, mutator, schema_zero in matrix:
        mutate(name, expect, mutator, schema_zero=schema_zero)

    # ---- frozen ratio/noise rules and the confidence-level semantics ---------------------------
    def rule_case(name: str, expect: str, ok: bool) -> None:
        cases.append({"name": name, "expect": expect, "schema_exit": "-", "pass": "-",
                      "problems": [] if ok else ["rule behaved differently"], "ok": ok})

    def entry(mean, ci_low, ci_high, allocated, *, level=12, n=15, standard_error=None):
        margin = None if (ci_low is None or ci_high is None) else (ci_high - ci_low) / 2.0
        if standard_error is None and margin is not None:
            standard_error = margin / student_t_critical(n - 1, 0.999)
        return {"mean": mean, "ci_low": ci_low, "ci_high": ci_high, "allocated": allocated,
                "ci_level": level, "ci_n": n, "ci_margin": margin, "standard_error": standard_error}

    for df, published in ((10, 3.1693), (13, 3.0123), (20, 2.8453), (30, 2.7500), (60, 2.6603)):
        observed = student_t_critical(df, 0.99)
        rule_case(f"t99-critical-df{df}", f"two-sided 99% t critical ~= {published}",
                  abs(observed - published) < 0.003)
    for df, multiplier in ((13, 4.2208), (14, 4.1405), (12, 4.3180)):
        implied = two_sided_t_confidence(multiplier, df)
        rule_case(f"t99-confidence-df{df}", "Margin/SE of the real reports implies >= 99%",
                  implied >= 0.99)

    tight_nextorm = entry(100.0, 99.5, 100.5, 0)
    tight_linq = entry(100.0, 99.5, 100.5, 0)
    slow_nextorm = entry(120.0, 119.0, 121.0, 10)
    wide_nextorm = entry(100.0, 90.0, 110.0, 0)
    narrow_nextorm = entry(100.0, 99.5, 100.5, 0, standard_error=1.0)

    decision, detail = time_decision(tight_nextorm, tight_linq)
    rule_case("rule-time-MET", "point 1.0 with symmetric tight CI -> MET", decision == "MET")
    decision, _ = time_decision(slow_nextorm, tight_linq)
    rule_case("rule-time-FAIL", "point 1.2 and ratio_low > 1.05 -> FAIL", decision == "FAIL")
    decision, _ = time_decision(wide_nextorm, tight_linq)
    rule_case("rule-time-OPEN-wide", "point 1.0 but ratio_high > 1.05 -> OPEN", decision == "OPEN")
    decision, _ = time_decision(tight_nextorm, entry(100.0, 99.5, 100.5, 0, level=99))
    rule_case("rule-time-OPEN-level", "arms with different exported levels -> OPEN",
              decision == "OPEN")
    decision, _ = time_decision(narrow_nextorm, tight_linq)
    rule_case("rule-time-OPEN-narrow-ci", "interval narrower than a 99% t-interval -> OPEN",
              decision == "OPEN")
    decision, _ = time_decision(entry(100.0, None, 100.5, 0), tight_linq)
    rule_case("rule-time-OPEN-missing-ci", "missing CI bound -> OPEN", decision == "OPEN")
    decision, _ = time_decision(entry(100.0, 99.5, 100.5, 0, level=99.0),
                                entry(100.0, 99.5, 100.5, 0, level=99.0))
    rule_case("rule-time-level-percent", "an exported percentage level >= 99 is accepted directly",
              decision == "MET")

    rule_case("rule-alloc-zero-to-zero", "0 -> 0 raw bytes is MET",
              allocation_decision(0, 0)[0] == "MET")
    rule_case("rule-alloc-zero-to-positive", "0 -> positive raw bytes is FAIL",
              allocation_decision(8, 0)[0] == "FAIL")
    rule_case("rule-alloc-equal", "equal raw bytes is MET",
              allocation_decision(1024, 1024)[0] == "MET")
    rule_case("rule-alloc-growth", "growth in raw bytes is FAIL",
              allocation_decision(1025, 1024)[0] == "FAIL")
    rule_case("rule-alloc-missing", "missing raw bytes is FAIL",
              allocation_decision(None, 1024)[0] == "FAIL")

    rule_case("rule-rounds-all-met", "every round MET -> MET",
              combine_rounds(["MET", "MET"], ["MET", "MET"]) == "MET")
    rule_case("rule-rounds-time-fail", "every time round decisively FAIL -> FAIL",
              combine_rounds(["FAIL", "FAIL"], ["MET", "MET"]) == "FAIL")
    rule_case("rule-rounds-alloc-fail", "any allocation round FAIL -> FAIL",
              combine_rounds(["MET", "MET"], ["MET", "FAIL"]) == "FAIL")
    rule_case("rule-rounds-mixed", "MET + OPEN -> OPEN",
              combine_rounds(["MET", "OPEN"], ["MET", "MET"]) == "OPEN")
    rule_case("rule-rounds-empty", "no valid round -> OPEN",
              combine_rounds([], []) == "OPEN")

    failures = [case for case in cases if not case["ok"]]
    payload = {
        "stage": "self-test",
        "generated_utc": utc_now(),
        "matrix": "docs/specs/status/D201-cached-parity-1.md §Manifest self-test matrix",
        "case_count": len(cases),
        "failure_count": len(failures),
        "cases": cases,
        "failures": failures,
    }
    write_json(fixtures / "self-test.json", payload)
    write_text(
        fixtures / "self-test.log",
        "W-S negative self-tests — "
        f"{len(cases)} cases, {len(failures)} misbehaving\n\n"
        + "\n".join(
            f"{'OK  ' if case['ok'] else 'FAIL'} {case['name']:<32s} "
            f"schema={case['schema_exit']} pass={case['pass']} :: {case['expect']}"
            for case in cases
        )
        + "\n",
    )
    log(f"self-test: {len(cases) - len(failures)}/{len(cases)} cases distinguished")
    for case in failures:
        log(f"  MISBEHAVING: {case['name']}: {case['problems']}")
    return EXIT_FAIL if failures else EXIT_OK


def reset_phase_reports(*directories) -> None:
    """Drop a phase's previous BDN reports so a stale run can never fill a current row."""
    for directory in directories:
        directory = Path(directory)
        if directory.exists():
            shutil.rmtree(directory)


def archive_pinned_reports(results_dir: Path, target_dir: Path) -> dict:
    target_dir.mkdir(parents=True, exist_ok=True)
    archived = {}
    for class_name in PINNED_CLASSES:
        for path in sorted(results_dir.glob(f"*{class_name}*")):
            shutil.copy2(path, target_dir / path.name)
            archived[path.name] = sha256_file(path)
    return archived


def copy_pinned_reports(results_dir: Path, target_dir: Path) -> dict:
    target_dir.mkdir(parents=True, exist_ok=True)
    fresh = {}
    for class_name in PINNED_CLASSES:
        for path in sorted(results_dir.glob(f"*{class_name}*")):
            shutil.copy2(path, target_dir / path.name)
            fresh[path.name] = sha256_file(path)
    return fresh


def run_native_gate(root: Path, recorder: Recorder, phase: str) -> dict:
    """Run the unchanged native gate live + report-only, archiving and restoring pinned reports."""
    stage_dir = root / phase / "iteration14"
    fresh_dir = stage_dir / "fresh"
    archive_dir = stage_dir / "archive"
    for directory in (fresh_dir, archive_dir):
        if directory.exists():
            shutil.rmtree(directory)

    snapshot_root = Path(tempfile.mkdtemp(prefix="t201-bdn-snapshot-"))
    snapshot = snapshot_root / "BenchmarkDotNet.Artifacts"
    pre_manifest = dir_manifest(BDN_RESULTS)
    shutil.copytree(BDN_ARTIFACTS, snapshot)
    write_json(stage_dir / "pre-run-reports.json", pre_manifest)
    archived = archive_pinned_reports(BDN_RESULTS, archive_dir)
    write_json(stage_dir / "archived-pinned-reports.json", archived)

    try:
        live = recorder.run(
            phase, "gate-live",
            ["python3", GATE_REL, "--manifest", BUDGETS_REL, "--reports-dir",
             "benchmarks/BenchmarkDotNet.Artifacts/results"],
            log_path=stage_dir / "gate-live.log",
            env_unset=("NEXTORM_BENCH_FULL",), timeout=G_LIVE_BUDGET_S + 600,
        )
        if live["timed_out"] or live["wall_seconds"] > G_LIVE_BUDGET_S:
            raise Inconclusive(
                f"G-LIVE exceeded its {G_LIVE_BUDGET_S}s budget (wall {live['wall_seconds']:.0f}s, "
                f"timed_out={live['timed_out']})"
            )

        fresh = copy_pinned_reports(BDN_RESULTS, fresh_dir)
        write_json(stage_dir / "fresh-pinned-reports.json", fresh)

        report = recorder.run(
            phase, "gate-report",
            ["python3", GATE_REL, "--manifest", BUDGETS_REL, "--reports-dir",
             "benchmarks/BenchmarkDotNet.Artifacts/results", "--no-run"],
            log_path=stage_dir / "gate-report.log",
            env_unset=("NEXTORM_BENCH_FULL",), timeout=600,
        )
    finally:
        restore_directory(snapshot, BDN_ARTIFACTS)
        shutil.rmtree(snapshot_root, ignore_errors=True)

    post_manifest = dir_manifest(BDN_RESULTS)
    restored = post_manifest == pre_manifest
    write_json(stage_dir / "restore-verification.json",
               {"restored": restored, "pre_files": len(pre_manifest), "post_files": len(post_manifest)})
    if not restored:
        raise EvidenceError("historical BenchmarkDotNet.Artifacts/results were not restored exactly")

    inventory = read_json(root / "oracle" / "iteration14-inventory.json")
    evidence = validate_gate_evidence(fresh_dir, inventory, live["exit_code"], report["exit_code"])
    evidence["archived_report_count"] = len(archived)
    evidence["fresh_report_count"] = len(fresh)
    evidence["restored_exactly"] = restored
    write_json(stage_dir / "gate-evidence.json", evidence)
    return {"live": live, "report": report, "evidence": evidence}


def stage_baseline(root: Path, recorder: Recorder) -> int:  # noqa: C901 - explicit command registry
    """W-B: Release build, B-BASE, A-BASE, G-LIVE, G-REPORT; then validate the inventories."""
    baseline = root / "baseline"
    oracle_b = read_json(root / "oracle" / "b-arms.json")
    oracle_acceptance = read_json(root / "oracle" / "acceptance.json")

    reset_phase_reports(baseline / "category-b", baseline / "acceptance")
    build = recorder.run(
        "baseline", "build-release", ["dotnet", "build", "-c", "Release"],
        log_path=baseline / "build.log", timeout=1800,
    )
    build_validation = validate_build_log(baseline / "build.log")
    if build["exit_code"] != 0 or build_validation["problems"]:
        raise EvidenceError(f"Release build failed: exit {build['exit_code']} {build_validation['problems']}")

    category_b = recorder.run(
        "baseline", "category-b",
        ["dotnet", "run", "--project", BENCH_PROJECT, "-c", "Release", "--", "--filter", BROAD_FILTER,
         "--exporters", "json", "--artifacts", "artifacts/pdca/T201/rv1/baseline/category-b"],
        log_path=baseline / "category-b.log", env_set={"NEXTORM_BENCH_FULL": "1"},
        timeout=B_BASE_BUDGET_S + 600,
    )
    if category_b["timed_out"] or category_b["wall_seconds"] > B_BASE_BUDGET_S:
        raise Inconclusive(
            f"B-BASE exceeded its {B_BASE_BUDGET_S}s budget (wall {category_b['wall_seconds']:.0f}s)"
        )
    if category_b["exit_code"] != 0:
        raise EvidenceError(f"B-BASE exited {category_b['exit_code']}")

    acceptance = recorder.run(
        "baseline", "acceptance",
        ["dotnet", "run", "--project", BENCH_PROJECT, "-c", "Release", "--", "--anyCategories=acceptance",
         "--exporters", "json", "--artifacts", "artifacts/pdca/T201/rv1/baseline/acceptance"],
        log_path=baseline / "acceptance.log", env_set={"NEXTORM_BENCH_FULL": "1"}, timeout=1800,
    )
    if acceptance["exit_code"] != 0:
        raise EvidenceError(f"A-BASE exited {acceptance['exit_code']}")

    b_extraction = extract_category_b(baseline / "category-b" / "results", oracle_b)
    if not extraction_ok(b_extraction):
        raise EvidenceError(f"category B inventory/pairing invalid: {b_extraction['problems']}")
    a_extraction = extract_acceptance(baseline / "acceptance" / "results", oracle_acceptance)
    if not acceptance_ok(a_extraction):
        raise EvidenceError(f"acceptance inventory invalid: {a_extraction['problems']}")

    gate = run_native_gate(root, recorder, "baseline")
    if not gate_evidence_ok(gate["evidence"]):
        raise EvidenceError(f"native gate evidence invalid: {gate['evidence']['problems']}")

    reconciliation = {
        "stage": "baseline",
        "generated_utc": utc_now(),
        "sources": {
            "b-arms": rel(root / "oracle" / "b-arms.json"),
            "acceptance": rel(root / "oracle" / "acceptance.json"),
            "iteration14": rel(root / "oracle" / "iteration14-inventory.json"),
        },
        "checks": [
            {
                "what": "source-backed 55-arm Category B selection",
                "expected": b_extraction["expected_count"],
                "observed": b_extraction["selected_count"],
                "ok": b_extraction["expected_count"] == b_extraction["selected_count"] == 55,
            },
            {
                "what": "exact ten-arm extraction (five scenarios, category-matched)",
                "expected": len(SCENARIOS),
                "observed": len(b_extraction["pairs"]),
                "ok": len(b_extraction["pairs"]) == len(SCENARIOS) and not b_extraction["problems"],
            },
            {
                "what": "seven active acceptance cases",
                "expected": oracle_acceptance["active_count"],
                "observed": a_extraction["case_count"],
                "ok": acceptance_ok(a_extraction),
            },
            {
                "what": "28 gate rows x 2 toolchains",
                "expected": gate["evidence"]["expected_row_count"],
                "observed": gate["evidence"]["row_count"],
                "ok": gate_evidence_ok(gate["evidence"]),
            },
        ],
        "problems": (
            b_extraction["problems"] + a_extraction["problems"] + gate["evidence"]["problems"]
        ),
    }
    write_json(root / "oracle" / "reconciliation.json", reconciliation)

    summary = {
        "stage": "baseline",
        "generated_utc": utc_now(),
        "tree": tree_identity(),
        "build": build,
        "build_validation": build_validation,
        "category_b": category_b,
        "acceptance": acceptance,
        "gate_live": gate["live"],
        "gate_report": gate["report"],
        "b_inventory": {"selected_count": b_extraction["selected_count"],
                        "expected_count": b_extraction["expected_count"],
                        "scenarios": sorted(b_extraction["pairs"])},
        "acceptance_inventory": {"case_count": a_extraction["case_count"]},
        "gate_passed_rows": gate["evidence"]["row_count"],
        "gate_expected_rows": gate["evidence"]["expected_row_count"],
        "gate_restored_exactly": gate["evidence"]["restored_exactly"],
    }
    write_json(baseline / "summary.json", summary)
    write_text(
        baseline / "summary.txt",
        "T201 D201-2 baseline\n"
        f"tree: {summary['tree']['branch']} @ {summary['tree']['head']}\n"
        f"build: exit {build['exit_code']} wall {build['wall_seconds']:.1f}s\n"
        f"B-BASE: exit {category_b['exit_code']} wall {category_b['wall_seconds']:.1f}s "
        f"arms {b_extraction['selected_count']}/{b_extraction['expected_count']}\n"
        f"A-BASE: exit {acceptance['exit_code']} wall {acceptance['wall_seconds']:.1f}s "
        f"cases {a_extraction['case_count']}/7\n"
        f"G-LIVE: exit {gate['live']['exit_code']} wall {gate['live']['wall_seconds']:.1f}s\n"
        f"G-REPORT: exit {gate['report']['exit_code']} wall {gate['report']['wall_seconds']:.1f}s\n"
        f"gate rows: {gate['evidence']['row_count']}/{gate['evidence']['expected_row_count']}\n"
        f"historical reports restored exactly: {gate['evidence']['restored_exactly']}\n",
    )
    log(
        f"baseline OK: arms {b_extraction['selected_count']}, cases {a_extraction['case_count']}, "
        f"gate rows {gate['evidence']['row_count']}"
    )
    return EXIT_OK


def stage_candidate(root: Path, recorder: Recorder) -> int:
    """W-C: mirror of the baseline runs on the post-edit tree."""
    candidate = root / "candidate"
    baseline_summary_path = root / "baseline" / "summary.json"
    if not baseline_summary_path.exists():
        raise Inconclusive("no baseline summary.json: run --stage baseline first")
    baseline_tree = read_json(baseline_summary_path)["tree"]
    current_tree = tree_identity()
    if (current_tree["head"] == baseline_tree["head"]
            and current_tree["status_sha256"] == baseline_tree["status_sha256"]):
        raise EvidenceError("candidate tree is identical to the baseline tree; nothing to measure")

    oracle_b = read_json(root / "oracle" / "b-arms.json")
    oracle_acceptance = read_json(root / "oracle" / "acceptance.json")

    reset_phase_reports(candidate / "category-b", candidate / "acceptance")
    build = recorder.run("candidate", "build-release", ["dotnet", "build", "-c", "Release"],
                         log_path=candidate / "build.log", timeout=1800)
    if build["exit_code"] != 0:
        raise EvidenceError(f"candidate Release build exited {build['exit_code']}")
    category_b = recorder.run(
        "candidate", "category-b",
        ["dotnet", "run", "--project", BENCH_PROJECT, "-c", "Release", "--", "--filter", BROAD_FILTER,
         "--exporters", "json", "--artifacts", "artifacts/pdca/T201/rv1/candidate/category-b"],
        log_path=candidate / "category-b.log", env_set={"NEXTORM_BENCH_FULL": "1"},
        timeout=B_BASE_BUDGET_S + 600,
    )
    if category_b["timed_out"] or category_b["wall_seconds"] > B_BASE_BUDGET_S:
        raise Inconclusive(f"B-CAND exceeded its budget (wall {category_b['wall_seconds']:.0f}s)")
    if category_b["exit_code"] != 0:
        raise EvidenceError(f"B-CAND exited {category_b['exit_code']}")
    acceptance = recorder.run(
        "candidate", "acceptance",
        ["dotnet", "run", "--project", BENCH_PROJECT, "-c", "Release", "--", "--anyCategories=acceptance",
         "--exporters", "json", "--artifacts", "artifacts/pdca/T201/rv1/candidate/acceptance"],
        log_path=candidate / "acceptance.log", env_set={"NEXTORM_BENCH_FULL": "1"}, timeout=1800,
    )
    if acceptance["exit_code"] != 0:
        raise EvidenceError(f"A-CAND exited {acceptance['exit_code']}")

    b_extraction = extract_category_b(candidate / "category-b" / "results", oracle_b)
    if not extraction_ok(b_extraction):
        raise EvidenceError(f"candidate category B inventory invalid: {b_extraction['problems']}")
    a_extraction = extract_acceptance(candidate / "acceptance" / "results", oracle_acceptance)
    if not acceptance_ok(a_extraction):
        raise EvidenceError(f"candidate acceptance inventory invalid: {a_extraction['problems']}")

    gate = run_native_gate(root, recorder, "candidate")
    if not gate_evidence_ok(gate["evidence"]):
        raise EvidenceError(f"candidate native gate evidence invalid: {gate['evidence']['problems']}")

    write_json(candidate / "summary.json", {
        "stage": "candidate", "generated_utc": utc_now(), "tree": current_tree, "build": build,
        "category_b": category_b, "acceptance": acceptance, "gate_live": gate["live"],
        "gate_report": gate["report"],
        "b_inventory": {"selected_count": b_extraction["selected_count"]},
        "acceptance_inventory": {"case_count": a_extraction["case_count"]},
    })
    log("candidate OK")
    return EXIT_OK


def parity_rounds(root: Path, oracle_b: dict) -> dict:
    rounds = []
    for phase in ("baseline", "candidate"):
        report_root = root / phase / "category-b" / "results"
        if not report_root.exists():
            continue
        extraction = extract_category_b(report_root, oracle_b)
        if not extraction_ok(extraction):
            raise EvidenceError(f"{phase} category B evidence invalid: {extraction['problems']}")
        rounds.append((phase, extraction))
    if not rounds:
        raise Inconclusive("no Category B rounds found; run --stage baseline and --stage candidate")

    scenarios = {}
    for scenario in SCENARIOS:
        time_decisions, allocation_decisions, details = [], [], []
        for phase, extraction in rounds:
            pair = extraction["pairs"][scenario]
            t_decision, t_detail = time_decision(pair["nextorm"], pair["linq2db"])
            a_decision, a_detail = allocation_decision(pair["nextorm"]["allocated"],
                                                       pair["linq2db"]["allocated"])
            time_decisions.append(t_decision)
            allocation_decisions.append(a_decision)
            details.append({
                "phase": phase, "time": t_decision, "allocation": a_decision,
                "time_detail": t_detail, "allocation_detail": a_detail,
                "nextorm_arm": pair["nextorm"]["full"], "linq2db_arm": pair["linq2db"]["full"],
            })
        scenarios[scenario] = {
            "decision": combine_rounds(time_decisions, allocation_decisions),
            "time_decisions": time_decisions,
            "allocation_decisions": allocation_decisions,
            "rounds": details,
            "tolerance": TIME_TOLERANCE,
        }
    return {"scenarios": scenarios, "round_count": len(rounds), "problems": []}


def stage_parity(root: Path, recorder: Recorder) -> int:
    """W-P: frozen ratio/noise decisions. Baseline alone never claims parity."""
    if not (root / "candidate" / "category-b" / "results").exists():
        write_json(root / "reports" / "parity.json",
                   {"status": "inconclusive", "reason": "candidate Category B round is absent",
                    "scenarios": {}})
        raise Inconclusive("candidate Category B round absent: parity cannot be decided from baseline")
    result = parity_rounds(root, read_json(root / "oracle" / "b-arms.json"))
    write_json(root / "reports" / "parity.json", result)
    decisions = {name: payload["decision"] for name, payload in result["scenarios"].items()}
    write_text(
        root / "reports" / "parity.md",
        "# T201 Category B parity\n\n"
        + "\n".join(f"- {name}: {decisions[name]} (rounds "
                    f"{len(result['scenarios'][name]['rounds'])})" for name in sorted(decisions))
        + "\n",
    )
    log(f"parity: {decisions}")
    return EXIT_OK if all(decision == "MET" for decision in decisions.values()) else EXIT_FAIL


def stage_acceptance(root: Path, recorder: Recorder) -> int:
    """W-A: seven-case acceptance evidence with recorded ratios and noise flags."""
    oracle_acceptance = read_json(root / "oracle" / "acceptance.json")
    payload = {"stage": "acceptance", "generated_utc": utc_now(), "phases": {}, "trips": [],
               "historical_ratios": HISTORICAL_ACCEPTANCE_RATIOS,
               "investigation_threshold": ACCEPTANCE_GROWTH_THRESHOLD}
    for phase in ("baseline", "candidate"):
        report_root = root / phase / "acceptance" / "results"
        if not report_root.exists():
            continue
        extraction = extract_acceptance(report_root, oracle_acceptance)
        if not acceptance_ok(extraction):
            raise EvidenceError(f"{phase} acceptance inventory invalid: {extraction['problems']}")
        summary_path = root / phase / "summary.json"
        extraction["wall_seconds"] = (
            read_json(summary_path).get("acceptance", {}).get("wall_seconds")
            if summary_path.exists() else None
        )
        extraction["ratios"] = acceptance_ratios(extraction)
        extraction["growth_vs_historical"] = {
            key: (extraction["ratios"][key] / historical - 1.0)
            for key, historical in HISTORICAL_ACCEPTANCE_RATIOS.items()
            if key in extraction["ratios"] and historical
        }
        payload["phases"][phase] = extraction

    if not payload["phases"]:
        raise Inconclusive("no acceptance round found; run --stage baseline first")

    for phase, extraction in payload["phases"].items():
        for key, growth in extraction.get("growth_vs_historical", {}).items():
            payload["trips"].append({"comparison": f"{phase} vs historical", "ratio": key,
                                     "growth": growth, "investigate": growth > ACCEPTANCE_GROWTH_THRESHOLD})
    if "baseline" in payload["phases"] and "candidate" in payload["phases"]:
        base = payload["phases"]["baseline"].get("ratios", {})
        cand = payload["phases"]["candidate"].get("ratios", {})
        for key in sorted(HISTORICAL_ACCEPTANCE_RATIOS):
            if key in base and key in cand and base[key]:
                growth = cand[key] / base[key] - 1.0
                payload["trips"].append({"comparison": "candidate vs baseline", "ratio": key,
                                         "baseline": base[key], "candidate": cand[key],
                                         "growth": growth,
                                         "investigate": growth > ACCEPTANCE_GROWTH_THRESHOLD})

    write_json(root / "reports" / "acceptance.json", payload)
    return EXIT_FAIL if any(trip["investigate"] for trip in payload["trips"]) else EXIT_OK


def stage_warm_gate(root: Path, recorder: Recorder) -> int:
    """W-W: validate archived gate evidence and re-reconcile with a report-only gate run."""
    inventory = read_json(root / "oracle" / "iteration14-inventory.json")
    phases = {}
    for phase in ("baseline", "candidate"):
        stage_dir = root / phase / "iteration14"
        fresh_dir = stage_dir / "fresh"
        if not fresh_dir.exists():
            continue
        evidence = validate_gate_evidence(fresh_dir, inventory, 0, 0, load_recorded_gate(fresh_dir))
        reconciliation_dir = stage_dir / "reconciliation"
        if reconciliation_dir.exists():
            shutil.rmtree(reconciliation_dir)
        reconciliation_dir.mkdir(parents=True, exist_ok=True)
        for path in fresh_dir.glob("*report-full*.json"):
            shutil.copy2(path, reconciliation_dir / path.name)
        run = recorder.run(
            phase, "gate-report-reconciliation",
            ["python3", GATE_REL, "--manifest", BUDGETS_REL, "--reports-dir", rel(reconciliation_dir),
             "--no-run"],
            log_path=stage_dir / "gate-report-reconciliation.log",
            env_unset=("NEXTORM_BENCH_FULL",), timeout=600,
        )
        evidence["reconciliation_exit"] = run["exit_code"]
        if run["exit_code"] != 0:
            evidence["problems"].append(f"report-only reconciliation exited {run['exit_code']}")
        provenance_path = stage_dir / "fresh-pinned-reports.json"
        if provenance_path.exists():
            provenance = read_json(provenance_path)
            stale = [name for name, digest in provenance.items()
                     if not (fresh_dir / name).exists() or sha256_file(fresh_dir / name) != digest]
            if stale:
                evidence["problems"].append(f"fresh report hashes changed: {stale}")
        phases[phase] = evidence
    if not phases:
        raise Inconclusive("no archived iteration14 evidence; run --stage baseline first")
    write_json(root / "reports" / "iteration14.json", {"phases": phases})
    ok = all(gate_evidence_ok(evidence) and evidence.get("reconciliation_exit") == 0
             for evidence in phases.values())
    log(f"warm-gate: phases {sorted(phases)} ok={ok}")
    return EXIT_OK if ok else EXIT_FAIL


def stage_tests(root: Path, recorder: Recorder) -> int:
    """W-T: core/dialect suites plus the container integration run."""
    tests_dir = root / "tests"
    docker_host = os.environ.get("DOCKER_HOST") or DOCKER_SOCKET
    problems = []

    build = recorder.run("tests", "build-solution", ["dotnet", "build", "nextorm.slnx", "-c", "Debug"],
                         log_path=tests_dir / "build.log", timeout=1800)
    if build["exit_code"] != 0 or validate_build_log(tests_dir / "build.log")["problems"]:
        raise EvidenceError(f"Debug solution build failed (exit {build['exit_code']})")

    suites = {
        "core": ["dotnet", "test", CORE_TESTS, "-c", "Debug", "--no-build"],
        "sqlite": ["dotnet", "test", "tests/nextorm.sqlite.tests", "-c", "Debug", "--no-build"],
        "clickhouse": ["dotnet", "test", "tests/nextorm.clickhouse.tests", "-c", "Debug", "--no-build"],
        "postgres": ["dotnet", "test", "tests/nextorm.postgres.tests", "-c", "Debug", "--no-build"],
        "sqlserver": ["dotnet", "test", "tests/nextorm.sqlserver.tests", "-c", "Debug", "--no-build"],
        "mysql": ["dotnet", "test", "tests/nextorm.mysql.tests", "-c", "Debug", "--no-build"],
    }
    results = {"exit_code": 0, "selected_count": 0, "skipped_required": [], "suites": {}}
    for name, command in suites.items():
        if not (REPO_ROOT / command[2]).exists():
            results["suites"][name] = {"skipped": True, "reason": "project directory absent"}
            continue
        run = recorder.run("tests", name, command, log_path=tests_dir / f"{name}.log", timeout=1800)
        selected = test_selection_count(tests_dir / f"{name}.log")
        results["suites"][name] = {"exit_code": run["exit_code"], "selected_count": selected,
                                   "wall_seconds": run["wall_seconds"], "log": run["log"]}
        results["selected_count"] += selected
        if run["exit_code"] != 0:
            results["exit_code"] = run["exit_code"]
            problems.append(f"{name} suite exited {run['exit_code']}")

    integration = recorder.run(
        "tests", "integration",
        ["dotnet", "run", "--project", INTEGRATION_PROJECT, "-c", "Debug", "--", "-noColor"],
        log_path=tests_dir / "integration.log", env_set={"DOCKER_HOST": docker_host}, timeout=3600,
    )
    skipped_required = scan_provider_skips(tests_dir / "integration.log")
    results["integration"] = {
        "exit_code": integration["exit_code"],
        "selected_count": test_selection_count(tests_dir / "integration.log"),
        "wall_seconds": integration["wall_seconds"],
        "docker_host": docker_host,
        "skipped_required": skipped_required,
        "log": integration["log"],
    }
    results["selected_count"] += results["integration"]["selected_count"]
    if integration["exit_code"] != 0:
        results["exit_code"] = integration["exit_code"]
        problems.append(f"integration run exited {integration['exit_code']}")
    if skipped_required:
        results["skipped_required"] = skipped_required
        problems.append(f"required provider(s) skipped: {skipped_required}")

    write_json(tests_dir / "results.json", results)
    if problems:
        raise EvidenceError("; ".join(problems))
    return EXIT_OK


def stage_coverage(root: Path, recorder: Recorder) -> int:
    """W-Q: native coverage workflow and thresholds."""
    coverage_dir = root / "coverage"
    coverage_dir.mkdir(parents=True, exist_ok=True)
    tooling = recorder.run("coverage", "tool-restore", ["dotnet", "tool", "restore"],
                           log_path=coverage_dir / "tool-restore.log", timeout=600)
    if tooling["exit_code"] != 0:
        raise Inconclusive("dotnet tool restore failed; coverage tooling unavailable")

    collect = recorder.run(
        "coverage", "collect",
        ["dotnet", "tool", "run", "dotnet-coverage", "collect", "-s", "coverage.settings.xml",
         "-f", "cobertura", "-o", "artifacts/pdca/T201/rv1/coverage/coverage.cobertura.xml",
         "dotnet test --no-build --verbosity normal"],
        log_path=coverage_dir / "collect.log", timeout=3600,
    )
    if collect["exit_code"] != 0:
        raise EvidenceError(f"coverage collect exited {collect['exit_code']}")
    report = recorder.run(
        "coverage", "report",
        ["dotnet", "tool", "run", "reportgenerator",
         "-reports:artifacts/pdca/T201/rv1/coverage/coverage.cobertura.xml",
         "-targetdir:artifacts/pdca/T201/rv1/coverage/report",
         "-reporttypes:Html;TextSummary;Cobertura",
         "-riskhotspotassemblyfilters:+nextorm.*"],
        log_path=coverage_dir / "report.log", timeout=1800,
    )
    if report["exit_code"] != 0:
        raise EvidenceError(f"coverage report exited {report['exit_code']}")
    summary_path = coverage_dir / "report" / "Summary.txt"
    if not summary_path.exists():
        raise EvidenceError("reportgenerator produced no Summary.txt")
    text = summary_path.read_text(encoding="utf-8", errors="replace")
    line = branch = None
    for row in text.splitlines():
        if "Line coverage" in row:
            line = row.strip()
        if "Branch coverage" in row:
            branch = row.strip()
    write_json(coverage_dir / "coverage.json",
               {"summary": text, "line": line, "branch": branch, "min_line": 85, "min_branch": 75,
                "policy": "1.0.9 warning"})
    log(f"coverage: {line} | {branch}")
    return EXIT_OK


def stage_api(root: Path, recorder: Recorder, phase: str, force: bool) -> int:
    """W-API: public-surface export (compiler metadata) plus the candidate diff when present."""
    api_dir = root / "api"
    tree = tree_identity()
    output_dir = next(
        (path.parent for path in sorted((REPO_ROOT / CORE_TESTS).glob("bin/*/Debug/net10.0/nextorm.core.dll"))),
        REPO_ROOT / CORE_TESTS / "bin" / "linux" / "Debug" / "net10.0",
    )

    def capture(label: str, target: Path, *, overwrite: bool) -> dict:
        if target.exists() and not overwrite:
            existing = validate_api_inventory(target)
            if not existing["problems"] and existing.get("capture"):
                log(f"api {label}: existing export kept ({existing['public_type_count']} public types)")
                return existing
        build = recorder.run("api", f"build-{label}", ["dotnet", "build", CORE_TESTS, "-c", "Debug"],
                             log_path=api_dir / f"{label}-build.log")
        if build["exit_code"] != 0:
            raise EvidenceError(f"{label} API export build exited {build['exit_code']}")
        run = recorder.run(
            "api", f"export-{label}",
            ["dotnet", "test", CORE_TESTS, "-c", "Debug", "--no-build", "--filter", API_TEST_FILTER],
            log_path=api_dir / f"{label}-export.log",
            env_set={"T201_API_EXPORT_PATH": str(target), "T201_API_EXPORT_LABEL": label},
        )
        if run["exit_code"] != 0:
            raise EvidenceError(f"{label} API export test run exited {run['exit_code']}")
        if not target.exists():
            raise EvidenceError(f"{label} API export was not written to {rel(target)}")
        document = read_json(target)
        document["capture"] = {
            "label": label,
            "captured_utc": utc_now(),
            "source_revision": "T201-D201-2-r2rv1",
            "pre_production": True,
            "tree": tree,
            "runs_utc": run["started_utc"],
            "wall_seconds": run["wall_seconds"],
            "command": run["command"],
            "log": run["log"],
            "assemblies": [
                {
                    "name": name,
                    "path": rel(output_dir / f"{name}.dll"),
                    "present": (output_dir / f"{name}.dll").exists(),
                    "sha256": sha256_file(output_dir / f"{name}.dll")
                    if (output_dir / f"{name}.dll").exists() else None,
                }
                for name in EXPORT_ASSEMBLIES
            ],
        }
        write_json(target, document)
        validated = validate_api_inventory(target)
        if validated["problems"]:
            raise EvidenceError(f"{label} API export invalid: {validated['problems']}")
        log(f"api {label}: {validated['public_type_count']} public types across "
            f"{len(validated['assemblies'])} assemblies")
        return validated

    labels = []
    if phase in ("baseline", "candidate"):
        labels = [phase]
    elif phase == "auto":
        labels = ["baseline"]
        if (root / "candidate").exists():
            labels.append("candidate")
    results = {label: capture(label, api_dir / f"{label}.json", overwrite=force) for label in labels}

    if "baseline" in results and "candidate" in results:
        diff = api_diff(api_dir / "baseline.json", api_dir / "candidate.json")
        write_json(api_dir / "diff.json", diff)
        write_json(root / "reports" / "api.json",
                   {"baseline": results["baseline"], "candidate": results["candidate"], "diff": diff})
        log(f"api diff: equal={diff['equal']} changed={len(diff['changed'])}")
        return EXIT_OK if diff["equal"] else EXIT_FAIL

    write_json(root / "reports" / "api.json", {"baseline": results.get("baseline")})
    return EXIT_OK


def stage_manifest(root: Path, recorder: Recorder) -> int:
    """W-M: assemble the sixteen-row manifest, then validate schema and local semantics."""
    manifest_path = root / "manifest.json"
    tree = tree_identity()
    rows: dict = {}

    def add(row_id: str, kind: str, path, phase=None) -> None:
        path = Path(path)
        if not path.exists() or (path.is_file() and path.stat().st_size == 0):
            return
        item = {"kind": kind, "path": rel(path)}
        if phase:
            item["phase"] = phase
        if kind == "gate" and path.is_file():
            item["sha256"] = sha256_file(path)
        rows.setdefault(row_id, []).append(item)

    add("EC201-01", "review", root / "reviews" / "design.md")
    add("EC201-01", "review", root / "reviews" / "plan.md")
    add("EC201-02", "b-arms", root / "oracle" / "b-arms.json")
    for row_id in ("EC201-03", "EC201-04", "EC201-05", "EC201-06"):
        add(row_id, "parity", root / "reports" / "parity.json")
    for phase in ("baseline", "candidate"):
        fresh_dir = root / phase / "iteration14" / "fresh"
        if fresh_dir.exists():
            pinned = sorted(fresh_dir.glob("*report-full-compressed.json"))
            if pinned:
                add("EC201-07", "gate", pinned[0])
    for phase in ("baseline", "candidate"):
        if (root / phase / "acceptance" / "results").exists():
            add("EC201-08", "acceptance", root / "oracle" / "acceptance.json", phase)
    add("EC201-09", "tests", root / "tests" / "results.json")
    add("EC201-09", "report", root / "reports" / "correctness.md")
    for row_id, name in (("EC201-10", "shared-command.md"), ("EC201-11", "temp-tvp.md"),
                         ("EC201-13", "cache-contract.md"), ("EC201-14", "recipe-consumers.md"),
                         ("EC201-15", "quality.md"), ("EC201-16", "manifest-validation.md")):
        add(row_id, "report", root / "reports" / name)
    if (root / "api" / "diff.json").exists():
        add("EC201-12", "api", root / "api" / "baseline.json")
        add("EC201-12", "file", root / "api" / "diff.json")
    else:
        add("EC201-12", "api", root / "api" / "baseline.json")
    add("EC201-15", "build", root / "baseline" / "build.log")
    add("EC201-15", "file", root / "instrumentation" / "self-test.json")
    for name in ("ownership-state.md", "shared-route.md", "m9-inventory.md", "siblings.md"):
        add("EC201-16", "discovery", root / "discovery" / name)
    add("EC201-16", "review", root / "reviews" / "collection-reconciliation.md")

    manifest_rows = []
    open_rows = []
    for row_id in REQUIRED_ROWS:
        items = rows.get(row_id, [])
        if items:
            manifest_rows.append({"id": row_id, "status": "met", "evidence": items})
        else:
            open_rows.append(row_id)
            manifest_rows.append({"id": row_id, "status": "open",
                                  "reason": f"no current evidence artifact recorded for {row_id}"})

    write_json(manifest_path, {
        "task": "T201",
        "tree": f"{tree['branch']}@{tree['head']} status:{tree['status_sha256'][:12]}",
        "status_file": "docs/specs/status/D201-cached-parity-1.md",
        "contract_rv": CONTRACT_RV,
        "required_rows": list(REQUIRED_ROWS),
        "rows": manifest_rows,
    })
    semantics = validate_manifest_semantics(manifest_path, root)
    write_json(root / "reports" / "manifest-validation.json", semantics)
    write_text(
        root / "reports" / "manifest-validation.md",
        "# T201 manifest validation\n\n"
        f"- schema validator exit: {semantics['schema_exit']}\n"
        f"- local semantic problems: {len(semantics['problems'])}\n"
        f"- open rows: {len(open_rows)} {open_rows}\n"
        + "\n".join(f"  - {problem}" for problem in semantics["problems"][:20])
        + "\n",
    )
    log(f"manifest: schema exit {semantics['schema_exit']}, open rows {len(open_rows)}")
    if open_rows:
        return EXIT_FAIL
    return EXIT_OK if semantics["pass"] else EXIT_FAIL


STAGE_HANDLERS = {
    "self-test": stage_self_test,
    "baseline": stage_baseline,
    "tests": stage_tests,
    "candidate": stage_candidate,
    "parity": stage_parity,
    "acceptance": stage_acceptance,
    "warm-gate": stage_warm_gate,
    "coverage": stage_coverage,
    "manifest": stage_manifest,
}


def parse_args(argv=None):
    parser = argparse.ArgumentParser(prog="t201_evidence.py",
                                     description="T201 (#201) cached-path parity evidence runner")
    parser.add_argument("--stage", required=True, choices=STAGES)
    parser.add_argument("--root", default=DEFAULT_ROOT, help="evidence root (default: %(default)s)")
    parser.add_argument("--phase", default="auto", choices=("auto", "baseline", "candidate"),
                        help="API stage: which export(s) to capture")
    parser.add_argument("--force", action="store_true",
                        help="re-capture an existing API export instead of keeping it")
    return parser.parse_args(argv)


def main(argv=None) -> int:
    args = parse_args(argv)
    root = Path(args.root)
    if not root.is_absolute():
        root = REPO_ROOT / root
    root.mkdir(parents=True, exist_ok=True)
    recorder = Recorder(root)
    stage = args.stage
    log(f"stage {stage} root {rel(root)}")
    try:
        if stage == "api":
            return stage_api(root, recorder, args.phase, args.force)
        if stage in ("baseline", "candidate"):
            with BenchmarkProjectGuard(root, stage):
                return STAGE_HANDLERS[stage](root, recorder)
        return STAGE_HANDLERS[stage](root, recorder)
    except Inconclusive as exc:
        log(f"INCONCLUSIVE (exit 2): {exc}")
        write_json(root / "reports" / f"{stage}-outcome.json",
                   {"stage": stage, "outcome": "inconclusive", "reason": str(exc), "utc": utc_now()})
        return EXIT_INCONCLUSIVE
    except EvidenceError as exc:
        log(f"FAIL (exit 1): {exc}")
        write_json(root / "reports" / f"{stage}-outcome.json",
                   {"stage": stage, "outcome": "fail", "reason": str(exc), "utc": utc_now()})
        return EXIT_FAIL


if __name__ == "__main__":
    sys.exit(main())
