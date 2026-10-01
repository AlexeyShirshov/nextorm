# PDCA #125 / round 1 — EF Core 10 named + anonymous query-filter bridge

- issue: #125 "Глобальные фильтры: мост keyed-фильтров EF Core 10" (milestone `1.0.9-b`)
- branch: `1.0.9-b` (single-group collection; work in place, no worktree/branch/commit/push)
- round: 1 of N. This file is the frozen contract + D0–D6 plan + evidence ledger.
- source of truth for the EF surface: `/tmp/opencode/125/probe/evidence.txt` (recon probe, EF Core 10.0.12).

## 1. Frozen contract (implement exactly)

### 1.1 Owner binding
- `EfCoreFilterBinding` (internal) is backed by `ConditionalWeakTable<IDataContext, DbContext>`.
- A **successfully created** nextorm context (i.e. after `builder.CreateDataContext()` returns) is
  bound to its exact owning EF `DbContext` via `EfCoreFilterBinding.Bind(context, dbContext)`.
  Binding the connection is not enough: two EF contexts can share a connection.
- `EfCoreFilterBinding.GetOwner(IDataContext)` is a static method referenced from the translated
  filter expression; it must be callable from a core-prepared plan (it lives in the EF assembly and
  is only ever *invoked*, never resolved by core).

### 1.2 Filter enumeration
- Enumerate **root `GetDeclaredQueryFilters()`** (`IQueryFilter { Key, IsAnonymous, Expression }`).
- Never use the obsolete singular `GetQueryFilter()`.
- For a mapped derived entity, inherit the root filters and adapt the entity parameter to the root
  type (the frozen contract keeps inheritance registration unsupported by the mapper; the inherited
  path is defined here so an all-or-none stage can validate it deterministically).
- Anonymous EF filter (`IQueryFilter.IsAnonymous == true`, `Key == null`) maps to
  `QueryFilters.AnonymousKey` (`""`). Named EF filter maps its key to the nextorm filter name.

### 1.3 Expression rewrite
- Rewrite **every `Constant(DbContext)` leaf** in the EF lambda to
  `(OriginalContextType)EfCoreFilterBinding.GetOwner(<contextParameter>)`, preserving direct and
  nested member chains (`this.X`, `this.X.Y`).
- The resulting expression is a **two-parameter** `(entity, IDataContext)` lambda,
  `Expression<Func<TEntity, IDataContext, bool>>`, registered through
  `EntityMetadataBuilder.HasQueryFilter<TEntity>(string key, Expression<Func<TEntity,IDataContext,bool>>)`
  semantics (`EntityMetadataBuilder.cs:765`): `IQueryFilterMetadata.Lambda` populated, `Func` absent.
  The EF mapper materialises this itself (it implements `IQueryFilterMetadata`, no core internals leak).

### 1.4 Live context lookup (D3)
- At `BuildFilterBody` (`QueryCommand.QueryPreparer.cs:1089-1108`) the filter's `IDataContext`
  parameter becomes `QueryFilterContext.Context` (`Query/QueryFilterContext.cs:10`).
- **New binding:** context-rooted member parameterisation is extended to accept the
  owner-getter/member subtree. The value accessor is compiled once per prepared plan (process-wide
  key that **excludes the host/owner identity and values**) and invoked during each execution's
  parameter binding with the **executing** nextorm context.
- The EF owner / context-derived value must **not** enter process-wide metadata or the plan key.
- A member subtree rooted at `QueryFilterContext.Context` is recognised structurally in core; core
  never names `EfCoreFilterBinding`.

### 1.5 Capture policy
- **Closure-local captures are REJECTED** (a `MemberExpression` over a compiler-generated
  `<>c__DisplayClass*` constant): the value is settled at model-build and cannot reflect the live
  context. Not snapshotted.
- **Static-member captures are REJECTED** (a `MemberExpression` with `Expression == null`).
- **Literal scalar constants remain supported** (e.g. `d => d.Value >= 100`), folded to a parameter.

### 1.6 Fail fast at bridge creation
`Register`/translation validates structurally (no live getter execution) and throws before any
metadata is published for:
- unsupported captures/nodes (closure local, static member, unknown constant shapes);
- `EF.Property` / `EF.Functions` (`EF.*` method calls or member accesses);
- navigation / subquery filters (`Queryable`/`Enumerable` calls, nested query roots);
- invalid lambda signatures (not exactly one entity parameter, or a non-bool body);
- invalid named keys (null/empty/whitespace is anonymous, not named; duplicate key on one entity);
- incompatible context types (a `Constant(DbContext)` whose runtime type is not assignable to the
  declared context type of its member access);
- unsupported provider/mapping and conflicting metadata.

A null **nested owner value** (e.g. `CurrentTenant` is null) may fail at execution; it must never
silently remove the filter.

### 1.7 Staging and publication
- Stage the **entire model + normalised filters + registration fingerprints** first.
- Recheck conflicts across the staged batch; publish **all-or-none** using the existing registration
  synchronisation (`DataContextCache`).
- Attach the owner only **after** successful publication.
- On any failure, prior metadata is preserved (no partial publish).

### 1.8 Collisions
- Two independent **named** registrations on the same entity/key collide (even identical predicates).
- Named EF vs core named follows the same rule.
- Anonymous–anonymous is **additive** (including core anonymous): both predicates are AND-ed.
- Same named key on **different** entities is allowed.
- Repeating an identical complete bridge registration is **idempotent** (provenance + normalised
  mapping/filter fingerprints, excluding owner identity/values). Changed mappings/filters fail.

