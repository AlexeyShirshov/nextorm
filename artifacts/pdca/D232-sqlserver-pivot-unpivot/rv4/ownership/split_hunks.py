#!/usr/bin/env python3
"""T232 (#232) D-11 — hunk-level ownership split of commit 001f9c9c.

Splits the single-file diff 001f9c9c^..001f9c9c (examples/.../AdventureWorksQueries.cs)
into a T232-only patch and a foreign-only patch (#160 pre-existing working-tree work),
proves the two hunk sets are disjoint, and proves reconstruction:

    blob(parent) + t232_patch + foreign_patch == blob(001f9c9c)

Both application orders are attempted (t232->foreign and foreign->t232); the parent
blob is never rewritten and commit 001f9c9c is never modified.
"""
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = "/home/alex/sources/nextorm"
COMMIT = "001f9c9c"
FILE = "examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs"
OUT = os.path.dirname(os.path.abspath(__file__))
TMP_ROOT = "/tmp/opencode"

# Hunk classification. T232 authored the typed PIVOT overload work in this file: the
# QuarterlyPivot comment/usage hunks. Every other hunk is the pre-existing foreign
# #160 working-tree work (AsCte / WithAlias / Alias.* / CustomerMetricRow / "#160").
T232_HUNKS = [11, 12]  # 1-based hunk ordinals in the diff


def run(argv, cwd=REPO, check=True):
    p = subprocess.run(argv, cwd=cwd, capture_output=True)
    if check and p.returncode != 0:
        sys.stderr.write("FAILED: %s\n%s\n%s\n" % (" ".join(argv), p.stdout.decode(), p.stderr.decode()))
        sys.exit(p.returncode)
    return p


def rev(x):
    return run(["git", "rev-parse", x]).stdout.decode().strip()


def blob(x):
    return run(["git", "rev-parse", "%s:%s" % (x, FILE)]).stdout.decode().strip()


def show(x):
    return run(["git", "show", "%s:%s" % (x, FILE)]).stdout


def sha256(b):
    return hashlib.sha256(b).hexdigest()


HDR_RE = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")


def parse_patch(text):
    lines = text.splitlines(keepends=True)
    header, hunks, cur = [], [], None
    for ln in lines:
        if ln.startswith("@@"):
            if cur is not None:
                hunks.append(cur)
            m = HDR_RE.match(ln)
            cur = {
                "header_line": ln,
                "old_start": int(m.group(1)),
                "old_len": int(m.group(2) or "1"),
                "new_start": int(m.group(3)),
                "new_len": int(m.group(4) or "1"),
                "body": [],
            }
        elif cur is None:
            header.append(ln)
        else:
            cur["body"].append(ln)
    if cur is not None:
        hunks.append(cur)
    return "".join(header), hunks


def old_seg(h):
    return "".join(l[1:] for l in h["body"] if l[:1] in (" ", "-")).encode()


def new_seg(h):
    return "".join(l[1:] for l in h["body"] if l[:1] in (" ", "+")).encode()


def render(header, hunks):
    return header + "".join(h["header_line"] + "".join(h["body"]) for h in hunks)


def classify(hunks):
    t232, foreign = [], []
    for i, h in enumerate(hunks, start=1):
        (t232 if i in T232_HUNKS else foreign).append((i, h))
    return t232, foreign


FOREIGN_CODE_CATEGORIES = [
    ("AsCte-call", r"\.AsCte\("),
    ("WithAlias-call", r"\.WithAlias\("),
    ("Alias-ref", r"Alias\.[A-Za-z]+"),
    ("alias-member-read",
     r"\.[A-Z][A-Za-z]*\.(?:CustomerID|CustomerName|SalesPersonID|LifetimeValue|"
     r"LastPurchaseDate|DaysSinceLastOrder|FirstName|LastName|BusinessEntityId)"),
    ("CustomerMetricRow", r"CustomerMetricRow"),
]
FOREIGN_META_CATEGORIES = [
    ("issue160-comment", r"#160"),
    ("using-import", r"^using NextORM\.(?:Generated|SqlServer)"),
]


def changes_in(h):
    """Enumerate foreign edit sites inside a foreign hunk.

    `changes` = code-surface edit sites (comment-only lines are counted separately as
    `comment_lines`; `#160` comments and the two `using` imports are `supporting`).
    """
    changes, supporting, comment_lines = [], [], 0
    for l in h["body"]:
        if not l.startswith("+"):
            continue
        text = l[1:].rstrip("\r\n")
        stripped = text.strip()
        if stripped.startswith("//"):
            comment_lines += 1
            for cat, rx in FOREIGN_META_CATEGORIES:
                for m in re.finditer(rx, text):
                    supporting.append({"category": cat, "token": m.group(0).strip(),
                                       "line": stripped[:160]})
            continue
        for cat, rx in FOREIGN_CODE_CATEGORIES:
            for m in re.finditer(rx, text):
                changes.append({"category": cat, "token": m.group(0).strip(),
                                "line": stripped[:160]})
        for cat, rx in FOREIGN_META_CATEGORIES:
            for m in re.finditer(rx, text):
                supporting.append({"category": cat, "token": m.group(0).strip(),
                                   "line": stripped[:160]})
    return changes, supporting, comment_lines


