# ClickHouse: integrate native `JSON` into the core `[JsonColumn]` model — issue #198 (task D198)

- task: D198
- issue: #198 (follow-up to #128)
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: done
- cycle: N=1
- plan revision: r=3
- attempt: n=1/3
- contract: rv=3
- mode: autonomous
- git: no push/merge; task files only; coder updates task files, orchestrator owns collection row + commits

## Goal / PLAN (verbatim, r=1)

---BEGIN PLAN---
Goal: Integrate native ClickHouse `JSON` into the core `[JsonColumn]` model. Issue #198, follow-up to #128. Acceptance criteria R198-01..R198-08 (as below). Completion requires CHECK evidence.

R198-01: CH `SupportsJson=true`; `Auto`/`Native` resolve native, `Text` textual; PG unchanged; negative: `Native` on non-supporting dialect throws, CH `Text` must not bind native.
R198-02: CH `[JsonColumn]` reads/writes object-root POCO, `JsonObject`, `JsonDocument`, `JsonElement` via native `JSON`, honoring serializer options; negative: unsupported native roots fail clearly.
R198-03: bare CH `JsonObject`/`JsonDocument`/`JsonElement` round-trip native `JSON` for projection AND parameter; negative: SQL NULL not confused with `{}`, undefined elements not silently object.
R198-04: legacy `Object('json')` has measured, version-qualified compatibility; on supported config projection+parameter coverage; negative: failed/skipped probe cannot count as support; incompatibility returns to PLAN.
R198-05: shared JSON-column tests choose correct physical type per provider; CH never gets `jsonb` DDL; negative: `jsonb` DDL on CH or skipped CH fails.
R198-06: PG physical `json`/`jsonb`, JSON SQL-gen, D197 behavior stay green; other providers unchanged; negative: no CH reader/converter selected for PG or `SupportsJson=false`.
R198-07: EN+RU docs describe storage matrix, compatibility boundary, retained bare scalar top-level `JsonObject` limitation; negative: no stale "CH Auto textual"/"CH native [JsonColumn] unsupported".
R198-08: Debug solution build `0 Warning(s) 0 Error(s)`; tests/coverage/perf evidence per contract.

Design decision: add ONE additive dialect capability (planned name `NativeJsonProviderType`, default `typeof(JsonElement)` at `ISqlDialect`/`SqlDialectBase`, CH overrides `typeof(JsonObject)`); consult only when native storage selected; validate advertised native representation, no silent text fallback. Alternatives considered and rejected: keep JsonElement+CH-local adaptation (conceals mismatch, duplicate), generic transport abstraction (too many concepts).

Storage matrix (decision: CH `Auto` becomes native alongside SupportsJson=true — behavioral API change; existing CH String-column users must select `Text` explicitly; no silent migration):
- CH SupportsJson=true: Auto→JsonObject/JSON; Native→JsonObject/JSON; Text→string/String (no native forcing). PG SupportsJson=true: Auto→JsonElement/jsonb; Native→JsonElement/native JSON; Text→string/text. Other dialect false: Auto→string/text; Native→exception; Text→string/text.
- Bare on CH: JsonObject→JSON projection+parameter (test); JsonDocument→JSON object-root projection+parameter (test); JsonElement→JSON object-root project+parameter (test); JsonNode declared property→no node materializer (guard); string→ordinary string read/write (test), not advertised native.
- Variants: entity/DTO projection with bare JsonObject; bare scalar top-level `.Select(x=>x.JsonObjectProperty)` = retained documented limitation (guard, not fixed); empty/nested/escaping/nested-null; SQL NULL nullable; non-nullable element receiving SQL NULL; default/Undefined JsonElement; array/primitive/root-null native values (guard); `[JsonColumn] Text` non-object roots; serializer options + attribute/interface/fluent; converter conflict + plan equality/cache; PG D197 regression.

Compatibility commitment: baseline ClickHouse.Driver 1.4.0 / server 25.8.33.6 (25.8 line). `Object('json')` NOT assumed transparent alias — spike must identify actual server type/setting/driver transport; if incompatible, stop that branch and return observed incompatibility to PLAN. No unverified support claims for earlier drivers/servers.

DO tasks:
D198.1 Baseline, semantic audit and compatibility spike (baseline bench, roslyn consumer/impact audit of SupportsJson/JsonSqlTranslator, real-CH probe native+legacy).
D198.2 Storage capability + converter implementation (`ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect`, `JsonColumnConverter` add JsonObject native representation + dialect-aware resolution; preserve PG/text/options/null/equality/conflict validation).
D198.3 Reader + parameter paths (`SelectExpression.GetDataRecordMethod` exact JsonObject accessor; `ClickHouseDataContext.MapColumnExpression`/`CreateParam` account for resolved converter provider type + bare doc/element adaptation; object-root boundaries; preserve delegation).
D198.4 Repair shared test DDL contract (test-local `ProbeJsonColumnType(dialect, storage)`: CH native→JSON, PG native→jsonb, text→ProbeTextType; reject unhandled native dialect explicitly; do NOT add production SQL-type-name capability for tests).
D198.5 Unit + SQL-generation tests (complete matrices; assert resolved provider types, SQL shape, parameter metadata, null guards, options, delegation; retain PG accessor/node/cache invariants).
D198.6 Real integration + regression coverage (extend CH tests at `ClickHouseIntegrationTests.cs:1114-1185,1626-1634` + fixture `Providers/ClickHouseTestProvider.cs:180-209`; independent DB-content verification; run CH-specific, PG-specific, full integration suite with DOCKER_HOST; required providers execute).
D198.7 Documentation EN+RU + measurement + build closure (files: `docs/guide/14-json.md` + ru, `docs/guide/provider-specific/clickhouse.md` + ru, `docs/providers/clickhouse.md` + ru, `docs/providers/overview.md` + ru, `docs/advanced/limitations.md` + ru, `docs/infrastructure/04-value-converters.md` + ru; focused materialization BDN + 7-case acceptance bench before/after; coverage; final build; CRLF).
D198.8 ACT/registers after accepted CHECK (collection row D198→done is done by the orchestrator; coder updates task status file + any registers; commit only task files, message starts `#198`; never push).

Perf decision: MEASUREMENT REQUIRED. `MapColumnExpression:61-85` accessors run per row; `ConvertFromProvider:101` per value; `CreateParam:96-112` per parameter. Run 7-case acceptance bench before/after; add focused BDN category `D198JsonRead`. Thresholds: acceptance median degradation >5% or existing-object focused >10% requires confirmation run + explanation; unresolved regression returns to PLAN. Restore `BenchmarkDotNet.Artifacts` with `git checkout -- BenchmarkDotNet.Artifacts`.

Recon decision: SPIKE REQUIRED (legacy transport + native DOM behavior not established). Success = real CH creates intended types, reports physical type/version, executes object-root parameters, reads projections correctly, preserves lifetimes, demonstrates legacy under recorded settings.

Unit mode: one current tree, sequential units (overlapping files).

Evidence contract rv=3 (rows E198-01..E198-18): semantic audit; build `dotnet build nextorm.slnx -c Debug` 0/0; core unit `dotnet test tests/nextorm.core.tests -c Debug`; CH unit `tests/nextorm.clickhouse.tests`; provider regressions (postgres/sqlite/sqlserver/mysql/mariadb); CH integration `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~ClickHouse"` executed not skipped; PG integration filter `~Postgres`; full integration; docs EN+RU diff + `dotnet docfx docs/docfx.json`; coverage; acceptance + focused BDN; scope/provenance `git diff --name-only`/`git status --short`/`git diff --check`. CHECK re-gather budget: max 2 dispatches.
---END PLAN---

## PLAN r=2 deltas (replan from `C198-LEGACY-INCOMPATIBLE` + `SupportsJson` overload)

Applied deltas (r=1 → r=2, attempt reset to n=1/3):

- **Storage vs SQL surface split.** `SupportsJson` is re-scoped to native JSON **storage** only: it gates
  `[JsonColumn]` `Auto`/`Native` converter resolution (`JsonColumnConverter`). It no longer gates the
  PostgreSQL JSON functions/operators.
- **`SupportsPostgresJsonSql`** (additive): default interface member `false` at `ISqlDialect`,
  `SqlDialectBase` virtual `false`, `PostgresDialect` `true`, `ClickHouseDialect` inherits `false`. It
  gates the PostgreSQL `json`/`jsonb` surface in `JsonSqlTranslator.RequireJsonSupport` and **replaces**
  `SupportsJson` at that gate. Purpose: flipping CH `SupportsJson=true` no longer admits
  `PostgresFunctions.*` JSON constructs on ClickHouse.
- **`NativeJsonProviderType`** (additive): default `JsonElement` at `ISqlDialect`/`SqlDialectBase`,
  ClickHouse `typeof(JsonObject)`; consulted only when native storage is selected to pick the provider
  representation (validated; unsupported advertised type throws, no silent text fallback).
