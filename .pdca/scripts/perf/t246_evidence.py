#!/usr/bin/env python3
""".pdca/scripts/perf/t246_evidence.py — T246 rv2 measurement-protocol driver and fail-closed gate.

Python 3 standard library only.  The tool implements the planned rv2 CLI of
``docs/specs/status/D246-raw-datareader-2.md`` ("Exact command registry"):

    measure    one exclusive host lock for the complete baseline/final block
    protocol   preflight: lock/DB/argv/list/runtime/host/harness hashes
    qualify    three valid baseline runs -> measured e_q/s_q -> frozen E*/S*
    oracle     canonical out-of-process oracle arithmetic and citations
    modes      fixed 15 % launch-median mode detector with run replication
    gate       fail-closed final gate over the frozen protocol lock
    self-test  negative fixtures proving fail-closed rejection
    parse-check  read-only log/JSON per-op reconciliation of captured runs
    replay     run exactly one pinned recipe command

Evidence is written under ``--root`` (normally ``artifacts/pdca/T246/rv2``).
Exit codes: 0 = pass, 1 = measured failure, 2 = invalid/unverifiable input
(fail closed), 3 = terminal protocol GAP.  No number in an artifact is ever
invented: if a statistic, launch, hash or sensor reading cannot be recovered
the run/subcommand is rejected and the reason is recorded.

Measurement contract invariants enforced here (rv2):
  * exactly one ``flock(LOCK_EX)`` acquisition for a complete block;
  * a shared, append-only attempt ledger (``perf/attempt-ledger.json``) with a
    two-hour cumulative measurement budget and an eight-complete-run ceiling;
  * fixed ``InvocationCount`` (64 for 10k, 4096 for empty) with exactly one
    authorized doubling per stratum on a ``MinIterationTime`` warning, and a
    fixed ``IterationCount=10`` that is never raised or substituted by
    invocation executions;
  * per-launch statistics recovered from BDN raw ``Measurements`` and
    independently from the BDN ``.log``; pooled ``Statistics`` are never copied
    into launch evidence;
  * 99.9 % confidence Error, precision ``Error/Mean`` and mode-spread caps;
  * ``time_upper_ratio <= 1.00`` and allocation ratio ``<= 1.00`` per arm/run,
    empty tolerance 0 %.
"""
from __future__ import annotations

import argparse
import csv
import fcntl
import glob
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path

TASK = "T246"
SCHEMA_VERSION = 1

CANONICAL_LOCK = "/tmp/opencode/nextorm-bdn-host.lock"
DB_SEED_PATH = "/tmp/nextorm-bench/test.db"
DB_SEED_SIZE = 753664
DB_SHA256 = "32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295"
DB_SHA_PREFIX = "32bf10a0"
DB_SHA_SUFFIX = "72295"
PARITY_ROWS = 10000
PARITY_CHECKSUM = 422955955263575176

ITERATION_COUNT = 10
UNROLL_FACTOR = 1
LAUNCH_COUNT = 3
WARMUP_COUNT = 5
REQUIRED_RUNS = 3
MAX_ATTEMPTS_PER_PHASE = 4
MAX_TOTAL_RUNS = 8
DEFAULT_TOTAL_BUDGET_SECONDS = 7200
DEFAULT_RUN_TIMEOUT_SECONDS = 600
QUIET_SECONDS = 60

MODE_GAP = 0.15
QUALIFICATION_ERROR_CAP = 0.027
QUALIFICATION_SPREAD_CAP = 0.04
FROZEN_ERROR_CAP = 0.0300
FROZEN_SPREAD_CAP = 0.0500
ERROR_MARGIN = 0.003
SPREAD_MARGIN = 0.010

BENCH_PROJECT = "benchmarks/nextorm.benchmark"
BENCH_TYPE = "SqliteBenchmarkStreaming"
HARNESS_PATH = "benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs"
CONFIG_PATH = "benchmarks/nextorm.benchmark/Infrastructure/NextormConfig.cs"
DRIVER_PATH = ".pdca/scripts/perf/t246_evidence.py"
BUDGETS_PATH = ".pdca/scripts/perf/t246-budgets.json"

RUNTIME_DIR = Path("/tmp/opencode/T246/rv2")
EXIT_OK = 0
EXIT_FAIL = 1
EXIT_REJECT = 2
EXIT_GAP = 3

STRATA = {
    "10k": {
        "invocation_count": 64,
        "invocation_count_authorized_max": 128,
        "categories": ("A_RawReader", "B_RawReader"),
        "methods": (
            "A_Nextorm_Prepared_ToDataReader",
            "B_Nextorm_ToDataReader",
            "Dapper_ToDataReader",
            "Linq2Db_ToDataReader",
        ),
        "arms": (
            {
                "id": "A_RawReader",
                "nextorm_method": "A_Nextorm_Prepared_ToDataReader",
                "comparator_method": "Linq2Db_ToDataReader",
                "diagnostic_method": None,
            },
            {
                "id": "B_RawReader",
                "nextorm_method": "B_Nextorm_ToDataReader",
                "comparator_method": "Linq2Db_ToDataReader",
                "diagnostic_method": "Dapper_ToDataReader",
            },
        ),
    },
    "empty": {
        "invocation_count": 4096,
        "invocation_count_authorized_max": 8192,
        "categories": ("B_RawReader_Empty",),
        "methods": (
            "B_Nextorm_ToDataReader_Empty",
            "Dapper_ToDataReader_Empty",
            "Linq2Db_ToDataReader_Empty",
        ),
        "arms": (
            {
                "id": "B_RawReader_Empty",
                "nextorm_method": "B_Nextorm_ToDataReader_Empty",
                "comparator_method": "Linq2Db_ToDataReader_Empty",
                "diagnostic_method": "Dapper_ToDataReader_Empty",
            },
        ),
    },
}

STRATUM_ORDER = ("10k", "empty")

# 99.9 % two-sided Student-t confidence interval: BDN serializes the legacy
# enum ordinal 12 for L999; the margin is t(0.9995, n-1) * stderr.
CONFIDENCE_LEVEL_PCT = 99.9
CONFIDENCE_LEVEL_ORDINAL = 12


class Reject(Exception):
    """Invalid, missing or unverifiable input — always fail closed (exit 2)."""

    def __init__(self, reason, details=None):
        super().__init__(reason)
        self.reason = reason
        self.details = details or {}


