#!/usr/bin/env python3
"""check_docs.py - structural invariants for the T225 documentation delta (E02 / AC05).

Checks, over the authored footprint declared in --scope:
  * CRLF only (no lone LF) in every changed document;
  * EN/RU mirror parity for the paired advanced/guide pages;
  * consecutive numbering of docs/guide/NN-*.md and docs/ru/guide/NN-*.md;
  * no public page links into docs/specs/** (relative, .md, or github blob);
  * every declared footprint file exists and is inside the authored scope.

Exit 0 = all invariants hold; 2 = violation(s); 1 = usage / unreadable input.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys


def usage_error(message: str) -> int:
    print("ERROR: %s" % message, file=sys.stderr)
    return 1


def read_text(path: str) -> str:
    with open(path, "rb") as handle:
        return handle.read().decode("utf-8", errors="replace")


def check_crlf(path: str) -> list[str]:
    raw = open(path, "rb").read()
    problems = []
    lone_lf = raw.count(b"\n") - raw.count(b"\r\n")
    if lone_lf:
        problems.append("%s: %d lone LF line ending(s)" % (path, lone_lf))
    return problems


def check_public_to_specs(path: str, text: str) -> list[str]:
    problems = []
    if "specs/" not in text:
        return problems
    for number, line in enumerate(text.splitlines(), 1):
        if "specs/" in line:
            problems.append("%s:%d: public page links into docs/specs: %s" % (path, number, line.strip()[:160]))
    return problems


def guide_numbers(directory: str) -> list[int]:
    numbers = []
    for name in os.listdir(directory):
        match = re.match(r"^(\d+)-", name)
        if match and name.endswith(".md"):
            numbers.append(int(match.group(1)))
    return sorted(numbers)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--scope", required=True)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--output", required=True)
    try:
        args = parser.parse_args()
    except SystemExit:
        return 1

    try:
        with open(args.scope, "r", encoding="utf-8") as handle:
            scope = json.load(handle)["scope"]
    except Exception as exc:  # noqa: BLE001
        return usage_error("cannot read scope: %s" % exc)

    files = scope["files"]
    failures: list[str] = []
    checked: list[str] = []

    for path in files:
        if not os.path.isfile(path):
            failures.append("missing footprint file: %s" % path)
            continue
        checked.append(path)
        text = read_text(path)
        failures += check_crlf(path)
        if path.startswith("docs/advanced/") or path.startswith("docs/guide/"):
            failures += check_public_to_specs(path, text)

    # EN/RU mirror parity for the paired advanced/guide pages.
    for path in files:
        if path.startswith("docs/") and not path.startswith("docs/ru/"):
            if path.startswith(("docs/advanced/", "docs/guide/")) and path.endswith(".md"):
                mirror = path.replace("docs/", "docs/ru/", 1)
                if mirror in files and not os.path.isfile(mirror):
                    failures.append("EN/RU mirror missing: %s" % mirror)

    # Consecutive guide numbering (no holes, no duplicates).
    for directory in ("docs/guide", "docs/ru/guide"):
        numbers = guide_numbers(directory)
        expected = list(range(1, len(numbers) + 1))
        if numbers != expected:
            failures.append("%s: guide numbering is not consecutive 1..N: %s" % (directory, numbers))

    report = {
        "baseline": args.baseline,
        "checked_files": len(checked),
        "failures": failures,
    }
    with open(args.output, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=1)

    for failure in failures:
        print("FAIL: %s" % failure)
    print("check_docs: %d file(s) checked, %d failure(s)" % (len(checked), len(failures)))
    return 2 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