### 1.9 Ignore forwarding (D4)
Parameterless `IgnoreQueryFilters()` forwards to `AllFilters`; named `IgnoreQueryFilters` forwards to a `FromKeys` snapshot. Both forms are accepted. `EF.Property`/`EF.Functions` operator guards remain unchanged.

### 1.10 Attribute-to-`[OCP]` guards
- Leave the operator rejection guards (`EF.Property`, `EF.Functions`) intact.

### 1.11 Defensive-guard qualification
Invalid lambda signatures and incompatible context types are defensive translator guards, not promised public-EF reproduction cases. EF may overwrite repeated named-key registrations before import; rejection applies to duplicates observable in imported/normalized input, not overwritten registration history.

### 1.12 Publication atomicity and owner-failure semantics
- Import is failure-atomic: failed publication preserves prior metadata. Concurrent-reader snapshot atomicity is not guaranteed.
- A missing bridge owner fails closed: `EfCoreFilterBinding.GetOwner` throws a documented `InvalidOperationException` when the executing nextorm context was not created through the bridge, instead of leaving a null owner.
- A null **intermediate** owner member (for example `CurrentTenant` is null) is rethrown as the same documented `InvalidOperationException`, not a raw `NullReferenceException`; a null **leaf** value (owner present) binds null and keeps the predicate.

## 2. D0–D6 list

| id | deliverable | files | status |
|----|-------------|-------|--------|
| D0 | frozen contract, D-plan, baselines to `/tmp/opencode/125/` | this file | done (this round) |
| D1 | staged EF filter translation, collision/provenance fingerprints, sealed translator/binding types, remove blanket rejection | `src/nextorm.entityframeworkcore/NextOrmModelMapper.cs` + new `EfCoreFilterBinding.cs`, `EfQueryFilterTranslator.cs` | done (this round) |
| D2 | attach owners; all-or-none metadata publication at the registration/cache seam | `EntityFrameworkCoreExtensions.cs`, `NextOrmDbContextExtensions.cs`, `DataContextCache.cs` | done (this round) |
| D3 | live context-rooted parameter accessors | `QueryCommand.QueryPreparer.cs`, `Visitors/MemberTranslator.cs`, `Visitors/BaseExpressionVisitor.cs` | done (this round) |
| D4 | `IgnoreQueryFilters()` / `IgnoreQueryFilters(keys)` forwarding | `NextOrmQueryableExtensions.cs` | done (this round) |
| D5 | replace rejection tests with bridge tests (named/anonymous/live-tenant/direct `From<T>`) | `tests/nextorm.entityframeworkcore.tests/*` | next round |
| D6 | docs EN+RU (`docs/advanced/integration-efcore.md`, `docs/advanced/query-filters.md` + `docs/ru/**`) | docs | next round |

## 3. Variant matrix (must end green or fail fast)

| # | form | key | owner read | expected |
|---|------|-----|-----------|----------|
| V1 | named EF, direct `this.TenantId` | `tenant` | live DbContext | filter applied per live context |
| V2 | named EF, nested `this.CurrentTenant.Threshold` | `nested` | live DbContext | filter applied per live context |
| V3 | anonymous EF (`Key=null`) | `""` | live DbContext | additive with core anonymous |
| V4 | literal scalar constant (`d.Value >= 100`) | any | none | folded parameter, supported |
| V5 | closure-local capture | any | none | fail fast at bridge creation |
| V6 | static-member capture | any | none | fail fast at bridge creation |
| V7 | `EF.Property` / `EF.Functions` | any | n/a | fail fast |
| V8 | navigation/subquery filter | any | n/a | fail fast |
| V9 | inherited root filter on derived entity | inherited | live DbContext | parameter adapted to root type |
| V10 | named EF collides with core named | same | n/a | error |
| V11 | two independent named EF registrations, same entity/key | same | n/a | error (even identical) |
| V12 | anonymous EF + core anonymous | `""` | mixed | additive AND |
| V13 | repeat identical complete bridge registration | any | n/a | idempotent |
| V14 | changed mapping/filter on repeat | any | n/a | error |
| V15 | two live EF contexts, different tenants, same plan shape | any | live | no tenant leak, plan shared |

## 4. Test plan

- **EF bridge tests** (`tests/nextorm.entityframeworkcore.tests`):
  - named + anonymous translation registered as `IQueryFilterMetadata` with `Lambda` set, `Func` null;
  - live owner: two EF contexts, different tenants, same plan key, each returns its own rows;
  - direct `From<T>()` on `CreateNextOrmContext`/`GetNextOrmContext` also filtered (not only `ToNextOrm`);
  - fail-fast: closure-local, static member, `EF.Property`, navigation/subquery, invalid signature;
  - collision: EF named vs core named; named vs named; anonymous additive;
  - idempotent repeat; changed mapping/filter fails;
  - owner not in process-wide metadata/plan key.
- **Core tests** (`tests/nextorm.core.tests`, `~QueryFilter`): context-rooted accessor invoked per
  execution; accessor key excludes host; null nested value does not drop the filter.
- Placeholder connections only; SQL generation needs no database.

## 5. Docs plan
- EN: `docs/advanced/integration-efcore.md` (bridge section), `docs/advanced/query-filters.md`
  ("Not yet" -> bridge supported).
- RU mirrors under `docs/ru/**`; renumber/link hygiene per `AGENTS.md`.

## 6. Perf plan
- Seven-case acceptance: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
  (must report 7 cases / 0 failures; track `Cached_ToList / Prepared_ToList` and alloc ratio).
- Baseline captured to `/tmp/opencode/125/perf-baseline.log`; artefacts restored.
- D3 touches `MemberTranslator` on the param-extraction path; the cached path must stay within the
  20 % investigation band.

