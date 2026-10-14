#!/usr/bin/env python3
"""D224 (#224) criterion->test mapping.

Runs the frozen scope's *filtered* selectors that cover each open variant/guard
row (R1 raw goldens, R3 param-mode/projections/shared-command cache, R7
dialect/statement guards) with `dotnet test <project> -c Debug --no-build
--filter <selector>`, capturing per-run exit code, selected_count and the exact
selected test FQNs, and writes artifacts/pdca/D224/rv1/mapping.json.

No product/test source is touched; only filtered execution + evidence capture.
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
A = ROOT / "artifacts/pdca/D224/rv1"
LOGDIR = A / "mapping"
MAX_TESTS = 80

T = "tests/nextorm.%s.tests"

RUNS = [
    # --- R1: SELECT/join raw goldens, one provider each -------------------
    ("R1", "SELECT 0/1/many-join + nested raw goldens: raw SELECT text starts each FROM/JOIN on a new line",
     T % "sqlite", "FullyQualifiedName~SqlGenerationTests"),
    ("R1", "same, PostgreSQL", T % "postgres", "FullyQualifiedName~SqlGenerationTests"),
    ("R1", "same, SQL Server", T % "sqlserver", "FullyQualifiedName~SqlGenerationTests"),
    ("R1", "same, MySQL", T % "mysql", "FullyQualifiedName~SqlGenerationTests"),
    ("R1", "same, MariaDB", T % "mariadb", "FullyQualifiedName~SqlGenerationTests"),
    ("R1", "same, ClickHouse", T % "clickhouse", "FullyQualifiedName~SqlGenerationTests"),
    # --- R3: text vs parameter-only mode ---------------------------------
    ("R3", "text mode vs parameter-only mode (ParamMode render root / parameter binding)",
     T % "sqlite", "FullyQualifiedName~CorrelatedParameterSqlGenerationTests"),
    ("R3", "same, PostgreSQL", T % "postgres", "FullyQualifiedName~CorrelatedParameterSqlGenerationTests"),
    ("R3", "same, SQL Server", T % "sqlserver", "FullyQualifiedName~CorrelatedParameterSqlGenerationTests"),
    ("R3", "same, ClickHouse", T % "clickhouse", "FullyQualifiedName~CorrelatedParameterSqlGenerationTests"),
    # --- R3: nullable/value/reference projections ------------------------
    ("R3", "nullable/value/reference projections keep row shape",
     T % "core", "FullyQualifiedName~RootProjectionTests"),
    ("R3", "same (in-memory provider)", T % "core", "FullyQualifiedName~EntityItemProjectionInMemoryTests"),
    ("R3", "same (SQLite provider)", T % "sqlite", "FullyQualifiedName~EntityItemProjectionTests"),
    # --- R3: shared QueryCommand / Any/Count/All cache, sticky _dontCache -
    ("R3", "shared QueryCommand cache flag unchanged (storeInCache:false never mutates sticky QueryCommand.Cache)",
     T % "core", "FullyQualifiedName~QueryCacheControlsTests"),
    ("R3", "shared Any/Count/All GetAnyCommand identity + cached path",
     T % "sqlite", "FullyQualifiedName~CachedPathCharacterizationTests"),
    ("R3", "eager execution must not poison the context-shared AnyCommand",
     T % "core", "FullyQualifiedName~EagerLoadingModeTests"),
    # --- R7: dialect keyword / JOIN / APPLY guards -----------------------
    ("R7", "ClickHouse JOIN keyword contract (GLOBAL/strictness) unchanged",
     T % "clickhouse", "FullyQualifiedName~JoinIntoSqlGenerationTests"),
    ("R7", "ClickHouse dialect keyword rendering unchanged",
     T % "clickhouse", "FullyQualifiedName~ClickHouseDialectTests"),
    ("R7", "SQL Server JOIN/APPLY + dialect keyword rendering unchanged",
     T % "sqlserver", "FullyQualifiedName~SqlServerDialectTests"),
    # --- R7: batch / INSERT / CTAS / MERGE layout + LF -------------------
    ("R7", "batch statement separation layout + LF",
     T % "sqlite", "FullyQualifiedName~BatchSqlGenerationTests"),
    ("R7", "INSERT (incl. INSERT ... SELECT) layout + LF",
     T % "sqlite", "FullyQualifiedName~InsertSqlGenerationTests"),
    ("R7", "CREATE TABLE AS SELECT recursive layout + LF",
     T % "sqlite", "FullyQualifiedName~CreateTableAsSqlGenerationTests"),
    ("R7", "MERGE layout + LF",
     T % "postgres", "FullyQualifiedName~MergeSqlGenerationTests"),
    # --- R7: standalone DELETE / UPDATE-join preservation ----------------
    ("R7", "standalone DELETE ... USING/FROM layout preserved",
     T % "sqlite", "FullyQualifiedName~DeleteSqlGenerationTests"),
    ("R7", "standalone UPDATE-join clause layout preserved (no SELECT clause placement imposed)",
     T % "sqlite", "FullyQualifiedName~UpdateJoinSqlGenerationTests"),
]

COUNT = {
    "total": re.compile(r"total:\s*(\d+)"),
    "failed": re.compile(r"failed:\s*(\d+)"),
    "succeeded": re.compile(r"succeeded:\s*(\d+)"),
    "skipped": re.compile(r"skipped:\s*(\d+)"),
}
LISTED = re.compile(r"^\s{2}([A-Za-z_][\w.<>+`,]*\.[A-Za-z_]\w*)\s*$")


def run_one(row, criterion, project, selector, idx):
    slug = re.sub(r"[^\w]+", "-", f"{row}-{project.split('.')[-2]}-{selector.replace('FullyQualifiedName~','')}")
    log = LOGDIR / f"{idx:02d}-{slug}.log"
    cmd = ["dotnet", "test", project, "-c", "Debug", "--no-build", "--filter", selector]
    proc = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
    log.write_text(proc.stdout + proc.stderr, encoding="utf-8")
    text = proc.stdout + proc.stderr
    counts = {k: (int(rx.search(text).group(1)) if rx.search(text) else 0) for k, rx in COUNT.items()}
    lproc = subprocess.run(cmd + ["--list-tests"], cwd=ROOT, capture_output=True, text=True)
    tests = sorted(set(LISTED.findall(lproc.stdout)))
    return {
        "row": row,
        "criterion": criterion,
        "project": project,
        "command": cmd,
        "exit_code": proc.returncode,
        "selected_count": counts["total"],
        "failed": counts["failed"],
        "succeeded": counts["succeeded"],
        "skipped": counts["skipped"],
        "selected_tests": tests[:MAX_TESTS],
        "selected_tests_total": len(tests),
        "selected_tests_truncated": len(tests) > MAX_TESTS,
        "list_tests_exit": lproc.returncode,
        "log": str(log.relative_to(ROOT)),
    }


def main():
    LOGDIR.mkdir(parents=True, exist_ok=True)
    runs = []
    fails = []
    for idx, (row, criterion, project, selector) in enumerate(RUNS, 1):
        r = run_one(row, criterion, project, selector, idx)
        print(f"{r['row']} {r['project']:38s} {r['command'][-1]:<62s} exit={r['exit_code']} selected={r['selected_count']} failed={r['failed']}")
        runs.append(r)
        if r["exit_code"] != 0 or r["selected_count"] < 1:
            fails.append(r["log"])
    doc = {
        "task": "T224",
        "unit": "D224-sql-from-join-formatting",
        "purpose": "criterion->test mapping for the CHECK re-gather: filtered (--no-build) runs naming the exact tests that cover each open variant/guard row",
        "build": {
            "command": ["dotnet", "build", "nextorm.slnx", "-c", "Debug"],
            "exit_code": 0,
            "value": "Build succeeded; 0 Warning(s); 0 Error(s) (artifacts/pdca/D224/rv1/E01-build.log)",
        },
        "runs": runs,
        "failures": fails,
        "notes": [
            "All test commands were run with --no-build on the boundary build (E01).",
            "R7 batch/INSERT/CTAS/MERGE layout and standalone DELETE/UPDATE-join preservation are additionally covered as a superset by every R1 `FullyQualifiedName~SqlGenerationTests` run, whose selected set includes <Batch|Insert|Delete|Update(Join)|Merge|CreateTableAs|JoinInto>SqlGenerationTests.",
            "R7 SQL Server APPLY goldens are inside the R1 SQL-Server `FullyQualifiedName~SqlGenerationTests` run (CrossApply_/OuterApply_*); the SqlServerDialectTests run covers the dialect keyword/JOIN rendering.",
        ],
    }
    (A / "mapping.json").write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"mapping.json written: {len(runs)} runs, {len(fails)} failing")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