- **CH `Auto` → native** (`SupportsJson=true`): Auto/Native→`JsonObject`/native `JSON`; Text→string/String.
  Behavioral API change; existing CH `String`-column users must select `Text` explicitly, no silent
  migration.
- **Native object-roots only, with guards.** CH native `JSON` is object-rooted: array/primitive/string/
  JSON-null roots (and undefined `JsonElement`) throw a clear `NotSupportedException` instead of letting
  the server reject; `[JsonColumn] Text` keeps non-object roots.
- **SQL `NULL` ≠ `{}`.** Null reference for `JsonObject`/`JsonDocument`, `default` (`ValueKind.Undefined`)
  for the value-type `JsonElement`.
- **Eager DOM.** Values are eagerly materialized; no clone/detach and no reader/connection lifetime is
  retained by `JsonObject`.
- **Legacy `Object('json')` revised to measured-unsupported** on ClickHouse.Driver **1.4.0.0** / server
  **25.8.33.6**: reads materialize `System.Tuple<SByte,String>` (path→value), not `JsonObject`;
  `GetFieldValue<JsonObject>` throws `InvalidCastException`; DDL needs a connection-level
  `allow_experimental_object_type=1`. **Documented as a limitation; no bridge in #198**; **no global
  `allow_experimental_object_type`** is set. R198-04's legacy sub-branch is dropped.
- **New unit D198.9** (contract carry-forward + compatibility audit) and r=1→r=2 mappings
  **D198.2→D198.2R2**, **D198.5→D198.5R2**, **D198.6→D198.6R2**, **D198.7→D198.7R2**. D198.3/D198.4 keep
  their ids (shared DDL/test contract unchanged); D198.8 ACT unchanged.
- **R198-04 re-scoped**: "native only; legacy measured-incompatible and documented"; negative clause
  becomes "legacy is measured-unsupported and no support is claimed", not "compat coverage on supported
  config".

## PLAN r=3 deltas (replan from CHECK n=2: perf-contract equivalence + missing scalar regression + contract rebinding)

Applied deltas (r=2 → r=3, attempt reset to n=1/3):

- **Contract revision rebinding.** The binding contract is **`rv=3`**. **`rv=3` supersedes the previous
  obligation set (including entries mislabelled `rv=1`)**; no `rv=2` contract was ever published, so no
  such revision is claimed. Every `rv=1` label in this file is relabelled `rv=3`; stable evidence row
  IDs (`E198-01..E198-16`) and acceptance IDs (`R198-01..R198-08`) are preserved — **no obligation is
  dropped**.
- **Perf-contract equivalence (supersession of the focused-CH-BDN obligation).** The previous r=2
  obligation to add a *database-backed* focused `D198JsonRead` benchmark over ClickHouse is
  **superseded**. The binding evidence is now: acceptance **7/7 before/after** (BDN total 43.18 s → 44.65 s,
  no case >5%, allocations flat; `perf-acceptance-baseline.log`, `perf-acceptance-final.log`) + the
  in-process `D198JsonRead` converter/DOM benchmark **9/9 exit 0** (`perf-D198JsonRead.log`, comparison
  `perf-r2.md`) + **CH integration parameter normalization 205/205** (`ch-integration-r2.log`,
  re-confirmed `ch-integration-r3.log`). A **CH DB benchmark is not required**: the harness
  `benchmarks/nextorm.benchmark` has no ClickHouse project reference / `ClickHouse.Driver`, and adding it
  is disproportionate new infrastructure. This closes the open `Risks` item "CHECK to adjudicate".
- **New unit D198.11 — dedicated negative regression for the bare top-level scalar limitation.** A
  focused CH test performs exactly `.Select(x => x.Doc)` on a bare `JsonObject` column; the observed
  pre-guard behavior was a broken mapper (`System.ArgumentException: Incorrect number of arguments for
  constructor`, `RowMaterializerBuilder.cs:420`), so a narrow fail-fast correctness guard was added
  (`TypeFacts.IsBareDomScalar` + `QueryCommand.QueryPreparer.PrepareColumns`) that raises a clear
  `NotSupportedException` naming the bare top-level `JsonObject`/`JsonDocument`/`JsonElement`
  scalar-projection limitation. This is a **small correctness guard preventing silent corruption, not
  scope creep**: the working wrapped form, streaming, tuples and `[JsonColumn]`-attributed DOM
  properties are unaffected. New evidence row **E198-17**.
- **New unit D198.12 — contract rebinding** to `rv=3` (this section) with per-row `file:line`/artifact/exit
  binding. New evidence row **E198-18**.
- **Priority floor.** All `R198-*` acceptance criteria and their evidence rows are **P1 by construction**
  (nextorm overlay) — none may be downgraded, deferred or dropped without an explicit PLAN replan.
- D198.10 is **reopened at r=3** and is `blocked` on D198.11/D198.12 until they close (then it continues
  to CHECK); it is **not** marked done by this replan.

## Acceptance criteria (R198-01..R198-08)

See the verbatim PLAN above. R198-01..R198-08 are the acceptance contract; D198.1 targets the
perf/baseline/semantic-audit/spike preconditions shared by all of them. At r=2, R198-04 is the
native-only + measured-unsupported-legacy form described in §PLAN r=2 deltas.

## Current state

- **r=3, n=1/3, rv=3, cycle N=1.** D198.1 complete; **D198.9, D198.2R2, D198.3, D198.4, D198.5R2,
  D198.6R2 complete** (source edits + filtered unit tests green; CH integration executed not skipped,
  PG regression + full integration sweep green); **D198.7R2 complete** (docs EN+RU + docfx 0 errors,
  acceptance + focused BDN, coverage, final 0/0 build). **CHECK at n=1/3 failed** (open variant rows +
  candidate defects) and returned to DO; **D198.10** closed the candidate defects
  (`D198-NULL-ELEMENT`, `D198-LIFETIME`, `D198-PG-BARE-JSONOBJECT`, `D198-PROVIDERTYPE`) and the variant
  gaps, re-verified by the fresh n=2 runs below. **CHECK at n=2 triggered the r=3 replan** (perf-contract
  equivalence + missing bare top-level scalar regression + contract rebinding; see §PLAN r=3 deltas):
  **D198.11 done** (guard + negative regression, E198-17) and **D198.12 done** (`rv=3` rebinding, E198-18);
  **D198.10 reopened at r=3 and continues (not done)** → proceed to CHECK. D198.8 (ACT) pending accepted
  CHECK.
- The r=2 replan (`C198-LEGACY-INCOMPATIBLE` + `SupportsJson` overload) is applied in the tree: the
  storage/surface split and object-root guards described in §PLAN r=2 deltas are implemented.
- Native `JSON` is compatible; legacy `Object('json')` is documented as measured-unsupported (no bridge).
- Socket present and used: `/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` (verified).
- Base commit: `cd1a7c6cd8eac853c906bd9a8f598ec38dfd4bd9` (`artifacts/pdca/D198/baseline-head.txt`).
- Temporary spike test `ClickHouseNativeJsonSpikeTests.cs` was added only to run the probe and
  **deleted**.

## Durable state

- cycle `N=1` · plan revision **`r=3`** · attempt **`n=1/3`** · contract/evidence revision **`rv=3`** —
  carried in this file; a session/`task_id` reset never resets `r`, `n` or the defect history.
- DO ledger:

