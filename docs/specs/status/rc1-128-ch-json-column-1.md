# ClickHouse JSON column mapping — issue #128 (task D128)

- task: D128
- issue: #128 (https://github.com/AlexeyShirshov/nextorm/issues/128)
- collection: 1.0.9-rc1
- group: G01
- branch: 1.0.9-rc1
- cycle: N=1
- plan revision: r=2
- attempt: n=1/3
- contract: rv=D128.r2.ec1
- mode: autonomous + auto-commit
- git: no push/merge

## Goal

ClickHouse: map the native `JSON` column type (projection and parameter); the driver surfaces `JsonObject`.

## Reconnaissance (persisted; reader/type-mapping site ESTABLISHED)

- Issue #128 "ClickHouse: маппинг нативного типа колонки JSON"; OPEN; enhancement; milestone 1.0.9-rc1; no PRs/comments. Scope: reader/type-mapping of a native `JSON` column (projection and parameter); driver returns `JsonObject`. Body links `docs/guide/16-json.md` (stale; actual `docs/guide/14-json.md`) and `docs/guide/provider-specific/clickhouse.md`.
- **Reader site established:** `src/nextorm.core/Expressions/SelectExpression.cs:177 GetDataRecordMethod(Type readType)` — central dispatcher: `:246` JsonDocument, `:251` JsonElement, `:261` arrays, `:268` Tuple, `:274` `Dictionary<string,string>`, else `NotSupportedException:281`. **No `JsonObject` branch.** Call path: `RowMapperFactory.MapColumn:48` → `GetReaderAccessor:114` → `Expression.Call(record, method, ordinal):120`.
- Provider seam: `DataContext.MapColumnExpression` (`DataContext.cs:621`) → `RowMapperFactory.MapColumn`; overridden only by `PostgresDataContext.cs:235` and `SqlServerDataContext.cs:379`; **ClickHouse does not override.** `SupportsJson` default false `SqlDialectBase.cs:93`, override only `PostgresDialect.cs:325`; ClickHouse has only `SupportsJsonExtract => true` (`ClickHouseDialect.cs:287`).
- Today: native CH column not mapped. `[JsonColumn]`+Auto → `SupportsJson==false` → text converter (`string`) → `GetString` on a JSON field; `Native` → `JsonElement` → `GetFieldValue<JsonElement>` (`JsonColumnConverter.cs:159,184-188`). Both wrong: `ClickHouse.Driver` `FrameworkType` for JSON = `typeof(JsonObject)` (driver 1.4.0, `Directory.Packages.props:9`). Parameter: `ClickHouseDataContext.CreateParam` (`ClickHouseDataContext.cs:55`) infers from CLR value — no `JsonObject` branch.
- Materialization point: `RowMapperFactory.GetReaderAccessor` (`RowMapperFactory.cs:114-121`) via `SelectExpression.GetDataRecordMethod` (`SelectExpression.cs:177`); a `JsonObject` property → `NotSupportedException:281`; a `string` property → type mismatch on `GetString`.
- Tests: SQL-gen only functions `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs:172,180,189`, `SqlGenerationTests.cs:1248,1276,1311,1329`; integration `ClickHouseIntegrationTests.cs:1008 (JsonAllPaths), :1020 (Map), :1034 (toJSONString)`; fixture `Providers/ClickHouseTestProvider.cs:180-192` (`create table json_entity (id Int32, doc JSON)`); `IJsonEntity.Doc` is `string` (`:1466-1473`). **No test reads the native column.** `ClickHouseSpecificTests.cs` does not exist. `CommonTestSuite.JsonColumn.cs` is PostgreSQL-oriented.
- Docs EN/RU: `docs/guide/provider-specific/clickhouse.md:258` ("Not yet supported … reader/type-mapping"); `docs/ru/providers/clickhouse.md:236`; `docs/ru/advanced/limitations.md:27` (`JsonObject` not materializable by the row reader); `docs/guide/14-json.md:192-214` + table `:55`; RU `:55,193`. Specs `sql-capabilities-gap-analysis.md:120-122,136-139`, `linq2db-backlog-gap-analysis.md:510`, `API-NAMING-REVIEW.md:545,5047`.
- **Minimal seam:** override `ClickHouseDataContext.MapColumnExpression` for `JsonObject` (analog `PostgresDataContext.cs:235`) + enable `SupportsJson` + support `JsonObject` in `JsonColumnConverter`/`GetDataRecordMethod:177`; parameter path `ClickHouseDataContext.cs:55-64`.
- Open PLAN questions: exact CLR contract (`JsonObject` vs `JsonDocument`/`JsonElement`/`string`); read+write scope; legacy `Object('json')` alias; `[JsonColumn]` storage; tests (real ClickHouse container is a hard prerequisite); docs.

