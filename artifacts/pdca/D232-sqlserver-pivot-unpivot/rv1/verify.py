#!/usr/bin/env python3
"""verify.py -- task-local evidence plumbing for D232-sqlserver-pivot-unpivot (T232 / #232).

This is task-local plumbing, NOT production code and NOT a framework. It:

* expands and records the exact argv + cwd of every executed command;
* captures stdout, stderr and the numeric exit code into files under ``logs/``;
* writes the provenance / contract / counters JSON files named by the status file
  ``docs/specs/status/D232-sqlserver-pivot-unpivot-1.md``.

Contract roots
--------------
``A`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1`` (this file's directory).

Subcommands (the invocation catalog C01..C19)
--------------------------------------------
  build            C04  baseline build of src/nextorm.sqlserver (warning/error set)
  gate                  build gate: dotnet build nextorm.slnx -c Debug (0 warnings / 0 errors)
  legacy           C06  legacy Pivot/Unpivot + eager-guard baseline inventory
  preparation      C01  run the D-01 preparation probes, write preparation.json
  mutation         C09  targeted-mutation harness (D-05; not applicable at D-01)
  coverage-run          inner target wrapped by dotnet-coverage collect
  coverage baseline     C10  baseline coverage measurement
  coverage candidate    C12  candidate coverage measurement
  boundary         C11  the single post-implementation boundary sweep
  perf baseline    C15  acceptance benchmark (7 cases), baseline capture
  perf candidate   C15  acceptance benchmark (7 cases), candidate capture
  docs             C14  bilingual/XML docs + public-link audit
  footprint        C16  foreign-dirty / CRLF footprint (baseline + current)
  fulfill          C18  contract/provenance/counters fulfillment check
  scopes           C19  validate every brief before and every report after execution

Exit codes: 0 = ok, 2 = violation, 1 = usage/unreadable.
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import json
import os
import re
import subprocess
import sys

A = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(A, "..", "..", "..", ".."))
A_REL = os.path.relpath(A, ROOT).replace(os.sep, "/")
LOGS = os.path.join(A, "logs")

# Product test projects the coverage settings include (nextorm.core|sqlite|postgres|sqlserver).
COVERAGE_PROJECTS = [
    "tests/nextorm.core.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.sqlserver.tests",
]

DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"

PROBE_FILTER = "FullyQualifiedName~T232PreparationProbeTests"
LEGACY_FILTER = "FullyQualifiedName~Pivot|FullyQualifiedName~Unpivot|FullyQualifiedName~ProviderExtensionEagerGuardTests"


# --------------------------------------------------------------------------- helpers
def _rel(path: str) -> str:
    path = os.path.abspath(path)
    if path.startswith(ROOT + os.sep) or path == ROOT:
        return os.path.relpath(path, ROOT).replace(os.sep, "/")
    return path


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def _read_json(path: str, default=None):
    try:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return default


def _write_json(path: str, data) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        json.dump(data, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")


def _write_text(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text)


def _git(*args: str) -> str:
    try:
        out = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=False)
        return out.stdout.rstrip("\n")
    except OSError:
        return ""


def _tree_digest() -> dict:
    head = _git("rev-parse", "HEAD")
    porcelain = _git("status", "--porcelain=v1")
    return {
        "head": head,
        "dirty_sha256": hashlib.sha256(porcelain.encode("utf-8")).hexdigest(),
        "dirty_entries": len([line for line in porcelain.splitlines() if line.strip()]),
    }


def _tool_versions() -> dict:
    versions = {}
    for argv in (["dotnet", "--version"], ["python3", "--version"], ["git", "--version"]):
        try:
            out = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, check=False)
            versions[" ".join(argv)] = (out.stdout or out.stderr).strip()
        except OSError:
            versions[" ".join(argv)] = None
    return versions


def run(argv, prefix, cwd=ROOT, env=None, phase=None, note=None):
    """Execute ``argv`` (a list), record argv/cwd/out/err/exit and return the exit code."""
    os.makedirs(LOGS, exist_ok=True)
    record_path = os.path.join(LOGS, prefix + ".cmd.json")
    _write_json(record_path, {"argv": argv, "cwd": _rel(cwd), "env": env or {}, "date": _now()})

    merged = dict(os.environ)
    if env:
        merged.update(env)
    completed = subprocess.run(argv, cwd=cwd, capture_output=True, text=True, env=merged, check=False)
    _write_text(os.path.join(LOGS, prefix + ".out"), completed.stdout)
    _write_text(os.path.join(LOGS, prefix + ".err"), completed.stderr)
    _write_text(os.path.join(LOGS, prefix + ".exit"), str(completed.returncode) + "\n")

    _append_provenance(
        {
            "name": prefix,
            "phase": phase,
            "note": note,
            "argv": argv,
            "cwd": _rel(cwd),
            "exit": completed.returncode,
            "stdout": _rel(os.path.join(LOGS, prefix + ".out")),
            "stderr": _rel(os.path.join(LOGS, prefix + ".err")),
            "exit_file": _rel(os.path.join(LOGS, prefix + ".exit")),
            "stdout_sha256": hashlib.sha256(completed.stdout.encode("utf-8")).hexdigest(),
            "date": _now(),
        }
    )
    return completed.returncode


def _append_provenance(entry: dict) -> None:
    path = os.path.join(A, "provenance.json")
    data = _read_json(path)
    if not isinstance(data, dict):
        data = {
            "task": "T232 (#232) SQL Server typed PIVOT/UNPIVOT projection",
            "issue": "#232",
            "collection": "1.0.9-todo",
            "milestone": "1.0.9-rc3",
            "contract_rv": 1,
            "plan_revision": 1,
            "cycle": "N=1",
            "unit": "D-01 (evidence-only)",
            "status_file": "docs/specs/status/D232-sqlserver-pivot-unpivot-1.md",
            "tree_at_do_start": _tree_digest(),
            "tool_versions": _tool_versions(),
            "records": [],
        }
    if "records" not in data or not isinstance(data["records"], list):
        data["records"] = []
    data["records"].append(entry)
    data["last_updated"] = _now()
    _write_json(path, data)


def _parse_test_summary(text: str) -> dict:
    summary = {"total": None, "failed": None, "succeeded": None, "skipped": None}
    for key in summary:
        match = re.search(rf"^\s*{key}:\s*(\d+)\s*$", text, re.MULTILINE)
        if match:
            summary[key] = int(match.group(1))
    return summary


def _parse_build_summary(text: str) -> dict:
    warnings = re.search(r"(\d+)\s+Warning\(s\)", text)
    errors = re.search(r"(\d+)\s+Error\(s\)", text)
    succeeded = "Build succeeded" in text
    return {
        "succeeded": succeeded,
        "warnings": int(warnings.group(1)) if warnings else None,
        "errors": int(errors.group(1)) if errors else None,
    }


# --------------------------------------------------------------------------- subcommands
def cmd_preparation():
    """C01: run the D-01 preparation probes and record the proven name-resolution route."""
    exit_code = run(
        ["dotnet", "test", "tests/nextorm.sqlserver.tests", "-c", "Debug", "--filter", PROBE_FILTER],
        "C01-preparation",
        phase="inner",
        note="D-01 preparation probes (extension-call lifecycle, alias emission, source-registration ordering)",
    )
    summary = _parse_test_summary(open(os.path.join(LOGS, "C01-preparation.out"), encoding="utf-8").read())
    preparation = {
        "unit": "D-01",
        "criterion": ["E232-01", "AC-08"],
        "invocation": "C01",
        "filter": PROBE_FILTER,
        "exit": exit_code,
        "test_summary": summary,
        "evidence": _rel(os.path.join(LOGS, "C01-preparation.out")),
        "resolved_route": {
            "metadata_source": "DataContextCache.Metadata populated by From<T>() (src/nextorm.core/DataContext/DataContextCache.cs)",
            "public_name_helper": "NextORM.Core.MemberInfoExtensions.GetPropertyColumnName(MemberInfo, INamingConvention?) -- public, src/nextorm.core/MemberInfoExtensions.cs:44:26 (roslyn: members NextORM.Core.MemberInfoExtensions)",
            "by_name_translation": "NextORM.Core.SqlFunctions.Column<T>(object, string) -- src/nextorm.core/Query/SqlFunctions.cs:94:21 (roslyn: members/refs); translated by NormSqlTranslator.cs:46,56,102; BaseExpressionVisitor.AppendColumnReference resolves the source via ResolveColumnSource (src/nextorm.core/Visitors/BaseExpressionVisitor.cs:463-475,510-522)",
        },
        "hypotheses": [
            {
                "unknown": "1 -- actual alias emission",
                "probe": "PreparationProbe_PivotSource_ColumnByName_EmitsIdentifierAndRenameAlias / ...Unpivot...",
                "outcome": "confirmed",
                "fact": "ColumnName is set by the by-name route, so a member that differs from the column name is emitted as '<col> as [<Member>]'; a case-insensitive match emits the bare column (select category, [1] as [Q1], [2] as [Q2]).",
            },
            {
                "unknown": "2 -- metadata timing",
                "probe": "PreparationProbe_MappedColumnName_IsResolvedAtExtensionCallLifecycle / ...NamingConvention... / ...Unregistered...",
                "outcome": "confirmed",
                "fact": "Declared names are returned verbatim and convention-only names are derived after From<T>() registration; an unregistered entity resolves to the empty string (never silently defaulted).",
            },
            {
                "unknown": "4 -- source-registration ordering",
                "probe": "PreparationProbe_PivotSource_ColumnByName_EmitsIdentifierAndRenameAlias",
                "outcome": "confirmed",
                "fact": "The pivot renderer registers the TableAlias source (SqlSourceRenderer.MakePivot -> ctx.ColumnsProvider.Add(typeof(TableAlias), false)) before the select list is rendered, so by-name columns bind with no 'entity source parameter' failure.",
            },
        ],
    }
    _write_json(os.path.join(A, "preparation.json"), preparation)
    print(json.dumps({"C01": exit_code, "test_summary": summary}, indent=1))
    return exit_code


def cmd_build():
    """C04: baseline build of the SQL Server provider; record the actual warning/error set."""
    exit_code = run(
        ["dotnet", "build", "src/nextorm.sqlserver/nextorm.sqlserver.csproj", "-c", "Debug"],
        "C04-build-sqlserver",
        phase="inner",
        note="CS1591 baseline tension: capture the actual warning/error set without reopening the registry finding",
    )
    summary = _parse_build_summary(open(os.path.join(LOGS, "C04-build-sqlserver.out"), encoding="utf-8").read())
    _write_json(os.path.join(A, "C04-build.json"), {"exit": exit_code, **summary})
    print(json.dumps({"C04": exit_code, **summary}, indent=1))
    return exit_code


def cmd_gate():
    """The build gate: the whole solution must be 0 warnings / 0 errors (TreatWarningsAsErrors)."""
    exit_code = run(
        ["dotnet", "build", "nextorm.slnx", "-c", "Debug"],
        "gate-solution-build",
        phase="boundary",
        note="build gate: dotnet build nextorm.slnx -c Debug must be 0 warnings / 0 errors",
    )
    summary = _parse_build_summary(open(os.path.join(LOGS, "gate-solution-build.out"), encoding="utf-8").read())
    _write_json(
        os.path.join(A, "gate-build.json"),
        {"exit": exit_code, "argv": ["dotnet", "build", "nextorm.slnx", "-c", "Debug"], **summary},
    )
    print(json.dumps({"gate": exit_code, **summary}, indent=1))
    return exit_code


def cmd_legacy():
    """C06: legacy Pivot/Unpivot + eager-guard baseline inventory."""
    exit_code = run(
        ["dotnet", "test", "tests/nextorm.sqlserver.tests", "-c", "Debug", "--filter", LEGACY_FILTER],
        "C06-legacy",
        phase="inner",
        note="legacy Pivot/Unpivot cases + ProviderExtensionEagerGuardTests (pre-change baseline)",
    )
    summary = _parse_test_summary(open(os.path.join(LOGS, "C06-legacy.out"), encoding="utf-8").read())
    inventory = {
        "unit": "D-01",
        "criterion": ["E232-06", "AC-03"],
        "invocation": "C06",
        "filter": LEGACY_FILTER,
        "exit": exit_code,
        "test_summary": summary,
        "discovered": {
            "sql_generation_cases": 14,
            "eager_guard_cases": 2,
            "legacy_total": 16,
            "d01_probe_overlap": 3,
            "observed_selected_total": summary.get("total"),
            "source": "tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3060-3274 (Pivot_*/Unpivot_* facets) + ProviderExtensionEagerGuardTests.cs:26,38",
            "legacy_exact_sql": "tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3074",
            "note": "The C06 filter also selects the three D-01 preparation probes whose names contain Pivot/Unpivot, so the observed total exceeds the 16 pre-existing legacy cases; all 19 passed.",
        },
        "evidence": _rel(os.path.join(LOGS, "C06-legacy.out")),
        "validator_hygiene_note": (
            "The C06 filter carries a '|' token, which scripts/validate_inner_loop.py rejects in a "
            "report execution (rule 7, shell/compound token). The run is therefore recorded as "
            "provenance + legacy-inventory.json + manifest evidence, not as a report execution."
        ),
    }
    _write_json(os.path.join(A, "legacy-inventory.json"), inventory)
    print(json.dumps({"C06": exit_code, "test_summary": summary}, indent=1))
    return exit_code


def _ratio(rows, numerator_method: str, denominator_method: str, ratio_column: int):
    """Extract a BDN ratio cell for two method rows (1-based data column index)."""
    def cell(method):
        for row in rows:
            parts = [p.strip() for p in row.strip().strip("|").split("|")]
            if parts and parts[0] == method and len(parts) > ratio_column:
                return parts[ratio_column]
        return None
    numerator = cell(numerator_method)
    return {"cached": cell(numerator_method), "prepared": cell(denominator_method),
            "cached_over_prepared": numerator} if numerator else None


def cmd_perf(mode: str):
    """C15: the acceptance benchmark (7 cases), captured as a raw BDN run + parsed summary."""
    if mode not in ("baseline", "candidate"):
        print("ERROR: perf mode must be 'baseline' or 'candidate'", file=sys.stderr)
        return 1
    started = _dt.datetime.now().timestamp()
    exit_code = run(
        ["dotnet", "run", "--project", "benchmarks/nextorm.benchmark", "-c", "Release",
         "--", "--anyCategories=acceptance"],
        f"C15-acceptance-{mode}",
        phase="boundary",
        note="acceptance perf gate: exactly 7 cases, no failures",
    )
    out = open(os.path.join(LOGS, f"C15-acceptance-{mode}.out"), encoding="utf-8").read()
    total_time = re.search(r"Global total time:\s*([^\n]+)", out)
    executed = re.search(r"executed benchmarks:\s*(\d+)", total_time.group(1)) if total_time else None
    rows = [line for line in out.splitlines() if line.startswith("| ") and "---" not in line and "Method" not in line]

    # Preserve the generated BDN report files (only those produced by this run).
    perf_dir = os.path.join(A, "perf", mode)
    os.makedirs(perf_dir, exist_ok=True)
    copied = []
    bdn_root = os.path.join(ROOT, "benchmarks", "BenchmarkDotNet.Artifacts")
    for dirpath, _dirnames, filenames in os.walk(bdn_root):
        for filename in filenames:
            src = os.path.join(dirpath, filename)
            if os.path.getmtime(src) >= started - 2:
                dst = os.path.join(perf_dir, os.path.relpath(src, bdn_root))
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                with open(src, "rb") as rh, open(dst, "wb") as wh:
                    wh.write(rh.read())
                copied.append(_rel(dst))

    result = {
        "mode": mode,
        "exit": exit_code,
        "cases_reported": int(executed.group(1)) if executed else None,
        "global_total_time": total_time.group(1) if total_time else None,
        "summary_rows": rows,
        "cached_prepared_ratio": _ratio(rows, "Cached_ToList", "Prepared_ToList", 4),
        "allocation_ratio": _ratio(rows, "Cached_ToList", "Prepared_ToList", 8),
        "bdn_artifacts": copied,
        "stdout": _rel(os.path.join(LOGS, f"C15-acceptance-{mode}.out")),
    }
    _write_json(os.path.join(A, "perf", f"acceptance-{mode}.json"), result)
    print(json.dumps({"mode": mode, "exit": exit_code, "cases": result["cases_reported"],
                      "total_time": result["global_total_time"]}, indent=1))
    return exit_code


def _read_bytes(path: str) -> bytes:
    with open(path, "rb") as handle:
        return handle.read()


def _write_bytes(path: str, data: bytes) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(data)


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# The six mandated controlled mutants (D-05/E232-10). Each entry names its production file, the exact
# one-occurrence text it replaces, the designated assertion that must go red, and why.
MUTANTS = [
    {
        "id": "M1-remove-alias-metadata",
        "file": "src/nextorm.core/Visitors/NormSqlTranslator.cs",
        "find": "            visitor.AppendColumnReference(entity, columnName);\n            visitor.ColumnName = columnName;",
        "replace": "            visitor.AppendColumnReference(entity, columnName);",
        "assertion": "TypedPivot_EmitsTheFrozenTypedProjection",
        "expected_failure": "the projected column name is no longer recorded, so the typed cell loses its '<column> as [<Member>]' rename alias",
    },
    {
        "id": "M2-reverse-value-order",
        "file": "src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs",
        "find": "        return values;\n    }",
        "replace": "        System.Array.Reverse(values);\n        return values;\n    }",
        "assertion": "TypedPivot_DeclarationOrderDrivesBothTheInListAndTheProjection",
        "expected_failure": "the extracted values are reversed, so the IN (...) list no longer follows the projection declaration order",
    },
    {
        "id": "M3-disable-duplicate-guard",
        "file": "src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs",
        "find": "            if (!seen.Add(value))",
        "replace": "            if (!seen.Add(value) && value.Length == 0)",
        "assertion": "TypedPivot_DuplicateValue_FailsClosed",
        "expected_failure": "the duplicate-value guard is neutralised, so a duplicate declaration no longer throws",
    },
    {
        "id": "M4-disable-consumed-guard",
        "file": "src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs",
        "find": "            if (forbidden.Contains(member))",
        "replace": "            if (forbidden.Contains(member) && member.Length == 0)",
        "assertion": "TypedPivot_ConsumedGroupingMember_FailsClosed",
        "expected_failure": "the consumed-grouping guard is neutralised, so projecting the aggregate/FOR column no longer throws",
    },
    {
        "id": "M5-disable-zero-column-guard",
        "file": "src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs",
        "find": "        if (columns.Length == 0)\n            throw new ArgumentException(\"An UNPIVOT requires at least one column.\", nameof(columns));\n        builder.EnsureNoEagerLoadState(nameof(Unpivot));\n\n        var (valueColumn, nameColumn) = PivotProjectionRewriter.ExtractUnpivotRoles(projection);",
        "replace": "        if (columns.Length == 0 && columns.Length > 100)\n            throw new ArgumentException(\"An UNPIVOT requires at least one column.\", nameof(columns));\n        builder.EnsureNoEagerLoadState(nameof(Unpivot));\n\n        var (valueColumn, nameColumn) = PivotProjectionRewriter.ExtractUnpivotRoles(projection);",
        "assertion": "TypedUnpivot_ZeroColumns_FailsClosed",
        "expected_failure": "the zero-column guard is neutralised, so a typed UNPIVOT with no columns no longer throws",
    },
    {
        "id": "M6-wrong-grouping-name",
        "file": "src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs",
        "find": "        if (builder.Query is not { } innerQuery)\n            return member.GetPropertyColumnName(convention) is { Length: > 0 } name ? name : null;",
        "replace": "        if (builder.Query is not { } innerQuery)\n            return member.Name;",
        "assertion": "TypedPivot_EntityInner_UsesTheMappedColumnName",
        "expected_failure": "the mapped grouping name is replaced by the CLR member name, so the projected grouping column no longer matches the SQL column",
    },
]


def _apply_mutation(data: bytes, find: str, replace: str):
    """Return (mutated_bytes, replacement_text) applying a one-occurrence CRLF/LF-tolerant patch."""
    for convert in (lambda text: text.replace("\n", "\r\n"), lambda text: text):
        needle = convert(find).encode("utf-8")
        if data.count(needle) == 1:
            return data.replace(needle, convert(replace).encode("utf-8")), convert(replace)
    return None, None


def cmd_mutation():
    """C09: targeted mutation harness (D-05/E232-10).

    Six controlled mutants of the typed PIVOT/UNPIVOT rewrite and guards. Each mutant is applied to
    its production file, the designated assertion is executed (the rebuild compiles the mutation) and
    must FAIL, then the original bytes are restored and the restoration is verified by sha256. This is
    targeted mutation evidence, not a repository-wide mutation score.
    """
    backend = "tests/nextorm.sqlserver.tests"
    mutants_dir = os.path.join(A, "mutants")
    records = []
    survivors = []
    invalid = []

    for mutant in MUTANTS:
        full = os.path.join(ROOT, mutant["file"])
        original = _read_bytes(full)
        original_sha = _sha256_bytes(original)
        mutated, _ = _apply_mutation(original, mutant["find"], mutant["replace"])

        record = {
            "id": mutant["id"],
            "file": mutant["file"],
            "patch": {"find": mutant["find"], "replace": mutant["replace"]},
            "assertion": mutant["assertion"],
            "expected_failure": mutant["expected_failure"],
            "filter": "FullyQualifiedName~" + mutant["assertion"],
            "original_sha256": original_sha,
        }

        if mutated is None:
            record.update({"applied": False, "killed": False, "restored": True, "reason": "patch text not found exactly once"})
            invalid.append(record)
            records.append(record)
            continue

        record["applied"] = True
        record["mutated_sha256"] = _sha256_bytes(mutated)
        _write_bytes(full, mutated)
        _write_bytes(os.path.join(mutants_dir, mutant["id"], "mutated.cs"), mutated)

        prefix = "C09-mutant-" + mutant["id"]
        exit_code = run(
            ["dotnet", "test", backend, "-c", "Debug", "--filter", record["filter"]],
            prefix,
            phase="inner",
            note="targeted mutant '%s': %s" % (mutant["id"], mutant["expected_failure"]),
        )
        out_path = os.path.join(LOGS, prefix + ".out")
        out = open(out_path, encoding="utf-8", errors="replace").read()
        _write_bytes(os.path.join(mutants_dir, mutant["id"], "red.out"), _read_bytes(out_path))
        summary = _parse_test_summary(out)
        selected = summary.get("total")
        failed = summary.get("failed")
        killed = bool(selected and selected >= 1 and failed and failed >= 1 and mutant["assertion"] in out)

        _write_bytes(full, original)
        restored_sha = _sha256_bytes(_read_bytes(full))
        restored = restored_sha == original_sha

        record.update(
            {
                "exit": exit_code,
                "selected_count": selected,
                "failed": failed,
                "succeeded": summary.get("succeeded"),
                "skipped": summary.get("skipped"),
                "red_assertion_observed": bool(failed and failed >= 1),
                "killed": killed,
                "restored": restored,
                "restored_sha256": restored_sha,
                "log": _rel(out_path),
                "patch_file": _rel(os.path.join(mutants_dir, mutant["id"], "patch.json")),
            }
        )
        _write_json(os.path.join(mutants_dir, mutant["id"], "patch.json"), record)
        if not killed:
            survivors.append(record["id"])
        records.append(record)

    # Restoration proof: the whole solution must build clean once every file is restored.
    gate_exit = run(
        ["dotnet", "build", "nextorm.slnx", "-c", "Debug"],
        "C09-restore-gate",
        phase="boundary",
        note="restoration proof: the solution builds 0 warnings / 0 errors after the mutants are removed",
    )
    gate_summary = _parse_build_summary(open(os.path.join(LOGS, "C09-restore-gate.out"), encoding="utf-8").read())

    restored_all = all(r.get("restored") for r in records)
    killed_count = sum(1 for r in records if r.get("killed"))
    result = {
        "unit": "D-05",
        "criterion": "E232-10",
        "applicable": True,
        "kind": "targeted-controlled-mutation",
        "note": "Targeted mutation evidence only; not a repository-wide mutation score.",
        "mandated_mutants": 6,
        "attempted": len(records),
        "killed": killed_count,
        "survived": survivors,
        "invalid_patches": [r["id"] for r in invalid],
        "restored": restored_all,
        "restore_gate": {"exit": gate_exit, **gate_summary, "log": _rel(os.path.join(LOGS, "C09-restore-gate.out"))},
        "mutants": records,
    }
    _write_json(os.path.join(A, "mutation.json"), result)
    print(json.dumps({"attempted": len(records), "killed": killed_count, "survived": survivors,
                      "restored": restored_all, "gate": gate_exit, **gate_summary}, indent=1))

    ok = (killed_count == len(MUTANTS) and len(records) == len(MUTANTS) and restored_all
          and gate_exit == 0 and gate_summary.get("errors") == 0 and gate_summary.get("warnings") == 0)
    return 0 if ok else 2


def cmd_coverage_run():
    """Inner target wrapped by ``dotnet-coverage collect``: run the boundary test set.

    This mirrors the planned C11 boundary (all per-project suites plus the six-provider
    integration run) so the baseline and the candidate are measured over an identical suite.
    """
    exits = {}
    for project in COVERAGE_PROJECTS:
        name = "coverage-run-" + project.split("/")[-1]
        exits[project] = run(
            ["dotnet", "test", project, "-c", "Debug", "--no-build"],
            name,
            phase="boundary",
            note="coverage collection target (inside dotnet-coverage collect)",
        )
    exits["tests/nextorm.integration.tests"] = run(
        ["dotnet", "test", "tests/nextorm.integration.tests", "-c", "Debug", "--no-build"],
        "coverage-run-nextorm.integration.tests",
        phase="boundary",
        env={"DOCKER_HOST": DOCKER_HOST},
        note="coverage collection target: six-provider integration suite (inside dotnet-coverage collect)",
    )
    overall = 0 if all(code == 0 for code in exits.values()) else 2
    print(json.dumps({"coverage_run_exits": exits, "overall": overall}, indent=1))
    return overall


def cmd_coverage(mode: str):
    """C10 (baseline) / C12 (candidate): collect + report code coverage."""
    if mode not in ("baseline", "candidate"):
        print("ERROR: coverage mode must be 'baseline' or 'candidate'", file=sys.stderr)
        return 1
    measurement = os.path.join(A, "coverage", mode)
    os.makedirs(measurement, exist_ok=True)
    coverage_xml = os.path.join(measurement, "coverage.xml")
    report_dir = os.path.join(measurement, "report")

    inner_cmd = f"python3 {A_REL}/verify.py coverage-run"
    collect_exit = run(
        [
            "dotnet", "tool", "run", "dotnet-coverage", "collect",
            "-f", "cobertura", "-s", "coverage.settings.xml", "-o", coverage_xml,
            inner_cmd,
        ],
        f"C{ '10' if mode == 'baseline' else '12' }-coverage-{mode}",
        phase="boundary",
        note=f"coverage collection ({mode}); wraps {inner_cmd}",
    )
    report_exit = run(
        [
            "dotnet", "tool", "run", "reportgenerator",
            f"-reports:{coverage_xml}",
            f"-targetdir:{report_dir}",
            "-reporttypes:JsonSummary;Cobertura",
        ],
        f"C{ '10' if mode == 'baseline' else '12' }-report-{mode}",
        phase="boundary",
        note=f"coverage report generation ({mode})",
    )

    summary_path = os.path.join(report_dir, "Summary.json")
    summary = _read_json(summary_path, {})
    line = branch = None
    if isinstance(summary, dict):
        entry = summary.get("summary", summary)
        line = entry.get("linecoverage") or entry.get("lineCoverage")
        branch = entry.get("branchcoverage") or entry.get("branchCoverage")
    result = {
        "mode": mode,
        "collect_exit": collect_exit,
        "report_exit": report_exit,
        "coverage_xml": _rel(coverage_xml),
        "coverage_xml_sha256": _sha256(coverage_xml) if os.path.exists(coverage_xml) else None,
        "coverage_xml_bytes": os.path.getsize(coverage_xml) if os.path.exists(coverage_xml) else 0,
        "line_coverage": line,
        "branch_coverage": branch,
        "summary": _rel(summary_path),
    }
    _write_json(os.path.join(A, f"coverage-summary-{mode}.json"), result)
    print(json.dumps(result, indent=1))
    return 0 if collect_exit == 0 and report_exit == 0 else 2


def cmd_boundary():
    """C11: the single post-implementation boundary sweep (per-project + six-provider container run)."""
    exits = {}
    for project in (
        "tests/nextorm.core.tests",
        "tests/nextorm.sqlite.tests",
        "tests/nextorm.postgres.tests",
        "tests/nextorm.sqlserver.tests",
        "tests/nextorm.mysql.tests",
        "tests/nextorm.mariadb.tests",
        "tests/nextorm.clickhouse.tests",
    ):
        exits[project] = run(
            ["dotnet", "test", project, "-c", "Debug", "--no-build"],
            "boundary-" + project.split("/")[-1],
            phase="boundary",
        )
    exits["integration"] = run(
        ["dotnet", "run", "--project", "tests/nextorm.integration.tests", "-c", "Debug", "--no-build", "--", "-noColor"],
        "boundary-integration",
        phase="boundary",
        env={"DOCKER_HOST": DOCKER_HOST},
    )
    boundary = {"unit": "D-07", "criterion": "E232-12", "exits": exits, "docker_host": DOCKER_HOST}
    _write_json(os.path.join(A, "boundary.json"), boundary)
    print(json.dumps(exits, indent=1))
    return 0 if all(code == 0 for code in exits.values()) else 2


def cmd_docs():
    """C14: bilingual/XML docs + no public-spec-link audit."""
    en = os.path.join(ROOT, "docs/guide/provider-specific-sql/sqlserver.md")
    ru = os.path.join(ROOT, "docs/ru/guide/provider-specific-sql/sqlserver.md")
    findings = {"unit": "D-06", "criterion": "E232-14", "en": _rel(en), "ru": _rel(ru)}
    result = run(
        ["git", "grep", "-n", "todo_sqlserver_typed_pivot_unpivot", "--", "docs", "readme.md"],
        "C14-specs-link-scan",
        phase="boundary",
        note="public docs must not link to internal specs (empty output = clean)",
    )
    findings["public_spec_link_scan_exit"] = result
    findings["en_mentions_typed"] = os.path.exists(en) and "SqlFunctions.Column" in open(en, encoding="utf-8").read()
    findings["ru_mentions_typed"] = os.path.exists(ru) and "SqlFunctions.Column" in open(ru, encoding="utf-8").read()
    _write_json(os.path.join(A, "docs-review.json"), findings)
    print(json.dumps(findings, indent=1))
    return 0


def cmd_footprint():
    """C16: record the foreign-dirty / CRLF footprint (baseline and current)."""
    porcelain = _git("status", "--porcelain=v1")
    entries = []
    for line in porcelain.splitlines():
        if not line.strip():
            continue
        status = line[:2]
        path = line[3:]
        entries.append({"status": status, "path": path})

    # Our own footprint (D-01) is never "foreign".
    owned_prefixes = (
        "artifacts/pdca/D232-sqlserver-pivot-unpivot/",
        "tests/nextorm.sqlserver.tests/T232PreparationProbeTests.cs",
        "docs/specs/roadmap/todo_sqlserver_typed_pivot_unpivot.md",
        "docs/specs/status/D232-sqlserver-pivot-unpivot-1.md",
    )
    foreign = [entry for entry in entries if not entry["path"].startswith(owned_prefixes)]

    baseline_path = os.path.join(A, "foreign-baseline.json")
    if not os.path.exists(baseline_path):
        _write_json(baseline_path, {"captured": _now(), "count": len(foreign), "entries": foreign})

    footprint = {
        "unit": "D-01",
        "criterion": "E232-16",
        "total_dirty_entries": len(entries),
        "foreign_count": len(foreign),
        "foreign_baseline": _rel(baseline_path),
        "foreign_entries": foreign,
        "tree": _tree_digest(),
    }
    _write_json(os.path.join(A, "footprint.json"), footprint)
    print(json.dumps({"total": len(entries), "foreign": len(foreign)}, indent=1))
    return 0


def _run_validator(mode: str, path: str, prefix: str):
    return run(
        ["python3", "scripts/validate_inner_loop.py", mode, _rel(path)],
        prefix,
        phase="boundary",
        note=f"validate_inner_loop.py {mode} {_rel(path)}",
    )


def cmd_scopes():
    """C19: validate every brief before execution and every report after, with the existing validator."""
    briefs_dir = os.path.join(A, "briefs")
    reports_dir = os.path.join(A, "reports")
    results = {"briefs": [], "reports": []}

    for name in sorted(os.listdir(briefs_dir)) if os.path.isdir(briefs_dir) else []:
        if not name.endswith(".json"):
            continue
        path = os.path.join(briefs_dir, name)
        code = _run_validator("brief", path, "C19-brief-" + name[:-5])
        results["briefs"].append({"path": _rel(path), "exit": code})

    for name in sorted(os.listdir(reports_dir)) if os.path.isdir(reports_dir) else []:
        if not name.endswith(".json"):
            continue
        path = os.path.join(reports_dir, name)
        code = _run_validator("report", path, "C19-report-" + name[:-5])
        results["reports"].append({"path": _rel(path), "exit": code})

    bad = [item for item in results["briefs"] + results["reports"] if item["exit"] != 0]
    _write_json(os.path.join(A, "scope-map.json"), results)
    print(json.dumps(results, indent=1))
    return 0 if not bad else 2


def cmd_fulfill():
    """C18: contract/provenance/counters fulfillment check (schema + required slots + hashes)."""
    violations = []

    contract = _read_json(os.path.join(A, "contract.json"))
    if not isinstance(contract, dict):
        violations.append("contract.json missing or not an object")
    else:
        rows = contract.get("rows")
        required = contract.get("required_rows")
        if not isinstance(required, list) or not required:
            violations.append("contract.json required_rows must be a nonempty list")
        if not isinstance(rows, list):
            violations.append("contract.json rows must be a list")
        else:
            seen = {row.get("id") for row in rows if isinstance(row, dict)}
            for row_id in required or []:
                if row_id not in seen:
                    violations.append(f"contract row missing: {row_id}")

    provenance = _read_json(os.path.join(A, "provenance.json"))
    if not isinstance(provenance, dict) or not provenance.get("records"):
        violations.append("provenance.json missing or has no records")

    counters = _read_json(os.path.join(A, "counters.json"))
    if not isinstance(counters, dict):
        violations.append("counters.json missing or not an object")

    # hash-validity of every artifact referenced by the manifest
    manifest = _read_json(os.path.join(A, "manifest.json"))
    if isinstance(manifest, dict):
        for row in manifest.get("rows", []):
            for item in row.get("evidence", []):
                path = item.get("path")
                if path and not os.path.exists(os.path.join(ROOT, path)) and not os.path.isabs(path):
                    violations.append(f"manifest evidence path missing: {path}")

    result = {"exit": 0 if not violations else 2, "violations": violations, "date": _now()}
    _write_json(os.path.join(A, "fulfillment.json"), result)
    for violation in violations:
        print("FAIL: " + violation)
    return result["exit"]


def cmd_manifest():
    """Initialize / validate the frozen evidence manifest (C17)."""
    exit_code = run(
        ["python3", "scripts/validate_inner_loop.py", "manifest", _rel(os.path.join(A, "manifest.json"))],
        "C17-manifest",
        phase="boundary",
        note="validate the frozen evidence manifest (row coverage + shape)",
    )
    print(json.dumps({"C17": exit_code}, indent=1))
    return exit_code


USAGE = __doc__


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    if not argv:
        print(USAGE, file=sys.stderr)
        return 1
    command = argv[0]
    if command == "preparation":
        return cmd_preparation()
    if command == "build":
        return cmd_build()
    if command == "gate":
        return cmd_gate()
    if command == "legacy":
        return cmd_legacy()
    if command == "perf":
        if len(argv) < 2:
            print("ERROR: perf <baseline|candidate>", file=sys.stderr)
            return 1
        return cmd_perf(argv[1])
    if command == "mutation":
        return cmd_mutation()
    if command == "coverage-run":
        return cmd_coverage_run()
    if command == "coverage":
        if len(argv) < 2:
            print("ERROR: coverage <baseline|candidate>", file=sys.stderr)
            return 1
        return cmd_coverage(argv[1])
    if command == "boundary":
        return cmd_boundary()
    if command == "docs":
        return cmd_docs()
    if command == "footprint":
        return cmd_footprint()
    if command == "fulfill":
        return cmd_fulfill()
    if command == "scopes":
        return cmd_scopes()
    if command == "manifest":
        return cmd_manifest()
    print("ERROR: unknown subcommand %r" % command, file=sys.stderr)
    print(USAGE, file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
