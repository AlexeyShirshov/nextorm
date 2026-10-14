#!/usr/bin/env python3
"""D224 (#224) doc transformer: insert LF before generated SELECT-mode FROM/JOIN
inside fenced ```sql blocks under docs/ (EN + RU).

Raw SQL fragments shown inside a C# fence in the same page (e.g. the argument of
FromSql/WithSql) are located in the sql block and excluded from the transform, so
verbatim fragments are left untouched.
"""
from __future__ import annotations
import glob, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]

MODIFIERS = {"left", "right", "full", "inner", "cross", "outer", "global", "any",
             "all", "asof", "semi", "anti", "paste", "natural", "array"}
WORD = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
SIMPLE_ESCAPES = {"n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a", "b": "\b",
                  "f": "\f", "v": "\v", "\\": "\\", '"': '"', "'": "'"}


def decode(raw):
    out, i = [], 0
    while i < len(raw):
        c = raw[i]
        if c != "\\":
            out.append(c); i += 1; continue
        nxt = raw[i + 1]
        if nxt in SIMPLE_ESCAPES:
            out.append(SIMPLE_ESCAPES[nxt]); i += 2
        elif nxt == "u":
            out.append(chr(int(raw[i + 2:i + 6], 16))); i += 6
        else:
            out.append(nxt); i += 2
    return "".join(out)


def phrase_start(s, word_start):
    start = pos = word_start
    while True:
        j = pos - 1
        while j >= 0 and s[j] in " \t\n":
            j -= 1
        if j < 0:
            break
        k = j
        while k >= 0 and (s[k].isalnum() or s[k] == "_"):
            k -= 1
        cand = s[k + 1:j + 1]
        if cand.lower() in MODIFIERS:
            start = pos = k + 1
        else:
            break
    return start


def insert_positions(s):
    out, select_depths, join_depths, depth, i, n = [], [], set(), 0, 0, len(s)
    while i < n:
        c = s[i]
        if c == "(":
            depth += 1; i += 1; continue
        if c == ")":
            depth -= 1
            select_depths = [d for d in select_depths if d <= depth]
            join_depths = {d for d in join_depths if d <= depth}
            i += 1; continue
        m = WORD.match(s, i)
        if m is None:
            i += 1; continue
        w = m.group(0).lower()
        if w == "select":
            select_depths.append(depth)
        elif w == "from":
            if depth in select_depths:
                select_depths.remove(depth)
                join_depths.add(depth)
                p = i - 1 if i > 0 and s[i - 1] == " " else i
                if not (p > 0 and s[p - 1] == "\n"):
                    out.append(p)
        elif w in ("join", "apply") and depth in join_depths:
            start = phrase_start(s, i)
            p = start - 1 if start > 0 and s[start - 1] == " " else start
            if not (p > 0 and s[p - 1] == "\n"):
                out.append(p)
        i = m.end()
    return sorted(set(out))


def _line_start(s, i):
    j = i - 1
    while j >= 0 and s[j] in " \t":
        j -= 1
    return j < 0 or s[j] == "\n"


def _next_word(s, i):
    m = WORD.match(s, i)
    return m.group(0).lower() if m else ""


def is_compacted(src):
    """True when a generated continuation clause sits on the same line as the
    preceding SQL: the page shows a compacted illustration, not exact output."""
    depth, i, n, seen_select = 0, 0, len(src), False
    query_depths = set()
    while i < n:
        c = src[i]
        if c == "(":
            j = i + 1
            while j < n and src[j] in " \t":
                j += 1
            if _next_word(src, j) in ("select", "with"):
                query_depths.add(depth + 1)
            depth += 1; i += 1; continue
        if c == ")":
            query_depths.discard(depth)
            depth -= 1; i += 1; continue
        if c == ";":
            seen_select = False; i += 1; continue
        m = WORD.match(src, i)
        if m is None:
            i += 1; continue
        w = m.group(0).lower()
        if depth == 0 or depth in query_depths:
            if w == "select":
                seen_select = True
            elif seen_select and w in ("where", "having", "window", "prewhere",
                                       "limit", "offset", "fetch", "union", "except",
                                       "settings"):
                if not _line_start(src, i):
                    return True
            elif seen_select and w in ("group", "order"):
                j = m.end()
                while j < n and src[j] in " \t":
                    j += 1
                if _next_word(src, j) == "by" and not _line_start(src, i):
                    return True
            elif seen_select and w == "for":
                j = m.end()
                while j < n and src[j] in " \t":
                    j += 1
                if _next_word(src, j) in ("json", "xml", "update", "share") and not _line_start(src, i):
                    return True
        i = m.end()
    return False


