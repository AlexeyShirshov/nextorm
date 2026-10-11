# T211 (#211) per-parent top-N — code audit, RAW findings (CHECK gather stream)

- Repo: `/home/alex/sources/nextorm`, branch `1.0.9`.
- Diff under review: `e98ae60e` vs base `b527be6f` (T211 change), paths `src/`, `tests/`.
- This document is **raw findings only — NO PASS/FAIL verdict, NO severity aggregation into a decision, NO fixes applied.**
- Routing column: which role should act (`coder` for production/test/doc changes).
- Skills loaded: `dotnet-csharp-code-smells`, `dotnet-api-surface-validation`, `api-design`. `slopwatch` skill was not present in the available skill list (smuggle scan performed manually, see §2).

## 1. Findings (per file/chunk)

### 1.1 Critical

**C-1 — Split path silently drops child shapes the ranked lowering cannot render (silent wrong data).**
`src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:344-354` (`BuildChildCommand`)

- `BuildChildCommand` sets `command.PerParentPaging = true` whenever `HasPerParentTopN(child)` on a SQL provider, but never calls `child.UnsupportedPerParentPagingChildShapes()` (that guard is invoked only in the single-query path at `EntityBuilderEagerLoading.cs:284`; confirmed sole call site).
- `MakePerParentPagingSelect` (`src/nextorm.core/DataContext/SqlBuilder.cs:641-654`) renders only `select *, row_number()… from <source> where <condition>` + page bounds. It never renders `cmd.IsDistinct` (see the extreme-row sibling `MakeExtremeRowOuter` at `SqlBuilder.cs:1268-1269`), `_group`/`Having`, `LimitBy`, joins, hints, `PreWhere`, tablesample, derived `As` source, row locks, CTEs, `Final`, `ArrayJoin`.
- Result: a split `LoadWith` child such as `c => c.From<TChild>().Distinct().OrderBy(..).Limit(1)` or `GroupBy(..).Limit(1)` is accepted and the `Distinct`/`GroupBy` clause is **silently omitted** from the emitted SQL, returning wrong rows with no exception.
- This contradicts the public doc `docs/advanced/eager-loading-per-parent-paging.md:105` ("Rejected **before any SQL is generated**: … `Distinct`, `GroupBy`, `Having` …"), which is true only for single-query.
- Fix: in `BuildChildCommand`, before setting `PerParentPaging`, call `child.UnsupportedPerParentPagingChildShapes()` and throw `NotSupportedException` (same message shape as the single-query path) so both modes fail closed.
- Routing: `coder` (production + regression test on the split path).

### 1.2 Warning

**W-1 — Window guard permits an empty partition key → global rank instead of per-parent.**
`src/nextorm.core/DataContext/SqlBuilder.cs:712-713` (`MakePerParentRankExpression`)

```
if (builder is not null && sorting.Length == 0 && partition.Length == 0)
    throw new BuildSqlCommandException("A per-parent eager top-N child requires a partition key.");
```
- The conjunction requires **both** arrays empty, so a non-empty `sorting` with an empty `partition` emits `row_number() over (order by …)` — a **global** rank, silently paginating across all parents. Message asserts a partition key is required; the condition does not enforce it.
- Fix: guard on `partition.Length == 0` alone (partition is mandatory for per-parent semantics).
- Routing: `coder`.

**W-2 — Multiple paged navigations share a single `ORDER BY`; later navigation order is not preserved.**
`src/nextorm.core/Builders/EntityBuilder.cs:1639-1658` (`AppendEmbeddedChildOrdering`), consumed at `EntityBuilder.cs:1509`.

- Each embedded per-parent child's ordered keys are appended after the parent keys, in join order. With two paged navigations each yielding >1 row per parent, the denormalized row order is fixed by the first navigation's keys, so the second navigation's stitched rows are ordered by the first navigation's key interleave — its own `ORDER BY` is not guaranteed.
- The integration test `EagerPerParentPaging_SingleQuery_TwoPagedNavigations_AreIndependent` (`tests/nextorm.integration.tests/CommonTestSuite.EagerLoadPerParentPaging.cs:203-224`) uses `Limit(1)` per navigation, so it cannot observe this.
- Fix: either document the limitation and reject >1 paged navigation with multi-row pages, or order the outer pair with a synthetic per-navigation rank column instead of one concatenated `ORDER BY`.
- Routing: `coder` (needs product decision → may route via planner).

**W-3 — Shape guard runs only in the SQL pass; skipped in parameter mode.**
`src/nextorm.core/DataContext/SqlBuilder.cs:712` (`builder is not null && …`)

- The "requires a partition key" validation is inside `if (builder is not null)`, so the parameter pass (`_ctx.ParamMode`) never validates the shape. Shape validation is mode-independent and should not be coupled to whether a string is being built.
- Fix: hoist the guard out of the `builder is not null` condition.
- Routing: `coder`.