## 7. Priority matrix

| priority | item | rationale |
|----------|------|-----------|
| P0 | live-tenant correctness (V1/V2/V15) | core acceptance criterion; cached-plan leak is a correctness bug |
| P0 | fail-fast at creation (V5–V8) | requirement 3; no late failure |
| P0 | all-or-none publication | no partial/corrupt metadata |
| P1 | collision semantics (V10–V12) | requirement 4 |
| P1 | idempotency (V13/V14) | repeated `CreateNextOrmContext` is normal |
| P2 | inherited root filter (V9) | inheritance still rejected by mapper for mapping |
| P2 | perf ratio | plan-cache correctness first |
| D4 | `IgnoreQueryFilters` forwarding | done (this round) |

## 8. Baselines (captured 2026-10-01)

| baseline | command | result | log |
|----------|---------|--------|-----|
| Debug build | `dotnet build nextorm.slnx -c Debug` | 0 Warning / 0 Error | `/tmp/opencode/125/build-debug-baseline.log` |
| Release build | `dotnet build nextorm.slnx -c Release` | 0 Warning / 0 Error | `/tmp/opencode/125/build-release-baseline.log` |
| EF bridge tests | `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug` | 60 total / 0 failed / 0 skipped | `/tmp/opencode/125/ef-tests-baseline.log` |
| Perf acceptance | `--anyCategories=acceptance` | 7 cases / 0 failures; wall 47.56 s; `Cached_ToList / Prepared_ToList` = 2.00 (time), 7.90 (alloc); `Cached_PlanOnly_Param` = 0.60 | `/tmp/opencode/125/perf-baseline.log` |
| EF recon probe | `/tmp/opencode/125/probe` | shapes captured | `/tmp/opencode/125/probe/evidence.txt`, `run.log` |

## 9. Done / Verified / Incomplete

- Done:
  - **D1 (this round).** EF Core 10 query-filter import in `nextorm.entityframeworkcore`:
    - new `EfCoreFilterBinding.cs` — `ConditionalWeakTable<IDataContext, DbContext>` + `GetOwner` (owner binding wired in D2);
    - new `EfQueryFilterTranslator.cs` — `IQueryFilter` -> two-parameter `(entity, IDataContext)` lambda, every `Constant(DbContext)` leaf rewritten to `(ctxType)GetOwner(context)`, named -> key, anonymous -> `QueryFilters.AnonymousKey`, root filters adapted to mapped derived CLR types, fail-fast for closure/static captures, `EF.Property`/`EF.Functions`, navigation/subquery, invalid signature/key, incompatible context;
    - `NextOrmModelMapper.Register` staged all-or-none publication under a lock, collision detection (core named vs EF named refused, anonymous merged), idempotency via an owner-free fingerprint = mapping + provenance (owning context type names) + sorted filter bodies; changed shape refused; blanket filter rejection removed.
  - Tests: `EfQueryFilterImportTests` (10 new, all green) — named+anonymous import (`GetOwner` present), literal scalar, closure reject, static reject, `EF.Property` reject, `EF.Functions` reject, navigation reject, idempotent re-registration, changed-filter refusal, different-context-type provenance refusal.
  - **D2 (this round).** Owner binding: `EntityFrameworkCoreExtensions.CreateContext` calls
    `EfCoreFilterBinding.Bind(context, dbContext)` immediately after `builder.CreateDataContext()`
    (`EntityFrameworkCoreExtensions.cs:84`), so every bridge-created context is bound to its exact EF
    owner. `NextOrmDbContextExtensions.GetNextOrmContext`, `ToNextOrm` and `AddNextOrmFromDbContext`
    all funnel through `CreateContext`, so no extra binding site is needed; no core API change.
  - **D3 (this round).** Live context-rooted accessor in core:
    - `Query/QueryFilterContext.cs` — new `QueryFilterContextAccessor.TryTranslate` detects the
      owner-getter subtree (a static single-argument call whose argument is the
      `QueryFilterContext.Context` access), swaps the context access for an `IDataContext` parameter,
      compiles the host-free accessor, and invokes it with the **live** host context on every render
      (placeholder emitted on the SQL path, value re-read on the `ExtractParams` path).
    - `Visitors/MemberTranslator.cs:411` calls `QueryFilterContextAccessor.TryTranslate` in the final
      `else` branch, before the legacy closure-constant path, so native `IDataContext` filters
      (no owner-getter) keep their existing behavior.
    - `DataContext/DataContextCache.cs` — internal `QueryFilterContextAccessors` cache (keyed by the
      parameterized, host-free accessor lambda), cleared in `DataContextCache.Clear()`; the EF owner and
      the context-derived value never enter `ExpressionsCache`, the metadata or the plan key.
  - Tests: `EfQueryFilterLiveBindingTests` (2 new, all green) — SQLite bridge end-to-end: two EF
    contexts with different tenants return their own rows through a direct `From<T>()`; the warmed
    (cached) second execution re-reads the changed owner value; both tenants produce identical SQL
    plan text (tenant not in the plan); the translated metadata contains `GetOwner(context)`; and the
    internal `QueryFilterContextAccessors` cache is populated (proves the D3 path, not the legacy
    closure cache, resolved the read).
  - **D4 (this round).** `IgnoreQueryFilters` forwarding in `NextOrmQueryableExtensions.cs`:
    - `ValidateOperators` accepts both overloads (`IgnoreQueryFilters` at the
      `EntityFrameworkQueryableExtensions` switch); `Apply` maps the parameterless form to
      `builder.IgnoreFilters()` (`QueryFilterScope.AllFilters`) and the keyed form to
      `builder.IgnoreFilters(NormalizeFilterKeys(...))` (`QueryFilterScope.FromKeys`, anonymous
      preserved). No type mask is added.
    - `EvaluateFilterKeys` snapshots the key collection immutably (constant or compiled), and
      `NormalizeFilterKeys` drops null/empty/whitespace entries so they can never select the anonymous
      key `""`; null/empty collection is a no-op, unknown keys match nothing, duplicate keys collapse,
      chained named calls union, and parameterless dominates regardless of order.
    - The scope is query-local: it is applied to the fresh builder returned by `ToNextOrm` only, flows
      into the command (`FilterScope` -> condition -> plan key) and into referenced Any/Count/All
      subqueries through the referenced command; nothing is written to shared commands, bridge state or
      global metadata.
  - Tests: `EfIgnoreQueryFiltersTests` (13 new, all green) — parameterless disables named + anonymous;
    named disables only the listed key and preserves anonymous; unknown key matches nothing; empty list
    no-op; empty-string key never selects anonymous; duplicates accepted; chained named union;
    parameterless dominates either order; converted query stays query-local (neighbor direct `From<T>()`
    filtered); scope affects the rendered plan; scope flows into the shared `Any` subquery without
    leaking to the next call.
