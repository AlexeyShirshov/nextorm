#!/usr/bin/env python3
"""Regenerate comparison tables in ``readme.md`` and the benchmark articles.

The article tables are *derived* data: every cell comes from a committed BenchmarkDotNet report
under ``benchmarks/BenchmarkDotNet.Artifacts/results``.  This script reads those reports and
rewrites the generated blocks in place, so a benchmark run cannot leave the prose behind:

    <!-- benchmarks:conditions:start --> ... <!-- benchmarks:conditions:end -->
    <!-- benchmarks:e2e:start -->        ... <!-- benchmarks:e2e:end -->
    <!-- benchmarks:allocations:start -->... <!-- benchmarks:allocations:end -->
    <!-- benchmarks:category-b:start --> ... <!-- benchmarks:category-b:end -->

Targets:

* ``readme.md`` — conditions + end-to-end (time) table, extended with the basic-feature rows of
  the Category A suite; every row also carries a ``Code`` link to the benchmark source it measures;
* ``docs/comparisons/benchmarks.md`` — conditions + end-to-end + allocations + Categories A/B + Tier-1;
* ``docs/ru/comparisons/benchmarks.md`` — the Russian mirror of the same.

Usage::

    python3 tools/benchmarks/update_benchmark_tables.py                    # rewrite the tables
    python3 tools/benchmarks/update_benchmark_tables.py --check            # exit 1 if any table is stale
    python3 tools/benchmarks/update_benchmark_tables.py --date 2026-10-17 # override the conditions date
    python3 tools/benchmarks/update_benchmark_tables.py --check --date 2026-10-17
    python3 tools/benchmarks/update_benchmark_tables.py \\
        --record-run-date SqliteBenchmarkIteration=2026-10-17 \\
        --run-evidence "local benchmark run evidence"

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
import hashlib
import json
import re
import sys
import textwrap
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_RESULTS_DIR = REPO_ROOT / "benchmarks" / "BenchmarkDotNet.Artifacts" / "results"
BENCH_SRC_DIR = REPO_ROOT / "benchmarks" / "nextorm.benchmark"
GITHUB_BLOB_PREFIX = "https://github.com/AlexeyShirshov/nextorm/blob/main"

LIBRARIES = ("Nextorm", "Dapper", "linq2db", "EF Core")
CATEGORY_A_CLASS = "SqliteBenchmarkFeaturesFair"
CATEGORY_A_LOGICAL_OPS = 10
CATEGORY_B_CLASS = "SqliteBenchmarkFeaturesFairCached"
CATEGORY_B_LOGICAL_OPS = 10

# Tier-1 tables use the same committed BDN CSV/Markdown pairs as the other generated tables. The
# companion run metadata binds the published snapshot date and execution context to the exact
# report files, so `--check` catches both stale cells and a stale "as of" claim.
TIER1_REPORTS = {
    "projection": ("SqliteBenchmarkProjection", 7),
    "aggregates": ("SqliteBenchmarkAggregates", 21),
    "paging": ("SqliteBenchmarkPaging", 7),
    "streaming": ("SqliteBenchmarkStreaming", 22),
    "write-json": ("SqliteBenchmarkWriteJson", 33),
    "csv": ("SqliteBenchmarkCsv", 8),
}
TIER1_RUN_REPORTS = TIER1_REPORTS
TIER1_RUN_METADATA = "tier1-run.json"
SUITE_RUN_DATE_METADATA = "suite-run-dates.json"
TIER1_BLOCKS = (
    "tier1-run-info",
    "tier1-caveats",
    "tier1-projection",
    "tier1-aggregates",
    "tier1-paging",
    "tier1-streaming-buffered",
    "tier1-streaming-unbuffered",
    "tier1-streaming-readers",
    "tier1-streaming-empty-readers",
    "tier1-write-json",
    "tier1-csv",
)

TIER1_EXPECTED_METHODS = {
    "projection": (
        "A_Nextorm_Prepared_ToListAsync",
        "A_EFCore_Compiled_ToListAsync",
        "A_Linq2Db_Compiled_ToList",
        "A_Dapper_ToListAsync",
        "B_Nextorm_Cached_ToListAsync",
        "B_Linq2Db_ToListAsync",
        "B_EFCore_ToListAsync",
    ),
    "aggregates": (
        "A_Nextorm_Prepared_Count",
        "A_Linq2Db_Compiled_Count",
        "A_EFCore_Compiled_Count",
        "A_Nextorm_Prepared_Sum",
        "A_Linq2Db_Compiled_Sum",
        "A_EFCore_Compiled_Sum",
        "A_Nextorm_Prepared_GroupByCount",
        "A_Linq2Db_Compiled_GroupByCount",
        "A_EFCore_Compiled_GroupByCount",
        "Dapper_Count",
        "Dapper_Sum",
        "Dapper_GroupByCount",
        "B_Nextorm_Cached_Count",
        "B_Linq2Db_Count",
        "B_EFCore_Count",
        "B_Nextorm_Cached_Sum",
        "B_Linq2Db_Sum",
        "B_EFCore_Sum",
        "B_Nextorm_Cached_GroupByCount",
        "B_Linq2Db_GroupByCount",
        "B_EFCore_GroupByCount",
    ),
    "paging": (
        "A_Nextorm_Prepared_PageAsync",
        "A_Linq2Db_Compiled_Page",
        "A_EFCore_Compiled_PageAsync",
        "A_Dapper_PageAsync",
        "B_Nextorm_Cached_PageAsync",
        "B_Linq2Db_PageAsync",
        "B_EFCore_PageAsync",
    ),
    "streaming": (
        "A_Nextorm_Prepared_ToList_Dto",
        "Linq2Db_Compiled_ToList_Dto",
        "A_EFCore_Compiled_ToList_Dto",
        "Dapper_ToList_Dto",
        "B_Nextorm_Cached_ToList_Dto",
        "B_Linq2Db_ToList_Dto",
        "B_EFCore_ToList_Dto",
        "A_Nextorm_Prepared_AsyncStream_Dto",
        "A_EFCore_Compiled_AsyncStream_Dto",
        "Dapper_AsyncStream_Dto",
        "B_Nextorm_Cached_AsyncStream_Dto",
        "B_Linq2Db_AsyncStream_Dto",
        "B_EFCore_AsyncStream_Dto",
        "A_Nextorm_Prepared_ToDataReader",
        "Linq2Db_ToDataReader",
        "Provider_Direct_ToDataReader",
        "Dapper_ToDataReader",
        "B_Nextorm_ToDataReader",
        "Provider_Direct_ToDataReader_Empty",
        "Dapper_ToDataReader_Empty",
        "Linq2Db_ToDataReader_Empty",
        "B_Nextorm_ToDataReader_Empty",
    ),
    "write-json": (
        "WriteJson_Array_Scalar",
        "WriteJsonAsync_Array_Scalar",
        "ToList_Dto",
        "Nextorm_ToList_Dto_Json",
        "WriteJson_Array_Dto",
        "Linq2Db_ToList_Json",
        "WriteJson_Array_ScalarPayload",
        "WriteJsonAsync_Array_ScalarPayload",
        "Dapper_ToList_Json",
        "EFCore_ToList_Json",
        "WriteJson_Array_WideDto",
    ),
    "csv": (
        "Nextorm_StringProjection_ToList",
        "Nextorm_StringProjection_WriteCsv",
        "Nextorm_ToList",
        "Nextorm_ToList_ManualCsv",
        "Nextorm_WriteCsv",
        "Linq2Db_ToList_ManualCsv",
        "Dapper_ToList_ManualCsv",
        "EFCore_ToList_ManualCsv",
    ),
}

TIER1_REQUIRED_REPORTS = tuple(
    f"NextORM.Benchmark.{class_name}{suffix}"
    for class_name, _ in TIER1_RUN_REPORTS.values()
    for suffix in ("-report-github.md", "-report.csv")
)

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

# Basic query features shared by the two fair-comparison suites: (BDN category suffix, label EN,
# label RU) in table order. Both derived tuples below come from this one list so the readme
# (Category A) and the article (Category B) can never drift apart.
FEATURE_CATEGORIES = (
    ("CaseWhen", "`CASE WHEN`", "`CASE WHEN`"),
    ("Cte", "CTE", "CTE"),
    ("Distinct", "`SELECT DISTINCT`", "`SELECT DISTINCT`"),
    ("Except", "`EXCEPT`", "`EXCEPT`"),
    ("InList", "IN-list", "IN-list"),
    ("Intersect", "`INTERSECT`", "`INTERSECT`"),
    ("Join4", "4-table join", "Join по 4 таблицам"),
    ("LeftJoin", "`LEFT JOIN`", "`LEFT JOIN`"),
    ("RecursiveCte", "Recursive CTE", "Рекурсивный CTE"),
    ("StringFunctions", "String functions", "Строковые функции"),
    ("Udf", "UDF", "UDF"),
    ("Window", "Window functions", "Оконные функции"),
)

# (BDN category, display label EN, display label RU) in table order.
CATEGORY_A = tuple((f"A_{key}", en, ru) for key, en, ru in FEATURE_CATEGORIES)
CATEGORY_B = tuple((f"B_{key}", en, ru) for key, en, ru in FEATURE_CATEGORIES)
SUITE_RUN_DATE_CLASSES = tuple(row.class_name for row in E2E_ROWS) + (
    CATEGORY_A_CLASS,
    CATEGORY_B_CLASS,
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
    # Append the basic-feature rows to the end-to-end table. Only the readme has no feature table
    # of its own: the article prints those numbers (time *and* allocations) in its Category A/B
    # sections, so growing its end-to-end table too would duplicate them on the same page.
    feature_rows: bool = False
    # Add a "Code" column that links every row to the benchmark source it measures (the readme is
    # the landing page: a reader must see the measured code next to the numbers, not only the
    # report). The article keeps its own layout and links reports from its prose instead.
    code_links: bool = False


TARGETS = (
    Target(
        "readme",
        REPO_ROOT / "readme.md",
        "en",
        ("conditions", "e2e"),
        feature_rows=True,
        code_links=True,
    ),
    Target(
        "docs",
        REPO_ROOT / "docs" / "comparisons" / "benchmarks.md",
        "en",
        ("conditions", "e2e", "allocations", "category-a", "category-b", *TIER1_BLOCKS),
    ),
    Target(
        "docs-ru",
        REPO_ROOT / "docs" / "ru" / "comparisons" / "benchmarks.md",
        "ru",
        ("conditions", "e2e", "allocations", "category-a", "category-b", *TIER1_BLOCKS),
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


def _report_sha256(path: Path) -> str:
    """Hash report text with CRLF normalized, independent of checkout line-ending settings."""
    return hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


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
    # The fair-comparison suites name their arms ``A_<Library>_<Feature>`` /
    # ``B_<Library>_<Feature>``; the end-to-end classes do not.
    name = method[2:] if method[:2] in ("A_", "B_") else method
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


def _source_url(class_name: str, pattern: str, description: str) -> str:
    """Blob URL of the benchmark source, anchored at the line matching *pattern*.

    The readme's "Code" column must point at the code a row measures, so the anchor is resolved
    from the source itself; a renamed class or category fails closed instead of emitting a link
    that lands on the top of the file.
    """
    path = _benchmark_source(class_name)
    text = path.read_text(encoding="utf-8")
    match = re.search(pattern, text)
    if not match:
        raise TableError(f"{path.name}: cannot find {description}")
    line = text.count("\n", 0, match.start()) + 1
    return f"{GITHUB_BLOB_PREFIX}/{path.relative_to(REPO_ROOT).as_posix()}#L{line}"


def _class_source_url(class_name: str) -> str:
    return _source_url(
        class_name, rf"\bclass\s+{re.escape(class_name)}\b", f"class {class_name}"
    )


def _category_source_url(class_name: str, category: str) -> str:
    return _source_url(
        class_name,
        rf'BenchmarkCategory\("{re.escape(category)}"\)',
        f'BenchmarkCategory("{category}")',
    )


def build_e2e_table(
    results_dir: Path, lang: str, *, include_features: bool = False, code_links: bool = False
) -> str:
    header = "| Method | Nextorm | Dapper | linq2db | EF Core |" if lang == "en" else "| Метод | Nextorm | Dapper | linq2db | EF Core |"
    if code_links:
        header += " Code |"
    lines = [header, "|---|" + ":---:|" * len(LIBRARIES) + ("---|" if code_links else "")]
    for row in E2E_ROWS:
        csv_path, _ = report_paths(results_dir, row.class_name)
        winners = fastest_per_library(row.class_name, read_measurements(csv_path))
        best = min(measurement.time_ns for measurement in winners.values())
        cells = [
            _bold(winners[library].time_raw, winners[library].time_ns == best)
            for library in LIBRARIES
        ]
        label = f"[{row.en if lang == 'en' else row.ru}]({_report_url(row.class_name)})"
        if code_links:
            cells.append(f"[code]({_class_source_url(row.class_name)})")
        lines.append("| " + " | ".join([label, *cells]) + " |")
    if include_features:
        lines.extend(build_feature_rows(results_dir, lang, code_links=code_links))
    return "\n".join(lines)


def _fair_groups(results_dir: Path, class_name: str, categories: tuple, expected_ops: int) -> tuple:
    """Read one fair-comparison suite and split it into per-feature groups.

    Every feature method runs ``Iterations`` logical queries, so the caller divides the BDN totals by
    that divisor. Unknown or missing categories fail closed: a renamed benchmark must break the
    generator, never silently drop a row.
    """
    divisor = _verify_divisor(class_name, expected_ops)
    csv_path, _ = report_paths(results_dir, class_name)
    measurements = read_measurements(csv_path)
    known = {category for category, _, _ in categories}
    unknown = sorted({m.category for m in measurements if m.category and m.category not in known})
    if unknown:
        raise TableError(
            f"{class_name}: unknown categories {unknown}; add them to FEATURE_CATEGORIES "
            "instead of dropping rows"
        )
    groups = []
    for category, label_en, label_ru in categories:
        group = [m for m in measurements if m.category == category]
        if not group:
            raise TableError(f"{class_name}: missing category {category} in {csv_path.name}")
        groups.append((label_en, label_ru, group))
    return divisor, groups


def build_feature_rows(results_dir: Path, lang: str, *, code_links: bool = False) -> list:
    """Basic-feature rows read from the Category A suite, one row per feature.

    Each feature method runs ``Iterations`` queries, so the cell is the per-query mean of the
    fastest arm of that library — the same numbers the article's Category A table prints, minus the
    allocations. With ``code_links`` the row also links the feature's first benchmark method (the
    nextorm arm) in the suite source.
    """
    divisor, groups = _fair_groups(
        results_dir, CATEGORY_A_CLASS, CATEGORY_A, CATEGORY_A_LOGICAL_OPS
    )
    url = _report_url(CATEGORY_A_CLASS)
    lines = []
    for label_en, label_ru, group in groups:
        winners = fastest_per_library(
            f"{CATEGORY_A_CLASS}/{group[0].category}", group, require_all=False
        )
        best = min(measurement.time_ns for measurement in winners.values())
        cells = []
        for library in LIBRARIES:
            measurement = winners.get(library)
            if measurement is None:
                cells.append("—")
                continue
            cells.append(
                _bold(
                    format_time_ns(measurement.time_ns / divisor),
                    measurement.time_ns == best,
                )
            )
        label = f"[{label_en if lang == 'en' else label_ru}]({url})"
        if code_links:
            cells.append(f"[code]({_category_source_url(CATEGORY_A_CLASS, group[0].category)})")
        lines.append("| " + " | ".join([label, *cells]) + " |")
    return lines


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


def _benchmark_source(class_name: str) -> Path:
    """Locate a benchmark class source anywhere under the project.

    Benchmark sources are grouped by topic (``Sqlite/``, ``Infrastructure/``, ...), so the
    file is resolved recursively; when the file name and the class name disagree (e.g. a typo
    such as ``SqliteBenchmarkSimulteWork.cs`` holding ``SqliteBenchmarkSimulateWork``) the class
    declaration itself is the fallback. A missing or duplicate class fails closed instead of
    silently reading the wrong source.
    """
    def candidates():
        return [
            path
            for path in BENCH_SRC_DIR.rglob("*.cs")
            if not {"bin", "obj"} & set(path.relative_to(BENCH_SRC_DIR).parts)
        ]

    files = candidates()
    matches = sorted(path for path in files if path.name == f"{class_name}.cs")
    if not matches:
        pattern = re.compile(rf"\bclass\s+{re.escape(class_name)}\b")
        matches = sorted(
            path for path in files if pattern.search(path.read_text(encoding="utf-8"))
        )
    if not matches:
        raise TableError(f"{class_name}.cs: not found under {BENCH_SRC_DIR}")
    if len(matches) > 1:
        raise TableError(
            f"{class_name}.cs: ambiguous, found {[str(path) for path in matches]}"
        )
    return matches[0]


def _verify_divisor(class_name: str, expected: int) -> int:
    source = _benchmark_source(class_name).read_text(encoding="utf-8")
    match = re.search(r"private\s+const\s+int\s+Iterations\s*=\s*(\d+)\s*;", source)
    if not match:
        raise TableError(f"{class_name}.cs: cannot find the Iterations constant")
    iterations = int(match.group(1))
    if iterations != expected:
        raise TableError(
            f"{class_name}.cs: Iterations={iterations}, "
            f"but the table divides by {expected}; update the divisor constant"
        )
    return iterations


def _feature_table(
    results_dir: Path,
    class_name: str,
    categories: tuple,
    expected_ops: int,
    header: str,
    lang: str,
) -> str:
    """One table per fair-comparison suite: per-query mean and allocation, fastest arm in bold."""
    divisor, groups = _fair_groups(results_dir, class_name, categories, expected_ops)
    lines = [header, "|---|---|---|---|---|"]
    for label_en, label_ru, group in groups:
        winners = fastest_per_library(
            f"{class_name}/{group[0].category}", group, require_all=False
        )
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


def _dated_feature_table(
    results_dir: Path,
    class_name: str,
    categories: tuple,
    expected_ops: int,
    header: str,
    lang: str,
) -> str:
    """A feature table preceded by its own run date.

    Category A can be re-run on its own (the readme's basic-feature rows come from it), so its block
    dates itself instead of borrowing the end-to-end date printed in the conditions paragraph.
    """
    stamp = report_date(results_dir, class_name)
    dated = f"As of **{stamp}**." if lang == "en" else f"На **{stamp}**."
    return (
        dated
        + "\n\n"
        + _feature_table(results_dir, class_name, categories, expected_ops, header, lang)
    )


def build_category_a_table(results_dir: Path, lang: str) -> str:
    header = (
        "| Feature | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |"
        if lang == "en"
        else "| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |"
    )
    return _dated_feature_table(
        results_dir, CATEGORY_A_CLASS, CATEGORY_A, CATEGORY_A_LOGICAL_OPS, header, lang
    )


def build_category_b_table(results_dir: Path, lang: str) -> str:
    header = (
        "| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |"
        if lang == "en"
        else "| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |"
    )
    return _feature_table(
        results_dir, CATEGORY_B_CLASS, CATEGORY_B, CATEGORY_B_LOGICAL_OPS, header, lang
    )


# ------------------------------------------------------------------------------------------------
# Conditions paragraph
# ------------------------------------------------------------------------------------------------
def read_run_conditions(results_dir: Path) -> dict:
    csv_path, md_path = report_paths(results_dir, E2E_ROWS[0].class_name)
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

    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        raise TableError(f"{csv_path.name}: no job metadata")
    jobs = {(row.get("Job") or "").strip() for row in rows}
    toolchains = {(row.get("Toolchain") or "").strip() for row in rows}
    if len(jobs) != 1 or not next(iter(jobs)):
        raise TableError(f"{csv_path.name}: cannot infer one job label from CSV metadata: {sorted(jobs)}")
    if len(toolchains) != 1 or not next(iter(toolchains)):
        raise TableError(
            f"{csv_path.name}: cannot infer one toolchain from CSV metadata: {sorted(toolchains)}"
        )

    csv_job = next(iter(jobs))
    csv_toolchain = next(iter(toolchains))
    default_job_fields = (
        "IterationCount",
        "WarmupCount",
        "MinWarmupIterationCount",
        "MaxWarmupIterationCount",
        "MinIterationCount",
        "MaxIterationCount",
        "MinIterationTime",
        "IterationTime",
        "LaunchCount",
        "RunStrategy",
        "InvocationCount",
    )
    # BenchmarkDotNet omits `Job=` from its Markdown header when the configured job matches the
    # default display. In that case the CSV carries a hashed Job-* id, but its parameter columns
    # distinguish Job.Default (all default values, UnrollFactor=16) from ShortRun (3 iterations).
    # Do not silently call an unknown hashed job ShortRun: preserve its actual id instead.
    if csv_job.startswith("Job-") and all(
        all((row.get(field) or "").strip() == "Default" for field in default_job_fields)
        and (row.get("UnrollFactor") or "").strip() == "16"
        for row in rows
    ):
        inferred_job = "Job.Default"
    else:
        inferred_job = csv_job

    cpu = re.sub(r"\s+\d+(?:\.\d+)?GHz$", "", machine.group("cpu").strip())
    return {
        "cpu": cpu,
        "logical": machine.group("logical"),
        "physical": machine.group("physical"),
        "runtime": runtime.group(1),
        "bdn": bdn.group(1),
        "job": toolchain.group(1) if toolchain else inferred_job,
        "toolchain": toolchain.group(2) if toolchain and toolchain.group(2) else csv_toolchain,
    }


def report_date(results_dir: Path, class_name: str = E2E_ROWS[0].class_name) -> str:
    """Return the persisted UTC run date after verifying it is bound to this suite's reports."""
    metadata = _read_suite_run_date_metadata(results_dir)
    try:
        return metadata["suites"][class_name]["asOfDate"]
    except KeyError as exc:
        raise TableError(f"{class_name}: no persisted run date in {SUITE_RUN_DATE_METADATA}") from exc


def _parse_run_date(value: str, label: str) -> str:
    if not isinstance(value, str):
        raise TableError(f"{label}: expected an ISO UTC date (YYYY-MM-DD)")
    try:
        parsed = dt.date.fromisoformat(value)
    except ValueError as exc:
        raise TableError(f"{label}: invalid ISO date {value!r}") from exc
    if parsed.isoformat() != value:
        raise TableError(f"{label}: invalid ISO date {value!r}")
    return value


def _validate_suite_run_date_record(
    results_dir: Path,
    class_name: str,
    record: dict,
    *,
    verify_hashes: bool = True,
) -> None:
    required = {"asOfDate", "sourceEvidence", "reportsSha256"}
    if not isinstance(record, dict) or set(record) != required:
        raise TableError(
            f"{class_name}: expected asOfDate, sourceEvidence, and reportsSha256 in "
            f"{SUITE_RUN_DATE_METADATA}"
        )
    _parse_run_date(record["asOfDate"], f"{class_name}.asOfDate")
    if not isinstance(record["sourceEvidence"], str) or not record["sourceEvidence"].strip():
        raise TableError(f"{class_name}: missing source evidence in {SUITE_RUN_DATE_METADATA}")

    csv_path, md_path = report_paths(results_dir, class_name)
    hashes = record["reportsSha256"]
    expected_files = {csv_path.name, md_path.name}
    if not isinstance(hashes, dict) or set(hashes) != expected_files:
        raise TableError(
            f"{class_name}: report hash inventory mismatch in {SUITE_RUN_DATE_METADATA}"
        )
    for report_path in (csv_path, md_path):
        expected_hash = hashes[report_path.name]
        if not isinstance(expected_hash, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_hash):
            raise TableError(f"{report_path.name}: invalid SHA-256 in {SUITE_RUN_DATE_METADATA}")
        if verify_hashes and _report_sha256(report_path) != expected_hash:
            raise TableError(
                f"{report_path.name}: SHA-256 differs from {SUITE_RUN_DATE_METADATA}; "
                "refresh the suite run metadata from matching run evidence"
            )


def _read_suite_run_date_metadata(
    results_dir: Path,
    *,
    require_complete: bool = True,
    replaced_classes: frozenset = frozenset(),
) -> dict:
    path = results_dir / SUITE_RUN_DATE_METADATA
    if not path.is_file():
        raise TableError(f"missing suite run date metadata: {path}")
    try:
        metadata = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise TableError(f"invalid suite run date metadata {path.name}: {exc}") from exc
    suites = metadata.get("suites") if isinstance(metadata, dict) else None
    if (
        not isinstance(metadata, dict)
        or metadata.get("schemaVersion") != 1
        or metadata.get("dateBasis") != "UTC suite-start date"
        or not isinstance(suites, dict)
    ):
        raise TableError(
            f"{path.name}: expected schemaVersion 1, UTC suite-start date, and suites object"
        )
    unexpected = sorted(set(suites) - set(SUITE_RUN_DATE_CLASSES))
    missing = sorted(set(SUITE_RUN_DATE_CLASSES) - set(suites))
    if unexpected or (require_complete and missing):
        raise TableError(
            f"{path.name}: suite inventory mismatch; missing={missing}, unexpected={unexpected}"
        )
    for class_name, record in suites.items():
        _validate_suite_run_date_record(
            results_dir,
            class_name,
            record,
            verify_hashes=class_name not in replaced_classes,
        )
    return metadata


def _record_suite_run_dates(
    results_dir: Path,
    run_dates: dict[str, str],
    source_evidence: str,
) -> None:
    if not run_dates:
        raise TableError("no suite run dates were provided")
    if not isinstance(source_evidence, str) or not source_evidence.strip():
        raise TableError("run evidence must be a non-empty path or URL")
    unexpected = sorted(set(run_dates) - set(SUITE_RUN_DATE_CLASSES))
    if unexpected:
        raise TableError(f"unknown benchmark suite classes in run dates: {unexpected}")
    for class_name, run_date in run_dates.items():
        _parse_run_date(run_date, f"{class_name}.asOfDate")

    path = results_dir / SUITE_RUN_DATE_METADATA
    if path.is_file():
        metadata = _read_suite_run_date_metadata(
            results_dir,
            require_complete=False,
            replaced_classes=frozenset(run_dates),
        )
    else:
        metadata = {
            "schemaVersion": 1,
            "dateBasis": "UTC suite-start date",
            "suites": {},
        }

    suites = metadata["suites"]
    for class_name, run_date in run_dates.items():
        csv_path, md_path = report_paths(results_dir, class_name)
        suites[class_name] = {
            "asOfDate": run_date,
            "sourceEvidence": source_evidence,
            "reportsSha256": {
                report_path.name: _report_sha256(report_path)
                for report_path in (csv_path, md_path)
            },
        }
        _validate_suite_run_date_record(results_dir, class_name, suites[class_name])

    serialized = json.dumps(metadata, indent=2, ensure_ascii=False).replace("\n", "\r\n") + "\r\n"
    path.write_text(serialized, encoding="utf-8", newline="")


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
            f"(`{conditions['job']}` + `{conditions['toolchain']}`). As of **{date}**. Each method name "
            "links to its BenchmarkDotNet report, and the **Code** column links to the benchmark source in "
            "this repository — the exact code behind the row. The last twelve rows "
            "are the basic-feature scenarios (Category A in the [comparison article]"
            "(https://alexeyshirshov.github.io/nextorm/comparisons/benchmarks.html)): Nextorm through "
            "`Prepare()` against the compiled form of EF Core and linq2db and raw Dapper, ten queries per "
            "iteration, so each cell there is the per-query mean. Those rows come from the Category A suite "
            f"run on **{report_date(results_dir, CATEGORY_A_CLASS)}**."
        )
    elif target.lang == "en":
        text = (
            "Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async "
            "queries; each cell is the **fastest method** for that library in the linked BenchmarkDotNet "
            f"report. Machine: {machine}, .NET {conditions['runtime']}, "
            f"BenchmarkDotNet {conditions['bdn']} (`{conditions['job']}` + "
            f"`{conditions['toolchain']}`). As of **{date}** for the end-to-end and allocations tables; "
            f"Category B was run on **{report_date(results_dir, CATEGORY_B_CLASS)}**. Category A and "
            "Tier-1 keep the dates of their own runs. In each row the "
            "leading cell is **bold** — the fastest in the time tables and the smallest allocation in the "
            "allocations table."
        )
    else:
        text = (
            "Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; "
            "в ячейке — **самый быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. "
            f"Машина: {machine}, .NET {conditions['runtime']}, BenchmarkDotNet {conditions['bdn']} "
            f"(`{conditions['job']}` + `{conditions['toolchain']}`). На **{date}** для таблиц сквозных "
            f"сценариев и аллокаций; категория B запущена **{report_date(results_dir, CATEGORY_B_CLASS)}**. "
            "Категория A и раздел tier-1 сохраняют даты своих прогонов. В каждой строке лидирующая ячейка "
            "выделена **полужирным** — быстрейшая в таблицах времени и с наименьшей аллокацией в таблице "
            "аллокаций."
        )
    return textwrap.fill(text, width=110)


