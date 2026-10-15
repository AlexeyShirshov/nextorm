#!/usr/bin/env python3
"""T232 (#232) D-11 — generate rv4/ownership.json from the hunk inventory.

Reads `hunk-inventory.json` (produced by split_hunks.py) and emits the ownership
record with the hunk-level classification, byte hashes, disjointness and the
reconstruction proof. Idempotent: re-running reproduces the same hashes.
"""
import datetime
import json
import os
import subprocess

OUT = os.path.dirname(os.path.abspath(__file__))
RV4 = os.path.dirname(OUT)
REPO = "/home/alex/sources/nextorm"
FILE = "examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs"


def sh(*argv):
    return subprocess.run(argv, cwd=REPO, capture_output=True).stdout.decode().strip()


def main():
    inv = json.load(open(os.path.join(OUT, "hunk-inventory.json"), encoding="utf-8"))
    rel = os.path.relpath(OUT, REPO)

    t232 = []
    for e in inv["t232_hunks"]:
        t232.append({
            "path": FILE,
            "hunk_header": e["hunk_header"],
            "old_range": e["old_range"],
            "new_range": e["new_range"],
            "kind": "typed QuarterlyPivot projection (T232 D-06 authored surface)",
            "before_hash": e["before_hash"],
            "after_hash": e["after_hash"],
            "before_bytes": e["before_bytes"],
            "after_bytes": e["after_bytes"],
            "line_endings": "CRLF",
        })

    foreign = []
    for e in inv["foreign_hunks"]:
        foreign.append({
            "path": FILE,
            "hunk_header": e["hunk_header"],
            "old_range": e["old_range"],
            "new_range": e["new_range"],
            "origin": "pre-existing working-tree work, not authored by T232",
            "baseline_source": [
                "docs/specs/status/collection-1.0.9-todo.md §Working-tree note "
                "(uncommitted entries not produced by this collection)",
                "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/d07-footprint.json:31-34 "
                "(removed_foreign / removed_foreign_explained)",
            ],
            "before_hash": e["before_hash"],
            "after_hash": e["after_hash"],
            "before_bytes": e["before_bytes"],
            "after_bytes": e["after_bytes"],
            "line_endings": "CRLF",
            "change_count": e["change_count"],
            "changes": e["changes"],
            "supporting_count": e["supporting_count"],
            "supporting": e["supporting"],
            "comment_lines": e["comment_lines"],
        })

    rec = inv["reconstruction"]
    doc = {
        "task": "T232 (#232)",
        "unit": "D-11",
        "case": "ownership",
        "captured": datetime.datetime.now(datetime.timezone.utc)
                      .strftime("%Y-%m-%dT%H:%M:%SZ"),
        "cwd": REPO,
        "branch": sh("git", "rev-parse", "--abbrev-ref", "HEAD"),
        "head_at_capture": sh("git", "rev-parse", "HEAD"),
        "commit": "001f9c9c",
        "commit_full": inv["commit"],
        "commit_parent": inv["parent"],
        "file": FILE,
        "classification": "mixed",
        "line_endings": "CRLF",
        "blob_parent": inv["blob_parent"],
        "blob_commit": inv["blob_commit"],
        "hunk_count_total": rec["hunk_count_total"],
        "t232_hunk_count": len(t232),
        "foreign_hunk_count": len(foreign),
        "t232_hunks": t232,
        "foreign_hunks": foreign,
        "foreign_discrete_changes": inv["foreign_discrete_changes"],
        "foreign_supporting_changes": inv["foreign_supporting_changes"],
        "foreign_total_edits": inv["foreign_discrete_changes"] + inv["foreign_supporting_changes"],
        "foreign_comment_lines": inv["foreign_comment_lines"],
        "foreign_changed_lines": inv["foreign_changed_lines"],
        "foreign_estimate_note": (
            "the D-11 brief estimated ~30 foreign hunks; measured at edit-site "
            "granularity: 11 diff hunks containing 30 discrete foreign edits "
            "(8 AsCte calls, 1 WithAlias(Alias.Metrics), 3 Alias.* refs, "
            "12 alias-member reads, 2 CustomerMetricRow sites, 2 using imports, "
            "2 '#160' comments) over 117 added / 97 removed foreign lines"
        ),
        "sibling_owner": "unidentified",
        "sibling_owner_note": (
            "no verified #160 owner identity in the evidence set; not guessed. "
            "Issue #160 is authored by AlexeyShirshov and CLOSED (milestone 1.0.9-rc2), "
            "which is not an identification of the working-tree author."
        ),
        "retention_authorization": {
            "status": "pending",
            "authorizer": None,
            "role": "collection owner/user",
            "channel": None,
            "timestamp": None,
            "quote": None,
            "timing": "post-discovery",
            "text": ("retain the listed foreign hunks in 001f9c9c; attribute them to #160; "
                     "T232 does not claim authorship"),
        },
        "issue_160": {
            "number": 160,
            "url": "https://github.com/AlexeyShirshov/nextorm/issues/160",
            "milestone": "1.0.9-rc2",
            "milestone_number": 20,
            "state": "CLOSED",
            "author": "AlexeyShirshov",
            "assignees": [],
            "labels": [],
            "comment_status": "pending owner consent",
        },
        "commit_001f9c9c_rewritten": False,
        "history_rewritten": False,
        "parent_collection_mandate_is_not_authorization": (
            "the parent collection mandate forbade touching examples/** beyond the example "
            "footprint; disclosure is not authorization and is not cited as one"
        ),
        "disjointness": inv["disjointness"],
        "reconstruction": rec,
        "patches": {
            "t232": "%s/t232.patch" % rel,
            "foreign": "%s/foreign.patch" % rel,
            "full": "%s/001f9c9c.full.patch" % rel,
            "per_hunk_dir": "%s/hunks" % rel,
        },
        "verification": {
            "command": ["python3", "%s/split_hunks.py" % rel],
            "cwd": REPO,
            "exit": 0,
            "log": "%s/split_hunks.out" % rel,
            "claim": ("blob(001f9c9c^) + t232.patch + foreign.patch == blob(001f9c9c) "
                      "(both orders), plus hunk-range disjointness"),
        },
        "blocker": {
            "key": "D232-AC12-foreign-hunks-owner",
            "row": "AC-12",
            "wording": ("AC-12 open: foreign #160 hunks in 001f9c9c have no identified owner "
                        "and no explicit retention authorization from the collection owner"),
        },
    }
    with open(os.path.join(RV4, "ownership.json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1, sort_keys=True)
        f.write("\n")
    print(json.dumps({"written": "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership.json",
                      "t232_hunks": len(t232), "foreign_hunks": len(foreign),
                      "foreign_total_edits": doc["foreign_total_edits"]}, indent=1))


if __name__ == "__main__":
    main()