**W-4 — `JoinIntoStitcher` now bypasses the paged-child guard; confirm in-memory `JoinInto` + paged `LoadWith` is still fail-closed.**
`src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs:112-114` passes `allowPerParentTopN: true`, which returns before the per-parent loop in `ValidateLoadWithForJoinInto` (`EntityBuilderEagerLoading.cs:576-577`). The code comment claims the in-memory rejection is retained via `BuildChildCommand` (`EntityBuilderEagerLoading.cs:346-350`), but that assumes every `JoinInto`+`LoadWith` execution goes through the split child loader. Needs a targeted in-memory `JoinInto` + paged `LoadWith` test to prove fail-closed rather than assume.
- Fix: add the regression test; if it does not throw, restore the stitcher guard for the in-memory provider.
- Routing: `coder` (verification).

### 1.3 Suggestion

**S-1 — Dead defensive branch in the rank filter.**
`src/nextorm.core/DataContext/SqlBuilder.cs:865-871` (`AppendPerParentRankFilter`): the `!hasLower && !hasUpper` branch (`rn >= 1`) is unreachable because `PerParentPaging` is only set when `!Paging.IsEmpty` (`Paging.IsEmpty => _offset <= 0 && _limit <= 0`, `src/nextorm.core/Builders/Paging.cs:44`). Harmless; comment already flags it.
- Routing: `coder`.

**S-2 — Outer projection ignores `cmd.SelectList`.**
`src/nextorm.core/DataContext/SqlBuilder.cs:789,797` always projects `BuildExtremeRowEntitySelectList(entityType)` (the extreme-row sibling at `SqlBuilder.cs:1259-1304` honours `cmd.SelectList`/`IgnoreColumns`/`IsDistinct`). For today's eager child commands this is entity-shaped, so no live defect, but it makes the renderer non-total for a non-entity projection.
- Routing: `coder`.

**S-3 — Wrong XML docstrings (copy-paste) in provider tests.**
`tests/nextorm.mysql.tests/EagerLoadPerParentPagingTests.cs:9`, `tests/nextorm.sqlserver.tests/EagerLoadPerParentPagingTests.cs:9`, `tests/nextorm.clickhouse.tests/EagerLoadPerParentPagingTests.cs:9` all say `#211 per-parent top-N SQL generation on PostgreSQL` in a MySQL/SQL Server/ClickHouse test file. (`tests/nextorm.postgres.tests/...:9` is correct; `tests/nextorm.sqlite.tests/...` uses a different, correct wording.)
- Fix: name the actual provider.
- Routing: `coder`.

**S-4 — Four near-identical 197-line provider SQL-generation test files.**
`tests/nextorm.postgres.tests/EagerLoadPerParentPagingTests.cs`, `tests/nextorm.mysql.tests/EagerLoadPerParentPagingTests.cs`, `tests/nextorm.sqlserver.tests/EagerLoadPerParentPagingTests.cs`, `tests/nextorm.clickhouse.tests/EagerLoadPerParentPagingTests.cs` (all 197 lines) duplicate the same fixture + assertions; the PostgreSQL string in three of them indicates blind copy-paste. Consider a shared base or per-dialect parameterization.
- Routing: `coder`.

**S-5 — `(dynamic)` invocation in new tests.**
5 added lines (`tests/nextorm.{postgres,mysql,sqlserver,clickhouse}.tests/EagerLoadPerParentPagingTests.cs` and one more) call `((dynamic)ctx.GetPreparedQueryCommand((dynamic)cmd, false, false, CancellationToken.None)).DbCommand.CommandText`. Test-only, but bypasses compile-time checking.
- Routing: `coder`.

**S-6 — Null-forgiving added on values guaranteed by guard.**
5 added `!` usages: `src/nextorm.core/DataContext/SqlBuilder.cs:673`, `:678` (`inner!`), `:783` (`_ctx.AliasProvider!`), `:793` (`outer!`), and `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:47` (`cmd._dataContext!`). All mirror the existing `MakePortableExtremeRowSelect`/`MakeExtremeRowOuter` pattern (guarded by `_ctx.ParamMode`); low risk. See §2.
- Routing: `coder` (optional).

## 2. Suppression / slop scan (counts)

Changed `.cs` files: 28 (10 `src/`, 18 `tests/`).

| Pattern | Added by T211 | Whole changed-file total | Justified | Suppressed |
|---|---|---|---|---|
| `#pragma warning disable` | 0 | 3 (`EntityBuilderExtensions.cs:356/373/389`, pre-existing CS8619) | 3/3 (all pre-existing, pair `restore`) | 0 |
| `[SuppressMessage]` (with/without Justification) | 0 | 0 | – | 0 |
| `NoWarn` | 0 | 0 | – | 0 |
| `!` null-forgiving (`\w+!.`) | 5 | 89 | 5/5 added mirror guarded existing patterns | 0 |
| `// TODO` / `HACK` / `FIXME` / `XXX` | 0 | 0 | – | 0 |
| Empty `catch { }` | 0 | 0 | – | 0 |
| Broad `catch (Exception)` added | 0 | 0 | – | 0 |
| `(dynamic)` added | 5 (tests only) | 27 | 0/5 (test-only convenience) | 0 |