# ------------------------------------------------------------------------------------------------
# Tier-1 comparison tables
# ------------------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Tier1Measurement:
    method: str
    category: str
    mean: str
    allocated: str
    allocated_bytes: float
    row_count: str
    ratio: str


def _tier1_measurements(results_dir: Path, key: str) -> list[Tier1Measurement]:
    class_name, expected_count = TIER1_RUN_REPORTS[key]
    csv_path, _ = report_paths(results_dir, class_name)
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        required_columns = {"Method", "Mean", "Allocated"}
        missing_columns = required_columns - set(reader.fieldnames or ())
        if missing_columns:
            raise TableError(
                f"{csv_path.name}: missing required columns {sorted(missing_columns)}"
            )
        measurements = []
        seen = set()
        for row in reader:
            method = (row.get("Method") or "").strip()
            mean = (row.get("Mean") or "").strip()
            allocated = (row.get("Allocated") or "").strip()
            row_count = (row.get("RowCount") or row.get("Rows") or "").strip()
            if not method or not mean or not allocated:
                raise TableError(
                    f"{csv_path.name}: row without Method/Mean/Allocated: {row!r}"
                )
            identity = (method, row_count) if row_count else method
            if identity in seen:
                raise TableError(f"{csv_path.name}: duplicate measurement {identity!r}")
            seen.add(identity)
            measurements.append(
                Tier1Measurement(
                    method=method,
                    category=(row.get("Categories") or "").strip(),
                    mean=mean,
                    allocated=allocated,
                    allocated_bytes=parse_bytes(allocated),
                    row_count=row_count,
                    ratio=(row.get("Ratio") or "").strip(),
                )
            )
    if len(measurements) != expected_count:
        raise TableError(
            f"{csv_path.name}: expected {expected_count} rows, found {len(measurements)}"
        )
    return measurements