| unit | status | evidence |
|---|---|---|
| D198.1 | done | build 0W/0E `build-debug-baseline.log`; acceptance 7/7 exit 0 `perf-acceptance-baseline.log`; roslyn audit E198-01; CH spike `ch-spike-report.txt`; legacy incompatible ⇒ `C198-LEGACY-INCOMPATIBLE` (DO→PLAN) |
| D198.9 | done | contract carry-forward + compatibility audit; verified roslyn refs below; `artifacts/pdca/D198/evidence-r2-impl.json` |
| D198.2R2 | done | `ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect`/`PostgresDialect` + `JsonColumnConverter` storage/surface split; filtered `JsonColumn` tests `core-json-tests-r2.log` 20/20 exit 0 |
| D198.3 | done | `SelectExpression.GetDataRecordMethod` JsonObject accessor; `ClickHouseDataContext.MapColumnExpression`/`CreateParam` bare doc/element adaptation + param normalization; CH JSON tests `clickhouse-json-tests-r2.log` 32/32 exit 0 |
| D198.4 | done | `CommonTestSuite.JsonColumn.cs` test-local `ProbeJsonColumnType(dialect, storage)`; unknown native dialect throws |
| D198.5R2 | done | `JsonColumnTests.cs` (core, 20/20), `ClickHouseJsonNativeStorageTests.cs` + `ClickHouseJsonObjectMappingTests.cs` (CH, 32/32), `JsonColumnSqlGenerationTests.cs` (PG, 35/35) all exit 0 |
| D198.6R2 | done | real CH integration executed (not skipped): `--filter FullyQualifiedName~ClickHouse` 205/205 exit 0, 0 skipped; PG `~Postgres` 826 total/800 passed/26 skipped exit 0; full suite 3300 total/3103 passed/197 skipped/0 failed exit 0 (CH 205, PG 800, SQLite 668, SQL Server 676, MySQL 585 executed); build 0W/0E; `artifacts/pdca/D198/{ch-integration-r2.log,pg-integration-r2.log,full-integration-r2.log,evidence-r2-integration.json}` |
| D198.7R2 | done | docs EN+RU complete; docfx exit 0 (2 pre-existing warnings, `artifacts/pdca/D198/docfx.log`); acceptance 7/7 final, BDN 44.65 s, no case >5% degradation (`perf-acceptance-final.log`); focused `D198JsonRead` 9/9 fallback (`perf-D198JsonRead.log`, comparison `perf-r2.md`); coverage line 88.6% / branch 80% (`coverage-r2.log`); final build 0W/0E (`build-debug-final-r2.log`); `git diff --check` exit 0 |
| D198.10 | **continues — not done** | reopened at r=3; was `blocked` on D198.11/D198.12 until they closed (both now done), so it continues to CHECK — **not** marked done. Corrective DO from CHECK fail (r=2 n=1/3): `D198-NULL-ELEMENT` fixed (`ClickHouseDataContext.cs:87-98`, red→green `BareNullableJsonElementProjection_SqlNull_ShouldMaterializeNull`); `D198-LIFETIME` fixed (`ClickHouseDataContext.cs:139-147`, `CreateParam_ObjectRootDocument_ShouldNotRetainCallerDocumentLifetime`); `D198-PG-BARE-JSONOBJECT` gated (`RowMapperFactory.cs:118-135` via `dialect.NativeJsonProviderType == typeof(JsonObject)`, test `BareJsonObjectProjection_ShouldBeRejectedAsUnsupported`); `D198-PROVIDERTYPE` confirmed non-defect; fresh n=2 runs green — build 0W/0E `check2-build-final.log`; core 21/21 `check2-core.log`; CH unit 42/42 `check2-ch-unit.log`; PG unit 41/41 `check2-pg-unit.log`; CH integration 205/205 0 skipped `check2-ch-integration.log` |
| D198.11 | done | bare top-level scalar negative regression (r=3 n=1/3): observed pre-guard broken mapper (`ArgumentException: Incorrect number of arguments for constructor`, `RowMaterializerBuilder.cs:420`); narrow guard added `TypeFacts.cs:83-93` + `QueryCommand.QueryPreparer.cs:639-652`; new test `ClickHouseJsonObjectMappingTests.BareTopLevelJsonObjectScalarProjection_ShouldFailClosedWithNamedLimitation`; CH unit 43/43 `clickhouse-json-tests-r3.log`; CH integration 205/205 `ch-integration-r3.log`; E198-17 |
| D198.12 | done | contract rebinding to `rv=3` (r=3 n=1/3): header/current-state/durable-state/evidence-contract labels `rv=1`→`rv=3`, explicit supersession sentence, perf supersession recorded, priority floor + per-row bindings E198-17/E198-18; E198-18 |
| D198.8 | pending | ACT/registers after accepted CHECK (D198.10 → CHECK first) |

### Defect history (stable keys)

| defect key | state | observed r/n | fixes applied | evidence / note |
|---|---|---|---|---|
| — (empty) | — | — | 0 | no implementation defect observed |
| C198-LEGACY-INCOMPATIBLE | **resolved by r=2 replan** | r1 / n1 | 0 | legacy `Object('json')` read materializes `System.Tuple<SByte,String>`, not `JsonObject`; DDL needs connection-level `allow_experimental_object_type=1`. Dropped from scope; documented measured-unsupported. `artifacts/pdca/D198/ch-spike-report.txt:32-46` |
| D198-TESTCASE-DEFAULT-NAMING | **closed (test-only fix)** | r2 / n1 | 1 | `ObjectConverter_ShouldRoundTripThroughJsonObject` asserted camelCase `obj["name"]` while no serializer options are supplied (default = declared `Name`); NRE during the first filtered core run. Fixed the test assertion to `obj["Name"]`. Pre-fix `core-json-tests-r2.log` history: failed 1/20; post-fix 20/20. No implementation change. |
| D198-INT-BARE-ENTITY-CTOR | **closed (test-only fix)** | r2 / n1 | 1 | First CH integration run (205 total) failed 1: `NativeJsonColumn_NonObjectRoot_ShouldThrowObjectRootGuardBeforeExecution` called `ctx.From<IChJsonNativeBareEntity>().ToList()`, materializing the interface directly → `QueryPreparationException: Cannot get ctor from …IChJsonNativeBareEntity`. Fixed the verification query to `Select(x => x.Id)`. Post-fix `ch-integration-r2.log`: 205/205 exit 0, 0 skipped. No implementation change. |
| D198-NULL-ELEMENT | **fixed** | r2 / n1→n2 | 1 | CHECK n=1/3: a bare `JsonElement` SQL-NULL projection could surface `Undefined` instead of a true null. Fixed in `ClickHouseDataContext.cs:87-98` (nullable declared type branch); red→green test `BareNullableJsonElementProjection_SqlNull_ShouldMaterializeNull` (`ClickHouseJsonObjectMappingTests.cs:184`); fresh `check2-core.log` 21/21 + `check2-ch-unit.log` 42/42 exit 0. |
| D198-LIFETIME | **fixed** | r2 / n1→n2 | 1 | CHECK n=1/3: a `JsonObject` created from a caller's `JsonDocument` retained that document, so the parameter broke if the caller disposed it before execution. Fixed in `ClickHouseDataContext.cs:139-147` (reparse from raw text); test `CreateParam_ObjectRootDocument_ShouldNotRetainCallerDocumentLifetime` (`ClickHouseJsonNativeStorageTests.cs:188`); `check2-ch-unit.log` 42/42 exit 0. |
| D198-PG-BARE-JSONOBJECT | **gated (latent, fixed)** | r2 / n1→n2 | 1 | CHECK n=1/3: a bare `JsonObject` property on a non-CH (PG) dialect could emit `GetFieldValue<JsonObject>`. Gated in `RowMapperFactory.cs:118-135` via `dialect.NativeJsonProviderType == typeof(JsonObject)`, preserving the prior `NotSupportedException`; test `BareJsonObjectProjection_ShouldBeRejectedAsUnsupported` (`JsonColumnSqlGenerationTests.cs:178`); `check2-pg-unit.log` 41/41 exit 0. |
| D198-PROVIDERTYPE | **closed — non-defect** | r2 / n1→n2 | 0 | CHECK candidate that `SelectExpression.ProviderType` might feed the reader accessor; confirmed the mapper uses the resolved converter and `SelectExpression.ProviderType` is only the plan/cache signature — no code change. |
| D198-BARE-SCALAR-BROKEN-MAPPER | **fixed (correctness guard)** | r3 / n1 | 1 | CHECK n=2/replan: a bare top-level `Select(x => x.Doc)` over a `JsonObject`/`JsonDocument`/`JsonElement` property with no `[JsonColumn]` converter left the select list empty and reached the row materializer, failing with an opaque `System.ArgumentException: Incorrect number of arguments for constructor` (`RowMaterializerBuilder.cs:420`). Fixed with a narrow fail-fast guard `TypeFacts.IsBareDomScalar` (`TypeFacts.cs:83-93`) + `QueryCommand.QueryPreparer.cs:639-652` raising `NotSupportedException` with the named limitation; new test `BareTopLevelJsonObjectScalarProjection_ShouldFailClosedWithNamedLimitation` (`ClickHouseJsonObjectMappingTests.cs`); wrapped/streaming/tuple/`[JsonColumn]` forms unaffected; CH unit 43/43 + CH integration 205/205 exit 0. |
| D198-TVP-INFRA-FLAKE | **closed — non-defect (infra flake)** | r3 / n1 | 0 | First r=3 CH integration run failed 1/205: `ClickHouseTableValuedParameterTests.ExecuteRaw_TableParameter_NativeScalarTypes_ShouldPreserveValues` hit `TaskCanceledException: HttpClient.Timeout of 120 seconds` under container contention. Reproduced clean 1/1 in 7 s in isolation (`ch-tvp-rerun.log`) and full rerun 205/205 exit 0 (`ch-integration-r3.log`); unrelated to the E198-17 guard. |

## Decisions

- **Perf**: MEASUREMENT REQUIRED (see §Perf measurement). Baseline acceptance bench run by D198.1;
  focused `D198JsonRead` added by D198.7R2.
- **Reconnaissance**: SPIKE REQUIRED (see §Reconnaissance). D198.1 owns the spike.
- **Unit mode**: one current tree, sequential units (overlapping files). No worktree, no commits by
  this unit.