- Suppressions introduced by T211: **0** (ratio of new suppressions to new findings: `0/10` — no finding is silenced).
- Justified null-forgiving: **5/5**; unjustified/smuggled: **0/5**.
- Slopwatch-style smear score (added): wrong-provider docstrings 3, duplicate test files 4, `dynamic` 5 → **12 raw slop signals, 0 of them masking a build/test failure**.
- No `#pragma warning disable` without `restore`, no blanket suppression, no empty catch, no `throw ex;` in the added code.

## 3. Public-API surface check

- **No new public type or member, no public rename, no new `ISqlDialect` member.** Confirmed by diff scan:
  - New `QueryCommand` members `PerParentPaging`, `PerParentPartition`, `PerParentPartitionColumns`, `PerParentPagingEmbedded` — all `internal` (`src/nextorm.core/Query/QueryCommand.cs` added block).
  - `JoinExpression.EagerPerParentOrdering` — `internal` (`src/nextorm.core/Expressions/JoinExpression.cs`).
  - `EntityBuilder<T>.UnsupportedPerParentPagingChildShapes()` / `ToParentCommand(bool)` — `internal` (`src/nextorm.core/Builders/EntityBuilder.cs`).
  - `IEagerLoadSpec<TEntity>.ToJoinIntoSpec` signature gained a `QueryFilterScope` parameter — the interface is `internal` (`EntityBuilderEagerLoading.cs:14`).
  - `EntityBuilderEagerLoading.ValidateLoadWithForJoinInto<T>` is `public` but its containing class `EntityBuilderEagerLoading` is `internal static class` (`EntityBuilderEagerLoading.cs:486`) → not part of the shipped surface.
  - `ITestProvider.SupportsTupleIn` is in `tests/`, not shipped.
  - `git diff … src/ | rg ISqlDialect` → only a pre-existing XML `<see cref>`; no member added/changed.
- Tooling note: repo has **no** `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`, no `EnablePackageValidation`, no `ApiCompatSuppressionFile`, no `Microsoft.CodeAnalysis.PublicApiAnalyzers` (searched `*.props`/`*.csproj`). API-freeze enforcement is therefore absent (the known P2 `EL2` tracking item), so this check is code-reading only, matching `dotnet-api-surface-validation`'s "no analyzer present" caveat.
- `docs/**` + `docs/ru/**` public pages were updated in the commit (`docs/advanced/eager-loading-per-parent-paging.md` + RU, `docs/advanced/eager-loading.md`, `toc.yml`, `docs/guide/02-joins.md`, `docs/guide/29-implicit-navigation.md`) — public-doc delta exists, but no public symbol delta.

## 4. Design registers check

- `docs/specs/design/code-smells-review.md` and `docs/specs/design/API-NAMING-REVIEW.md` were **not touched** by `e98ae60e` (neither appears in `git diff --name-status b527be6f..e98ae60e`).
- `rg "T211|D211"` in both files → **0 matches**. The only nearby hit in `API-NAMING-REVIEW.md:73` is the prior **#209** eager-strategy entry, not T211.
- Therefore: **T211 added no register entries**, there are no T211 `file:line` claims to validate, and no self-certification is present. Nothing to check in §4 beyond this negative result.

## 5. Tests — coverage of changed paths

- Production paths covered by new tests: SQL lowering (`MakePerParentPaging*`) via `Trait("Feature","D211")` in 11 test files — 4 provider SQL-gen projects (`postgres`/`mysql`/`sqlserver`/`clickhouse` tests), core in-memory rejection (`tests/nextorm.core.tests`), sqlite SQL-generation, and integration (`CommonTestSuite`, `ClickHouseEagerPerParentPagingTests`, `MariaDbEagerPagingTests`).
- **Coverage gaps (tie to findings):**
  - C-1: split + unsupported child shape (`Distinct`/`GroupBy` + paging) has **no** test; unsupported-shape tests exist only for `SingleQuery` (`tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs:722-724` `SingleQuery_PerParentTopN_UnsupportedChildShape_IsRejectedWithZeroSql`).
  - W-2: multiple paged navigations tested only with `Limit(1)` per navigation, so multi-row ordering is unverified.
  - W-4: in-memory `JoinInto` + paged `LoadWith` fail-closed is unverified.
- Existing coverage for the changed plan/Clone/EqualityComparer paths is exercised indirectly by the new per-parent SQL-gen and cache tests (`Split_PerParentTopN_…`, `PerParentTopN_PopulatesThePlanCacheAndLaterQueriesStillCache`, `SingleQuery_…`) — see `tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs`.
