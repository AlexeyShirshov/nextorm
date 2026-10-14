# D226 / rv2 — summary (P226-R2-06, candidate for CHECK; no PASS declared here)

- task: T226 (#226) — `ProcedureResult`/`BatchResult` `RecordsAffected`; revision **r=2**, cycle N=1,
  iteration n=1/3, evidence contract **rv=2**; artifact root `artifacts/pdca/D226/rv2/`; owner `DO-G1`.
- independent CHECK owns the verdict; this file records what r=2 produced, not a pass.

## What r=2 changed

1. **The frozen row-count obligation is now executed literally.** `make-row-count-mutant.py` generates
   `mutations/total-to-row-count.patch` from the CURRENT bytes at both batch release sites
   (`BatchRunner.cs:320`, `:349`): the total is substituted by `System.Linq.Enumerable.Sum(sets, set =>
   set.Count)` — a genuine returned-row count (`site count 2`, `git apply --check` exit 0).
2. **The killing oracle was strengthened without renaming**
   (`tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs`,
   `Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount`): both returned lists
   are retained once, their combined count asserted to be **3**, and `RecordsAffected` asserted to be
   **1** afterwards. Clean control exit 0 (1/1); mutated exit 2 with
   **“Expected result.RecordsAffected to be 1, but found 3”**.
3. **The mutation verifier is fail-closed** (`verify-mutations.sh` → `mutation-runner.py`): base-build
   gate, clean controls (core 16/16, sqlite row-count control 1/1), per-restore **actual byte
   comparisons** with hashes persisted per mutant and in the final cleanup, report generated after the
   final restoration, and a verified kill classifier (successful build + matching control + nonzero
   mutated exit with executed failures + designated test bound to its failure record and stack +
   declared expected/actual + no infrastructure token, with infrastructure rejection taking precedence).
   Result: `mutation.exit` = 0, **8/8 mutants killed** (the five frozen ones plus X1–X3), every restore
   and the final cleanup byte-identical, `guards = []`.
4. **Six negative controls** (`verify-mutations-negative.sh`, isolated copies under `/tmp/opencode`):
   all six exit 1 for the intended reason — `PATCH_NO_MATCH`; surviving mutant
   (`kill condition failed: mutated_exit_nonzero`); restore comparison failure; failing control;
   invalid artifact path (`unexpected exception`); and **N6 infrastructure rejection**
   (`infrastructure diagnostic present`). Aggregate exit 0.
5. **Provenance erratum** (additive): X1–X3 substantiate the three former survivors by hand-mutant
   artifacts; the scoped **pre-fix** Stryker output (`artifacts/pdca/D226/rv1/check/**`) exists as
   history but its identity against the final tree is unverified and it is NOT a substantiated post-fix
   rerun; the two `ConfigureAwait` entries stay `justified` reasoning; no `stryker_re_run` claim exists
   in rv2 records; rv1's invalid E226-09 is not reused.
6. **Delta/E226-06 artifacts** vs the r2 starting tree: production bytes unchanged
   (`production-before.sha256` == `production-after.sha256`, 4 files), `branch-delta.patch` empty and no
   tracked file outside the snapshot baseline plus the two allowed r2 changes.
7. **rv1 is byte-identical**: 48 artifacts hashed in `rv1-frozen.sha256` and re-verified by
   `verify-accepted-evidence.py` / `verify-provenance.py` / `verify-plan-evidence.sh` (0 changed).

## Evidence slots (real child exit codes via `run-evidence.sh`)

| slot | command | exit | selected / passed / failed / skipped |
|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | — (0 warnings / 0 errors) |
| core-tests | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~RecordsAffected` | 0 | 16 / 16 / 0 / 0 |
| sqlite-tests | same project, `~RecordsAffected` | 0 | 6 / 6 / 0 / 0 |
| row-count-control | sqlite `~Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount` | 0 | 1 / 1 / 0 / 0 |
| integration-tests | integration `~RecordsAffected` (DOCKER_HOST) | 0 | 27 / 27 / 0 / 0 |
| clickhouse-tests | integration `~ClickHouseRecordsAffected` (DOCKER_HOST) | 0 | 5 / 5 / 0 / 0 |
| batch-integration-control | integration `~Batch_SideEffect_ShouldExposeOneDriverRecordsAffected_NotTheResultSetCount` (DOCKER_HOST) | 0 | 4 / 4 / 0 / 0 |

## Verifier / validator results

`verify-accepted-evidence.py --all` 0 · `verify-provenance.py` 0 · `verify-r2-delta.py check` 0 ·
`verify-mutations.sh` 0 (`mutation.exit` 0) · `verify-mutations-negative.sh` 0 ·
`validate-manifest.sh` 0 (via `scripts/validate_inner_loop.py manifest`) ·
`validate_inner_loop.py report evidence.json` 0 · `verify-plan-evidence.sh` 0 (zero MISS).
Manifest: 11/11 rows `met`, no `open`, no `na`.

## Candidate state for CHECK

All eleven required rows carry actual-artifact evidence; the five frozen mutants are assertion-killed
with the literal row-count semantics; the six negative controls behave correctly; provenance is
truthful; production and rv1 bytes are unchanged. **No final PASS or D completion is declared here** —
the independent CHECK verdict and, if needed, at most two targeted read-only re-gather batches decide.