def _tier1_expected_rows(
    measurements: list[Tier1Measurement],
    expected: tuple,
    report: str,
    *,
    exact: bool = True,
):
    by_method = {measurement.method: measurement for measurement in measurements}
    missing = sorted(set(expected) - by_method.keys())
    if missing:
        raise TableError(f"{report}: missing documented methods {missing}")
    if exact:
        unexpected = sorted(by_method.keys() - set(expected))
        if unexpected:
            raise TableError(f"{report}: unclassified methods {unexpected}")
    return [by_method[method] for method in expected]


def _tier1_category_label(category: str) -> str:
    prefixes = {
        category_part[0]
        for category_part in category.split(",")
        if category_part.startswith(("A_", "B_"))
    }
    if prefixes == {"A", "B"}:
        return "A+B"
    if prefixes:
        return "A" if "A" in prefixes else "B"
    return "—"


def _tier1_markdown_table(headers: tuple[str, ...], rows: list[tuple[str, ...]]) -> str:
    numeric_columns = 3 if "Ratio" in headers else 2
    separators = ["---"] * (len(headers) - numeric_columns) + ["---:"] * numeric_columns
    lines = ["| " + " | ".join(headers) + " |", "| " + " | ".join(separators) + " |"]
    lines.extend("| " + " | ".join(row) + " |" for row in rows)
    return "\n".join(lines)


