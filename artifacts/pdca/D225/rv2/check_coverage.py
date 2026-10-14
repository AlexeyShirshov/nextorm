#!/usr/bin/env python3
"""check_coverage.py - enforce the coverage thresholds on a reportgenerator Summary.txt.

Usage: check_coverage.py <Summary.txt> --line 85 --branch 75

Reads the overall line/branch percentages from the first coverage table of the
reportgenerator TextSummary and fails when either is below its threshold.
Exit 0 = thresholds met; 2 = below threshold; 1 = usage / unreadable input.
"""
from __future__ import annotations

import argparse
import re
import sys

LINE_RE = re.compile(r"Line coverage:?\**\s*\|?\s*\**\s*([0-9]+(?:\.[0-9]+)?)%")
BRANCH_RE = re.compile(r"Branch coverage:?\**\s*\|?\s*\**\s*([0-9]+(?:\.[0-9]+)?)%")


def first_match(pattern: re.Pattern, text: str) -> float | None:
    match = pattern.search(text)
    return float(match.group(1)) if match else None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("summary")
    parser.add_argument("--line", type=float, required=True)
    parser.add_argument("--branch", type=float, required=True)
    args = parser.parse_args()

    try:
        with open(args.summary, "r", encoding="utf-8-sig") as handle:
            text = handle.read()
    except OSError as exc:
        print("ERROR: cannot read summary: %s" % exc, file=sys.stderr)
        return 1

    line = first_match(LINE_RE, text)
    branch = first_match(BRANCH_RE, text)
    if line is None or branch is None:
        print("ERROR: could not parse line/branch coverage from %s" % args.summary, file=sys.stderr)
        return 1

    failures = []
    if line < args.line:
        failures.append("line coverage %.2f%% < %.2f%%" % (line, args.line))
    if branch < args.branch:
        failures.append("branch coverage %.2f%% < %.2f%%" % (branch, args.branch))

    print("check_coverage: line %.2f%% (>=%.2f), branch %.2f%% (>=%.2f)" % (line, args.line, branch, args.branch))
    for failure in failures:
        print("FAIL: %s" % failure)
    return 2 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