# ---------------------------------------------------------------------------
# Small IO helpers
# ---------------------------------------------------------------------------
def utc_now():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def monotonic():
    return time.monotonic()


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_text(text):
    return sha256_bytes(text.encode("utf-8"))


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_json(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def treatment_hash(treatment):
    return sha256_text(canonical_json(treatment))


def write_json(path, payload):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    with open(tmp, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2, ensure_ascii=False)
        handle.write("\n")
    os.replace(tmp, path)


def read_json(path):
    try:
        with open(path, "r", encoding="utf-8") as handle:
            return json.load(handle)
    except FileNotFoundError as exc:
        raise Reject("missing-file:%s" % path) from exc
    except (OSError, ValueError) as exc:
        raise Reject("unreadable-json:%s:%s" % (path, exc)) from exc


def write_text(path, text):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def read_text(path):
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        return handle.read()


def ensure_dir(path):
    Path(path).mkdir(parents=True, exist_ok=True)
    return Path(path)


def rel(path):
    try:
        return str(Path(path).resolve().relative_to(Path.cwd().resolve()))
    except (ValueError, OSError):
        return str(path)


def repo_root():
    return Path(__file__).resolve().parents[3]


# ---------------------------------------------------------------------------
# BDN value parsing / statistics
# ---------------------------------------------------------------------------
_TIME_UNITS = {
    "ns": 1.0,
    "us": 1e3,
    "μs": 1e3,
    "µs": 1e3,
    "ms": 1e6,
    "s": 1e9,
    "sec": 1e9,
    "secs": 1e9,
    "kb": 1e3,
    "mb": 1e6,
    "gb": 1e9,
}
_TIME_RE = re.compile(
    r"^(?P<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*(?P<unit>[A-Za-zμµ]+)$"
)


def _clean_number(text):
    return str(text).replace("\u00a0", " ").replace(",", "").strip()


def parse_size_bytes(text):
    """Parse a BDN ``Allocated`` cell (``12.74 KB``) into bytes, or None."""
    if text is None:
        return None
    cleaned = _clean_number(text).strip()
    if cleaned in ("", "-", "Default", "N/A", "NA", "?"):
        return None
    match = _TIME_RE.match(cleaned)
    if not match:
        return None
    unit = match.group("unit").lower()
    if unit not in ("b", "byte", "bytes", "kb", "mb", "gb"):
        return None
    value = float(match.group("value"))
    multiplier = 1.0 if unit in ("b", "byte", "bytes") else _TIME_UNITS[unit]
    return int(round(value * multiplier))


def parse_time_ns(text):
    """Parse a BDN time cell (``6.296 ms`` / ``745.4 μs``) into nanoseconds."""
    if text is None:
        return None
    cleaned = _clean_number(text).strip()
    if cleaned in ("", "-", "Default", "N/A", "NA", "?"):
        return None
    match = _TIME_RE.match(cleaned)
    if not match:
        return None
    unit = match.group("unit").lower()
    if unit not in _TIME_UNITS:
        return None
    return float(match.group("value")) * _TIME_UNITS[unit]


def mean(values):
    if not values:
        raise Reject("empty-statistics")
    return sum(values) / float(len(values))


def median(values):
    if not values:
        raise Reject("empty-statistics")
    ordered = sorted(values)
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return float(ordered[middle])
    return (ordered[middle - 1] + ordered[middle]) / 2.0


def sample_stddev(values):
    if len(values) < 2:
        raise Reject("insufficient-observations:%d" % len(values))
    center = mean(values)
    return math.sqrt(sum((value - center) ** 2 for value in values) / (len(values) - 1))


def _betacf(a, b, x):
    """Continued fraction for the regularized incomplete beta function."""
    max_iterations = 300
    epsilon = 3.0e-14
    fpmin = 1.0e-300
    qab = a + b
    qap = a + 1.0
    qam = a - 1.0
    c = 1.0
    d = 1.0 - qab * x / qap
    if abs(d) < fpmin:
        d = fpmin
    d = 1.0 / d
    h = d
    for m in range(1, max_iterations + 1):
        m2 = 2 * m
        aa = m * (b - m) * x / ((qam + m2) * (a + m2))
        d = 1.0 + aa * d
        if abs(d) < fpmin:
            d = fpmin
        c = 1.0 + aa / c
        if abs(c) < fpmin:
            c = fpmin
        d = 1.0 / d
        h *= d * c
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
        d = 1.0 + aa * d
        if abs(d) < fpmin:
            d = fpmin
        c = 1.0 + aa / c
        if abs(c) < fpmin:
            c = fpmin
        d = 1.0 / d
        delta = d * c
        h *= delta
        if abs(delta - 1.0) < epsilon:
            break
    return h


def _betai(a, b, x):
    if x <= 0.0:
        return 0.0
    if x >= 1.0:
        return 1.0
    log_front = (
        math.lgamma(a + b)
        - math.lgamma(a)
        - math.lgamma(b)
        + a * math.log(x)
        + b * math.log1p(-x)
    )
    front = math.exp(log_front)
    if x < (a + 1.0) / (a + b + 2.0):
        return front * _betacf(a, b, x) / a
    return 1.0 - front * _betacf(b, a, 1.0 - x) / b


def student_t_cdf(t, df):
    if df <= 0:
        raise Reject("invalid-degrees-of-freedom:%s" % df)
    x = df / (df + t * t)
    lower = 0.5 * _betai(df / 2.0, 0.5, x)
    return 1.0 - lower if t > 0 else lower


def student_t_quantile(probability, df):
    """Two-sided Student-t quantile by bisection (stdlib only)."""
    if not 0.5 < probability < 1.0:
        raise Reject("unsupported-t-probability:%s" % probability)
    low, high = 0.0, 1000.0
    for _ in range(200):
        middle = (low + high) / 2.0
        if student_t_cdf(middle, df) < probability:
            low = middle
        else:
            high = middle
    return (low + high) / 2.0


def confidence_error_ns(values, level_pct=CONFIDENCE_LEVEL_PCT):
    """BDN-style unrounded confidence Error (margin) for one launch.

    ``level_pct`` is the two-sided level: 99.9 % -> t quantile probability
    0.9995.  The computation is the same Student-t margin BDN exports in
    ``Statistics.ConfidenceInterval.Margin``.
    """
    n = len(values)
    if n < 2:
        raise Reject("insufficient-launch-observations:%d" % n)
    stderr = sample_stddev(values) / math.sqrt(n)
    probability = 1.0 - (1.0 - level_pct / 100.0) / 2.0
    return student_t_quantile(probability, n - 1) * stderr


def launch_statistics(values_ns, launch_index, requested_n=ITERATION_COUNT,
                      level_pct=CONFIDENCE_LEVEL_PCT):
    values = [float(value) for value in values_ns]
    stats = {
        "launch": launch_index,
        "n": len(values),
        "requested_n": requested_n,
        "confidence_level_pct": level_pct,
        "mean_ns": mean(values),
        "median_ns": median(values),
        "stddev_ns": sample_stddev(values),
        "min_ns": min(values),
        "max_ns": max(values),
    }
    stats["error_ns"] = confidence_error_ns(values, level_pct)
    stats["stderr_ns"] = stats["stddev_ns"] / math.sqrt(len(values))
    return stats


def pooled_statistics(values_ns, level_pct=CONFIDENCE_LEVEL_PCT):
    values = [float(value) for value in values_ns]
    return {
        "n": len(values),
        "mean_ns": mean(values),
        "median_ns": median(values),
        "stddev_ns": sample_stddev(values),
        "error_ns": confidence_error_ns(values, level_pct),
        "min_ns": min(values),
        "max_ns": max(values),
        "original_values": values,
    }


def relative_diff(a, b):
    scale = max(abs(a), abs(b), 1e-12)
    return abs(a - b) / scale


# ---------------------------------------------------------------------------
# BDN report discovery and parsing
# ---------------------------------------------------------------------------
CSV_COLUMNS = {
    "method": "Method",
    "job": "Job",
    "toolchain": "Toolchain",
    "categories": "Categories",
    "invocation_count": "InvocationCount",
    "unroll_factor": "UnrollFactor",
    "launch_count": "LaunchCount",
    "warmup_count": "WarmupCount",
    "iteration_count": "IterationCount",
    "mean": "Mean",
    "error": "Error",
    "stddev": "StdDev",
    "allocated": "Allocated",
}

DURATION_WARNING_PATTERNS = (
    re.compile(r"miniterationtime", re.IGNORECASE),
    re.compile(r"minimum iteration time", re.IGNORECASE),
    re.compile(r"iteration time (?:is )?too (?:small|short)", re.IGNORECASE),
    re.compile(r"too (?:small|short) (?:an? )?iteration time", re.IGNORECASE),
)


def _find_one(root, patterns):
    for pattern in patterns:
        matches = sorted(glob.glob(str(Path(root) / "**" / pattern), recursive=True))
        if matches:
            return matches[-1]
    return None


def discover_report_files(stratum_dir):
    stratum_dir = Path(stratum_dir)
    json_path = _find_one(
        stratum_dir,
        (
            "*-report-full-compressed.json",
            "*-report-full.json",
            "*-report.json",
        ),
    )
    csv_path = _find_one(stratum_dir, ("*-report.csv",))
    log_path = None
    candidates = sorted(
        glob.glob(str(Path(stratum_dir) / "**" / "*.log"), recursive=True)
    )
    candidates = [
        path for path in candidates
        if not Path(path).name.startswith(("bdn-stdout", "bdn-stderr"))
    ]
    if candidates:
        log_path = max(candidates, key=lambda path: os.path.getmtime(path))
    return {"json": json_path, "csv": csv_path, "log": log_path}


def _read_csv_rows(csv_path):
    if not csv_path:
        raise Reject("missing-bdn-csv")
    try:
        with open(csv_path, "r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            rows = [row for row in reader]
    except OSError as exc:
        raise Reject("unreadable-bdn-csv:%s" % exc) from exc
    if not rows:
        raise Reject("empty-bdn-csv")
    return rows


def _csv_categories(row):
    raw = row.get(CSV_COLUMNS["categories"], "") or ""
    return tuple(part.strip() for part in raw.split(",") if part.strip())


def _parse_int_field(row, key):
    raw = row.get(CSV_COLUMNS[key])
    if raw is None:
        raise Reject("missing-csv-column:%s" % CSV_COLUMNS[key])
    cleaned = _clean_number(raw)
    if cleaned in ("", "Default", "-"):
        raise Reject("unset-csv-count:%s" % CSV_COLUMNS[key])
    try:
        return int(cleaned)
    except ValueError as exc:
        raise Reject("invalid-csv-count:%s=%r" % (CSV_COLUMNS[key], raw)) from exc


# ---------------------------------------------------------------------------
# Resolved out-of-process job (H-N2)
# ---------------------------------------------------------------------------
# The required settings tuple of the effective default toolchain.  BDN names the
# auto-generated job differently on every machine (``Job-NREBDM`` in the rv2
# capture), so a literal ``Job=Default`` display name is neither necessary nor
# sufficient: the job is resolved from the retained full log header instead.
JOB_SETTING_ORDER = (
    "invocationcount",
    "iterationcount",
    "launchcount",
    "unrollfactor",
    "warmupcount",
)
JOB_SETTING_COUNTS = {
    "invocationcount": "invocation_count",
    "iterationcount": "iteration_count",
    "launchcount": "launch_count",
    "unrollfactor": "unroll_factor",
    "warmupcount": "warmup_count",
}
_JOB_HEADER_RE = re.compile(r"^\s*//\s*Job:\s*(?P<decl>.+?)\s*$")
_JOB_DECLARATION_RE = re.compile(
    r"^(?P<job_id>[A-Za-z0-9_.\-]+)\((?P<settings>[^()]*)\)$"
)
# ``WorkloadResult   1: 64 op, 259854520.00 ns, 4.0602 ms/op``: the retained
# value is the **iteration total**; per-op requires exactly one division by the
# logged operation count.
WORKLOAD_RESULT_RE = re.compile(
    r"^WorkloadResult\s+\d+:\s+(\d+)\s+op,\s+([0-9.]+)\s+ns,\s+[0-9.]+\s+(?:ns|μs|ms|s)/op"
)


def _parse_job_settings(settings_text):
    settings = {}
    for part in settings_text.split(","):
        part = part.strip()
        if not part:
            continue
        if "=" not in part:
            raise Reject("job-setting-unparseable:%s" % part)
        name, value = part.split("=", 1)
        name = name.strip().lower()
        value = value.strip()
        try:
            settings[name] = int(value)
        except ValueError as exc:
            raise Reject("job-setting-not-integer:%s=%s" % (name, value)) from exc
    return settings


def parse_job_header(log_text):
    """Return the first retained ``// Job:`` declaration of a BDN log.

    The full header is the only evidence that names the resolved settings:
    ``Job-NREBDM(InvocationCount=64, IterationCount=10, LaunchCount=3,
    UnrollFactor=1, WarmupCount=5)``.  A bare or truncated declaration returns
    an empty ``settings`` mapping so the caller can reject it explicitly.
    """
    if not log_text:
        return None
    for line in log_text.splitlines():
        match = _JOB_HEADER_RE.match(line)
        if not match:
            continue
        declaration = match.group("decl").strip()
        parsed = _JOB_DECLARATION_RE.match(declaration)
        if not parsed:
            return {
                "raw": declaration,
                "job_id": declaration.split("(", 1)[0].strip(),
                "settings": {},
            }
        return {
            "raw": declaration,
            "job_id": parsed.group("job_id"),
            "settings": _parse_job_settings(parsed.group("settings")),
        }
    return None


def resolve_job_view(job, toolchain, display_info, log_text, counts,
                     expected_invocation_count):
    """Resolve the effective job from the log header instead of its name.

    Accepted only when the retained log header resolves to the required
    settings tuple, the execution is out-of-process and the CSV ``Job`` cell
    agrees with the header's job id.  Every wrong or missing setting is named
    individually so the failure is observable in evidence.
    """
    expected = {
        "invocationcount": expected_invocation_count,
        "iterationcount": ITERATION_COUNT,
        "launchcount": LAUNCH_COUNT,
        "unrollfactor": UNROLL_FACTOR,
        "warmupcount": WARMUP_COUNT,
    }
    errors = []
    out_of_process = (
        "inprocess" not in (job + " " + toolchain).lower().replace(" ", "")
    )
    if not out_of_process:
        errors.append("job-toolchain-inprocess:%s" % toolchain)
    header = parse_job_header(log_text)
    resolved = None
    if header is None:
        errors.append("job-header-unresolved:no-log-job-header")
    else:
        settings = dict(header["settings"])
        if not settings:
            errors.append("job-header-unresolved:no-settings:%s" % header["raw"])
        for name in JOB_SETTING_ORDER:
            if name not in settings:
                errors.append("job-setting-missing:%s" % name)
                continue
            if settings[name] != expected[name]:
                errors.append(
                    "job-setting-mismatch:%s:%d!=%d"
                    % (name, settings[name], expected[name])
                )
            csv_value = (counts or {}).get(JOB_SETTING_COUNTS[name])
            if csv_value != settings[name]:
                errors.append(
                    "job-settings-csv-mismatch:%s:%d!=%r"
                    % (name, settings[name], csv_value)
                )
        if header["job_id"] != job:
            errors.append(
                "csv-log-job-mismatch:%s!=%s" % (job, header["job_id"])
            )
        resolved = {
            "job_id": header["job_id"],
            "settings": settings,
            "header": header["raw"],
        }
    return {
        "job": job,
        "toolchain": toolchain,
        "display_info": display_info,
        "out_of_process": out_of_process,
        "is_default": not errors,
        "resolved": resolved,
        "resolution_errors": errors,
    }


def _parse_log_launches(log_text):
    """Recover per-launch WorkloadResult values from the BDN log.

    The log marks each child launch with ``// Launch: i / N``; the subsequent
    ``WorkloadResult`` lines are the values that feed that launch's statistics.
    A ``WorkloadResult`` line reports the iteration total in ns plus the operation
    count (``64 op, 259854520.00 ns, 4.0602 ms/op``), so the value is normalised
    to ns/op exactly like the JSON ``Nanoseconds / Operations`` field.
    """
    launches = {}
    current = 0
    launch_re = re.compile(r"^//\s*Launch:\s*(\d+)\s*/\s*(\d+)\s*$")
    declared = None
    for line in log_text.splitlines():
        match = launch_re.match(line.strip())
        if match:
            current = int(match.group(1))
            count = int(match.group(2))
            if declared is None:
                declared = count
            elif declared != count:
                raise Reject("log-launch-count-drift:%s!=%s" % (declared, count))
            launches.setdefault(current, [])
            continue
        match = WORKLOAD_RESULT_RE.match(line.strip())
        if match and current:
            operations = int(match.group(1))
            if operations <= 0:
                raise Reject("log-operations-nonpositive:%d" % operations)
            launches[current].append(float(match.group(2)) / operations)
    return launches, declared


def _log_parity(log_text):
    rows = {}
    pattern = re.compile(
        r"\[D188\]\s+(?P<context>Streaming/[A-Za-z0-9_\-]+):\s+rows=(?P<rows>\d+),\s+checksum=(?P<checksum>-?\d+)"
    )
    for line in log_text.splitlines():
        match = pattern.search(line)
        if match:
            rows.setdefault(match.group("context"), []).append(
                (int(match.group("rows")), int(match.group("checksum")))
            )
    return rows


def duration_warning_line(text):
    for line in text.splitlines():
        for pattern in DURATION_WARNING_PATTERNS:
            if pattern.search(line):
                return line.strip()
    return None


def extract_launch_statistics(benchmark, requested_launches=LAUNCH_COUNT,
                              requested_n=ITERATION_COUNT):
    """Extract launch statistics from raw BDN ``Measurements``.

    A report that carries only pooled ``Statistics`` is rejected: BDN's export
    never authorizes copying pooled numbers into per-launch evidence.
    """
    measurements = benchmark.get("Measurements")
    if not isinstance(measurements, list) or not measurements:
        raise Reject("launch-statistics-unavailable:no-raw-measurements")
    by_launch = {}
    for measurement in measurements:
        if not isinstance(measurement, dict):
            raise Reject("malformed-measurement-row")
        if measurement.get("IterationMode") != "Workload":
            continue
        if measurement.get("IterationStage") != "Result":
            continue
        launch_index = measurement.get("LaunchIndex")
        if not isinstance(launch_index, int):
            raise Reject("malformed-launch-index")
        operations = measurement.get("Operations")
        nanoseconds = measurement.get("Nanoseconds")
        if not isinstance(operations, int) or operations <= 0:
            raise Reject("malformed-operations:%r" % (operations,))
        if not isinstance(nanoseconds, (int, float)) or nanoseconds <= 0:
            raise Reject("nonpositive-measurement:%r" % (nanoseconds,))
        by_launch.setdefault(launch_index, []).append(float(nanoseconds) / operations)
    expected = list(range(1, requested_launches + 1))
    if sorted(by_launch) != expected:
        raise Reject(
            "launch-count-mismatch:%s!=%s" % (sorted(by_launch), expected)
        )
    launches = []
    for launch_index in expected:
        values = by_launch[launch_index]
        if len(values) < 2:
            raise Reject("launch-%d-insufficient-results:%d" % (launch_index, len(values)))
        launches.append(launch_statistics(values, launch_index, requested_n))
    return launches


def validate_pooled_statistics(benchmark, launches):
    """Cross-check launch evidence against the exported pooled statistics.

    Also verifies the recorded confidence level and its numeric meaning:
    BDN exports the ``LegacyConfidenceLevel`` ordinal (12 == L999) and the
    margin must equal t(0.9995, N-1) * StandardError.
    """
    stats = benchmark.get("Statistics")
    if not isinstance(stats, dict):
        raise Reject("missing-pooled-statistics")
    interval = stats.get("ConfidenceInterval")
    if not isinstance(interval, dict):
        raise Reject("missing-confidence-interval")
    level = interval.get("Level")
    if level != CONFIDENCE_LEVEL_ORDINAL:
        raise Reject(
            "unsupported-confidence-level:%r (expected ordinal %d = 99.9%%)"
            % (level, CONFIDENCE_LEVEL_ORDINAL)
        )
    pooled_n = interval.get("N", stats.get("N"))
    pooled_mean = interval.get("Mean", stats.get("Mean"))
    stderr = interval.get("StandardError", stats.get("StandardError"))
    margin = interval.get("Margin")
    for name, value in (
        ("N", pooled_n),
        ("Mean", pooled_mean),
        ("StandardError", stderr),
        ("Margin", margin),
    ):
        if not isinstance(value, (int, float)) or isinstance(value, bool):
            raise Reject("missing-pooled-%s" % name)
    if pooled_n < 2 or pooled_mean <= 0 or margin <= 0:
        raise Reject("invalid-pooled-statistics")
    expected_margin = student_t_quantile(0.9995, int(pooled_n) - 1) * stderr
    if relative_diff(margin, expected_margin) > 1e-6:
        raise Reject(
            "confidence-level-margin-mismatch:%.9f!=%.9f" % (margin, expected_margin)
        )
    original = stats.get("OriginalValues")
    combined = []
    for launch in launches:
        combined.extend(launch.pop("_values", []))
    if isinstance(original, list) and original:
        if len(original) != len(combined):
            raise Reject(
                "pooled-statistics-mismatch:n=%d!=%d" % (len(original), len(combined))
            )
        if relative_diff(mean([float(value) for value in original]), mean(combined)) > 1e-9:
            raise Reject("pooled-statistics-mismatch:mean")
    return {
        "level_raw": level,
        "level_pct": CONFIDENCE_LEVEL_PCT,
        "n": int(pooled_n),
        "mean_ns": float(pooled_mean),
        "error_ns": float(margin),
        "stddev_ns": stats.get("StandardDeviation"),
        "median_ns": stats.get("Median"),
    }


def _attach_values(benchmark, requested_launches=LAUNCH_COUNT):
    """Return launches with the raw values kept privately for cross-checks."""
    measurements = benchmark.get("Measurements")
    if not isinstance(measurements, list) or not measurements:
        raise Reject("launch-statistics-unavailable:no-raw-measurements")
    by_launch = {}
    for measurement in measurements:
        if not isinstance(measurement, dict):
            raise Reject("malformed-measurement-row")
        if measurement.get("IterationMode") != "Workload" or measurement.get(
            "IterationStage"
        ) != "Result":
            continue
        launch_index = measurement.get("LaunchIndex")
        operations = measurement.get("Operations")
        nanoseconds = measurement.get("Nanoseconds")
        if not isinstance(launch_index, int):
            raise Reject("malformed-launch-index")
        if not isinstance(operations, int) or operations <= 0:
            raise Reject("malformed-operations")
        if not isinstance(nanoseconds, (int, float)) or nanoseconds <= 0:
            raise Reject("nonpositive-measurement")
        by_launch.setdefault(launch_index, []).append(float(nanoseconds) / operations)
    expected = list(range(1, requested_launches + 1))
    if sorted(by_launch) != expected:
        raise Reject("launch-count-mismatch:%s!=%s" % (sorted(by_launch), expected))
    launches = []
    for launch_index in expected:
        values = by_launch[launch_index]
        if len(values) < 2:
            raise Reject("launch-%d-insufficient-results:%d" % (launch_index, len(values)))
        stats = launch_statistics(values, launch_index, ITERATION_COUNT)
        stats["_values"] = values
        launches.append(stats)
    return launches


def parse_stratum(stratum_dir, stratum, invocation_count, require_log=True):
    """Parse one stratum directory into a normalized, validated report view."""
    if stratum not in STRATA:
        raise Reject("unknown-stratum:%s" % stratum)
    spec = STRATA[stratum]
    files = discover_report_files(stratum_dir)
    if not files["json"]:
        raise Reject("missing-bdn-json:%s" % stratum_dir)
    if not files["csv"]:
        raise Reject("missing-bdn-csv:%s" % stratum_dir)
    if require_log and not files["log"]:
        raise Reject("missing-bdn-log:%s" % stratum_dir)
    document = read_json(files["json"])
    benchmarks_raw = document.get("Benchmarks")
    if not isinstance(benchmarks_raw, list) or not benchmarks_raw:
        raise Reject("empty-bdn-json:%s" % stratum_dir)
    csv_rows = _read_csv_rows(files["csv"])

    duplicates = []
    json_methods = {}
    for benchmark in benchmarks_raw:
        method = benchmark.get("Method")
        if not method:
            raise Reject("benchmark-without-method")
        if method in json_methods:
            duplicates.append(method)
        json_methods[method] = benchmark
    csv_methods = {}
    for row in csv_rows:
        method = row.get(CSV_COLUMNS["method"])
        if not method:
            raise Reject("csv-row-without-method")
        if method in csv_methods:
            duplicates.append(method)
        csv_methods[method] = row

    expected = set(spec["methods"])
    selected_csv = set()
    for method, row in csv_methods.items():
        if any(category in _csv_categories(row) for category in spec["categories"]):
            selected_csv.add(method)
    if selected_csv != expected:
        raise Reject(
            "method-set-mismatch:%s:missing=%s:extra=%s"
            % (
                stratum,
                sorted(expected - selected_csv),
                sorted(selected_csv - expected),
            )
        )
    if set(json_methods) != expected:
        raise Reject(
            "json-method-set-mismatch:%s:missing=%s:extra=%s"
            % (
                stratum,
                sorted(expected - set(json_methods)),
                sorted(set(json_methods) - expected),
            )
        )

    job_row = next(iter(csv_methods.values()))
    job = (job_row.get(CSV_COLUMNS["job"]) or "").strip()
    toolchain = (job_row.get(CSV_COLUMNS["toolchain"]) or "").strip()
    display = benchmarks_raw[0].get("DisplayInfo") or ""

    counts = {
        "invocation_count": _parse_int_field(job_row, "invocation_count"),
        "unroll_factor": _parse_int_field(job_row, "unroll_factor"),
        "launch_count": _parse_int_field(job_row, "launch_count"),
        "warmup_count": _parse_int_field(job_row, "warmup_count"),
        "iteration_count": _parse_int_field(job_row, "iteration_count"),
    }
    log_text = read_text(files["log"]) if files["log"] else None
    job_view = resolve_job_view(
        job,
        toolchain,
        display,
        log_text,
        counts,
        invocation_count,
    )

    benchmarks = {}
    csv_errors = {}
    for method in spec["methods"]:
        benchmark = json_methods[method]
        row = csv_methods[method]
        row_counts = {
            "invocation_count": _parse_int_field(row, "invocation_count"),
            "unroll_factor": _parse_int_field(row, "unroll_factor"),
            "launch_count": _parse_int_field(row, "launch_count"),
            "warmup_count": _parse_int_field(row, "warmup_count"),
            "iteration_count": _parse_int_field(row, "iteration_count"),
        }
        if row_counts != counts:
            raise Reject("csv-count-drift:%s" % method)
        launches = _attach_values(benchmark, counts["launch_count"])
        pooled = validate_pooled_statistics(benchmark, launches)
        for launch in launches:
            launch.pop("_values", None)
        if counts["launch_count"] != LAUNCH_COUNT:
            raise Reject("launch-count-not-3:%s:%d" % (method, counts["launch_count"]))
        if len(launches) != LAUNCH_COUNT:
            raise Reject("launch-evidence-count:%s:%d" % (method, len(launches)))
        memory = benchmark.get("Memory") or {}
        bytes_per_op = memory.get("BytesAllocatedPerOperation")
        if not isinstance(bytes_per_op, int) or isinstance(bytes_per_op, bool):
            raise Reject("missing-allocation:%s" % method)
        csv_error = parse_time_ns(row.get(CSV_COLUMNS["error"]))
        if csv_error is None or csv_error <= 0:
            raise Reject("csv-rounded-or-absent-error:%s" % method)
        csv_error_ns = parse_time_ns(row.get(CSV_COLUMNS["error"]))
        if relative_diff(csv_error, pooled["error_ns"]) > 0.02:
            raise Reject("csv-error-disagrees-with-json:%s" % method)
        csv_mean = parse_time_ns(row.get(CSV_COLUMNS["mean"]))
        if csv_mean is None or relative_diff(csv_mean, pooled["mean_ns"]) > 0.02:
            raise Reject("csv-mean-disagrees-with-json:%s" % method)
        csv_alloc = parse_size_bytes(row.get(CSV_COLUMNS["allocated"]))
        if csv_alloc is None or csv_alloc == 0:
            raise Reject("csv-allocation-absent:%s" % method)
        benchmarks[method] = {
            "method": method,
            "categories": _csv_categories(row),
            "counts": row_counts,
            "launches": launches,
            "pooled": pooled,
            "bytes_per_op": bytes_per_op,
            "csv_error_ns": csv_error_ns,
            "csv_mean_ns": csv_mean,
            "csv_alloc_bytes": csv_alloc,
            "job": job_view,
        }
        csv_errors[method] = csv_error
    if duplicates:
        # The gate rejects duplicates explicitly; keep them in the view.
        duplicates = sorted(set(duplicates))

    parity_rows = {}
    warning = None
    if files["log"]:
        parity_rows = _log_parity(log_text)
        warning = duration_warning_line(log_text)
        method_blocks = _parse_log_method_blocks(log_text)
        for method, blocks in method_blocks.items():
            if method in benchmarks:
                for launch_index, values in blocks.items():
                    evidence = benchmarks[method]["launches"][launch_index - 1]
                    if len(values) != evidence["n"]:
                        raise Reject(
                            "log-json-launch-n-mismatch:%s:%d:%d!=%d"
                            % (method, launch_index, len(values), evidence["n"])
                        )
                    if relative_diff(mean(values), evidence["mean_ns"]) > 1e-9:
                        raise Reject(
                            "log-json-launch-mean-mismatch:%s:%d"
                            % (method, launch_index)
                        )

    return {
        "stratum": stratum,
        "dir": str(stratum_dir),
        "files": files,
        "job": job_view,
        "counts": counts,
        "benchmarks": benchmarks,
        "duplicates": duplicates,
        "parity": parity_rows,
        "duration_warning": warning,
        "host": document.get("HostEnvironmentInfo") or {},
        "title": document.get("Title"),
    }


def parse_log_workload_records(log_text):
    """Recover ``method -> [record]`` from a BDN log without dividing.

    BDN logs a ``// Benchmark: <type>.<method>: Job-...`` header before each
    benchmark and ``// Launch: i / N`` before each child launch.  Each
    ``WorkloadResult`` line carries the iteration total in ns and the operation
    count; the record keeps both so every caller can divide exactly once.
    """
    blocks = {}
    current_method = None
    current_launch = 0
    header_re = re.compile(r"^//\s*Benchmark:\s*([A-Za-z0-9_.]+)")
    launch_re = re.compile(r"^//\s*Launch:\s*(\d+)\s*/\s*(\d+)\s*$")
    for line in log_text.splitlines():
        stripped = line.strip()
        match = header_re.match(stripped)
        if match:
            full = match.group(1)
            current_method = full.split(".")[-1]
            current_launch = 0
            continue
        match = launch_re.match(stripped)
        if match:
            current_launch = int(match.group(1))
            continue
        match = WORKLOAD_RESULT_RE.match(stripped)
        if match and current_method and current_launch:
            operations = int(match.group(1))
            if operations <= 0:
                raise Reject("log-operations-nonpositive:%d" % operations)
            blocks.setdefault(current_method, []).append(
                {
                    "launch": current_launch,
                    "operations": operations,
                    "nanoseconds": float(match.group(2)),
                }
            )
    return blocks


def _parse_log_method_blocks(log_text):
    """Recover ``method -> launch -> [ns/op]`` from a BDN log.

    Each ``WorkloadResult`` line carries the iteration total in ns and the
    operation count, so it is normalised to ns/op exactly once to be comparable
    with the JSON ``Nanoseconds / Operations`` launch means.
    """
    blocks = {}
    for method, records in parse_log_workload_records(log_text).items():
        for record in records:
            blocks.setdefault(method, {}).setdefault(record["launch"], []).append(
                record["nanoseconds"] / record["operations"]
            )
    return blocks


def validate_stratum_view(view, stratum, expected_invocation_count,
                          required_categories=None):
    """Return a list of rejection reasons for a parsed stratum view."""
    reasons = []
    if view.get("duplicates"):
        reasons.append("duplicate-row:%s" % ",".join(view["duplicates"]))
    job = view.get("job") or {}
    if not job.get("out_of_process"):
        reasons.append("job-not-out-of-process:%s" % job.get("toolchain"))
    for reason in job.get("resolution_errors") or []:
        reasons.append(reason)
    if not job.get("is_default"):
        reasons.append("job-not-default:%s" % job.get("job"))
    counts = view.get("counts") or {}
    expected_counts = {
        "unroll_factor": UNROLL_FACTOR,
        "launch_count": LAUNCH_COUNT,
        "warmup_count": WARMUP_COUNT,
        "iteration_count": ITERATION_COUNT,
    }
    for key, expected in expected_counts.items():
        if counts.get(key) != expected:
            reasons.append("count-mismatch:%s:%r!=%r" % (key, counts.get(key), expected))
    if counts.get("invocation_count") != expected_invocation_count:
        reasons.append(
            "count-mismatch:invocation_count:%r!=%r"
            % (counts.get("invocation_count"), expected_invocation_count)
        )
    for method, benchmark in (view.get("benchmarks") or {}).items():
        launches = benchmark.get("launches") or []
        if len(launches) != LAUNCH_COUNT:
            reasons.append("launch-evidence-missing:%s:%d" % (method, len(launches)))
            continue
        for launch in launches:
            if launch.get("requested_n") != ITERATION_COUNT:
                reasons.append(
                    "requested-n-not-iteration-count:%s:%r"
                    % (method, launch.get("requested_n"))
                )
            if launch.get("requested_n") == expected_invocation_count:
                reasons.append("invocation-count-substituted-for-n:%s" % method)
            if launch.get("n") is None or launch.get("n") < 2:
                reasons.append("launch-n-missing:%s:%d" % (method, launch["launch"]))
            if not launch.get("error_ns") or launch["error_ns"] <= 0:
                reasons.append(
                    "launch-error-absent:%s:%d" % (method, launch["launch"])
                )
            if not launch.get("mean_ns") or launch["mean_ns"] <= 0:
                reasons.append(
                    "launch-mean-nonpositive:%s:%d" % (method, launch["launch"])
                )
        if not isinstance(benchmark.get("bytes_per_op"), int):
            reasons.append("allocation-missing:%s" % method)
    if view.get("duration_warning"):
        reasons.append("duration-warning:%s" % view["duration_warning"])
    return reasons


def parity_ok(parity_rows):
    """Require the harness parity evidence for every raw-reader arm."""
    if not parity_rows:
        return False, "parity-evidence-missing"
    required = (
        "Streaming/nextorm-buffered",
        "Streaming/nextorm-reader",
        "Streaming/dapper-reader",
        "Streaming/linq2db-reader",
    )
    for context in required:
        observations = parity_rows.get(context)
        if not observations:
            return False, "parity-context-missing:%s" % context
        for rows, checksum in observations:
            if rows != PARITY_ROWS or checksum != PARITY_CHECKSUM:
                return False, "parity-mismatch:%s:%s/%s" % (context, rows, checksum)
    return True, None


# ---------------------------------------------------------------------------
# Ratios, thresholds and modes
# ---------------------------------------------------------------------------
def time_upper_ratio(nextorm_mean_ns, nextorm_error_ns, comparator_mean_ns,
                     comparator_error_ns):
    if nextorm_mean_ns <= 0:
        raise Reject("nonpositive-nextorm-mean")
    denominator = comparator_mean_ns - comparator_error_ns
    if denominator <= 0:
        raise Reject(
            "nonpositive-comparator-lower-bound:%.6f" % denominator
        )
    return (nextorm_mean_ns + nextorm_error_ns) / denominator


def allocation_ratio(nextorm_bytes, comparator_bytes):
    if not isinstance(nextorm_bytes, (int, float)) or nextorm_bytes < 0:
        raise Reject("invalid-nextorm-allocation")
    if not isinstance(comparator_bytes, (int, float)) or comparator_bytes <= 0:
        raise Reject("nonpositive-comparator-allocation")
    return nextorm_bytes / comparator_bytes


def ceil4(value):
    """``ceil(10000 * value) / 10000`` with float-noise protection."""
    scaled = float(value) * 10000.0
    nearest = round(scaled)
    if abs(scaled - nearest) < 1e-7:
        scaled = float(nearest)
    return math.ceil(scaled) / 10000.0


def derive_thresholds(error_fraction, spread_fraction):
    e_star = ceil4(error_fraction + ERROR_MARGIN)
    s_star = ceil4(spread_fraction + SPREAD_MARGIN)
    if e_star > FROZEN_ERROR_CAP + 1e-12:
        raise Reject("frozen-error-cap-exceeded:%.4f>%.4f" % (e_star, FROZEN_ERROR_CAP))
    if s_star > FROZEN_SPREAD_CAP + 1e-12:
        raise Reject(
            "frozen-spread-cap-exceeded:%.4f>%.4f" % (s_star, FROZEN_SPREAD_CAP)
        )
    return e_star, s_star


def validate_requested_n(requested_n, invocation_count):
    if requested_n == invocation_count:
        raise Reject("invocation-count-substituted-for-statistical-n")
    if requested_n != ITERATION_COUNT:
        raise Reject(
            "requested-n-must-be-iteration-count:%r" % (requested_n,)
        )


def detect_labeled_modes(observations):
    """Fixed detector: sort launch medians, split only at adjacent gaps >= 15 %.

    ``observations`` is a list of dicts with ``run``, ``launch`` and ``median_ns``.
    The returned modes carry their member (run, launch) pairs; no averaging or
    post-result tuning is applied.
    """
    ordered = sorted(observations, key=lambda item: item["median_ns"])
    if not ordered:
        raise Reject("mode-detector-no-observations")
    groups = [[ordered[0]]]
    for previous, current in zip(ordered, ordered[1:]):
        previous_value = previous["median_ns"]
        if previous_value <= 0:
            raise Reject("mode-detector-nonpositive-median")
        gap = (current["median_ns"] - previous_value) / previous_value
        if gap >= MODE_GAP - 1e-12:
            groups.append([current])
        else:
            groups[-1].append(current)
    modes = []
    for index, group in enumerate(groups):
        modes.append(
            {
                "index": index,
                "min_ns": min(item["median_ns"] for item in group),
                "max_ns": max(item["median_ns"] for item in group),
                "center_ns": median([item["median_ns"] for item in group]),
                "members": [
                    {"run": item["run"], "launch": item["launch"]} for item in group
                ],
            }
        )
    return modes


def mode_replication_reasons(modes, required_runs):
    reasons = []
    for mode in modes:
        runs = sorted({member["run"] for member in mode["members"]})
        if runs != list(range(1, required_runs + 1)):
            reasons.append(
                "unreplicated-mode:%d:runs=%s" % (mode["index"], runs)
            )
    return reasons


def check_mode_declaration(observations, declared_modes, required_runs):
    """Reject declared modes that differ from the fixed detector's output.

    A declared single mode over observably split medians is exactly the
    "averaged/hidden modes" failure the gate must never accept.
    """
    detected = detect_labeled_modes(observations)
    reasons = []
    if len(declared_modes) != len(detected):
        reasons.append(
            "declared-modes-do-not-match-detected:declared=%d:detected=%d"
            % (len(declared_modes), len(detected))
        )
        return reasons
    declared_members = [
        sorted((member["run"], member["launch"]) for member in mode.get("members", []))
        for mode in declared_modes
    ]
    detected_members = [
        sorted((member["run"], member["launch"]) for member in mode["members"])
        for mode in detected
    ]
    if sorted(declared_members) != sorted(detected_members):
        reasons.append("declared-mode-membership-mismatch")
    reasons.extend(mode_replication_reasons(detected, required_runs))
    return reasons


def mode_ratio_spread(nextorm_modes, comparator_modes, nextorm_obs, comparator_obs,
                      required_runs):
    """Per-mode-pair spread over per-run mode ratios.

    Each per-run ratio is the median of the individual nextorm/comparator
    launch pairings inside that run (no pooled center is averaged into the
    spread); the spread is ``max/min - 1`` across the three runs.
    """
    results = []
    index_n = {(item["run"], item["launch"]): item for item in nextorm_obs}
    index_c = {(item["run"], item["launch"]): item for item in comparator_obs}
    for nextorm_mode in nextorm_modes:
        for comparator_mode in comparator_modes:
            per_run = {}
            for run in range(1, required_runs + 1):
                ratios = []
                for n_member in nextorm_mode["members"]:
                    if n_member["run"] != run:
                        continue
                    n_item = index_n[(n_member["run"], n_member["launch"])]
                    for c_member in comparator_mode["members"]:
                        if c_member["run"] != run:
                            continue
                        c_item = index_c[(c_member["run"], c_member["launch"])]
                        if c_item["mean_ns"] <= 0:
                            raise Reject("nonpositive-comparator-mean")
                        ratios.append(n_item["mean_ns"] / c_item["mean_ns"])
                if not ratios:
                    raise Reject(
                        "mode-pair-not-represented:run=%d:n=%d:c=%d"
                        % (run, nextorm_mode["index"], comparator_mode["index"])
                    )
                per_run[run] = median(ratios)
            values = [per_run[run] for run in range(1, required_runs + 1)]
            spread = max(values) / min(values) - 1.0
            results.append(
                {
                    "nextorm_mode": nextorm_mode["index"],
                    "comparator_mode": comparator_mode["index"],
                    "per_run_ratios": {str(run): per_run[run] for run in per_run},
                    "spread": spread,
                }
            )
    return results


# ---------------------------------------------------------------------------
# Campaign analysis shared by qualify/gate/modes
# ---------------------------------------------------------------------------
def _observations(benchmark, run_index):
    observations = []
    for launch in benchmark.get("launches") or []:
        observations.append(
            {
                "run": run_index,
                "launch": launch["launch"],
                "mean_ns": launch["mean_ns"],
                "median_ns": launch["median_ns"],
                "error_ns": launch["error_ns"],
                "n": launch["n"],
                "requested_n": launch["requested_n"],
            }
        )
    return observations


def analyze_campaign(run_reports, budgets, precision_cap, spread_cap,
                     required_runs=REQUIRED_RUNS, enforce_product_gates=True):
    """Analyze all arms across the supplied runs.

    ``run_reports`` maps run index -> ``{"manifest": ..., "strata": {...}}``.
    Returns predicates plus measured maxima; it never fabricates a value when a
    required arm, launch or statistic is missing.
    """
    input_errors = []
    reasons = []
    predicates = []
    arms_out = {}
    e_worst = 0.0
    spread_worst = 0.0
    upper_worst = 0.0
    alloc_worst = 0.0

    if sorted(run_reports) != list(range(1, required_runs + 1)):
        input_errors.append(
            "run-coverage:%s!=%s"
            % (sorted(run_reports), list(range(1, required_runs + 1)))
        )

    for row in budgets.get("rows", []):
        stratum = row.get("stratum") or row.get("id")
        if stratum not in STRATA:
            input_errors.append("budgets-unknown-stratum:%s" % stratum)
            continue
        if stratum == "empty" and float(row.get("empty_tolerance", 0) or 0) != 0.0:
            input_errors.append(
                "empty-tolerance-widened:%r" % (row.get("empty_tolerance"),)
            )
        spec = STRATA[stratum]
        for arm in row.get("required_arms", []):
            arm_id = arm.get("id")
            arm_key = "%s|%s" % (stratum, arm_id)
            nextorm_method = arm.get("nextorm_method")
            comparator_method = arm.get("comparator_method")
            diagnostic_method = arm.get("diagnostic_method")
            nextorm_obs = []
            comparator_obs = []
            arm_missing = False
            for run_index in range(1, required_runs + 1):
                run = run_reports.get(run_index)
                if not run:
                    input_errors.append("missing-run:%d" % run_index)
                    arm_missing = True
                    continue
                report = (run.get("strata") or {}).get(stratum)
                if not report:
                    input_errors.append("missing-stratum:run=%d:%s" % (run_index, stratum))
                    arm_missing = True
                    continue
                if report.get("duplicates"):
                    input_errors.append(
                        "duplicate-row:run=%d:%s:%s"
                        % (run_index, stratum, ",".join(report["duplicates"]))
                    )
                benchmarks = report.get("benchmarks") or {}
                nextorm = benchmarks.get(nextorm_method)
                comparator = benchmarks.get(comparator_method)
                if nextorm is None:
                    input_errors.append(
                        "missing-arm:run=%d:%s:%s" % (run_index, arm_key, nextorm_method)
                    )
                    arm_missing = True
                    continue
                if comparator is None:
                    input_errors.append(
                        "missing-arm:run=%d:%s:%s"
                        % (run_index, arm_key, comparator_method)
                    )
                    arm_missing = True
                    continue
                if diagnostic_method and benchmarks.get(diagnostic_method) is None:
                    input_errors.append(
                        "missing-diagnostic:run=%d:%s:%s"
                        % (run_index, arm_key, diagnostic_method)
                    )
                    arm_missing = True
                n_obs = _observations(nextorm, run_index)
                c_obs = _observations(comparator, run_index)
                nextorm_obs.extend(n_obs)
                comparator_obs.extend(c_obs)
                for launch in n_obs:
                    if launch["mean_ns"] <= 0:
                        input_errors.append(
                            "nonpositive-mean:%s:run=%d:launch=%d"
                            % (arm_key, run_index, launch["launch"])
                        )
                        continue
                    error_fraction = launch["error_ns"] / launch["mean_ns"]
                    e_worst = max(e_worst, error_fraction)
                    eligible = error_fraction <= precision_cap + 1e-12
                    predicates.append(
                        {
                            "id": "error-over-mean|%s|run%d|launch%d"
                            % (arm_key, run_index, launch["launch"]),
                            "kind": "error_over_mean",
                            "arm": arm_key,
                            "run": run_index,
                            "launch": launch["launch"],
                            "value": error_fraction,
                            "limit": precision_cap,
                            "pass": eligible,
                        }
                    )
                    if not eligible:
                        reasons.append(
                            "error-over-mean:%s:run=%d:launch=%d:%.6f>%.6f"
                            % (arm_key, run_index, launch["launch"], error_fraction,
                               precision_cap)
                        )
                for launch in c_obs:
                    if launch["mean_ns"] <= 0:
                        input_errors.append(
                            "nonpositive-comparator-mean:%s:run=%d:launch=%d"
                            % (arm_key, run_index, launch["launch"])
                        )
                        continue
                    error_fraction = launch["error_ns"] / launch["mean_ns"]
                    e_worst = max(e_worst, error_fraction)
                    eligible = error_fraction <= precision_cap + 1e-12
                    predicates.append(
                        {
                            "id": "error-over-mean|%s|run%d|launch%d|comparator"
                            % (arm_key, run_index, launch["launch"]),
                            "kind": "error_over_mean",
                            "arm": arm_key,
                            "run": run_index,
                            "launch": launch["launch"],
                            "value": error_fraction,
                            "limit": precision_cap,
                            "pass": eligible,
                        }
                    )
                    if not eligible:
                        reasons.append(
                            "comparator-error-over-mean:%s:run=%d:launch=%d:%.6f>%.6f"
                            % (arm_key, run_index, launch["launch"], error_fraction,
                               precision_cap)
                        )
                # Every launch pairing is evaluated; the largest upper ratio
                # governs.  No favorable pairing is selected.
                run_upper_max = 0.0
                for n_launch in n_obs:
                    for c_launch in c_obs:
                        try:
                            ratio = time_upper_ratio(
                                n_launch["mean_ns"],
                                n_launch["error_ns"],
                                c_launch["mean_ns"],
                                c_launch["error_ns"],
                            )
                        except Reject as exc:
                            input_errors.append(
                                "nonpositive-denominator:%s:run=%d:%s"
                                % (arm_key, run_index, exc.reason)
                            )
                            continue
                        run_upper_max = max(run_upper_max, ratio)
                        upper_worst = max(upper_worst, ratio)
                        eligible = ratio <= float(row.get("time_upper_ratio", 1.0)) + 1e-12
                        predicates.append(
                            {
                                "id": "time-upper-ratio|%s|run%d|nl%d-cl%d"
                                % (
                                    arm_key,
                                    run_index,
                                    n_launch["launch"],
                                    c_launch["launch"],
                                ),
                                "kind": "time_upper_ratio",
                                "arm": arm_key,
                                "run": run_index,
                                "launch": n_launch["launch"],
                                "comparator_launch": c_launch["launch"],
                                "value": ratio,
                                "limit": float(row.get("time_upper_ratio", 1.0)),
                                "pass": eligible,
                            }
                        )
                        if not eligible and enforce_product_gates:
                            reasons.append(
                                "time-upper-ratio:%s:run=%d:nl%d-cl%d:%.6f>%.6f"
                                % (
                                    arm_key,
                                    run_index,
                                    n_launch["launch"],
                                    c_launch["launch"],
                                    ratio,
                                    float(row.get("time_upper_ratio", 1.0)),
                                )
                            )
                try:
                    alloc = allocation_ratio(
                        nextorm.get("bytes_per_op"), comparator.get("bytes_per_op")
                    )
                except Reject as exc:
                    input_errors.append(
                        "allocation:%s:run=%d:%s" % (arm_key, run_index, exc.reason)
                    )
                    continue
                alloc_worst = max(alloc_worst, alloc)
                alloc_limit = float(row.get("allocation_ratio", 1.0))
                alloc_pass = alloc <= alloc_limit + 1e-12
                predicates.append(
                    {
                        "id": "allocation-ratio|%s|run%d" % (arm_key, run_index),
                        "kind": "allocation_ratio",
                        "arm": arm_key,
                        "run": run_index,
                        "value": alloc,
                        "limit": alloc_limit,
                        "pass": alloc_pass,
                    }
                )
                if not alloc_pass and enforce_product_gates:
                    reasons.append(
                        "allocation-ratio:%s:run=%d:%.6f>%.6f"
                        % (arm_key, run_index, alloc, alloc_limit)
                    )
            if arm_missing:
                continue
            nextorm_modes = detect_labeled_modes(nextorm_obs)
            comparator_modes = detect_labeled_modes(comparator_obs)
            replication = mode_replication_reasons(nextorm_modes, required_runs)
            replication.extend(mode_replication_reasons(comparator_modes, required_runs))
            for reason in replication:
                input_errors.append("mode:%s:%s" % (arm_key, reason))
            spreads = mode_ratio_spread(
                nextorm_modes,
                comparator_modes,
                nextorm_obs,
                comparator_obs,
                required_runs,
            )
            arm_spread_worst = 0.0
            for spread in spreads:
                arm_spread_worst = max(arm_spread_worst, spread["spread"])
                spread_worst = max(spread_worst, spread["spread"])
                eligible = spread["spread"] <= spread_cap + 1e-12
                predicates.append(
                    {
                        "id": "mode-spread|%s|n%d-c%d"
                        % (arm_key, spread["nextorm_mode"], spread["comparator_mode"]),
                        "kind": "mode_spread",
                        "arm": arm_key,
                        "nextorm_mode": spread["nextorm_mode"],
                        "comparator_mode": spread["comparator_mode"],
                        "per_run_ratios": spread["per_run_ratios"],
                        "value": spread["spread"],
                        "limit": spread_cap,
                        "pass": eligible,
                    }
                )
                if not eligible:
                    reasons.append(
                        "mode-spread:%s:n%d-c%d:%.6f>%.6f"
                        % (
                            arm_key,
                            spread["nextorm_mode"],
                            spread["comparator_mode"],
                            spread["spread"],
                            spread_cap,
                        )
                    )
            arms_out[arm_key] = {
                "stratum": stratum,
                "arm": arm_id,
                "nextorm_method": nextorm_method,
                "comparator_method": comparator_method,
                "diagnostic_method": diagnostic_method,
                "nextorm_modes": nextorm_modes,
                "comparator_modes": comparator_modes,
                "mode_spreads": spreads,
                "spread_worst": arm_spread_worst,
                "nextorm_observations": nextorm_obs,
                "comparator_observations": comparator_obs,
            }

    return {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "status": "PASS" if not input_errors and not reasons else "FAIL",
        "input_errors": input_errors,
        "reasons": reasons,
        "predicates": predicates,
        "arms": arms_out,
        "measured": {
            "e_worst": e_worst,
            "spread_worst": spread_worst,
            "upper_ratio_worst": upper_worst,
            "allocation_ratio_worst": alloc_worst,
        },
        "caps": {
            "error_over_mean": precision_cap,
            "spread": spread_cap,
            "required_runs": required_runs,
        },
    }


# ---------------------------------------------------------------------------
# Synthetic fixture builder (unit tests and self-test only)
# ---------------------------------------------------------------------------
__FIXTURE_RUNTIME = ".NET 10.0.12 (10.0.12, 10.0.1226.42308)"
__FIXTURE_OS = "Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)"
__FIXTURE_SDK = "10.0.401"


def _csv_row(method, categories, counts, mean_ns, error_ns, stddev_ns, alloc_bytes,
             job="Default", toolchain="Default"):
    def time_cell(value_ns):
        if value_ns >= 1e6:
            return "%.1f ms" % (value_ns / 1e6)
        if value_ns >= 1e3:
            return "%.1f μs" % (value_ns / 1e3)
        return "%.1f ns" % value_ns

    return {
        "Method": method,
        "Job": job,
        "Toolchain": toolchain,
        "Categories": categories,
        "InvocationCount": str(counts["invocation_count"]),
        "UnrollFactor": str(counts["unroll_factor"]),
        "LaunchCount": str(counts["launch_count"]),
        "WarmupCount": str(counts["warmup_count"]),
        "IterationCount": str(counts["iteration_count"]),
        "Mean": time_cell(mean_ns),
        "Error": time_cell(error_ns),
        "StdDev": time_cell(stddev_ns),
        "Allocated": "%.2f KB" % (alloc_bytes / 1000.0),
    }


def _launch_values(launch_mean):
    spread = launch_mean * 0.002
    return [
        launch_mean + spread * offset
        for offset in (-1.0, -0.5, 0.0, 0.5, 1.0, -0.7, 0.7, -0.2, 0.2, 0.1)
    ]


def _benchmark_json(method, launch_means, level_ordinal=CONFIDENCE_LEVEL_ORDINAL,
                    alloc_bytes=1000, launch_values=None, operations=1,
                    display_job=None):
    if not isinstance(operations, int) or operations < 1:
        raise Reject("fixture-operations-invalid:%r" % (operations,))
    values = []
    measurements = []
    for index, launch_mean in enumerate(launch_means, start=1):
        if launch_values and index in launch_values:
            launch_vals = launch_values[index]
        else:
            launch_vals = _launch_values(launch_mean)
        values.extend(launch_vals)
        for iteration, value in enumerate(launch_vals, start=1):
            measurements.append(
                {
                    "IterationMode": "Workload",
                    "IterationStage": "Result",
                    "LaunchIndex": index,
                    "IterationIndex": iteration,
                    "Operations": operations,
                    # BDN exports the iteration total; the per-operation value
                    # is ``Nanoseconds / Operations`` (see _attach_values).
                    "Nanoseconds": value * operations,
                }
            )
    pooled = pooled_statistics(values)
    pooled_stderr = pooled["stddev_ns"] / math.sqrt(len(values))
    interval = {
        "N": len(values),
        "Mean": pooled["mean_ns"],
        "StandardError": pooled_stderr,
        "Level": level_ordinal,
        "Margin": pooled["error_ns"],
        "Lower": pooled["mean_ns"] - pooled["error_ns"],
        "Upper": pooled["mean_ns"] + pooled["error_ns"],
    }
    statistics = {
        "OriginalValues": values,
        "N": len(values),
        "Min": min(values),
        "Q1": median(sorted(values)[: len(values) // 2]),
        "Median": pooled["median_ns"],
        "Mean": pooled["mean_ns"],
        "Q3": median(sorted(values)[len(values) // 2:]),
        "Max": max(values),
        "StandardError": pooled_stderr,
        "Variance": pooled["stddev_ns"] ** 2,
        "StandardDeviation": pooled["stddev_ns"],
        "ConfidenceInterval": interval,
    }
    return {
        "DisplayInfo": "%s.%s: %s"
        % (
            BENCH_TYPE,
            method,
            display_job or "Default(InvocationCount=64, UnrollFactor=1)",
        ),
        "Namespace": "NextORM.Benchmark",
        "Type": BENCH_TYPE,
        "Method": method,
        "MethodTitle": method,
        "Parameters": "",
        "FullName": "NextORM.Benchmark.%s.%s" % (BENCH_TYPE, method),
        "Statistics": statistics,
        "Memory": {
            "BytesAllocatedPerOperation": int(alloc_bytes),
            "TotalOperations": operations,
        },
        "Measurements": measurements,
    }


def build_stratum_fixture(stratum_dir, stratum, method_means, method_allocations,
                          counts=None, level_ordinal=CONFIDENCE_LEVEL_ORDINAL,
                          write_log=True, launch_values=None, labels=None,
                          operations=1, log_header_job=None, job_id="Default",
                          csv_job=None, toolchain="Default",
                          job_header_override=None, job_setting_overrides=None):
    """Write a synthetic BDN JSON/CSV/log triple for one stratum."""
    stratum_dir = ensure_dir(stratum_dir)
    results = ensure_dir(stratum_dir / "results")
    spec = STRATA[stratum]
    counts = counts or {
        "invocation_count": spec["invocation_count"],
        "unroll_factor": UNROLL_FACTOR,
        "launch_count": LAUNCH_COUNT,
        "warmup_count": WARMUP_COUNT,
        "iteration_count": ITERATION_COUNT,
    }
    if not isinstance(operations, int) or operations < 1:
        raise Reject("fixture-operations-invalid:%r" % (operations,))
    job_id = job_id or log_header_job or "Default"
    csv_job = job_id if csv_job is None else csv_job
    settings = {
        "InvocationCount": counts["invocation_count"],
        "IterationCount": counts["iteration_count"],
        "LaunchCount": counts["launch_count"],
        "UnrollFactor": counts["unroll_factor"],
        "WarmupCount": counts["warmup_count"],
    }
    settings.update(job_setting_overrides or {})
    declaration = job_header_override or "%s(%s)" % (
        job_id,
        ", ".join("%s=%s" % (name, value) for name, value in settings.items()),
    )
    benchmarks = []
    rows = []
    for method in spec["methods"]:
        launch_means = method_means[method]
        alloc = method_allocations[method]
        benchmark = _benchmark_json(
            method,
            launch_means,
            level_ordinal=level_ordinal,
            alloc_bytes=alloc,
            launch_values=launch_values,
            operations=operations,
            display_job=declaration,
        )
        benchmarks.append(benchmark)
        rows.append(
            _csv_row(
                method,
                ",".join(spec["categories"]),
                counts,
                benchmark["Statistics"]["Mean"],
                benchmark["Statistics"]["ConfidenceInterval"]["Margin"],
                benchmark["Statistics"]["StandardDeviation"],
                alloc,
                job=csv_job,
                toolchain=toolchain,
            )
        )
    document = {
        "Title": "%s-%s-fixture" % (BENCH_TYPE, stratum),
        "HostEnvironmentInfo": {
            "BenchmarkDotNetCaption": "BenchmarkDotNet",
            "BenchmarkDotNetVersion": "0.15.8",
            "OsVersion": __FIXTURE_OS,
            "RuntimeVersion": __FIXTURE_RUNTIME,
            "DotNetCliVersion": __FIXTURE_SDK,
            "LogicalCoreCount": 8,
            "Architecture": "X64",
            "Configuration": "RELEASE",
        },
        "Benchmarks": benchmarks,
    }
    write_json(results / ("%s-report-full-compressed.json" % BENCH_TYPE), document)
    columns = [
        "Method", "Job", "AnalyzeLaunchVariance", "EvaluateOverhead",
        "MaxAbsoluteError", "MaxRelativeError", "MinInvokeCount", "MinIterationTime",
        "OutlierMode", "Affinity", "EnvironmentVariables", "Jit", "LargeAddressAware",
        "Platform", "PowerPlanMode", "Runtime", "AllowVeryLargeObjects", "Concurrent",
        "CpuGroups", "Force", "HeapAffinitizeMask", "HeapCount", "NoAffinitize",
        "RetainVm", "Server", "Arguments", "BuildConfiguration", "Clock", "EngineFactory",
        "NuGetReferences", "Toolchain", "IsMutator", "InvocationCount", "IterationCount",
        "IterationTime", "LaunchCount", "MaxIterationCount", "MaxWarmupIterationCount",
        "MemoryRandomization", "MinIterationCount", "MinWarmupIterationCount",
        "RunStrategy", "UnrollFactor", "WarmupCount", "Categories", "Mean", "Error",
        "StdDev", "Gen0", "Gen1", "Gen2", "Allocated",
    ]
    csv_path = results / ("%s-report.csv" % BENCH_TYPE)
    with open(csv_path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    if write_log:
        lines = [
            "// BenchmarkDotNet v0.15.8",
            "Job=%s  Runtime=.NET 10.0  Toolchain=%s" % (job_id, toolchain),
            "// Job: %s" % declaration,
            "IterationCount=10  LaunchCount=3  WarmupCount=5",
        ]
        for method in spec["methods"]:
            lines.append("// Benchmark: %s.%s: %s"
                         % (BENCH_TYPE, method, declaration))
            for launch_index, launch_mean in enumerate(method_means[method], start=1):
                lines.append("// Launch: %d / %d" % (launch_index, LAUNCH_COUNT))
                for iteration, value in enumerate(
                    _launch_values(launch_mean), start=1
                ):
                    lines.append(
                        "WorkloadResult %3d: %d op, %.2f ns, %.4f ms/op"
                        % (iteration, operations, value * operations,
                           value / 1e6)
                    )
        lines.extend(
            [
                "[D188] Streaming/nextorm-buffered: rows=10000, checksum=422955955263575176",
                "[D188] Streaming/nextorm-reader: rows=10000, checksum=422955955263575176, bytes=360000",
                "[D188] Streaming/dapper-reader: rows=10000, checksum=422955955263575176, bytes=360000",
                "[D188] Streaming/linq2db-reader: rows=10000, checksum=422955955263575176, bytes=360000",
            ]
        )
        (stratum_dir / ("%s-fixture.log" % BENCH_TYPE)).write_text(
            "\n".join(lines) + "\n", encoding="utf-8"
        )
    return {
        "json": str(results / ("%s-report-full-compressed.json" % BENCH_TYPE)),
        "csv": str(csv_path),
        "log": str(stratum_dir / ("%s-fixture.log" % BENCH_TYPE)) if write_log else None,
    }


def _fixture_means(run_index, base_nextorm, base_comparator):
    run_shift = {1: 0.0, 2: 0.002, 3: -0.002}.get(run_index, 0.0)
    return [
        {
            "nextorm": base_nextorm * (1.0 + run_shift + launch_shift),
            "comparator": base_comparator * (1.0 + run_shift + launch_shift),
        }
        for launch_shift in (0.0, 0.001, -0.001)
    ]


def build_gate_fixture(base_dir, precision_cap=0.02, spread_cap=0.03):
    """Build a small but complete PASS fixture (three runs, both strata)."""
    base = ensure_dir(base_dir)
    perf = ensure_dir(base / "perf")
    final_dir = ensure_dir(perf / "final")
    treatment = {
        "job": "Default",
        "toolchain": "Default",
        "runtime": {
            "runtime_version": __FIXTURE_RUNTIME,
            "dotnet_cli_version": __FIXTURE_SDK,
            "os_version": __FIXTURE_OS,
        },
        "db_sha256": DB_SHA256,
        "db_size": DB_SEED_SIZE,
        "parity": {"rows": PARITY_ROWS, "checksum": PARITY_CHECKSUM},
        "invocation_counts": {"10k": 64, "empty": 4096},
        "counts": {
            "unroll_factor": UNROLL_FACTOR,
            "launch_count": LAUNCH_COUNT,
            "warmup_count": WARMUP_COUNT,
            "iteration_count": ITERATION_COUNT,
        },
        "method_sets": {stratum: list(STRATA[stratum]["methods"]) for stratum in STRATA},
    }
    lock = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "status": "frozen",
        "frozen_at": utc_now(),
        "confidence_level_pct": CONFIDENCE_LEVEL_PCT,
        "invocation_counts": {"10k": 64, "empty": 4096},
        "caps": {"E_star": precision_cap, "S_star": spread_cap},
        "operands": {"e_q": 0.01, "s_q": 0.01},
        "treatment": treatment,
        "treatment_hash": treatment_hash(treatment),
    }
    write_json(perf / "protocol-lock.json", lock)

    manifests = {}
    for run_index in (1, 2, 3):
        run_dir = ensure_dir(final_dir / ("run-%d" % run_index))
        manifest = {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "phase": "final",
            "attempt": run_index,
            "valid": True,
            "reason": None,
            "db_sha256": DB_SHA256,
            "db_size": DB_SEED_SIZE,
            "parity": {"rows": PARITY_ROWS, "checksum": PARITY_CHECKSUM},
            "invocation_counts": {"10k": 64, "empty": 4096},
            "counts": dict(treatment["counts"]),
            "runtime": dict(treatment["runtime"]),
            "treatment": treatment,
            "treatment_hash": lock["treatment_hash"],
            "strata": {},
            "host_monitor": {
                "ps_available": True,
                "frequency_available": True,
                "thermal_available": True,
                "foreign_activity": False,
                "thermal_throttle": False,
                "frequency_shift": 0.0,
            },
        }
        for stratum in STRATUM_ORDER:
            spec = STRATA[stratum]
            if stratum == "10k":
                pairs = _fixture_means(run_index, 5_000_000.0, 5_100_000.0)
                methods = {
                    "A_Nextorm_Prepared_ToDataReader": [item["nextorm"] for item in pairs],
                    "B_Nextorm_ToDataReader": [item["nextorm"] for item in pairs],
                    "Linq2Db_ToDataReader": [item["comparator"] for item in pairs],
                    "Dapper_ToDataReader": [item["comparator"] * 1.002 for item in pairs],
                }
                allocations = {
                    "A_Nextorm_Prepared_ToDataReader": 960_000,
                    "B_Nextorm_ToDataReader": 961_000,
                    "Linq2Db_ToDataReader": 961_024,
                    "Dapper_ToDataReader": 960_500,
                }
            else:
                pairs = _fixture_means(run_index, 1_000_000.0, 1_010_000.0)
                methods = {
                    "B_Nextorm_ToDataReader_Empty": [item["nextorm"] for item in pairs],
                    "Linq2Db_ToDataReader_Empty": [item["comparator"] for item in pairs],
                    "Dapper_ToDataReader_Empty": [item["comparator"] * 0.99 for item in pairs],
                }
                allocations = {
                    "B_Nextorm_ToDataReader_Empty": 800,
                    "Linq2Db_ToDataReader_Empty": 1040,
                    "Dapper_ToDataReader_Empty": 808,
                }
            build_stratum_fixture(
                run_dir / stratum,
                stratum,
                methods,
                allocations,
            )
            manifest["strata"][stratum] = {
                "exit_code": 0,
                "argv": ["fixture"],
                "db_sha_before": DB_SHA256,
                "db_sha_after": DB_SHA256,
                "dir": str(run_dir / stratum),
            }
        write_json(run_dir / "manifest.json", manifest)
        manifests[run_index] = manifest

    qualification = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "phase": "baseline",
        "status": "PASS",
        "reason": None,
        "caps": lock["caps"],
        "operands": lock["operands"],
        "treatment": treatment,
        "treatment_hash": lock["treatment_hash"],
        "runs": {
            str(run): {"manifest_sha256": sha256_text(canonical_json(manifests[run]))}
            for run in manifests
        },
    }
    write_json(perf / "qualification.json", qualification)

    budgets = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "contract_rv": 2,
        "required_runs": REQUIRED_RUNS,
        "confidence_level_pct": CONFIDENCE_LEVEL_PCT,
        "counts": dict(treatment["counts"]),
        "qualification_caps": {
            "error_over_mean": QUALIFICATION_ERROR_CAP,
            "spread": QUALIFICATION_SPREAD_CAP,
        },
        "rows": [
            {
                "id": stratum,
                "stratum": stratum,
                "invocation_count": STRATA[stratum]["invocation_count"],
                "invocation_count_authorized_max": STRATA[stratum][
                    "invocation_count_authorized_max"
                ],
                "time_upper_ratio": 1.0,
                "allocation_ratio": 1.0,
                "empty_tolerance": 0,
                "required_arms": [dict(arm) for arm in STRATA[stratum]["arms"]],
            }
            for stratum in STRATUM_ORDER
        ],
    }
    write_json(base / "budgets.json", budgets)
    return {
        "base": base,
        "perf": perf,
        "runs": final_dir,
        "lock": perf / "protocol-lock.json",
        "qualification": perf / "qualification.json",
        "budgets": base / "budgets.json",
    }


def load_gate_inputs(runs_dir, budgets_path):
    runs_dir = Path(runs_dir)
    budgets = read_json(budgets_path)
    lock = read_json(runs_dir.parent / "protocol-lock.json")
    qualification = read_json(runs_dir.parent / "qualification.json")
    run_dirs = sorted(
        [path for path in runs_dir.glob("run-*") if path.is_dir()],
        key=lambda path: int(path.name.split("-")[1]),
    )
    if not run_dirs:
        raise Reject("no-run-directories:%s" % runs_dir)
    runs = {}
    for run_dir in run_dirs:
        try:
            index = int(run_dir.name.split("-")[1])
        except (IndexError, ValueError) as exc:
            raise Reject("invalid-run-directory:%s" % run_dir) from exc
        if index in runs:
            raise Reject("duplicate-run-index:%d" % index)
        manifest = read_json(run_dir / "manifest.json")
        strata = {}
        for stratum in STRATUM_ORDER:
            spec = STRATA[stratum]
            invocation_count = (manifest.get("invocation_counts") or {}).get(
                stratum, spec["invocation_count"]
            )
            try:
                strata[stratum] = parse_stratum(
                    run_dir / stratum, stratum, invocation_count, require_log=True
                )
            except Reject as exc:
                strata[stratum] = {"parse_error": exc.reason, "benchmarks": {}}
        runs[index] = {"manifest": manifest, "strata": strata, "dir": str(run_dir)}
    return {"runs": runs, "budgets": budgets, "lock": lock, "qualification": qualification}


# ---------------------------------------------------------------------------
# Gate evaluation
# ---------------------------------------------------------------------------
def _number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _gate_fail_payload(command, reasons, input_errors):
    return {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "command": command,
        "generated_at": utc_now(),
        "status": "FAIL",
        "reasons": reasons,
        "input_errors": input_errors,
    }


def evaluate_gate(inputs, required_runs=REQUIRED_RUNS):
    """Fail-closed gate over loaded final runs and the frozen protocol lock."""
    budgets = inputs.get("budgets") or {}
    lock = inputs.get("lock") or {}
    qualification = inputs.get("qualification") or {}
    input_errors = []
    reasons = []

    lock_status = lock.get("status")
    if lock_status != "frozen":
        input_errors.append("protocol-lock-not-frozen:%r" % lock_status)
    treatment = lock.get("treatment") or {}
    computed_hash = treatment_hash(treatment)
    if lock.get("treatment_hash") != computed_hash:
        input_errors.append("protocol-lock-treatment-hash-mismatch")
    if qualification.get("status") != "PASS":
        input_errors.append(
            "qualification-not-pass:%r" % qualification.get("status")
        )
    if qualification.get("treatment_hash") != computed_hash:
        input_errors.append("stale-qualification-treatment-hash")
    if qualification.get("caps") != lock.get("caps"):
        input_errors.append("qualification-caps-differ-from-lock")
    if qualification.get("runs"):
        expected_runs = {str(index) for index in range(1, required_runs + 1)}
        if set(qualification["runs"]) != expected_runs:
            input_errors.append(
                "qualification-run-coverage:%s" % sorted(qualification["runs"])
            )

    caps = lock.get("caps") or {}
    e_star = caps.get("E_star")
    s_star = caps.get("S_star")
    if not _number(e_star) or e_star <= 0 or e_star > FROZEN_ERROR_CAP + 1e-12:
        input_errors.append("invalid-frozen-E-star:%r" % (e_star,))
    if not _number(s_star) or s_star <= 0 or s_star > FROZEN_SPREAD_CAP + 1e-12:
        input_errors.append("invalid-frozen-S-star:%r" % (s_star,))
    invocation_counts = lock.get("invocation_counts") or {}

    raw_runs = inputs.get("runs") or {}
    valid_runs = {}
    invalid_runs = []
    for index in sorted(raw_runs):
        run = raw_runs[index]
        manifest = run.get("manifest") or {}
        if manifest.get("valid") is True:
            valid_runs[index] = run
        else:
            invalid_runs.append(
                {
                    "attempt": index,
                    "reason": manifest.get("reason"),
                }
            )
    if len(valid_runs) != required_runs:
        input_errors.append(
            "valid-run-count:%d!=%d" % (len(valid_runs), required_runs)
        )
    mapped = {
        position + 1: valid_runs[index]
        for position, index in enumerate(sorted(valid_runs))
    }

    for position, run in mapped.items():
        manifest = run.get("manifest") or {}
        if manifest.get("db_sha256") != treatment.get("db_sha256"):
            input_errors.append(
                "db-sha-mismatch:run%d:%s" % (position, manifest.get("db_sha256"))
            )
        parity = manifest.get("parity") or {}
        if parity.get("rows") != PARITY_ROWS or parity.get("checksum") != PARITY_CHECKSUM:
            input_errors.append("parity-mismatch:run%d:%r" % (position, parity))
        if manifest.get("counts") != treatment.get("counts"):
            input_errors.append("count-mismatch:run%d" % position)
        if manifest.get("invocation_counts") != invocation_counts:
            input_errors.append("invocation-count-mismatch:run%d" % position)
        if manifest.get("treatment_hash") != computed_hash:
            input_errors.append("stale-run-treatment:run%d" % position)
        for stratum, record in (manifest.get("strata") or {}).items():
            if not isinstance(record, dict):
                input_errors.append("malformed-stratum-record:run%d:%s"
                                    % (position, stratum))
                continue
            if (
                record.get("db_sha_before") != treatment.get("db_sha256")
                or record.get("db_sha_after") != treatment.get("db_sha256")
            ):
                input_errors.append("db-sha-stratum-mismatch:run%d:%s"
                                    % (position, stratum))
        runtime = manifest.get("runtime") or {}
        want_runtime = treatment.get("runtime") or {}
        for key in ("runtime_version", "dotnet_cli_version"):
            if runtime.get(key) != want_runtime.get(key):
                input_errors.append("runtime-mismatch:run%d:%s" % (position, key))

        run_hashes = qualification.get("runs") or {}
        declared = run_hashes.get(str(position)) or {}
        actual_manifest_sha = sha256_text(canonical_json(manifest))
        if declared.get("manifest_sha256") not in (None, actual_manifest_sha):
            input_errors.append("stale-manifest-hash:run%d" % position)

        for stratum in STRATUM_ORDER:
            spec = STRATA[stratum]
            view = (run.get("strata") or {}).get(stratum)
            if not view:
                input_errors.append("missing-empty-arm" if stratum == "empty"
                                    else "missing-stratum:run%d:%s" % (position, stratum))
                continue
            if view.get("parse_error"):
                input_errors.append(
                    "unparseable-report:run%d:%s:%s"
                    % (position, stratum, view["parse_error"])
                )
                continue
            expected_invocation = invocation_counts.get(stratum)
            if expected_invocation not in (None, spec["invocation_count"],
                                           spec["invocation_count_authorized_max"]):
                input_errors.append(
                    "unauthorized-invocation-count:run%d:%s:%r"
                    % (position, stratum, expected_invocation)
                )
            view_reasons = validate_stratum_view(
                view,
                stratum,
                expected_invocation or spec["invocation_count"],
            )
            for reason in view_reasons:
                input_errors.append("run%d:%s:%s" % (position, stratum, reason))
            ok, reason = parity_ok(view.get("parity") or {})
            if not ok:
                input_errors.append("run%d:%s:%s" % (position, stratum, reason))
            host = view.get("host") or {}
            want_runtime = treatment.get("runtime") or {}
            if host.get("RuntimeVersion") != want_runtime.get("runtime_version"):
                input_errors.append(
                    "bdn-runtime-mismatch:run%d:%s" % (position, stratum)
                )
            if host.get("DotNetCliVersion") != want_runtime.get("dotnet_cli_version"):
                input_errors.append(
                    "bdn-sdk-mismatch:run%d:%s" % (position, stratum)
                )
            for method, benchmark in (view.get("benchmarks") or {}).items():
                pooled = benchmark.get("pooled") or {}
                if pooled.get("level_raw") != CONFIDENCE_LEVEL_ORDINAL:
                    input_errors.append(
                        "unsupported-confidence-level:run%d:%s:%s:%r"
                        % (position, stratum, method, pooled.get("level_raw"))
                    )
                if pooled.get("level_pct") != CONFIDENCE_LEVEL_PCT:
                    input_errors.append(
                        "unsupported-confidence-level-pct:run%d:%s:%s"
                        % (position, stratum, method)
                    )

    if len(input_errors) or len(reasons):
        return _gate_fail_payload("gate", reasons, input_errors) | {
            "treatment": {"matched": not input_errors},
            "invalid_runs": invalid_runs,
            "valid_runs": sorted(valid_runs),
        }

    try:
        analysis = analyze_campaign(
            mapped,
            budgets,
            float(e_star),
            float(s_star),
            required_runs=required_runs,
        )
    except Reject as exc:
        return _gate_fail_payload("gate", [exc.reason], ["analysis-rejected:%s" % exc.reason])

    reasons.extend(analysis["reasons"])
    input_errors.extend(analysis["input_errors"])
    status = "PASS" if not reasons and not input_errors else "FAIL"
    payload = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "command": "gate",
        "generated_at": utc_now(),
        "status": status,
        "reasons": reasons,
        "input_errors": input_errors,
        "treatment": {"matched": not input_errors},
        "invalid_runs": invalid_runs,
        "valid_runs": sorted(valid_runs),
        "caps": {"E_star": e_star, "S_star": s_star},
        "budgets": str(inputs.get("budgets_path") or ""),
        "predicates": analysis["predicates"],
        "measured": analysis["measured"],
        "arms": {
            key: {
                "stratum": arm["stratum"],
                "arm": arm["arm"],
                "nextorm_modes": arm["nextorm_modes"],
                "comparator_modes": arm["comparator_modes"],
                "mode_spreads": arm["mode_spreads"],
            }
            for key, arm in analysis["arms"].items()
        },
        "inputs": {
            "runs": str(inputs.get("runs_dir") or ""),
            "lock": str(inputs.get("lock_path") or ""),
            "qualification": str(inputs.get("qualification_path") or ""),
        },
    }
    return payload


# ---------------------------------------------------------------------------
# Self-test negative fixtures
# ---------------------------------------------------------------------------
def _mode_observations(values):
    observations = []
    run = 1
    launch = 1
    for value in values:
        observations.append(
            {"run": run, "launch": launch, "median_ns": value, "mean_ns": value,
             "error_ns": value * 0.001, "n": 10, "requested_n": ITERATION_COUNT}
        )
        launch += 1
        if launch > 3:
            launch = 1
            run += 1
    return observations


def self_test_cases():
    """Build the reference fixture, mutate it, and prove every bad input rejects."""
    import copy
    import tempfile

    temp_root = Path(tempfile.mkdtemp(prefix="t246-selftest-"))
    paths = build_gate_fixture(temp_root)
    base_inputs = load_gate_inputs(paths["runs"], paths["budgets"])
    base_result = evaluate_gate(base_inputs)
    if base_result["status"] != "PASS":
        raise Reject(
            "self-test-reference-fixture-not-pass:%s"
            % (base_result.get("input_errors") or base_result.get("reasons"))
        )

    cases = []

    def add_case(case_id, fragment, mutate):
        inputs = copy.deepcopy(base_inputs)
        try:
            fragment_value = mutate(inputs)
            if (
                isinstance(fragment_value, list)
                and fragment_value
                and all(isinstance(item, str) for item in fragment_value)
            ):
                rejected = True
                case_reasons = fragment_value
            else:
                result = evaluate_gate(inputs)
                rejected = result["status"] != "PASS"
                case_reasons = (result.get("input_errors") or []) + (
                    result.get("reasons") or []
                )
        except Exception as exc:  # fail closed: an exception is a rejection
            rejected = True
            case_reasons = ["exception:%s" % exc]
            fragment_value = fragment
        cases.append(
            {
                "id": case_id,
                "input_fragment": fragment_value or fragment,
                "expected_reject": True,
                "actual_reject": bool(rejected),
                "pass": bool(rejected),
                "reasons": case_reasons[:5],
            }
        )

    def report(inputs, run_index, stratum):
        return inputs["runs"][run_index]["strata"][stratum]

    add_case(
        "missing-arm",
        "delete A_Nextorm_Prepared_ToDataReader from run-1/10k",
        lambda inputs: report(inputs, 1, "10k")["benchmarks"].pop(
            "A_Nextorm_Prepared_ToDataReader"
        ),
    )
    add_case(
        "duplicate-row",
        "duplicate A_Nextorm_Prepared_ToDataReader row in run-1/10k",
        lambda inputs: report(inputs, 1, "10k")["duplicates"].append(
            "A_Nextorm_Prepared_ToDataReader"
        ),
    )
    add_case(
        "na-status",
        "BytesAllocatedPerOperation = null",
        lambda inputs: report(inputs, 1, "10k")["benchmarks"][
            "B_Nextorm_ToDataReader"
        ].__setitem__("bytes_per_op", None),
    )
    add_case(
        "rounded-or-absent-error",
        "Measurements removed; only pooled Statistics and rounded CSV Error remain",
        lambda inputs: report(inputs, 1, "10k")["benchmarks"][
            "B_Nextorm_ToDataReader"
        ].__setitem__("launches", []),
    )
    add_case(
        "missing-empty-arm",
        "run-2/empty absent",
        lambda inputs: inputs["runs"][2]["strata"].pop("empty"),
    )
    add_case(
        "missing-dapper",
        "Dapper_ToDataReader removed from run-1/10k",
        lambda inputs: report(inputs, 1, "10k")["benchmarks"].pop(
            "Dapper_ToDataReader"
        ),
    )
    add_case(
        "job-mismatch-inprocess",
        "Job=ShortRun Toolchain=InProcessEmitToolchain",
        lambda inputs: report(inputs, 1, "10k")["job"].update(
            {
                "job": "ShortRun",
                "toolchain": "InProcessEmitToolchain",
                "display_info": "Job-ABC InProcessEmitToolchain",
                "out_of_process": False,
                "is_default": False,
            }
        ),
    )
    add_case(
        "count-mismatch",
        "InvocationCount=32",
        lambda inputs: report(inputs, 1, "10k")["counts"].__setitem__(
            "invocation_count", 32
        ),
    )
    add_case(
        "db-sha-mismatch",
        "manifest db_sha256 all zeroes",
        lambda inputs: inputs["runs"][1]["manifest"].__setitem__("db_sha256", "0" * 64),
    )
    add_case(
        "parity-mismatch",
        "manifest parity rows=9999",
        lambda inputs: inputs["runs"][1]["manifest"]["parity"].__setitem__(
            "rows", 9999
        ),
    )
    add_case(
        "nonpositive-denominator",
        "comparator Mean-Error <= 0",
        lambda inputs: [
            launch.update({"mean_ns": 10.0, "error_ns": 20.0})
            for launch in report(inputs, 1, "10k")["benchmarks"][
                "Linq2Db_ToDataReader"
            ]["launches"]
        ],
    )

    def raise_error(inputs):
        benchmark = report(inputs, 1, "10k")["benchmarks"]["B_Nextorm_ToDataReader"]
        benchmark["launches"][0]["error_ns"] = benchmark["launches"][0]["mean_ns"] * 0.05

    add_case("error-above-cap", "launch Error/Mean = 5 % > E*", raise_error)

    def raise_spread(inputs):
        for run_index in (2, 3):
            for launch in report(inputs, run_index, "10k")["benchmarks"][
                "B_Nextorm_ToDataReader"
            ]["launches"]:
                launch["mean_ns"] *= 1.05
                launch["median_ns"] *= 1.05

    add_case("spread-above-cap", "run-2/3 matched-mode ratios shifted 5 %", raise_spread)

    add_case(
        "unsupported-confidence-level",
        "pooled CI Level=7 (L95) and level_pct=95.0",
        lambda inputs: report(inputs, 1, "10k")["benchmarks"][
            "B_Nextorm_ToDataReader"
        ]["pooled"].update({"level_raw": 7, "level_pct": 95.0}),
    )
    add_case(
        "stale-hash",
        "qualification treatment_hash = 0*64",
        lambda inputs: inputs["qualification"].__setitem__(
            "treatment_hash", "0" * 64
        ),
    )

    def averaged_modes(inputs):
        observations = _mode_observations([100.0, 101.0, 102.0, 140.0, 141.0, 142.0,
                                           100.5, 140.5, 101.5])
        declared = [
            {
                "index": 0,
                "members": [
                    {"run": item["run"], "launch": item["launch"]}
                    for item in observations
                ],
            }
        ]
        reasons = check_mode_declaration(observations, declared, 3)
        if not reasons:
            raise AssertionError("averaged modes were accepted")
        return reasons

    add_case(
        "averaged-modes",
        "one declared mode over an observable 15 %+ split",
        averaged_modes,
    )

    def unreplicated_mode(inputs):
        observations = _mode_observations([100.0, 101.0, 102.0, 140.0, 141.0, 142.0,
                                           100.5, 101.5, 102.5])
        modes = detect_labeled_modes(observations)
        reasons = mode_replication_reasons(modes, 3)
        if not reasons:
            raise AssertionError("unreplicated mode was accepted")
        return reasons

    add_case(
        "unreplicated-mode",
        "fast mode reproduced only in run 1",
        unreplicated_mode,
    )

    fixture_counts = {
        "invocation_count": 64,
        "unroll_factor": UNROLL_FACTOR,
        "launch_count": LAUNCH_COUNT,
        "warmup_count": WARMUP_COUNT,
        "iteration_count": ITERATION_COUNT,
    }
    fixture_means = {
        method: [5_000_000.0, 5_005_000.0, 5_010_000.0]
        for method in STRATA["10k"]["methods"]
    }
    fixture_allocations = {method: 1000 for method in STRATA["10k"]["methods"]}

    def unnormalised_iteration_total(inputs):
        fixture_root = temp_root / "unnormalised-64"
        build_stratum_fixture(
            fixture_root, "10k", fixture_means, fixture_allocations,
            counts=fixture_counts, operations=64,
        )
        log_path = next(fixture_root.glob("*.log"))
        log_path.write_text(
            log_path.read_text(encoding="utf-8").replace(" 64 op, ", " 1 op, "),
            encoding="utf-8",
        )
        try:
            parse_stratum(fixture_root, "10k", 64)
        except Reject as exc:
            if exc.reason.startswith("log-json-launch-mean-mismatch"):
                return [exc.reason]
            return []
        return []

    add_case(
        "unnormalised-iteration-total",
        "64-op WorkloadResult relabelled as 1 op, raw totals kept",
        unnormalised_iteration_total,
    )

    def job_reasons(case_name, **overrides):
        fixture_root = temp_root / case_name
        build_stratum_fixture(
            fixture_root, "10k", fixture_means, fixture_allocations,
            counts=fixture_counts, job_id="Job-NREBDM", **overrides
        )
        view = parse_stratum(fixture_root, "10k", 64)
        return validate_stratum_view(view, "10k", 64)

    def truncated_job_header(inputs):
        reasons = job_reasons("truncated-job", job_header_override="Job-NREBDM")
        if any(reason.startswith("job-header-unresolved") for reason in reasons):
            return reasons
        return []

    add_case(
        "resolved-job-truncated",
        "// Job: Job-NREBDM without the resolved settings tuple",
        truncated_job_header,
    )

    def csv_log_job_mismatch(inputs):
        reasons = job_reasons("csv-job-mismatch", csv_job="Job-OTHER")
        if "csv-log-job-mismatch:Job-OTHER!=Job-NREBDM" in reasons:
            return reasons
        return []

    add_case(
        "csv-log-job-mismatch",
        "CSV Job=Job-OTHER while the log header resolves Job-NREBDM",
        csv_log_job_mismatch,
    )

    def active_testhost_with_idle_compiler_server(inputs):
        processes = [
            {
                "pid": 4100, "pcpu": 38.5, "comm": "VBCSCompiler",
                "args": "dotnet exec VBCSCompiler.dll",
            },
            {
                "pid": 4200, "pcpu": 55.0, "comm": "testhost",
                "args": "testhost --port 4242",
            },
        ]
        foreign = _foreign_processes(processes, {424242})
        if [item["pid"] for item in foreign] == [4200]:
            return ["foreign-process:%d" % foreign[0]["pid"]]
        return []

    add_case(
        "active-testhost-with-idle-compiler-server",
        "active testhost alongside an idle VBCSCompiler",
        active_testhost_with_idle_compiler_server,
    )

    payload = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "command": "self-test",
        "generated_at": utc_now(),
        "status": "PASS" if all(case["pass"] for case in cases) else "FAIL",
        "reference_fixture": {
            "status": base_result["status"],
            "caps": base_result.get("caps"),
        },
        "cases": cases,
    }
    shutil.rmtree(temp_root, ignore_errors=True)
    return payload


# ---------------------------------------------------------------------------
# Attempt ledger, budget, lock and campaign
# ---------------------------------------------------------------------------
def ledger_path(root):
    return Path(root) / "perf" / "attempt-ledger.json"


def load_ledger(root, caller_argv=None):
    path = ledger_path(root)
    if not path.exists():
        ledger = {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "created_at": utc_now(),
            "caller_argv": list(caller_argv or []),
            "overhead_seconds": 0.0,
            "lock_history": [],
            "attempts": [],
            "remedies": {},
            "invocation_counts": {},
        }
        write_json(path, ledger)
        return ledger
    ledger = read_json(path)
    if ledger.get("task") != TASK:
        raise Reject("attempt-ledger-task-mismatch")
    ledger.setdefault("attempts", [])
    ledger.setdefault("lock_history", [])
    ledger.setdefault("remedies", {})
    ledger.setdefault("invocation_counts", {})
    ledger.setdefault("overhead_seconds", 0.0)
    return ledger


def save_ledger(root, ledger):
    write_json(ledger_path(root), ledger)


def ledger_used_seconds(ledger):
    used = float(ledger.get("overhead_seconds") or 0.0)
    for attempt in ledger.get("attempts", []):
        try:
            used += float(attempt.get("duration_seconds") or 0.0)
        except (TypeError, ValueError):
            raise Reject("invalid-ledger-duration")
    return used


def phase_attempts(ledger, phase):
    return [attempt for attempt in ledger.get("attempts", [])
            if attempt.get("phase") == phase]


def check_campaign_limits(ledger, phase, max_attempts, total_budget):
    used = ledger_used_seconds(ledger)
    if used >= float(total_budget):
        raise Reject(
            "measurement-budget-exhausted:%.1f>=%.1f" % (used, float(total_budget))
        )
    if len(phase_attempts(ledger, phase)) >= int(max_attempts):
        raise Reject(
            "phase-attempt-ceiling:%s:%d"
            % (phase, len(phase_attempts(ledger, phase)))
        )
    if len(ledger.get("attempts", [])) >= MAX_TOTAL_RUNS:
        raise Reject(
            "total-execution-ceiling:%d" % len(ledger.get("attempts", []))
        )
    return used


class _HostLock:
    def __init__(self, lock_path, wait_seconds):
        self.lock_path = Path(lock_path)
        self.wait_seconds = float(wait_seconds)
        self.fd = None
        self.meta = {}

    def __enter__(self):
        ensure_dir(self.lock_path.parent)
        self.fd = os.open(str(self.lock_path), os.O_CREAT | os.O_RDWR, 0o644)
        started = monotonic()
        while True:
            try:
                fcntl.flock(self.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                elapsed = monotonic() - started
                if elapsed >= self.wait_seconds:
                    os.close(self.fd)
                    self.fd = None
                    raise Reject("lock-timeout:%s" % self.lock_path)
                time.sleep(min(1.0, max(0.05, self.wait_seconds - elapsed)))
        self.meta = {
            "path": str(self.lock_path),
            "acquired_at": utc_now(),
            "wait_seconds": round(monotonic() - started, 6),
            "acquisitions": 1,
        }
        return self.meta

    def __exit__(self, exc_type, exc, tb):
        if self.fd is not None:
            self.meta["released_at"] = utc_now()
            try:
                fcntl.flock(self.fd, fcntl.LOCK_UN)
            finally:
                os.close(self.fd)
                self.fd = None
        return False


def host_lock(lock_path, wait_seconds):
    return _HostLock(lock_path, wait_seconds)


def apply_invocation_remedy(ledger, phase, stratum, current_counts, attempt_index):
    """Apply the one authorized InvocationCount doubling for a stratum."""
    if stratum not in STRATA:
        raise Reject("unknown-stratum:%s" % stratum)
    spec = STRATA[stratum]
    remedies = ledger.setdefault("remedies", {}).setdefault(phase, {})
    if remedies.get(stratum):
        raise Reject("second-duration-warning:%s:%s" % (phase, stratum))
    from_count = int(current_counts.get(stratum, spec["invocation_count"]))
    to_count = from_count * 2
    if to_count > spec["invocation_count_authorized_max"]:
        raise Reject(
            "invocation-increase-exceeds-authorized-max:%s:%d"
            % (stratum, to_count)
        )
    remedies[stratum] = {
        "from": from_count,
        "to": to_count,
        "authorized_invocation_count": to_count,
        "iteration_count_unchanged": ITERATION_COUNT,
        "attempt": attempt_index,
        "applied_at": utc_now(),
    }
    for attempt in ledger.get("attempts", []):
        if attempt.get("phase") != phase:
            continue
        if attempt.get("valid") and attempt.get("invocation_counts") != {
            **current_counts,
            stratum: to_count,
        }:
            attempt["valid"] = False
            attempt["invalidated_by_remedy"] = True
            attempt["reason"] = "invocation-count-remedy-incompatible"
    ledger.setdefault("invocation_counts", {})
    ledger["invocation_counts"].setdefault(phase, {})[stratum] = to_count
    return to_count


def measure_campaign(config, execute):
    """Run one complete block under exactly one exclusive host lock.

    ``execute(config, attempt_index, invocation_counts)`` returns a dict with
    at least ``valid``, ``reason`` and ``duration_seconds``.  It may return a
    ``remedy`` dict ``{"stratum": ...}`` after the first duration warning.
    """
    root = Path(config["root"])
    phase = config["phase"]
    ledger = load_ledger(root, config.get("caller_argv"))
    status = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "phase": phase,
        "started_at": utc_now(),
        "status": "RUNNING",
        "reason": None,
        "valid_runs": [],
        "attempts": [],
    }
    status_path = ensure_dir(root / "perf" / phase) / "measure-status.json"
    try:
        check_campaign_limits(
            ledger,
            phase,
            config["max_attempts"],
            config["total_budget_seconds"],
        )
    except Reject as exc:
        status.update({"status": "REJECT", "reason": exc.reason})
        write_json(status_path, status)
        raise

    locked_counts = config.get("locked_invocation_counts") or {}
    invocation_counts = {
        stratum: int(
            locked_counts.get(
                stratum,
                (ledger.get("invocation_counts", {}).get(phase, {}) or {}).get(
                    stratum, STRATA[stratum]["invocation_count"]
                ),
            )
        )
        for stratum in STRATUM_ORDER
    }
    valid_count = len(
        [
            attempt
            for attempt in phase_attempts(ledger, phase)
            if attempt.get("valid")
        ]
    )
    lock_meta = {}
    try:
        with host_lock(config["lock"], config["lock_wait_seconds"]) as lock_meta:
            ledger.setdefault("lock_history", []).append(dict(lock_meta))
            save_ledger(root, ledger)
            quiet = config.get("quiet_probe")
            if quiet and config.get("quiet_seconds", 0) > 0:
                started = monotonic()
                verdict = quiet(config["quiet_seconds"], root, phase)
                elapsed = monotonic() - started
                ledger["overhead_seconds"] = (
                    float(ledger.get("overhead_seconds") or 0.0) + elapsed
                )
                save_ledger(root, ledger)
                status["quiet_window"] = {
                    "ok": verdict.get("ok"),
                    "reason": verdict.get("reason"),
                    "loadavg_max": verdict.get("loadavg_max"),
                    "foreign_pids": verdict.get("foreign_pids"),
                }
                if not verdict.get("ok"):
                    status.update(
                        {
                            "status": "REJECT",
                            "reason": "quiet-window-rejected:%s"
                            % verdict.get("reason"),
                        }
                    )
                    write_json(status_path, status)
                    raise Reject(status["reason"])
            while valid_count < int(config["runs"]):
                try:
                    check_campaign_limits(
                        ledger,
                        phase,
                        config["max_attempts"],
                        config["total_budget_seconds"],
                    )
                except Reject:
                    break
                attempt_index = (
                    max(
                        [attempt.get("attempt", 0) for attempt in phase_attempts(ledger, phase)]
                        + [0]
                    )
                    + 1
                )
                attempt_started_at = utc_now()
                started = monotonic()
                config["_remedies"] = ledger.get("remedies", {}).get(phase, {})
                try:
                    result = execute(config, attempt_index, dict(invocation_counts))
                except Reject as exc:
                    result = {
                        "valid": False,
                        "reason": exc.reason,
                        "duration_seconds": monotonic() - started,
                    }
                duration = float(result.get("duration_seconds") or (monotonic() - started))
                entry = {
                    "phase": phase,
                    "attempt": attempt_index,
                    "started_at": attempt_started_at,
                    "finished_at": utc_now(),
                    "duration_seconds": round(duration, 3),
                    "valid": bool(result.get("valid")),
                    "reason": result.get("reason"),
                    "invocation_counts": dict(invocation_counts),
                    "exit_codes": {
                        record.get("stratum"): record.get("exit_code")
                        for record in (result.get("strata") or [])
                    },
                    "strata": result.get("strata") or [],
                    "run_dir": result.get("run_dir"),
                    "manifest": result.get("manifest"),
                    "remedy": result.get("remedy"),
                }
                ledger.setdefault("attempts", []).append(entry)
                save_ledger(root, ledger)
                status["attempts"].append(
                    {"attempt": attempt_index, "valid": entry["valid"],
                     "reason": entry["reason"]}
                )
                if result.get("remedy"):
                    stratum = result["remedy"].get("stratum")
                    try:
                        new_count = apply_invocation_remedy(
                            ledger, phase, stratum, invocation_counts, attempt_index
                        )
                        invocation_counts[stratum] = new_count
                        entry["reason"] = "duration-warning-remedy-applied"
                        valid_count = len(
                            [
                                attempt
                                for attempt in phase_attempts(ledger, phase)
                                if attempt.get("valid")
                            ]
                        )
                    except Reject as exc:
                        entry["valid"] = False
                        entry["reason"] = exc.reason
                    save_ledger(root, ledger)
                elif entry["valid"]:
                    valid_count += 1
                    status["valid_runs"].append(
                        {"attempt": attempt_index, "run_dir": entry["run_dir"]}
                    )
    except Reject as exc:
        status.update({"status": "REJECT", "reason": exc.reason})
        write_json(status_path, status)
        raise
    finally:
        if lock_meta:
            if ledger.get("lock_history"):
                ledger["lock_history"][-1] = dict(lock_meta)
            ledger["overhead_seconds"] = float(
                ledger.get("overhead_seconds") or 0.0
            ) + float(lock_meta.get("wait_seconds") or 0.0)
            save_ledger(root, ledger)

    if valid_count >= int(config["runs"]):
        status.update(
            {
                "status": "PASS",
                "reason": None,
                "finished_at": utc_now(),
                "valid_run_count": valid_count,
            }
        )
        write_json(status_path, status)
        return {"exit_code": EXIT_OK, "valid_runs": valid_count, "status": status}

    used = ledger_used_seconds(ledger)
    if used >= float(config["total_budget_seconds"]):
        reason = "TERMINAL_GAP:measurement-budget-exhausted"
    elif len(phase_attempts(ledger, phase)) >= int(config["max_attempts"]):
        reason = "TERMINAL_GAP:phase-attempt-ceiling"
    elif len(ledger.get("attempts", [])) >= MAX_TOTAL_RUNS:
        reason = "TERMINAL_GAP:total-execution-ceiling"
    else:
        reason = "TERMINAL_GAP:valid-runs-unavailable"
    status.update(
        {"status": "GAP", "reason": reason, "finished_at": utc_now(),
         "valid_run_count": valid_count}
    )
    write_json(status_path, status)
    write_json(
        ensure_dir(root / "perf" / phase) / "TERMINAL_GAP.json",
        {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "phase": phase,
            "reason": reason,
            "used_seconds": used,
            "attempts": len(phase_attempts(ledger, phase)),
            "ledger": str(ledger_path(root)),
            "generated_at": utc_now(),
        },
    )
    return {"exit_code": EXIT_GAP, "valid_runs": valid_count, "status": status}


# ---------------------------------------------------------------------------
# Host monitoring (1 Hz samples, JSONL)
# ---------------------------------------------------------------------------
_FORBIDDEN_DOTNET = re.compile(r"\bdotnet\b.*\b(build|test|publish)\b", re.IGNORECASE)
_TESTHOST = re.compile(r"\btesthost\b", re.IGNORECASE)
_COVERAGE = re.compile(r"\bdotnet-coverage\b", re.IGNORECASE)
# Shared build servers are not attributable to a process tree and must never count as
# "active compiler work".  ``VBCSCompiler`` outlives the build that started it and is
# re-used by every later build on the machine -- including the driver's own
# ``dotnet run`` and BDN instrumentation build -- so it is not a descendant of the
# driver, while ``ps`` %CPU is a *lifetime* average: an idle server that compiled once
# keeps a large value forever (observed: 34-40 % lifetime, 0.00 % instantaneous) and
# would otherwise reject the quiet window / the run itself.  MSBuild ``/nodemode``
# entries are the same kind of idle reuse nodes.  A foreign build is still detected
# through its client processes (``dotnet build|test|publish``, csc, testhost,
# dotnet-coverage, BDN) below.
_COMPILER_SERVER = re.compile(r"\bVBCSCompiler\b|/nodemode:\d+", re.IGNORECASE)
_CSC = re.compile(r"\bcsc(?:\.dll)?(?=\s|$)", re.IGNORECASE)
_BDN = re.compile(r"benchmarkdotnet", re.IGNORECASE)


def _read_cpu_times():
    try:
        with open("/proc/stat", "r", encoding="utf-8") as handle:
            parts = handle.readline().split()
        values = [int(value) for value in parts[1:]]
        if len(values) < 4:
            return None
        idle = values[3] + (values[4] if len(values) > 4 else 0)
        return sum(values), idle
    except (OSError, ValueError):
        return None


def _read_proc_cpu_seconds(pid):
    if not pid:
        return None
    try:
        text = Path("/proc/%d/stat" % int(pid)).read_text(encoding="utf-8")
    except OSError:
        return None
    try:
        rest = text[text.rfind(")") + 2:].split()
        return (int(rest[11]) + int(rest[12])) / float(os.sysconf("SC_CLK_TCK"))
    except (IndexError, ValueError, OSError):
        return None


def _read_frequencies_mhz():
    values = []
    for path in sorted(glob.glob("/sys/devices/system/cpu/cpu*/cpufreq/scaling_cur_freq")):
        try:
            values.append(int(Path(path).read_text(encoding="utf-8").strip()) / 1000.0)
        except (OSError, ValueError):
            continue
    if values:
        return values
    try:
        text = Path("/proc/cpuinfo").read_text(encoding="utf-8")
    except OSError:
        return []
    for line in text.splitlines():
        if line.lower().startswith("cpu mhz"):
            try:
                values.append(float(line.split(":", 1)[1].strip()))
            except (IndexError, ValueError):
                continue
    return values


def _read_temperatures():
    values = []
    for path in sorted(glob.glob("/sys/class/thermal/thermal_zone*/temp")):
        try:
            values.append(int(Path(path).read_text(encoding="utf-8").strip()) / 1000.0)
        except (OSError, ValueError):
            continue
    return values


def _read_throttle_total():
    total = 0
    found = False
    for path in glob.glob(
        "/sys/devices/system/cpu/cpu*/thermal_throttle/*_throttle_count"
    ):
        try:
            total += int(Path(path).read_text(encoding="utf-8").strip())
            found = True
        except (OSError, ValueError):
            continue
    return total if found else None


def _list_processes():
    try:
        proc = subprocess.run(
            ["ps", "-eo", "pid,pcpu,comm,args", "--no-headers"],
            capture_output=True,
            text=True,
            timeout=10,
        )
    except Exception:
        # Fail closed: no process list means foreign activity cannot be ruled
        # out, so the monitor marks the sensor unavailable.
        return None
    if proc.returncode != 0:
        return None
    processes = []
    for line in proc.stdout.splitlines():
        parts = line.split(None, 3)
        if len(parts) < 3:
            continue
        try:
            pid = int(parts[0])
            pcpu = float(parts[1])
        except ValueError:
            continue
        processes.append(
            {
                "pid": pid,
                "pcpu": pcpu,
                "comm": parts[2],
                "args": parts[3] if len(parts) > 3 else "",
            }
        )
    return processes


def _is_descendant(pid, roots):
    seen = 0
    current = pid
    while current > 1 and seen < 32:
        if current in roots:
            return True
        try:
            text = Path("/proc/%d/stat" % current).read_text(encoding="utf-8")
            rest = text[text.rfind(")") + 2:].split()
            current = int(rest[1])
        except (OSError, IndexError, ValueError):
            return False
        seen += 1
    return False


def _foreign_processes(processes, roots):
    if processes is None:
        return None
    foreign = []
    for process in processes:
        if process["pid"] in roots or _is_descendant(process["pid"], roots):
            continue
        if process["pcpu"] < 1.0:
            continue
        text = (process["comm"] or "") + " " + (process["args"] or "")
        if _COMPILER_SERVER.search(text):
            # Idle/shared build servers and MSBuild reuse nodes: never "active
            # compiler work" (see _COMPILER_SERVER above).
            continue
        if (
            _FORBIDDEN_DOTNET.search(text)
            or _TESTHOST.search(text)
            or _COVERAGE.search(text)
            or _CSC.search(text)
            or _BDN.search(text)
        ):
            foreign.append(process)
    return foreign


def _sample_host(child_pid, previous, roots):
    total, idle = _read_cpu_times() or (None, None)
    previous = previous or {}
    busy = None
    if (
        total is not None
        and previous.get("total") is not None
        and total > previous["total"]
    ):
        delta_total = total - previous["total"]
        delta_idle = idle - previous["idle"]
        busy = 100.0 * (delta_total - delta_idle) / delta_total
    child_cpu = None
    proc_seconds = _read_proc_cpu_seconds(child_pid) if child_pid else None
    if (
        proc_seconds is not None
        and previous.get("proc_seconds") is not None
        and previous.get("proc_pid") == child_pid
    ):
        delta_t = monotonic() - previous.get("proc_t", monotonic())
        if delta_t > 0:
            child_cpu = 100.0 * (proc_seconds - previous["proc_seconds"]) / delta_t
    frequencies = _read_frequencies_mhz()
    temperatures = _read_temperatures()
    throttle_total = _read_throttle_total()
    processes = _list_processes()
    foreign = _foreign_processes(processes, roots)
    sample = {
        "t": utc_now(),
        "monotonic": round(monotonic(), 3),
        "child_pid": child_pid,
        "loadavg_1m": os.getloadavg()[0],
        "nproc": os.cpu_count(),
        "busy_cpu_percent": None if busy is None else round(busy, 3),
        "child_cpu_percent": None if child_cpu is None else round(child_cpu, 3),
        "freq_available": bool(frequencies),
        "freq_median_mhz": None if not frequencies else round(median(frequencies), 3),
        "max_temp_c": None if not temperatures else round(max(temperatures), 3),
        "thermal_available": bool(temperatures),
        "throttle_total": throttle_total,
        "ps_available": processes is not None,
        "foreign_pids": foreign or [],
        "foreign_cpu_percent": round(sum(item["pcpu"] for item in (foreign or [])), 3),
    }
    state = {
        "total": total,
        "idle": idle,
        "proc_pid": child_pid,
        "proc_seconds": proc_seconds,
        "proc_t": monotonic(),
    }
    return sample, state


class HostMonitor(threading.Thread):
    def __init__(self, jsonl_path, roots, interval=1.0):
        super().__init__(daemon=True)
        self.jsonl_path = Path(jsonl_path)
        self.roots = set(roots)
        self.interval = float(interval)
        self.stop_event = threading.Event()
        self.stratum = None
        self.child_pid = None
        self.samples = []
        self.lock = threading.Lock()

    def set_stratum(self, stratum):
        self.stratum = stratum

    def set_child(self, pid):
        self.child_pid = pid

    def run(self):
        ensure_dir(self.jsonl_path.parent)
        previous = {}
        with open(self.jsonl_path, "w", encoding="utf-8", newline="\n") as handle:
            while not self.stop_event.is_set():
                try:
                    sample, previous = _sample_host(self.child_pid, previous, self.roots)
                except Exception as exc:
                    sample = {
                        "t": utc_now(),
                        "monotonic": round(monotonic(), 3),
                        "loadavg_1m": None,
                        "nproc": os.cpu_count(),
                        "child_cpu_percent": None,
                        "busy_cpu_percent": None,
                        "freq_available": False,
                        "freq_median_mhz": None,
                        "max_temp_c": None,
                        "thermal_available": False,
                        "throttle_total": None,
                        "ps_available": False,
                        "foreign_pids": [],
                        "foreign_cpu_percent": 0.0,
                        "monitor_error": str(exc),
                    }
                sample["stratum"] = self.stratum
                with self.lock:
                    self.samples.append(sample)
                handle.write(json.dumps(sample, ensure_ascii=False) + "\n")
                handle.flush()
                self.stop_event.wait(self.interval)

    def stop(self):
        self.stop_event.set()


def summarize_monitor(samples):
    ordered = sorted(samples, key=lambda item: item.get("monotonic") or 0)
    by_stratum = {}
    for sample in ordered:
        by_stratum.setdefault(sample.get("stratum"), []).append(sample)
    foreign_consecutive = 0
    foreign_sustained = False
    for sample in ordered:
        if (sample.get("foreign_cpu_percent") or 0.0) >= 100.0:
            foreign_consecutive += 1
            if foreign_consecutive >= 5:
                foreign_sustained = True
        else:
            foreign_consecutive = 0
    thermal_throttle = False
    throttle_counters_available = any(
        sample.get("throttle_total") is not None for sample in ordered
    )
    for stratum, values in by_stratum.items():
        if not values:
            continue
        base = None
        for sample in values:
            if sample.get("throttle_total") is not None:
                if base is None:
                    base = sample["throttle_total"]
                elif sample["throttle_total"] > base:
                    thermal_throttle = True
            # Temperature is only a fallback signal; a hot but non-throttling
            # package must not invalidate a run when counters are available.
            if (
                not throttle_counters_available
                and (sample.get("max_temp_c") or 0.0) >= 95.0
            ):
                thermal_throttle = True
    frequencies = {}
    for stratum, values in by_stratum.items():
        observed = [sample["freq_median_mhz"] for sample in values
                    if sample.get("freq_median_mhz") is not None]
        if observed:
            frequencies[stratum] = median(observed)
    frequency_shift = None
    if len(frequencies) >= 2:
        values = list(frequencies.values())
        frequency_shift = max(values) / min(values) - 1.0
    ps_available = all(sample.get("ps_available") for sample in ordered) if ordered else False
    frequency_available = any(sample.get("freq_available") for sample in ordered)
    thermal_available = any(sample.get("thermal_available") for sample in ordered)
    foreign_pids = {}
    for sample in ordered:
        for process in sample.get("foreign_pids") or []:
            foreign_pids[process["pid"]] = process
    reasons = []
    if not ps_available:
        reasons.append("monitor-unavailable:ps")
    if foreign_sustained:
        reasons.append("foreign-cpu-activity")
    if thermal_throttle:
        reasons.append("thermal-throttle")
    if frequency_shift is not None and frequency_shift > 0.15:
        reasons.append("frequency-shift:%.4f" % frequency_shift)
    return {
        "samples": len(ordered),
        "ps_available": ps_available,
        "frequency_available": frequency_available,
        "thermal_available": thermal_available,
        "foreign_sustained": foreign_sustained,
        "foreign_pids": sorted(foreign_pids.values(), key=lambda item: item["pid"]),
        "thermal_throttle": thermal_throttle,
        "frequency_median_mhz": frequencies,
        "frequency_shift": frequency_shift,
        "reasons": reasons,
        "contaminated": bool(foreign_sustained or thermal_throttle),
    }


# ---------------------------------------------------------------------------
# Quiet window and measure CLI
# ---------------------------------------------------------------------------
def quiet_window(seconds, root, phase):
    path = ensure_dir(Path(root) / "perf" / phase) / "quiet-window.jsonl"
    samples = []
    previous = {}
    roots = {os.getpid()}
    started = monotonic()
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        while monotonic() - started < seconds:
            sample, previous = _sample_host(None, previous, roots)
            samples.append(sample)
            handle.write(json.dumps(sample, ensure_ascii=False) + "\n")
            handle.flush()
            remaining = seconds - (monotonic() - started)
            if remaining > 0:
                time.sleep(min(1.0, remaining))
    loads = [sample["loadavg_1m"] for sample in samples if sample.get("loadavg_1m")]
    nproc = os.cpu_count() or 1
    load_max = max(loads) if loads else None
    foreign = {}
    for sample in samples:
        for process in sample.get("foreign_pids") or []:
            foreign[process["pid"]] = process
    if not all(sample.get("ps_available") for sample in samples):
        return {
            "ok": False,
            "reason": "monitor-unavailable:ps",
            "loadavg_max": load_max,
            "foreign_pids": sorted(foreign.values(), key=lambda item: item["pid"]),
            "samples": len(samples),
        }
    if load_max is not None and load_max > nproc / 2.0:
        return {
            "ok": False,
            "reason": "loadavg:%.2f>%.2f" % (load_max, nproc / 2.0),
            "loadavg_max": load_max,
            "foreign_pids": sorted(foreign.values(), key=lambda item: item["pid"]),
            "samples": len(samples),
        }
    if foreign:
        return {
            "ok": False,
            "reason": "foreign-activity:%s"
            % ",".join(str(pid) for pid in sorted(foreign)),
            "loadavg_max": load_max,
            "foreign_pids": sorted(foreign.values(), key=lambda item: item["pid"]),
            "samples": len(samples),
        }
    return {
        "ok": True,
        "reason": None,
        "loadavg_max": load_max,
        "foreign_pids": [],
        "samples": len(samples),
        "path": str(path),
    }


def bdn_argv(stratum, invocation_count, artifacts_dir):
    spec = STRATA[stratum]
    argv = [
        "dotnet",
        "run",
        "--project",
        BENCH_PROJECT,
        "-c",
        "Release",
        "--",
        "--filter",
        "*SqliteBenchmarkStreaming*",
        "--anyCategories",
    ]
    argv.extend(spec["categories"])
    argv.extend(
        [
            "--invocationCount",
            str(invocation_count),
            "--unrollFactor",
            str(UNROLL_FACTOR),
            "--launchCount",
            str(LAUNCH_COUNT),
            "--warmupCount",
            str(WARMUP_COUNT),
            "--iterationCount",
            str(ITERATION_COUNT),
            "--exporters",
            "json",
            "csv",
            "--artifacts",
            str(artifacts_dir),
        ]
    )
    return argv


def _verify_db_seed(seed_path):
    seed = Path(seed_path)
    if not seed.exists():
        raise Reject("db-seed-missing:%s" % seed)
    size = seed.stat().st_size
    if size != DB_SEED_SIZE:
        raise Reject("db-seed-size:%d!=%d" % (size, DB_SEED_SIZE))
    digest = sha256_file(seed)
    if (
        digest != DB_SHA256
        or not digest.startswith(DB_SHA_PREFIX)
        or not digest.endswith(DB_SHA_SUFFIX)
    ):
        raise Reject("db-seed-sha256:%s" % digest)
    return {"path": str(seed), "size": size, "sha256": digest}


def _build_treatment(runtime, counts, invocation_counts, parity, db_sha, job=None):
    job = job or {}
    resolved = job.get("resolved") or {}
    return {
        "job": resolved.get("job_id") or job.get("job") or "Default",
        "toolchain": job.get("toolchain") or "Default",
        "runtime": runtime,
        "db_sha256": db_sha,
        "db_size": DB_SEED_SIZE,
        "parity": parity,
        "invocation_counts": dict(invocation_counts),
        "counts": dict(counts),
        "resolved_job": {
            "job_id": resolved.get("job_id"),
            "settings": dict(resolved.get("settings") or {}),
            "out_of_process": job.get("out_of_process"),
        },
        "method_sets": {stratum: list(STRATA[stratum]["methods"]) for stratum in STRATA},
    }


def execute_complete_run(config, attempt_index, invocation_counts):
    root = Path(config["root"])
    phase = config["phase"]
    run_dir = root / "perf" / phase / ("run-%d" % attempt_index)
    if run_dir.exists():
        raise Reject("attempt-directory-exists:%s" % run_dir)
    ensure_dir(run_dir)
    started = monotonic()
    run_started_at = utc_now()
    seed = _verify_db_seed(config.get("db_seed", DB_SEED_PATH))
    db_dir = ensure_dir(RUNTIME_DIR / phase / ("run-%d" % attempt_index))
    db_path = db_dir / "test.db"
    shutil.copyfile(seed["path"], db_path)
    copy_sha = sha256_file(db_path)
    if copy_sha != seed["sha256"] or db_path.stat().st_size != DB_SEED_SIZE:
        raise Reject("db-copy-verification-failed")
    monitor = HostMonitor(run_dir / "host-monitor.jsonl", {os.getpid()})
    monitor.start()
    strata = []
    timeout = float(config.get("run_timeout_seconds", DEFAULT_RUN_TIMEOUT_SECONDS))
    deadline = monotonic() + timeout
    timed_out = False
    warning_remedy = None
    reasons = []
    try:
        for stratum in STRATUM_ORDER:
            remaining = deadline - monotonic()
            if remaining <= 1.0:
                raise Reject("run-timeout-before-stratum:%s" % stratum)
            out_dir = ensure_dir(run_dir / stratum)
            sha_before = sha256_file(db_path)
            monitor.set_stratum(stratum)
            argv = bdn_argv(stratum, invocation_counts[stratum], out_dir)
            env = dict(os.environ)
            env["NEXTORM_BENCH_FULL"] = "1"
            env["NEXTORM_BENCH_DB"] = str(db_path)
            env_diff = {
                "NEXTORM_BENCH_FULL": "1",
                "NEXTORM_BENCH_DB": str(db_path),
            }
            stdout_path = run_dir / ("%s-stdout.log" % stratum)
            stderr_path = run_dir / ("%s-stderr.log" % stratum)
            started_at = utc_now()
            stratum_started = monotonic()
            timed_out_stratum = False
            with open(stdout_path, "w", encoding="utf-8", newline="\n") as out_handle, open(
                stderr_path, "w", encoding="utf-8", newline="\n"
            ) as err_handle:
                process = subprocess.Popen(
                    argv,
                    cwd=str(config.get("cwd") or repo_root()),
                    env=env,
                    stdout=out_handle,
                    stderr=err_handle,
                )
                monitor.set_child(process.pid)
                try:
                    exit_code = process.wait(timeout=remaining)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
                    exit_code = -9
                    timed_out_stratum = True
                    timed_out = True
                finally:
                    monitor.set_child(None)
            sha_after = sha256_file(db_path)
            record = {
                "stratum": stratum,
                "argv": argv,
                "env_diff": env_diff,
                "started_at": started_at,
                "finished_at": utc_now(),
                "duration_seconds": round(monotonic() - stratum_started, 3),
                "exit_code": exit_code,
                "timed_out": timed_out_stratum,
                "db_path": str(db_path),
                "db_sha_before": sha_before,
                "db_sha_after": sha_after,
                "stdout": str(stdout_path),
                "stderr": str(stderr_path),
            }
            strata.append(record)
            if timed_out_stratum:
                reasons.append("timeout:%s" % stratum)
                break
            if exit_code != 0:
                reasons.append("exit-code:%s:%d" % (stratum, exit_code))
                break
            if sha_before != sha_after or sha_after != DB_SHA256:
                reasons.append("db-sha-changed:%s" % stratum)
                break
            try:
                view = parse_stratum(
                    run_dir / stratum, stratum, invocation_counts[stratum]
                )
            except Reject as exc:
                reasons.append("invalid-report:%s:%s" % (stratum, exc.reason))
                break
            view_reasons = [
                reason
                for reason in validate_stratum_view(
                    view, stratum, invocation_counts[stratum]
                )
                if not reason.startswith("duration-warning:")
            ]
            reasons.extend(view_reasons)
            ok, parity_reason = parity_ok(view.get("parity") or {})
            if not ok:
                reasons.append("parity:%s:%s" % (stratum, parity_reason))
            warning = view.get("duration_warning")
            if warning:
                existing = (config.get("_remedies") or {}).get(stratum)
                record["duration_warning"] = warning
                if existing:
                    reasons.append(
                        "duration-warning-after-authorized-increase:%s" % stratum
                    )
                else:
                    warning_remedy = {"stratum": stratum}
                    reasons.append("duration-warning:%s" % stratum)
                break
            record["view_status"] = "parsed"
    finally:
        monitor.stop()
        monitor.join(timeout=10)
    summary = summarize_monitor(monitor.samples)
    reasons.extend(summary["reasons"])

    runtime = {}
    counts = {}
    parity = {}
    job = {}
    if not reasons:
        first_view = parse_stratum(
            run_dir / STRATUM_ORDER[0], STRATUM_ORDER[0],
            invocation_counts[STRATUM_ORDER[0]],
        )
        host = first_view.get("host") or {}
        runtime = {
            "runtime_version": host.get("RuntimeVersion"),
            "dotnet_cli_version": host.get("DotNetCliVersion"),
            "os_version": host.get("OsVersion"),
        }
        counts = dict(first_view.get("counts") or {})
        job = first_view.get("job") or {}
        parity = {"rows": PARITY_ROWS, "checksum": PARITY_CHECKSUM}

    treatment = _build_treatment(
        runtime, counts, invocation_counts, parity, DB_SHA256, job=job
    ) if runtime else {}
    lock = config.get("lock_treatment")
    if lock and treatment and treatment_hash(treatment) != treatment_hash(lock):
        reasons.append("treatment-mismatch:frozen-lock")

    valid = not reasons
    manifest = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "phase": phase,
        "attempt": attempt_index,
        "valid": valid,
        "reason": None if valid else ";".join(reasons),
        "db_sha256": DB_SHA256,
        "db_size": DB_SEED_SIZE,
        "parity": parity,
        "invocation_counts": dict(invocation_counts),
        "counts": counts,
        "runtime": runtime,
        "treatment": treatment,
        "treatment_hash": treatment_hash(treatment) if treatment else None,
        "strata": {record["stratum"]: record for record in strata},
        "host_monitor": summary,
        "monitor_path": str(run_dir / "host-monitor.jsonl"),
        "started_at": run_started_at,
        "duration_seconds": round(monotonic() - started, 3),
    }
    write_json(run_dir / "manifest.json", manifest)
    return {
        "valid": valid,
        "reason": None if valid else ";".join(reasons),
        "duration_seconds": monotonic() - started,
        "run_dir": str(run_dir),
        "strata": strata,
        "manifest": manifest,
        "remedy": warning_remedy if warning_remedy else None,
    }


def cmd_measure(args):
    root = Path(args.root)
    ensure_dir(root / "perf")
    if os.path.abspath(args.lock) != os.path.abspath(CANONICAL_LOCK):
        raise Reject("non-canonical-lock:%s" % args.lock)
    if int(args.runs) != REQUIRED_RUNS:
        raise Reject("runs-must-be-three:%d" % int(args.runs))
    if not 1 <= int(args.max_attempts) <= MAX_ATTEMPTS_PER_PHASE:
        raise Reject("max-attempts-out-of-budget:%d" % int(args.max_attempts))
    if float(args.run_timeout_seconds) > DEFAULT_RUN_TIMEOUT_SECONDS:
        raise Reject("run-timeout-exceeds-ceiling")
    if float(args.total_measurement_budget_seconds) > DEFAULT_TOTAL_BUDGET_SECONDS:
        raise Reject("measurement-budget-exceeds-two-hour-ceiling")
    if float(args.lock_wait_seconds) > 3600:
        raise Reject("lock-wait-exceeds-ceiling")
    # Environment failure must not consume an attempt slot.
    _verify_db_seed(DB_SEED_PATH)
    lock = None
    if args.phase == "final":
        lock_path = root / "perf" / "protocol-lock.json"
        if not lock_path.exists():
            raise Reject("final-without-frozen-protocol-lock")
        lock = read_json(lock_path)
    elif (root / "perf" / "protocol-lock.json").exists():
        raise Reject("baseline-after-protocol-freeze")
    invocation_counts = None
    if lock:
        invocation_counts = lock.get("invocation_counts") or {}
        for stratum in STRATUM_ORDER:
            value = invocation_counts.get(stratum)
            if value is None:
                raise Reject("locked-invocation-count-missing:%s" % stratum)
            spec = STRATA[stratum]
            if value not in (
                spec["invocation_count"],
                spec["invocation_count_authorized_max"],
            ):
                raise Reject(
                    "locked-invocation-count-invalid:%s:%r" % (stratum, value)
                )
    config = {
        "root": str(root),
        "phase": args.phase,
        "runs": int(args.runs),
        "max_attempts": int(args.max_attempts),
        "lock": str(args.lock),
        "lock_wait_seconds": float(args.lock_wait_seconds),
        "run_timeout_seconds": float(args.run_timeout_seconds),
        "total_budget_seconds": float(args.total_measurement_budget_seconds),
        "quiet_seconds": QUIET_SECONDS,
        "quiet_probe": quiet_window,
        "db_seed": DB_SEED_PATH,
        "cwd": str(repo_root()),
        "caller_argv": list(sys.argv),
        "locked_invocation_counts": invocation_counts,
        "lock_treatment": (lock or {}).get("treatment"),
    }
    result = measure_campaign(config, execute_complete_run)
    return result["exit_code"]


# ---------------------------------------------------------------------------
# Shared run loading
# ---------------------------------------------------------------------------
def load_valid_runs(root, phase, required=REQUIRED_RUNS):
    phase_dir = Path(root) / "perf" / phase
    if not phase_dir.exists():
        raise Reject("missing-phase-directory:%s" % phase_dir)
    run_dirs = sorted(
        [path for path in phase_dir.glob("run-*") if path.is_dir()],
        key=lambda path: int(path.name.split("-")[1]),
    )
    valid = {}
    invalid = []
    for run_dir in run_dirs:
        manifest = read_json(run_dir / "manifest.json")
        if manifest.get("valid") is True:
            valid[int(run_dir.name.split("-")[1])] = run_dir
        else:
            invalid.append(
                {
                    "attempt": int(run_dir.name.split("-")[1]),
                    "reason": manifest.get("reason"),
                }
            )
    if len(valid) != required:
        raise Reject(
            "valid-run-count:%d!=%d (invalid=%s)" % (len(valid), required, invalid)
        )
    mapped = {}
    manifests = {}
    for position, attempt in enumerate(sorted(valid), start=1):
        run_dir = valid[attempt]
        manifest = read_json(run_dir / "manifest.json")
        manifests[position] = manifest
        strata = {}
        for stratum in STRATUM_ORDER:
            invocation_count = (manifest.get("invocation_counts") or {}).get(
                stratum, STRATA[stratum]["invocation_count"]
            )
            strata[stratum] = parse_stratum(
                run_dir / stratum, stratum, invocation_count, require_log=True
            )
        mapped[position] = {
            "manifest": manifest,
            "strata": strata,
            "dir": str(run_dir),
            "attempt": attempt,
        }
    return {"runs": mapped, "manifests": manifests, "invalid": invalid}


def load_repo_budgets():
    path = repo_root() / BUDGETS_PATH
    return read_json(path)


# ---------------------------------------------------------------------------
# protocol
# ---------------------------------------------------------------------------
def run_command(argv, cwd, env, timeout):
    started = monotonic()
    started_at = utc_now()
    base_env = dict(os.environ)
    env_diff = {
        key: value for key, value in (env or {}).items()
        if base_env.get(key) != value
    }
    try:
        process = subprocess.run(
            argv,
            cwd=str(cwd),
            env={**base_env, **(env or {})},
            capture_output=True,
            text=True,
            timeout=timeout,
        )
    except FileNotFoundError as exc:
        raise Reject("command-not-found:%s" % argv[0]) from exc
    except subprocess.TimeoutExpired as exc:
        return {
            "argv": list(argv),
            "cwd": str(cwd),
            "env_diff": env_diff,
            "started_at": started_at,
            "finished_at": utc_now(),
            "duration_seconds": round(monotonic() - started, 3),
            "exit_code": -1,
            "timed_out": True,
            "stdout": (exc.stdout or "") if isinstance(exc.stdout, str) else "",
            "stderr": (exc.stderr or "") if isinstance(exc.stderr, str) else "",
        }
    return {
        "argv": list(argv),
        "cwd": str(cwd),
        "env_diff": env_diff,
        "started_at": started_at,
        "finished_at": utc_now(),
        "duration_seconds": round(monotonic() - started, 3),
        "exit_code": process.returncode,
        "timed_out": False,
        "stdout": process.stdout or "",
        "stderr": process.stderr or "",
    }


def _parse_list_methods(text):
    methods = set()
    for line in text.splitlines():
        match = re.search(r"SqliteBenchmarkStreaming\.([A-Za-z0-9_]+)", line)
        if match:
            methods.add(match.group(1))
    return methods


def cmd_protocol(args):
    root = Path(args.root)
    perf = ensure_dir(root / "perf")
    started = monotonic()
    payload = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "command": "protocol",
        "started_at": utc_now(),
        "status": "REJECT",
        "verified": False,
        "reason": None,
    }
    try:
        if os.path.abspath(args.lock) != os.path.abspath(CANONICAL_LOCK):
            raise Reject("non-canonical-lock:%s" % args.lock)
        payload["lock"] = {
            "path": os.path.abspath(args.lock),
            "canonical": True,
        }
        seed = _verify_db_seed(DB_SEED_PATH)
        preflight_dir = ensure_dir(perf / "preflight")
        preflight_db = preflight_dir / "test.db"
        shutil.copyfile(seed["path"], preflight_db)
        preflight_sha = sha256_file(preflight_db)
        if preflight_sha != seed["sha256"]:
            raise Reject("preflight-db-copy-sha-mismatch")
        payload["db"] = {
            **seed,
            "expected_sha256": DB_SHA256,
            "expected_size": DB_SEED_SIZE,
            "preflight_copy": str(preflight_db),
            "preflight_copy_sha256": preflight_sha,
            "prefix_ok": seed["sha256"].startswith(DB_SHA_PREFIX),
            "suffix_ok": seed["sha256"].endswith(DB_SHA_SUFFIX),
        }

        version = run_command(["dotnet", "--version"], repo_root(), None, 120)
        if version["exit_code"] != 0:
            raise Reject("dotnet-version-exit:%d" % version["exit_code"])
        runtimes = run_command(["dotnet", "--list-runtimes"], repo_root(), None, 120)
        if runtimes["exit_code"] != 0:
            raise Reject("dotnet-runtimes-exit:%d" % runtimes["exit_code"])
        payload["runtime"] = {
            "dotnet_version": version["stdout"].strip(),
            "dotnet_version_command": version,
            "list_runtimes": [
                line.strip() for line in runtimes["stdout"].splitlines() if line.strip()
            ],
        }
        os_release = {}
        try:
            for line in Path("/etc/os-release").read_text(encoding="utf-8").splitlines():
                if "=" in line:
                    key, value = line.split("=", 1)
                    os_release[key] = value.strip().strip('"')
        except OSError:
            os_release = {}
        payload["os"] = {
            "os_release": os_release,
            "kernel": os.uname().release if hasattr(os, "uname") else None,
            "nproc": os.cpu_count(),
        }
        payload["gc"] = {
            key: value
            for key, value in os.environ.items()
            if key.startswith("DOTNET_gc") or key in ("DOTNET_TieredPGO", "DOTNET_TieredCompilation")
        }
        try:
            payload["affinity"] = sorted(os.sched_getaffinity(0))
        except (AttributeError, OSError):
            payload["affinity"] = None
        governor_path = Path(
            "/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor"
        )
        payload["governor"] = (
            governor_path.read_text(encoding="utf-8").strip()
            if governor_path.exists()
            else None
        )
        global_json_path = repo_root() / "global.json"
        if not global_json_path.exists():
            raise Reject("global-json-missing")
        global_text = read_text(global_json_path)
        payload["global_json"] = {
            "path": str(global_json_path),
            "sha256": sha256_text(global_text),
            "content": global_text,
        }
        hashes = {}
        for label, relative in (
            ("harness", HARNESS_PATH),
            ("config", CONFIG_PATH),
            ("driver", DRIVER_PATH),
            ("budgets", BUDGETS_PATH),
        ):
            path = repo_root() / relative
            if not path.exists():
                raise Reject("missing-hash-input:%s" % relative)
            hashes[label] = {"path": relative, "sha256": sha256_file(path)}
        payload["hashes"] = hashes

        list_runs = []
        for stratum in STRATUM_ORDER:
            spec = STRATA[stratum]
            argv = bdn_argv(
                stratum, spec["invocation_count"], preflight_dir / stratum
            ) + ["--list", "flat"]
            env = {
                "NEXTORM_BENCH_FULL": "1",
                "NEXTORM_BENCH_DB": str(preflight_db),
            }
            result = run_command(
                argv, repo_root(), env, DEFAULT_RUN_TIMEOUT_SECONDS
            )
            methods = _parse_list_methods(result["stdout"] + "\n" + result["stderr"])
            expected = set(spec["methods"])
            record = {
                "stratum": stratum,
                "argv": argv,
                "env_diff": result["env_diff"],
                "exit_code": result["exit_code"],
                "timed_out": result["timed_out"],
                "methods": sorted(methods),
                "expected_methods": sorted(expected),
                "expected_count": len(expected),
                "actual_count": len(methods),
                "stdout_path": None,
            }
            out_path = preflight_dir / ("%s-list.log" % stratum)
            write_text(
                out_path,
                "ARGV: %s\n\nSTDOUT:\n%s\nSTDERR:\n%s\n"
                % (json.dumps(argv), result["stdout"], result["stderr"]),
            )
            record["stdout_path"] = str(out_path)
            list_runs.append(record)
        payload["list"] = list_runs
        for record in list_runs:
            if record["exit_code"] != 0 or record["timed_out"]:
                raise Reject("list-exit:%s:%s" % (record["stratum"], record["exit_code"]))
            if set(record["methods"]) != set(record["expected_methods"]):
                raise Reject(
                    "list-method-set:%s:%s!=%s"
                    % (
                        record["stratum"],
                        record["methods"],
                        record["expected_methods"],
                    )
                )
            if record["actual_count"] != record["expected_count"]:
                raise Reject(
                    "list-count:%s:%d!=%d"
                    % (
                        record["stratum"],
                        record["actual_count"],
                        record["expected_count"],
                    )
                )
        payload.update(
            {
                "status": "PASS",
                "verified": True,
                "reason": None,
                "finished_at": utc_now(),
                "duration_seconds": round(monotonic() - started, 3),
            }
        )
    except Reject as exc:
        payload.update(
            {
                "status": "REJECT",
                "verified": False,
                "reason": exc.reason,
                "finished_at": utc_now(),
                "duration_seconds": round(monotonic() - started, 3),
            }
        )
        write_json(perf / "preflight.json", payload)
        raise
    write_json(perf / "preflight.json", payload)
    elapsed = monotonic() - started
    ledger = load_ledger(root, sys.argv)
    ledger["overhead_seconds"] = float(ledger.get("overhead_seconds") or 0.0) + elapsed
    ledger.setdefault("preflight", []).append(
        {"finished_at": utc_now(), "duration_seconds": round(elapsed, 3),
         "status": payload["status"]}
    )
    save_ledger(root, ledger)
    return EXIT_OK


