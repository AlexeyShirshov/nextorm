#!/usr/bin/env python3
"""D226 / rv2 — fail-closed mutation verifier (P226-R2-03).

Invoked by verify-mutations.sh. Properties required by the sealed r=2 plan:
  * restoration derivation: the real source bytes are saved per patched file, every reverse/restore
    performs an ACTUAL byte comparison, the outcome is persisted per mutant and again in the final
    cleanup, and `restored_byte_identical` is derived from those comparisons (never from a patch hash);
  * the report is written only AFTER the final restoration;
  * kill classifier: a verified kill requires a successful mutant build + a successful matching clean
    control, a nonzero mutated test exit with executed failing tests, the designated killing test
    bound to its failure record AND stack, the declared expected/actual assertion evidence, and NO
    infrastructure diagnostic in that invocation (infrastructure rejection wins over apparent
    assertion evidence);
  * nonzero exit on any unexpected abort / comparison failure / missing restoration evidence.

Exit code (also written to $R/mutation.exit): 0 only when every mutant is verified-killed, every
restore compared byte-identically and the report/exit artifacts exist.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

R = Path(__file__).resolve().parent
REPO = R.parents[3]
R1 = R.parent / "rv1"
WORK = Path(tempfile.mkdtemp(prefix="d226-r2-mut-", dir="/tmp/opencode"))
LOG = R / "mutation.log"
REPORT = R / "mutation-report.json"
EXITFILE = R / "mutation.exit"
MUTDIR = R / "mutations"

OWNER = "src/nextorm.core/DataContext/CommandReaderOwner.cs"
RUNNER = "src/nextorm.core/DataContext/BatchRunner.cs"
CORE_PROJECT = "tests/nextorm.core.tests"
SQLITE_PROJECT = "tests/nextorm.sqlite.tests"
CORE_FILTER = "FullyQualifiedName~RecordsAffected"
ROWCOUNT_FILTER = "FullyQualifiedName~Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount"

EXTRA_PROVENANCE_IDS = [
    "remove-publish-after-release-closereader",
    "remove-publish-after-release-disposeasync",
    "remove-command-disposeasync",
]

INFRA_TOKENS = [
    "GUARD-FAIL: base build failed",
    "GUARD-FAIL: the script aborted with status",
    "GUARD-FAIL: restore hash mismatch",
    "infrastructure failure, not a kill",
    "PATCH_NO_MATCH",
    "PATCH_FAILED",
    "GUARD-FAIL: nonzero exit without a failing test",
    "Zero tests ran",
    "TypeLoadException",
    "error CS",
]

# --- mutant definitions ----------------------------------------------------------------------
# hunk applier: list of (file, old, new, expected_count) applied to the working tree, then reversed
# by restoring the saved bytes (the patches are also saved under $R/mutations/*.patch).
MUTANTS = [
    {
        "id": "total-to-row-count",
        "frozen_phrase": "replace the total with row-count",
        "mechanism": "hunks applied to CURRENT bytes; the patch artifact was generated from CURRENT bytes by make-row-count-mutant.py and validated with `git apply --check` (exit 0, recorded in row-count-scope.md); git apply itself is a CRLF no-op in this worktree",
        "patch": "mutations/total-to-row-count.patch",
        "files": [RUNNER],
        "project": SQLITE_PROJECT,
        "filter": ROWCOUNT_FILTER,
        "control": "sqlite-rowcount",
        "designated_test": "NextORM.Sqlite.Tests.BatchMultipleResultTests.Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount",
        "expected_actual": ["Expected result.RecordsAffected to be 1", "but found 3"],
        "hunks": [
            (RUNNER,
             "        return new BatchResult(sets, resultTypes, columnNames, CommandReaderOwner.ReadRecordsAffected(reader));\n",
             "        return new BatchResult(sets, resultTypes, columnNames, System.Linq.Enumerable.Sum(sets, static set => set.Count));\n",
             2),
        ],
    },
    {
        "id": "remove-capture",
        "frozen_phrase": "remove capture",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/remove-capture.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.BeforeRelease_ReturnsSentinel_AfterDispose_ReturnsCapturedTotal",
        "expected_actual": ["Expected result.RecordsAffected to be 3", "but found -1"],
        "hunks": [
            (OWNER, "        _recordsAffected = ReadRecordsAffected(reader);\n", "", 1),
            (OWNER, "        if (afterRelease != -1)\n            _recordsAffected = afterRelease;\n", "        _ = afterRelease;\n", 1),
        ],
    },
    {
        "id": "sentinel-to-0",
        "frozen_phrase": "replace the sentinel with 0",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/sentinel-to-0.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.UnavailableMetadata_IsTheSentinel_AndNeverThrows",
        "expected_actual": ["Expected result.RecordsAffected to be -1", "but found 0"],
        "hunks": [
            (OWNER, "    private int _recordsAffected = -1;\n", "    private int _recordsAffected = 0;\n", 1),
            (OWNER, "            return -1;\n", "            return 0;\n", 1),
            (OWNER, "        if (afterRelease != -1)\n", "        if (afterRelease != 0)\n", 1),
        ],
    },
    {
        "id": "reset-on-repeated-release",
        "frozen_phrase": "reset the cache on repeated release",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/reset-on-repeated-release.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.RepeatedReleaseAndGetter_AreIdempotent_AndDoNotThrow",
        "expected_actual": ["Expected result.RecordsAffected to be 8", "but found -1"],
        "hunks": [
            (OWNER, "    public void Dispose()\n    {\n        var reader = _reader;\n",
             "    public void Dispose()\n    {\n        _recordsAffected = -1;\n        var reader = _reader;\n", 1),
        ],
    },
    {
        "id": "disposed-reader-from-getter",
        "frozen_phrase": "read a disposed reader from the getter",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/disposed-reader-from-getter.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.UnavailableMetadata_IsTheSentinel_AndNeverThrows",
        "expected_actual": ["Did not expect any exception, but found System.NullReferenceException"],
        "hunks": [
            (OWNER, "    internal int RecordsAffected => _recordsAffected;\n",
             "    internal int RecordsAffected => _reader is { } live ? live.RecordsAffected : _reader!.RecordsAffected;\n", 1),
        ],
    },
    # extras: substantiate the three former Stryker survivors (X1-X3) in rv2 records
    {
        "id": "remove-publish-after-release-closereader",
        "frozen_phrase": "extra (provenance: stryker survivor X1)",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/remove-publish-after-release-closereader.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.CloseReader_PublishesTheTotal_PreferringThePostReleaseValue",
        "expected_actual": ["Expected result.RecordsAffected to be 5", "but found 2"],
        "hunks": [
            (OWNER, "        reader.Dispose();\n        PublishAfterRelease(reader);\n", "        reader.Dispose();\n", 1),
        ],
    },
    {
        "id": "remove-publish-after-release-disposeasync",
        "frozen_phrase": "extra (provenance: stryker survivor X2)",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/remove-publish-after-release-disposeasync.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.AsyncRelease_PrefersThePostReleaseValue",
        "expected_actual": ["Expected result.RecordsAffected to be 6", "but found 2"],
        "hunks": [
            (OWNER, "            await reader.DisposeAsync().ConfigureAwait(false);\n            PublishAfterRelease(reader);\n",
             "            await reader.DisposeAsync().ConfigureAwait(false);\n", 1),
        ],
    },
    {
        "id": "remove-command-disposeasync",
        "frozen_phrase": "extra (provenance: stryker survivor X3)",
        "mechanism": "hunks (rv1 patch copied byte-identically to mutations/)",
        "patch": "mutations/remove-command-disposeasync.patch",
        "files": [OWNER],
        "project": CORE_PROJECT,
        "filter": CORE_FILTER,
        "control": "core-recordsaffected",
        "designated_test": "NextORM.Core.Tests.RecordsAffectedTests.AsyncRelease_DisposesTheCommand",
        "expected_actual": ["Expected command.DisposeAsyncCount to be 1", "but found 0"],
        "hunks": [
            (OWNER, "        if (command is not null)\n            await command.DisposeAsync().ConfigureAwait(false);\n", "        _ = command;\n", 1),
        ],
    },
]

CONTROLS = {
    "core-recordsaffected": {"project": CORE_PROJECT, "filter": CORE_FILTER, "expected_total": 16, "exit": 0},
    "sqlite-rowcount": {"project": SQLITE_PROJECT, "filter": ROWCOUNT_FILTER, "expected_total": 1, "exit": 0},
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(cmd, cwd=REPO):
    proc = subprocess.run(cmd, cwd=str(cwd), capture_output=True, text=True)
    return proc.returncode, proc.stdout + proc.stderr


def count_of(text: str, field: str) -> int | None:
    m = re.search(rf"^\s*{field}:\s*(\d+)\s*$", text, re.M)
    return int(m.group(1)) if m else None


def build_provenance(records: list[dict]) -> dict:
    """The truthful provenance block, emitted by the generator itself (never appended by hand).

    Emitting it here makes report regeneration non-destructive: a fresh `mutation-report.json` always
    carries the erratum that `verify-provenance.py` requires, with no `stryker_re_run` claim.
    """
    by_id = {r["id"]: r for r in records}
    extras = {}
    for mid in EXTRA_PROVENANCE_IDS:
        rec = by_id.get(mid)
        if rec is not None:
            extras[mid] = {
                "killing_test": rec["killing_test"],
                "assertion": rec["assertion_message"],
                "patch": rec["patch"],
                "kill_status": rec["kill_status"],
                "restore_ok": rec["restore_ok"],
            }
    return {
        "erratum": "artifacts/pdca/D226/rv2/provenance.md (additive; rv1 artifacts remain immutable history)",
        "stryker_post_fix_rerun": "not-substantiated",
        "stryker_statement": (
            "The scoped PRE-FIX Stryker output left by the r=1 CHECK re-run under "
            "artifacts/pdca/D226/rv1/check/ exists as history, but its identity against the final "
            "accepted tree is unverified, so it is NOT evidence of a substantiated post-fix/final-tree "
            "Stryker rerun. rv2 makes no such claim and carries no stryker_re_run key."
        ),
        "x1_x3_substantiated_by": extras,
        "configureawait_entries": [
            {
                "mutant_id": "stryker-configureawait-reader-disposeasync",
                "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:135",
                "decision": "justified",
                "basis": "reasoning",
                "reason": (
                    "Removing ConfigureAwait(false) changes only the continuation context; no "
                    "SynchronizationContext is captured and no RecordsAffected behaviour depends on it."
                ),
            },
            {
                "mutant_id": "stryker-configureawait-command-disposeasync",
                "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:142",
                "decision": "justified",
                "basis": "reasoning",
                "reason": (
                    "Same structural equivalence as the reader ConfigureAwait removal; the disposal "
                    "itself is covered by AsyncRelease_DisposesTheCommand."
                ),
            },
        ],
        "rv1_e226_09": (
            "invalid (its row-count patch counted columns/result sets) — superseded prospectively by "
            "E226-09-BATCH-ROWS and E226-09-N6; never reused as rv2 proof"
        ),
    }


def main() -> int:
    fails: list[str] = []
    transcript: list[str] = []

    def say(line: str = "") -> None:
        transcript.append(line)
        print(line)

    def fail(line: str) -> None:
        fails.append(line)
        say(f"GUARD-FAIL: {line}")

    saved: dict[str, bytes] = {}
    hashes_before: dict[str, str] = {}
    for rel in (OWNER, RUNNER):
        path = REPO / rel
        saved[rel] = path.read_bytes()
        hashes_before[rel] = sha256(path)
        (WORK / Path(rel).name).write_bytes(saved[rel])

    _, tree_before = run(["git", "diff", "--no-ext-diff", "--", OWNER, RUNNER])
    tree_hash_before = hashlib.sha256(tree_before.encode()).hexdigest()

    say("== D226 rv2 mutation verification ==")
    say(f"repo={REPO}")
    say(f"work={WORK}")
    for rel in (OWNER, RUNNER):
        say(f"before {rel} sha256={hashes_before[rel]}")

    def restore_all(tag: str) -> bool:
        ok = True
        for rel, data in saved.items():
            path = REPO / rel
            path.write_bytes(data)
            actual = path.read_bytes()
            same = actual == data
            digest = hashlib.sha256(actual).hexdigest()
            say(f"restore[{tag}] {rel} byte_identical={str(same).lower()} sha256={digest} hash_match={str(digest == hashes_before[rel]).lower()}")
            if not same or digest != hashes_before[rel]:
                ok = False
        return ok

    final_restore_ok = False
    records: list[dict] = []
    control_results: dict[str, dict] = {}
    base_build = {"exit_code": None, "ok": False}
    try:
        rc_base, out_base = run(["dotnet", "build", "nextorm.slnx", "-c", "Debug"])
        (WORK / "base-build.log").write_text(out_base, encoding="utf-8")
        base_build = {"exit_code": rc_base, "ok": rc_base == 0}
        say(f"base build exit={rc_base} ok={str(rc_base == 0).lower()}")
        if rc_base != 0:
            fail("base build failed")

        for name, spec in CONTROLS.items():
            if not base_build["ok"]:
                control_results[name] = {"ok": False, "reason": "base build failed"}
                continue
            rc, out = run(["dotnet", "test", spec["project"], "-c", "Debug", "--no-build", "--filter", spec["filter"]])
            (WORK / f"control-{name}.log").write_text(out, encoding="utf-8")
            total = count_of(out, "total")
            failed = count_of(out, "failed")
            ok = rc == spec["exit"] and total == spec["expected_total"] and failed == 0
            control_results[name] = {"exit_code": rc, "total": total, "failed": failed, "ok": ok}
            say(f"control {name}: exit={rc} total={total} failed={failed} ok={str(ok).lower()}")
            if not ok:
                fail(f"clean control {name} did not pass as declared")

        for mutant in MUTANTS:
            mid = mutant["id"]
            say()
            say(f"== {mid} — {mutant['frozen_phrase']} ==")
            applied = True
            patch_file = MUTDIR / Path(mutant["patch"]).name

            if mutant.get("apply_git"):
                rc, out = run(["git", "apply", mutant["patch"]], cwd=R)
                say(f"git apply {mutant['patch']}: exit={rc}")
                if rc != 0:
                    fail(f"{mid}: git apply failed")
                    applied = False
            else:
                for rel, old, new, expected in mutant.get("hunks", []):
                    text = (REPO / rel).read_text(encoding="utf-8")
                    found = text.count(old)
                    if found != expected:
                        fail(f"{mid}: PATCH_NO_MATCH {rel} found={found} expected={expected}")
                        applied = False
                        break
                    (REPO / rel).write_text(text.replace(old, new), encoding="utf-8")

            if not applied:
                restore_all(f"{mid}-after-patch-failure")
                continue

            hashes_mutated = {rel: sha256(REPO / rel) for rel in mutant["files"]}
            changed = any(hashes_mutated[rel] != hashes_before[rel] for rel in mutant["files"])
            say(f"mutated sha256={hashes_mutated} changed={str(changed).lower()}")
            if not changed:
                fail(f"{mid}: patch is a no-op (no byte changed)")
                restore_all(f"{mid}-noop")
                continue

            rc_build, out_build = run(["dotnet", "build", mutant["project"], "-c", "Debug"])
            (WORK / f"{mid}-build.log").write_text(out_build, encoding="utf-8")
            say(f"build exit={rc_build}")
            if rc_build != 0:
                fail(f"{mid}: build failed for {mutant['project']} (infrastructure failure, not a kill)")
                restore_all(f"{mid}-after-build-failure")
                continue

            rc_test, out_test = run(["dotnet", "test", mutant["project"], "-c", "Debug", "--no-build", "--filter", mutant["filter"]])
            (WORK / f"{mid}-test.log").write_text(out_test, encoding="utf-8")
            log_text = out_test
            total = count_of(log_text, "total") or 0
            failed_n = count_of(log_text, "failed") or 0

            m = re.search(r"^failed (.*?) \(", log_text, re.M)
            failing_test = m.group(1) if m else ""
            assertion = ""
            lines = log_text.split("\n")
            for i, line in enumerate(lines):
                if line.startswith("failed "):
                    for follow in lines[i + 1:i + 8]:
                        if follow.startswith("  ") and not follow.startswith("  from "):
                            assertion = follow.strip()
                            break
                    break
            stack_bound = bool(re.search(rf"at\s+{re.escape(mutant['designated_test'])}\s*\(", log_text))
            infra = [t for t in INFRA_TOKENS if t in log_text]

            say(f"test exit={rc_test} total={total} failed={failed_n}")
            say(f"  failing_test={failing_test}")
            say(f"  assertion={assertion}")
            say(f"  designated_test_bound={str(stack_bound).lower()} infra_tokens={infra}")

            conditions = {
                "control_ok": control_results.get(mutant["control"], {}).get("ok", False),
                "mutated_exit_nonzero": rc_test != 0,
                "failed_tests_executed": failed_n >= 1 and total >= 1,
                "designated_test_failed": failing_test == mutant["designated_test"],
                "stack_bound": stack_bound,
                "assertion_evidence": all(token in log_text for token in mutant["expected_actual"]),
                "no_infrastructure_diagnostic": not infra,
            }
            if infra:
                fail(f"{mid}: infrastructure diagnostic present ({infra[0]!r}) — cannot count as a kill")
            for name, ok in conditions.items():
                if not ok:
                    fail(f"{mid}: kill condition failed: {name}")
            killed = all(conditions.values())
            say(f"  conditions={conditions}")
            say(f"  kill_status={'killed' if killed else 'unverified'}")

            restore_ok = restore_all(mid)
            if not restore_ok:
                fail(f"{mid}: restore did not compare byte-identically")

            records.append({
                "id": mid,
                "frozen_phrase": mutant["frozen_phrase"],
                "mechanism": mutant["mechanism"],
                "patch": f"artifacts/pdca/D226/rv2/{mutant['patch']}",
                "patch_sha256": sha256(patch_file) if patch_file.exists() else None,
                "before_sha256": {rel: hashes_before[rel] for rel in mutant["files"]},
                "after_sha256": hashes_mutated,
                "build_exit_code": rc_build,
                "test_exit_code": rc_test,
                "selected_total": total,
                "failed_count": failed_n,
                "killing_test": failing_test,
                "assertion_message": assertion,
                "stack_bound": stack_bound,
                "infrastructure_tokens": infra,
                "conditions": conditions,
                "kill_status": "killed" if killed else "unverified",
                "restore_ok": restore_ok,
            })
    finally:
        final_restore_ok = restore_all("final-cleanup")

    say()
    say(f"final_restore_ok={str(final_restore_ok).lower()}")
    if not final_restore_ok:
        fail("final restoration did not compare byte-identically")

    _, diff_after = run(["git", "diff", "--no-ext-diff", "--", OWNER, RUNNER])
    tree_hash_after = hashlib.sha256(diff_after.encode()).hexdigest()
    tree_unchanged = tree_hash_before == tree_hash_after
    say(f"tree_unchanged={str(tree_unchanged).lower()} (diff hash {tree_hash_before[:12]} -> {tree_hash_after[:12]})")
    if not tree_unchanged:
        fail("the working-tree diff for the mutated files changed")

    frozen_ids = [m["id"] for m in MUTANTS if m["frozen_phrase"] in
                  ("remove capture", "replace the total with row-count", "replace the sentinel with 0",
                   "reset the cache on repeated release", "read a disposed reader from the getter")]
    required = {"remove-capture", "total-to-row-count", "sentinel-to-0", "reset-on-repeated-release",
                "disposed-reader-from-getter"}
    if set(frozen_ids) != required:
        fail(f"frozen mutant set mismatch: {sorted(set(frozen_ids))}")

    report = {
        "task": "T226 (#226) — rv2 mutation verification (P226-R2-03)",
        "contract_rv": 2,
        "revision": "r=2",
        "verifier": "artifacts/pdca/D226/rv2/verify-mutations.sh",
        "exit_file": "artifacts/pdca/D226/rv2/mutation.exit",
        "restoration": {
            "compared_byte_identically": final_restore_ok and all(r["restore_ok"] for r in records),
            "per_mutant_restore_ok": {r["id"]: r["restore_ok"] for r in records},
            "final_cleanup_restore_ok": final_restore_ok,
            "working_tree_diff_unchanged": tree_unchanged,
            "before_sha256": hashes_before,
        },
        "controls": control_results,
        "base_build": base_build,
        "frozen_ids": sorted(required),
        "mutants": records,
        "notes": [
            "Application mechanism: the row-count patch artifact is generated from CURRENT bytes by make-row-count-mutant.py and validated with `git apply --check` (exit 0); at run time every mutant (including this one) is applied through its parsed hunks, because `git apply` is a CRLF no-op in this worktree (core.autocrlf=true) — it exits 0 without changing a byte, which this verifier's no-op guard detects and reports.",
            "The other mutants' rv1 patches are copied byte-identically into mutations/ (cmp + sha256 recorded in inherited-contract.json) and applied through the same hunk mechanism.",
            "kill_status consumes the verified classifier above, never `exit != 0 && killing_test`.",
        ],
        "provenance": build_provenance(records),
        "guards": fails,
    }
    REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    for mid in required:
        if not (MUTDIR / f"{mid}.patch").is_file():
            fail(f"missing patch artifact for {mid}")

    say("== summary ==")
    for r in records:
        say(f"{r['id']:44} kill={r['kill_status']:9} restore_ok={str(r['restore_ok']).lower():5} test={r['killing_test']}")

    status = 1 if fails else 0
    EXITFILE.write_text(f"{status}\n", encoding="utf-8")
    LOG.write_text("\n".join(transcript) + "\n", encoding="utf-8")
    print(f"mutation_exit={status}")
    return status


if __name__ == "__main__":
    try:
        code = main()
    except Exception as exc:  # any unexpected error is a fail-closed abort
        message = f"GUARD-FAIL: unexpected exception {type(exc).__name__}: {exc}"
        print(message)
        try:
            EXITFILE.write_text("1\n", encoding="utf-8")
        except Exception:
            pass
        try:
            with LOG.open("a", encoding="utf-8") as fh:
                fh.write(message + "\n")
        except Exception:
            pass
        code = 1
    sys.exit(code)
