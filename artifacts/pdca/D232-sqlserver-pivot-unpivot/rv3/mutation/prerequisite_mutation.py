#!/usr/bin/env python3
"""C31 -- targeted controlled mutation for the r=3 typed-derived prerequisite (#232).

Task-local evidence plumbing, NOT production code. It runs the six D-05/E232-10 mutants of the typed
PIVOT/UNPIVOT rewrite and guards (the same patches recorded in ``rv1/mutants``) PLUS the new r=3
prerequisite mutants of ``QueryCommand.QueryPreparer.cs`` (the additive derived pass-through). Each
mutant is applied to its production file, its designated assertion is executed and must go red, then
the original bytes are restored and the restoration is proven by sha256.

The revert-passthrough mutant carries a second arm: the SQL Server container fact, which must fail with
``Invalid column name '1'`` (the original defect) as well as the offline R-probes.

Exit 0 iff every mutant is applied, killed and restored and the restore gate builds clean.
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
RV3 = os.path.abspath(os.path.join(HERE, ".."))
ROOT = os.path.abspath(os.path.join(RV3, "..", "..", "..", ".."))
LOGS = os.path.join(HERE, "logs")
PATCHES = os.path.join(HERE, "patches")
DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
SQLSERVER_TESTS = "tests/nextorm.sqlserver.tests"
INTEGRATION_TESTS = "tests/nextorm.integration.tests"

# The six mandated D-05/E232-10 mutants, verbatim from rv1/verify.py MUTANTS.
ORIGINAL_MUTANTS = [
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

# The new r=3 prerequisite mutants of QueryCommand.QueryPreparer.cs (the additive derived pass-through).
PREREQ_MUTANTS = [
    {
        "id": "PP1-revert-pass-through",
        "file": "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
        "find": "                                    selectList = Array.ConvertAll(reusableShape, DerivedPassThroughColumn);",
        "replace": "                                    selectList = reusableShape;",
        "assertion": "FullyQualifiedName~T232DerivedSourceReproTests",
        "explicit_only": True,
        "expected_failure": "the intermediate derived layer re-renders the inner projection and re-aliases the source column ('[1] as [Q1]'), the exact original defect",
        "extra_arm": {
            "kind": "container",
            "argv": ["dotnet", "test", INTEGRATION_TESTS, "-c", "Debug", "--filter",
                     "FullyQualifiedName~TypedPivot_DerivedBuilderRoute_Executes"],
            "env": {"DOCKER_HOST": DOCKER_HOST},
            "expect": "Invalid column name '1'",
        },
    },
    {
        "id": "PP2-drop-output-reference",
        "file": "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
        "find": "                Expression = src.Expression,\n                OutputName = outputName,\n                IsProjectionOutputReference = true,",
        "replace": "                Expression = src.Expression,\n                OutputName = outputName,",
        "assertion": "FullyQualifiedName~Repro01_TypedBuilderSource_ProjectsAsDerivedSource",
        "explicit_only": True,
        "expected_failure": "the clone is not marked as a projection-output reference, so MakeColumn re-renders the defining expression instead of the derived table's output identifier",
    },
    {
        "id": "PP3-always-property-name",
        "file": "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
        "find": "            var outputName = src.PropertyName is { } propertyName\n                ? TryGetColumnIdentifier(src.Expression, out var columnIdentifier)\n                    && string.Equals(columnIdentifier, propertyName, StringComparison.OrdinalIgnoreCase)\n                    ? columnIdentifier\n                    : propertyName\n                : src.OutputName;",
        "replace": "            var outputName = src.PropertyName is { } propertyName\n                ? propertyName\n                : src.OutputName;",
        "assertion": "FullyQualifiedName~Repro06_DerivedGroupingColumn_KeepsTheUnaliasedIdentifier",
        "explicit_only": True,
        "expected_failure": "the unaliased source-column spelling is ignored, so the grouping column is emitted as '[Category]' instead of the source's '[category]'",
    },
]


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _sha_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _read_bytes(path: str) -> bytes:
    with open(path, "rb") as handle:
        return handle.read()


def _write_bytes(path: str, data: bytes) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(data)


def _write_json(path: str, data) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        json.dump(data, handle, indent=1, sort_keys=True, ensure_ascii=False)
        handle.write("\n")


def _apply(data: bytes, find: str, replace: str):
    for convert in (lambda text: text.replace("\n", "\r\n"), lambda text: text):
        needle = convert(find).encode("utf-8")
        if data.count(needle) == 1:
            return data.replace(needle, convert(replace).encode("utf-8"))
    return None


def _parse_summary(text: str) -> dict:
    summary = {}
    for key in ("total", "failed", "succeeded", "skipped"):
        match = re.search(rf"^\s*{key}:\s*(\d+)\s*$", text, re.MULTILINE)
        summary[key] = int(match.group(1)) if match else None
    return summary


def _run(argv, prefix, env=None, timeout=900):
    os.makedirs(LOGS, exist_ok=True)
    _write_json(os.path.join(LOGS, prefix + ".cmd.json"),
                {"argv": argv, "cwd": ".", "env": env or {}, "date": _now()})
    merged = dict(os.environ)
    if env:
        merged.update(env)
    completed = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, env=merged,
                               check=False, timeout=timeout)
    _write_bytes(os.path.join(LOGS, prefix + ".out"), completed.stdout.encode("utf-8"))
    _write_bytes(os.path.join(LOGS, prefix + ".err"), completed.stderr.encode("utf-8"))
    _write_bytes(os.path.join(LOGS, prefix + ".exit"), (str(completed.returncode) + "\n").encode("utf-8"))
    return completed.returncode, completed.stdout, completed.stderr


def _mutant_argv(mutant):
    filter_value = mutant["assertion"] if mutant["assertion"].startswith("FullyQualifiedName~") \
        else "FullyQualifiedName~" + mutant["assertion"]
    if mutant.get("explicit_only"):
        return ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--filter", filter_value, "--", "-explicit", "only"]
    return ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--filter", filter_value]


def main() -> int:
    records = []
    survivors = []
    invalid = []
    mutants = ORIGINAL_MUTANTS + PREREQ_MUTANTS

    for mutant in mutants:
        full = os.path.join(ROOT, mutant["file"])
        original = _read_bytes(full)
        original_sha = _sha_bytes(original)
        mutated = _apply(original, mutant["find"], mutant["replace"])

        record = {
            "id": mutant["id"],
            "file": mutant["file"],
            "patch": {"find": mutant["find"], "replace": mutant["replace"]},
            "expected_failure": mutant["expected_failure"],
            "original_sha256": original_sha,
        }

        if mutated is None:
            record.update({"applied": False, "killed": False, "restored": True,
                           "reason": "patch text not found exactly once"})
            invalid.append(record["id"])
            records.append(record)
            continue

        record["applied"] = True
        record["mutated_sha256"] = _sha_bytes(mutated)
        _write_bytes(full, mutated)
        _write_bytes(os.path.join(PATCHES, mutant["id"], "mutated.cs"), mutated)
        _write_json(os.path.join(PATCHES, mutant["id"], "patch.json"), record)

        prefix = "C31-mutant-" + mutant["id"]
        argv = _mutant_argv(mutant)
        exit_code, out, err = _run(argv, prefix)
        summary = _parse_summary(out)
        selected = summary.get("total")
        failed = summary.get("failed")
        # Designated-assertion kill: at least one selected test failed and the run went red.
        killed = bool(exit_code != 0 and selected and selected >= 1 and failed and failed >= 1)
        record.update({
            "argv": argv,
            "exit": exit_code,
            "selected_count": selected,
            "failed": failed,
            "succeeded": summary.get("succeeded"),
            "skipped": summary.get("skipped"),
            "log": os.path.relpath(os.path.join(LOGS, prefix + ".out"), ROOT).replace(os.sep, "/"),
        })

        arm = mutant.get("extra_arm")
        if arm:
            arm_prefix = prefix + "-arm"
            arm_exit, arm_out, arm_err = _run(arm["argv"], arm_prefix, env=arm.get("env"))
            arm_summary = _parse_summary(arm_out)
            arm_text = arm_out + "\n" + arm_err
            arm_failed = bool(arm_exit != 0 and arm_summary.get("failed") and arm_summary["failed"] >= 1)
            arm_marker = arm.get("expect") in arm_text
            record["extra_arm"] = {
                "kind": arm["kind"],
                "argv": arm["argv"],
                "exit": arm_exit,
                "selected_count": arm_summary.get("total"),
                "failed": arm_summary.get("failed"),
                "skipped": arm_summary.get("skipped"),
                "marker_observed": arm_marker,
                "expected_marker": arm.get("expect"),
                "failed_arm": arm_failed,
                "log": os.path.relpath(os.path.join(LOGS, arm_prefix + ".out"), ROOT).replace(os.sep, "/"),
            }
            killed = killed and arm_failed

        _write_bytes(full, original)
        restored_sha = _sha_bytes(_read_bytes(full))
        record["killed"] = killed
        record["restored"] = restored_sha == original_sha
        record["restored_sha256"] = restored_sha
        if not killed:
            survivors.append(record["id"])
        records.append(record)

    gate_exit, gate_out, _ = _run(["dotnet", "build", "nextorm.slnx", "-c", "Debug"], "C31-restore-gate")
    warnings = re.search(r"(\d+)\s+Warning\(s\)", gate_out)
    errors = re.search(r"(\d+)\s+Error\(s\)", gate_out)
    gate = {
        "exit": gate_exit,
        "succeeded": "Build succeeded" in gate_out,
        "warnings": int(warnings.group(1)) if warnings else None,
        "errors": int(errors.group(1)) if errors else None,
    }

    restored_all = all(r.get("restored") for r in records)
    killed_count = sum(1 for r in records if r.get("killed"))
    result = {
        "unit": "D232-PREREQ-typed-derived",
        "criterion": ["EC232-PREREQ-MUTATION", "AC-11", "V26", "V29"],
        "applicable": True,
        "kind": "targeted-controlled-mutation",
        "note": "Targeted prerequisite mutation evidence only; not a repository-wide mutation score.",
        "original_mutants": len(ORIGINAL_MUTANTS),
        "prerequisite_mutants": len(PREREQ_MUTANTS),
        "attempted": len(records),
        "killed": killed_count,
        "survived": survivors,
        "invalid_patches": invalid,
        "restored": restored_all,
        "restore_gate": gate,
        "mutants": records,
        "captured": _now(),
    }
    _write_json(os.path.join(HERE, "prerequisite.json"), result)
    print(json.dumps({"attempted": len(records), "killed": killed_count, "survived": survivors,
                      "restored": restored_all, "gate": gate}, indent=1))
    ok = (killed_count == len(records) and len(invalid) == 0 and restored_all
          and gate_exit == 0 and gate.get("errors") == 0 and gate.get("warnings") == 0)
    return 0 if ok else 2


if __name__ == "__main__":
    sys.exit(main())