## Locked plan r=2 (rv=D128.r2.ec1)

Decision **A** (locked): bare `JsonObject` read+write, **provider-local `src/nextorm.clickhouse` only**. `[JsonColumn]`/`Auto`/`SupportsJson` are **unchanged**; **no** shared `GetDataRecordMethod` branch; the legacy `Object('json')` alias and option B (dialect `SupportsJson` integration) are **deferred**.

### Acceptance criteria

- D128.R01: a native ClickHouse `JSON` column projected as `JsonObject` is materialized by the buffered row mapper (nested document plus empty `{}`) via the proven accessor.
- D128.R02: a `JsonObject` parameter binds through the proven native JSON parameter mechanism and round-trips.
- D128.R03: a SQL `NULL` in a `JsonObject` column materializes as `null` (defensive `DBNull` handling).
- D128.R04: provider-local only — `[JsonColumn]`, `Auto` storage and `SupportsJson` behave exactly as before; `SelectExpression.GetDataRecordMethod`, `JsonColumnConverter`, and other providers are untouched.
- D128.R05: every non-`JsonObject` column delegates to the base mapping unchanged (no regression in existing ClickHouse reads).
- D128.R06: no literal interpolation / string formatting into SQL or the parameter placeholder; the native driver mechanism is used.
- D128.R07: `dotnet build nextorm.slnx -c Debug` is 0 errors / 0 warnings under `TreatWarningsAsErrors=true`.

### DO units

- D128.D01 (Step 1, mandatory prerequisite): driver probe against real ClickHouse via `/tmp/nextorm-D128-r2/probe` — which accessor works (`GetFieldValue<JsonObject>` vs `GetValue`), the actual CLR types, the `JsonObject` parameter mechanism (`{p:JSON}` vs string), and the driver/server versions. If neither native accessor works → STOP for PLAN (no text-parsing substitute, no dependency change).
- D128.D02: `ClickHouseDataContext.MapColumnExpression` override for **exactly** `typeof(JsonObject)` using the proven accessor; `DBNull` → `null` defensively; all other types delegate to base.
- D128.D03: `JsonObject` branch in `ClickHouseDataContext.CreateParam` (`:55`) using the proven native JSON parameter mechanism.
- D128.D04: type-mapper applicability — via roslyn check whether a ClickHouse CLR type-mapper is consumed by supported temp/TVP paths; add provider-local `JsonObject → JSON` only if a supported caller needs it, else record non-applicability evidence.
- D128.D05: build `nextorm.slnx -c Debug` 0/0.
- D128.D06: append probe/implementation/build evidence and progress rows to this status file (CRLF).

### Variant matrix

| Variant | Core change | `[JsonColumn]`/`Auto` | `SupportsJson` | Scope | Verdict |
| --- | --- | --- | --- | --- | --- |
| A (chosen) | none (provider override only) | unchanged | unchanged | CH provider-local | minimal, no cross-provider risk |
| B | dialect `SupportsJson=true` + converter | integrated | changed | core + CH | deferred (widens contract, out of issue scope) |
| C | `SelectExpression.GetDataRecordMethod` `JsonObject` branch | unchanged | unchanged | shared core | rejected by decision A (shared branch) |
| D | legacy `Object('json')` alias | unchanged | unchanged | CH | deferred (legacy alias) |

### Test / perf / recon decisions

- Tests: **deferred to the next DO stream** (acceptance + regression coverage of D128.R01–R03). The real ClickHouse container is a hard prerequisite and is exercised by the probe.
- Perf: cached row-mapper path unchanged in shape — one accessor call per column, no per-row allocations beyond the driver-owned `JsonObject`; no plan-cache interaction.
- Recon: persisted above; reader/type-mapping site established; probe (D128.D01) is the remaining unknown.

### Design checklist

- XML docs on the new override and any new helper; no public API widening beyond an existing `public` override.
- Provider-local: only `src/nextorm.clickhouse/ClickHouseDataContext.cs` may change (plus evidence rows here).
- `TreatWarningsAsErrors=true` clean; CRLF preserved.
- No dependency/version changes; no commit/push.

### Evidence rows

