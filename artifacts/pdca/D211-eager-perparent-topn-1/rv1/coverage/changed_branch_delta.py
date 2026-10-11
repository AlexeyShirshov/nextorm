#!/usr/bin/env python3
"""Changed-line / changed-branch delta for T211.

Uses `git diff -U0 HEAD` on the T211 production files (uncommitted working-tree
changes) to obtain changed line ranges, then reads the dotnet-coverage cobertura
report to report line hits and branch condition coverage for those lines only.
Uncovered / partially covered changed lines are listed explicitly.
"""
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path("/home/alex/sources/nextorm")
ART = ROOT / "artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage"
_argv = sys.argv[1:]
if _argv:
    OUT = Path(_argv[-1])
    COVERAGE_FILES = [Path(a) for a in _argv[:-1]]
else:
    OUT = ART / "branch-delta.json"
    COVERAGE_FILES = [ART / "coverage.xml"]

# T211 production files = uncommitted src/ modifications (working tree vs HEAD).
status = subprocess.run(
    ["git", "-C", str(ROOT), "status", "--porcelain", "--", "src/"],
    capture_output=True, text=True, check=True).stdout
files = []
for line in status.splitlines():
    st, _, path = line.partition(" ")
    path = path.strip()
    if st.strip() in ("M", "A") and path.endswith(".cs"):
        files.append(path)

# Fall back to explicit T211 file list if nothing uncommitted (defensive).
if not files:
    files = [
        "src/nextorm.core/Builders/EntityBuilder.cs",
        "src/nextorm.core/Builders/EntityBuilderEagerLoading.cs",
        "src/nextorm.core/Builders/EntityBuilderExtensions.cs",
        "src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs",
        "src/nextorm.core/DataContext/SqlBuilder.cs",
        "src/nextorm.core/Expressions/JoinExpression.cs",
        "src/nextorm.core/Query/QueryCommand.cs",
        "src/nextorm.core/Query/QueryCommand.Clone.cs",
        "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
        "src/nextorm.core/Query/QueryPlanEqualityComparer.cs",
    ]

HUNK = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")
changed = {}  # relpath -> set of line numbers
for rel in files:
    diff = subprocess.run(
        ["git", "-C", str(ROOT), "diff", "-U0", "HEAD", "--", rel],
        capture_output=True, text=True, check=True).stdout
    lines = set()
    for dl in diff.splitlines():
        m = HUNK.match(dl)
        if m:
            start = int(m.group(1))
            count = int(m.group(2)) if m.group(2) is not None else 1
            lines.update(range(start, start + count))
    if lines:
        changed[rel] = lines

# Parse coverage per file, unioning several runs (max hits / max branch coverage).
cover = {}  # abs filename -> {line: (hits, branch, cond_pct)}
for cov_path in COVERAGE_FILES:
    root = ET.parse(cov_path).getroot()
    for cls in root.iter("class"):
        fn = cls.get("filename")
        cov = cover.setdefault(fn, {})
        for ln in cls.findall("./lines/line"):
            num = int(ln.get("number"))
            hits = int(ln.get("hits"))
            is_branch = ln.get("branch") == "True"
            cond = None
            cc = ln.get("condition-coverage")
            if cc:
                mm = re.search(r"\((\d+)/(\d+)\)", cc)
                if mm:
                    cond = (int(mm.group(1)), int(mm.group(2)))
            prev = cov.get(num)
            if prev is None:
                cov[num] = (hits, is_branch, cond)
            else:
                ph, pb, pc = prev
                if pc is None:
                    bc = cond
                elif cond is None:
                    bc = pc
                else:
                    bc = (max(pc[0], cond[0]), max(pc[1], cond[1]))
                cov[num] = (max(ph, hits), pb or is_branch, bc)

report = {}
tot_changed = 0
tot_changed_covered = 0
tot_branch = 0
tot_branch_covered = 0
uncovered_lines = []
uncovered_branches = []
for rel, lines in sorted(changed.items()):
    absf = str(ROOT / rel)
    info = cover.get(absf)
    entry = {"changed_lines": sorted(lines), "line_total": 0,
             "line_covered": 0, "branch_conditions": 0,
             "branch_conditions_covered": 0, "uncovered_lines": [],
             "partial_or_missing_branches": []}
    if info is None:
        entry["note"] = "file not present in coverage report (assembly not covered)"
        report[rel] = entry
        continue
    for n in sorted(lines):
        if n not in info:
            continue
        hits, is_branch, cond = info[n]
        entry["line_total"] += 1
        tot_changed += 1
        if hits > 0:
            entry["line_covered"] += 1
            tot_changed_covered += 1
        else:
            entry["uncovered_lines"].append(n)
            uncovered_lines.append(f"{rel}:{n}")
        if is_branch and cond is not None:
            cov, total = cond
            entry["branch_conditions"] += total
            entry["branch_conditions_covered"] += cov
            tot_branch += total
            tot_branch_covered += cov
            if cov < total:
                entry["partial_or_missing_branches"].append(
                    {"line": n, "covered": cov, "total": total})
                uncovered_branches.append(f"{rel}:{n} ({cov}/{total})")
    report[rel] = entry

summary = {
    "tree": "working tree (uncommitted T211 changes) vs HEAD "
            f"{subprocess.run(['git','-C',str(ROOT),'rev-parse','--short','HEAD'],capture_output=True,text=True).stdout.strip()}",
    "files": report,
    "changed_line_coverage": {
        "covered": tot_changed_covered, "total": tot_changed},
    "changed_branch_condition_coverage": {
        "covered": tot_branch_covered, "total": tot_branch},
    "uncovered_changed_lines": uncovered_lines,
    "uncovered_or_partial_changed_branches": uncovered_branches,
}
OUT.write_text(json.dumps(summary, indent=2), encoding="utf-8")

print(f"T211 changed files with changes: {len(report)}")
print(f"changed lines covered: {tot_changed_covered}/{tot_changed}")
print(f"changed branch conditions covered: {tot_branch_covered}/{tot_branch}")
print(f"uncovered changed lines ({len(uncovered_lines)}):")
for x in uncovered_lines:
    print("  " + x)
print(f"uncovered/partial changed branches ({len(uncovered_branches)}):")
for x in uncovered_branches:
    print("  " + x)
print(f"-> {OUT}")
