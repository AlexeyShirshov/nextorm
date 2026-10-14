#!/usr/bin/env python3
"""D226 / rv2 — verify the provenance erratum (P226-R2-04).

Checks, against actual artifacts:
  * `provenance.md` exists and states the three required truths;
  * `mutation-report.json` carries a `provenance` block with `stryker_post_fix_rerun =
    not-substantiated`, has NO `stryker_re_run` key, records the X1-X3 extras as killed with patches,
    and records the two ConfigureAwait entries as justified reasoning (never kills);
  * the pre-fix rv1 Stryker artifacts are present as history (existence recorded, not treated as
    post-fix evidence);
  * rv1 stays byte-identical (hashes verified against rv1-frozen.sha256).
Exit 0 only when every check passes.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

R = Path(__file__).resolve().parent
REPO = R.parents[3]
R1 = R.parent / "rv1"

REQUIRED_STATEMENTS = [
    "X1–X3 substantiate the three former survivors",
    "NOT evidence of a",
    "reasoning, never a kill",
]
EXTRA_IDS = [
    "remove-publish-after-release-closereader",
    "remove-publish-after-release-disposeasync",
    "remove-command-disposeasync",
]
HISTORY = [
    "check/stryker-output/reports/mutation-report.json",
    "check/stryker.log",
    "check/stryker-config.json",
]


def main() -> int:
    fails: list[str] = []

    def check(ok: bool, message: str) -> None:
        print(f"{'OK  ' if ok else 'FAIL'} {message}")
        if not ok:
            fails.append(message)

    doc = R / "provenance.md"
    text = doc.read_text(encoding="utf-8") if doc.is_file() else ""
    check(doc.is_file(), "provenance.md exists")
    for statement in REQUIRED_STATEMENTS:
        check(statement in text, f"provenance.md states: {statement!r}")

    report = json.loads((R / "mutation-report.json").read_text(encoding="utf-8"))
    prov = report.get("provenance", {})
    check(bool(prov), "mutation-report.json has a provenance block")
    check(prov.get("stryker_post_fix_rerun") == "not-substantiated",
          "provenance.stryker_post_fix_rerun == not-substantiated")
    check("stryker_re_run" not in report, "mutation-report.json has no unsupported stryker_re_run key")
    check("stryker_re_run" not in prov, "provenance block has no unsupported stryker_re_run key")

    mutants = {m["id"]: m for m in report.get("mutants", [])}
    for mid in EXTRA_IDS:
        m = mutants.get(mid)
        check(m is not None and m.get("kill_status") == "killed",
              f"{mid} is killed in the rv2 report")
        if m:
            check(m.get("killing_test") != "" and m.get("assertion_message", "") != "",
                  f"{mid} carries a killing test and an assertion message")
            patch = REPO / m["patch"]
            check(patch.is_file(), f"{mid} patch artifact exists: {m['patch']}")

    justified = prov.get("configureawait_entries", [])
    check(len(justified) == 2, "provenance records exactly two ConfigureAwait entries")
    check(all(e.get("decision") == "justified" and e.get("basis") == "reasoning" for e in justified),
          "both ConfigureAwait entries are justified reasoning (not kills)")

    for rel in HISTORY:
        exists = (R1 / rel).is_file()
        print(f"{'OK  ' if exists else 'INFO'} pre-fix rv1 Stryker history present={exists}: {rel}")
        if exists:
            prov.setdefault("pre_fix_stryker_history_present", []).append(rel)

    frozen = R / "rv1-frozen.sha256"
    if frozen.is_file():
        changed = []
        for line in frozen.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            digest, _, rel = line.partition("  ")
            path = REPO / rel
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                changed.append(rel)
        check(not changed, f"rv1 byte-identical ({len(changed)} changed)")
    else:
        check(False, "rv1-frozen.sha256 exists")

    print(f"provenance_exit={1 if fails else 0}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
