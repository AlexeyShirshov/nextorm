#!/usr/bin/env python3
"""check_patch.py - baseline-relative documentation patch portability (E05 / AC05-C8).

Produces the authored documentation patch against --baseline, restricted to the
footprint declared in --scope, then validates it against a *temporary* index
read-tree'd from that same baseline:

    GIT_INDEX_FILE=<tmp> git read-tree <baseline>
    GIT_INDEX_FILE=<tmp> git apply --cached --check <patch>

No real index or working tree is touched. Exit 0 = patch is scope-clean and applies
to the frozen baseline; 2 = violation(s); 1 = usage / environment error.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile


def run(cmd: list[str], env: dict | None = None) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, capture_output=True, text=True, env=env)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--scope", required=True)
    parser.add_argument("--patch", required=True)
    parser.add_argument("--report", required=True)
    args = parser.parse_args()

    with open(args.scope, "r", encoding="utf-8") as handle:
        files = json.load(handle)["scope"]["files"]

    failures: list[str] = []
    receipts: list[dict] = []

    diff = run(["git", "diff", "--binary", args.baseline, "--", *files])
    receipts.append({"cmd": ["git", "diff", "--binary", args.baseline, "--"] + files, "exit": diff.returncode})
    if diff.returncode != 0:
        failures.append("git diff failed: %s" % diff.stderr.strip())
    with open(args.patch, "w", encoding="utf-8", newline="") as handle:
        handle.write(diff.stdout)

    # Every hunk header path must be inside the declared footprint.
    for line in diff.stdout.splitlines():
        match = re.match(r"^diff --git a/(\S+) b/(\S+)$", line)
        if not match:
            continue
        for path in match.groups():
            if path not in files:
                failures.append("patch touches out-of-scope path: %s" % path)

    # Temp-index apply check against the frozen baseline.
    with tempfile.TemporaryDirectory() as tmp:
        index = os.path.join(tmp, "index")
        env = dict(os.environ, GIT_INDEX_FILE=index)
        read_tree = run(["git", "read-tree", args.baseline], env=env)
        receipts.append({"cmd": ["git", "read-tree", args.baseline], "exit": read_tree.returncode})
        if read_tree.returncode != 0:
            failures.append("read-tree failed: %s" % read_tree.stderr.strip())
        apply_check = run(["git", "apply", "--cached", "--check", args.patch], env=env)
        receipts.append({"cmd": ["git", "apply", "--cached", "--check", args.patch], "exit": apply_check.returncode})
        if apply_check.returncode != 0:
            failures.append("apply --cached --check failed: %s" % apply_check.stderr.strip())

    report = {
        "baseline": args.baseline,
        "patch": args.patch,
        "files": len(files),
        "hunks": diff.stdout.count("\n@@ "),
        "receipts": receipts,
        "failures": failures,
    }
    with open(args.report, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=1)

    for failure in failures:
        print("FAIL: %s" % failure)
    print("check_patch: %d file(s), %d hunk(s), %d failure(s)" % (len(files), report["hunks"], len(failures)))
    return 2 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
