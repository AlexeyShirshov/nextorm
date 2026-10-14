#!/usr/bin/env python3
"""D226 / rv2 — verify the INHERITED (rv1) accepted evidence and freeze the per-row records.

Usage:
  python3 verify-accepted-evidence.py --all
  python3 verify-accepted-evidence.py --row E226-03

For every inherited rv1 row it
  * re-verifies the frozen rv1 artifact hashes against `rv1-frozen.sha256`,
  * checks that every rv1 evidence path the row references still exists,
  * writes `$R/accepted/<ID>.json` (+ `.log`) recording the rv1 row definition, its referenced
    artifacts, their exit codes and the acceptance decision.
It NEVER upgrades a row: `E226-09` is recorded as NOT accepted, because the rv1 row-count mutant is
invalid (it counted columns/result sets) and is superseded prospectively by E226-09-BATCH-ROWS and
E226-09-N6. Exit 0 only when all checks pass for every requested row.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

R = Path(__file__).resolve().parent
REPO = R.parents[3]
R1 = R.parent / "rv1"
FROZEN = R / "rv1-frozen.sha256"
ACCEPTED = R / "accepted"

INHERITED_ROWS = [f"E226-0{i}" for i in range(1, 10)]
NOT_ACCEPTED = {
    "E226-09": {
        "accepted": False,
        "reason": (
            "rv1 E226-09 is INVALID and must not be reused as rv2 proof: its 'row-count' patch "
            "substituted a column count / result-set count, not a returned-row count (CHECK #3). It is "
            "superseded prospectively by the new rows E226-09-BATCH-ROWS (literal two-site returned-row "
            "substitution, killed with expected 1 / actual 3) and E226-09-N6 (infrastructure rejection)."
        ),
    }
}


def verify_frozen(log: list[str]) -> bool:
    ok = True
    if not FROZEN.is_file():
        log.append(f"MISSING frozen manifest {FROZEN}")
        return False
    for line in FROZEN.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        digest, _, rel = line.partition("  ")
        path = REPO / rel
        if not path.is_file():
            log.append(f"MISSING rv1 artifact {rel}")
            ok = False
            continue
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != digest:
            log.append(f"CHANGED rv1 artifact {rel} ({digest[:12]} -> {actual[:12]})")
            ok = False
    log.append(f"rv1 frozen artifacts verified: {len(FROZEN.read_text(encoding='utf-8').splitlines())}")
    return ok


def main(argv: list[str]) -> int:
    if not argv or argv[0] not in ("--all", "--row"):
        print(__doc__)
        return 2
    ids = INHERITED_ROWS if argv[0] == "--all" else [argv[1]]
    r1_manifest = json.loads((R1 / "manifest.json").read_text(encoding="utf-8"))
    r1_rows = {row["id"]: row for row in r1_manifest["rows"]}

    ACCEPTED.mkdir(parents=True, exist_ok=True)
    ok = True
    for row_id in ids:
        log: list[str] = [f"row {row_id}"]
        frozen_ok = verify_frozen(log)
        row = r1_rows.get(row_id)
        if row is None:
            log.append(f"MISSING rv1 row definition for {row_id}")
            ok = False
            resolved = []
            decision = {"accepted": False, "reason": "no rv1 row definition"}
        else:
            resolved = []
            for item in row.get("evidence", []):
                path = REPO / item["path"]
                exists = path.is_file() or path.is_dir()
                if not exists:
                    log.append(f"MISSING evidence {item['path']}")
                    ok = False
                resolved.append({"kind": item["kind"], "path": item["path"], "exists": exists,
                                 "exit": item.get("exit"), "note": item.get("note")})
            decision = NOT_ACCEPTED.get(row_id, {"accepted": True, "reason": "inherited rv1 row retained unchanged"})
        if not frozen_ok:
            ok = False

        record = {
            "row": row_id,
            "inherited_from": "rv1",
            "rv1_status": row.get("status") if row else None,
            "rv1_requirement": row.get("row") if row else None,
            "acceptance": decision,
            "evidence": resolved,
            "rv1_frozen_verified": frozen_ok,
            "verifier": "artifacts/pdca/D226/rv2/verify-accepted-evidence.py",
        }
        (ACCEPTED / f"{row_id}.json").write_text(json.dumps(record, indent=2, ensure_ascii=False), encoding="utf-8")
        (ACCEPTED / f"{row_id}.log").write_text("\n".join(log) + "\n", encoding="utf-8")
        print(f"{row_id}: accepted={decision['accepted']} evidence={len(resolved)} frozen_ok={frozen_ok}")

    print(f"accepted_evidence_exit={0 if ok else 1}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
