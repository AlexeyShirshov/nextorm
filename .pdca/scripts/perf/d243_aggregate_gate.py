#!/usr/bin/env python3
"""D243 (#243) warm-aggregate allocation + time gate.

Scope
-----
Decides R243-01 (allocation) and R243-02 (time) for the ``four-<i>`` BenchmarkDotNet
rounds of this task only.  It is deliberately *separate* from the shared
``iteration14_gate.py`` / ``iteration14-budgets.json`` (owned by T244/T245/T247) and reads
nothing from them.

What is measured
----------------
Allocation (R243-01).  For every ``after`` round, per terminal (``Count``, ``Sum``), the
NextORM arm's ``Allocated`` value must not exceed the pinned budget::

    Count <= 2864 B/op
    Sum   <= 3490 B/op

Every ``after`` round is checked on its own -- the rounds are never averaged or reduced to
a best round, and Count and Sum are never averaged together.

Allocation source (and why it is a window, not an integer).  The archived BDN rounds carry
only the csv/html/github exporters (no JSON exporter), so no exact
``Memory.BytesAllocatedPerOperation`` exists: the only allocation datum is the *formatted*
CSV ``Allocated`` string (``0.##`` in the largest fitting unit, 1 KB = 1024 B, e.g.
``2.91 KB``).  ``bytes_per_op`` is therefore the display value multiplied by 1024 -- a
derived, non-integer value -- and it is reported together with its display-quantization
bound ``0.005 KB = 5.12 B`` (documented as the +-5 B window).  No exact byte count is
invented, and ``bytes_per_op`` is never rounded to an integer.

Time (R243-02).  Per round the same-round ratio ``q = Mean(NextORM) / Mean(linq2db)`` is
computed for Count and Sum; the *median* of those per-round ratios is taken per terminal,
in ``before`` and in ``after``, and both predicates are required::

    median(q_after) <= 1.20
    median(q_after) <= 1.20 * median(q_before)

The rows report the rule's own numbers (``q`` per round, medians, bounds) rather than
collapsing them into a Mean+-Error summary.

Exit codes
----------
0 -- every allocation row and both time predicates pass;
1 -- at least one predicate fails (the failing rows are listed in ``violations``);
2 -- input error (missing/malformed round dirs, reports, arms or cell values).  A measured
     miss is never reported as an input error and vice versa: a value that cannot be
     parsed is rejected (2), a parsed value that exceeds its bound fails (1).

Usage
-----
    python3 scripts/perf/d243_aggregate_gate.py \
        --before artifacts/pdca/D243/rv1/before \
        --after  artifacts/pdca/D243/rv1/after \
        --output artifacts/pdca/D243/rv1/gate.json \
        --summary artifacts/pdca/D243/rv1/gate.md
"""
from __future__ import annotations

import argparse
import csv
import fnmatch
import json
import re
import statistics
import sys
from datetime import datetime, timezone
from pathlib import Path

# Pinned budgets (R243-01), bytes per operation, never averaged across terminals.
THRESHOLDS_B = {"Count": 2864, "Sum": 3490}
TERMINALS = ("Count", "Sum")

# BDN displays allocated sizes with ``0.##`` in the largest fitting unit, so one displayed
# unit step (0.01 KB) bounds the true value by +-0.005 KB = +-5.12 B (documented +-5 B).
DISPLAY_QUANTIZATION_KB = 0.005
QUANTIZATION_WINDOW_B = DISPLAY_QUANTIZATION_KB * 1024.0  # 5.12

# R243-02 time predicates.
TIME_ABSOLUTE_LIMIT = 1.20
TIME_RELATIVE_LIMIT = 1.20

ROUND_RE = re.compile(r"^four-(\d+)$")
NEXTORM_GLOB = "*B_Nextorm_Cached_{terminal}*"
LINQ_GLOB = "*B_Linq2Db_{terminal}*"
# BDN CSV exporter output, e.g. NextORM.Benchmark.SqliteBenchmarkAggregates-report.csv.
REPORT_GLOB = "*SqliteBenchmarkAggregates-report.csv"

ALLOC_FACTORS = {"": 1, "B": 1, "KB": 1024, "MB": 1024 ** 2, "GB": 1024 ** 3}
TIME_FACTORS_NS = {
    "ns": 1.0,
    "us": 1e3,
    "ms": 1e6,
    "s": 1e9,
}

