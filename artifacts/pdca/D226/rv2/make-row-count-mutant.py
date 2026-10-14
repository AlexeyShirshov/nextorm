#!/usr/bin/env python3
"""D226 / rv2 — generate the LITERAL row-count mutant patch from the CURRENT source bytes.

The mutant substitutes the driver's affected-row total with the number of materialized rows, at both
release sites of the batch runner:
    System.Linq.Enumerable.Sum(sets, static set => set.Count)

The patch is produced against the working-tree bytes (never against HEAD) and rewritten to
repository-relative a/ b/ headers so `git apply --check` accepts it.

Exit codes: 0 ok, 9 when the expected sites do not match exactly (fail closed).
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile

R = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(R, "..", "..", "..", ".."))
REL = "src/nextorm.core/DataContext/BatchRunner.cs"
SRC = os.path.join(REPO, REL)
PATCH = os.path.join(R, "mutations", "total-to-row-count.patch")

NEEDLE = "        return new BatchResult(sets, resultTypes, columnNames, CommandReaderOwner.ReadRecordsAffected(reader));"
REPLACEMENT = "        return new BatchResult(sets, resultTypes, columnNames, System.Linq.Enumerable.Sum(sets, static set => set.Count));"
EXPECTED_SITES = 2


def main() -> int:
    if not os.path.isfile(SRC):
        print(f"MISSING source {SRC}")
        return 9

    text = open(SRC, encoding="utf-8", newline="").read()
    sites = text.count(NEEDLE)
    if sites != EXPECTED_SITES:
        print(f"SITE_COUNT {sites} expected {EXPECTED_SITES}")
        return 9

    mutated = text.replace(NEEDLE, REPLACEMENT)
    if mutated == text:
        print("PATCH_NO_MATCH (substitution produced identical bytes)")
        return 9

    work = tempfile.mkdtemp(prefix="d226-r2-mutant-")
    mutated_path = os.path.join(work, "BatchRunner.cs")
    open(mutated_path, "w", encoding="utf-8", newline="").write(mutated)

    diff = subprocess.run(
        ["git", "-C", REPO, "diff", "--no-index", "--src-prefix=a/", "--dst-prefix=b/", "--", SRC, mutated_path],
        capture_output=True, text=True,
    )
    out = diff.stdout
    if "@@ " not in out:
        print("DIFF_EMPTY (git diff --no-index produced no hunk)")
        return 9

    rewritten = []
    for line in out.split("\n"):
        if line.startswith("--- a/"):
            line = "--- a/" + REL
        elif line.startswith("+++ b/"):
            line = "+++ b/" + REL
        rewritten.append(line)
    patch = "\n".join(rewritten)

    os.makedirs(os.path.dirname(PATCH), exist_ok=True)
    open(PATCH, "w", encoding="utf-8", newline="").write(patch)

    hunks = patch.count("\n@@ ")
    print(f"patch={PATCH}")
    print(f"sites={sites} hunks={hunks} added_lines={patch.count(chr(10) + '+')}")
    if hunks < EXPECTED_SITES:
        print(f"HUNK_COUNT {hunks} expected >= {EXPECTED_SITES}")
        return 9
    return 0


if __name__ == "__main__":
    sys.exit(main())
