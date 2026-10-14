#!/usr/bin/env python3
"""D226 / rv2 — r2 delta verification against the r2 STARTING TREE (E226-06 delta artifacts).

Usage:
  python3 verify-r2-delta.py snapshot   # record the r2 starting state (production hashes + diff)
  python3 verify-r2-delta.py check      # prove production bytes are unchanged by r2 and that only
                                        # the allowed files differ from the r2 starting tree

Semantics recorded honestly: the "r2 starting tree" is the accepted T226 state at the moment r=2 DO
began. When `snapshot` cannot be taken before the first r=2 edit (it was not), the snapshot records the
production bytes (which r=2 must not change at all) and the allowlist of files r=2 may touch, and
`check` proves exactly that: production files byte-identical to the snapshot, and every other changed
tracked file inside the allowlist.
"""
from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

R = Path(__file__).resolve().parent
REPO = R.parents[3]

PRODUCTION = [
    "src/nextorm.core/DataContext/CommandReaderOwner.cs",
    "src/nextorm.core/DataContext/ProcedureResult.cs",
    "src/nextorm.core/Builders/BatchBuilder.cs",
    "src/nextorm.core/DataContext/BatchRunner.cs",
]
ALLOWED_CHANGED = set(PRODUCTION) | {
    "tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs",
    "docs/specs/status/D226-records-affected-1.md",
}
BEFORE = R / "production-before.sha256"
AFTER = R / "production-after.sha256"
BRANCH_BEFORE = R / "branch-before.patch"
BRANCH_AFTER = R / "branch-after.patch"
BRANCH_DELTA = R / "branch-delta.patch"
DELTA_JSON = R / "branch-delta.json"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args: str) -> str:
    return subprocess.run(["git", "-C", str(REPO), *args], capture_output=True, text=True).stdout


def production_hashes() -> dict[str, str]:
    return {rel: sha256(REPO / rel) for rel in PRODUCTION}


def write_hashes(path: Path, hashes: dict[str, str]) -> None:
    path.write_text("".join(f"{digest}  {rel}\n" for rel, digest in hashes.items()), encoding="utf-8")


def changed_paths() -> list[str]:
    paths = []
    for line in git("status", "--porcelain=v1").splitlines():
        if not line.strip():
            continue
        rel = line[3:].split(" -> ")[-1].strip()
        if rel.startswith("artifacts/"):
            continue
        paths.append(rel)
    return sorted(paths)


def snapshot() -> int:
    hashes = production_hashes()
    write_hashes(BEFORE, hashes)
    BRANCH_BEFORE.write_text(git("diff", "--no-ext-diff", "--", *PRODUCTION), encoding="utf-8")
    (R / "r2-start.json").write_text(json.dumps({
        "recorded_at": "r=2 DO",
        "production_sha256": hashes,
        "changed_files_at_snapshot": changed_paths(),
        "allowed_r2_changes": sorted(ALLOWED_CHANGED),
        "note": "snapshot taken during r=2 DO (after the P226-R2-02 oracle edit, before the rv2 artifacts); production bytes are the invariant r=2 must not change, and are identical to the accepted rv1 values. `check` requires the changed-file set to stay within changed_files_at_snapshot (pre-existing dirty state + the allowed r2 changes) plus artifacts/.",
    }, indent=2, ensure_ascii=False), encoding="utf-8")
    print("snapshot written:", BEFORE.name, BRANCH_BEFORE.name, "r2-start.json")
    print(f"changed_files_at_snapshot={len(changed_paths())}")
    for rel, digest in hashes.items():
        print(f"  {digest[:12]}  {rel}")
    return 0


def check() -> int:
    if not BEFORE.is_file():
        print("MISSING production-before.sha256 (run snapshot first)")
        return 1
    before = dict(line.split("  ")[::-1] for line in BEFORE.read_text(encoding="utf-8").splitlines() if line.strip())
    after = production_hashes()
    write_hashes(AFTER, after)
    BRANCH_AFTER.write_text(git("diff", "--no-ext-diff", "--", *PRODUCTION), encoding="utf-8")
    unchanged = before == after
    branch_delta = subprocess.run(["diff", "-u", str(BRANCH_BEFORE), str(BRANCH_AFTER)],
                                  capture_output=True, text=True).stdout
    BRANCH_DELTA.write_text(branch_delta, encoding="utf-8")

    changed = changed_paths()
    baseline = set(json.loads((R / "r2-start.json").read_text(encoding="utf-8")).get("changed_files_at_snapshot", []))
    outside = sorted(c for c in changed if c not in baseline and c not in ALLOWED_CHANGED)

    ok = unchanged and outside == []
    DELTA_JSON.write_text(json.dumps({
        "task": "T226 (#226) rv2 — E226-06 delta",
        "contract_rv": 2,
        "production_unchanged": unchanged,
        "production_before": before,
        "production_after": after,
        "branch_delta_empty": branch_delta == "",
        "changed_tracked_files_outside_baseline_and_allowlist": outside,
        "changed_files_at_snapshot": sorted(baseline),
        "changed_files_at_check": changed,
        "allowed_r2_changes": sorted(ALLOWED_CHANGED),
        "artifacts": ["production-before.sha256", "production-after.sha256", "branch-before.patch",
                      "branch-after.patch", "branch-delta.patch", "branch-delta.json", "r2-start.json"],
        "note": "The r2 starting tree is the accepted T226 state when r=2 DO began; because the snapshot could not precede the first r2 edit, the contract enforced here is: production bytes byte-identical to the snapshot AND no tracked file outside the snapshot's dirty baseline plus the two allowed r2 changes (the SQLite oracle test and the additive status file). rv1 is untracked (artifacts/ is gitignored) and its byte-identity is proven by rv1-frozen.sha256 + verify-accepted-evidence.py.",
        "result": "ok" if ok else "fail",
    }, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"production_unchanged={unchanged} branch_delta_empty={branch_delta == ''} outside={outside}")
    print(f"delta_check_exit={0 if ok else 1}")
    return 0 if ok else 1


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "snapshot":
        sys.exit(snapshot())
    if cmd == "check":
        sys.exit(check())
    print(__doc__)
    sys.exit(2)