QUANTIZATION_NOTE = (
    "BDN CSV `Allocated` is a display value (0.## in the largest fitting unit, "
    "1 KB = 1024 B); bytes_per_op = display * 1024 is derived and NOT an exact integer -- "
    "the archived rounds have no JSON exporter, so `Memory.BytesAllocatedPerOperation` is "
    "absent. One display step (0.01 KB) bounds each value by +-0.005 KB = "
    f"+-{QUANTIZATION_WINDOW_B:.2f} B (documented +-5 B), i.e. each value denotes the "
    "interval [bytes_per_op - 5.12, bytes_per_op + 5.12)."
)


class InputError(Exception):
    """Missing/malformed input evidence (exit 2)."""


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


# ------------------------------------------------------------------------------------------------
# Cell parsing
# ------------------------------------------------------------------------------------------------
def _split_value_unit(text, where):
    if text is None:
        raise InputError(f"{where}: empty cell")
    raw = text.strip()
    if not raw or raw in ("-", "NA", "N/A", "?"):
        raise InputError(f"{where}: no measurement in cell {text!r}")
    match = re.match(r"^([0-9]+(?:\.[0-9]+)?)\s*([A-Za-z\u00b5\u03bc]*)$", raw)
    if not match:
        raise InputError(f"{where}: unparsable measurement {text!r}")
    return float(match.group(1)), match.group(2)


def parse_allocated_bytes(text, where):
    """``'2.91 KB'`` -> 2979.84 B (derived display value; never an exact count)."""
    value, unit = _split_value_unit(text, where)
    key = unit.upper()
    if key not in ALLOC_FACTORS:
        raise InputError(f"{where}: unsupported allocation unit {unit!r} in {text!r}")
    return value * ALLOC_FACTORS[key]


def parse_time_ns(text, where):
    """``'18.48 μs'`` -> 18480.0 ns (normalized so ratios are unit-safe)."""
    value, unit = _split_value_unit(text, where)
    key = unit.replace("\u00b5", "u").replace("\u03bc", "u").lower()
    if key not in TIME_FACTORS_NS:
        raise InputError(f"{where}: unsupported time unit {unit!r} in {text!r}")
    return value * TIME_FACTORS_NS[key]


# ------------------------------------------------------------------------------------------------
# Discovery
# ------------------------------------------------------------------------------------------------
def discover_rounds(root: Path, label: str) -> dict:
    if not root.exists() or not root.is_dir():
        raise InputError(f"--{label} is not a directory: {root}")
    rounds = {}
    for child in sorted(root.iterdir()):
        if child.is_dir() and ROUND_RE.match(child.name):
            rounds[child.name] = child
    if not rounds:
        raise InputError(f"--{label} contains no four-<i>/ round directory: {root}")
    return rounds


def find_result_csv(round_dir: Path, label: str) -> Path:
    found = sorted(round_dir.rglob(REPORT_GLOB))
    if not found:
        raise InputError(
            f"--{label} round {round_dir.name}: no {REPORT_GLOB} under {round_dir}"
        )
    if len(found) > 1:
        joined = ", ".join(str(p) for p in found)
        raise InputError(
            f"--{label} round {round_dir.name}: multiple {REPORT_GLOB} matches: {joined}"
        )
    return found[0]


def load_round(round_dir: Path, label: str) -> dict:
    """Return ``{method: {'mean_ns': float, 'allocated_b': float}}`` for one round."""
    report = find_result_csv(round_dir, label)
    where = f"--{label} round {round_dir.name} ({report.name})"
    try:
        with report.open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            headers = reader.fieldnames or []
            for required in ("Method", "Mean", "Allocated"):
                if required not in headers:
                    raise InputError(f"{where}: CSV has no {required!r} column (headers: {headers})")
            arms = {}
            for index, row in enumerate(reader, start=2):
                if None in row:
                    raise InputError(f"{where}: line {index} has more fields than the header")
                method = (row.get("Method") or "").strip()
                if not method:
                    raise InputError(f"{where}: line {index} has an empty Method")
                if method in arms:
                    raise InputError(f"{where}: duplicate arm {method!r}")
                arms[method] = {
                    "mean_ns": parse_time_ns(row.get("Mean"), f"{where} line {index} Mean"),
                    "allocated_b": parse_allocated_bytes(
                        row.get("Allocated"), f"{where} line {index} Allocated"
                    ),
                }
    except OSError as exc:
        raise InputError(f"{where}: cannot read report: {exc}") from exc
    if not arms:
        raise InputError(f"{where}: report has no benchmark rows")
    return arms