def _metadata_timestamp(value, label: str, path: Path) -> dt.datetime:
    if not isinstance(value, str):
        raise TableError(f"{path.name}: {label} must be an ISO UTC timestamp")
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise TableError(f"{path.name}: invalid {label} {value!r}") from exc
    if parsed.tzinfo is None:
        raise TableError(f"{path.name}: {label} must include a timezone")
    return parsed


def _validate_group_runs(path: Path, run: dict, started: dt.datetime, finished: dt.datetime) -> None:
    """Represent a resumed Tier-1 run explicitly through its ordered per-group UTC windows.

    A run that finished on a later date than it started (for example a resumed session) carries
    ``run.groupRuns``: the actual window of every group, in execution order. Same-day runs keep the
    previous single-date contract and must not carry the field.
    """
    same_day = started.date() == finished.date()
    if same_day:
        if "groupRuns" in run:
            raise TableError(f"{path.name}: groupRuns is only meaningful for a multi-date run")
        return
    groups = run.get("groupRuns")
    if not isinstance(groups, list) or not groups:
        raise TableError(
            f"{path.name}: Tier-1 run spans multiple dates; add run.groupRuns with the actual per-group UTC windows"
        )
    labels: list[str] = []
    previous_end = None
    for index, entry in enumerate(groups):
        if not isinstance(entry, dict):
            raise TableError(f"{path.name}: groupRuns[{index}] must be an object")
        label = entry.get("group")
        if not isinstance(label, str) or not label:
            raise TableError(f"{path.name}: groupRuns[{index}].group must be a non-empty string")
        group_start = _metadata_timestamp(entry.get("startedUtc"), f"groupRuns[{index}].startedUtc", path)
        group_end = _metadata_timestamp(entry.get("finishedUtc"), f"groupRuns[{index}].finishedUtc", path)
        if group_end < group_start:
            raise TableError(f"{path.name}: groupRuns[{index}] ({label}) ends before it starts")
        if previous_end is not None and group_start < previous_end:
            raise TableError(f"{path.name}: groupRuns must be ordered and non-overlapping ({label})")
        previous_end = group_end
        labels.append(label)
    if len(set(labels)) != len(labels):
        raise TableError(f"{path.name}: duplicate group labels in groupRuns")
    if groups[0].get("startedUtc") != run["startedUtc"] or groups[-1].get("finishedUtc") != run["finishedUtc"]:
        raise TableError(f"{path.name}: groupRuns must cover the overall run window exactly")


