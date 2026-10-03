#!/usr/bin/env python3
"""Iteration-14 allocation regression gate (slice D5, plan revision r3).

What it does
------------
Runs the three SQLite benchmark classes that cover the Iteration-14 allocation
budgets (``SqliteBenchmarkWarmDecompose``, ``SqliteBenchmarkCachedPlan``,
``SqliteBenchmarkFeaturePlanBuild``) in Release with the *same* BDN job mode used
for the D1/D5 measurements: ``--job short``.  ``NextormConfig`` always adds a
``Job.ShortRun`` + ``InProcessEmitToolchain`` job; ``--job short`` adds a second
``ShortRun`` job on the default (out-of-process) toolchain.  Every benchmark is
therefore measured twice, and the gate compares the *worse* (larger) value of the
two jobs with its pinned budget.

Allocation data
---------------
The gate consumes the BDN JSON exporter (``--exporters json`` ->
``<Namespace>.<Type>-report-full-compressed.json``) because it carries the exact
``Memory.BytesAllocatedPerOperation`` (bytes per invocation, unrounded).  The CSV
exporter only carries a human-formatted ``Allocated`` string (e.g. ``80.82 KB``)
and is deliberately *not* parsed.  The job/toolchain is recovered from the
``DisplayInfo`` string, which renders ``Toolchain=InProcessEmitToolchain`` for the
config job and omits the toolchain for the default one.

Normalization
-------------
Each benchmark body runs 100 *logical* operations per invocation while BDN's
``OperationsPerInvoke`` is 1, so::

    B/op = Memory.BytesAllocatedPerOperation / logical_ops_per_invocation

The divisor is pinned in the manifest (``logical_ops_per_invocation``, 100).

The BDN JSON exporter does *not* emit ``OperationsPerInvoke``: the real report
has only ``Title``, ``HostEnvironmentInfo`` and ``Benchmarks``, and the only
operation-count metadata is ``Memory.TotalOperations`` and
``Measurements[].Operations`` -- neither is the per-invocation scaling factor.
Normalization is therefore additionally pinned by an independent, checked-in
source *contract*: every budgeted benchmark method must be a ``[Benchmark]``
method with no ``OperationsPerInvoke`` argument whose body loops over a class
constant ``Iterations`` equal to ``logical_ops_per_invocation``.  If a row cannot
be verified this way the gate fails closed; it never silently assumes 100.  When
a report *does* carry an explicit ``OperationsPerInvoke`` it must equal the
manifest's ``bdn_operations_per_invoke`` (1).

Missing allocation data is never treated as zero; a non-integer, negative or
absent value, a duplicate result, a missing row/job, a non-zero exit code, a
timeout, a failed source contract, or a report whose BDN ``OperationsPerInvoke``
is not 1 all fail closed.

Usage
-----
    python3 eng/perf/iteration14_gate.py                 # full run + check
    python3 eng/perf/iteration14_gate.py --no-run        # parse existing reports
    python3 eng/perf/iteration14_gate.py --reports-dir D # custom artifacts dir

Exit code 0 only when every row/job is within budget and every report is complete.
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
GATE_DIR = Path(__file__).resolve().parent
MANIFEST_PATH = GATE_DIR / "iteration14-budgets.json"
DEFAULT_REPORTS_DIR = REPO_ROOT / "BenchmarkDotNet.Artifacts" / "results"
BENCH_SRC_DIR = REPO_ROOT / "benchmarks" / "nextorm.benchmark"
SEED_DB = REPO_ROOT / "benchmarks" / "nextorm.benchmark" / "data" / "test.db"

BENCH_PROJECT = "benchmarks/nextorm.benchmark"
FILTERS = [
    "*SqliteBenchmarkWarmDecompose*",
    "*SqliteBenchmarkCachedPlan*",
    "*SqliteBenchmarkFeaturePlanBuild*",
]

_TOOLCHAIN_RE = re.compile(r"Toolchain=([A-Za-z0-9_.]+)")
_CLASS_RE = re.compile(r"\bclass\s+([A-Za-z_]\w*)")
_ITERATIONS_CONST_RE = re.compile(r"\bconst\s+int\s+Iterations\s*=\s*(\d+)\s*;")
_BENCHMARK_ATTR_RE = re.compile(r"\[Benchmark\b(?P<body>[^\]]*)\]")
_BENCHMARK_METHOD_RE = re.compile(
    r"\s*(?:\[[^\]]*\]\s*)*public\s+[^\n;{=]*?\b(?P<name>[A-Za-z_]\w*)\s*\("
)
_OPERATIONS_PER_INVOKE_RE = re.compile(r"OperationsPerInvoke\s*=\s*([^,)\]]+)")
_ITERATIONS_LOOP_RE = re.compile(r"<\s*Iterations\b")


class GateError(Exception):
    """Fatal gate error: malformed manifest/report or a failed benchmark run."""


def _as_int(value):
    """Return *value* as int, or None if it is not an integer/non-negative number."""
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, float) and value.is_integer():
        return int(value)
    return None


def parse_manifest(path):
    try:
        with open(path, "r", encoding="utf-8") as handle:
            manifest = json.load(handle)
    except (OSError, ValueError) as exc:
        raise GateError(f"cannot read manifest {path}: {exc}") from exc

    ops = _as_int(manifest.get("logical_ops_per_invocation"))
    if ops is None or ops <= 0:
        raise GateError("manifest logical_ops_per_invocation must be a positive integer")
    if not isinstance(manifest.get("rows"), list) or not manifest["rows"]:
        raise GateError("manifest has no rows")

    seen = set()
    for row in manifest["rows"]:
        for field in ("id", "type", "method", "max_bytes_per_op"):
            if field not in row:
                raise GateError(f"manifest row missing {field!r}: {row!r}")
        budget = _as_int(row["max_bytes_per_op"])
        if budget is None or budget < 0:
            raise GateError(f"manifest budget must be a non-negative integer: {row!r}")
        key = (row["type"], row["method"])
        if key in seen:
            raise GateError(f"duplicate manifest row for {key[0]}.{key[1]}")
        seen.add(key)

    manifest.setdefault("required_toolchains", ["Default", "InProcessEmitToolchain"])
    return manifest


def _toolchain_from_display(display):
    match = _TOOLCHAIN_RE.search(display or "")
    if match:
        return match.group(1)
    return "Default"


def parse_report(path):
    """Parse one BDN JSON report into a list of benchmark records."""
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError) as exc:
        raise GateError(f"cannot read report {path}: {exc}") from exc

    benchmarks = data.get("Benchmarks")
    if not isinstance(benchmarks, list):
        raise GateError(f"report {path} has no Benchmarks array")

    records = []
    for entry in benchmarks:
        namespace = entry.get("Namespace") or ""
        short_type = entry.get("Type")
        method = entry.get("Method")
        if not short_type or not method:
            raise GateError(f"report {path} has a benchmark without Type/Method")
        full_type = f"{namespace}.{short_type}" if namespace else short_type
        memory = entry.get("Memory")
        allocated = None
        if isinstance(memory, dict):
            allocated = memory.get("BytesAllocatedPerOperation")
        records.append(
            {
                "type": full_type,
                "method": method,
                "toolchain": _toolchain_from_display(entry.get("DisplayInfo")),
                "allocated_bytes_per_invocation": allocated,
                "operations_per_invoke": entry.get("OperationsPerInvoke"),
                "display": entry.get("DisplayInfo") or "",
                "source": str(path),
            }
        )
    return records


def load_results(reports_dir, manifest):
    """Load every report under *reports_dir* whose type is pinned in the manifest."""
    wanted = {row["type"] for row in manifest["rows"]}
    patterns = (
        "*-report-full-compressed.json",
        "*-report-full.json",
    )
    paths = []
    for pattern in patterns:
        paths.extend(sorted(glob.glob(os.path.join(str(reports_dir), pattern))))
    if not paths:
        raise GateError(f"no BDN JSON reports found under {reports_dir}")

    records = []
    for path in paths:
        for record in parse_report(path):
            if record["type"] in wanted:
                records.append(record)
    return records


def clean_target_reports(reports_dir, manifest):
    """Delete stale target-type JSON reports so a failed run cannot pass on old data."""
    for row in manifest["rows"]:
        for suffix in ("report-full-compressed.json", "report-full.json"):
            path = Path(reports_dir) / f"{row['type']}-{suffix}"
            if path.exists():
                path.unlink()


def _extract_method_body(text, start):
    """Return the brace-delimited body starting at/after *start*, or None."""
    brace = text.find("{", start)
    if brace < 0:
        return None
    depth = 0
    for index in range(brace, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[brace : index + 1]
    return None


def parse_benchmark_sources(src_dir, wanted_types):
    """Parse the normalization contract from the benchmark sources.

    Returns ``{short_type: {"iterations": int|None, "methods": {name: {...}},
    "source": path}}`` for the classes named in *wanted_types*.  Only ``*.cs``
    files directly in *src_dir* are read (never build artifacts), and for each
    ``[Benchmark]`` method we record whether it declares ``OperationsPerInvoke``
    and whether its body loops over the class constant ``Iterations``.
    """
    result = {}
    src_path = Path(src_dir)
    if not src_path.is_dir():
        return result
    for path in sorted(src_path.glob("*.cs")):
        try:
            text = path.read_text(encoding="utf-8")
        except OSError:
            continue
        class_match = _CLASS_RE.search(text)
        if class_match is None or class_match.group(1) not in wanted_types:
            continue
        short_type = class_match.group(1)
        iterations_match = _ITERATIONS_CONST_RE.search(text)
        iterations = int(iterations_match.group(1)) if iterations_match else None
        methods = {}
        for attr in _BENCHMARK_ATTR_RE.finditer(text):
            method_match = _BENCHMARK_METHOD_RE.match(text, attr.end())
            if method_match is None:
                continue
            opi_match = _OPERATIONS_PER_INVOKE_RE.search(attr.group("body"))
            body = _extract_method_body(text, method_match.end())
            methods[method_match.group("name")] = {
                "operations_per_invoke": (
                    opi_match.group(1).strip() if opi_match else None
                ),
                "loops_iterations": body is not None
                and _ITERATIONS_LOOP_RE.search(body) is not None,
                "source": str(path),
            }
        result[short_type] = {
            "iterations": iterations,
            "methods": methods,
            "source": str(path),
        }
    return result


def verify_normalization_contract(manifest, src_dir):
    """Fail closed unless each budgeted row's 100-logical-ops contract holds.

    The BDN JSON report does not carry ``OperationsPerInvoke``, so the divisor
    (``logical_ops_per_invocation``) is only trustworthy if the checked-in source
    shows the body performs that many logical operations per BDN invocation.
    """
    ops = manifest["logical_ops_per_invocation"]
    bdn_ops = _as_int(manifest.get("bdn_operations_per_invoke", 1))
    if bdn_ops != 1:
        raise GateError(
            f"manifest bdn_operations_per_invoke must be 1 (got {bdn_ops!r}); "
            "dividing by logical_ops_per_invocation would double-divide"
        )

    wanted = {row["type"].rsplit(".", 1)[-1] for row in manifest["rows"]}
    parsed = parse_benchmark_sources(src_dir, wanted)
    problems = []
    for row in manifest["rows"]:
        short_type = row["type"].rsplit(".", 1)[-1]
        row_id = row["id"]
        info = parsed.get(short_type)
        if info is None:
            problems.append(
                f"{row_id} ({short_type}): benchmark class not found under {src_dir}"
            )
            continue
        if info["iterations"] != ops:
            problems.append(
                f"{row_id} ({short_type}): class Iterations={info['iterations']!r}, "
                f"expected logical_ops_per_invocation={ops}"
            )
            continue
        method = info["methods"].get(row["method"])
        if method is None:
            problems.append(
                f"{row_id} ({short_type}.{row['method']}): no [Benchmark] method found"
            )
            continue
        if method["operations_per_invoke"] is not None:
            problems.append(
                f"{row_id} ({short_type}.{row['method']}): declares "
                f"OperationsPerInvoke={method['operations_per_invoke']!r}; the pinned "
                "contract requires BDN OperationsPerInvoke=1 (no declaration)"
            )
            continue
        if not method["loops_iterations"]:
            problems.append(
                f"{row_id} ({short_type}.{row['method']}): body does not loop over "
                "the class constant Iterations"
            )
    if problems:
        raise GateError(
            "normalization contract not verified:\n  - " + "\n  - ".join(problems)
        )


def evaluate(records, manifest):
    """Compare parsed *records* with the manifest. Returns (verdicts, failures)."""
    ops = manifest["logical_ops_per_invocation"]
    expected_opi = _as_int(manifest.get("bdn_operations_per_invoke", 1))
    required = list(manifest.get("required_toolchains", ["Default", "InProcessEmitToolchain"]))
    by_row = {}
    failures = []

    for record in records:
        key = (record["type"], record["method"])
        jobs = by_row.setdefault(key, {})
        toolchain = record["toolchain"]
        if toolchain in jobs:
            failures.append(
                f"duplicate result for {record['type']}.{record['method']}/{toolchain}"
            )
            continue
        jobs[toolchain] = record

    verdicts = []
    for row in manifest["rows"]:
        row_id = row["id"]
        key = (row["type"], row["method"])
        jobs = by_row.get(key)
        if not jobs:
            failures.append(
                f"missing row {row_id} ({row['type']}.{row['method']}): no result"
            )
            continue

        for toolchain in required:
            record = jobs.get(toolchain)
            if record is None:
                failures.append(f"missing job {row_id}/{toolchain}")
                continue

            opi = record.get("operations_per_invoke")
            if opi is not None and _as_int(opi) != expected_opi:
                failures.append(
                    f"normalization mismatch {row_id}/{toolchain}: "
                    f"OperationsPerInvoke={opi!r}, expected {expected_opi}"
                )
                continue

            allocated = record.get("allocated_bytes_per_invocation")
            allocated_int = _as_int(allocated)
            if allocated_int is None or allocated_int < 0:
                failures.append(
                    f"invalid allocation {row_id}/{toolchain}: {allocated!r} "
                    f"({record.get('source')})"
                )
                continue

            bytes_per_op = allocated_int / ops
            budget = row["max_bytes_per_op"]
            ok = bytes_per_op <= budget
            verdicts.append(
                {
                    "id": row_id,
                    "toolchain": toolchain,
                    "bytes_per_op": bytes_per_op,
                    "budget": budget,
                    "kind": row.get("kind", "budget"),
                    "ok": ok,
                }
            )
            if not ok:
                failures.append(
                    f"over budget {row_id}/{toolchain}: "
                    f"{bytes_per_op:.2f} B/op > {budget} B/op"
                )

    return verdicts, failures


def render(verdicts, failures):
    lines = []
    for verdict in verdicts:
        status = "OK" if verdict["ok"] else "FAIL"
        lines.append(
            f"  {status:4s} {verdict['id']:<34s} {verdict['toolchain']:<24s} "
            f"{verdict['bytes_per_op']:>10.2f} B/op <= {verdict['budget']:>6d}"
        )
    if failures:
        lines.append("")
        lines.append("Violations:")
        lines.extend(f"  - {failure}" for failure in failures)
    else:
        lines.append("")
        lines.append(f"All {len(verdicts)} row/job verdicts within budget.")
    return "\n".join(lines)


def prepare_db(db_path):
    path = Path(db_path)
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        if not SEED_DB.exists():
            raise GateError(f"seed database not found: {SEED_DB}")
        shutil.copyfile(SEED_DB, path)
    return str(path)


def run_benchmarks(args, db_path):
    """Run the three benchmark classes; returns (exit_code, wall_seconds)."""
    env = dict(os.environ)
    env["NEXTORM_BENCH_DB"] = db_path
    cmd = [
        "dotnet",
        "run",
        "--project",
        BENCH_PROJECT,
        "-c",
        "Release",
        "--",
        "--filter",
        *FILTERS,
        "--job",
        "short",
        "--exporters",
        "json",
    ]
    print("+ " + " ".join(cmd), flush=True)
    print(f"  NEXTORM_BENCH_DB={db_path}", flush=True)
    start = time.monotonic()
    try:
        completed = subprocess.run(cmd, cwd=str(REPO_ROOT), env=env, timeout=args.timeout)
    except subprocess.TimeoutExpired:
        return None, time.monotonic() - start
    return completed.returncode, time.monotonic() - start


def parse_args(argv):
    parser = argparse.ArgumentParser(description="Iteration-14 allocation regression gate")
    parser.add_argument("--manifest", default=str(MANIFEST_PATH))
    parser.add_argument("--reports-dir", default=str(DEFAULT_REPORTS_DIR))
    parser.add_argument(
        "--bench-src-dir",
        default=str(BENCH_SRC_DIR),
        help="benchmark source dir used to verify the normalization contract",
    )
    parser.add_argument(
        "--db",
        default=os.environ.get("NEXTORM_BENCH_DB", ""),
        help="SQLite database path (default: NEXTORM_BENCH_DB or a fresh temp dir)",
    )
    parser.add_argument("--timeout", type=int, default=1800, help="benchmark run timeout (s)")
    parser.add_argument(
        "--no-run",
        action="store_true",
        help="skip the benchmark run and parse reports already present",
    )
    return parser.parse_args(argv)


def gate_main(argv=None, runner=None):
    args = parse_args(argv)
    if runner is None:
        runner = run_benchmarks

    try:
        manifest = parse_manifest(args.manifest)
    except GateError as exc:
        print(f"FAIL: {exc}")
        return 1

    try:
        verify_normalization_contract(manifest, args.bench_src_dir)
    except GateError as exc:
        print(f"FAIL: {exc}")
        return 1

    if not args.no_run:
        db_path = args.db or os.path.join(
            tempfile.mkdtemp(prefix="iteration14-gate-"), "test.db"
        )
        try:
            db_path = prepare_db(db_path)
        except GateError as exc:
            print(f"FAIL: {exc}")
            return 1
        reports_dir = Path(args.reports_dir)
        reports_dir.mkdir(parents=True, exist_ok=True)
        clean_target_reports(reports_dir, manifest)
        exit_code, wall = runner(args, db_path)
        if exit_code is None:
            print(f"FAIL: benchmark run timed out after {wall:.1f}s")
            return 1
        print(f"benchmark run exit code {exit_code}, wall {wall:.1f}s")
        if exit_code != 0:
            print(f"FAIL: benchmark run exited non-zero ({exit_code})")
            return 1

    try:
        records = load_results(args.reports_dir, manifest)
        verdicts, failures = evaluate(records, manifest)
    except GateError as exc:
        print(f"FAIL: {exc}")
        return 1

    print(render(verdicts, failures))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(gate_main())