def resolve_arm(methods, glob: str, terminal: str, where: str) -> str:
    matches = sorted(m for m in methods if fnmatch.fnmatchcase(m, glob))
    if not matches:
        raise InputError(f"{where}: no arm matching {glob!r} for terminal {terminal}")
    if len(matches) > 1:
        raise InputError(
            f"{where}: ambiguous arms for terminal {terminal}: {', '.join(matches)}"
        )
    return matches[0]


def load_rounds(rounds: dict, label: str) -> dict:
    return {name: load_round(path, label) for name, path in rounds.items()}


def check_round_alignment(before: dict, after: dict) -> None:
    missing = sorted(set(before) - set(after))
    unexpected = sorted(set(after) - set(before))
    if missing:
        raise InputError(f"--after is missing round director(y/ies): {', '.join(missing)}")
    if unexpected:
        raise InputError(
            f"--after has round director(y/ies) absent from --before: {', '.join(unexpected)}"
        )
    if not after:
        raise InputError("--after contains no four-<i>/ round directory")


def check_arm_alignment(before: dict, after: dict) -> None:
    reference = None
    for name, arms in before.items():
        current = frozenset(arms)
        if reference is None:
            reference = current
        elif current != reference:
            raise InputError(
                f"--before rounds disagree on the arm set ({name} differs from "
                f"{sorted(before)[0]})"
            )
    for name, arms in after.items():
        current = frozenset(arms)
        if current != reference:
            missing = sorted(reference - current)
            extra = sorted(current - reference)
            parts = []
            if missing:
                parts.append(f"missing: {', '.join(missing)}")
            if extra:
                parts.append(f"unexpected: {', '.join(extra)}")
            raise InputError(
                f"--after round {name}: arm set differs from --before ({'; '.join(parts)})"
            )


# ------------------------------------------------------------------------------------------------
# Evaluation
# ------------------------------------------------------------------------------------------------
def allocation_rows(before_arms: dict, after_arms: dict) -> list:
    rows = []
    for terminal in TERMINALS:
        for round_name in sorted(after_arms, key=_round_order):
            arms = after_arms[round_name]
            where = f"--after round {round_name}"
            method = resolve_arm(arms, NEXTORM_GLOB.format(terminal=terminal), terminal, where)
            # The before side must expose the same arm; the arm-set alignment already
            # guarantees it, but resolve so a missing terminal is an explicit input error.
            resolve_arm(
                before_arms[round_name],
                NEXTORM_GLOB.format(terminal=terminal),
                terminal,
                f"--before round {round_name}",
            )
            bytes_per_op = arms[method]["allocated_b"]
            threshold = THRESHOLDS_B[terminal]
            rows.append(
                {
                    "terminal": terminal,
                    "round": round_name,
                    "arm": method,
                    "bytes_per_op": bytes_per_op,
                    "bytes_per_op_interval_b": [
                        bytes_per_op - QUANTIZATION_WINDOW_B,
                        bytes_per_op + QUANTIZATION_WINDOW_B,
                    ],
                    "quantization_window_b": QUANTIZATION_WINDOW_B,
                    "threshold_b": threshold,
                    "pass": bytes_per_op <= threshold,
                    "slack_b": threshold - bytes_per_op,
                }
            )
    return rows


def _round_order(round_name: str):
    match = ROUND_RE.match(round_name)
    return (int(match.group(1)) if match else 10 ** 9, round_name)


def same_round_ratios(arms_by_round: dict, terminal: str, label: str) -> list:
    ratios = []
    for round_name in sorted(arms_by_round, key=_round_order):
        arms = arms_by_round[round_name]
        where = f"--{label} round {round_name}"
        nextorm = resolve_arm(arms, NEXTORM_GLOB.format(terminal=terminal), terminal, where)
        linq = resolve_arm(arms, LINQ_GLOB.format(terminal=terminal), terminal, where)
        denominator = arms[linq]["mean_ns"]
        if denominator <= 0:
            raise InputError(f"{where}: non-positive linq2db Mean for {terminal}")
        ratios.append(
            {
                "round": round_name,
                "nextorm_mean_ns": arms[nextorm]["mean_ns"],
                "linq2db_mean_ns": denominator,
                "q": arms[nextorm]["mean_ns"] / denominator,
            }
        )
    return ratios