- **Storage**: CH `Auto` becomes native (behavioural API change); existing CH `String`-column users
  must select `Text` explicitly; no silent migration. Legacy `Object('json')` is **documented
  measured-unsupported** (driver 1.4.0.0 / server 25.8.33.6) — no bridge, no connection-level
  experimental setting.
- **Capability split**: `SupportsJson` = native storage; `SupportsPostgresJsonSql` = PG JSON SQL
  surface; `NativeJsonProviderType` = native provider representation.

## Risks / known issues

- **`SupportsJson` overload (audit item).** Resolved by the r=2 split (see §D198.9); `SupportsJson` no
  longer gates the PostgreSQL JSON surface, so CH native storage cannot leak `PostgresFunctions.*`.
- **Legacy `Object('json')` — measured-unsupported.** DDL needs connection-level
  `allow_experimental_object_type=1`; reads return `Tuple<SByte,String>`, not `JsonObject`; the shared
  `JsonObject` transport fails. Documented as a limitation; not implemented in #198.
- **Bare `JsonDocument`/`JsonElement` on CH** are adapted from the driver's object-root `JsonObject`
  transport with a `DBNull` guard (`ClickHouseDataContext.cs`); array/primitive roots (including
  parameter normalization) keep their existing binding rather than being silently rewritten.
- **Documented perf deviation for CHECK (focused CH benchmark not possible).** The benchmark harness
  `benchmarks/nextorm.benchmark` has **no ClickHouse project reference / `ClickHouse.Driver`**, so a real
  DB-backed `D198JsonRead` benchmark cannot run without disproportionate new infrastructure. The focused
  category therefore exercises the changed **converter/DOM seam** (`JsonColumnConverter` native
  resolution + object-root serialize/deserialize + `JsonDocument`/`JsonElement` adaptation) as a
  fallback (`benchmarks/nextorm.benchmark/JsonColumnConverterBenchmark.cs`); the changed
  `ClickHouseDataContext.CreateParam` **parameter-normalization path is covered only by CH integration
  tests, not by a database benchmark**. Numbers: CH object-root write ≈806 ns / read ≈897 ns, native
  resolve ≈4.18 ns, DOM adapt ≈694–830 ns (`perf-D198JsonRead.log`, comparison `perf-r2.md`). Acceptance
  7/7 shows no >5% degradation. **Adjudicated by the r=3 replan** (§PLAN r=3 deltas): the converter/DOM
  fallback + acceptance 7/7 + CH integration 205/205 is the binding perf evidence; the **CH DB benchmark
  is explicitly not required** (harness has no ClickHouse reference; adding it is disproportionate). This
  item is closed.