# ---------------------------------------------------------------------------
# qualify
# ---------------------------------------------------------------------------
def _qualification_payload(status, reasons, input_errors, analysis, extra=None):
    payload = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "command": "qualify",
        "phase": "baseline",
        "generated_at": utc_now(),
        "status": status,
        "reasons": reasons,
        "input_errors": input_errors,
        "caps": analysis.get("caps") if analysis else None,
        "measured": analysis.get("measured") if analysis else None,
        "predicates": analysis.get("predicates") if analysis else [],
        "arms": {
            key: {
                "stratum": arm["stratum"],
                "arm": arm["arm"],
                "nextorm_modes": arm["nextorm_modes"],
                "comparator_modes": arm["comparator_modes"],
                "mode_spreads": arm["mode_spreads"],
            }
            for key, arm in (analysis.get("arms") if analysis else {}).items()
        },
    }
    if extra:
        payload.update(extra)
    return payload


def cmd_qualify(args):
    root = Path(args.root)
    perf = ensure_dir(root / "perf")
    try:
        loaded = load_valid_runs(root, "baseline")
        budgets = load_repo_budgets()
        # Qualification fixes precision only: product time/allocation ratios
        # are measured and reported, but the unchanged baseline is not expected
        # to pass <=1.00 before the production change.  The final gate enforces
        # them against the frozen treatment.
        analysis = analyze_campaign(
            loaded["runs"],
            budgets,
            QUALIFICATION_ERROR_CAP,
            QUALIFICATION_SPREAD_CAP,
            enforce_product_gates=False,
        )
        treatments = []
        for position, run in loaded["runs"].items():
            manifest = run["manifest"]
            treatment = manifest.get("treatment")
            if not treatment:
                raise Reject("manifest-missing-treatment:run%d" % position)
            treatments.append(treatment)
        if len({treatment_hash(item) for item in treatments}) != 1:
            raise Reject("treatment-drift-across-baseline-runs")
        treatment = treatments[0]
        launch_table = {}
        for position, run in loaded["runs"].items():
            strata_table = {}
            for stratum in STRATUM_ORDER:
                view = run["strata"][stratum]
                stratum_table = {
                    "job": view.get("job"),
                    "counts": view.get("counts"),
                    "benchmarks": {},
                }
                for method, benchmark in (view.get("benchmarks") or {}).items():
                    stratum_table["benchmarks"][method] = {
                        "bytes_per_op": benchmark.get("bytes_per_op"),
                        "pooled": {
                            key: benchmark.get("pooled", {}).get(key)
                            for key in ("mean_ns", "error_ns", "stddev_ns", "n",
                                        "median_ns", "level_raw", "level_pct")
                        },
                        "launches": [
                            {
                                "launch": launch["launch"],
                                "mean_ns": launch["mean_ns"],
                                "error_ns": launch["error_ns"],
                                "stddev_ns": launch["stddev_ns"],
                                "n": launch["n"],
                                "requested_n": launch["requested_n"],
                                "median_ns": launch["median_ns"],
                                "confidence_level_pct": launch[
                                    "confidence_level_pct"
                                ],
                            }
                            for launch in benchmark.get("launches") or []
                        ],
                    }
                strata_table[stratum] = stratum_table
            launch_table[str(position)] = {
                "attempt": run.get("attempt"),
                "dir": run.get("dir"),
                "strata": strata_table,
            }
        if analysis["input_errors"] or analysis["reasons"]:
            payload = _qualification_payload(
                "FAIL",
                analysis["reasons"],
                analysis["input_errors"],
                analysis,
                {
                    "runs": {
                        str(position): {
                            "attempt": run.get("attempt"),
                            "manifest_sha256": sha256_text(
                                canonical_json(run["manifest"])
                            ),
                        }
                        for position, run in loaded["runs"].items()
                    },
                    "invalid_runs": loaded["invalid"],
                    "launch_table": launch_table,
                },
            )
            write_json(perf / "qualification.json", payload)
            return EXIT_FAIL
        e_q = analysis["measured"]["e_worst"]
        s_q = analysis["measured"]["spread_worst"]
        e_star, s_star = derive_thresholds(e_q, s_q)
        lock_path = perf / "protocol-lock.json"
        lock_core = {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "status": "frozen",
            "confidence_level_pct": CONFIDENCE_LEVEL_PCT,
            "caps": {"E_star": e_star, "S_star": s_star},
            "operands": {"e_q": e_q, "s_q": s_q},
            "invocation_counts": dict(treatment["invocation_counts"]),
            "treatment": treatment,
            "treatment_hash": treatment_hash(treatment),
        }
        if lock_path.exists():
            existing = read_json(lock_path)
            unchanged = (
                existing.get("caps") == lock_core["caps"]
                and existing.get("treatment_hash") == lock_core["treatment_hash"]
                and existing.get("invocation_counts") == lock_core["invocation_counts"]
            )
            if not unchanged:
                payload = _qualification_payload(
                    "FAIL",
                    ["protocol-lock-frozen-mismatch"],
                    [],
                    analysis,
                )
                write_json(perf / "qualification.json", payload)
                return EXIT_REJECT
            lock = existing
        else:
            lock = {
                **lock_core,
                "frozen_at": utc_now(),
                "qualified_runs": {
                    str(position): {
                        "attempt": run.get("attempt"),
                        "dir": run.get("dir"),
                        "manifest_sha256": sha256_text(
                            canonical_json(run["manifest"])
                        ),
                    }
                    for position, run in loaded["runs"].items()
                },
                "argv": {
                    stratum: bdn_argv(
                        stratum,
                        int(treatment["invocation_counts"][stratum]),
                        str(Path(loaded["runs"][1]["dir"]) / stratum),
                    )
                    for stratum in STRATUM_ORDER
                },
            }
            write_json(lock_path, lock)
        payload = _qualification_payload(
            "PASS",
            [],
            [],
            analysis,
            {
                "caps": {"E_star": e_star, "S_star": s_star},
                "frozen": {"E_star": e_star, "S_star": s_star,
                           "e_q": e_q, "s_q": s_q},
                "treatment": treatment,
                "treatment_hash": lock_core["treatment_hash"],
                "runs": {
                    str(position): {
                        "attempt": run.get("attempt"),
                        "dir": run.get("dir"),
                        "manifest_sha256": sha256_text(
                            canonical_json(run["manifest"])
                        ),
                    }
                    for position, run in loaded["runs"].items()
                },
                "invalid_runs": loaded["invalid"],
                "launch_table": launch_table,
            },
        )
        write_json(perf / "qualification.json", payload)
        return EXIT_OK
    except Reject as exc:
        payload = {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "command": "qualify",
            "phase": "baseline",
            "generated_at": utc_now(),
            "status": "REJECT",
            "reasons": [],
            "input_errors": [exc.reason],
        }
        write_json(perf / "qualification.json", payload)
        raise


