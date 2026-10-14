#!/usr/bin/env python3
"""d07_assess.py -- D-07 assessment over already-captured rv1 evidence (no execution here).

Reads the rv1 baseline/candidate artifacts produced by the C11/C12/C15 plumbing and writes:

  * ``rv1/branch-delta.json``     -- AC-06: line/branch totals, delta and the touched-area mapping
  * ``rv1/perf-comparison.json``  -- AC-11: acceptance perf candidate vs the D-01 baseline

Task-local evidence plumbing only; it runs nothing and edits no production file.
"""
from __future__ import annotations

import datetime as _dt
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
R1 = os.path.join(ROOT, "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1")

# The production types the r=2/r=3 typed PIVOT/UNPIVOT change touches (AC-06 touched-area mapping).
TOUCHED = {
    "NextORM.Core.PivotValues": "nextorm.core (new public marker)",
    "NextORM.Core.NormSqlTranslator": "nextorm.core (marker translation, ColumnName)",
    "NextORM.Core.QueryCommand.QueryPreparer": "nextorm.core (derived pass-through, D-05 n=2)",
    "NextORM.SqlServer.SqlServerEntityBuilderExtensions": "nextorm.sqlserver (typed overloads)",
    "NextORM.SqlServer.PivotProjectionRewriter": "nextorm.sqlserver (new rewriter)",
    "NextORM.Core.PivotExpression": "nextorm.core (legacy PIVOT expression, unchanged oracle)",
    "NextORM.SqlServer.SqlServerPivotRenderer": "nextorm.sqlserver (legacy PIVOT renderer)",
}


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _read(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def _rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def _summary(path):
    data = _read(path)
    return data.get("summary", data)


def _class_index(path):
    """Map class name -> per-class summary from a reportgenerator Summary.json."""
    index = {}
    data = _read(path)
    for assembly in data.get("coverage", {}).get("assemblies", []):
        for cls in assembly.get("classesinassembly", []):
            index[cls.get("name")] = {
                "assembly": assembly.get("name"),
                "line_coverage": cls.get("coverage"),
                "coveredlines": cls.get("coveredlines"),
                "coverablelines": cls.get("coverablelines"),
                "branch_coverage": cls.get("branchcoverage"),
                "coveredbranches": cls.get("coveredbranches"),
                "totalbranches": cls.get("totalbranches"),
            }
    return index


def _cobertura_uncovered(path, suffixes):
    """Map each touched source file (by suffix) to its sorted uncovered line numbers."""
    import xml.etree.ElementTree as ET
    out = {}
    try:
        root = ET.parse(path).getroot()
    except (OSError, ET.ParseError):
        return out
    for cls in root.iter("class"):
        filename = cls.get("filename") or ""
        for suffix in suffixes:
            if filename.endswith(suffix):
                lines = set()
                for line in cls.iter("line"):
                    if line.get("hits") == "0" and line.get("number"):
                        lines.add(int(line.get("number")))
                out.setdefault(suffix, set()).update(lines)
    return {k: sorted(v) for k, v in out.items()}


def branch_delta():
    base_summary = os.path.join(R1, "coverage/baseline/report/Summary.json")
    cand_summary = os.path.join(R1, "coverage/candidate/report/Summary.json")
    cand_xml = os.path.join(R1, "coverage/candidate/coverage.xml")
    base = _summary(base_summary)
    cand = _summary(cand_summary)
    base_classes = _class_index(base_summary)
    cand_classes = _class_index(cand_summary)

    touched = []
    for name, role in TOUCHED.items():
        entry = {
            "class": name,
            "role": role,
            "baseline": base_classes.get(name),
            "candidate": cand_classes.get(name),
        }
        b = entry["baseline"]
        c = entry["candidate"]
        if b and c:
            entry["line_delta"] = round((c["line_coverage"] or 0) - (b["line_coverage"] or 0), 1)
            entry["branch_delta"] = round((c["branch_coverage"] or 0) - (b["branch_coverage"] or 0), 1)
        touched.append(entry)

    thresholds = {"line": 85.0, "branch": 75.0}
    result = {
        "criterion": ["E232-11", "AC-06"],
        "captured": _now(),
        "thresholds": thresholds,
        "baseline": {
            "summary": _rel(base_summary),
            "line_coverage": base.get("linecoverage"),
            "branch_coverage": base.get("branchcoverage"),
            "coveredlines": base.get("coveredlines"),
            "coverablelines": base.get("coverablelines"),
            "coveredbranches": base.get("coveredbranches"),
            "totalbranches": base.get("totalbranches"),
            "classes": base.get("classes"),
        },
        "candidate": {
            "summary": _rel(cand_summary),
            "line_coverage": cand.get("linecoverage"),
            "branch_coverage": cand.get("branchcoverage"),
            "coveredlines": cand.get("coveredlines"),
            "coverablelines": cand.get("coverablelines"),
            "coveredbranches": cand.get("coveredbranches"),
            "totalbranches": cand.get("totalbranches"),
            "classes": cand.get("classes"),
        },
        "delta": {
            "line_coverage": round((cand.get("linecoverage") or 0) - (base.get("linecoverage") or 0), 1),
            "branch_coverage": round((cand.get("branchcoverage") or 0) - (base.get("branchcoverage") or 0), 1),
            "coveredlines": (cand.get("coveredlines", 0) or 0) - (base.get("coveredlines", 0) or 0),
            "coverablelines": (cand.get("coverablelines", 0) or 0) - (base.get("coverablelines", 0) or 0),
            "coveredbranches": (cand.get("coveredbranches", 0) or 0) - (base.get("coveredbranches", 0) or 0),
            "totalbranches": (cand.get("totalbranches", 0) or 0) - (base.get("totalbranches", 0) or 0),
        },
        "touched_area": touched,
        "touched_uncovered_lines": _cobertura_uncovered(cand_xml, [
            "PivotValues.cs", "NormSqlTranslator.cs", "QueryCommand.QueryPreparer.cs",
            "SqlServerEntityBuilderExtensions.cs", "PivotProjectionRewriter.cs",
        ]),
    }
    result["thresholds_met"] = bool(
        (cand.get("linecoverage") or 0) >= thresholds["line"]
        and (cand.get("branchcoverage") or 0) >= thresholds["branch"]
    )
    # "Comparable touched-area coverage does not decrease": reported on BOTH the percentage
    # (may drop when new defensive branches enlarge the denominator) and the absolute covered
    # counts (must not drop). The distinction is surfaced, not resolved, for CHECK.
    result["touched_pct_decreased"] = any(
        (entry.get("line_delta") is not None and entry["line_delta"] < 0)
        or (entry.get("branch_delta") is not None and entry["branch_delta"] < 0)
        for entry in touched
    )
    result["touched_covered_abs_decreased"] = any(
        entry["baseline"] and entry["candidate"] and (
            entry["candidate"]["coveredlines"] < entry["baseline"]["coveredlines"]
            or entry["candidate"]["coveredbranches"] < entry["baseline"]["coveredbranches"]
        )
        for entry in touched
    )
    result["touched_area_decreased"] = result["touched_pct_decreased"]
    with open(os.path.join(R1, "branch-delta.json"), "w", encoding="utf-8", newline="") as handle:
        json.dump(result, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")
    print(json.dumps({"line": result["candidate"]["line_coverage"],
                      "branch": result["candidate"]["branch_coverage"],
                      "delta_line": result["delta"]["line_coverage"],
                      "delta_branch": result["delta"]["branch_coverage"],
                      "thresholds_met": result["thresholds_met"],
                      "touched_area_decreased": result["touched_area_decreased"]}, indent=1))


PROVIDER_TOKENS = [
    ("sqlite", ["Sqlite"]),
    ("sqlserver", ["SqlServer"]),
    ("postgres", ["Postgres"]),
    ("mysql", ["MySql"]),
    ("mariadb", ["MariaDb", "MariaDB"]),
    ("clickhouse", ["ClickHouse"]),
]
PROVIDER_UNAVAILABLE = re.compile(r"(SQLite|SQL Server|PostgreSQL|MySQL|MariaDB|ClickHouse) is not available")


def _parse_test_summary(text):
    summary = {}
    for key in ("total", "failed", "succeeded", "skipped"):
        match = re.search(rf"^\s*{key}:\s*(\d+)\s*$", text, re.MULTILINE)
        if match:
            summary[key] = int(match.group(1))
    return summary or None


def _parse_inprocess_summary(text):
    match = re.search(r"Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)", text)
    if not match:
        return None
    total, errors, failed, skipped, not_run = (int(g) for g in match.groups())
    return {"total": total, "errors": errors, "failed": failed, "skipped": skipped,
            "not_run": not_run, "succeeded": total - errors - failed - skipped - not_run}


def _skip_reasons(text):
    lines = text.splitlines()
    reasons = []
    for idx, line in enumerate(lines):
        if line.strip().endswith("[SKIP]"):
            reasons.append(lines[idx + 1].strip() if idx + 1 < len(lines) else "")
    return reasons


def boundary_providers():
    """Parse the C11 boundary logs into a per-suite + per-provider summary (E232-12)."""
    boundary = _read(os.path.join(R1, "boundary.json"))
    suites = {}
    for project in ("nextorm.core.tests", "nextorm.sqlite.tests", "nextorm.postgres.tests",
                    "nextorm.sqlserver.tests", "nextorm.mysql.tests", "nextorm.mariadb.tests",
                    "nextorm.clickhouse.tests"):
        out = os.path.join(R1, "logs", "boundary-" + project + ".out")
        text = open(out, encoding="utf-8", errors="replace").read()
        suites[project] = {
            "exit": boundary["exits"].get("tests/" + project),
            "log": _rel(out),
            "summary": _parse_test_summary(text),
        }
    integ_out = os.path.join(R1, "logs", "boundary-integration.out")
    integ_text = open(integ_out, encoding="utf-8", errors="replace").read()
    reasons = _skip_reasons(integ_text)
    provider_skips = sorted({r for r in reasons if PROVIDER_UNAVAILABLE.search(r)})
    result = {
        "criterion": ["E232-12", "AC-03", "AC-09", "AC-11"],
        "captured": _now(),
        "docker_host": boundary.get("docker_host"),
        "suites": suites,
        "integration": {
            "exit": boundary["exits"].get("integration"),
            "log": _rel(integ_out),
            "summary": _parse_inprocess_summary(integ_text),
            "capability_skips": len(reasons),
            "provider_skips": provider_skips,
        },
        "six_providers_executed": not provider_skips,
        "provider_inventory_source": "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/regression/inventory.json#per_provider",
    }
    with open(os.path.join(R1, "boundary-summary.json"), "w", encoding="utf-8", newline="") as handle:
        json.dump(result, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")
    print(json.dumps({"suites": {k: v["summary"] for k, v in suites.items()},
                      "integration": result["integration"]}, indent=1))


def _ratio(value):
    try:
        return float(str(value).replace(",", ""))
    except (TypeError, ValueError):
        return None


def _row_cells(rows, method):
    for row in rows:
        cells = [c.strip() for c in row.strip().strip("|").split("|")]
        if cells and cells[0] == method:
            return cells
    return None


def _alloc_ratio(rows):
    """allocated/prepared ratio = last numeric column of Cached_ToList vs Prepared_ToList."""
    cached = _row_cells(rows, "Cached_ToList")
    prepared = _row_cells(rows, "Prepared_ToList")
    if not cached or not prepared:
        return None
    try:
        return round(_ratio(cached[-1]) / _ratio(prepared[-1]), 2)
    except (TypeError, ValueError, ZeroDivisionError):
        return None


def perf_comparison():
    base = _read(os.path.join(R1, "perf/acceptance-baseline.json"))
    cand = _read(os.path.join(R1, "perf/acceptance-candidate.json"))

    def block(src):
        return {
            "mode": src.get("mode"),
            "exit": src.get("exit"),
            "cases_reported": src.get("cases_reported"),
            "global_total_time": src.get("global_total_time"),
            "cached_prepared_ratio": src.get("cached_prepared_ratio"),
            "allocation_ratio": src.get("allocation_ratio"),
            "stdout": src.get("stdout"),
        }

    def pct(v0, v1):
        try:
            a = float(str(v0))
            b = float(str(v1))
            if a == 0:
                return None
            return round((b - a) / a * 100.0, 1)
        except (TypeError, ValueError):
            return None

    bc = (base.get("cached_prepared_ratio") or {}).get("cached_over_prepared")
    cc = (cand.get("cached_prepared_ratio") or {}).get("cached_over_prepared")
    ba = _alloc_ratio(base.get("summary_rows") or [])
    ca = _alloc_ratio(cand.get("summary_rows") or [])
    result = {
        "criterion": ["E232-15", "AC-11"],
        "captured": _now(),
        "baseline": block(base),
        "candidate": block(cand),
        "cached_prepared_ratio_delta_pct": pct(bc, cc),
        "allocation_ratio_baseline": ba,
        "allocation_ratio_candidate": ca,
        "allocation_ratio_delta_pct": pct(ba, ca),
        "cases": {"baseline": base.get("cases_reported"), "candidate": cand.get("cases_reported")},
        "reference_baseline": base.get("reference_baseline"),
        "noise_threshold_pct": 20.0,
        "env_note": "NuGetAudit=false exported for the acceptance run (pre-existing NU1902/NU1903 advisory promoted by TreatWarningsAsErrors); argv unchanged.",
        "material_regression": bool(
            (pct(bc, cc) is not None and pct(bc, cc) >= 20.0)
            or (pct(ba, ca) is not None and pct(ba, ca) >= 20.0)
        ),
    }
    with open(os.path.join(R1, "perf-comparison.json"), "w", encoding="utf-8", newline="") as handle:
        json.dump(result, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")
    print(json.dumps({"baseline_ratio": bc, "candidate_ratio": cc,
                      "delta_pct": result["cached_prepared_ratio_delta_pct"],
                      "material_regression": result["material_regression"]}, indent=1))


def footprint_check():
    """D-07 (AC-12/E232-16): foreign-byte preservation, no foreign staging, CRLF, clean src/tests."""
    import subprocess

    def git(*args):
        return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=False).stdout

    dirty = [line for line in git("status", "--porcelain=v1").splitlines() if line.strip()]
    staged = [line for line in git("diff", "--cached", "--name-only").splitlines() if line.strip()]
    src_tests = [line for line in git("status", "--porcelain", "--", "src", "tests").splitlines() if line.strip()]
    baseline = _read(os.path.join(R1, "foreign-baseline.json"))
    fp = _read(os.path.join(R1, "footprint.json"))
    base_paths = {e["path"] for e in baseline["entries"]}
    cur_paths = {e["path"] for e in fp["foreign_entries"]}
    bdn_prefix = "benchmarks/BenchmarkDotNet.Artifacts/"
    added = sorted(cur_paths - base_paths)
    removed = sorted(base_paths - cur_paths)
    checks = {
        "baseline_foreign_count": baseline["count"],
        "current_foreign_count": fp["foreign_count"],
        "added_foreign": added,
        "added_foreign_are_generated_benchmark_outputs": all(p.startswith(bdn_prefix) for p in added),
        "removed_foreign": removed,
        "removed_foreign_explained": all(p.endswith("AdventureWorksQueries.cs") for p in removed),
        "foreign_entries_staged": [p for p in staged if p in (base_paths | cur_paths)],
        "any_staged": staged,
        "src_tests_dirty": src_tests,
        "edited_text_files_crlf": {},
    }
    for rel in ("docs/specs/status/D232-sqlserver-pivot-unpivot-1.md",
                "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py",
                "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/d07_assess.py"):
        path = os.path.join(ROOT, rel)
        if os.path.exists(path):
            data = open(path, "rb").read()
            checks["edited_text_files_crlf"][rel] = (b"\r\n" in data and b"\n" not in data.replace(b"\r\n", b""))
    ok = (not checks["foreign_entries_staged"] and not src_tests
          and checks["added_foreign_are_generated_benchmark_outputs"]
          and checks["removed_foreign_explained"]
          and all(checks["edited_text_files_crlf"].values()))
    result = {"criterion": ["E232-16", "AC-12"], "captured": _now(), "exit": 0 if ok else 2, "checks": checks}
    with open(os.path.join(R1, "d07-footprint.json"), "w", encoding="utf-8", newline="") as handle:
        json.dump(result, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")
    print(json.dumps({"ok": ok, "added": len(added), "removed": removed,
                      "staged": staged, "src_tests_dirty": len(src_tests)}, indent=1))


if __name__ == "__main__":
    boundary_providers()
    branch_delta()
    perf_comparison()
    footprint_check()
