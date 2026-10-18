#!/usr/bin/env python3
"""Mechanical extraction of T245 entry set-operation semantics (D245-1 preflight).

Reads the provider SqlGenerationTests set-operation tests, the core in-memory set-op
tests and the integration CommonTestSuite set-op cases; writes sql-entry.json and
results-entry.json with verbatim expected literals plus sha256 of every source file.
No test is executed here.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

ROOT = Path("/home/alex/sources/nextorm")
SEM = ROOT / ".pdca/T245/rv1/semantics"

PROVIDERS = {
    "sqlite": ROOT / "tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs",
    "postgres": ROOT / "tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs",
    "sqlserver": ROOT / "tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs",
    "mariadb": ROOT / "tests/nextorm.mariadb.tests/SqlGeneration/SqlGenerationTests.cs",
}
INMEMORY = ROOT / "tests/nextorm.core.tests/InMemory/InMemoryTests.cs"
COMMON_SETOPS = ROOT / "tests/nextorm.integration.tests/CommonTestSuite/Queries/CommonTestSuite.SetOperations.cs"
CORE_T240 = ROOT / "tests/nextorm.core.tests/Builders/T240CommandConstructionTests.cs"
CORE_RECIPE = ROOT / "tests/nextorm.core.tests/Caching/CachedPreparationRecipeTests.cs"

SETOP_RE = re.compile(r"\.(UnionAll|Union|IntersectAll|Intersect|ExceptAll|Except)\s*\(")
METHOD_RE = re.compile(r"\[Fact\]\s*\n\s*public void (\w+)\(\)\s*\n\s*\{(.*?)\n    \}", re.S)
SQL_LITERAL_RE = re.compile(r'\.Should\(\)\.Be\("((?:[^"\\]|\\.)*)"\)', re.S)
THROW_MSG_RE = re.compile(r'Throw<NotSupportedException>\(\)\.WithMessage\("((?:[^"\\]|\\.)*)"\)')
BE_EQUIV_RE = re.compile(r"\.Should\(\)\.BeEquivalentTo\((.*?)\);", re.S)
HAVE_COUNT_RE = re.compile(r"\.Should\(\)\.HaveCount\((\d+)\)")


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def decode_cs_literal(raw: str) -> str:
    # Only the escapes used by the expected SQL literals and the exception patterns.
    return (
        raw.replace("\\\\", "\x00")
        .replace('\\"', '"')
        .replace("\\n", "\n")
        .replace("\\r", "\r")
        .replace("\\t", "\t")
        .replace("\x00", "\\")
    )


def line_of(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def parse_methods(path: Path, require_name_setop: bool = True):
    text = path.read_text(encoding="utf-8-sig")
    methods = []
    for m in METHOD_RE.finditer(text):
        body = m.group(2)
        name_has_setop = any(k in m.group(1) for k in ("Union", "Intersect", "Except"))
        if not SETOP_RE.search(body) or (require_name_setop and not name_has_setop):
            continue
        if "Cte" in m.group(1) or "Recursive" in m.group(1):
            continue
        start = m.start(2)
        body_lines = text.count("\n", 0, start)
        ops = sorted({op for op in SETOP_RE.findall(body)})
        methods.append(
            {
                "test_name": m.group(1),
                "ops": ops,
                "body": body,
                "base_line": body_lines + 1,
                "method_offset": start,
            }
        )
    return text, methods


def sql_entries():
    entries = []
    files = {}
    for provider, path in PROVIDERS.items():
        text, methods = parse_methods(path)
        files[provider] = {"file": str(path.relative_to(ROOT)), "sha256": sha256_file(path)}
        for meth in methods:
            body = meth["body"]
            sql_m = SQL_LITERAL_RE.search(body)
            throw_m = THROW_MSG_RE.search(body)
            if sql_m:
                expected = decode_cs_literal(sql_m.group(1))
                line = line_of(text, meth["method_offset"] + sql_m.start())
                entries.append(
                    {
                        "provider": provider,
                        "test_name": meth["test_name"],
                        "file": str(path.relative_to(ROOT)),
                        "line": line,
                        "ops": meth["ops"],
                        "expectation_kind": "sql-literal",
                        "expected_sql": expected,
                        "expected_sql_sha256": hashlib.sha256(expected.encode("utf-8")).hexdigest(),
                        "expected_exception": None,
                    }
                )
            elif throw_m:
                line = line_of(text, meth["method_offset"] + throw_m.start())
                entries.append(
                    {
                        "provider": provider,
                        "test_name": meth["test_name"],
                        "file": str(path.relative_to(ROOT)),
                        "line": line,
                        "ops": meth["ops"],
                        "expectation_kind": "throws-not-supported",
                        "expected_sql": None,
                        "expected_sql_sha256": None,
                        "expected_exception": {
                            "type": "NotSupportedException",
                            "message_pattern": decode_cs_literal(throw_m.group(1)),
                        },
                    }
                )
            else:
                entries.append(
                    {
                        "provider": provider,
                        "test_name": meth["test_name"],
                        "file": str(path.relative_to(ROOT)),
                        "line": meth["base_line"],
                        "ops": meth["ops"],
                        "expectation_kind": "no-literal",
                        "expected_sql": None,
                        "expected_sql_sha256": None,
                        "expected_exception": None,
                    }
                )

    # Core set-operation tests: no SQL literal, relational/identity assertions only.
    core_relational = []
    for path, tests in (
        (
            CORE_T240,
            {
                "NestedSetOperations_KeepOperandsUnchanged": "operands render unchanged; except SQL differs from union SQL (no literal)",
            },
        ),
        (
            CORE_RECIPE,
            {
                "Recipe_SetOperationKindIsPartOfTheRawGraph": "EXCEPT vs INTERSECT produce different prepared SQL; operand order matters (no literal)",
            },
        ),
    ):
        text = path.read_text(encoding="utf-8-sig")
        files[path.name] = {"file": str(path.relative_to(ROOT)), "sha256": sha256_file(path)}
        for name, expectation in tests.items():
            idx = text.find(name)
            core_relational.append(
                {
                    "provider": "core",
                    "test_name": name,
                    "file": str(path.relative_to(ROOT)),
                    "line": line_of(text, idx),
                    "ops": sorted({op for op in SETOP_RE.findall(text[idx : idx + 3000])}),
                    "expectation_kind": "relational-no-literal",
                    "expected_sql": None,
                    "expected_sql_sha256": None,
                    "expected_exception": None,
                    "expectation": expectation,
                }
            )

    files[INMEMORY.name] = {"file": str(INMEMORY.relative_to(ROOT)), "sha256": sha256_file(INMEMORY)}
    files[COMMON_SETOPS.name] = {"file": str(COMMON_SETOPS.relative_to(ROOT)), "sha256": sha256_file(COMMON_SETOPS)}

    return {
        "task": "T245",
        "unit": "D245-1",
        "captured_at_head": "f82edf36437429e7a96d8a1525059934303f2567",
        "note": "expected_sql is the literal expected string verbatim (C# escape sequences decoded); no test executed",
        "entries": entries + core_relational,
        "source_files": list(files.values()),
    }


def results_entries():
    cases = []
    file_hashes = {}

    text = INMEMORY.read_text(encoding="utf-8-sig")
    file_hashes[str(INMEMORY.relative_to(ROOT))] = sha256_file(INMEMORY)
    _, methods = parse_methods(INMEMORY, require_name_setop=False)
    for meth in methods:
        body = meth["body"]
        equiv = BE_EQUIV_RE.search(body)
        count = HAVE_COUNT_RE.search(body)
        be_count = re.search(r"\.Count\(\)\.Should\(\)\.Be\((\d+)\)", body)
        if equiv:
            expected = re.sub(r"\s+", " ", equiv.group(1)).strip()
            assertion = "BeEquivalentTo"
            line = line_of(text, meth["method_offset"] + equiv.start())
        elif count:
            expected = count.group(1)
            assertion = "HaveCount"
            line = line_of(text, meth["method_offset"] + count.start())
        elif be_count:
            expected = be_count.group(1)
            assertion = "CountBe"
            line = line_of(text, meth["method_offset"] + be_count.start())
        else:
            expected = None
            assertion = "none-found"
            line = meth["base_line"]
        cases.append(
            {
                "case": meth["test_name"],
                "in_memory_sequence": "Seed1To10: ids 1..10; right operand ids 1..3",
                "ops": meth["ops"],
                "assertion": assertion,
                "expected_values": expected,
                "expected_count": int(count.group(1)) if count else (int(be_count.group(1)) if be_count else None),
                "file": str(INMEMORY.relative_to(ROOT)),
                "line": line,
            }
        )

    common_text = COMMON_SETOPS.read_text(encoding="utf-8-sig")
    file_hashes[str(COMMON_SETOPS.relative_to(ROOT))] = sha256_file(COMMON_SETOPS)
    for m in re.finditer(r"\[Fact\]\s*\n\s*public void (\w+)\(\)\s*\n\s*\{(.*?)\n    \}", common_text, re.S):
        body = m.group(2)
        if not SETOP_RE.search(body):
            continue
        skip = "SkipUnless" in body
        count = re.search(r"\.Count\(\)\.Should\(\)\.Be\((\d+)\)", body)
        throw = "Throw<NotSupportedException>" in body
        line = line_of(common_text, m.start(2) + (count.start() if count else 0))
        cases.append(
            {
                "case": m.group(1),
                "in_memory_sequence": None,
                "ops": sorted({op for op in SETOP_RE.findall(body)}),
                "assertion": "CountBe" if count else ("ThrowsNotSupported" if throw else "other"),
                "expected_values": count.group(1) if count else None,
                "expected_count": int(count.group(1)) if count else None,
                "skip_unless_supports_intersect_except_all": skip,
                "file": str(COMMON_SETOPS.relative_to(ROOT)),
                "line": line,
            }
        )

    return {
        "task": "T245",
        "unit": "D245-1",
        "captured_at_head": "f82edf36437429e7a96d8a1525059934303f2567",
        "note": "expected sequences/multisets read from test sources; no integration execution in this preflight",
        "cases": cases,
        "source_files": [{"file": f, "sha256": h} for f, h in file_hashes.items()],
    }


def write_json(path: Path, payload) -> None:
    with open(path, "w", encoding="utf-8", newline="\r\n") as fh:
        json.dump(payload, fh, indent=2, sort_keys=False, ensure_ascii=False)
        fh.write("\n")


def main() -> int:
    SEM.mkdir(parents=True, exist_ok=True)
    sql = sql_entries()
    res = results_entries()
    write_json(SEM / "sql-entry.json", sql)
    write_json(SEM / "results-entry.json", res)
    print("sql entries:", len(sql["entries"]), "source files:", len(sql["source_files"]))
    print("result cases:", len(res["cases"]), "source files:", len(res["source_files"]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
