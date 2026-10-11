#!/usr/bin/env python3
"""Regenerate mutate-spans.json + stryker-config.corrected.json for D211.

Maps `git diff -U0 b527be6f -- src/**/*.cs` new-file line ranges (the full T211
footprint on the FINAL corrected tree) to CRLF raw-byte offsets as Stryker.NET
mutate-filter character spans (project-relative paths, as in the running config).

Convention (matches the previously validated `run-survivors` config):
  span = byte offset of the first byte of the range's first line
  end  = byte offset of the first byte of the line AFTER the range, minus 1
"""
import json
import os
import re
import subprocess
import sys

BASE_SHA = "b527be6f"
REPO = "/home/alex/sources/nextorm"
PROJECT_ROOT = os.path.join(REPO, "src/nextorm.core")
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_SPANS = os.path.join(HERE, "mutate-spans.json")
OUT_CONFIG = os.path.join(HERE, "stryker-config.corrected.json")
CONFIG_TEMPLATE = os.path.join(HERE, "stryker-config.corrected.json")


def git_diff():
    out = subprocess.run(
        ["git", "diff", "-U0", BASE_SHA, "--", "src/**/*.cs"],
        cwd=REPO, capture_output=True, text=True, check=True,
    )
    return out.stdout


def parse_hunks(text):
    files = {}
    cur = None
    for line in text.splitlines():
        if line.startswith("+++ b/"):
            cur = line[len("+++ b/"):]
            files.setdefault(cur, [])
        elif line.startswith("@@") and cur is not None:
            m = re.match(r"@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", line)
            if not m:
                continue
            start = int(m.group(1))
            count = int(m.group(2)) if m.group(2) is not None else 1
            if count > 0:
                files[cur].append((start, start + count - 1))
    return files


def line_offsets(data):
    # offset of line n (1-indexed) for CRLF content; line includes its trailing \r
    offsets = [0]
    off = 0
    for chunk in data.split(b"\n")[:-1]:
        off += len(chunk) + 1
        offsets.append(off)
    return offsets


def build_spans():
    files = parse_hunks(git_diff())
    spans = []
    for path, ranges in files.items():
        rel = path
        prefix = "src/nextorm.core/"
        if rel.startswith(prefix):
            rel = rel[len(prefix):]
        proj = os.path.join(PROJECT_ROOT, rel)
        data = open(proj, "rb").read()
        offsets = line_offsets(data)
        for start, end in ranges:
            span = offsets[start - 1]
            after = end + 1
            if after - 1 <= len(offsets) - 1:
                end_off = offsets[after - 1] - 1
            else:
                end_off = len(data) - 1
            spans.append({
                "f": rel,
                "line": f"{start}..{end}",
                "span": span,
                "end": end_off,
                "pat": f"{rel}{{{span}..{end_off}}}",
            })
    return spans


def main():
    spans = build_spans()
    with open(OUT_SPANS, "w", newline="") as fh:
        json.dump(spans, fh, indent=2)
        fh.write("\n")
    cfg = json.load(open(CONFIG_TEMPLATE))
    cfg["stryker-config"]["mutate"] = [s["pat"] for s in spans]
    with open(OUT_CONFIG, "w", newline="") as fh:
        json.dump(cfg, fh, indent=2)
        fh.write("\n")
    print(f"spans={len(spans)} files={len(set(s['f'] for s in spans))}")
    for s in spans:
        if "SqlBuilder" in s["f"]:
            print("  ", s["pat"])


if __name__ == "__main__":
    main()