def apply_patch(patch_text, base_bytes, label, log):
    tmp = tempfile.mkdtemp(prefix="D232-recon-%s-" % label, dir=TMP_ROOT)
    path = os.path.join(tmp, FILE)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(base_bytes)
    pf = os.path.join(tmp, "p.patch")
    with open(pf, "wb") as f:
        f.write(patch_text.encode("utf-8"))
    chk = subprocess.run(["git", "apply", "--check", pf], cwd=tmp, capture_output=True)
    res = {"label": label, "check_exit": chk.returncode,
           "check_stderr": chk.stderr.decode(errors="replace").strip()}
    if chk.returncode != 0:
        res["apply_exit"] = None
        res["sha256"] = None
        res["tmpdir"] = tmp
        log.append(res)
        return None, tmp
    ap = subprocess.run(["git", "apply", pf], cwd=tmp, capture_output=True)
    data = open(path, "rb").read()
    res["apply_exit"] = ap.returncode
    res["apply_stdout"] = ap.stdout.decode(errors="replace").strip()
    res["apply_stderr"] = ap.stderr.decode(errors="replace").strip()
    res["sha256"] = sha256(data)
    res["bytes"] = len(data)
    res["blob"] = subprocess.run(["git", "hash-object", path], cwd=tmp,
                                 capture_output=True).stdout.decode().strip()
    res["tmpdir"] = tmp
    log.append(res)
    return data, tmp


