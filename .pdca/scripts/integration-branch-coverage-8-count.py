#!/usr/bin/env python3
"""Cycle-8 counted-model reproduction.

The cycle-7 counted model of record is defined in
`.pdca/integration-branch-coverage-7/rv2/results/coverage-delta.md` section 4: the literal
reportgenerator Summary totals minus (a) the #247 constant already excluded from the pinned
cycle-6 baseline, (b) the cycle-7 protected-file delta, (c) the concurrent #275 delta; the
counted denominator is pinned at 38317 branches / 59713 statements and the method denominator
at 8191. It is NOT a raw-cobertura number: the cobertura is recomputed here only to record the
raw reference model (with `NextORM.Core.InMemory*` excluded) and to prove the archive is the
cycle-7 final capture.

Deterministic over the archived `coverage/baseline-integration.cobertura.xml` and
`coverage/baseline-Summary.txt` in the cycle-8 rv1 tree.

Usage:
  python3 .pdca/scripts/integration-branch-coverage-8-count.py \
      --cobertura .pdca/integration-branch-coverage-8/rv1/coverage/baseline-integration.cobertura.xml \
      --summary   .pdca/integration-branch-coverage-8/rv1/coverage/baseline-Summary.txt
"""

import argparse
import re
import sys
import xml.etree.ElementTree as ET

INMEM = "NextORM.Core.InMemory"

# Documented constants (coverage-delta.md sections 3-4).
PINNED_BRANCH_TOTAL = 38317
PINNED_LINE_TOTAL = 59713
PINNED_METHOD_TOTAL = 8191
T247_BRANCH = 47
T247_LINE = 154
PROTECTED_BRANCH = 12
PROTECTED_LINE = 18
CONCURRENT_275_BRANCH = 6
CONCURRENT_275_LINE = 10

EXPECTED = {
    "literal_branch_covered": 27351,
    "literal_branch_total": 38407,
    "literal_line_covered": 47287,
    "literal_line_total": 59949,
    "literal_method_total": 8219,
    "counted_branch_covered": 27286,
    "counted_line_covered": 47105,
    "raw_branch_covered": 19268,
    "raw_branch_total": 27814,
    "raw_line_covered": 33945,
    "raw_line_total": 44242,
    "raw_classes": 800,
}


def cond(text):
    m = re.search(r"\((\d+)/(\d+)\)", text or "")
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)


def raw_model(path):
    root = ET.parse(path).getroot()
    bc = bt = lc = lt = 0
    classes = 0
    for pkg in root.iter("package"):
        for cls in pkg.findall("./classes/class"):
            if (cls.get("name") or "").startswith(INMEM):
                continue
            classes += 1
            for ln in cls.findall("./lines/line"):
                lt += 1
                if int(ln.get("hits") or 0) > 0:
                    lc += 1
                if ln.get("branch") == "True":
                    c, t = cond(ln.get("condition-coverage"))
                    bc += c
                    bt += t
    return {"classes": classes, "bc": bc, "bt": bt, "lc": lc, "lt": lt}


def summary_model(path):
    text = open(path, "r", encoding="utf-8-sig").read()
    def grab(pattern):
        m = re.search(pattern, text)
        if not m:
            raise SystemExit(f"summary: pattern not found: {pattern}")
        return int(m.group(1))
    return {
        "branch_covered": grab(r"Covered branches:\s*(\d+)"),
        "branch_total": grab(r"Total branches:\s*(\d+)"),
        "line_covered": grab(r"Covered lines:\s*(\d+)"),
        "line_total": grab(r"Coverable lines:\s*(\d+)"),
        "method_total": grab(r"Total methods:\s*(\d+)"),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cobertura", required=True)
    ap.add_argument("--summary", required=True)
    args = ap.parse_args()

    raw = raw_model(args.cobertura)
    lit = summary_model(args.summary)

    counted_branch = lit["branch_covered"] - T247_BRANCH - PROTECTED_BRANCH - CONCURRENT_275_BRANCH
    counted_line = lit["line_covered"] - T247_LINE - PROTECTED_LINE - CONCURRENT_275_LINE

    observed = {
        "literal_branch_covered": lit["branch_covered"],
        "literal_branch_total": lit["branch_total"],
        "literal_line_covered": lit["line_covered"],
        "literal_line_total": lit["line_total"],
        "literal_method_total": lit["method_total"],
        "counted_branch_covered": counted_branch,
        "counted_line_covered": counted_line,
        "raw_branch_covered": raw["bc"],
        "raw_branch_total": raw["bt"],
        "raw_line_covered": raw["lc"],
        "raw_line_total": raw["lt"],
        "raw_classes": raw["classes"],
    }

    print("cycle-8 counted model reproduction")
    print(f"  cobertura: {args.cobertura}")
    print(f"  summary:   {args.summary}")
    print()
    print("literal report model (Summary.txt, InMemory excluded by reportgenerator classfilters):")
    print(f"  branch  {lit['branch_covered']} / {lit['branch_total']}")
    print(f"  line    {lit['line_covered']} / {lit['line_total']}")
    print(f"  methods total {lit['method_total']}")
    print()
    print("raw cobertura reference (classes not starting with NextORM.Core.InMemory):")
    print(f"  classes {raw['classes']}")
    print(f"  branch  {raw['bc']} / {raw['bt']}")
    print(f"  line    {raw['lc']} / {raw['lt']}")
    print()
    print("documented subtraction (coverage-delta.md section 4):")
    print(f"  #247 constant          branch -{T247_BRANCH}  line -{T247_LINE}")
    print(f"  protected 5 files      branch -{PROTECTED_BRANCH}  line -{PROTECTED_LINE}")
    print(f"  concurrent #275        branch -{CONCURRENT_275_BRANCH}  line -{CONCURRENT_275_LINE}")
    print()
    print("counted absolute (pinned denominators):")
    print(f"  branch {counted_branch} / {PINNED_BRANCH_TOTAL} = {100.0*counted_branch/PINNED_BRANCH_TOTAL:.3f} %")
    print(f"  line   {counted_line} / {PINNED_LINE_TOTAL} = {100.0*counted_line/PINNED_LINE_TOTAL:.3f} %")
    print(f"  methods denominator {PINNED_METHOD_TOTAL}")
    print()

    mismatches = [k for k, v in EXPECTED.items() if observed.get(k) != v]
    if mismatches:
        for k in mismatches:
            print(f"MISMATCH {k}: expected {EXPECTED[k]}, observed {observed.get(k)}")
        print("VERDICT: FAIL (reproduction differs from the documented cycle-7 model)")
        return 1

    print("VERDICT: PASS (reproduces 27286/38317 branch, 47105/59713 line, methods 8191)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