# ---------------------------------------------------------------------------
# oracle
# ---------------------------------------------------------------------------
HISTORICAL_MARKDOWN = (
    "benchmarks/BenchmarkDotNet.Artifacts/results/"
    "NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md"
)
HISTORICAL_CSV = (
    "benchmarks/BenchmarkDotNet.Artifacts/results/"
    "NextORM.Benchmark.SqliteBenchmarkStreaming-report.csv"
)
HISTORICAL_ACCEPTANCE = "docs/specs/performance/acceptance-benchmarks.md"
HISTORICAL_FEATURE = "docs/comparisons/feature-benchmarks-2026-10-17.md"

HISTORICAL_JOB = "Job.ShortRun + InProcessEmitToolchain"
HISTORICAL_NEXTORM_EMPTY_BYTES = 12740
HISTORICAL_LINQ2DB_EMPTY_BYTES = 5520
DEFAULT_RUN1_NEXTORM_EMPTY_BYTES = 8256
DEFAULT_RUN1_LINQ2DB_EMPTY_BYTES = 1040
DEFAULT_LATER_NEXTORM_EMPTY_BYTES = 8176
DEFAULT_LATER_LINQ2DB_EMPTY_BYTES = 1040


def _file_lines(relative):
    path = repo_root() / relative
    if not path.exists():
        raise Reject("oracle-citation-missing:%s" % relative)
    return read_text(path).splitlines()