- D128.E01: driver + server versions; probe accessor result for a nested document.
- D128.E02: probe result for empty `{}`.
- D128.E03: probe parameter mechanism/type round-trip.
- D128.E04: `ClickHouseDataContext` diff (override + `CreateParam` branch).
- D128.E05: roslyn type-mapper applicability verdict.
- D128.E06: build log path + exit code + error/warning counts.
- D128.E07: acceptance D128.R01–R07 mapping.
- D128.E08: scope proof — no changes to core/other providers.
- D128.E09: CRLF verification.
- D128.E10: deferrals (tests/docs/B follow-up).

### CHECK re-gather budget

- 2 (a CHECK re-gather may occur at most twice before escalation).

## Progress log

Recon persisted (run 8 start); reader/type-mapping site established.

- Notice: host has no todowrite tool; this file is the progress log.
- 2026-10-06T00:53:38Z | DO | r=2 | n=1/3 | probe D128.D01 started | /tmp/nextorm-D128-r2/probe
- 2026-10-06T00:53:38Z | DO | r=2 | n=1/3 | probe D128.D01 passed (accessor + native JSON parameter) | /tmp/nextorm-D128-r2/probe-run2.log
- 2026-10-06T00:53:38Z | DO | r=2 | n=1/3 | implemented D128.D02/D03 (provider-local override + CreateParam branch) | src/nextorm.clickhouse/ClickHouseDataContext.cs
- 2026-10-06T00:53:38Z | DO | r=2 | n=1/3 | D128.D04 type-mapper non-applicable (roslyn: only TVP calls ColumnTypeName) | src/nextorm.clickhouse/ClickHouseDataContext.cs:101,109
- 2026-10-06T00:53:38Z | DO | r=2 | n=1/3 | build D128.D05 green 0/0 | /tmp/nextorm-D128-r2/build.log
- 2026-10-06T01:06:00Z | DO | r=2 | n=1/3 | DO stream 2 started (perf probe + docs EN/RU + B issue + hygiene) | /tmp/nextorm-D128-r2
- 2026-10-06T01:06:00Z | DO | r=2 | n=1/3 | perf probe fixed/rebuilt/run PASS (baseline vs ORM, 10x10k) | /tmp/nextorm-D128-r2/perf.log
- 2026-10-06T01:06:00Z | DO | r=2 | n=1/3 | docs EN+RU updated (native JSON column; bare JsonObject; core scalar limitation; #128 body 16-json fixed) | docs/guide/14-json.md (+RU, providers, limitations)
- 2026-10-06T01:06:00Z | DO | r=2 | n=1/3 | B follow-up issue #198 created in 1.0.9-rc1, links #128 | https://github.com/AlexeyShirshov/nextorm/issues/198
- 2026-10-06T01:06:00Z | DO | r=2 | n=1/3 | hygiene: build 0/0; docfx exit 0 (2 pre-existing warnings); git diff --check clean; CRLF ok | /tmp/nextorm-D128-r2/build-final.log

## DO stream 1 — results (r=2, n=1)

### Driver probe (D128.D01, mandatory prerequisite) — PASS

- Driver `ClickHouse.Driver` `1.4.0.0`; server `25.8.33.6` (`clickhouse/clickhouse-server:25.8-alpine`); Testcontainers over the Podman socket.
- **Read (chosen accessor):** `GetFieldValue<JsonObject>(ordinal)` returns `System.Text.Json.Nodes.JsonObject` for a nested document (`{"nested":{"x":1},"age":30,"name":"alice"}`) and for an empty `{}`. `GetValue(ordinal)` also returns the same `JsonObject`, so the typed accessor is used (Postgres precedent, no boxing/cast).
- **Parameter (chosen mechanism):** `ClickHouseDbParameter { ParameterName = "p", Value = <JsonObject>, ClickHouseType = "JSON" }` with the ADO-style `@p` placeholder (driver rewrites to `{p:JSON}`). Inferred `toTypeName(@p)` = `JSON`; round-trips to a `JsonObject`. The driver also infers `JSON` from a `JsonObject` value alone (no explicit type); the explicit `ClickHouseType = "JSON"` makes the native binding independent of inference. `Value = string` binds as `String`, not JSON — the branch is therefore required for the native contract.
- Note: a nested empty `{}` value inside a non-empty document is not retained by ClickHouse's JSON dynamic storage (server behavior); an empty document column value reads back as `{}`.

### Implementation (D128.D02/D03) — only `src/nextorm.clickhouse/ClickHouseDataContext.cs`

- Added `GetFieldValueMI`/`IsDBNullMI` fields and a `MapColumnExpression` override for **exactly** `typeof(JsonObject)` **with no converter** (`column.Converter is null`, so `[JsonColumn]` keeps its storage policy): `MapJsonColumn` builds `GetFieldValue<JsonObject>` plus a defensive `IsDBNull` → `null` condition; every other type delegates to `base.MapColumnExpression`.
- Added a `JsonObject` branch in `CreateParam`: sets `parameter.ClickHouseType = "JSON"`; no literal interpolation. `CreateProcedureParameter`/TVP path unchanged.
- D128.R04 scope held: `SelectExpression.GetDataRecordMethod`, `JsonColumnConverter`, `SupportsJson`, `[JsonColumn]`/`Auto` and other providers untouched.

### Type-mapper applicability (D128.D04) — non-applicable

- roslyn `callers` of `NextORM.ClickHouse.ClickHouseDataContext.ColumnTypeName` (the only ClickHouse CLR→CH type mapper): 2 call sites, both in `CreateTableValuedParameter` (TVP, `:101`/`:109`). Temp tables use CTAS (`create table as select`), which has no CLR-side type mapper. No supported caller needs `JsonObject → JSON`, so it was **not** added (a `JsonObject` TVP column still fails fast with the existing `NotSupportedException`).

### Build (D128.D05) — PASS

- `dotnet build nextorm.slnx -c Debug` → exit **0**, **0 warnings**, **0 errors**. Log: `/tmp/nextorm-D128-r2/build2.log`.

### Evidence rows

- D128.E01: driver `1.4.0.0`, server `25.8.33.6`; nested read via `GetFieldValue<JsonObject>` = `JsonObject`. `/tmp/nextorm-D128-r2/probe-run2.log`.
- D128.E02: empty `{}` read via both accessors = `JsonObject {}`. `/tmp/nextorm-D128-r2/probe-run2.log`.
- D128.E03: parameter `Value=JsonObject` + `@p` → `JSON`; `Value=string` → `String`; round-trip read `JsonObject`. `/tmp/nextorm-D128-r2/probe-run2.log`.
- D128.E04: `src/nextorm.clickhouse/ClickHouseDataContext.cs` (override + `CreateParam` branch). `git diff`.
- D128.E05: `ColumnTypeName` callers = TVP only (roslyn). Non-applicable. `src/nextorm.clickhouse/ClickHouseDataContext.cs:101,109`.
- D128.E06: build exit 0, 0/0 (final incl. converter guard). `/tmp/nextorm-D128-r2/build2.log`.
- D128.E07: R01/R02/R03 satisfied by the proven accessor/mechanism and the override/branch; R04 provider-local scope; R05 base delegation; R06 native mechanism, no interpolation; R07 build 0/0.
- D128.E08: `git status --porcelain` → only `src/nextorm.clickhouse/ClickHouseDataContext.cs` (plus this status file). Core/other providers untouched.
- D128.E09: CRLF verified on the status file and `ClickHouseDataContext.cs`.
- D128.E10: RESOLVED in DO stream 2 — tests (R01–R03 acceptance/regression) and docs EN/RU delivered; option B (`Object('json')` alias, dialect `SupportsJson` integration) remains deferred to #198.

## DO stream 2 — results (r=2, n=1)

### Perf probe (D128.E11) — PASS (acceptance)

- Probe `/tmp/nextorm-D128-r2/perf` (Release), real ClickHouse container (server `25.8.33.6`, driver `1.4.0.0`), 10 runs × 10k rows: baseline (raw `ClickHouse.Driver` read) mean **68.07 ms / 2746.8 B/row**; candidate (nextorm bare-`JsonObject` projection) mean **69.61 ms / 2771.2 B/row** → **+2.3% time / +0.9% alloc**, acceptance PASS. Warmup result sums equal (`40000`/`40000`) — no divergence. Perf probe build `1 Warning(s)` (testcontainers obsoletion in the throwaway probe), `0 Error(s)`. Logs `/tmp/nextorm-D128-r2/perf.log`, `/tmp/nextorm-D128-r2/perf-build.log`.

### Docs EN+RU (D128.E12) — PASS

- EN: `docs/guide/14-json.md` (native-JSON section `:194`, provider matrix `:55`, tail note `:396`), `docs/guide/provider-specific/clickhouse.md` (`## Native JSON columns` + "Not yet supported" trimmed), `docs/providers/clickhouse.md`, `docs/providers/overview.md`, `docs/advanced/limitations.md`.
- RU: `docs/ru/guide/14-json.md` (anchor `#отобразить-clr-объект-на-нативную-json-колонку` `:195`), `docs/ru/guide/provider-specific/clickhouse.md` (`## Нативные JSON-колонки`), `docs/ru/providers/clickhouse.md`, `docs/ru/providers/overview.md`, `docs/ru/advanced/limitations.md`.
- #128 body stale `docs/guide/16-json.md` link fixed to `docs/guide/14-json.md` (verified via `gh issue view 128`).

### Follow-up issue variant B (D128.E13)

- [#198](https://github.com/AlexeyShirshov/nextorm/issues/198) "ClickHouse: native JSON support for [JsonColumn], JsonDocument and JsonElement" — OPEN, milestone 1.0.9-rc1, links #128.

### Hygiene (D128.E14) — PASS

- `dotnet build nextorm.slnx -c Debug` → `0 Warning(s)` / `0 Error(s)`; `/tmp/nextorm-D128-r2/build-final.log`.
- `dotnet docfx docs/docfx.json` → exit 0, `2 warning(s)` / `0 error(s)` (both pre-existing `AnalyzerReleases.*` duplicate-source warnings); `/tmp/nextorm-D128-r2/docfx.log`.
- `git diff --check` → exit 0, clean; CRLF verified on status/source/test files; artifacts mirrored under `artifacts/d128/`.

### Tests (D128.E15) — PASS

- `nextorm.clickhouse.tests` **509/509** succeeded, 0 failed, 0 skipped; new `ClickHouseJsonObjectMappingTests` **8/8**. Logs `artifacts/d128/clickhouse-tests.log`, `artifacts/d128/unit-json-mapping.log`.
- `nextorm.core.tests` **1548/1548** succeeded, 0 failed, 0 skipped. `artifacts/d128/core-tests.log`.
- Container integration **3185** total, **0** failed, **193** skipped; ClickHouse class **107/107**. `artifacts/d128/integration-full.log`, `artifacts/d128/ch-class2.log`.

## ACT (2026-10-06)

- CHECK: **PASS** — freeze point `r=2, n=1/3, rv=D128.r2.ec1`; all acceptance criteria D128.R01–R07 satisfied with explicit evidence (unit `ClickHouseJsonObjectMappingTests` 8/8; `nextorm.clickhouse.tests` 509/509; ClickHouse integration class 107/107; container integration 3185 total / 0 failed / 193 skipped; build 0/0 under `TreatWarningsAsErrors=true`; docs EN+RU; perf +2.3% time / +0.9% alloc).
- Commit plan: one D128 commit for `src/nextorm.clickhouse/ClickHouseDataContext.cs`, `tests/nextorm.clickhouse.tests/ClickHouseJsonObjectMappingTests.cs`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`, `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs`, the changed EN+RU docs (`docs/guide/14-json.md` + RU, `docs/guide/provider-specific/clickhouse.md` + RU, `docs/providers/clickhouse.md` + RU, `docs/providers/overview.md` + RU, `docs/advanced/limitations.md` + RU), and this status file; plus a separate collection bookkeeping commit for `docs/specs/status/collection-1.0.9-rc1.md`. Message `#128 ClickHouse native JSON column: bare JsonObject read/write`. No push, no merge, no `git add -A`.
- Disposition: provider-local bare `JsonObject` read+write (variant **A**); `[JsonColumn]`/`Auto` storage and `SupportsJson` unchanged; no shared `GetDataRecordMethod` branch. Variant **B** (dialect `SupportsJson` integration, `JsonDocument`/`JsonElement`/`Object('json')`) deferred to **#198**; variants C/D rejected/deferred.
- Follow-up: **#198** "ClickHouse: native JSON support for [JsonColumn], JsonDocument and JsonElement" — OPEN, milestone `1.0.9-rc1` (https://github.com/AlexeyShirshov/nextorm/issues/198).
- Issue outcome: **#128 closed** — bare `JsonObject` read/write over a native ClickHouse `JSON` column is supported.

`2026-10-06T06:15:00Z | ACT | r=2 | 1/3 | CHECK PASS (rv=D128.r2.ec1); disposition provider-local bare JsonObject read+write (variant A), B deferred #198; D128 change set committed; collection bookkeeping separate; #128 closed | this file, commit SHA in collection status`