def _tier1_run_metadata(results_dir: Path, measurements: dict) -> dict:
    path = results_dir / TIER1_RUN_METADATA
    if not path.is_file():
        raise TableError(f"missing Tier-1 run metadata: {path}")
    try:
        metadata = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise TableError(f"invalid Tier-1 run metadata {path.name}: {exc}") from exc
    run = metadata.get("run")
    hashes = metadata.get("reportsSha256")
    if metadata.get("schemaVersion") != 1 or not isinstance(run, dict) or not isinstance(hashes, dict):
        raise TableError(f"{path.name}: expected schemaVersion 1, run object, and reportsSha256")

    expected_files = set(TIER1_REQUIRED_REPORTS)
    if set(hashes) != expected_files:
        raise TableError(
            f"{path.name}: report hash inventory mismatch; "
            f"missing={sorted(expected_files - set(hashes))}, "
            f"unexpected={sorted(set(hashes) - expected_files)}"
        )
    for filename in sorted(expected_files):
        report_path = results_dir / filename
        if not report_path.is_file():
            raise TableError(f"missing Tier-1 report required by {path.name}: {filename}")
        actual_hash = _report_sha256(report_path)
        if actual_hash != hashes[filename]:
            raise TableError(
                f"{filename}: SHA-256 differs from {path.name}; refresh Tier-1 run metadata "
                "from the matching run evidence"
            )

    required = {
        "startedUtc",
        "finishedUtc",
        "sourceHead",
        "sourceTreeDirty",
        "sourceEvidence",
        "localNextormProjects",
        "localSourceGenerator",
        "matchedBuildRuntimeHashRows",
        "runtimeAssemblySha256",
        "sourceGeneratorAnalyzerSha256",
        "cpu",
        "logicalProcessors",
        "physicalCores",
        "sdk",
        "runtime",
        "benchmarkDotNet",
        "toolchain",
        "defaultJobRows",
        "defaultUnrollFactor",
        "rawReaderRows",
        "rawReaderInvocationCount",
        "rawReaderUnrollFactor",
        "tier1Rows",
        "totalRunCases",
        "additionalDiagnosticCases",
        "failedRows",
        "minIterationTimeWarnings",
        "multimodalWarnings",
    }
    missing = sorted(required - run.keys())
    if missing:
        raise TableError(f"{path.name}: missing run metadata fields {missing}")
    assembly_hashes = run["runtimeAssemblySha256"]
    analyzer_hash = run["sourceGeneratorAnalyzerSha256"]
    has_analyzer = run["localSourceGenerator"]
    if (
        not isinstance(assembly_hashes, dict)
        or not isinstance(run["sourceTreeDirty"], bool)
        or not isinstance(has_analyzer, bool)
        or len(assembly_hashes) != run["localNextormProjects"]
        or run["matchedBuildRuntimeHashRows"] != len(assembly_hashes) + int(has_analyzer)
        or not run["sourceEvidence"]
        or (has_analyzer and not re.fullmatch(r"[0-9a-f]{64}", analyzer_hash))
        or any(not re.fullmatch(r"[0-9a-f]{64}", value) for value in assembly_hashes.values())
    ):
        raise TableError(f"{path.name}: invalid local-source assembly provenance")
    try:
        started = dt.datetime.fromisoformat(run["startedUtc"].replace("Z", "+00:00"))
        finished = dt.datetime.fromisoformat(run["finishedUtc"].replace("Z", "+00:00"))
    except (AttributeError, ValueError) as exc:
        raise TableError(f"{path.name}: invalid UTC run timestamps") from exc
    if started.tzinfo is None or finished.tzinfo is None or finished < started:
        raise TableError(f"{path.name}: run timestamps must be ordered and include a timezone")
    _validate_group_runs(path, run, started, finished)

    all_measurements = [
        measurement for group in measurements.values() for measurement in group
    ]
    if len(all_measurements) != run["tier1Rows"]:
        raise TableError(
            f"{path.name}: tier1Rows={run['tier1Rows']}, "
            f"but reports contain {len(all_measurements)} rows"
        )
    published_rows = sum(len(measurements[key]) for key in TIER1_REPORTS)
    if published_rows != run["tier1Rows"]:
        raise TableError(
            f"{path.name}: tier1Rows={run['tier1Rows']}, "
            f"but the six documented reports contain {published_rows} rows"
        )
    if run["totalRunCases"] != run["tier1Rows"] + run["additionalDiagnosticCases"]:
        raise TableError(f"{path.name}: report and diagnostic counts do not sum to totalRunCases")
    default_rows = 0
    raw_reader_rows = 0
    for class_name, _ in TIER1_RUN_REPORTS.values():
        csv_path, _ = report_paths(results_dir, class_name)
        with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                if (row.get("Toolchain") or "").strip() != run["toolchain"]:
                    raise TableError(
                        f"{csv_path.name}: unexpected toolchain for {row.get('Method')}"
                    )
                unroll = (row.get("UnrollFactor") or "").strip()
                invocation = (row.get("InvocationCount") or "").strip()
                if unroll == str(run["defaultUnrollFactor"]) and invocation == "Default":
                    default_rows += 1
                elif (
                    unroll == str(run["rawReaderUnrollFactor"])
                    and invocation == str(run["rawReaderInvocationCount"])
                ):
                    raw_reader_rows += 1
                else:
                    raise TableError(
                        f"{csv_path.name}: unclassified job parameters for {row.get('Method')}"
                    )
    if default_rows != run["defaultJobRows"] or raw_reader_rows != run["rawReaderRows"]:
        raise TableError(
            f"{path.name}: job row counts differ from reports "
            f"(default={default_rows}, raw-reader={raw_reader_rows})"
        )
    if run["defaultJobRows"] + run["rawReaderRows"] != run["tier1Rows"]:
        raise TableError(f"{path.name}: job row counts do not sum to tier1Rows")
    if run["failedRows"] != 0:
        raise TableError(f"{path.name}: published Tier-1 snapshot has failed rows")
    as_of_date = started.date().isoformat()
    if started.date() != finished.date():
        as_of_date = f"{as_of_date}–{finished.date().isoformat()}"
    return {**metadata, "asOfDate": as_of_date}