def _require_line(relative, lineno, needle):
    lines = _file_lines(relative)
    if lineno < 1 or lineno > len(lines):
        raise Reject("oracle-citation-line-out-of-range:%s:%d" % (relative, lineno))
    if needle not in lines[lineno - 1]:
        raise Reject(
            "oracle-citation-mismatch:%s:%d:expected=%r"
            % (relative, lineno, needle)
        )
    return lines[lineno - 1].strip()


def _markdown_mean_us(relative, lineno, method):
    line = _require_line(relative, lineno, method)
    match = re.search(r"([0-9][0-9,]*\.?[0-9]*)\s*[μµ]s", line)
    if not match:
        raise Reject("oracle-mean-unparseable:%s:%d" % (relative, lineno))
    return float(match.group(1).replace(",", ""))


def cmd_oracle(args):
    root = Path(args.root)
    perf = ensure_dir(root / "perf")
    try:
        citations = {
            "historical_job": {
                "source": "%s:8-9" % HISTORICAL_MARKDOWN,
                "line8": _require_line(HISTORICAL_MARKDOWN, 8, "InProcessEmitToolchain"),
                "line9": _require_line(HISTORICAL_MARKDOWN, 9, "IterationCount=3"),
                "job": HISTORICAL_JOB,
            },
            "historical_bytes": {
                "nextorm_empty": {
                    "value": HISTORICAL_NEXTORM_EMPTY_BYTES,
                    "source": "%s:41" % HISTORICAL_MARKDOWN,
                    "line": _require_line(HISTORICAL_MARKDOWN, 41, "12.74 KB"),
                    "csv": "%s:21" % HISTORICAL_CSV,
                    "csv_line": _require_line(
                        HISTORICAL_CSV, 21, "B_Nextorm_ToDataReader_Empty"
                    ),
                },
                "linq2db_empty": {
                    "value": HISTORICAL_LINQ2DB_EMPTY_BYTES,
                    "source": "%s:39" % HISTORICAL_MARKDOWN,
                    "line": _require_line(HISTORICAL_MARKDOWN, 39, "5.52 KB"),
                    "csv": "%s:19" % HISTORICAL_CSV,
                    "csv_line": _require_line(
                        HISTORICAL_CSV, 19, "Linq2Db_ToDataReader_Empty"
                    ),
                },
            },
            "job_documents": [
                {
                    "source": "%s:20-21" % HISTORICAL_ACCEPTANCE,
                    "lines": [
                        _require_line(HISTORICAL_ACCEPTANCE, 20, "Job.ShortRun"),
                        _require_line(HISTORICAL_ACCEPTANCE, 21, "Job.Default"),
                    ],
                },
                {
                    "source": "%s:49-50" % HISTORICAL_ACCEPTANCE,
                    "lines": [
                        _require_line(HISTORICAL_ACCEPTANCE, 49, "Job.ShortRun"),
                        _require_line(HISTORICAL_ACCEPTANCE, 50, "InProcessEmitToolchain"),
                    ],
                },
                {
                    "source": "%s:17,22-23" % HISTORICAL_FEATURE,
                    "lines": [
                        _require_line(HISTORICAL_FEATURE, 17, "InProcessEmitToolchain"),
                        _require_line(HISTORICAL_FEATURE, 22, ".NET 10.0.12"),
                        _require_line(HISTORICAL_FEATURE, 23, "InProcessEmitToolchain"),
                    ],
                },
            ],
        }
        historical_nextorm_us = _markdown_mean_us(HISTORICAL_MARKDOWN, 41, "B_Nextorm_ToDataReader_Empty")
        historical_linq2db_us = _markdown_mean_us(HISTORICAL_MARKDOWN, 39, "Linq2Db_ToDataReader_Empty")

        loaded = load_valid_runs(root, "baseline")
        default_empty = {}
        for position, run in loaded["runs"].items():
            for stratum in STRATUM_ORDER:
                view = run["strata"][stratum]
                if not view["job"]["out_of_process"] or not view["job"]["is_default"]:
                    raise Reject(
                        "oracle-mixed-job:run%d:%s" % (position, stratum)
                    )
            empty = run["strata"]["empty"]["benchmarks"]
            nextorm_bytes = empty["B_Nextorm_ToDataReader_Empty"]["bytes_per_op"]
            linq2db_bytes = empty["Linq2Db_ToDataReader_Empty"]["bytes_per_op"]
            nextorm_mean_ns = empty["B_Nextorm_ToDataReader_Empty"]["pooled"]["mean_ns"]
            linq2db_mean_ns = empty["Linq2Db_ToDataReader_Empty"]["pooled"]["mean_ns"]
            default_empty[position] = {
                "nextorm_bytes": nextorm_bytes,
                "linq2db_bytes": linq2db_bytes,
                "differential_bytes": nextorm_bytes - linq2db_bytes,
                "nextorm_mean_us": nextorm_mean_ns / 1000.0,
                "linq2db_mean_us": linq2db_mean_ns / 1000.0,
            }
            manifest = run["manifest"]
            if manifest.get("db_sha256") != DB_SHA256:
                raise Reject("oracle-dataset-sha:run%d" % position)
            parity = manifest.get("parity") or {}
            if parity.get("rows") != PARITY_ROWS or parity.get("checksum") != PARITY_CHECKSUM:
                raise Reject("oracle-dataset-parity:run%d" % position)
        run1 = default_empty[1]
        if (
            run1["nextorm_bytes"] != DEFAULT_RUN1_NEXTORM_EMPTY_BYTES
            or run1["linq2db_bytes"] != DEFAULT_RUN1_LINQ2DB_EMPTY_BYTES
            or run1["differential_bytes"] != 7216
        ):
            raise Reject(
                "oracle-default-run1-bytes:%s" % (run1,)
            )
        for position in (2, 3):
            later = default_empty[position]
            if (
                later["nextorm_bytes"] != DEFAULT_LATER_NEXTORM_EMPTY_BYTES
                or later["linq2db_bytes"] != DEFAULT_LATER_LINQ2DB_EMPTY_BYTES
                or later["differential_bytes"] != 7136
            ):
                raise Reject(
                    "oracle-default-later-bytes:run%d:%s" % (position, later)
                )
        additive_nextorm = HISTORICAL_NEXTORM_EMPTY_BYTES - run1["nextorm_bytes"]
        additive_linq2db = HISTORICAL_LINQ2DB_EMPTY_BYTES - run1["linq2db_bytes"]
        historical_differential = (
            HISTORICAL_NEXTORM_EMPTY_BYTES - HISTORICAL_LINQ2DB_EMPTY_BYTES
        )
        if additive_nextorm != 4484 or additive_linq2db != 4480:
            raise Reject(
                "oracle-additive-bytes:%d,%d" % (additive_nextorm, additive_linq2db)
            )
        if historical_differential != 7220:
            raise Reject("oracle-historical-differential:%d" % historical_differential)
        residual_nextorm = historical_nextorm_us - run1["nextorm_mean_us"]
        residual_linq2db = historical_linq2db_us - run1["linq2db_mean_us"]
        if abs(residual_nextorm - residual_linq2db) <= 5.0:
            raise Reject("oracle-common-time-constant-claimed")
        payload = {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "command": "oracle",
            "generated_at": utc_now(),
            "status": "PASS",
            "canonical_job": "Default-out-of-process",
            "historical_job": HISTORICAL_JOB,
            "citations": citations,
            "historical": {
                "nextorm_empty_bytes": HISTORICAL_NEXTORM_EMPTY_BYTES,
                "linq2db_empty_bytes": HISTORICAL_LINQ2DB_EMPTY_BYTES,
                "differential_bytes": historical_differential,
                "nextorm_empty_mean_us": historical_nextorm_us,
                "linq2db_empty_mean_us": historical_linq2db_us,
            },
            "default": {
                "runs": {str(position): value for position, value in default_empty.items()},
                "run1_differential_bytes": 7216,
                "later_differential_bytes": 7136,
            },
            "arithmetic": {
                "historical_differential_bytes": historical_differential,
                "additive_nextorm_bytes": additive_nextorm,
                "additive_linq2db_bytes": additive_linq2db,
                "common_additive_component_bytes": 4480,
                "discrepancy_bytes": additive_nextorm - additive_linq2db,
                "time_residual_nextorm_us": residual_nextorm,
                "time_residual_linq2db_us": residual_linq2db,
                "time_residuals_job_incomparable": True,
            },
            "decision": {
                "canonical_oracle": "out-of-process Job.Default only",
                "mixed_job_gate": False,
                "historical_231x": "historical regime only; not canonical",
                "current_empty_allocation_ratio": "7.86-7.94x pre-existing production deficit",
            },
            "runs": {
                str(position): {
                    "attempt": run.get("attempt"),
                    "dir": run.get("dir"),
                }
                for position, run in loaded["runs"].items()
            },
        }
        write_json(perf / "oracle.json", payload)
        lines = [
            "# T246 canonical-oracle reconciliation (rv2)",
            "",
            "Canonical oracle: **out-of-process `Job.Default`**. Historical figures are",
            "`%s`; they are job/regime-incomparable and are never mixed into the gate." % HISTORICAL_JOB,
            "",
            "## Raw byte arithmetic",
            "",
            "| Quantity | Bytes | Source |",
            "|---|---:|---|",
            "| historical B empty | %d | %s:41; %s:21 |"
            % (HISTORICAL_NEXTORM_EMPTY_BYTES, HISTORICAL_MARKDOWN, HISTORICAL_CSV),
            "| historical linq2db empty | %d | %s:39; %s:19 |"
            % (HISTORICAL_LINQ2DB_EMPTY_BYTES, HISTORICAL_MARKDOWN, HISTORICAL_CSV),
            "| historical differential | %d | 12740 - 5520 |" % historical_differential,
            "| Default run-1 B empty | %d | run-1 raw JSON |" % run1["nextorm_bytes"],
            "| Default run-1 linq2db empty | %d | run-1 raw JSON |" % run1["linq2db_bytes"],
            "| Default run-1 differential | %d | 8256 - 1040 |" % run1["differential_bytes"],
            "| additive nextorm | %d | 12740 - 8256 |" % additive_nextorm,
            "| additive linq2db | %d | 5520 - 1040 |" % additive_linq2db,
            "",
            "The common additive component is ~4.48 KB (4 B discrepancy); the reader",
            "deficit remains: historical 7220 B, Default run-1 7216 B, runs 2-3 7136 B.",
            "The toolchain compresses the ratio; it does not remove the deficit.",
            "",
            "## Time residuals",
            "",
            "| Arm | historical us | Default run-1 us | residual us |",
            "|---|---:|---:|---:|",
            "| B empty | %.1f | %.1f | %.2f |"
            % (historical_nextorm_us, run1["nextorm_mean_us"], residual_nextorm),
            "| linq2db empty | %.1f | %.1f | %.2f |"
            % (historical_linq2db_us, run1["linq2db_mean_us"], residual_linq2db),
            "",
            "The residuals differ (%0.2f us vs %0.2f us), so they are **not** a common"
            % (residual_nextorm, residual_linq2db),
            "~130 us constant; historical and Default timings are job/regime-incomparable.",
            "",
            "## Decision",
            "",
            "- Canonical: out-of-process `Job.Default` only; no mixed-job gate.",
            "- Historical 2.31x describes its historical regime, not the current product.",
            "- Current empty allocation 7.86-7.94x is a pre-existing production problem.",
            "- Dataset identity: sha256 `%s`, rows=%d, checksum=%d."
            % (DB_SHA256, PARITY_ROWS, PARITY_CHECKSUM),
            "",
        ]
        write_text(perf / "oracle-reconciliation.md", "\n".join(lines))
        return EXIT_OK
    except Reject as exc:
        write_json(
            perf / "oracle.json",
            {
                "task": TASK,
                "schema": SCHEMA_VERSION,
                "command": "oracle",
                "generated_at": utc_now(),
                "status": "REJECT",
                "reason": exc.reason,
            },
        )
        raise


