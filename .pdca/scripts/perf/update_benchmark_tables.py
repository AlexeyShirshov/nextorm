#!/usr/bin/env python3
"""Regenerate the comparison tables in ``readme.md`` and ``docs/comparisons/benchmarks.md``.

The article tables are *derived* data: every cell comes from a committed BenchmarkDotNet report
under ``benchmarks/BenchmarkDotNet.Artifacts/results``.  This script reads those reports and
rewrites the generated blocks in place, so a benchmark run cannot leave the prose behind:

    <!-- benchmarks:conditions:start --> ... <!-- benchmarks:conditions:end -->
    <!-- benchmarks:e2e:start -->        ... <!-- benchmarks:e2e:end -->
    <!-- benchmarks:allocations:start -->... <!-- benchmarks:allocations:end -->
    <!-- benchmarks:category-b:start --> ... <!-- benchmarks:category-b:end -->

Targets:

* ``readme.md`` — conditions + end-to-end (time) table;
* ``docs/comparisons/benchmarks.md`` — conditions + end-to-end + allocations + Category B;
* ``docs/ru/comparisons/benchmarks.md`` — the Russian mirror of the same.

Usage::

    python3 .pdca/scripts/perf/update_benchmark_tables.py                    # rewrite the tables
    python3 .pdca/scripts/perf/update_benchmark_tables.py --check            # exit 1 if any table is stale
    python3 .pdca/scripts/perf/update_benchmark_tables.py --date 2026-10-17 # pin the "as of" date
    python3 .pdca/scripts/perf/update_benchmark_tables.py --check --date 2026-10-17

Fail-closed rules (a table is never generated from a partial run):

* the report for every row/class must exist and contain all four libraries;
* an unknown Category-B category is an error, not a silently dropped row;
* the Category-B logical-ops divisor is verified against the benchmark source.

Exit codes: 0 = written/up to date, 1 = stale (``--check``) or invalid evidence.
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import re
import sys
import textwrap
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
DEFAULT_RESULTS_DIR = REPO_ROOT / "benchmarks" / "BenchmarkDotNet.Artifacts" / "results"
BENCH_SRC_DIR = REPO_ROOT / "benchmarks" / "nextorm.benchmark"
GITHUB_BLOB_PREFIX = "https://github.com/AlexeyShirshov/nextorm/blob/main"

LIBRARIES = ("Nextorm", "Dapper", "linq2db", "EF Core")
CATEGORY_B_CLASS = "SqliteBenchmarkFeaturesFairCached"
CATEGORY_B_LOGICAL_OPS = 10

_TIME_RE = re.compile(r"^(?P<value>[0-9][0-9.,]*)\s*(?P<unit>ns|μs|us|ms|s)$")
_BYTES_RE = re.compile(r"^(?P<value>[0-9][0-9.,]*)\s*(?P<unit>B|KB|MB|GB)$")

_NS_PER_UNIT = {"ns": 1.0, "μs": 1_000.0, "us": 1_000.0, "ms": 1_000_000.0, "s": 1_000_000_000.0}
_BYTES_PER_UNIT = {"B": 1.0, "KB": 1024.0, "MB": 1024.0**2, "GB": 1024.0**3}


class TableError(Exception):
    """Invalid or incomplete benchmark evidence (exit 1)."""


@dataclass(frozen=True)
class E2ERow:
    class_name: str
    en: str
    ru: str


E2E_ROWS = (
    E2ERow("SqliteBenchmarkIteration", "Data fetch", "Выборка данных"),
    E2ERow("SqliteBenchmarkLargeIteration", "Wide data fetch", "Широкая выборка данных"),
    E2ERow("SqliteBenchmarkWhere", "Where", "Where"),
    E2ERow("SqliteBenchmarkSimulateWork", "Simulate work", "Simulate work"),
    E2ERow("SqliteBenchmarkAny", "Any", "Any"),
    E2ERow("SqliteBenchmarkFirst", "FirstOrDefault", "FirstOrDefault"),
    E2ERow("SqliteBenchmarkSingle", "SingleOrDefault", "SingleOrDefault"),
)

# (BDN category, display label EN, display label RU) in table order.
CATEGORY_B = (
    ("B_CaseWhen", "`CASE WHEN`", "`CASE WHEN`"),
    ("B_Cte", "CTE", "CTE"),
    ("B_Distinct", "`SELECT DISTINCT`", "`SELECT DISTINCT`"),
    ("B_Except", "`EXCEPT`", "`EXCEPT`"),
    ("B_InList", "IN-list", "IN-list"),
    ("B_Intersect", "`INTERSECT`", "`INTERSECT`"),
    ("B_Join4", "4-table join", "Join по 4 таблицам"),
    ("B_LeftJoin", "`LEFT JOIN`", "`LEFT JOIN`"),
    ("B_RecursiveCte", "Recursive CTE", "Рекурсивный CTE"),
    ("B_StringFunctions", "String functions", "Строковые функции"),
    ("B_Udf", "UDF", "UDF"),
    ("B_Window", "Window functions", "Оконные функции"),
)

_BLOCK_RE = re.compile(
    r"(?ms)^<!-- benchmarks:(?P<name>[a-z0-9-]+):start -->\r?\n.*?^<!-- benchmarks:(?P=name):end -->"
)


@dataclass(frozen=True)
class Target:
    name: str
    path: Path
    lang: str
    blocks: tuple


TARGETS = (
    Target(
        "readme",
        REPO_ROOT / "readme.md",
        "en",
        ("conditions", "e2e"),
    ),
    Target(
        "docs",
        REPO_ROOT / "docs" / "comparisons" / "benchmarks.md",
        "en",
        ("conditions", "e2e", "allocations", "category-b"),
    ),
    Target(
        "docs-ru",
        REPO_ROOT / "docs" / "ru" / "comparisons" / "benchmarks.md",
        "ru",
        ("conditions", "e2e", "allocations", "category-b"),
    ),
)


# ------------------------------------------------------------------------------------------------
# Number formatting / parsing
# ------------------------------------------------------------------------------------------------
def _number(text: str) -> float:
    return float(text.replace(",", ""))


def parse_time_ns(text: str) -> float:
    match = _TIME_RE.match(text.strip())
    if not match:
        raise TableError(f"cannot parse BenchmarkDotNet time value: {text!r}")
    return _number(match.group("value")) * _NS_PER_UNIT[match.group("unit")]


def parse_bytes(text: str) -> float:
    match = _BYTES_RE.match(text.strip())
    if not match:
        raise TableError(f"cannot parse BenchmarkDotNet allocation value: {text!r}")
    return _number(match.group("value")) * _BYTES_PER_UNIT[match.group("unit")]


def format_time_ns(ns: float) -> str:
    """Two decimals in the largest unit that keeps the mantissa >= 1."""
    if ns < 1_000.0:
        return f"{ns:.2f} ns"
    if ns < 1_000_000.0:
        return f"{ns / 1_000.0:.2f} μs"
    if ns < 1_000_000_000.0:
        return f"{ns / 1_000_000.0:.2f} ms"
    return f"{ns / 1_000_000_000.0:.2f} s"


def format_bytes(value: float, unit: str | None = None) -> str:
    """Format bytes in *unit* (or the largest unit that keeps the mantissa >= 1).

    BenchmarkDotNet picks one unit per report (e.g. the whole ``SqliteBenchmarkWhere`` allocation
    column is KB), so the end-to-end table reuses the unit of the winning row.  Bytes keep a
    thousands separator; larger units keep two decimals.
    """
    if unit is None:
        if value < 1024.0:
            unit = "B"
        elif value < 1024.0**2:
            unit = "KB"
        elif value < 1024.0**3:
            unit = "MB"
        else:
            unit = "GB"
    if unit == "B":
        return f"{value:,.0f} B"
    return f"{value / _BYTES_PER_UNIT[unit]:.2f} {unit}"


def format_alloc_like(raw: str, value: float) -> str:
    match = _BYTES_RE.match(raw.strip())
    if not match:
        raise TableError(f"cannot parse BenchmarkDotNet allocation value: {raw!r}")
    return format_bytes(value, match.group("unit"))


# ------------------------------------------------------------------------------------------------
# Report discovery / parsing
# ------------------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Measurement:
    method: str
    time_raw: str
    time_ns: float
    alloc_raw: str
    alloc_bytes: float
    category: str


def report_paths(results_dir: Path, class_name: str) -> tuple:
    stem = f"NextORM.Benchmark.{class_name}"
    csv_path = results_dir / f"{stem}-report.csv"
    md_path = results_dir / f"{stem}-report-github.md"
    if not csv_path.is_file() or not md_path.is_file():
        raise TableError(
            f"missing report for {class_name}: expected {csv_path.name} and {md_path.name} "
            f"under {results_dir} (run the benchmark first)"
        )
    return csv_path, md_path


def read_measurements(csv_path: Path) -> list:
    with csv_path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        measurements = []
        jobs = set()
        for row in reader:
            jobs.add(((row.get("Job") or "").strip(), (row.get("Toolchain") or "").strip()))
            method = (row.get("Method") or "").strip()
            mean = (row.get("Mean") or "").strip()
            allocated = (row.get("Allocated") or "").strip()
            if not method or not mean or not allocated:
                raise TableError(f"{csv_path.name}: row without Method/Mean/Allocated: {row!r}")
            measurements.append(
                Measurement(
                    method=method,
                    time_raw=mean,
                    time_ns=parse_time_ns(mean),
                    alloc_raw=allocated,
                    alloc_bytes=parse_bytes(allocated),
                    category=(row.get("Categories") or "").strip(),
                )
            )
    if not measurements:
        raise TableError(f"{csv_path.name}: no benchmark rows")
    if len(jobs) > 1:
        raise TableError(
            f"{csv_path.name}: report mixes {len(jobs)} job/toolchain pairs {sorted(jobs)}; "
            "rerun with the default NextormConfig job so every cell comes from one toolchain"
        )
    return measurements


def library_of(method: str) -> str | None:
    # Category B names its arms ``B_<Library>_<Feature>``; the end-to-end classes do not.
    name = method[2:] if method.startswith("B_") else method
    if name.startswith("Nextorm"):
        return "Nextorm"
    if name.startswith("Dapper"):
        return "Dapper"
    if name.startswith("Linq2Db"):
        return "linq2db"
    if name.startswith("EFCore"):
        return "EF Core"
    return None


def fastest_per_library(class_name: str, measurements: list, *, require_all: bool = True) -> dict:
    winners = {}
    for measurement in measurements:
        library = library_of(measurement.method)
        if library is None:
            continue
        current = winners.get(library)
        if current is None or measurement.time_ns < current.time_ns:
            winners[library] = measurement
    missing = [library for library in LIBRARIES if library not in winners]
    # Category B legitimately has no EF Core arm for CTE / recursive CTE / window functions
    # (the article prints an em dash there), so only the end-to-end tables demand all four.
    if missing and require_all:
        raise TableError(
            f"{class_name}: report has no arm for {', '.join(missing)}; "
            "a filtered/acceptance-only run cannot feed the comparison table"
        )
    return winners


def _bold(text: str, is_leader: bool) -> str:
    return f"**{text}**" if is_leader else text


def _report_url(class_name: str) -> str:
    return (
        f"{GITHUB_BLOB_PREFIX}/benchmarks/BenchmarkDotNet.Artifacts/results/"
        f"NextORM.Benchmark.{class_name}-report-github.md"
    )


def build_e2e_table(results_dir: Path, lang: str) -> str:
    header = "| Method | Nextorm | Dapper | linq2db | EF Core |" if lang == "en" else "| Метод | Nextorm | Dapper | linq2db | EF Core |"
    lines = [header, "|---|:---:|:---:|:---:|:---:|"]
    for row in E2E_ROWS:
        csv_path, _ = report_paths(results_dir, row.class_name)
        winners = fastest_per_library(row.class_name, read_measurements(csv_path))
        best = min(measurement.time_ns for measurement in winners.values())
        cells = [
            _bold(winners[library].time_raw, winners[library].time_ns == best)
            for library in LIBRARIES
        ]
        lines.append(
            f"| [{row.en if lang == 'en' else row.ru}]({_report_url(row.class_name)}) | "
            + " | ".join(cells)
            + " |"
        )
    return "\n".join(lines)


def build_allocations_table(results_dir: Path, lang: str) -> str:
    header = "| Method | Nextorm | Dapper | linq2db | EF Core |" if lang == "en" else "| Метод | Nextorm | Dapper | linq2db | EF Core |"
    lines = [header, "|---|:---:|:---:|:---:|:---:|"]
    for row in E2E_ROWS:
        csv_path, _ = report_paths(results_dir, row.class_name)
        winners = fastest_per_library(row.class_name, read_measurements(csv_path))
        smallest = min(measurement.alloc_bytes for measurement in winners.values())
        cells = [
            _bold(
                format_alloc_like(winners[library].alloc_raw, winners[library].alloc_bytes),
                winners[library].alloc_bytes == smallest,
            )
            for library in LIBRARIES
        ]
        lines.append(
            f"| {row.en if lang == 'en' else row.ru} | " + " | ".join(cells) + " |"
        )
    return "\n".join(lines)


def _verify_category_b_divisor() -> int:
    source = (BENCH_SRC_DIR / f"{CATEGORY_B_CLASS}.cs").read_text(encoding="utf-8")
    match = re.search(r"private\s+const\s+int\s+Iterations\s*=\s*(\d+)\s*;", source)
    if not match:
        raise TableError(f"{CATEGORY_B_CLASS}.cs: cannot find the Iterations constant")
    iterations = int(match.group(1))
    if iterations != CATEGORY_B_LOGICAL_OPS:
        raise TableError(
            f"{CATEGORY_B_CLASS}.cs: Iterations={iterations}, "
            f"but the table divides by {CATEGORY_B_LOGICAL_OPS}; update CATEGORY_B_LOGICAL_OPS"
        )
    return iterations


def build_category_b_table(results_dir: Path, lang: str) -> str:
    divisor = _verify_category_b_divisor()
    csv_path, _ = report_paths(results_dir, CATEGORY_B_CLASS)
    measurements = read_measurements(csv_path)
    known = {category for category, _, _ in CATEGORY_B}
    unknown = sorted({m.category for m in measurements if m.category and m.category not in known})
    if unknown:
        raise TableError(
            f"{CATEGORY_B_CLASS}: unknown Category-B categories {unknown}; "
            "add them to CATEGORY_B (labels EN/RU) instead of dropping rows"
        )

    header = (
        "| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |"
        if lang == "en"
        else "| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |"
    )
    lines = [header, "|---|---|---|---|---|"]
    for category, label_en, label_ru in CATEGORY_B:
        group = [m for m in measurements if m.category == category]
        if not group:
            raise TableError(f"{CATEGORY_B_CLASS}: missing category {category} in {csv_path.name}")
        winners = fastest_per_library(f"{CATEGORY_B_CLASS}/{category}", group, require_all=False)
        best = min(measurement.time_ns for measurement in winners.values())
        cells = []
        for library in LIBRARIES:
            measurement = winners.get(library)
            if measurement is None:
                cells.append("—")
                continue
            cell = (
                f"{format_time_ns(measurement.time_ns / divisor)} / "
                f"{format_bytes(measurement.alloc_bytes / divisor)}"
            )
            cells.append(_bold(cell, measurement.time_ns == best))
        lines.append(
            f"| {label_en if lang == 'en' else label_ru} | " + " | ".join(cells) + " |"
        )
    return "\n".join(lines)


# ------------------------------------------------------------------------------------------------
# Conditions paragraph
# ------------------------------------------------------------------------------------------------
def read_run_conditions(results_dir: Path) -> dict:
    _, md_path = report_paths(results_dir, E2E_ROWS[0].class_name)
    text = md_path.read_text(encoding="utf-8")
    bdn = re.search(r"BenchmarkDotNet v([\w.\-]+)", text)
    machine = re.search(
        r"^(?P<cpu>.+?), \d+ CPU, (?P<logical>\d+) logical and (?P<physical>\d+) physical cores",
        text,
        re.MULTILINE,
    )
    runtime = re.search(r"\[Host\][^:]*:\s*\.NET ([\d.]+)", text)
    toolchain = re.search(r"Job=(\w+)(?:\s+Toolchain=(\w+))?", text)
    if not bdn or not machine or not runtime:
        raise TableError(f"{md_path.name}: cannot read the machine/runtime header")
    cpu = re.sub(r"\s+\d+(?:\.\d+)?GHz$", "", machine.group("cpu").strip())
    return {
        "cpu": cpu,
        "logical": machine.group("logical"),
        "physical": machine.group("physical"),
        "runtime": runtime.group(1),
        "bdn": bdn.group(1),
        # BDN omits the job line when it matches the default display; every comparison class uses
        # NextormConfig, which is ShortRun + InProcessEmitToolchain unless NEXTORM_BENCH_FULL=1.
        "job": toolchain.group(1) if toolchain else "ShortRun",
        "toolchain": toolchain.group(2) if toolchain and toolchain.group(2) else "InProcessEmitToolchain",
    }


def report_date(results_dir: Path) -> str:
    """Date of the benchmark run, taken from the end-to-end report mtime.

    Deriving it from the report (not from the clock) keeps ``--check`` stable on later days.
    """
    _, md_path = report_paths(results_dir, E2E_ROWS[0].class_name)
    return dt.date.fromtimestamp(md_path.stat().st_mtime).isoformat()


def build_conditions(target: Target, results_dir: Path, date: str) -> str:
    conditions = read_run_conditions(results_dir)
    if target.lang == "en":
        machine = (
            f"{conditions['cpu']} ({conditions['logical']} logical / "
            f"{conditions['physical']} physical cores)"
        )
    else:
        machine = (
            f"{conditions['cpu']} ({conditions['logical']} логических / "
            f"{conditions['physical']} физических ядер)"
        )
    if target.name == "readme":
        text = (
            "Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`); async queries; each cell is "
            "the fastest method for that ORM in the linked report. Machine: "
            f"{machine}, .NET {conditions['runtime']}, BenchmarkDotNet {conditions['bdn']} "
            f"(`{conditions['job']}` + `{conditions['toolchain']}`). As of **{date}**."
        )
    elif target.lang == "en":
        text = (
            "Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async "
            "queries; each cell is the **fastest method** for that library in the linked BenchmarkDotNet "
            f"report. Machine: {machine}, .NET {conditions['runtime']}, "
            f"BenchmarkDotNet {conditions['bdn']} (`{conditions['job']}` + "
            f"`{conditions['toolchain']}`). As of **{date}** for the end-to-end, allocations, Category A and "
            "Category B tables (the tier-1 section keeps the date of its own run). In each row the "
            "leading cell is **bold** — the fastest in the time tables and the smallest allocation in the "
            "allocations table."
        )
    else:
        text = (
            "Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; "
            "в ячейке — **самый быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. "
            f"Машина: {machine}, .NET {conditions['runtime']}, BenchmarkDotNet {conditions['bdn']} "
            f"(`{conditions['job']}` + `{conditions['toolchain']}`). На **{date}** для таблиц сквозных "
            "сценариев, аллокаций, категории A и категории B (раздел tier-1 сохраняет дату своего "
            "прогона). В каждой строке лидирующая ячейка выделена **полужирным** — быстрейшая в таблицах "
            "времени и с наименьшей аллокацией в таблице аллокаций."
        )
    return textwrap.fill(text, width=110)


# ------------------------------------------------------------------------------------------------
# File rewriting
# ------------------------------------------------------------------------------------------------
def read_text_preserving(path: Path) -> str:
    """Read without universal-newline translation so CRLF files stay CRLF."""
    with path.open("r", encoding="utf-8", newline="") as handle:
        return handle.read()


def _line_end(text: str) -> str:
    return "\r\n" if "\r\n" in text else "\n"


def wrap_block(name: str, content: str, line_end: str) -> str:
    body = content.replace("\n", line_end)
    return f"<!-- benchmarks:{name}:start -->{line_end}{body}{line_end}<!-- benchmarks:{name}:end -->"


def replace_blocks(text: str, blocks: dict) -> str:
    present = {match.group("name") for match in _BLOCK_RE.finditer(text)}
    missing = sorted(set(blocks) - present)
    if missing:
        raise TableError(
            f"marker block(s) {missing} not found; add "
            "<!-- benchmarks:<name>:start/end --> around the table"
        )
    line_end = _line_end(text)

    def substitute(match):
        name = match.group("name")
        if name not in blocks:
            return match.group(0)
        return wrap_block(name, blocks[name], line_end)

    return _BLOCK_RE.sub(substitute, text)


def render_target(target: Target, results_dir: Path, date: str) -> str:
    text = read_text_preserving(target.path)
    blocks = {}
    for name in target.blocks:
        if name == "conditions":
            blocks[name] = build_conditions(target, results_dir, date)
        elif name == "e2e":
            blocks[name] = build_e2e_table(results_dir, target.lang)
        elif name == "allocations":
            blocks[name] = build_allocations_table(results_dir, target.lang)
        elif name == "category-b":
            blocks[name] = build_category_b_table(results_dir, target.lang)
        else:
            raise TableError(f"unknown block {name!r} on target {target.name}")
    return replace_blocks(text, blocks)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--results-dir",
        type=Path,
        default=DEFAULT_RESULTS_DIR,
        help=f"BenchmarkDotNet results directory (default: {DEFAULT_RESULTS_DIR})",
    )
    parser.add_argument(
        "--date",
        default=None,
        help=(
            "'as of' date written into the conditions line "
            "(default: the mtime date of the end-to-end report)"
        ),
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="do not write; exit 1 when a generated block differs from the report data",
    )
    args = parser.parse_args(argv)

    results_dir = args.results_dir.resolve()
    stale = []
    try:
        date = args.date or report_date(results_dir)
        for target in TARGETS:
            current = read_text_preserving(target.path)
            updated = render_target(target, results_dir, date)
            if updated != current:
                stale.append(target.path)
                if not args.check:
                    target.path.write_text(updated, encoding="utf-8", newline="")
                    print(f"updated {target.path.relative_to(REPO_ROOT)}")
        if args.check:
            for path in stale:
                print(f"stale {path.relative_to(REPO_ROOT)}", file=sys.stderr)
            if stale:
                print("benchmark tables are out of date; run .pdca/scripts/perf/update_benchmark_tables.py", file=sys.stderr)
                return 1
        print("benchmark tables are up to date" if not stale else f"updated {len(stale)} file(s)")
        return 0
    except TableError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