def _build_tier1_run_info(run: dict, lang: str) -> str:
    info = run["run"]
    started = dt.datetime.fromisoformat(info["startedUtc"].replace("Z", "+00:00"))
    finished = dt.datetime.fromisoformat(info["finishedUtc"].replace("Z", "+00:00"))
    if started.date() == finished.date():
        interval = f"{started.strftime('%H:%M:%S')}–{finished.strftime('%H:%M:%S')} UTC"
    else:
        interval = f"{started.strftime('%Y-%m-%d %H:%M:%S')}–{finished.strftime('%Y-%m-%d %H:%M:%S')} UTC"
    local_source_en = (
        f"built from {info['localNextormProjects']} local NextORM project references and "
        + ("the local source-generator analyzer" if info["localSourceGenerator"] else "no local source-generator analyzer")
        + f"; all {info['matchedBuildRuntimeHashRows']} build/runtime hash rows matched those outputs."
    )
    local_source_ru = (
        f"сборка использовала {info['localNextormProjects']} локальных ссылок на проекты NextORM и "
        + ("локальный source-generator analyzer" if info["localSourceGenerator"] else "без локального source-generator analyzer")
        + f"; все {info['matchedBuildRuntimeHashRows']} хеш-строк сборки/runtime совпали с этими выходными файлами."
    )
    if lang == "ru":
        text = (
            f"Снимок на **{run['asOfDate']}** (замеры **{interval}**, по журналу прогона). "
            f"Шесть отчётов ниже содержат {info['tier1Rows']} корректных строк. "
            f"Весь прогон включал {info['totalRunCases']} сценариев, в том числе "
            f"{info['additionalDiagnosticCases']} дополнительных диагностических случаев CSV, "
            "которые здесь не сравниваются между библиотеками. "
            f"Машина: {info['cpu']} ({info['logicalProcessors']} логических / "
            f"{info['physicalCores']} физических ядер), SDK .NET {info['sdk']}, "
            f"runtime {info['runtime']}, BenchmarkDotNet {info['benchmarkDotNet']}. "
            f"{info['defaultJobRows']} строк отчётов сняты с параметрами `Job.Default` "
            f"(UnrollFactor={info['defaultUnrollFactor']}); {info['rawReaderRows']} строк raw-reader — "
            f"со специализированными InvocationCount={info['rawReaderInvocationCount']} и "
            f"UnrollFactor={info['rawReaderUnrollFactor']}. Все строки использовали "
            f"`{info['toolchain']}`. Источник: HEAD `{info['sourceHead']}`; "
            f"{local_source_ru} "
            + (
                "Рабочее дерево исходников содержало незакоммиченные изменения."
                if info["sourceTreeDirty"]
                else "Рабочее дерево исходников было чистым."
            )
        )
    else:
        text = (
            f"Snapshot: **{run['asOfDate']}** (measurements **{interval}**, from the run log). "
            f"The six reports below contain {info['tier1Rows']} valid rows. "
            f"The full run included {info['totalRunCases']} Tier-1 cases, including "
            f"{info['additionalDiagnosticCases']} additional CSV diagnostic cases not compared here. "
            f"Machine: {info['cpu']} ({info['logicalProcessors']} logical / "
            f"{info['physicalCores']} physical cores), .NET SDK {info['sdk']}, runtime "
            f"{info['runtime']}, BenchmarkDotNet {info['benchmarkDotNet']}. "
            f"{info['defaultJobRows']} report rows used `Job.Default` parameters "
            f"(UnrollFactor={info['defaultUnrollFactor']}); {info['rawReaderRows']} raw-reader rows "
            f"used the specialized InvocationCount={info['rawReaderInvocationCount']} and "
            f"UnrollFactor={info['rawReaderUnrollFactor']}. Every row used "
            f"`{info['toolchain']}`. Source: HEAD `{info['sourceHead']}`; "
            f"{local_source_en} "
            + (
                "The source tree was dirty at measurement time."
                if info["sourceTreeDirty"]
                else "The source tree was clean at measurement time."
            )
        )
    return textwrap.fill(
        text,
        width=110,
        break_long_words=False,
        break_on_hyphens=False,
    )