# ---------------------------------------------------------------------------
# modes
# ---------------------------------------------------------------------------
def cmd_modes(args):
    root = Path(args.root)
    perf = ensure_dir(root / "perf")
    try:
        loaded = load_valid_runs(root, args.phase)
        budgets = load_repo_budgets()
        lock_path = perf / "protocol-lock.json"
        caps = None
        if lock_path.exists():
            caps = read_json(lock_path).get("caps")
        precision_cap = 0.027
        spread_cap = 0.04
        if caps:
            precision_cap = float(caps.get("E_star", precision_cap))
            spread_cap = float(caps.get("S_star", spread_cap))
        analysis = analyze_campaign(
            loaded["runs"],
            budgets,
            precision_cap,
            spread_cap,
            enforce_product_gates=False,
        )
        mode_errors = [
            item for item in analysis["input_errors"] if "mode" in item
        ]
        other_errors = [
            item for item in analysis["input_errors"] if "mode" not in item
        ]
        status = (
            "PASS"
            if not analysis["input_errors"] and not mode_errors
            else "FAIL"
        )
        payload = {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "command": "modes",
            "phase": args.phase,
            "generated_at": utc_now(),
            "status": status,
            "detector": {
                "rule": "sort launch medians; split only at adjacent gaps >= 15%",
                "gap": MODE_GAP,
                "required_runs": REQUIRED_RUNS,
                "no_averaging": True,
            },
            "runs": {
                str(position): {
                    "attempt": run.get("attempt"),
                    "dir": run.get("dir"),
                    "host_monitor": (run["manifest"].get("host_monitor") or {}),
                }
                for position, run in loaded["runs"].items()
            },
            "arms": {
                key: {
                    "stratum": arm["stratum"],
                    "arm": arm["arm"],
                    "nextorm": {
                        "modes": arm["nextorm_modes"],
                        "observations": arm["nextorm_observations"],
                    },
                    "comparator": {
                        "modes": arm["comparator_modes"],
                        "observations": arm["comparator_observations"],
                    },
                    "mode_spreads": arm["mode_spreads"],
                    "spread_worst": arm["spread_worst"],
                }
                for key, arm in analysis["arms"].items()
            },
            "mode_errors": mode_errors,
            "input_errors": other_errors,
            "precision_reasons": analysis["reasons"],
            "measured": analysis["measured"],
            "invalid_runs": loaded["invalid"],
        }
        if args.phase == "final":
            baseline_path = perf / "baseline" / "modes.json"
            if baseline_path.exists():
                baseline = read_json(baseline_path)
                removed = []
                for key, arm in payload["arms"].items():
                    base_arm = (baseline.get("arms") or {}).get(key)
                    if not base_arm:
                        continue
                    if len(arm["nextorm"]["modes"]) < len(
                        base_arm.get("nextorm", {}).get("modes", [])
                    ):
                        removed.append(key)
                if removed:
                    audit_path = perf / "final" / "mode-elimination-audit.json"
                    if not audit_path.exists():
                        payload["input_errors"].append(
                            "mode-removal-without-audit:%s" % ",".join(sorted(removed))
                        )
                        payload["status"] = "FAIL"
        write_json(perf / args.phase / "modes.json", payload)
        if args.phase == "baseline":
            host_contaminated = any(
                (run["manifest"].get("host_monitor") or {}).get("contaminated")
                or (run["manifest"].get("host_monitor") or {}).get("reasons")
                for run in loaded["runs"].values()
            )
            lines = [
                "# T246 mode decision (baseline, rv2)",
                "",
                "Fixed detector (frozen before qualification): sort launch medians; split",
                "only at adjacent gaps >= 15%; otherwise one mode. No post-result tuning,",
                "no averaging, no deletion of a slow launch.",
                "",
                "| Arm | nextorm modes | comparator modes | worst spread | classification |",
                "|---|---:|---:|---:|---|",
            ]
            for key, arm in payload["arms"].items():
                if host_contaminated:
                    classification = "host contamination -> GAP"
                elif len(arm["nextorm"]["modes"]) > 1 or len(arm["comparator"]["modes"]) > 1:
                    classification = "Default-runtime product state retained; worst pairing governs"
                else:
                    classification = "unimodal"
                lines.append(
                    "| %s | %d | %d | %.4f | %s |"
                    % (
                        key,
                        len(arm["nextorm"]["modes"]),
                        len(arm["comparator"]["modes"]),
                        arm["spread_worst"],
                        classification,
                    )
                )
            lines.extend(
                [
                    "",
                    "Candidate discriminators for any multi-mode arm: tiered JIT/OSR/PGO,",
                    "GC mode/collections, process start, DB page cache/preparation and host",
                    "frequency/thermal. Host telemetry availability is recorded per run;",
                    "absence of a sensor is never used to infer stability.",
                    "",
                ]
            )
            write_text(perf / "baseline" / "mode-decision.md", "\n".join(lines))
        if payload["status"] != "PASS":
            return EXIT_REJECT if payload["input_errors"] else EXIT_FAIL
        return EXIT_OK
    except Reject as exc:
        write_json(
            perf / args.phase / "modes.json",
            {
                "task": TASK,
                "schema": SCHEMA_VERSION,
                "command": "modes",
                "phase": args.phase,
                "generated_at": utc_now(),
                "status": "REJECT",
                "reason": exc.reason,
            },
        )
        raise


