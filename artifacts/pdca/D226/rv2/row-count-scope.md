# D226 / rv2 — row-count mutation scope (P226-R2-02)

- task: T226 (#226), revision r=2, contract rv=2, artifact root `artifacts/pdca/D226/rv2/`
- frozen obligation (source: `docs/specs/status/D226-records-affected-1.md:132-135`): *replace the total
  with row-count*.

## What the mutant does

`mutations/total-to-row-count.patch` (generated from the CURRENT working-tree bytes by
`make-row-count-mutant.py`, never from HEAD) substitutes the driver's affected-row total with the sum
of the materialized result rows at both release sites of the batch runner:

```
- return new BatchResult(sets, resultTypes, columnNames, CommandReaderOwner.ReadRecordsAffected(reader));
+ return new BatchResult(sets, resultTypes, columnNames, System.Linq.Enumerable.Sum(sets, static set => set.Count));
```

- sites: `src/nextorm.core/DataContext/BatchRunner.cs:320` and `:349` (2 substitutions, verified by the
  generator: `sites=2 hunks=2`).
- the substituted quantity is a **genuine returned-row count**: the sum of the materialized
  `IList.Count` of the batch's result sets — not a column count and not a result-set count.
- no `CommandReaderOwner` substitution is made.

## How it is applied and validated

- `python3 make-row-count-mutant.py` → exit 0, `sites=2 hunks=2`, patch written to
  `mutations/total-to-row-count.patch`.
- `git apply --check mutations/total-to-row-count.patch` → **exit 0** (proof that the patch applies to
  the current bytes).
- The verifier applies the substitution as a hunk because `git apply` is a CRLF no-op in this worktree
  (`core.autocrlf=true`): it exited 0 without changing any byte, which the verifier's no-op guard
  detects (`patch is a no-op (no byte changed)`). The patch artifact and the `git apply --check` proof
  are kept; the application mechanism is recorded per mutant in `mutation-report.json`.

## Killing oracle and observed numbers

- clean control: `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount`
  → exit 0, 1 selected / 1 passed / 0 failed (`sqlite-rowcount-control.log`, `.exit` = 0).
- mutated: exit 2, 1 selected / 1 failed, designated killing test
  `NextORM.Sqlite.Tests.BatchMultipleResultTests.Execute_SideEffectBatch_ExposesOneDriverRecordsAffected_NotTheResultSetCount`,
  assertion **“Expected result.RecordsAffected to be 1, but found 3”** — i.e. expected **1**, actual **3**.
  The strengthened oracle retains both returned lists once, asserts their combined count is **3**, and
  asserts `RecordsAffected == 1` after the row assertions, so a returned-row substitution fails on the
  affected-total assertion (not on a row assertion).
- additional oracle: `CommonTestSuite.BatchMultipleResults.cs` (2 returned rows vs total 1) — the
  integration batch case; its recorded clean runs are in the rv2 evidence slots.

## Explicit limitation (nothing else is relabelled)

- `CommandReaderOwner.cs:74-91` has **no returned/read-row counter**, so a row-count substitution cannot
  be expressed there; `FieldCount` is a **column** count and is deliberately not used as a stand-in.
- the procedure read lists at `ProcedureResult.cs:149,375` are **not retained**, so no procedure-path row
  counter exists; pure DML has no result set and `Read<T>` throws at `:226-227`.
- the batch lists at `BatchRunner.cs:311,338` **do** provide genuine row counts, which is why the batch
  mutation fulfils the obligation literally. The procedure-path variant is therefore guarded as a
  documented unavailable quantity, not as an unexpressible obligation, and no procedure counter is
  fabricated.
