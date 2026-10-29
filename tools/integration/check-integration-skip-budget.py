#!/usr/bin/env python3
"""Fail when the integration-suite skip set drifts from the pinned budget.

The suite is intentionally capability-driven, so a bounded number of skips is legitimate and
reviewed. This gate makes every *new* skip (or a silently disappearing one) a build failure: the
budget is a snapshot of the reasons and their counts, checked into the repository.

Usage:
    python3 tools/integration/check-integration-skip-budget.py --log integration-run.log
    python3 tools/integration/check-integration-skip-budget.py --log integration-run.log --update

The log is the stdout of:
    DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- \
        -noColor -notrait "Category=Diagnostic"

Exit codes: 0 - budget matched; 1 - drift or malformed log.
"""

from __future__ import annotations

import argparse
import collections
import json
import pathlib
import re
import sys

DEFAULT_BUDGET = pathlib.Path("tests/nextorm.integration.tests/skip-budget.json")
SKIP_LINE = re.compile(r"^\s+(\S+)\s+\[SKIP\]\s*$")
SUMMARY = re.compile(r"Skipped:\s*(\d+)")

# Opt-in harnesses, excluded from the canonical run with `-notrait "Category=Diagnostic"`. When the
# suite is run without that filter they still skip, and they must not count as budget drift.
DIAGNOSTIC_REASONS = {
    "Set NEXTORM_LOB_PROBE=1 to run the LOB capability probe.",
    "Set NEXTORM_LOB_PERF=1 to run the LOB allocation harness.",
    "D243_ATTRIBUTION_REPORT is not set; the stage attribution probe is diagnostic-only.",
}


def parse_log(text: str) -> tuple[collections.Counter[str], int | None, int]:
    lines = text.splitlines()
    reasons: collections.Counter[str] = collections.Counter()
    skipped_in_summary: int | None = None
    ignored = 0

    for index, line in enumerate(lines):
        match = SUMMARY.search(line)
        if match:
            skipped_in_summary = int(match.group(1))

        if SKIP_LINE.match(line):
            reason = lines[index + 1].strip() if index + 1 < len(lines) else ""
            if reason in DIAGNOSTIC_REASONS:
                ignored += 1
                continue
            reasons[reason] += 1

    return reasons, skipped_in_summary, ignored


def _ignored_note(ignored: int) -> str:
    return f" ({ignored} diagnostic skips ignored)" if ignored else ""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--log", required=True, type=pathlib.Path)
    parser.add_argument("--budget", default=DEFAULT_BUDGET, type=pathlib.Path)
    parser.add_argument("--update", action="store_true", help="rewrite the budget from the log")
    args = parser.parse_args()

    reasons, summary, ignored_diagnostics = parse_log(
        args.log.read_text(encoding="utf-8", errors="replace"))
    parsed = sum(reasons.values())

    if summary is None:
        print(f"FAIL: no 'Skipped: N' summary in {args.log}", file=sys.stderr)
        return 1
    if summary != parsed + ignored_diagnostics:
        print(
            f"FAIL: summary reports {summary} skips but {parsed + ignored_diagnostics} [SKIP] entries were parsed; "
            "the log is incomplete or truncated",
            file=sys.stderr,
        )
        return 1

    payload = {
        "totalSkipped": parsed,
        "reasons": dict(sorted(reasons.items())),
    }

    if args.update:
        args.budget.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"updated {args.budget}: {parsed} skips" + _ignored_note(ignored_diagnostics))
        return 0

    if not args.budget.exists():
        print(f"FAIL: no skip budget at {args.budget} (run once with --update)", file=sys.stderr)
        return 1

    budget = json.loads(args.budget.read_text(encoding="utf-8"))
    expected = collections.Counter(budget.get("reasons", {}))
    expected_total = int(budget.get("totalSkipped", sum(expected.values())))

    added = reasons - expected
    removed = expected - reasons

    if not added and not removed and parsed == expected_total:
        print(f"OK: {parsed} skips match the pinned budget" + _ignored_note(ignored_diagnostics))
        return 0

    print(f"FAIL: integration skip set drifted (budget {expected_total}, actual {parsed})", file=sys.stderr)
    for reason, count in sorted(added.items()):
        print(f"  +{count:3}  {reason}", file=sys.stderr)
    for reason, count in sorted(removed.items()):
        print(f"  -{count:3}  {reason}", file=sys.stderr)
    print(
        "Capability skips are reviewed coverage decisions. If the drift is intentional, "
        "update the pinned matrix in ProviderCapabilityContractTests and refresh this budget with --update.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
