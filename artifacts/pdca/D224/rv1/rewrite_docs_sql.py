#!/usr/bin/env python3
"""D224 (#224) doc transformer: put generated SELECT-mode FROM/JOIN on their own
line with exactly one leading space, in fenced ```sql blocks and in ```csharp
comment depictions under docs/ (EN + RU).

Raw SQL fragments shown inside a C# fence in the same page (e.g. the argument of
FromSql/WithSql) are located in the sql block and excluded from the transform, so
verbatim fragments are left untouched.

Every generated-SELECT sample is normalised, including "compacted illustrations"
that also keep a later clause (`where`, `group by`, ...) on the SELECT line: the
earlier revision skipped those blocks as a carve-out, which left `from`/`join`
inline and contradicted the layout note. Only `from`/`join` placement is changed;
the remaining inline clause is out of this round's scope.

Two passes:

1. ```sql fences — a `from`/`join` clause that is inline or that starts a line
   with indentation other than the layout's single space gets its own line with
   exactly one leading space (`\\n from` / `\\n join`).
2. ```csharp fences — a run of contiguous `//` comment lines that depicts
   generated SQL (a `select`/`with`/`insert`/... statement) is re-rendered: each
   `from`/`join` clause starts its own comment line, emitted as `// ` + clause
   (the comment marker's single space is the layout's one leading space). Prose
   comments are never touched: a run is rewritten only when `insert_positions`
   finds a clause to move, and raw-mirror descriptions (`... -> select ... from
   ...`) are skipped outright because opaque raw input is never reflowed.
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

# A wrapped prose/comment sentence that sits inside a ```sql fence without a `--`
# prefix (the `-- SQLite: self-` / `join of one descriptor ...` pair in
# docs/guide/08-cte.md and its RU twin). It is not a SQL clause, so it is
# preserved verbatim instead of being re-laid out.
NON_CLAUSE_LINES = {
    "join of one descriptor -> one declaration, two aliases",
    "join одного дескриптора -> одно объявление, два алиаса",
}


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
        if c == "." and s.startswith("...", i) and (i == 0 or s[i - 1] == "\n"):
            # An abbreviated illustration: a leading `...` stands for the omitted
            # select list, so the statement is in SELECT (and therefore JOIN)
            # context. Only the line-initial ellipsis counts; an inline `...`
            # (e.g. `select ... from x`, `(select ...) as t`) never opens one.
            if depth not in select_depths:
                select_depths.append(depth)
            join_depths.add(depth)
            i += 3
            continue
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
                if needs_change(s, i):
                    out.append(i)
        elif w in ("join", "apply") and depth in join_depths:
            start = phrase_start(s, i)
            if needs_change(s, start):
                out.append(start)
        i = m.end()
    return sorted(set(out))


def needs_change(s, t):
    """A clause at word/phrase start `t` needs the layout fix when it is inline
    (no newline before it) or when it starts a line with any leading indentation
    other than the layout's single space. A clause already at the start of a line
    with exactly one leading space is left alone."""
    j = t
    while j > 0 and s[j - 1] in " \t":
        j -= 1
    if j > 0 and s[j - 1] == "\n":
        return s[j:t] != " "
    return True


def apply_layout(src, starts):
    """Render every clause start with the generated layout: on its own line with
    exactly one leading space (`\\n from`, `\\n join`), dropping any inline or
    over-indented run of spaces so no whitespace-only line is left behind."""
    for t in sorted(set(starts), reverse=True):
        j = t
        while j > 0 and src[j - 1] in " \t":
            j -= 1
        if j > 0 and src[j - 1] == "\n":
            # already at a line start: normalise the indent to one space
            src = src[:j] + " " + src[t:]
        else:
            src = src[:j] + "\n " + src[t:]
    return src


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


COMMENT = re.compile(r"^(\s*)//(.*)$")


def rewrite_comment_line(run):
    """Rewrite one run of contiguous `//` comment lines that depicts generated
    SQL. Returns (new_lines, count, before, after)."""
    indent = COMMENT.match(run[0]).group(1)
    bodies = []
    for line in run:
        body = COMMENT.match(line).group(2)
        # drop the marker's own single separator space, keep any further indent
        bodies.append(body[1:] if body.startswith(" ") else body)
    src = "\n".join(bodies)
    # `... -> select ... from ...` describes an opaque raw SQL argument; opaque
    # raw input is never reflowed (contract).
    if "->" in src:
        return run, 0, None, None
    pos = insert_positions(src)
    if not pos:
        return run, 0, None, None
    new_src = apply_layout(src, pos)
    new = [indent + "// " + line.strip() for line in new_src.split("\n")]
    return new, len(pos), run, new


def rewrite_comment_runs(text):
    """Normalise generated-SQL `//` depictions inside ```csharp fences."""
    lines = text.split("\n")
    out, kind, i, total, reports = [], None, 0, 0, []
    while i < len(lines):
        s = lines[i].strip()
        if s.startswith("```"):
            kind = None if kind is not None else s[3:].strip().lower()
            out.append(lines[i]); i += 1; continue
        if kind in ("csharp", "cs") and COMMENT.match(lines[i]):
            run = []
            while i < len(lines) and COMMENT.match(lines[i]):
                run.append(lines[i]); i += 1
            new, n, before, after = rewrite_comment_line(run)
            if n:
                total += n
                reports.append((before, after))
            out.extend(new)
            continue
        out.append(lines[i]); i += 1
    return "\n".join(out), total, reports


def main():
    apply = "--apply" in sys.argv
    files = [Path(a) for a in sys.argv[1:] if not a.startswith("--")]
    if not files:
        files = [Path(p) for p in glob.glob(str(ROOT / "docs" / "**" / "*.md"), recursive=True)
                 if "/specs/" not in p and "/_site/" not in p and "/api/" not in p]
    total = changed = 0
    for p in sorted(files):
        text = p.read_bytes().decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
        original = text
        raws = csharp_raw_sql(text)
        text, c_inserts, c_reports = rewrite_comment_runs(text)
        lines = text.split("\n")
        out, i, inserts = [], 0, c_inserts
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
                def line_at(q):
                    a = src.rfind("\n", 0, q) + 1
                    b = src.find("\n", q)
                    return src[a:] if b < 0 else src[a:b]

                pos = [q for q in insert_positions(src)
                       if not any(a <= q < b for a, b in excl)
                       and line_at(q).strip() not in NON_CLAUSE_LINES]
                src = apply_layout(src, pos)
                inserts += len(pos)
                out.extend(src.split("\n"))
                continue
            out.append(lines[i]); i += 1
        new = "\n".join(out)
        if new != original:
            changed += 1; total += inserts
            print(f"{'applied' if apply else 'would'} {inserts:4d}  {p.relative_to(ROOT)}")
            for before, after in c_reports:
                for label, block in (("-", before), ("+", after)):
                    for line in block:
                        print(f"    {label} {line}")
            if apply:
                p.write_bytes(new.replace("\n", "\r\n").encode("utf-8"))
    print(f"--- {'applied' if apply else 'dry-run'}: {total} insertion(s) in {changed} file(s)")


if __name__ == "__main__":
    main()
