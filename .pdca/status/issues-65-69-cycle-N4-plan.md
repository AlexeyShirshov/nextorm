# Immutable PLAN artifact — task `issues-65-69`, cycle N=4, revision r=1
Source: `pdca-planner` return supplied in the dispatch.
Counters: `N=4`, `r=1`, `n=0`.

---8<--- PLAN BEGIN
# Cycle N=4 PLAN — Step-1 Remainder (`#65` `MappingScope`), `r=1`, `n=0`

## (a) Form, Overlay & Scope
- **Form & overlay:** `incremental` + `pdca-dotnet` (`coder+.NET`), carried by reference from `.pdca/status/issues-65-69.md` and `.pdca/status/issues-65-69-cycle-N2-plan.md` §1 (no reclassification, no revision bump: `N=4, r=1, n=0`).
- **Original task & requested/delivered result:** Verbatim by reference to `.pdca/status/issues-65-69.md` §Original task — Step-1 remainder `#65` (`MappingScope` isolation, fluent `From<T>(cfg)` / `Map<T>()` parity, cache-key isolation, and regression safety); `#69` remains out of scope until Step 1 is accepted.

## (b) Acceptance Criteria in Scope & Evidence-Reading Contract
- **Criteria in scope (verbatim by exact reference to `.pdca/status/issues-65-69-cycle-N2-plan.md` §3, unchanged):** `AC-65-1`, `AC-65-2`, `AC-65-3`, `AC-REPO-1`. No new criteria, rows, or rewordings are added.
- **Evidence-reading contract (what constitutes comparable, sufficient evidence per unchanged AC):**
  - **`AC-65-1` (Scope isolation, cloning, additive re-invocation, cache-key distinction, zero static mutation):** Satisfied by deterministic unit/core and dialect test evidence exercising `MappingScope` creation, `WithMapping(scope)` cloning, independent `From<T>(cfg)` registrations per scope, additive re-entry on the same scope, distinct plan-cache entries across distinct scopes vs. default context, and untouched process-wide static metadata for scoped contexts, with exact passing test identities matching the compiled test methods.
  - **`AC-65-2` (End-to-end provider parity across all 6 providers):** Satisfied by live provider execution evidence on the unchanged Step-1 candidate tree demonstrating `From<T>(cfg)` / `Map<T>()` parity across SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, and ClickHouse, paired with a skip-budget-compliant canonical live suite measurement (`0 failed`, skip count `<= 197` pinned budget with NativeAOT runner binary present at its expected publish output so the NativeAOT gate executes rather than skips).
  - **`AC-65-3` (TVP and `[DecimalPrecision]` cache/validation invariants under `MappingScope`):** Satisfied by passing `TvpMetadataCacheTests` and `DecimalPrecisionRules` validation evidence showing TVP auto-resolution never seeds configured metadata, defers to scoped/configured `From<T>(cfg)`, clears via `DataContextCache.Clear()`, and validates `[DecimalPrecision(p, s)]` against `converter?.ProviderType ?? propertyType`.
  - **`AC-REPO-1` (Build, conventions, and workspace hygiene):** Recorded scope is strictly zero-warning solution build (`TreatWarningsAsErrors=true`), passing repository convention tests (`AssertionStyleTests`, `IntegrationSuiteScopingTests`), CRLF hygiene, `<= 10` `.cs` files per folder, and preservation of out-of-scope dirty worktree files; full live-suite execution belongs to `AC-65-2`/`AC-65-3` evidence, not `AC-REPO-1`.

## (c) Manifest Required Row Set & Fields
- **Exact row set (4 rows, 1:1 with the acceptance set — no extra acceptance rows):**
  1. `AC-65-1`
  2. `AC-65-2`
  3. `AC-65-3`
  4. `AC-REPO-1`
- **Required fields per row:** `ac_id` | `verbatim_ref` (`.pdca/status/issues-65-69-cycle-N2-plan.md#§3`) | `candidate_footprint_digest` (identical Step-1 tree state across bound evidence) | `evidence_paths` (exact relative log/trx/artifact paths under `.pdca/issues-65-69/`) | `verified_facts` (exact test/build/skip-budget counts and exact compiled test names; `PD3` prevention) | `ac_status` (populated from actual verification outcome before handoff, never left `open` by construction; `PD4` prevention).

## (d) ClickHouse `Code: 241` Disposition
- **Environment limitation vs. product defect:** `DB::Exception: (total) memory limit exceeded (Code: 241)` in `ClickHouseContainer.cs:59` is a container server-side 1.50 GiB query-memory cap (`ServerMemoryUsageBytes = 1536 MiB` while container RSS is ~620–989 MiB).
- **Comparability rule:** When a run exhibits `Code: 241` solely on rotating, unmodified tests outside the `#65` candidate footprint (e.g., `ClickHouseImplicitNavigationTests.Wide_count_boundary_should_stay_in_range_and_wide`, `ClickHouseIntegrationTests.SequenceMatch_ShouldMatchPattern`, `ClickHouseExtremeRowNativeSpecificTests.Counterexample_DirectArgMinOnFloatingKey*`) AND a same-generation canonical capped full-suite run on the identical Step-1 candidate tree is green and skip-budget-compliant (`8537 passed, 0 failed, 197 skipped`, budget OK, as in `rv3/attempts/1/canonical-capped-full-suite-rerun.log`) alongside green targeted ClickHouse `#65` parity tests, the rotating `Code: 241` occurrence is classified as a known container memory-cap environment limitation rather than an unmet `AC-65-2`/`AC-REPO-1` product regression. Any failure inside the `#65` footprint or any deterministic failure remains an unmet defect.

## (e) Stop Conditions & Bounds
- **Pre-set boundary rule:** On independent CHECK confirmation that `AC-65-1`, `AC-65-2`, `AC-65-3`, and `AC-REPO-1` are met under this contract, Step 1 (`#65`) is accepted and the campaign transitions at the step boundary to Step 2 (`#69`).
- **Bounds & prohibitions:** Same `r=1` contract; max 3 attempts (`n <= 3`) before `pdca-escalate`; no edits to protected out-of-scope dirty paths; no weakening of skip budget (`197`) or warning-free build gate; no git commits, merges, or pushes.
---8<--- PLAN END