def _build_tier1_caveats(run: dict, lang: str) -> str:
    info = run["run"]
    if lang == "ru":
        text = (
            f"**Как читать результаты tier-1.** `Job.Default` с "
            f"`{info['toolchain']}` — это запуск внутри процесса; различия меньше ~20% считайте "
            f"шумом и проверяйте error, стандартное отклонение и аллокации в полном отчёте. "
            f"В {info['minIterationTimeWarnings']} замерах чтения через reader сработало "
            "предупреждение о слишком коротком времени итерации (рекомендация — не менее 100 мс); "
            f"в {info['multimodalWarnings']} сценариях стриминга — предупреждение о мультимодальном "
            f"распределении. Ошибочных случаев: {info['failedRows']}."
        )
    else:
        text = (
            f"**Reading Tier-1 results.** `Job.Default` with `{info['toolchain']}` runs in process; "
            "treat differences below ~20% as noise and check error, standard deviation, and allocations "
            "in the full report. The raw-reader runs produced "
            f"{info['minIterationTimeWarnings']} warnings for iteration times below the recommended "
            "100 ms; streaming produced "
            f"{info['multimodalWarnings']} multimodal-distribution warnings. Failed cases: "
            f"{info['failedRows']}."
        )
    return textwrap.fill(
        text,
        width=108,
        initial_indent="> ",
        subsequent_indent="> ",
        break_long_words=False,
        break_on_hyphens=False,
    )


def _tier1_table_rows(
    measurements: list[Tier1Measurement],
    methods: tuple[str, ...],
    report: str,
    *,
    reader_allocations: bool = False,
) -> list[Tier1Measurement]:
    rows = _tier1_expected_rows(measurements, methods, report, exact=False)
    displayed = []
    for measurement in rows:
        raw_unit = _BYTES_RE.match(measurement.allocated.strip()).group("unit")
        if reader_allocations:
            allocated = f"{measurement.allocated_bytes / 1024:.2f} KB"
        elif raw_unit == "B":
            allocated = format_bytes(measurement.allocated_bytes, "B")
        else:
            allocated = measurement.allocated
        displayed.append(
            Tier1Measurement(
                **{**measurement.__dict__, "allocated": allocated}
            )
        )
    return displayed