# ---------------------------------------------------------------------------
# gate / self-test / replay
# ---------------------------------------------------------------------------
def cmd_gate(args):
    output = Path(args.output)
    try:
        inputs = load_gate_inputs(args.runs, args.budgets)
        inputs["runs_dir"] = str(Path(args.runs))
        inputs["budgets_path"] = str(Path(args.budgets))
        inputs["lock_path"] = str(Path(args.runs).parent / "protocol-lock.json")
        inputs["qualification_path"] = str(
            Path(args.runs).parent / "qualification.json"
        )
        payload = evaluate_gate(inputs)
    except Reject as exc:
        payload = _gate_fail_payload(
            "gate", [], ["gate-input-rejected:%s" % exc.reason]
        )
        write_json(output, payload)
        return EXIT_REJECT
    write_json(output, payload)
    if payload.get("status") != "PASS":
        return EXIT_REJECT if payload.get("input_errors") else EXIT_FAIL
    return EXIT_OK


def cmd_self_test(args):
    payload = self_test_cases()
    write_json(args.output, payload)
    return EXIT_OK if payload["status"] == "PASS" else EXIT_FAIL


def cmd_replay(args):
    root = Path(args.root)
    recon = ensure_dir(root / "recon")
    recipe = read_json(args.recipe)
    commands = recipe.get("commands") or {}
    unpinned = recipe.get("_unpinned") or []
    unpinned_ids = set()
    for item in unpinned:
        if isinstance(item, str):
            unpinned_ids.add(item)
        elif isinstance(item, dict) and item.get("id"):
            unpinned_ids.add(item["id"])
    if args.id in unpinned_ids:
        raise Reject("replay-unpinned-id:%s" % args.id)
    if args.id not in commands:
        raise Reject("replay-unlisted-id:%s" % args.id)
    entry = commands[args.id]
    argv = entry.get("argv")
    if (
        not isinstance(argv, list)
        or not argv
        or not all(isinstance(token, str) and token for token in argv)
    ):
        raise Reject("replay-argv-invalid:%s" % args.id)
    shell_names = {"sh", "bash", "zsh", "pwsh", "powershell", "cmd", "cmd.exe"}
    if Path(argv[0]).name.lower() in shell_names:
        raise Reject("replay-shell-wrapper-rejected:%s" % args.id)
    for token in argv:
        if token in ("&&", "||", ";", "|", ">", "<") or "$(" in token:
            raise Reject("replay-compound-command-rejected:%s" % args.id)
    cwd = Path(entry.get("cwd") or ".")
    if not cwd.is_absolute():
        cwd = repo_root() / cwd
    if not cwd.exists():
        raise Reject("replay-cwd-missing:%s" % cwd)
    env = entry.get("env") or {}
    if not isinstance(env, dict) or not all(
        isinstance(key, str) and isinstance(value, (str, int, float))
        for key, value in env.items()
    ):
        raise Reject("replay-env-invalid")
    result = run_command(
        argv,
        cwd,
        {key: str(value) for key, value in env.items()},
        int(entry.get("timeout_seconds") or 3600),
    )
    log_path = recon / ("%s.log" % args.id)
    write_text(
        log_path,
        "ID: %s\nARGV: %s\nCWD: %s\nENV DIFF: %s\nSTART: %s\nEND: %s\nEXIT: %s\n"
        "TIMED OUT: %s\n\nSTDOUT:\n%s\n\nSTDERR:\n%s\n"
        % (
            args.id,
            json.dumps(argv),
            cwd,
            json.dumps(result["env_diff"], sort_keys=True),
            result["started_at"],
            result["finished_at"],
            result["exit_code"],
            result["timed_out"],
            result["stdout"],
            result["stderr"],
        ),
    )
    write_json(
        recon / ("%s.json" % args.id),
        {
            "task": TASK,
            "schema": SCHEMA_VERSION,
            "command": "replay",
            "id": args.id,
            "argv": argv,
            "cwd": str(cwd),
            "env_diff": result["env_diff"],
            "started_at": result["started_at"],
            "finished_at": result["finished_at"],
            "duration_seconds": result["duration_seconds"],
            "exit_code": result["exit_code"],
            "timed_out": result["timed_out"],
            "log": str(log_path),
            "source": entry.get("source"),
        },
    )
    return EXIT_OK if result["exit_code"] == 0 else EXIT_FAIL


