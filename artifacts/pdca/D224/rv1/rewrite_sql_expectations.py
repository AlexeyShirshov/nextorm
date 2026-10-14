#!/usr/bin/env python3
"""D224 (#224) — rewrite exact-string SQL expectations for the unconditional
``\n`` before ``from``/``join`` contract.

Deterministic transform of a generated-SQL string: insert LF immediately before
the ``from`` of every SELECT and before every join phrase (``join``, ``left join``,
``cross join lateral``, ``cross apply`` ...). DML ``from`` (update/delete/merge)
and joins outside a SELECT are left untouched, and raw/opaque SQL passed to the
builder (``FromSql``/``RawSqlSource``/...) is never reflowed.

Usage:  rewrite_sql_expectations.py [--apply] [<file> ...]
        (no file args => every target test file below)
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]

TARGET_DIRS = [
    "tests/nextorm.core.tests",
    "tests/nextorm.alias.tests",
    "tests/nextorm.publicextensibility.tests",
    "tests/nextorm.clickhouse.tests",
    "tests/nextorm.clickhouse.extensions.tests",
    "tests/nextorm.mariadb.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.entityframeworkcore.tests",
]

WORD = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
MODIFIERS = {
    "left", "right", "full", "inner", "cross", "outer", "global", "any", "all",
    "asof", "semi", "anti", "paste", "natural",
}
# Argument positions that carry raw/opaque SQL: never reflow.
RAW_ARG = re.compile(
    r"(FromSql|FromSqlRaw|FromRaw|ExecuteRaw|ExecuteScalar|ExecuteReader|CommandText\s*=)\s*\"?$"
)
RAW_ARG_LITERAL = re.compile(r"(FromSql|FromSqlRaw|FromRaw)\s*\(\s*\"?$")

SIMPLE_ESCAPES = {
    "n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a", "b": "\b",
    "f": "\f", "v": "\v", "\\": "\\", '"': '"', "'": "'",
}


def phrase_start(s: str, word_start: int) -> int:
    """Walk back over join modifiers so the LF lands before ``left join``."""
    start = word_start
    pos = word_start
    while True:
        # skip whitespace backwards
        j = pos - 1
        while j >= 0 and s[j] in " \t\n":
            j -= 1
        if j < 0:
            break
        m = None
        k = j
        while k >= 0 and (s[k].isalnum() or s[k] == "_"):
            k -= 1
        cand = s[k + 1:j + 1]
        if cand.lower() in MODIFIERS:
            start = k + 1
            pos = k + 1
        else:
            break
    return start


def insert_positions(s: str) -> list[int]:
    """Decoded-string indices at which an LF must be inserted."""
    out: list[int] = []
    # A literal may be a *fragment* of a larger statement (sliced out of a ``"..." + "..."``
    # concatenation at an arbitrary point). Shift the depth so the enclosing statement's level
    # is 0 again, and seed a select context for a fragment that starts inside parentheses or
    # begins exactly at a ``from`` clause.
    min_depth = 0
    depth = 0
    for ch in s:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            min_depth = min(min_depth, depth)
    shift = -min_depth
    lead = WORD.search(s)
    first_word = lead.group(0).lower() if lead else ""
    select_depths: list[int] = []
    join_depths: set[int] = set()
    if shift > 0:
        select_depths.append(0)
    elif first_word == "from":
        select_depths.append(0)
    depth = 0
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c == "(":
            depth += 1
            i += 1
            continue
        if c == ")":
            depth -= 1
            level = depth + shift
            select_depths = [d for d in select_depths if d <= level]
            join_depths = {d for d in join_depths if d <= level}
            i += 1
            continue
        m = WORD.match(s, i)
        if m is None:
            i += 1
            continue
        w = m.group(0).lower()
        level = depth + shift
        if w == "select":
            select_depths.append(level)
        elif w == "from":
            if level in select_depths:
                select_depths.remove(level)
                join_depths.add(level)
                p = i - 1 if i > 0 and s[i - 1] == " " else i
                if p > 0 and s[p - 1] == "\n":
                    pass  # already on its own line (idempotent re-run)
                else:
                    out.append(p)
        elif w in ("join", "apply") and level in join_depths:
            start = phrase_start(s, i)
            p = start - 1 if start > 0 and s[start - 1] == " " else start
            if p > 0 and s[p - 1] == "\n":
                pass
            else:
                out.append(p)
        i = m.end()
    return sorted(set(out))


def decode(raw: str) -> str:
    out: list[str] = []
    i = 0
    while i < len(raw):
        c = raw[i]
        if c != "\\":
            out.append(c)
            i += 1
            continue
        nxt = raw[i + 1]
        if nxt in SIMPLE_ESCAPES:
            out.append(SIMPLE_ESCAPES[nxt])
            i += 2
        elif nxt == "u":
            out.append(chr(int(raw[i + 2:i + 6], 16)))
            i += 6
        elif nxt == "U":
            out.append(chr(int(raw[i + 2:i + 10], 16)))
            i += 10
        elif nxt == "x":
            j = i + 2
            hexs = ""
            while j < len(raw) and len(hexs) < 4 and raw[j] in "0123456789abcdefABCDEF":
                hexs += raw[j]
                j += 1
            out.append(chr(int(hexs, 16)))
            i = j
        else:
            out.append(nxt)
            i += 2
    return "".join(out)


def raw_index_map(raw: str) -> list[int]:
    """Map decoded char index -> raw index (start of that char's encoding)."""
    mapping = [0]
    i = 0
    while i < len(raw):
        c = raw[i]
        if c != "\\":
            i += 1
        else:
            nxt = raw[i + 1]
            if nxt in SIMPLE_ESCAPES:
                i += 2
            elif nxt == "u":
                i += 6
            elif nxt == "U":
                i += 10
            elif nxt == "x":
                j = i + 2
                hexs = ""
                while j < len(raw) and len(hexs) < 4 and raw[j] in "0123456789abcdefABCDEF":
                    hexs += raw[j]
                    j += 1
                i = j
            else:
                i += 2
        mapping.append(i)
    return mapping


def scan(text: str):
    """Yield (raw_start, raw_end, content_start, content_end, kind) for literals."""
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            j = text.find("\n", i)
            i = n if j < 0 else j + 1
            continue
        if c == "/" and i + 1 < n and text[i + 1] == "*":
            j = text.find("*/", i + 2)
            i = n if j < 0 else j + 2
            continue
        if c == "'":
            j = i + 1
            while j < n:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == "'":
                    break
                j += 1
            i = j + 1
            continue
        if c == "@" and i + 1 < n and text[i + 1] == '"':
            j = i + 2
            while j < n:
                if text[j] == '"':
                    if j + 1 < n and text[j + 1] == '"':
                        j += 2
                        continue
                    break
                j += 1
            yield (i, j + 1, i + 2, j, "verbatim")
            i = j + 1
            continue
        if c == "$" or (c == "@" and i + 1 < n and text[i + 1] == "$"):
            # interpolated string: consume it without transforming
            j = i + 1 if c == "$" else i + 2
            if j < n and text[j] == "@":
                j += 1
            if j < n and text[j] == '"':
                j += 1
                while j < n:
                    if text[j] == "\\":
                        j += 2
                        continue
                    if text[j] == '"':
                        break
                    j += 1
                i = j + 1
                continue
            i += 1
            continue
        if c == '"':
            if text.startswith('"""', i):
                j = text.find('"""', i + 3)
                i = n if j < 0 else j + 3
                continue
            j = i + 1
            while j < n:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == '"':
                    break
                j += 1
            yield (i, j + 1, i + 1, j, "regular")
            i = j + 1
            continue
        i += 1


def collect_raw_texts(text: str) -> list[tuple[int, str]]:
    """(offset, value) of string values used as raw/opaque SQL arguments in this file."""
    found = []
    for (_, _, cs, ce, kind) in scan(text):
        if kind != "regular":
            continue
        prefix = text[max(0, cs - 85):cs]
        if RAW_ARG.search(prefix) or RAW_ARG_LITERAL.search(prefix):
            found.append((cs, decode(text[cs:ce])))
    return found


# A raw-SQL input and the assertion that quotes it live in the same test method; only raw
# texts near the literal count, so an unrelated raw input elsewhere in the file cannot
# suppress (or protect) an unrelated generated-SQL expectation.
RAW_WINDOW = 1500


def transform_literal(value: str, raw_texts, offset: int) -> list[int] | None:
    """Return the decoded-string indices at which an LF must be inserted."""
    spans = []
    for (rt_off, rt) in raw_texts:
        if not rt or abs(rt_off - offset) > RAW_WINDOW:
            continue
        start = value.find(rt)
        while start >= 0:
            spans.append((start, start + len(rt)))
            start = value.find(rt, start + 1)
    positions = [
        p for p in insert_positions(value)
        if not any(a - 1 <= p < b for (a, b) in spans)
    ]
    return positions or None


def rewrite_file(path: Path, apply: bool) -> int:
    text = path.read_bytes().decode("utf-8")
    raw_texts = collect_raw_texts(text)
    edits = []
    for (_, _, cs, ce, kind) in scan(text):
        if kind != "regular":
            continue
        raw = text[cs:ce]
        value = decode(raw)
        lowered = value.lower()
        if not any(k in lowered for k in ("select", "from", "join")):
            continue
        if RAW_ARG.search(text[max(0, cs - 85):cs]):
            continue
        positions = transform_literal(value, raw_texts, cs)
        if not positions:
            continue
        mapping = raw_index_map(raw)
        new_raw = raw
        for p in sorted((mapping[min(p, len(raw))] for p in positions), reverse=True):
            new_raw = new_raw[:p] + "\\n" + new_raw[p:]
        edits.append((cs, ce, new_raw))
    if not edits:
        return 0
    for (cs, ce, new_raw) in reversed(edits):
        text = text[:cs] + new_raw + text[ce:]
    if apply:
        path.write_bytes(text.encode("utf-8"))
    return len(edits)


def iter_targets():
    for d in TARGET_DIRS:
        base = ROOT / d
        if not base.is_dir():
            continue
        for p in sorted(base.rglob("*.cs")):
            if any(part in {"bin", "obj"} for part in p.parts):
                continue
            yield p


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--apply"]
    apply = "--apply" in sys.argv[1:]
    files = [Path(a) for a in args] if args else list(iter_targets())
    total = 0
    changed = 0
    for p in files:
        if not p.is_file():
            print(f"skip (missing): {p}")
            continue
        count = rewrite_file(p, apply)
        if count:
            changed += 1
            total += count
            print(f"{'rewrote' if apply else 'would rewrite'} {count:4d} literal(s)  {p}")
    print(f"--- {'applied' if apply else 'dry-run'}: {total} literal(s) in {changed} file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