def build_tier1_blocks(results_dir: Path, lang: str) -> dict[str, str]:
    measurements = {
        key: _tier1_measurements(results_dir, key)
        for key in TIER1_RUN_REPORTS
    }
    run = _tier1_run_metadata(results_dir, measurements)
    blocks = {
        "tier1-run-info": _build_tier1_run_info(run, lang),
        "tier1-caveats": _build_tier1_caveats(run, lang),
    }

    for key in ("projection", "aggregates", "paging", "streaming", "csv"):
        _tier1_expected_rows(
            measurements[key],
            TIER1_EXPECTED_METHODS[key],
            TIER1_RUN_REPORTS[key][0],
        )
    json_rows = measurements["write-json"]
    json_methods = TIER1_EXPECTED_METHODS["write-json"]
    for count in ("1000", "10000", "100000"):
        count_rows = [measurement for measurement in json_rows if measurement.row_count == count]
        _tier1_expected_rows(
            count_rows, json_methods, f"SqliteBenchmarkWriteJson ({count} rows)"
        )
    if {measurement.row_count for measurement in json_rows} != {"1000", "10000", "100000"}:
        raise TableError("SqliteBenchmarkWriteJson: expected RowCount 1000, 10000, and 100000")

    def table(key: str, methods: tuple, headers: tuple, report: str, *, category=True, operation=False, ratio=False, reader_allocations=False, row_count=None):
        source_rows = measurements[key]
        if row_count is not None:
            source_rows = [
                measurement for measurement in source_rows if measurement.row_count == row_count
            ]
        selected = _tier1_table_rows(
            source_rows, methods, report, reader_allocations=reader_allocations
        )
        rows = []
        for measurement in selected:
            cells = []
            if operation:
                if measurement.method.endswith("_GroupByCount"):
                    cells.append("GroupBy→Count")
                elif measurement.method.endswith("_Count"):
                    cells.append("Count")
                elif measurement.method.endswith("_Sum"):
                    cells.append("Sum")
                else:
                    raise TableError(f"{report}: cannot classify aggregate {measurement.method}")
            cells.append(measurement.method)
            if category:
                cells.append(_tier1_category_label(measurement.category))
            cells.append(measurement.mean)
            if ratio:
                if not measurement.ratio:
                    raise TableError(f"{report}: missing Ratio for {measurement.method}")
                cells.append(measurement.ratio)
            cells.append(measurement.allocated)
            rows.append(tuple(cells))
        return _tier1_markdown_table(headers, rows)

    arm_headers = (
        ("Arm", "Category", "Mean", "Allocated")
        if lang == "en"
        else ("Арм", "Категория", "Mean", "Allocated")
    )
    aggregate_headers = (
        ("Operation", "Arm", "Category", "Mean", "Allocated")
        if lang == "en"
        else ("Операция", "Арм", "Категория", "Mean", "Allocated")
    )
    blocks["tier1-projection"] = table(
        "projection",
        TIER1_EXPECTED_METHODS["projection"],
        arm_headers,
        "SqliteBenchmarkProjection",
    )
    blocks["tier1-aggregates"] = table(
        "aggregates",
        TIER1_EXPECTED_METHODS["aggregates"],
        aggregate_headers,
        "SqliteBenchmarkAggregates",
        operation=True,
    )
    blocks["tier1-paging"] = table(
        "paging",
        TIER1_EXPECTED_METHODS["paging"],
        arm_headers,
        "SqliteBenchmarkPaging",
    )
    streaming = TIER1_EXPECTED_METHODS["streaming"]
    blocks["tier1-streaming-buffered"] = table(
        "streaming",
        streaming[:7],
        arm_headers,
        "SqliteBenchmarkStreaming buffered DTO",
    )
    blocks["tier1-streaming-unbuffered"] = table(
        "streaming",
        streaming[7:13],
        arm_headers,
        "SqliteBenchmarkStreaming unbuffered DTO",
    )
    blocks["tier1-streaming-readers"] = table(
        "streaming",
        streaming[13:18],
        arm_headers,
        "SqliteBenchmarkStreaming raw readers",
        reader_allocations=True,
    )
    blocks["tier1-streaming-empty-readers"] = table(
        "streaming",
        streaming[18:],
        arm_headers,
        "SqliteBenchmarkStreaming empty readers",
        reader_allocations=True,
    )
    blocks["tier1-write-json"] = table(
        "write-json",
        json_methods,
        (
            ("Method (10,000 rows)", "Mean", "Ratio", "Allocated")
            if lang == "en"
            else ("Метод (10 000 строк)", "Mean", "Ratio", "Allocated")
        ),
        "SqliteBenchmarkWriteJson (10,000 rows)",
        category=False,
        ratio=True,
        row_count="10000",
    )
    blocks["tier1-csv"] = table(
        "csv",
        TIER1_EXPECTED_METHODS["csv"],
        (
            ("Method", "Mean", "Ratio", "Allocated")
            if lang == "en"
            else ("Метод", "Mean", "Ratio", "Allocated")
        ),
        "SqliteBenchmarkCsv",
        category=False,
        ratio=True,
    )
    return blocks


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
    tier1_blocks = (
        build_tier1_blocks(results_dir, target.lang)
        if target.name in ("docs", "docs-ru")
        else {}
    )
    for name in target.blocks:
        if name == "conditions":
            blocks[name] = build_conditions(target, results_dir, date)
        elif name == "e2e":
            blocks[name] = build_e2e_table(
                results_dir,
                target.lang,
                include_features=target.feature_rows,
                code_links=target.code_links,
            )
        elif name == "allocations":
            blocks[name] = build_allocations_table(results_dir, target.lang)
        elif name == "category-a":
            blocks[name] = build_category_a_table(results_dir, target.lang)
        elif name == "category-b":
            blocks[name] = build_category_b_table(results_dir, target.lang)
        elif name in tier1_blocks:
            blocks[name] = tier1_blocks[name]
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
            "override the end-to-end date in the conditions line only "
            "(default: the persisted UTC suite-start date)"
        ),
    )
    parser.add_argument(
        "--record-run-date",
        action="append",
        default=[],
        metavar="CLASS=YYYY-MM-DD",
        help="record one completed suite's UTC start date (repeat for multiple suites)",
    )
    parser.add_argument(
        "--run-evidence",
        default=None,
        help="source path or URL for dates recorded with --record-run-date",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="do not write; exit 1 when a generated block differs from report data or run metadata",
    )
    args = parser.parse_args(argv)

    results_dir = args.results_dir.resolve()
    stale = []
    try:
        if args.record_run_date:
            if args.date is not None or args.check:
                raise TableError("--record-run-date cannot be combined with --date or --check")
            if not args.run_evidence:
                raise TableError("--record-run-date requires --run-evidence")
            run_dates = {}
            for assignment in args.record_run_date:
                class_name, separator, run_date = assignment.partition("=")
                if not separator or not class_name:
                    raise TableError(
                        f"invalid --record-run-date {assignment!r}; expected CLASS=YYYY-MM-DD"
                    )
                if class_name in run_dates:
                    raise TableError(f"duplicate --record-run-date for {class_name}")
                run_dates[class_name] = run_date
            _record_suite_run_dates(results_dir, run_dates, args.run_evidence)
            print(f"recorded run dates for {len(run_dates)} benchmark suite(s)")
            return 0
        if args.run_evidence:
            raise TableError("--run-evidence requires --record-run-date")

        date = _parse_run_date(args.date, "--date") if args.date else report_date(results_dir)
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
                print("benchmark tables are out of date; run tools/benchmarks/update_benchmark_tables.py", file=sys.stderr)
                return 1
        print("benchmark tables are up to date" if not stale else f"updated {len(stale)} file(s)")
        return 0
    except TableError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
