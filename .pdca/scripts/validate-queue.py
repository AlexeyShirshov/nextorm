#!/usr/bin/env python3
"""Validate the collection queue table: one row per unit, exactly 17 fields.

The queue is data, and ad-hoc `python3 -c` checks have twice reported a false green:
`io.open(path, encoding='utf-8').read()` applies universal-newline translation, so a
CRLF file becomes one long line, `split('\\r\\n')` yields a single element, no line
starts with `| T`, and the "bad rows" list comes back empty. Reading with
`newline=''` is the fix, and this script exists so nobody has to remember it.

Checks:
  * every row in the units table has exactly 17 pipe-separated fields;
  * unit ids are unique;
  * `status` (field 9) is one of the known values;
  * `notes` (field 15) contains no stray pipe (the failure mode that silently added
    a field: `X|unspecified`);
  * every `done` unit names a lane or a merge commit;
  * (advisory) referenced groups missing from the group table are reported, not failed:
    the table keeps the scheduled groups, not the history of every lane.

Exit code 0 = valid, 1 = at least one violation (printed with the row id).
"""
from __future__ import annotations

import io
import sys

PATH = sys.argv[1] if len(sys.argv) > 1 else ".pdca/status/collection-rc4.md"

KNOWN_STATUS = {
    "pending",
    "running",
    "blocked",
    "done",
    "deferred",
    "cancelled",
}


def main() -> int:
    # newline='' is the whole point: no universal-newline translation.
    text = io.open(PATH, encoding="utf-8", newline="").read()
    lines = text.split("\r\n") if "\r\n" in text else text.split("\n")

    rows = [l for l in lines if l.startswith("| T")]
    problems: list[str] = []
    seen: dict[str, int] = {}

    for row in rows:
        fields = row.split("|")
        uid = fields[1].strip() if len(fields) > 1 else "?"
        if len(fields) != 17:
            problems.append(f"{uid}: {len(fields)} fields, expected 17")
            continue
        seen[uid] = seen.get(uid, 0) + 1

        status = fields[9].strip()
        if status not in KNOWN_STATUS:
            problems.append(f"{uid}: unknown status {status!r}")

        notes = fields[15]
        if "|" in notes:
            problems.append(f"{uid}: stray pipe in notes")

        if status == "done" and fields[10].strip() in {"", "—"}:
            problems.append(f"{uid}: done without a lane")

    for uid, count in sorted(seen.items()):
        if count > 1:
            problems.append(f"{uid}: {count} rows")

    # The group table records the groups that were actually scheduled; completed lanes are
    # dropped from it over time, so a missing entry is not a defect. Reported as a count only.
    groups = {
        l.split("|")[1].strip()
        for l in lines
        if l.startswith("| G") and len(l.split("|")) > 2
    }
    missing = sorted(
        {
            row.split("|")[4].strip()
            for row in rows
            if len(row.split("|")) == 17
            and row.split("|")[4].strip().startswith("G")
            and row.split("|")[4].strip() not in groups
        }
    )
    if missing:
        print(f"note: {len(missing)} referenced groups are not in the group table (history): "
              + ", ".join(missing))

    if problems:
        print("FAIL")
        for p in problems:
            print("  " + p)
        return 1

    print(f"OK: {len(rows)} unit rows, 17 fields each, no duplicates, statuses valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