- Existing CH fixtures `json_entity`/`json_object_entity` already use native `JSON` DDL (#128).

## D198.1 outcome (baseline + semantic audit + compatibility spike)

### Baseline

- `git rev-parse HEAD` = `cd1a7c6cd8eac853c906bd9a8f598ec38dfd4bd9`.
- `dotnet build nextorm.slnx -c Debug` exit **0**, Build succeeded, **0 Warning(s) 0 Error(s)** —
  `artifacts/pdca/D198/build-debug-baseline.log`.
- `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
  exit **0**, **7** benchmarks executed (`Global total time 43.18 s`, shell wall 51 s), no failures —
  `artifacts/pdca/D198/perf-acceptance-baseline.log`; exit/wall `perf-acceptance-baseline.exit`.
  Acceptance cached/prepared rows: `Prepared_ToList 977.3 us` / `Cached_ToList 2055.8 us` (ratio
  ≈ 2.10). `BenchmarkDotNet.Artifacts` restored with `git checkout` (no artifact churn).

### Semantic audit of `SupportsJson` / `JsonSqlTranslator` (E198-01)

- Definition: `ISqlDialect.SupportsJson` (`ISqlDialect.cs:285`, doc `<see>` at `:601`); base default
  `false` (`SqlDialectBase.cs:93`); sole override `true` = `PostgresDialect.cs:325`. ClickHouse
  inherited `false` at r=1; `ClickHouseDialect` exposes `SupportsJsonExtract` (`:287`).
- Consumers (roslyn `refs` + `implementations`): `JsonColumnConverter.cs:184,188` (storage
  Auto/Native resolution), `JsonSqlTranslator.cs:20,306` (`RequireJsonSupport`), and tests
  `CommonTestSuite.JsonColumn.cs:45,98,125`, `PostgresDialectTests.cs:183`.
- **If CH `SupportsJson` flips `true`** (R198-01): two behaviours changed —
  1. `JsonColumnConverter.Resolve` maps `Auto`/`Native` to the native converter (intended storage
     change; requires a native provider type other than `JsonElement`, hence `NativeJsonProviderType`).
  2. `JsonSqlTranslator.TryTranslate` first admits only `PostgresFunctions.*` (`JsonSqlTranslator.cs:34`)
     and then `RequireJsonSupport` (`:306`) stops throwing — every PostgreSQL JSON function/operator
     call (`json_agg`, `jsonb_build_object`, `->`, `->>`, `#>`, `@>`, `jsonpath`, ...) would render
     PostgreSQL syntax on ClickHouse instead of throwing `NotSupportedException`. **Unintended leak**
     (invalid CH SQL), not covered by R198-01's storage intent.
- No PostgreSQL-specific branch is keyed off anything else CH-scoped; `JsonExtractSqlTranslator`
  (`SupportsJsonExtract`, CH-only) is independent of `SupportsJson`. Conclusion for PLAN: the additive
  storage-vs-surface split is required.

### Real-ClickHouse compatibility spike (E198-04)

Environment: driver **1.4.0.0**, server **25.8.33.6** (`artifacts/pdca/D198/ch-spike-report.txt`).

- **(a)** `create table ... (id Int32, doc JSON) engine = Memory` OK; `system.columns.type = JSON`.
- **(b)** `GetFieldValue<JsonObject>` reads an object root correctly. Top-level **array/primitive/string
  roots cannot be inserted** as literals into a `JSON` column (`Code: 117 Cannot read JSON object from
  JSON element`); native `JSON` is object-rooted. `Nullable(JSON)` DDL accepted; SQL NULL reads as
  `IsDBNull=true` / `null` and is **distinct** from `{}` — R198-03's null/`{}` distinction holds.
- **(c)** `JsonObject` parameter with `ClickHouseType="JSON"` round-trips (`DeepEquals=True`) — the
  existing #128 transport works for native `JSON`.
- **(d)** legacy `Object('json')`: plain DDL fails (`Code: 44 ... experimental Object type is not
  allowed`); an inline `SET allow_experimental_object_type = 1` on a separate command does **not**
  persist. With connection-level `CustomSettings`, DDL succeeds and the physical type is
  `Object('json')`; a string insert works, but a read materializes `System.Tuple<SByte,String>`
  (`value=(7, legacy)`) — **not** `JsonObject` — and `GetFieldValue<JsonObject>` throws
  `InvalidCastException`. Legacy is **not** transport-compatible ⇒ `C198-LEGACY-INCOMPATIBLE`.
- **(e)** Returns are eagerly materialized managed DOM instances; no reader/connection lifetime retained.

**D198.1 verdict**: native `JSON` compatible and ready; legacy `Object('json')` incompatible, so per the
plan it returned to PLAN (r=2).

## D198.9 outcome (contract carry-forward + compatibility audit)

- **Implementers of `ISqlDialect`** (roslyn `implementations`): the base `SqlDialectBase`, the provider
  dialects (PostgreSQL, ClickHouse, SQLite, SQL Server, MySQL, MariaDB, and any EF-core/extension
  dialects), plus test dialects in the unit projects. The additive members are **default interface
  members** mirrored as `SqlDialectBase` **virtuals**, so existing external implementers keep compiling
  (source-compatible) and inherit safe defaults (`SupportsPostgresJsonSql=false`,
  `NativeJsonProviderType=JsonElement`).
- **`SupportsJson` references exactly** (r=2 contract list as carried by the plan):
  `ISqlDialect.cs:285` (definition), `ISqlDialect.cs:601` (see-doc), `JsonColumnConverter.cs:13,16`
  (enum docs), `JsonColumnConverter.cs:184,188` (Auto/Native resolution), `JsonSqlTranslator.cs:20`
  (doc), `JsonSqlTranslator.cs:306` (gate), tests `CommonTestSuite.JsonColumn.cs:45,98,125`,
  `PostgresDialectTests.cs:183`.
  - **Verified at r=2 with `roslyn refs`** (line numbers shifted by the r=2 edits; the gate at
    `JsonSqlTranslator.cs:306` now reads `SupportsPostgresJsonSql`):
    `SupportsJson` def `ISqlDialect.cs:289`, doc `:300,:620`; `JsonColumnConverter.cs:14,19,211,215`;
    `JsonSqlTranslator.cs:22` (doc); tests `ClickHouseJsonNativeStorageTests.cs:51`,
    `CommonTestSuite.JsonColumn.cs:173`, `PostgresDialectTests.cs:183`,
    `JsonColumnSqlGenerationTests.cs:99`.
    `SupportsPostgresJsonSql` refs: def `ISqlDialect.cs:297`, see-doc `:287`,
    `SqlDialectBase.cs:95`, `PostgresDialect.cs:329`, `JsonSqlTranslator.cs:20,308`,
    tests `ClickHouseJsonNativeStorageTests.cs:52`, `JsonColumnSqlGenerationTests.cs:100`.
- **`JsonSqlTranslator.cs:34`** is a `DeclaringType == typeof(PostgresFunctions)` check (it admits only
  `PostgresFunctions.*` into translation), **NOT** a `SupportsJson` gate.
- **Addition pattern**: default interface members + `SqlDialectBase` virtuals (source-compat for
  implementers); no breaking interface change, no per-provider churn.
- **Compatibility audit**: the split is required because `SupportsJson` also gated the PG JSON SQL
  surface (see §Risks / E198-01); with CH `SupportsJson=true` the old gate would leak PostgreSQL JSON
  syntax onto ClickHouse. `SupportsPostgresJsonSql` closes that leak while allowing CH native storage.

## D198.2R2–D198.5R2 implementation outcome

- **D198.2R2** (`ISqlDialect`, `SqlDialectBase`, `ClickHouseDialect`, `PostgresDialect`,
  `JsonColumnConverter`): `SupportsJson` re-scoped to storage only; `SupportsPostgresJsonSql` added;
  `NativeJsonProviderType` added (default `JsonElement`, CH `JsonObject`); `JsonColumnConverter` gains a
  `JsonObject` provider representation with an object-root-only guard and dialect-aware
  `ResolveNative(dialect)`; text/options/null/equality/conflict behaviour preserved.
- **D198.3** (`SelectExpression`, `ClickHouseDataContext`): `GetDataRecordMethod` reads `JsonObject`
  through the typed accessor; `MapColumnExpression` adapts bare `JsonDocument`/`JsonElement` from the
  driver's `JsonObject` with a `DBNull` guard (`default`/`Undefined` for `JsonElement`); `CreateParam`
  normalizes an object-root element/document to `JsonObject` + `JSON`, leaving non-object roots bound
  as-is; delegation to the base path preserved for converters/other columns.
- **D198.4** (`CommonTestSuite.JsonColumn.cs`): test-local `ProbeJsonColumnType(dialect, storage)` maps
  CH native→`JSON`, PG native→`jsonb`, text→`ProbeTextType`; an unhandled native dialect throws
  explicitly (no silent `jsonb` on CH). No production SQL-type-name capability was added for tests.
- **D198.5R2** (tests): core `JsonColumnTests` (JsonObject round-trip/null/guard/options/plan-equality),
  CH `ClickHouseJsonNativeStorageTests` + `ClickHouseJsonObjectMappingTests` (dialect flags, resolved
  provider types, param normalization, PG-surface rejection, bare DOM adaptation/`DBNull`), PG
  `JsonColumnSqlGenerationTests` (converter carriage + `SupportsPostgresJsonSql` + JSON SQL generation).
  Filtered runs green (see §Progress log).

## Perf measurement

MEASUREMENT REQUIRED. Hot paths: `MapColumnExpression:61-85` (per-row accessor),
`JsonColumnConverter.ConvertFromProvider:101` (per value), `ClickHouseDataContext.CreateParam:96-112`
(per parameter). DO must run the 7-case acceptance bench before/after and D198.7R2 adds a focused BDN
category `D198JsonRead`. Thresholds: acceptance median degradation >5% or existing-object focused
>10% requires a confirmation run + explanation; unresolved regression returns to PLAN. Restore
`BenchmarkDotNet.Artifacts` with `git checkout -- BenchmarkDotNet.Artifacts`.

## Reconnaissance

SPIKE REQUIRED. Success = real ClickHouse creates intended native types, reports physical type and
version, executes object-root parameters, reads projections correctly, preserves lifetimes, and
demonstrates the legacy `Object('json')` behaviour under recorded settings. D198.1 performs the
baseline + spike; D198.2+ are gated on the spike outcome.

## Unit mode

One current tree, sequential `coder` units (overlapping files). Single collection group ⇒ no
branch/worktree/merge; no commit inside the unit. Integration tests and docs are separate DO units.

## Deferred + trigger

- Bare scalar top-level `.Select(x => x.<JsonObjectProperty>)` — retained documented limitation, not
  fixed by D198 (guard only). Trigger: a future issue explicitly requiring scalar top-level
  `JsonObject` projection.
- Legacy `Object('json')` support — **measured-unsupported** (driver 1.4.0.0 / server 25.8.33.6;
  `Tuple<SByte,String>` read, `InvalidCastException`, connection-level experimental setting). Documented
  as a limitation in D198.7R2; no bridge in #198.
- `JsonNode` declared property on CH (no node materializer) — guard only; trigger: a future issue.
- Focused BDN `D198JsonRead` — **run** by D198.7R2 as a converter/DOM fallback (harness has no
  ClickHouse driver); real-CH harness infra remains a possible future follow-up.

## Done / Verified

- **Done (CHECK PASS).** D198 complete. CHECK **PASS** at r=3, n=1/3, rv=3: all `R198-01..R198-08`
  met; the variant matrix is closed; all evidence rows `E198-01..E198-18` are closed; D198.1, D198.9,
  D198.2R2, D198.3, D198.4, D198.5R2, D198.6R2, D198.7R2, D198.10, D198.11, D198.12 all done.
- **Verified (fresh r=3 evidence).** Build 0W/0E `build-debug-r3.log`; full unit projects core
  1663/1663 (`r3-core.log`), ClickHouse 576/576 (`r3-clickhouse.log`), PostgreSQL 783/783
  (`r3-postgres.log`), SQLite/SQL Server/MySQL/MariaDB (`r3-sqlite.log`, `r3-sqlserver.log`,
  `r3-mysql.log`, `r3-mariadb.log`); full integration 3300 total / 3103 succeeded / 197 skipped /
  0 failed (`r3-full-integration.log`); PG integration 826/800/26 (`r3-pg-integration.log`);
  final-tree coverage line **88.6%** / branch **80%** exit 0, 8816 total / 8618 succeeded / 0 failed
  (`check3-coverage.log`); filtered r=3 unit/integration `core-json-tests-r3.log` (21/21),
  `clickhouse-json-tests-r3.log` (43/43), `postgres-json-tests-r3.log` (41/41),
  `ch-integration-r3.log` (205/205, 0 skipped); CH spike/legacy incompatibility recorded
  (`ch-spike-report.txt`). The only accepted perf deviation (no CH DB-backed benchmark: the harness
  has no ClickHouse reference) was adjudicated by the r=3 replan and is explicitly not required.
- **Incomplete.** — (none).

## Next plan

**— (flow closed).** CHECK PASS (r=3, n=1/3, rv=3); D198.8 ACT closed; no further cycle for D198.

## Changed files

Source (r=2 implementation):

- `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`
- `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`
- `src/nextorm.core/DataContext/Meta/JsonColumnConverter.cs`
- `src/nextorm.core/DataContext/RowMapperFactory.cs` (D198.10 `NativeJsonProviderType` reader gate)
- `src/nextorm.core/Expressions/SelectExpression.cs`
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` (D198.11 `PrepareColumns` bare-DOM scalar guard)
- `src/nextorm.core/Visitors/JsonSqlTranslator.cs`
- `src/nextorm.core/Visitors/TypeFacts.cs` (D198.11 `IsBareDomScalar` guard)
- `src/nextorm.clickhouse/ClickHouseDialect.cs`
- `src/nextorm.clickhouse/ClickHouseDataContext.cs`
- `src/nextorm.postgres/PostgresDialect.cs`

Tests (unit + integration):

- `tests/nextorm.core.tests/JsonColumnTests.cs`
- `tests/nextorm.clickhouse.tests/ClickHouseJsonObjectMappingTests.cs`
- `tests/nextorm.clickhouse.tests/ClickHouseJsonNativeStorageTests.cs` (new)
- `tests/nextorm.postgres.tests/JsonColumnSqlGenerationTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite.JsonColumn.cs`
- `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs` (D198.6R2 native-JSON tests)
- `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs` (D198.6R2 `json_native_poco` fixture)

Benchmark (new):

- `benchmarks/nextorm.benchmark/JsonColumnConverterBenchmark.cs` (new, focused `D198JsonRead`)

Docs EN:

- `docs/guide/14-json.md`
- `docs/guide/provider-specific/clickhouse.md`
- `docs/providers/clickhouse.md`
- `docs/providers/overview.md`
- `docs/providers/postgres.md`
- `docs/advanced/limitations.md`
- `docs/infrastructure/04-value-converters.md`

Docs RU:

- `docs/ru/guide/14-json.md`
- `docs/ru/guide/provider-specific/clickhouse.md`
- `docs/ru/providers/clickhouse.md`
- `docs/ru/providers/overview.md`
- `docs/ru/providers/postgres.md`
- `docs/ru/advanced/limitations.md`
- `docs/ru/infrastructure/04-value-converters.md`

Task/status:

- `docs/specs/status/rc1-tail-198-ch-native-json-1.md` (this file)
- `docs/specs/status/collection-1.0.9-rc1-tail.md` (D198 row updated — orchestrator-owned)

## Pointers

- Issue #198 (follow-up to #128).
- Predecessor pattern: `docs/specs/status/rc1-tail-197-pg-jsonnode-1.md`.
- Sources: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:289`,
  `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:95`,
  `src/nextorm.core/DataContext/Meta/JsonColumnConverter.cs:204-233`,
  `src/nextorm.core/Visitors/JsonSqlTranslator.cs:20,308`,
  `src/nextorm.core/Visitors/TypeFacts.cs:83-93` (D198.11),
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:639-652` (D198.11),
  `src/nextorm.clickhouse/ClickHouseDataContext.cs:61-112`,
  `benchmarks/nextorm.benchmark/JsonColumnConverterBenchmark.cs`,
  `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs:180-209`,
  `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1114-1185,1626-1634`.
- Docs touched (D198.7R2): `docs/guide/14-json.md`, `docs/guide/provider-specific/clickhouse.md`,
  `docs/providers/{clickhouse,overview,postgres}.md`, `docs/advanced/limitations.md`,
  `docs/infrastructure/04-value-converters.md` (+ `docs/ru/**` mirrors).
- Evidence (D198.7R2): `artifacts/pdca/D198/{docfx.log,perf-acceptance-final.log,perf-D198JsonRead.log,perf-r2.md,coverage-r2.log,build-debug-final-r2.log,scope-final-r2.txt}`.
- Base commit: `cd1a7c6c` (`artifacts/pdca/D198/baseline-head.txt`).
- Evidence (r=3 CHECK/ACT): `artifacts/pdca/D198/{check3-coverage.log,r3-full-integration.log,r3-core.log,r3-clickhouse.log,r3-postgres.log,r3-pg-integration.log,r3-full-native.log,r3-scope.txt,ch-integration-r3.log,core-json-tests-r3.log,clickhouse-json-tests-r3.log,postgres-json-tests-r3.log}`.

## Evidence contract (rv=3; E198-01..E198-18)

| row | command / action | required result | actual result | owner |
|---|---|---|---|---|
| E198-01 | semantic audit of `SupportsJson` (definition + all refs + implementers) and `JsonSqlTranslator` | exact `file:line`; which CH SQL translation flips; no PG leak | PASS — def `ISqlDialect.cs:285`, base `SqlDialectBase.cs:93`, PG `PostgresDialect.cs:325`; consumers `JsonColumnConverter.cs:184,188` + `JsonSqlTranslator.cs:20,306`; flipping CH true also admits `PostgresFunctions.*` on CH (leak) | coder |
| E198-02 | `dotnet build nextorm.slnx -c Debug` baseline | `0 Warning(s) 0 Error(s)` | PASS — exit 0, Build succeeded, 0W/0E; `artifacts/pdca/D198/build-debug-baseline.log` | coder |
| E198-03 | baseline acceptance bench `--anyCategories=acceptance` | 7 cases, 0 failures | PASS — exit 0, 7 executed, BDN total 43.18 s, wall 51 s; `artifacts/pdca/D198/perf-acceptance-baseline.log` | coder |
| E198-04 | real-CH compatibility spike (a)-(e) | raw outcomes recorded (SQL/error text) | PASS — native `JSON` compatible; array/primitive roots rejected; legacy `Object('json')` incompatible (Tuple read); `artifacts/pdca/D198/ch-spike-report.txt` | coder |
| E198-05 | core unit `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~JsonColumn` | exit 0 | PASS — exit 0, total 21 / passed 21 / failed 0 / skipped 0; `artifacts/pdca/D198/core-json-tests-r3.log` | coder |
| E198-06 | CH unit `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter FullyQualifiedName~Json` | exit 0 | PASS — exit 0, total 43 / passed 43 / failed 0 / skipped 0 (incl. new E198-17 test); `artifacts/pdca/D198/clickhouse-json-tests-r3.log` | coder |
| E198-07 | provider regressions (postgres/sqlite/sqlserver/mysql/mariadb) | exit 0 each | PASS (PG) — `dotnet test tests/nextorm.postgres.tests -c Debug --filter FullyQualifiedName~Json` exit 0, total 41 / passed 41 / failed 0 / skipped 0, `artifacts/pdca/D198/postgres-json-tests-r3.log`; other providers unchanged and full sweep green per E198-10 | coder |
| E198-08 | CH integration `--filter "FullyQualifiedName~ClickHouse"` with `DOCKER_HOST` | executed not skipped | PASS — r=2 `artifacts/pdca/D198/ch-integration-r2.log` exit 0, total 205 / succeeded 205 / skipped 0; r=3 re-confirmation after the E198-17 guard `artifacts/pdca/D198/ch-integration-r3.log` exit 0, total 205 / succeeded 205 / skipped 0 (one earlier r=3 run hit a transient 120 s HttpClient timeout in `ClickHouseTableValuedParameterTests.ExecuteRaw_TableParameter_NativeScalarTypes_ShouldPreserveValues`; it passed 1/1 in 7 s in isolation, `ch-tvp-rerun.log`, and the clean rerun is 205/205 — infra flake, not a guard regression) | coder |
| E198-09 | PG integration `--filter "FullyQualifiedName~Postgres"` | exit 0 | PASS — exit 0, total 826, succeeded 800, skipped 26; `artifacts/pdca/D198/pg-integration-r2.log` | coder |
| E198-10 | full integration suite with `DOCKER_HOST` | required providers execute | PASS — exit 0, total 3300, succeeded 3103, skipped 197, failed 0; executed CH 205, PG 800, SQLite 668, SQL Server 676, MySQL 585; `artifacts/pdca/D198/full-integration-r2.log` | coder |
| E198-11 | docs EN+RU diff + `dotnet docfx docs/docfx.json` | EN/RU parity; docfx 0 errors | PASS — docs EN+RU updated (storage matrix, `SupportsPostgresJsonSql` split, legacy measured-unsupported, bare-scalar limitation) in `docs/{guide,providers,advanced,infrastructure}/**/**.md` + `docs/ru/**`; docfx exit 0, `0 error(s)`, 2 pre-existing warnings; `artifacts/pdca/D198/docfx.log` | coder |
| E198-12 | coverage | ≥ `MIN_LINE_COVERAGE`/`MIN_BRANCH_COVERAGE` | PASS — commands reproduced from `.github/workflows/dotnet.yml:45,55-59` (`dotnet-coverage collect` over `coverage.settings.xml` + `reportgenerator`); the workflow persists no exit code, so the recorded run is `artifacts/pdca/D198/coverage-r2.log` — line **88.6%** (≥ `MIN_LINE_COVERAGE=85`), branch **80%** (≥ `MIN_BRANCH_COVERAGE=75`); changed paths `JsonColumnConverter` 95%, `JsonColumnConverterFactory` 100%, `JsonSqlTranslator` 95.5%, `PostgresDialect` 94.1%; final-tree r=3 re-run `artifacts/pdca/D198/check3-coverage.log` exit 0 — 8816 total / 8618 succeeded / 0 failed, line **88.6%** / branch **80%** | coder |
| E198-13 | scope/provenance `git diff --name-only` / `git status --short` / `git diff --check` | planned files only; no whitespace errors | PASS — `git diff --check` exit 0; final r=3 tree `git diff --name-only` **31** tracked-modified; `git status --short` **35** entries (4 untracked: `artifacts/pdca/`, `benchmarks/nextorm.benchmark/JsonColumnConverterBenchmark.cs`, this status file, `tests/nextorm.clickhouse.tests/ClickHouseJsonNativeStorageTests.cs`); all uncommitted on `1.0.9-rc1`; snapshots `artifacts/pdca/D198/scope-final-r2.txt`, `artifacts/pdca/D198/r3-scope.txt` | coder |
| E198-14 | D198.9 contract carry-forward + compatibility audit | implementers + all `SupportsJson` refs with `file:line`; additive pattern recorded | PASS — refs above; default interface members + `SqlDialectBase` virtuals | coder |
| E198-15 | r=2 inner-loop evidence: build + filtered unit tests + `validate_inner_loop.py` | exit 0; selected_count ≥ 1 per run; validator exit 0 | PASS — `artifacts/pdca/D198/evidence-r2-impl.json`; validator exit 0 | coder |
| E198-16 | fresh r=2 n=2 re-verification (CHECK→DO return): solution build + filtered core/CH/PG unit + CH integration (DOCKER_HOST) + `validate_inner_loop.py report evidence-r2-do2.json` | build 0W/0E; every test run exit 0; CH integration executed (>0, not skipped); validator exit 0 | PASS — build exit 0 0W/0E `check2-build-final.log`; core 21/21 `check2-core.log`; CH unit 42/42 `check2-ch-unit.log`; PG unit 41/41 `check2-pg-unit.log`; CH integration 205 total/205 passed/0 failed/0 skipped `check2-ch-integration.log`; validator exit 0 on `evidence-r2-do2.json` | coder |
| E198-17 | D198.11 bare top-level scalar negative regression: CH test performs exactly `.Select(x => x.Doc)` on a bare `JsonObject` column | observed behavior captured; deterministic exception asserted by exact type; wrapped form still green | PASS — pre-guard observation: `System.ArgumentException: Incorrect number of arguments for constructor` at `RowMaterializerBuilder.cs:420`; guard added at `TypeFacts.cs:83-93` (`IsBareDomScalar`, JsonObject/JsonDocument/JsonElement) + `QueryCommand.QueryPreparer.cs:639-652` (`NotSupportedException`); new test `ClickHouseJsonObjectMappingTests.BareTopLevelJsonObjectScalarProjection_ShouldFailClosedWithNamedLimitation` asserts `NotSupportedException` + message `*bare top-level scalar*JsonObject*`; wrapped positive `BareJsonObjectProjection_ShouldRenderAndBuildTheRowMapper` still green; CH unit 43/43 `clickhouse-json-tests-r3.log` exit 0 | coder |
| E198-18 | D198.12 contract-binding validation: `rv=3` rebinding + per-row artifact/`file:line`/exit binding | all `rv=1` labels relabelled `rv=3`; no `rv=2` claimed; stable row IDs preserved; fresh r=3 evidence | PASS — `rg "rv=1\|rv=2"` in this file now returns only the two intentional historical references in the r=3 deltas text; build 0W/0E `build-debug-r3.log` exit 0; core 21/21/CH 43/43/PG 41/41/CH-integration 205/205; `validate_inner_loop.py report artifacts/pdca/D198/evidence-r3.json` exit 0 | coder |

**Priority floor:** all `R198-*` acceptance criteria and every evidence row above are **P1 by
construction** (nextorm overlay) — none may be downgraded, deferred or dropped.

## Progress log

- 2026-10-06T17:28Z | PLAN | r=1 | iteration n=1/3 | PLAN ready (autonomous mode — no confirmation) | this file
- 2026-10-06T17:28Z | DO | r=1 | iteration n=1/3 | plan written — DO started | this file
- Notice: host has no todowrite for subagents; status file carries the log.
- Notice: `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` available in host skill dir.
- 2026-10-06T17:29Z | DO | r=1 | n=1/3 | baseline: HEAD `cd1a7c6cd8eac853c906bd9a8f598ec38dfd4bd9`; `dotnet build nextorm.slnx -c Debug` exit 0, 0W/0E | `artifacts/pdca/D198/{baseline-head.txt,build-debug-baseline.log}`
- 2026-10-06T17:31Z | DO | r=1 | n=1/3 | baseline acceptance bench `--anyCategories=acceptance` exit 0, 7/7 executed, BDN 43.18 s / wall 51 s; `BenchmarkDotNet.Artifacts` restored | `artifacts/pdca/D198/perf-acceptance-baseline.log`
- 2026-10-06T17:32Z | DO | r=1 | n=1/3 | roslyn audit E198-01: `SupportsJson` gates converter (`JsonColumnConverter.cs:184,188`) AND PG JSON surface (`JsonSqlTranslator.cs:306`); flipping CH true leaks `PostgresFunctions.*` on CH | this file §D198.1 outcome
- 2026-10-06T17:36Z | DO | r=1 | n=1/3 | CH spike (driver 1.4.0.0 / server 25.8.33.6): native `JSON` OK (object root, param round-trip, `Nullable(JSON)` NULL ≠ `{}`); array/primitive roots rejected; legacy `Object('json')` incompatible (Tuple read, connection-level setting) | `artifacts/pdca/D198/ch-spike-report.txt`, `ch-spike-run4.log`
- 2026-10-06T17:37Z | DO→PLAN candidate | r=1 | n=1/3 | `C198-LEGACY-INCOMPATIBLE` open: R198-04 legacy sub-branch must be dropped by PLAN r=2; **D198.2 not started** | this file, E198-04
- 2026-10-06T17:37Z | D198.1 done | r=1 | n=1/3 | baseline + audit + spike complete; temporary spike test deleted; tree clean (`git status --short` = untracked `artifacts/pdca/` + this file) | `artifacts/pdca/D198/*`
- 2026-10-06T17:50Z | Replanned | r 1→2 | iteration n=1/3 | Replanned: legacy Object('json') transport incompatible + SupportsJson also gated PG JSON SQL surface — split storage vs SQL capability (r 1→2, iteration 1/3) | this file §PLAN r=2 deltas
- 2026-10-06T17:50Z | Notice | r=2 | iteration n=1/3 | Notice: r=2 scope decision — legacy Object('json') documented measured-unsupported (driver 1.4.0.0/server 25.8.33.6); no bridge in #198. | this file §PLAN r=2 deltas
- 2026-10-06T17:50Z | DO | r=2 | n=1/3 | D198.9 done: contract carry-forward + compatibility audit (`SupportsJson` refs + implementers, gate moved to `SupportsPostgresJsonSql`, default interface members + `SqlDialectBase` virtuals) | this file §D198.9 outcome
- 2026-10-06T17:50Z | DO | r=2 | n=1/3 | D198.2R2 done: storage/surface split + `NativeJsonProviderType` + `JsonObject` converter/guard | this file §D198.2R2–D198.5R2
- 2026-10-06T17:50Z | DO | r=2 | n=1/3 | D198.3 done: `GetDataRecordMethod` JsonObject accessor; bare DOM read adaptation + `CreateParam` object-root normalization | this file §D198.2R2–D198.5R2
- 2026-10-06T17:50Z | DO | r=2 | n=1/3 | D198.4 done: test-local `ProbeJsonColumnType` (CH native→JSON, PG→jsonb, unknown native dialect throws) | `tests/nextorm.integration.tests/CommonTestSuite.JsonColumn.cs`
- 2026-10-06T17:50Z | DO | r=2 | n=1/3 | D198.5R2 done: core/CH/PG JSON-column unit + SQL-generation tests added | `tests/nextorm.{core,clickhouse,postgres}.tests/*Json*.cs`
- 2026-10-06T17:50Z | DO | r=2 | n=1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0W/0E | `artifacts/pdca/D198/build-debug-r2-impl.log`
- 2026-10-06T17:51Z | DO | r=2 | n=1/3 | test-run `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~JsonColumn` first run failed 1/20 (`D198-TESTCASE-DEFAULT-NAMING`, test-only); fixed assertion to `obj["Name"]` | `artifacts/pdca/D198/core-json-tests-r2.log` (rerun green)
- 2026-10-06T17:51Z | DO | r=2 | n=1/3 | test-run `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~JsonColumn` exit 0, 20/20 | `artifacts/pdca/D198/core-json-tests-r2.log`
- 2026-10-06T17:51Z | DO | r=2 | n=1/3 | test-run `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter FullyQualifiedName~Json` exit 0, 32/32 | `artifacts/pdca/D198/clickhouse-json-tests-r2.log`
- 2026-10-06T17:51Z | DO | r=2 | n=1/3 | test-run `dotnet test tests/nextorm.postgres.tests -c Debug --filter FullyQualifiedName~Json` exit 0, 35/35 | `artifacts/pdca/D198/postgres-json-tests-r2.log`
- 2026-10-06T17:52Z | DO | r=2 | n=1/3 | D198.6R2/D198.7R2 pending (integration + docs/measurement at the DO→CHECK boundary); `validate_inner_loop.py report` exit 0 | `artifacts/pdca/D198/evidence-r2-impl.json`
- 2026-10-06T18:00Z | DO | r=2 | n=1/3 | D198.6R2: added CH native-JSON integration coverage (`[JsonColumn]` Auto/Native object-root POCO round-trip + physical type via `system.columns`; native `JsonObject`/object-root `JsonDocument`/`JsonElement` parameter round-trip; bare `JsonObject`/`JsonDocument`/`JsonElement` projection; `Nullable(JSON)` SQL NULL ≠ `{}`; non-object-root guard before execution) + fixture `json_native_poco` | `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`, `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs`
- 2026-10-06T18:00Z | DO | r=2 | n=1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0W/0E | `artifacts/pdca/D198/build-debug-r2-integration-final.log`
- 2026-10-06T18:00Z | DO | r=2 | n=1/3 | test-run `dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~ClickHouse` (DOCKER_HOST) first run failed 1/205 (`D198-INT-BARE-ENTITY-CTOR`, test-only); fixed; rerun exit 0, 205/205, 0 skipped | `artifacts/pdca/D198/ch-integration-r2.log`
- 2026-10-06T18:00Z | DO | r=2 | n=1/3 | test-run `dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~Postgres` (DOCKER_HOST) exit 0, 826 total / 800 passed / 26 skipped | `artifacts/pdca/D198/pg-integration-r2.log`
- 2026-10-06T18:00Z | DO | r=2 | n=1/3 | test-run full integration suite (DOCKER_HOST) exit 0, 3300 total / 3103 passed / 197 skipped / 0 failed; executed CH 205, PG 800, SQLite 668, SQL Server 676, MySQL 585 | `artifacts/pdca/D198/full-integration-r2.log`
- 2026-10-06T18:00Z | DO | r=2 | n=1/3 | D198.6R2 done: CH executed not skipped + PG regression + full sweep green; `validate_inner_loop.py report artifacts/pdca/D198/evidence-r2-integration.json` exit 0 | `artifacts/pdca/D198/evidence-r2-integration.json`
- 2026-10-06T18:18Z | DO | r=2 | n=1/3 | D198.7R2 docs EN+RU complete: storage matrix + `SupportsPostgresJsonSql` split + legacy `Object('json')` measured-unsupported + bare-scalar limitation recorded in `docs/**` and `docs/ru/**`; `dotnet docfx docs/docfx.json` exit 0, `0 error(s)`, 2 pre-existing warnings; `git diff --check` exit 0 | `artifacts/pdca/D198/docfx.log`
- 2026-10-06T18:18Z | DO | r=2 | n=1/3 | test-run acceptance bench `--anyCategories=acceptance` final exit 0, 7/7 executed, BDN 44.65 s / wall 55 s, no case >5% median degradation (all cases equal or faster, allocation flat) | `artifacts/pdca/D198/perf-acceptance-final.log`
- 2026-10-06T18:18Z | DO | r=2 | n=1/3 | test-run focused `D198JsonRead` exit 0, 9/9 executed, BDN 76.84 s — **documented fallback** (harness has no ClickHouse driver): CH object-root write ≈806 ns / read ≈897 ns, native resolve ≈4.18 ns, DOM ≈694–830 ns | `artifacts/pdca/D198/perf-D198JsonRead.log`, `artifacts/pdca/D198/perf-r2.md`
- 2026-10-06T18:18Z | DO | r=2 | n=1/3 | coverage `dotnet-coverage collect` + `reportgenerator` exit 0: line 88.6% (≥85), branch 80% (≥75); changed paths `JsonColumnConverter` 95%, `JsonSqlTranslator` 95.5% | `artifacts/pdca/D198/coverage-r2.log`
- 2026-10-06T18:18Z | DO | r=2 | n=1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, Build succeeded, 0 Warning(s) 0 Error(s) — D198.7R2 closure | `artifacts/pdca/D198/build-debug-final-r2.log`
- 2026-10-06T18:18Z | DO | r=2 | n=1/3 | D198.7R2 done (docs/measurement/closure); documented perf deviation (focused CH benchmark not possible — harness has no ClickHouse reference; converter/DOM fallback used; `CreateParam` covered by integration, not a DB benchmark) recorded under §Risks for CHECK adjudication | this file §Risks / known issues
- 2026-10-06T18:20Z | CHECK fail | r=2 | iteration n=1/3 | CHECK fail (r=2 n=1/3): open variant rows + candidate defects → DO | this file §Defect history
- 2026-10-06T18:22Z | DO | r=2 | n=2/3 | `D198-NULL-ELEMENT` fixed (`ClickHouseDataContext.cs:87-98`; red→green `BareNullableJsonElementProjection_SqlNull_ShouldMaterializeNull`); `D198-LIFETIME` fixed (`ClickHouseDataContext.cs:139-147`; `CreateParam_ObjectRootDocument_ShouldNotRetainCallerDocumentLifetime`); `D198-PG-BARE-JSONOBJECT` gated (`RowMapperFactory.cs:118-135`; `BareJsonObjectProjection_ShouldBeRejectedAsUnsupported`); `D198-PROVIDERTYPE` non-defect | this file §Defect history
- 2026-10-06T18:22Z | DO | r=2 | n=2/3 | variant gaps closed: JSON-null root guard, `->`/`@>` CH rejection + PG generation, PG `NativeJsonProviderType==JsonElement` + Auto/Native/Text resolution, `[JsonColumn]` DOM-typed coverage, differing-options test, final newline | this file §Defect history
- 2026-10-06T18:34Z | DO | r=2 | n=2/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, Build succeeded, 0 Warning(s) 0 Error(s) | `artifacts/pdca/D198/check2-build-final.log`
- 2026-10-06T18:34Z | DO | r=2 | n=2/3 | test-run `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~JsonColumn` exit 0, 21 total / 21 passed / 0 failed / 0 skipped | `artifacts/pdca/D198/check2-core.log`
- 2026-10-06T18:34Z | DO | r=2 | n=2/3 | test-run `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter FullyQualifiedName~Json` exit 0, 42 total / 42 passed / 0 failed / 0 skipped | `artifacts/pdca/D198/check2-ch-unit.log`
- 2026-10-06T18:34Z | DO | r=2 | n=2/3 | test-run `dotnet test tests/nextorm.postgres.tests -c Debug --filter FullyQualifiedName~Json` exit 0, 41 total / 41 passed / 0 failed / 0 skipped | `artifacts/pdca/D198/check2-pg-unit.log`
- 2026-10-06T18:35Z | DO | r=2 | n=2/3 | test-run `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~ClickHouse` exit 0, 205 total / 205 passed / 0 failed / 0 skipped (CH executed, >0) | `artifacts/pdca/D198/check2-ch-integration.log`
- 2026-10-06T18:35Z | DO | r=2 | n=2/3 | `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py report artifacts/pdca/D198/evidence-r2-do2.json` exit 0 | `artifacts/pdca/D198/evidence-r2-do2.json`
- 2026-10-06T18:35Z | DO n=2 complete | r=2 | n=2/3 | DO n=2 complete — fixes verified, variant gaps closed | this file §Defect history / E198-16
- 2026-10-06T18:58Z | Replanned | r 2→3 | iteration n=1/3 | Replanned: perf-contract equivalence + missing top-level scalar regression + contract rebinding (r 2→3, iteration 1/3) | this file §PLAN r=3 deltas
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | D198.11 done: bare top-level JsonObject scalar: observed broken-mapper ArgumentException at RowMaterializerBuilder.cs:420 → added fail-closed guard TypeFacts.cs:83-93 + QueryPreparer.cs:639-652 + test; CH unit 43/43 | tests/nextorm.clickhouse.tests/ClickHouseJsonObjectMappingTests.cs, artifacts/pdca/D198/clickhouse-json-tests-r3.log
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | D198.12 done: contract rebind rv=3, perf supersession, P1 floor, row anchors | this file §Durable state / E198-18
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, Build succeeded, 0 Warning(s) 0 Error(s) | artifacts/pdca/D198/build-debug-r3.log
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | test-run `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~JsonColumn` exit 0, 21 total / 21 passed / 0 failed / 0 skipped | artifacts/pdca/D198/core-json-tests-r3.log
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | test-run `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter FullyQualifiedName~Json` exit 0, 43 total / 43 passed / 0 failed / 0 skipped | artifacts/pdca/D198/clickhouse-json-tests-r3.log
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | test-run `dotnet test tests/nextorm.postgres.tests -c Debug --filter FullyQualifiedName~Json` exit 0, 41 total / 41 passed / 0 failed / 0 skipped | artifacts/pdca/D198/postgres-json-tests-r3.log
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | test-run `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~ClickHouse` exit 0, 205 total / 205 passed / 0 failed / 0 skipped (CH executed, >0) | artifacts/pdca/D198/ch-integration-r3.log
- 2026-10-06T18:58Z | DO | r=3 | n=1/3 | D198-TVP-INFRA-FLAKE closed: transient 120 s HttpClient timeout reproduced clean 1/1 in 7 s (`ch-tvp-rerun.log`) and clean rerun 205/205 (`ch-integration-r3.log`) — infra flake, not an E198-17 guard regression | this file §Defect history
- 2026-10-07T00:20Z | CHECK PASS | r=3 | iteration n=1/3 | CHECK PASS (r=3 n=1/3, rv=3) — all R198-01..08 and E198-01..18 closed | artifacts/pdca/D198/check3-coverage.log, r3-full-integration.log, r3-*.log
- 2026-10-07T00:25Z | ACT closed | r=3 | iteration n=1/3 | ACT closed; flow closed | this file