def fences(text):
    lines = text.split("\n")
    i, cur = 0, None
    while i < len(lines):
        s = lines[i].strip()
        if s.startswith("```"):
            if cur is None:
                cur = (s[3:].strip().lower(), i, [])
            else:
                yield cur[0], cur[1], i, cur[2]
                cur = None
        elif cur is not None:
            cur[2].append(lines[i])
        i += 1


def csharp_raw_sql(text):
    """Whitespace-normalised SQL literals that appear inside C# fences."""
    vals = []
    for kind, _, _, body in fences(text):
        if kind not in ("csharp", "cs"):
            continue
        block = "\n".join(body)
        for m in re.finditer(r'"((?:[^"\\]|\\.)*)"', block):
            val = decode(m.group(1))
            if "select" not in val.lower():
                continue
            norm = re.sub(r"\s+", " ", val).strip()
            if len(norm) > 15:
                vals.append(norm)
    return vals


def main():
    apply = "--apply" in sys.argv
    files = [Path(a) for a in sys.argv[1:] if a != "--apply"]
    if not files:
        files = [Path(p) for p in glob.glob(str(ROOT / "docs" / "**" / "*.md"), recursive=True)
                 if "/specs/" not in p and "/_site/" not in p and "/api/" not in p]
    total = changed = 0
    for p in sorted(files):
        text = p.read_bytes().decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
        raws = csharp_raw_sql(text)
        lines = text.split("\n")
        out, i, inserts = [], 0, 0
        while i < len(lines):
            if lines[i].strip().lower().startswith("```sql"):
                out.append(lines[i]); i += 1
                body = []
                while i < len(lines) and not lines[i].strip().startswith("```"):
                    body.append(lines[i]); i += 1
                src = "\n".join(body)
                norm = re.sub(r"\s+", " ", src)
                spans = []
                for r in raws:
                    start = norm.find(r)
                    while start >= 0:
                        spans.append((start, start + len(r)))
                        start = norm.find(r, start + 1)
                # positions in src coordinates: map norm index -> src index
                # (only used to exclude; build a norm->src map)
                nmap, k = [], 0
                while k < len(src):
                    if src[k].isspace():
                        if nmap and nmap[-1] == k:
                            pass
                        j = k
                        while j < len(src) and src[j].isspace():
                            j += 1
                        nmap.append(k)
                        k = j
                    else:
                        nmap.append(k)
                        k += 1
                nmap.append(len(src))

                def norm_to_src(pos):
                    if pos < len(nmap):
                        return nmap[pos]
                    return len(src)

                excl = []
                for a, b in spans:
                    excl.append((norm_to_src(a), norm_to_src(b)))
                masked = list(src)
                for a, b in excl:
                    for q in range(a, min(b, len(masked))):
                        masked[q] = " "
                pos = [q for q in insert_positions(src)
                       if not any(a - 1 <= q < b for a, b in excl)]
                if is_compacted("".join(masked)):
                    pos = []
                for q in sorted(pos, reverse=True):
                    src = src[:q] + "\n" + src[q:]
                inserts += len(pos)
                out.extend(src.split("\n"))
                continue
            out.append(lines[i]); i += 1
        new = "\n".join(out)
        if new != text:
            changed += 1; total += inserts
            print(f"{'applied' if apply else 'would'} {inserts:4d}  {p.relative_to(ROOT)}")
            if apply:
                p.write_bytes(new.replace("\n", "\r\n").encode("utf-8"))
    print(f"--- {'applied' if apply else 'dry-run'}: {total} insertion(s) in {changed} file(s)")


if __name__ == "__main__":
    main()
