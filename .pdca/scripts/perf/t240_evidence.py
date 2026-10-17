#!/usr/bin/env python3
"""scripts/perf/t240_evidence.py — T240 (#240) construction-allocation ratio reader.

Reader/computator over BenchmarkDotNet JSON exports only.  It introduces **no new
measurement methodology**: the measurement stays BenchmarkDotNet plus the same-round
NextORM-vs-LINQ-to-DB ratio already used by the approved M12 #3 comparison
(``docs/specs/performance/m12-3-prepare-command-results.md``).  Per the T240 design
decisions §10 this is a *reproducibility adapter*, not a new allocation oracle; the
fallback if §10 is held to forbid even this is an ad-hoc check computation.

Discovery (no hardcoded arm map, no guessed symbols): the arms are found in the BDN JSON
export of the class ``SqliteBenchmarkFeaturesFairCached`` by **method name**:

    B_Nextorm_Cached_<S>   (NextORM arm)
    B_Linq2Db_<S>          (LINQ to DB arm)

for each scenario ``<S>`` in the five Category-B acceptance scenarios ``SCENARIOS``.  The
pairing is derived from the method names.

Rejections (exit 2, never a fabricated verdict): duplicate arm for a scenario, a missing
arm for a scenario, a scenario absent from the export, a null/NA allocation or mean, and a
zero (LINQ) denominator.

Ratios: allocation = NextORM ``Memory.BytesAllocatedPerOperation`` / LINQ; time = NextORM
``Statistics.Mean`` / LINQ.  Fixed limits ``--alloc-tol`` (default 1.20) and ``--time-tol``
(default 1.05).  No averaging; no best-round selection.

Across-round rule (frozen; ``m12-3-prepare-command-results.md:106-109,124-126``): *MET only
if EVERY supplied round is MET for time and allocations; FAIL if any round fails
allocations.*  Every supplied round counts -- no round is dropped, and a round could be
excluded only for a recorded operational-invalidation reason established independently of
its ratio (this adapter accepts no such exclusion).

Exit: 0 only when every required ratio passes in every supplied round; 1 for a measured
miss; 2 for invalid/duplicate/missing/NA evidence or a usage error.
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

# The five Category-B acceptance scenarios (docs/specs/status/D240-construction-allocation-1.md
# RC240-01..05).  The *pairing* is derived from method names; only the scenario list is fixed.
SCENARIOS = ("Cte", "RecursiveCte", "Except", "Intersect", "Distinct")
BENCH_TYPE = "SqliteBenchmarkFeaturesFairCached"
NEXTORM_PREFIX = "B_Nextorm_Cached_"
LINQ_PREFIX = "B_Linq2Db_"

DEFAULT_ALLOC_TOL = 1.20
DEFAULT_TIME_TOL = 1.05

EXIT_OK = 0
EXIT_FAIL = 1
EXIT_REJECT = 2


class Reject(Exception):
    """Invalid/duplicate/missing/NA evidence, or a usage error (exit 2)."""


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def is_number(value) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


# ------------------------------------------------------------------------------------------------
# Report discovery + parsing
# ------------------------------------------------------------------------------------------------
def discover_reports(path) -> list:
    """Resolve a ``--round`` argument (an artifacts dir, or an explicit JSON report)."""
    path = Path(path)
    if path.is_file():
        return [path]
    if not path.exists():
        raise Reject(f"round path does not exist: {path}")
    compressed = sorted(path.rglob("*-report-full-compressed.json"))
    plain = sorted(path.rglob("*-report-full.json"))
    reports, seen = [], set()
    for candidate in compressed + plain:
        stem = candidate.name.split("-report-")[0]
        if stem in seen:
            continue  # compressed and plain "full" exports of the same class are one report
        seen.add(stem)
        reports.append(candidate)
    if not reports:
        reports = sorted(path.rglob("*-report.json"))
    if not reports:
        raise Reject(f"no BDN JSON report under: {path}")
    return reports


def load_entries(report_paths) -> list:
    entries = []
    for path in report_paths:
        try:
            data = json.loads(Path(path).read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            raise Reject(f"cannot read BDN report {path}: {exc}") from exc
        benchmarks = data.get("Benchmarks") if isinstance(data, dict) else None
        if not isinstance(benchmarks, list):
            raise Reject(f"BDN report {path} has no Benchmarks array")
        for benchmark in benchmarks:
            namespace = benchmark.get("Namespace") or ""
            short_type = benchmark.get("Type")
            method = benchmark.get("Method")
            if not short_type or not method:
                raise Reject(f"BDN report {path} has an entry without Type/Method")
            statistics = benchmark.get("Statistics") or {}
            memory = benchmark.get("Memory") or {}
            entries.append(
                {
                    "full": f"{namespace}.{short_type}" if namespace else short_type,
                    "short_type": short_type,
                    "method": method,
                    "mean": statistics.get("Mean"),
                    "allocated": memory.get("BytesAllocatedPerOperation"),
                    "source": str(path),
                }
            )
    return entries


# ------------------------------------------------------------------------------------------------
# Pairing (derived from method names) + per-round ratios
# ------------------------------------------------------------------------------------------------
def pair_round(entries, label) -> dict:
    by_method = {}
    for entry in entries:
        if entry["short_type"] != BENCH_TYPE:
            continue
        method = entry["method"]
        if method in by_method:
            raise Reject(
                f"{label}: duplicate arm for method {method!r} "
                f"({by_method[method]['source']} and {entry['source']})"
            )
        by_method[method] = entry

    pairs = {}
    for scenario in SCENARIOS:
        nextorm_name = NEXTORM_PREFIX + scenario
        linq_name = LINQ_PREFIX + scenario
        nextorm = by_method.get(nextorm_name)
        linq = by_method.get(linq_name)
        if nextorm is None and linq is None:
            raise Reject(f"{label}: scenario not found: {scenario!r} ({nextorm_name!r}/{linq_name!r})")
        if nextorm is None:
            raise Reject(f"{label}: missing NextORM arm for {scenario!r}: {nextorm_name!r}")
        if linq is None:
            raise Reject(f"{label}: missing LINQ to DB arm for {scenario!r}: {linq_name!r}")
        pairs[scenario] = {"nextorm": nextorm, "linq2db": linq}
    return pairs


def _require_nonneg(value, what, label) -> float:
    if not is_number(value) or value < 0:
        raise Reject(f"{label}: null/NA or negative {what}: {value!r}")
    return float(value)


def _require_positive(value, what, label) -> float:
    if not is_number(value) or value <= 0:
        raise Reject(f"{label}: zero/missing denominator {what}: {value!r}")
    return float(value)


def round_ratios(pairs, label, alloc_tol, time_tol) -> dict:
    per_scenario = {}
    for scenario in SCENARIOS:
        pair = pairs[scenario]
        nextorm, linq = pair["nextorm"], pair["linq2db"]
        nx_alloc = _require_nonneg(nextorm["allocated"], f"allocation for {scenario}", label)
        lq_alloc = _require_positive(linq["allocated"], f"LINQ allocation for {scenario}", label)
        nx_mean = _require_nonneg(nextorm["mean"], f"mean for {scenario}", label)
        lq_mean = _require_positive(linq["mean"], f"LINQ mean for {scenario}", label)
        if lq_alloc == 0 or lq_mean == 0:
            raise Reject(f"{label}: zero denominator for {scenario}")

        alloc_ratio = nx_alloc / lq_alloc
        time_ratio = nx_mean / lq_mean
        per_scenario[scenario] = {
            "round": label,
            "nextorm_arm": f"{nextorm['full']}.{nextorm['method']}",
            "linq2db_arm": f"{linq['full']}.{linq['method']}",
            "nextorm_allocated": nextorm["allocated"],
            "linq2db_allocated": linq["allocated"],
            "alloc_ratio": alloc_ratio,
            "alloc_verdict": "MET" if alloc_ratio <= alloc_tol else "FAIL",
            "nextorm_mean": nextorm["mean"],
            "linq2db_mean": linq["mean"],
            "time_ratio": time_ratio,
            "time_verdict": "MET" if time_ratio <= time_tol else "FAIL",
            "sources": sorted({nextorm["source"], linq["source"]}),
        }
    return per_scenario


# ------------------------------------------------------------------------------------------------
# Frozen across-round rule
# ------------------------------------------------------------------------------------------------
def combine_rounds(time_decisions, alloc_decisions) -> str:
    """MET only if EVERY supplied round is MET for time and allocations; FAIL if any round fails
    allocations or every time round is decisively FAIL; otherwise OPEN.  No round is dropped."""
    if not time_decisions:
        return "OPEN"
    if all(decision == "MET" for decision in time_decisions) and all(
        decision == "MET" for decision in alloc_decisions
    ):
        return "MET"
    if all(decision == "FAIL" for decision in time_decisions) or any(
        decision == "FAIL" for decision in alloc_decisions
    ):
        return "FAIL"
    return "OPEN"


# ------------------------------------------------------------------------------------------------
# Driver
# ------------------------------------------------------------------------------------------------
def load_manifest(path):
    manifest_path = Path(path)
    if not manifest_path.exists():
        raise Reject(f"manifest does not exist: {path}")
    try:
        data = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise Reject(f"cannot read manifest {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise Reject(f"manifest is not a JSON object: {path}")
    return data


def evaluate(round_args, alloc_tol, time_tol) -> dict:
    """Compute per-scenario per-round ratios and the frozen across-round verdict."""
    rounds_data = []
    sources = {}
    for index, raw in enumerate(round_args):
        label = raw
        reports = discover_reports(raw)
        entries = load_entries(reports)
        pairs = pair_round(entries, label)
        per_scenario = round_ratios(pairs, label, alloc_tol, time_tol)
        rounds_data.append((label, per_scenario))
        sources[label] = [str(report) for report in reports]

    scenarios = {}
    for scenario in SCENARIOS:
        per_round = [per_scenario[scenario] for _, per_scenario in rounds_data]
        alloc_decisions = [item["alloc_verdict"] for item in per_round]
        time_decisions = [item["time_verdict"] for item in per_round]
        scenarios[scenario] = {
            "nextorm_arm": per_round[0]["nextorm_arm"],
            "linq2db_arm": per_round[0]["linq2db_arm"],
            "alloc_verdict": "FAIL" if any(d == "FAIL" for d in alloc_decisions) else "MET",
            "time_verdict": "MET" if all(d == "MET" for d in time_decisions) else (
                "FAIL" if all(d == "FAIL" for d in time_decisions) else "OPEN"
            ),
            "decision": combine_rounds(time_decisions, alloc_decisions),
            "rounds": per_round,
        }

    decisions = [payload["decision"] for payload in scenarios.values()]
    if all(decision == "MET" for decision in decisions):
        overall = "MET"
    elif any(decision == "FAIL" for decision in decisions):
        overall = "FAIL"
    else:
        overall = "OPEN"

    return {
        "rounds": [label for label, _ in rounds_data],
        "sources": sources,
        "scenarios": scenarios,
        "overall": overall,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="t240_evidence.py",
        description="T240 (#240) BDN same-round construction-allocation ratio reader.",
    )
    parser.add_argument(
        "--round", action="append", default=[], metavar="PATH",
        help="BDN artifacts directory, or an explicit BDN JSON report path; repeatable.",
    )
    parser.add_argument(
        "--json", action="append", default=[], dest="json_reports", metavar="PATH",
        help="Explicit BDN JSON report path (alias of --round for a plain report file).",
    )
    parser.add_argument("--manifest", metavar="PATH", help="Frozen evidence manifest to record.")
    parser.add_argument("--output", metavar="PATH", help="Write the machine-readable verdict JSON here.")
    parser.add_argument("--alloc-tol", type=float, default=DEFAULT_ALLOC_TOL, dest="alloc_tol",
                        help=f"Allocation ratio limit (default {DEFAULT_ALLOC_TOL}).")
    parser.add_argument("--time-tol", type=float, default=DEFAULT_TIME_TOL, dest="time_tol",
                        help=f"Time ratio limit (default {DEFAULT_TIME_TOL}).")
    return parser


def main(argv=None) -> int:
    args = build_parser().parse_args(argv)
    round_args = list(args.round) + list(args.json_reports)
    if not round_args:
        print("REJECT: at least one --round <dir-or-json> is required", file=sys.stderr)
        return EXIT_REJECT

    try:
        manifest = load_manifest(args.manifest) if args.manifest else None
        result = evaluate(round_args, args.alloc_tol, args.time_tol)
    except Reject as exc:
        print(f"REJECT: {exc}", file=sys.stderr)
        return EXIT_REJECT

    report = {
        "task": "D240",
        "generated_utc": utc_now(),
        "alloc_tolerance": args.alloc_tol,
        "time_tolerance": args.time_tol,
        "manifest": args.manifest,
        "manifest_task": (manifest or {}).get("task"),
        "manifest_contract_rv": (manifest or {}).get("contract_rv"),
        "round_count": len(result["rounds"]),
        "rounds": result["rounds"],
        "sources": result["sources"],
        "scenarios": result["scenarios"],
        "overall": result["overall"],
    }

    print("consumed BDN JSON reports:")
    for label in result["rounds"]:
        for source in result["sources"][label]:
            print(f"  {source}")
    for index, label in enumerate(result["rounds"]):
        print(f"round: {label}")
        for scenario in SCENARIOS:
            item = result["scenarios"][scenario]["rounds"][index]
            print(
                f"  {scenario:<13s} alloc {item['alloc_ratio']:.4f} {item['alloc_verdict']:<4s}"
                f"  time {item['time_ratio']:.4f} {item['time_verdict']}"
            )
    print("across rounds (frozen conjunction):")
    for scenario in SCENARIOS:
        payload = result["scenarios"][scenario]
        print(
            f"  {scenario:<13s} alloc {payload['alloc_verdict']:<4s}"
            f" time {payload['time_verdict']:<4s} -> {payload['decision']}"
        )
    print(f"overall: {result['overall']}")

    if args.output:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(
            json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
        )
        print(f"wrote {output_path}")

    return EXIT_OK if result["overall"] == "MET" else EXIT_FAIL


if __name__ == "__main__":
    sys.exit(main())