def main():
    parent = rev(COMMIT + "^")
    target_blob = blob(COMMIT)
    parent_blob = blob(parent)
    full = run(["git", "diff", parent, COMMIT, "--", FILE]).stdout.decode("utf-8")
    header, hunks = parse_patch(full)
    t232, foreign = classify(hunks)

    t232_patch = render(header, [h for _, h in t232])
    foreign_patch = render(header, [h for _, h in foreign])

    t232_path = os.path.join(OUT, "t232.patch")
    foreign_path = os.path.join(OUT, "foreign.patch")
    full_path = os.path.join(OUT, "001f9c9c.full.patch")
    for p, txt in ((t232_path, t232_patch), (foreign_path, foreign_patch), (full_path, full)):
        with open(p, "w", encoding="utf-8", newline="") as f:
            f.write(txt)

    # per-hunk patches
    per = os.path.join(OUT, "hunks")
    os.makedirs(per, exist_ok=True)
    for i, h in enumerate(hunks, start=1):
        side = "t232" if i in T232_HUNKS else "foreign"
        with open(os.path.join(per, "h%02d-%s.patch" % (i, side)), "w",
                  encoding="utf-8", newline="") as f:
            f.write(render(header, [h]))

    # disjointness: old and new line ranges must not intersect across the two sets
    t_old = set()
    t_new = set()
    for _, h in t232:
        t_old |= set(range(h["old_start"], h["old_start"] + h["old_len"]))
        t_new |= set(range(h["new_start"], h["new_start"] + h["new_len"]))
    f_old = set()
    f_new = set()
    for _, h in foreign:
        f_old |= set(range(h["old_start"], h["old_start"] + h["old_len"]))
        f_new |= set(range(h["new_start"], h["new_start"] + h["new_len"]))

    base = show(parent)
    log = []
    # order 1: t232 then foreign
    mid1, d1 = apply_patch(t232_patch, base, "t232-first", log)
    if mid1 is not None:
        final1, d1b = apply_patch(foreign_patch, mid1, "t232-then-foreign", log)
    else:
        final1 = None
    # order 2: foreign then t232
    mid2, d2 = apply_patch(foreign_patch, base, "foreign-first", log)
    if mid2 is not None:
        final2, d2b = apply_patch(t232_patch, mid2, "foreign-then-t232", log)
    else:
        final2 = None
    # full-patch sanity
    fullfinal, d3 = apply_patch(full, base, "full-patch", log)

    target_sha = sha256(show(COMMIT))
    recon = {
        "parent": {"rev": parent, "blob": parent_blob, "sha256": sha256(base)},
        "target": {"rev": rev(COMMIT), "blob": target_blob, "sha256": target_sha},
        "attempts": log,
        "order_t232_then_foreign_sha256": sha256(final1) if final1 is not None else None,
        "order_foreign_then_t232_sha256": sha256(final2) if final2 is not None else None,
        "full_patch_sha256": sha256(fullfinal) if fullfinal is not None else None,
        "old_ranges_disjoint": not (t_old & f_old),
        "new_ranges_disjoint": not (t_new & f_new),
        "hunk_count_total": len(hunks),
        "hunk_count_t232": len(t232),
        "hunk_count_foreign": len(foreign),
    }
    ok = (
        recon["old_ranges_disjoint"] and recon["new_ranges_disjoint"]
        and recon["order_t232_then_foreign_sha256"] == target_sha
        and recon["order_foreign_then_t232_sha256"] == target_sha
        and recon["full_patch_sha256"] == target_sha
    )
    recon["verified"] = ok

    for p in (d1, d2, d3):
        try:
            shutil.rmtree(p, ignore_errors=True)
        except Exception:
            pass
    # remove temp dirs from the log before persisting (keep them referenced in stdout)
    for e in log:
        e.pop("tmpdir", None)

    # hunk inventory for ownership.json
    def entry(i, h, side):
        e = {
            "ordinal": i,
            "hunk_header": h["header_line"].rstrip("\n"),
            "old_range": "%d,%d" % (h["old_start"], h["old_len"]),
            "new_range": "%d,%d" % (h["new_start"], h["new_len"]),
            "side": side,
            "before_hash": sha256(old_seg(h)),
            "after_hash": sha256(new_seg(h)),
            "before_bytes": len(old_seg(h)),
            "after_bytes": len(new_seg(h)),
        }
        return e

    t232_entries = [entry(i, h, "t232") for i, h in t232]
    foreign_entries = [entry(i, h, "foreign") for i, h in foreign]
    total_foreign_changes = 0
    total_foreign_supporting = 0
    total_foreign_comment_lines = 0
    for e, (i, h) in zip(foreign_entries, foreign):
        ch, sup, comments = changes_in(h)
        e["changes"] = ch
        e["change_count"] = len(ch)
        e["supporting"] = sup
        e["supporting_count"] = len(sup)
        e["comment_lines"] = comments
        total_foreign_changes += len(ch)
        total_foreign_supporting += len(sup)
        total_foreign_comment_lines += comments

    inventory = {
        "generated_by": "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership/split_hunks.py",
        "commit": rev(COMMIT),
        "parent": parent,
        "file": FILE,
        "line_endings": "CRLF",
        "blob_parent": parent_blob,
        "blob_commit": target_blob,
        "t232_hunks": t232_entries,
        "foreign_hunks": foreign_entries,
        "t232_hunk_count": len(t232_entries),
        "foreign_hunk_count": len(foreign_entries),
        "t232_changed_lines": {"added": sum(1 for l in t232_patch.splitlines(keepends=True)
                                            if l.startswith("+") and not l.startswith("+++")),
                               "removed": sum(1 for l in t232_patch.splitlines(keepends=True)
                                              if l.startswith("-") and not l.startswith("---"))},
        "foreign_changed_lines": {"added": sum(1 for l in foreign_patch.splitlines(keepends=True)
                                               if l.startswith("+") and not l.startswith("+++")),
                                  "removed": sum(1 for l in foreign_patch.splitlines(keepends=True)
                                                 if l.startswith("-") and not l.startswith("---"))},
        "foreign_discrete_changes": total_foreign_changes,
        "foreign_supporting_changes": total_foreign_supporting,
        "foreign_comment_lines": total_foreign_comment_lines,
        "disjointness": {
            "old_ranges_disjoint": recon["old_ranges_disjoint"],
            "new_ranges_disjoint": recon["new_ranges_disjoint"],
        },
        "reconstruction": recon,
    }
    with open(os.path.join(OUT, "hunk-inventory.json"), "w", encoding="utf-8") as f:
        json.dump(inventory, f, indent=1, sort_keys=True)
        f.write("\n")
    with open(os.path.join(OUT, "reconstruction.json"), "w", encoding="utf-8") as f:
        json.dump(recon, f, indent=1, sort_keys=True)
        f.write("\n")

    print(json.dumps({
        "hunks": len(hunks), "t232": len(t232), "foreign": len(foreign),
        "old_disjoint": recon["old_ranges_disjoint"],
        "new_disjoint": recon["new_ranges_disjoint"],
        "t232_then_foreign": recon["order_t232_then_foreign_sha256"],
        "foreign_then_t232": recon["order_foreign_then_t232_sha256"],
        "full": recon["full_patch_sha256"],
        "target": recon["target"]["sha256"],
        "target_blob": target_blob,
        "verified": ok,
        "foreign_discrete_changes": total_foreign_changes,
    }, indent=1))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