def time_rows(before_arms: dict, after_arms: dict) -> list:
    rows = []
    for terminal in TERMINALS:
        before_ratios = same_round_ratios(before_arms, terminal, "before")
        after_ratios = same_round_ratios(after_arms, terminal, "after")
        median_before = statistics.median([item["q"] for item in before_ratios])
        median_after = statistics.median([item["q"] for item in after_ratios])
        relative_bound = TIME_RELATIVE_LIMIT * median_before
        pass_absolute = median_after <= TIME_ABSOLUTE_LIMIT
        pass_relative = median_after <= relative_bound
        rows.append(
            {
                "terminal": terminal,
                "q_before_per_round": before_ratios,
                "q_after_per_round": after_ratios,
                "median_q_before": median_before,
                "median_q_after": median_after,
                "absolute_limit": TIME_ABSOLUTE_LIMIT,
                "relative_limit": TIME_RELATIVE_LIMIT,
                "relative_bound": relative_bound,
                "pass_absolute": pass_absolute,
                "pass_relative": pass_relative,
                "pass": pass_absolute and pass_relative,
            }
        )
    return rows


def collect_violations(rows: list, t_rows: list) -> list:
    violations = []
    for row in rows:
        if row["pass"]:
            continue
        violations.append(
            {
                "kind": "allocation",
                "terminal": row["terminal"],
                "round": row["round"],
                "bytes_per_op": row["bytes_per_op"],
                "threshold_b": row["threshold_b"],
                "over_by_b": row["bytes_per_op"] - row["threshold_b"],
                "detail": (
                    f"{row['terminal']} {row['round']}: {row['bytes_per_op']:.2f} B/op "
                    f"> {row['threshold_b']} B/op"
                ),
            }
        )
    for row in t_rows:
        if row["pass"]:
            continue
        predicates = []
        if not row["pass_absolute"]:
            predicates.append(
                f"median(q_after)={row['median_q_after']:.4f} > {row['absolute_limit']:.2f}"
            )
        if not row["pass_relative"]:
            predicates.append(
                f"median(q_after)={row['median_q_after']:.4f} > "
                f"{row['relative_limit']:.2f} * median(q_before)={row['relative_bound']:.4f}"
            )
        violations.append(
            {
                "kind": "time",
                "terminal": row["terminal"],
                "median_q_before": row["median_q_before"],
                "median_q_after": row["median_q_after"],
                "relative_bound": row["relative_bound"],
                "detail": f"{row['terminal']}: " + "; ".join(predicates),
            }
        )
    return violations


# ------------------------------------------------------------------------------------------------
# Rendering
# ------------------------------------------------------------------------------------------------
def summary_markdown(payload: dict) -> str:
    if payload["status"] == "input-error":
        return (
            "# D243 aggregate gate — INPUT ERROR\n\n"
            f"`{payload.get('error', 'unknown input error')}`\n"
        )
    lines = [
        "# D243 aggregate gate",
        "",
        f"- status: **{payload['status']}**",
        f"- generated: `{payload['generated_utc']}`",
        f"- before: `{payload['before_dir']}`",
        f"- after: `{payload['after_dir']}`",
        "",
        "## Allocation (R243-01) — B/op per round, never averaged",
        "",
        "| terminal | round | B/op (derived) | ±5.12 B window | threshold | slack | pass |",
        "| --- | --- | ---: | --- | ---: | ---: | --- |",
    ]
    for row in payload["rows"]:
        low, high = row["bytes_per_op_interval_b"]
        lines.append(
            f"| {row['terminal']} | {row['round']} | {row['bytes_per_op']:.2f} | "
            f"{low:.2f} … {high:.2f} | {row['threshold_b']} | {row['slack_b']:.2f} | "
            f"{'yes' if row['pass'] else '**NO**'} |"
        )
    lines += [
        "",
        "## Time (R243-02) — same-round ratio `q = Mean(nextorm)/Mean(linq2db)`",
        "",
        "| terminal | q before per round | median q before | q after per round | median q after | "
        "<= 1.20 | <= 1.20·median(q_before) | pass |",
        "| --- | --- | ---: | --- | ---: | --- | --- | --- |",
    ]
    for row in payload["time_rows"]:
        before = ", ".join(f"{item['q']:.4f}" for item in row["q_before_per_round"])
        after = ", ".join(f"{item['q']:.4f}" for item in row["q_after_per_round"])
        lines.append(
            f"| {row['terminal']} | {before} | {row['median_q_before']:.4f} | {after} | "
            f"{row['median_q_after']:.4f} | {'yes' if row['pass_absolute'] else '**NO**'} | "
            f"{'yes' if row['pass_relative'] else '**NO**'} | "
            f"{'yes' if row['pass'] else '**NO**'} |"
        )
    lines.append("")
    if payload["violations"]:
        lines.append("## Violations")
        lines.append("")
        for violation in payload["violations"]:
            lines.append(f"- {violation['detail']}")
    else:
        lines.append("No violations.")
    lines.append("")
    return "\n".join(lines)