- Verified:
  - `dotnet build nextorm.slnx -c Debug` = 0 Warning / 0 Error; `-c Release` = 0 Warning / 0 Error.
  - `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug` = 85 total / 1 failed / 84 passed / 0 skipped.
    - The single failure is the obsolete rejection test
      `NextOrmModelMapperValidationTests.Register_ShouldThrow_WhenEntityDeclaresQueryFilter`
      (`NextOrmModelMapperValidationTests.cs:16-29`), which asserts the blanket rejection removed by D1;
      D5 replaces it.
    - The obsolete `ToNextOrmTests.IgnoreQueryFilters_ShouldThrowNotSupported` (`:342-352`) is replaced
      by `ToNextOrmTests.IgnoreQueryFilters_ShouldBeAccepted` (minimal positive coverage); the full
      matrix lives in `EfIgnoreQueryFiltersTests`.
  - `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~QueryFilter` = 93 / 93 passed (native filter paths #123/#124 unchanged).
  - `dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~QueryFilter` = 138 total / 0 failed / 34 passed / 104 skipped (SQLite ran; container providers skipped — no `DOCKER_HOST`).
  - CRLF normalized on all touched files; `BenchmarkDotNet.Artifacts/` restored after the D0 baseline.
- Incomplete:
  - D5 (replace the obsolete `NextOrmModelMapperValidationTests` rejection test with bridge coverage;
    the D1–D4 bridge tests already added), D6 (docs EN+RU).

## 10. Round-1 close-out — harness fix + final evidence (2026-10-01)

### 10.1 Harness fix (test-only)

`tests/nextorm.integration.tests/EfCoreQueryFilterBridgeTests.cs` server cases failed with
`ef_qf_named` / `ef_qf_anon` missing on the reused containers: `Database.EnsureCreated()` is a no-op
once the shared database already has any table. Replaced it with an explicit, deterministic
`RecreateFilterSchema(db, provider)` that drops and recreates both tables per provider (PostgreSQL
`identity`, SQL Server `identity(1,1)`, MySQL `auto_increment`, SQLite `integer primary key
autoincrement`), following the existing `EfCoreServerSharedTransactionTests.RecreateSchema` pattern.
All five assert helpers now take the provider; the test is idempotent on a dirty shared DB. No
production code changed.

### 10.2 Final evidence (all with `DOCKER_HOST=...podman-user.sock`)

| area | command / scope | result | log |
|------|-----------------|--------|-----|
| bridge theory | `--filter FullyQualifiedName~EfCoreQueryFilterBridgeTests` | 20 total / 0 failed / 0 skipped — SQLite + PostgreSQL + SQL Server + MySQL, every adapter executed | `/tmp/opencode/125/final/bridge-final.txt` |
| MySQL temp-table | `--filter FullyQualifiedName~OracleDriver_TempTableBatch` | 1 total / 0 failed / 0 skipped | `/tmp/opencode/125/final/mysql-temp-final.txt` |
| targeted filters | `--filter FullyQualifiedName~QueryFilter` | 158 total / 0 failed / 0 skipped | `/tmp/opencode/125/final/qf-final.txt` |
| full integration | `dotnet test tests/nextorm.integration.tests -c Debug --no-build` | 2834 total / 0 failed / 2647 passed / 187 skipped (all capability-only; every container provider executed) | `/tmp/opencode/125/final/integration-final.txt` |
| coverage | existing cobertura report | line 88.0 %, branch 78.8 % (thresholds 85/75 PASS; delta +0.1 / +0.8) | `/tmp/opencode/125/final/coverage.txt` |
| acceptance perf | `--anyCategories=acceptance` | 7 benchmarks / 0 failures; wall 43.83 s; `Cached_ToList / Prepared_ToList` ratio 2.07 (time), 7.90 (alloc); `Cached_PlanOnly_Param` 0.61 | `/tmp/opencode/125/final/acceptance.txt` |
| bridge benchmark | `BridgeFilterBenchmark` (`--anyCategories=bridge-filter`) | 6 benchmarks; cold prep 0.13–0.21 ms / ~14 KB, warm 7.9–10.6 us/op / ~7.26 KB/op; accessor cache added during warm = 0; owner-value change keeps identical SQL and rebinds rows (1,3)->(2) | `/tmp/opencode/125/final/bridge-bench.txt` |
| mutation (manual) | seam -> killing-test map | survivors are fail-fast/unreachable guards only; Stryker unusable with the MTP runner | `/tmp/opencode/125/final/mutation.txt` |
| hygiene | status/diff/CRLF/suppression/API | all changed text files CRLF; no added `NoWarn`/`#pragma`/`SuppressMessage`; no new public surface; artifacts clean | `/tmp/opencode/125/final/hygiene.txt` |

Builds re-confirmed after the CRLF-normalization of `BridgeFilterBenchmark.cs`:
`dotnet build nextorm.slnx -c Debug` = 0 Warning / 0 Error;
`dotnet build nextorm.slnx -c Release` = 0 Warning / 0 Error.

EF unit project 96/96 passed, core `~QueryFilter` 93/93 passed
(`/tmp/opencode/125/final/ef-unit-final.txt`, `core-qf-final.txt`).
All provider skips in the full run are capability-only (ClickHouse 0, Postgres 25, SqlServer 41,
Sqlite 41, MySql 77); no container-unavailable skip occurred.

## 11. Round-2 (iteration 2) — P1 mapper/publication fixes (2026-10-01)

Files changed: `src/nextorm.entityframeworkcore/NextOrmModelMapper.cs`,
`src/nextorm.core/DataContext/DataContextCache.cs`,
`src/nextorm.core/DataContext/DataContextExtensions.cs`,
`src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs`,
`src/nextorm.core/nextorm.core.csproj`,
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterPublicationTests.cs` (new).

### 11.1 D2 / #1 — merge preserves the core mapping

`EfEntityMetadata` now carries `IsTableNameAuto`, `DynamicColumnsStore` and `Relationships`.
`MergeWithExisting` always returns an EF-owned overlay whose table, properties, relationships,
dynamic-columns store and auto-name flag come from the pre-existing core mapping and whose `Filters`
are the merged list (EF imports + non-colliding core filters) and `EfFingerprint` is the EF-only
fingerprint. Previously it returned the fresh staged `EfEntityMetadata` whenever no core filter had to
be carried, silently dropping the whole core mapping state. Regressions (red→green, see 11.5):
`ImportPreservesCoreRelationships`, `ImportPreservesDynamicColumns`, `ImportPreservesIsTableNameAuto`.

### 11.2 D1 — failure-atomic publication + coordinated writers

- `NextOrmModelMapper.Register` publishes one entity at a time under
  `DataContextCache.MetadataRegistrationGate`, re-reading the current entry, recomputing the merge and
  revalidating conflicts immediately before each write; a failure rolls every already-written entry
  back (restore prior / remove newly added), so the batch is all-or-none. Owner binding still happens
  only after a successful `CreateContext`→`Register`.
- Class remarks now state the frozen wording: "Import is failure-atomic: failed publication preserves
  prior metadata. Concurrent-reader snapshot atomicity is not guaranteed."
- **Writer census** (`roslyn refs NextORM.Core.DataContextCache.Metadata`, 101 refs; every mutation in
  `src`): the sole writers are `NextOrmModelMapper.cs:153`, `DataContextExtensions.cs:230,244`,
  `JunctionLinkSourceFactory.cs:67,165`. All three files are now coordinated through the one gate:
  metadata is built outside the gate (no user configuration callback runs under it), then the write
  revalidates against the current entry under the gate. A single process-wide lock is used and no
  other lock is acquired while holding it, so no lock-ordering deadlock is possible.
- `nextorm.core.csproj` gains `InternalsVisibleTo("nextorm.entityframeworkcore")` — genuinely new
  (there was no prior friend for the EF project), needed for the internal gate. `MetadataRegistrationGate`
  is `internal`; no public type/member was added or changed.

### 11.3 D6 / #4 — property CLR type in the mapping identity

`IsSameMapping` and `ComputeFingerprint` now include `IPropertyMetadata.PropertyInfo.PropertyType`.
Verdict: **reachable and fixed** at the mapper boundary. It cannot be produced by two EF-only
registrations for one CLR entity type (a CLR property's type is fixed), but `DataContextCache.Metadata`
is public and accepts any external `IEntityMetadata`; a foreign mapping with the same property
name/column but a different CLR type used to be silently accepted. Regression:
`PropertyClrTypeChangeConflicts` (red before, green after).

### 11.4 D6 / #5 — merged entry reusing stale core provenance

Verdict: **checked-clean, not reproduced through the public API.** Once a merge publishes an
`EfEntityMetadata` with a non-empty `TableName`, no public writer replaces a non-empty entry except
the marker-gated auto-junction rebuild and the synthetic junction-link path; the idempotency
short-circuit (`existingEf.EfFingerprint == metadata.EfFingerprint`) therefore only ever reuses the
entry that already carries the merged core state, so core mapping/filters cannot change underneath it.
Guard `MergeRechecksCurrentCoreMapping` proves that when a core writer does replace the entry with a
new core mapping, the bridge re-reads the current entry and re-merges (both core filters present)
instead of reusing a stale merged entry. The guard is green in both the reverted and fixed states.

### 11.5 Gates and evidence (round 2)

| gate | command / scope | result | log |
|------|-----------------|--------|-----|
| Debug build | `dotnet build nextorm.slnx -c Debug` | 0 Warning / 0 Error | `/tmp/opencode/125/i2-build-debug.txt` |
| Release build | `dotnet build nextorm.slnx -c Release` | 0 Warning / 0 Error | `/tmp/opencode/125/i2-build-release.txt` |
| EF unit tests | `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug` | 103 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-mapper.txt` |
| core filters | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~QueryFilter` | 93 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-mapper.txt` |
| red (controlled revert of #1 carry-over + #4 CLR type) | `--filter FullyQualifiedName~EfQueryFilterPublicationTests` | 7 total / 4 failed / 3 passed — exactly `ImportPreservesCoreRelationships`, `ImportPreservesDynamicColumns`, `ImportPreservesIsTableNameAuto`, `PropertyClrTypeChangeConflicts` | `/tmp/opencode/125/i2-mapper-red.txt` |
| green after restore | same filter | 7 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-mapper.txt` |

Restore verified byte-identical: `cmp /tmp/opencode/125/mapper-fixed.cs
src/nextorm.entityframeworkcore/NextOrmModelMapper.cs` and
`cmp /tmp/opencode/125/src-fixed.diff /tmp/opencode/125/src-restored.diff` both OK.

New tests: `EfQueryFilterPublicationTests` (7). `FailedImportPublishesNoEntities` and
`ConcurrentCoreAndEfRegistrationDoesNotLoseMapping` are guards that are green in both states (the
pre-fix `Register` already validated the whole batch before publishing; the deterministic concurrency
test pins the coordinated revalidation); the four genuine red→green regressions are the three #1
tests and `PropertyClrTypeChangeConflicts`.

## 12. Round-3 (iteration-2 binding stream) — P1 binding fixes (2026-10-01)

Files changed: `src/nextorm.core/Query/QueryFilterContext.cs`,
`src/nextorm.entityframeworkcore/EfCoreFilterBinding.cs`,
`src/nextorm.entityframeworkcore/EfQueryFilterTranslator.cs`,
`tests/nextorm.core.tests/QueryFilterOwnerGetterTests.cs` (new),
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterHardeningTests.cs` (new).

### 12.1 D3 / #2 — missing bridge owner fails closed

`EfCoreFilterBinding.GetOwner` now returns a non-nullable `DbContext` and throws a documented
`InvalidOperationException` when the executing nextorm context is not bound (an imported filter lives in
process-wide metadata and also runs on a plain, unbridged context). The message names the missing EF
owner and the bridge requirement (`CreateNextOrmContext`/`GetNextOrmContext`/`ToNextOrm`/
`AddNextOrmFromDbContext`). Core's `QueryFilterContextAccessor.TryTranslate` wraps the accessor
invocation, so a null **intermediate** owner member (for example `CurrentTenant` is null) is rethrown as
the same documented `InvalidOperationException` instead of a raw `NullReferenceException`. A null
**leaf** value (owner present) still binds null and keeps the predicate. Regressions:
`NullOwnerIntermediateFailsClosed` and `ImportedFilterWithoutBridgeFailsClosed` (both threw raw
`NullReferenceException` before, red→green); `NullOwnerLeafRetainsPredicate` is a guard that is green in
both states (it exercises the already-correct null-leaf bind, not the missing owner).

### 12.2 D4 / #3 — exact owner-getter identity

`QueryFilterContextAccessor` gains `RegisterOwnerGetter(MethodInfo)` + `IsOwnerGetter(MethodCallExpression)`
(compared with `node.Method == _ownerGetterMethod`, i.e. assembly/type/method/signature identity).
`EfQueryFilterTranslator` registers `EfCoreFilterBinding.GetOwner` in its static constructor, so core
never has to name the bridge type. The old finder matched any static, non-generic, single-argument call
over `QueryFilterContext.Context`; a native filter that passes the context to a static helper is no
longer misclassified and keeps its own translation path. `EF.Property`/`EF.Functions` rejection is
untouched. Regression: `NativeStaticContextHelperIsNotOwnerGetter` (red before, green after).

### 12.3 D5 / #13 — stable, distinct parameter identities

`TryTranslate` now names the emitted parameter from `BuildSourceName(node)`, a host-free, value-free
walk of the structural member/method chain (for example `GetOwner_TenantId` vs
`GetOwner_Settings_TenantId`) instead of `ParameterNamePrefix + node.Member.Name`. Two distinct owner
chains that end in the same leaf therefore emit distinct placeholder names; an identical chain reuses
the same name and captured source. Regressions:
`SameLeafDifferentOwnerPathsBindDistinctValues` and `MixedOwnerLeavesAreNotCollapsed` (before: duplicate
`@TenantId` left a parameter unbound, `Must add values for the following parameters: $TenantId`, or the
two reads collapsed onto one value; red→green, including cached reuse after both owner values changed).

### 12.4 D6 / #9 — mixed-owner collapse verdict: **unreachable**

Checked-clean, not reproducible through the supported surface. `BuildFilterBody`
(`QueryCommand.QueryPreparer.cs:1102`) creates exactly **one** `QueryFilterContext` host per filter body
and replaces the filter's single `IDataContext` parameter with that host's `Context` access; the EF
translator rewrites every `Constant(DbContext)` leaf against the same `context` parameter. A member
subtree can therefore contain only one host constant, so `ContextAccessReplacer` can never observe a
second, different context access and the "last matched owner-getter" can never differ from the first.
`MixedOwnerLeavesAreNotCollapsed` pins the reachable mixed-owner-leaves case (two different owner chains
in one predicate over the one live host) and is red under the D5 duplicate-name bug / green after, but
it does not and cannot exhibit a host collapse. No dead guard was added.

### 12.5 Gates and evidence (round 3)

| gate | command / scope | result | log |
|------|-----------------|--------|-----|
| Debug build | `dotnet build nextorm.slnx -c Debug` | 0 Warning / 0 Error | `/tmp/opencode/125/i2-binding-build-debug.txt` |
| Release build | `dotnet build nextorm.slnx -c Release` | 0 Warning / 0 Error | `/tmp/opencode/125/i2-binding-build-release.txt` |
| EF unit tests | `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug` | 108 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-binding.txt` |
| core filters | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~QueryFilter` | 94 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-binding.txt` |
| red (controlled revert: loose owner-getter, old leaf-name parameter, `GetOwner` returns null, no NRE wrap) | core `~QueryFilterOwnerGetterTests` + EF `~EfQueryFilterHardeningTests` | core 1/1 failed; EF 5 total / 4 failed / 1 passed (`NullOwnerLeafRetainsPredicate` guard) | `/tmp/opencode/125/i2-binding-red.txt` |
| green after restore | same filters | core 1/1 passed; EF 5/5 passed | `/tmp/opencode/125/i2-binding.txt` |

Restore verified byte-identical: the three production files were copied to
`/tmp/opencode/125/i2-binding/*.fixed.cs`, the controlled-revert variants were run, and the originals
were restored with `cmp` OK before the green run. All touched/new files are CRLF; no new public
surface (the two helpers are `internal`); no commits/branches/push.

### 12.6 D7/D8 — inheritance/collision and ignore→normal scope restoration tests

Test-only stream: no production code changed. Files changed:
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterMatrixTests.cs` (D7, extended) and
`tests/nextorm.core.tests/QueryFilterTests.cs` (D8).

- **D7 (EF unit project):**
  - `InheritedUnsupportedFilterRejected` — a base type carrying a root `HasQueryFilter` whose derived
    type is mapped is rejected at registration with the documented inheritance `NotSupportedException`,
    and nothing is published (the mapper keeps inheritance registration unsupported).
  - `DuplicateEfNamedKeyImportsOnlyEfSurvivor` — EF Core 10 applies same-key `HasQueryFilter`
    declarations on one entity last-writer-wins before import; the importer sees and publishes only the
    surviving (last) declaration.
  - `Register_ShouldThrow_WhenEfNamedFilterChangesAcrossModels` — two independent EF named
    registrations, same entity/key, different bodies → `InvalidOperationException` (the observable
    EF-named vs EF-named collision / changed filter).
  - `Register_ShouldBeIdempotent_WhenNamedModelRegisteredTwice` — repeating the identical complete
    named registration is idempotent.
  - (`EfQueryFilterMatrixTests` already covered imported named vs core named → error and anonymous EF
    + core anonymous → additive AND; unchanged.)
- **D8 (core `~QueryFilter`):**
  - `IgnoredCountThenNormalRestoresScope` — after `IgnoreFilters(["soft"]).Count()`, a later normal
    `Count()` on the same context restores the full scope (warm/cached path), the context-shared
    `AnyCommand` is untouched, and its sticky `Cache` flag stays `true`.
  - `IgnoredAllThenNormalRestoresScope` — the same with the parameterless all-filters `IgnoreFilters()`.
- **All-terminal ambiguity (explicit).** The task named an "All" read terminal, but the core engine has
  none: `All()` exists only on `DeleteBuilder<TEntity>` (DML) and as `TemporalClause.All()`. In these
  tests "All" therefore means the parameterless all-filters *scope* (`QueryFilterScope.AllFilters`,
  `IgnoreFilters()`), not a terminal. No `All` API was invented and no src was touched. If a real read
  `All` terminal is added later, add a dedicated test beside `IgnoredAllThenNormalRestoresScope`.

Gates and evidence (D7/D8):

| gate | command / scope | result | log |
|------|-----------------|--------|-----|
| Debug build | `dotnet build nextorm.slnx -c Debug` | 0 Warning / 0 Error | `/tmp/opencode/125/i2-tests-build.log` |
| EF unit tests | `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug` | 112 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-tests.txt` |
| core filters | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~QueryFilter` | 96 total / 0 failed / 0 skipped | `/tmp/opencode/125/i2-tests.txt` |
| red (scope neutralized + inheritance guard removed) | core `~IgnoredCountThenNormalRestoresScope` and `~IgnoredAllThenNormalRestoresScope` = 2/2 failed; EF `~InheritedUnsupportedFilterRejected` = 1/1 failed | see evidence | `/tmp/opencode/125/i2-tests-red.txt` |

Restore verified byte-identical with `cmp` for `QueryFilterResolver.cs` and `NextOrmModelMapper.cs`;
`grep -rn "CONTROLLED-REVERT" src/` = NONE. All touched/new files are CRLF; no commits/branches/push.

### 12.7 Coverage close-out (iteration 2) — rollback arm + multi-filter fingerprint

Test-only stream: no production code changed. Files changed:
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterPublicationTests.cs`,
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterMatrixTests.cs`.

- `FailedImportRollsBackNewlyAddedEntity` — a two-entity batch where the first (brand-new) entity is
  written and the second collides with a core named filter registered outside the bridge: the batch
  fails, the pre-existing mapping is preserved (`BeSameAs`), and the already-published new entity is
  removed by the rollback. Coverage `NextOrmModelMapper.cs:135` `if (prior is null)` 0 hits -> 1 hit
  (condition 50 %); kills mutation survivor S1.
- `FingerprintIsOrderIndependentAcrossMultipleFilters` — one entity with two named filters
  (`zeta`, `alpha`) built with opposite declaration order: the owner-free fingerprint sorts the filter
  list, so the second registration is idempotent (`BeSameAs`, no throw). Coverage
  `NextOrmModelMapper.cs:259` (the sort comparator) 0 hits -> 1 hit (condition 50 %); the `byKey == 0`
  tie-break sub-arm stays unreached because EF Core 10 refuses named+anonymous on one entity and
  collapses same-key declarations, so imported filters always have distinct keys (justified partial).

Gates and evidence (coverage close-out):

| gate | command / scope | result | log |
|------|-----------------|--------|-----|
| Debug build | `dotnet build nextorm.slnx -c Debug` | 0 Warning / 0 Error | `/tmp/opencode/125/final2/ef3/build-debug.txt` |
| EF unit tests | `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug` | 114 total / 0 failed / 0 skipped (was 112) | `/tmp/opencode/125/final2/ef3/ef-tests.txt` |
| new tests | `--filter ...FailedImportRollsBackNewlyAddedEntity` + `...FingerprintIsOrderIndependentAcrossMultipleFilters` | 2 total / 0 failed / 0 skipped | `/tmp/opencode/125/final2/ef3/newtests.txt` |
| EF module coverage | `dotnet-coverage` + `reportgenerator -assemblyfilters:+nextorm.entityframeworkcore` (temp settings, whole-solution run) | line 81.1 % -> 82.1 % (737->746/908), branch 67.7 % -> 68.1 % (460->463/679) | `/tmp/opencode/125/final2/coverage-ef2.txt`, `/tmp/opencode/125/final2/cov2/report-ef/Summary.txt` |
| mutation map | manual seam -> killing-test map | S1 killed by `FailedImportRollsBackNewlyAddedEntity`; S2/S3/S4 justified survivors | `/tmp/opencode/125/final2/mutation.txt` (S1 row superseded) |

Configured scope (core+sqlite+postgres+sqlserver) unchanged at line 88.0 %, branch 78.8 % (both
thresholds 85/75 pass). All touched/new files CRLF; no commits/branches/push.

## 13. Round 4 — lifecycle fail-closed (2026-10-01)

Files changed: `src/nextorm.core/Query/QueryFilterContext.cs` (expectation registry + host-free
accessor key), `src/nextorm.core/DataContext/DataContextExtensions.cs`,
`src/nextorm.core/DataContext/Meta/QueryFilterResolver.cs` (re-check on resolution),
`src/nextorm.entityframeworkcore/EntityFrameworkCoreExtensions.cs` (bridge diagnostic + registration),
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterFailClosedTests.cs` (new),
`tests/nextorm.entityframeworkcore.tests/EfQueryFilterAccessorCacheTests.cs` (ownership regression).

### 13.1 Decision — lifecycle fail-closed

When the imported-filter metadata is dropped from the process-wide cache — by an explicit
`DataContextCache.Clear()` or by a sliding-expiration eviction — a bridge-bound context that still
expects that filter must **refuse to run** rather than silently execute unfiltered. The expectation is
recorded per context in `QueryFilterExpectations` (a `ConditionalWeakTable<IDataContext, ...>` that
lives outside `DataContextCache`, so `Clear()` cannot erase it) and re-checked on every metadata
resolution (`QueryFilterResolver.cs:43`, `DataContextExtensions.cs:293/322/329/346`); a miss throws the
bridge-supplied, documented `InvalidOperationException`. **Recovery** is to create/re-register a
context through the bridge (`CreateNextOrmContext` / `GetNextOrmContext` / `ToNextOrm` /
`AddNextOrmFromDbContext`), which re-publishes the metadata and re-records the expectation. The
bounded-concurrency semantics of §1.12 (import is failure-atomic; concurrent-reader snapshot atomicity
is not guaranteed) are retained unchanged.

### 13.2 Variant matrix (round 4)

| # | form | priority | coverage |
|---|------|----------|----------|
| V16 | `DataContextCache.Clear()` drops the imported metadata | P1 | guard + unit + live-provider tests |
| V17 | sliding-eviction miss on the imported metadata | P1 | guard + unit + live-provider tests |

Guard/unit live in `EfQueryFilterFailClosedTests` (`Clear_DoesNotClearExpectations`,
`MissingExpectedFilters_BothResolveOverloads_Throw`, `FailedBind_DoesNotRegisterExpectation`); the
live-provider harness is `tests/nextorm.integration.tests/EfCoreQueryFilterBridgeTests.cs` (SQLite plus
the container providers).

### 13.3 D4.1–D4.8

| id | deliverable | status |
|----|-------------|--------|
| D4.1 | lifecycle fail-closed decision + this status section | done |
| D4.2 | owner-free `QueryFilterExpectations` registry and `EnsureFiltersPresent` on metadata resolution; documented `InvalidOperationException` | done (green) |
| D4.3 | recovery contract documented (create/re-register a context); a failed bind publishes no expectation | done |
| D4.4 | accessor-cache ownership: canonical `string` key (not interned; D5), host-free `Func<IDataContext, object>` value, GC regression | done |
| D4.5 | V16 Clear (P1) — guard + unit + live-provider tests | done (guard/unit green; live-provider harness: integration bridge suite) |
| D4.6 | V17 sliding eviction (P1) — guard + unit + live-provider tests | done (guard/unit green; live-provider harness: integration bridge suite) |
| D4.7 | CRLF/hygiene + build/test ledger | done |
| D4.8 | bridge-retention observation for CHECK (root outside the accessor cache) | open — see 13.4 |

### 13.4 Observation for CHECK — the bridge-retention root is not the accessor cache

The accessor cache itself is host-free: key type `string`, value type `Func<IDataContext, object>`,
and the compiled delegate's closure `Constants` array is empty, so invoking it with a plain, unbridged
context does not make the cache root that context (`AccessorCache_DoesNotRetainLiveContext`, green).
A bridge context that rendered a query nevertheless stays alive after `DataContextCache.Clear()`; the
remaining root is `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs:24`
(`[ThreadStatic] private static Visitor? _tlsVisitor`), whose `Visitor._queryProvider` is the live
`QueryCommand`, hence its `IDataContext`; `Clear()` does not reset the thread-static visitor. Reported
for CHECK only — no speculative fix was applied.
