# Collection rc2-final — CHECK (C) evidence contract

Status artifact for the integrated collection CHECK of `rc2-final`. Self-contained: every claim
cites a raw log under `artifacts/pdca/collection-rc2-final/C/` (all produced fresh on the integrated
tree @ `4328c1bf`) or a `file:line` in a per-task status file. Nothing is fabricated; gaps are
recorded in §8 as `missing`/`N/A`.

## 1. Scope and inputs

- **Collection id:** `rc2-final` (milestone `1.0.9-rc2`, #20).
- **Branch / HEAD:** `1.0.9-rc2` @ `4328c1bf4c26ae9976488ee6f662221f1db6a28d`; `git diff --name-only 4328c1bf..HEAD` is empty (integrated tree == HEAD).
- **Mode:** single lane **G1** in the current worktree — no group/task worktrees, no group branch, no `git merge --no-ff`; integration = the authorized commits on `1.0.9-rc2`.
- **Terminal DO state:** 3 DONE (T208/#208 `09f772a7`, T170/#170 `9715f13f`, T151/#151 `57e02e66`), 2 INCOMPLETE preserved (T160/#160 `c817ac95`, T206/#206 `3d1b7036`), 2 EXCLUDED-GAP (T171/#171, T172/#172, issues OPEN).
- **Raw evidence root:** `artifacts/pdca/collection-rc2-final/C/`.
- **Inputs:** `docs/specs/status/collection-1.0.9-rc2-final.md`; per-task `docs/specs/status/rc2-*.md`; reference schema `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`.

## 2. Pinned rv table

`selected_variant` = `pdca-dotnet` for every row.

| task | issue | status | rv | r | task commit | source status file:`line` |
|---|---|---|---|---|---|---|
| T208 | #208 | done | 3 | 3 | `09f772a7` | `rc2-208-sqlserver-native-json-param-alias-1.md:135` (CHECK PASS r=3/rv3) |
| T170 | #170 | done | 1 | 1 | `9715f13f` | `rc2-170-onetoone-fk-uniqueness-1.md:5,:343` (CHECK PASS r=1/rv1, 9/9) |
| T151 | #151 | done | 2 | 2 | `57e02e66` | `rc2-151-stryker-mutation-1.md:4,:735` (CHECK PASS r=2/rv2, 15/15) |
| T160 | #160 | incomplete | 1 | r=2 closed (no DO completion, no r=3) | `c817ac95` | `rc2-160-join-alias-mixing-1.md:6,:136-137` |
| T206 | #206 | incomplete | 1 | 1 | `3d1b7036` | `rc2-206-sourcegen-cs0111-alias-join-1.md:3,:104-106` |

**Resolved discrepancies.**

- **T208** — collection table `collection-1.0.9-rc2-final.md:42` records `plan_revision r=1`, `rv1`; the status file's own CHECK line `rc2-208-...:135` records **`CHECK PASS (r=3, contract rv3)`** (r1→r2 at `:107/:109`, r2→r3 at `:121/:123`). **Authoritative = status file `r=3/rv3`**; the collection-table `r=1/rv1` is stale.
- **T208 commit** — the status file still carries the literal placeholder `commit: <sha>` (`:139`); the actual task commit is `09f772a7`.

## 3. Mandatory-row-version ledger

| task | mandatory rows (rv) | satisfying evidence |
|---|---|---|
| T208 | `E208.00..E208.08` (rv3; `:89`) | CHECK PASS r=3/rv3 — `:135`, journal `:141`; evidence root `artifacts/pdca/D208/rv1/` |
| T170 | `REQ170-TRUST`/`R170-01..R170-07` (rv1; `:84`) | CHECK PASS r=1/rv1 — `:343` (9/9 rows met); evidence root `artifacts/pdca/D170/rv1/` |
| T151 | rv1 `R-PROV/E01..R-CHECK/E10` (`:78`); rv2 adds `R-ENTRY-BIND`,`R-RAW-TRACE`,`R-LEDGER-NEGATIVE`,`R-LOOP`,`R-BOUNDARY` (`:702`) | CHECK PASS r=2/rv2 — `:735` (15/15 rows met); bundles `artifacts/pdca/D151/rv1`, `.../rv2` |
| T160 | `N160-01..N160-16` (`:114`); acceptance `R160-01..R160-11` (`:23`,`:98-111`) | **no CHECK** — terminal incomplete at PLAN/DO (`P160.close` `:136`); patch `missing` in worktree (§8 G1) |
| T206 | `E00..E07` (rv1; `:82`); acceptance `R01..R06` (`:21-26`) | **no CHECK** — terminal incomplete at DO (`P206.close` `:104-106`); evidence `artifacts/pdca/D206/rv1/` |

T160/T206 have no CHECK verdict **by construction** (both terminal `incomplete` before CHECK) — recorded as N/A, not `missing`.

## 4. Priority + variant matrix

| task | priority | variant matrix |
|---|---|---|
| T208 | inline `P1 by construction` `:78` (no separate heading) | `## Test strategy and closed variant matrix` `:65` |
| T170 | `## Priority matrix` `:225` (+ provider/flag `:176`) | `## Variant matrix (every row closed)` `:162` |
| T151 | inline P1 `:288`; `## Priority, docs, performance…` `:67` (no separate priority heading) | variant matrix `:52` |
| T160 | inline `priority=P1` `:114` (no separate heading) | `## Test strategy and closed variant matrix` `:63` |
| T206 | inline `P1 rows R01–R04/R06` `:70` (no separate heading) | `## Design checklist, test strategy and closed variant matrix` `:61` |

## 5. AC1–AC5 + perf references (fresh on `4328c1bf`; raw paths under `artifacts/pdca/collection-rc2-final/C/`)

| AC | raw path(s) | key evidence |
|---|---|---|
| AC1 build | `build-debug.log`, `build-release.log` | each exit 0; `Build succeeded. 0 Warning(s) 0 Error(s)` |
| AC2 unit | `unit-*.log`, `unit-projects.txt`, `unit-summary-numbers.txt` | 11 runnable projects; 6303 total / 6302 succeeded / 0 failed / 1 skipped (SQLite LOB probe, `unit-sqlite.log`). `nextorm.alias.poc` excluded — not a test project (exit 1 `No test projects were found.`) |
| AC3 integration | `integration.log`, `perprovider-*.log`, `integration-totals-reconciliation.txt`, `provider-matrix.md` | final line `nextorm.integration.tests  Total: 3591, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0`; exit 0; providers RAN = PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse, SQLite; `grep -c "is not available"` = 0 ⇒ 197 are per-test capability skips, not provider-unavailable. Reconciliation: provider buckets 3475 + non-provider 116 = 3591 exact; skips 196+1=197. **No gap.** The earlier observed "3433 vs total" gap does not reproduce — it was a bucket-attribution artifact (the `LobCapabilityProbeTests` MySQL/MariaDB split) |
| AC4 coverage | `coverage-collect-full.log`, `coverage-full.cobertura.xml`, `coverage-Summary-full.txt`, `reportgenerator-full.log` | CI shape (whole solution incl. integration, `dotnet test --no-build`): **Line 88.3% (50601/57257)**, **Branch 80.3% (27186/33817)**; assemblies core 88.2 / postgres 90.2 / sqlite 90.5 / sqlserver 95.0. Both ≥ 85 / 75. The earlier restricted 11-unit run (Line 84.7 / Branch 78.1) was **superseded by re-gather**, not a product defect |
| AC5 docfx | `docfx.log` | exit 0; `Build succeeded with warning. 2 warning(s) / 0 error(s)` (2 pre-existing duplicate `AnalyzerReleases.*.md` warnings) |
| PERF | `perf-acceptance.log` | `--anyCategories=acceptance`: exactly 7 cases, 0 failures, wall 53.89 s ≤ 240 s. `Cached_ToList/Prepared_ToList` = 2.07 vs baseline 1.87 = **+10.9% (< 20%, no trigger)** |

## 6. Per-DONE-task spot verification (integrated tree)

| task | spot result |
|---|---|
| T208 | `src/nextorm.core/Visitors/NormSqlTranslator.cs:85` (method `TranslateNormParam`) sets `visitor.NeedAliasForColumn = true`; filtered `tests/nextorm.sqlserver.tests` `~ProjectedParameter` → exit 0, 3/3 passed (a 4th name `ProjectedStringParameter_*` is not matched by that filter — filter artifact, not a failure). Log `spot-t208.log` |
| T170 | `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:535-537` throws `…matched more than one distinct…`; filtered `tests/nextorm.core.tests` `~JoinIntoOneToOne` → exit 0, 30/30 passed. Integration oracles present: `tests/nextorm.integration.tests/CommonTestSuite.JoinInto.cs:322,:336`; `ClickHouseJoinIntoOneToOneTests.cs:19,:33`. Log `spot-t170.log` |
| T151 | bundles exist: `artifacts/pdca/D151/rv1/raw/` (33 files), `rv1/{manifest,mutant-ledger,row-hashes}.json`, `rv2/{manifest,mutant-ledger,row-hashes}.json`; validator `python3 scripts/validate_inner_loop.py manifest …` → exit 0 for rv1 and rv2 (`spot-t151-validate.log`). Hash re-verify: rv2 43/44 inventory hashes match; the rv2 `provenance.md` stale declared hash is now covered by an explicit reconciliation record at `artifacts/pdca/D151/rv2/RECONCILE.md` (declared `ed72fbeb`, actual `42d81b91`, LF-normalized `bd312fd3`, non-EOL post-freeze rewrite 02:44:01 vs freeze 02:39:47); refreshed authenticated ledger `artifacts/pdca/D151/rv2/row-hashes.reconciled.{sha256,json}`; collection-level full-bundle hashes `artifacts/pdca/collection-rc2-final/C/t151-rv2-actual-hashes.sha256`; validator exit 0; rv2 obligations/rows preserved. rv1 mismatch = `tools/stryker/d151-evidence.py` declared `0a4c5b70` (pre-commit) vs on-disk `e86d9522` (matches rv2), explicitly `superseded_by_rv2:true` in `rv2/verifier-result.json:84` → resolved |

## 7. Non-green characterization

| task | classification | preserved artifact |
|---|---|---|
| T160 | terminal `incomplete` (fresh redo not executable; R02 red only in rejected patch) | STOP patch `D160-STOP-incomplete.patch` **ABSENT in this worktree**; present on branch `wip/d160-incomplete` (HEAD `214c1323`). Status file + `docs/specs/status/rc2-160-evidence/` present (untracked). Commits `c817ac95`, plan `1675a978` |
| T206 | terminal `incomplete` (NORMGEN007 conflict unreachable; no product change) | evidence `artifacts/pdca/D206/rv1/` present: `BLOCKER.md`, `D1-red.log`, `D1D2-evidence.json`, `probe.log`, `scope.json`. Commits `3d1b7036`, plan `22f0171d` |
| T171/T172 | excluded-gap, issues OPEN | none (not-actionable-now) |

## 8. Honest gaps / open rows

| # | gap | status | where |
|---|---|---|---|
| G1 | T160 STOP patch in this worktree | **`missing`** — present only on `wip/d160-incomplete` | `collection-1.0.9-rc2-final.md:40` |
| G2 | T160/T206 per-task CHECK verdict | **N/A by construction** (terminal incomplete before CHECK) | `rc2-160-...:136`; `rc2-206-...:104` |
| G3 | T208 rv in collection table | **inconsistent** — table `r=1/rv1` vs status `r=3/rv3`; authoritative = status | `collection-1.0.9-rc2-final.md:42` vs `rc2-208-...:135` |
| G4 | T151 rv2 `provenance.md` declared hash | **reconciled — 1 file (explicit record, obligations preserved)** — declared `ed72fbeb` vs actual `42d81b91` (non-EOL, post-freeze rewrite); validator exit 0 | `artifacts/pdca/D151/rv2/RECONCILE.md`; `artifacts/pdca/D151/rv2/row-hashes.reconciled.{sha256,json}`; `artifacts/pdca/collection-rc2-final/C/t151-rv2-actual-hashes.sha256` |
| G5 | T208 status-file commit | **placeholder** `commit: <sha>`; actual `09f772a7` | `rc2-208-...:139` |
| G6 | DocFX warnings | 2 pre-existing duplicate `AnalyzerReleases.*.md`; non-fatal, exit 0 | `docfx.log` |
| G7 | `nextorm.alias.poc` in test enumeration | **excluded** — not a test project (exit 1) | `unit-nextorm.alias.poc.log` |
| G8 | working tree | 0 tracked-modified; 21 untracked under `BenchmarkDotNet.Artifacts/results/` (out of scope) | `git-state.log` |

## 9. Provenance

- AC1–AC5, perf and spot commands run **fresh on `4328c1bf`** by the `coder` role; raw logs under `artifacts/pdca/collection-rc2-final/C/`.
- No tracked source modified during the C gather.
- T151 bundle integrity characterized by `coder` git/FS forensics; `check` (medium) renders the C verdict over this contract.

## 10. Re-gather record (post first C)

- First C verdict was **`blocked-for-evidence`**.
- Gaps addressed here:
  - **(a) T151 rv2 stale declared `provenance.md` hash** → explicit reconciliation record
    `artifacts/pdca/D151/rv2/RECONCILE.md` (declared `ed72fbeb`, actual `42d81b91`, LF-normalized
    `bd312fd3`, non-EOL post-freeze rewrite 02:44:01 vs freeze 02:39:47; the prior rv2 obligations and
    all 15/15 CHECK rows are preserved and NOT withdrawn) plus the refreshed authenticated ledger
    `artifacts/pdca/D151/rv2/row-hashes.reconciled.{sha256,json}` and collection-level full-bundle
    hashes `artifacts/pdca/collection-rc2-final/C/t151-rv2-actual-hashes.sha256`. **No product defect;
    no DO iteration.**
  - **(b) collection-level frozen evidence manifest** produced at
    `artifacts/pdca/collection-rc2-final/C/manifest.json` and validated exit 0 by
    `python3 scripts/validate_inner_loop.py manifest`.
- Remaining disclosures: **G1 / G2 / G6 / G7 / G8** as before.
- Reconcile log: `artifacts/pdca/collection-rc2-final/C/t151-provenance-reconcile.log`.