def write_text(path, text):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_json(path, payload):
    write_text(path, json.dumps(payload, indent=2, ensure_ascii=False) + "\n")


# ------------------------------------------------------------------------------------------------
# Driver
# ------------------------------------------------------------------------------------------------
def evaluate(before_dir: str, after_dir: str) -> dict:
    before_root = Path(before_dir)
    after_root = Path(after_dir)
    before_rounds = discover_rounds(before_root, "before")
    after_rounds = discover_rounds(after_root, "after")
    check_round_alignment(before_rounds, after_rounds)
    before_arms = load_rounds(before_rounds, "before")
    after_arms = load_rounds(after_rounds, "after")
    check_arm_alignment(before_arms, after_arms)

    rows = allocation_rows(before_arms, after_arms)
    t_rows = time_rows(before_arms, after_arms)
    violations = collect_violations(rows, t_rows)
    return {
        "status": "pass" if not violations else "fail",
        "generated_utc": utc_now(),
        "before_dir": str(before_root),
        "after_dir": str(after_root),
        "rows": rows,
        "time_rows": t_rows,
        "violations": violations,
        "quantization_note": QUANTIZATION_NOTE,
    }


def run(before_dir: str, after_dir: str, output: str | None, summary: str | None) -> int:
    try:
        payload = evaluate(before_dir, after_dir)
        code = 0 if payload["status"] == "pass" else 1
    except InputError as exc:
        payload = {
            "status": "input-error",
            "generated_utc": utc_now(),
            "before_dir": str(before_dir),
            "after_dir": str(after_dir),
            "rows": [],
            "time_rows": [],
            "violations": [{"kind": "input", "detail": str(exc)}],
            "quantization_note": QUANTIZATION_NOTE,
            "error": str(exc),
        }
        code = 2
        print(f"d243_aggregate_gate: input error: {exc}", file=sys.stderr)

    if output:
        try:
            write_json(output, payload)
        except OSError as exc:
            print(f"d243_aggregate_gate: cannot write --output {output}: {exc}", file=sys.stderr)
            return 2
    if summary:
        try:
            write_text(summary, summary_markdown(payload))
        except OSError as exc:
            print(f"d243_aggregate_gate: cannot write --summary {summary}: {exc}", file=sys.stderr)
            return 2

    if code == 0:
        print(f"d243_aggregate_gate: PASS ({len(payload['rows'])} allocation rows, "
              f"{len(payload['time_rows'])} time rows)")
    elif code == 1:
        for violation in payload["violations"]:
            print(f"  VIOLATION {violation['detail']}")
        print(f"d243_aggregate_gate: FAIL ({len(payload['violations'])} violation(s))")
    return code


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description="D243 warm-aggregate allocation/time gate (R243-01, R243-02)."
    )
    parser.add_argument("--before", required=True, help="before artifacts dir (four-<i>/ rounds)")
    parser.add_argument("--after", required=True, help="after artifacts dir (four-<i>/ rounds)")
    parser.add_argument("--output", help="JSON result path")
    parser.add_argument("--summary", help="optional markdown summary path")
    args = parser.parse_args(argv)
    return run(args.before, args.after, args.output, args.summary)


if __name__ == "__main__":
    sys.exit(main())