# ---------------------------------------------------------------------------
# parse-check (read-only reconciliation of captured runs)
# ---------------------------------------------------------------------------
PARSE_CHECK_RELATIVE_TOLERANCE = 1e-9


def _resolve_parse_runs(runs_path):
    """Accept a parent with ``run-*`` directories or a single run directory."""
    root = Path(runs_path)
    if not root.is_dir():
        raise Reject("missing-runs-directory:%s" % root)
    if (root / "10k").is_dir():
        return [root]
    indexed = []
    for path in root.glob("run-*"):
        if not path.is_dir():
            continue
        try:
            index = int(path.name.split("-")[1])
        except (IndexError, ValueError) as exc:
            raise Reject("invalid-run-directory:%s" % path) from exc
        indexed.append((index, path))
    return [path for _, path in sorted(indexed)]


def parse_check_run(run_dir):
    """Check one captured run's ``10k`` log/JSON/csv triple; never executes BDN.

    The check proves that the log's iteration totals are divided exactly once
    by the logged operation count: the per-op mean recovered from the log must
    match the JSON per-op mean within ``1e-9``, while the unnormalised total is
    ~``Operations`` times the JSON mean.
    """
    run_dir = Path(run_dir)
    entry = {
        "run": run_dir.name,
        "log": None,
        "json": None,
        "csv": None,
        "operations": None,
        "log_per_op_ns": None,
        "json_per_op_ns": None,
        "relative_diff": None,
        "unnormalised_total_ns": None,
        "unnormalised_ratio": None,
        "verdict": "fail",
        "reason": None,
    }
    try:
        _check_captured_run(run_dir, entry)
    except Reject as exc:
        entry["reason"] = exc.reason
        return entry
    entry["verdict"] = "pass"
    return entry


def _check_captured_run(run_dir, entry):
    stratum_dir = run_dir / "10k"
    if not stratum_dir.is_dir():
        raise Reject("missing-stratum-directory:10k")
    files = discover_report_files(stratum_dir)
    entry["log"] = files["log"]
    entry["json"] = files["json"]
    entry["csv"] = files["csv"]
    missing = [key for key in ("log", "json", "csv") if not files[key]]
    if missing:
        raise Reject("missing-measurement-data:%s" % ",".join(missing))
    document = read_json(files["json"])
    benchmarks = document.get("Benchmarks")
    if not isinstance(benchmarks, list) or not benchmarks:
        raise Reject("empty-bdn-json")
    records_by_method = parse_log_workload_records(read_text(files["log"]))
    if not records_by_method:
        raise Reject("missing-log-workload-results")

    operations_seen = set()
    json_per_op = {}
    for benchmark in benchmarks:
        if not isinstance(benchmark, dict):
            raise Reject("malformed-benchmark-row")
        method = benchmark.get("Method") or "<unknown>"
        statistics = benchmark.get("Statistics") or {}
        json_mean = statistics.get("Mean")
        if (
            not isinstance(json_mean, (int, float))
            or isinstance(json_mean, bool)
            or json_mean <= 0
        ):
            raise Reject("missing-json-mean:%s" % method)
        measurements = [
            measurement
            for measurement in (benchmark.get("Measurements") or [])
            if isinstance(measurement, dict)
            and measurement.get("IterationMode") == "Workload"
            and measurement.get("IterationStage") == "Result"
        ]
        if not measurements:
            raise Reject("missing-workload-measurements:%s" % method)
        for measurement in measurements:
            operations = measurement.get("Operations")
            if (
                not isinstance(operations, int)
                or isinstance(operations, bool)
                or operations <= 0
            ):
                raise Reject("malformed-operations:%s" % method)
            if measurement.get("Nanoseconds") is None:
                raise Reject("missing-nanoseconds:%s" % method)
            operations_seen.add(operations)
        json_per_op[method] = float(json_mean)
        if method not in records_by_method:
            raise Reject("missing-log-method:%s" % method)
    extra_methods = sorted(set(records_by_method) - set(json_per_op))
    if extra_methods:
        raise Reject("log-method-not-in-json:%s" % ",".join(extra_methods))

    log_per_op = {}
    raw_totals = {}
    for method, records in records_by_method.items():
        if not records:
            raise Reject("missing-log-workload-results:%s" % method)
        for record in records:
            operations_seen.add(record["operations"])
        log_per_op[method] = mean(
            [record["nanoseconds"] / record["operations"] for record in records]
        )
        raw_totals[method] = mean(
            [record["nanoseconds"] for record in records]
        )
    if len(operations_seen) != 1:
        raise Reject("ambiguous-operations:%s" % sorted(operations_seen))
    operations = operations_seen.pop()
    entry["operations"] = operations
    required = STRATA["10k"]["invocation_count"]
    if operations != required:
        raise Reject("operations-not-%d:%d" % (required, operations))

    methods = sorted(json_per_op)
    entry["log_per_op_ns"] = mean([log_per_op[method] for method in methods])
    entry["json_per_op_ns"] = mean([json_per_op[method] for method in methods])
    entry["relative_diff"] = relative_diff(
        entry["log_per_op_ns"], entry["json_per_op_ns"]
    )
    entry["unnormalised_total_ns"] = mean(
        [raw_totals[method] for method in methods]
    )
    entry["unnormalised_ratio"] = (
        entry["unnormalised_total_ns"] / entry["json_per_op_ns"]
    )
    if entry["relative_diff"] > PARSE_CHECK_RELATIVE_TOLERANCE:
        raise Reject("log-json-per-op-mismatch:%.12g" % entry["relative_diff"])
    if not (
        operations / 2.0
        <= entry["unnormalised_ratio"]
        <= operations * 2.0
    ):
        raise Reject(
            "unnormalised-ratio-not-%dx:%.6f"
            % (operations, entry["unnormalised_ratio"])
        )


def cmd_parse_check(args):
    run_dirs = _resolve_parse_runs(args.runs)
    if not run_dirs:
        raise Reject("no-run-directories:%s" % args.runs)
    entries = [parse_check_run(run_dir) for run_dir in run_dirs]
    passed = sum(1 for entry in entries if entry["verdict"] == "pass")
    payload = {
        "task": TASK,
        "schema": SCHEMA_VERSION,
        "command": "parse-check",
        "runs_root": str(Path(args.runs)),
        "runs": entries,
        "summary": {
            "runs_checked": len(entries),
            "passed": passed,
            "failed": len(entries) - passed,
        },
    }
    print(json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False))
    return EXIT_OK if passed == len(entries) else EXIT_FAIL


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
def build_parser():
    parser = argparse.ArgumentParser(
        prog="t246_evidence.py",
        description="T246 rv2 measurement-protocol driver and fail-closed gate",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    measure = sub.add_parser("measure", help="run one locked baseline/final block")
    measure.add_argument("--phase", required=True, choices=("baseline", "final"))
    measure.add_argument("--runs", type=int, default=REQUIRED_RUNS)
    measure.add_argument("--max-attempts", type=int, default=MAX_ATTEMPTS_PER_PHASE)
    measure.add_argument("--root", required=True)
    measure.add_argument("--lock", default=CANONICAL_LOCK)
    measure.add_argument("--lock-wait-seconds", type=float, default=3600)
    measure.add_argument("--run-timeout-seconds", type=float,
                         default=DEFAULT_RUN_TIMEOUT_SECONDS)
    measure.add_argument("--total-measurement-budget-seconds", type=float,
                         default=DEFAULT_TOTAL_BUDGET_SECONDS)
    measure.set_defaults(handler=cmd_measure)

    protocol = sub.add_parser("protocol", help="preflight lock/DB/list/runtime checks")
    protocol.add_argument("--root", required=True)
    protocol.add_argument("--lock", default=CANONICAL_LOCK)
    protocol.set_defaults(handler=cmd_protocol)

    qualify = sub.add_parser("qualify", help="freeze E*/S* from three baseline runs")
    qualify.add_argument("--root", required=True)
    qualify.set_defaults(handler=cmd_qualify)

    oracle = sub.add_parser("oracle", help="canonical-oracle arithmetic and citations")
    oracle.add_argument("--root", required=True)
    oracle.set_defaults(handler=cmd_oracle)

    modes = sub.add_parser("modes", help="fixed 15%% mode detector and replication")
    modes.add_argument("--root", required=True)
    modes.add_argument("--phase", required=True, choices=("baseline", "final"))
    modes.set_defaults(handler=cmd_modes)

    gate = sub.add_parser("gate", help="fail-closed final gate")
    gate.add_argument("--budgets", required=True)
    gate.add_argument("--runs", required=True)
    gate.add_argument("--output", required=True)
    gate.set_defaults(handler=cmd_gate)

    self_test = sub.add_parser("self-test", help="negative fixtures for the gate")
    self_test.add_argument("--output", required=True)
    self_test.set_defaults(handler=cmd_self_test)

    parse_check = sub.add_parser(
        "parse-check",
        help="read-only log/JSON per-op reconciliation of captured runs",
    )
    parse_check.add_argument("--runs", required=True)
    parse_check.set_defaults(handler=cmd_parse_check)

    replay = sub.add_parser("replay", help="run exactly one pinned recipe command")
    replay.add_argument("--recipe", required=True)
    replay.add_argument("--id", required=True)
    replay.add_argument("--root", required=True)
    replay.set_defaults(handler=cmd_replay)
    return parser


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return args.handler(args)
    except Reject as exc:
        print(
            json.dumps(
                {"status": "REJECT", "reason": exc.reason, "details": exc.details},
                ensure_ascii=False,
            ),
            file=sys.stderr,
        )
        return EXIT_REJECT


if __name__ == "__main__":
    sys.exit(main())
