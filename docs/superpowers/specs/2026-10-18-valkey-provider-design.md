# Read-only Valkey provider — server-side queries

> **Status: written design APPROVED by user 2026-10-18; implementation plan NOT created and NOT approved; implementation NOT authorized. Next stage: a separate implementation plan.**
>
> Review history (preserved): architect resolutions from the independent review are incorporated; **independent builder-check B1–B5 resolved; minor clarifications incorporated.** The user approved this written design on 2026-10-18; approval covers the design only. No implementation plan exists and none was approved; implementation is not authorized. Design decisions below are unchanged from the reviewed draft. No implementation tests are claimed. Except where marked **AGREED** they are architect calls, not user approvals. This change edits only this markdown file (and posts an issue status comment): no product code, tests, dependencies, scaffolding, commits, push or merge.

| Field | Value |
|---|---|
| Task | [#310](https://github.com/AlexeyShirshov/nextorm/issues/310), **OPEN**, VERIFIED 2026-10-18 |
| Milestone | `1.0.10-a.1` ([#5](https://github.com/AlexeyShirshov/nextorm/milestone/5)), VERIFIED |
| Related | [#50](https://github.com/AlexeyShirshov/nextorm/issues/50), [#51](https://github.com/AlexeyShirshov/nextorm/issues/51), [#187](https://github.com/AlexeyShirshov/nextorm/issues/187) — context only, not prerequisites |
| Local path | `docs/superpowers/specs/2026-10-18-valkey-provider-design.md` |
| Stage | Written design **APPROVED** by user 2026-10-18; builder-check B1–B5 resolved; implementation plan NOT created / NOT approved; implementation NOT authorized (next stage: separate implementation plan) |
| External baseline | investigated 2026-10-18; Valkey Search 1.2.1 / core 9.0.1 (see §9) |

Labels: **FACT** — observed fact; **AGREED** — explicitly agreed in conversation; **DESIGN** — target contract after approval; **ARCHITECT CALL** — proposal for review, not a user approval; **PLANNING** — exact name/format/library left to the implementation plan.

## 1. Scope

### 1.1 Intent

Expose the existing NextORM fluent query surface, read-only, over externally populated Valkey HASH and JSON documents indexed by Valkey Search, including provider-native full-text search (FTS) and vector KNN. The initial request was feasibility; this is the design project. The provider is a query provider, not an ORM or a replacement for a Valkey client: writes, index creation and administration stay outside.

### 1.2 Explicitly agreed (AGREED)

1. Separate Valkey context; not a SQL `DataContext`, not a fake SQL dialect.
2. Both HASH and JSON are supported as read sources.
3. No transactions, snapshot or read-transaction semantics.
4. v1 includes existing standard fluent operations, including grouping/aggregates, plus FTS and vector search.
5. Server-side execution only: user JOINs, subqueries and untranslatable expressions are rejected; client-side filtering/sorting/grouping/joining is forbidden.
6. Valkey Search numeric **double** semantics are accepted; precision loss for `decimal` and large `long` is documented.

### 1.3 Scope requirements (DESIGN; from the task brief, not separate approvals)

No write path; indexes/data maintained externally; no silent `FT.SEARCH LIMIT 0 10` truncation; real-Valkey tests; EN/RU user docs; concurrent pagination is not snapshot-consistent; ordinary `string.Contains` does not become FTS; `FT.HYBRID` and vector range are not promised; baseline is stable Valkey Search 1.2.1.

### 1.4 Non-goals

Writes/insert/update/delete/upsert/mutations/`Returning`/DDL; index create/drop/rebuild/migrations; transaction API; JOINs, correlated/uncorrelated subqueries, navigation/eager-loading; SQL/raw SQL, stored procedures, temp tables, batches requiring writes; window/set operators, `Distinct`, `GroupByRollup`/`Cube`/`GroupingSets`; client computational fallback; transparent relational equivalence; mandatory 1.3-RC features; server snapshot/read transactions.

## 2. Architecture

### 2.1 Package and context (ARCHITECT CALL)

- `src/nextorm.valkey/nextorm.valkey.csproj`, `PackageId` `nextorm.valkey`, namespace `NextORM.Valkey`; references `nextorm.core`, no SQL provider.
- `public sealed class ValkeyDataContext : IDataContext`, directly implementing the roles, following `InMemoryDataContext`; not a `NextORM.Core.DataContext` subclass. It implements `IQueryExecutor`, `IQueryMaterializer` (`IQueryPlanner` + `IRowReaderFactory`), `IQueryCache`, `IContextEnvironment`, `IRawCommandExecutor`, `IDisposable`/`IAsyncDisposable`.
- All `IRawCommandExecutor` methods throw a distinct `NotSupportedException` (raw SQL/procedure, read-only provider). No `ISqlDialect`, `IConnectionManager`, `DbConnection`/`DbCommand` or fake capability flags; the support matrix is built from implemented operations only.
- Dedicated translator, immutable server plan/prepared command, RESP executor and materializer. Do not copy/rewrite the SQL planner and do not make a broad core refactor.

### 2.2 Core reuse and access (ARCHITECT CALL)

Reuse provider-neutral contracts: `QueryCommand.PrepareCommand` and prepared state, `SelectExpression`, `Sorting`, `Paging`, `FromExpression`, `DataContextCache.Metadata`, `IEntityMetadata`/`IPropertyMetadata`, read-direction `IPropertyValueConverter`, and the public terminal/enumerable roles. Do not reuse `SqlBuilder`, `QueryPlanner`, `QueryExecutor`, `DbPreparedQueryCommand`, `RowMapperFactory` or the `IDataRecord` pipeline.

The seam is not assumed complete. Minimal **internal** access/IVT is allowed where required, listed as deliberate plan changes, not magically public API:

- internal getters for query flags/state (`LoadWith`/eager-load shape and mode, locks, `WithTies`, `LimitBy`, `Sample`, table modifiers/options, `IgnoreColumns`/projection visibility);
- a provider-neutral optional capability interface (default preserves existing providers; exact name PLANNING) with `SupportsEagerLoading=false`. All eager-loading builder entry points check it **before** the parent data query/materialization and before child commands (metadata-only probes allowed), throwing `NotSupportedException` naming `LoadWith`/split/single. Tests assert neither parent nor child data command runs. This hook is needed because `LoadWith` split execution happens outside `IDataContext` after `ToList`; `EagerLoadShape` alone is not an executor/guaranteed single flag;
- the same preflight validates unsupported query options/flags through narrow internal getters/IVT; this is a narrow seam, not a broad rewrite;
- marker-expression feasibility is checked in the plan; no new core marker state carrier unless the expression approach is disproved.

Prepared query metadata must preserve invalid/unsupported shape until validation; do not silently drop or ignore flags during preparation.

### 2.3 Pipeline

1. Existing core `PrepareCommand` normalizes the query; do not change its semantics.
2. Resolve explicit entity → Valkey index mapping.
3. Readonly schema probe (`FT.INFO`/equivalent) and version/module check.
4. Full capability validation before any data/search command; invalid shape remains visible here.
5. Build immutable server plan + parameter slots; cache by shape.
6. Execute `FT.SEARCH` or `FT.AGGREGATE` (sequential windows for unbounded results, §6.2).
7. Decode and materialize locally; converters/deserialization are the only local computation.

No partial search followed by client "fix-up" or a fallback query.

### 2.4 Cache and parameters

- Per-context, immutable shape-plan cache. Key includes mapping identity, index, schema fingerprint, normalized query shape, marker state (field/dim/K/metric), result/materializer shape. Parameter values never enter the key; results are never cached.
- Values bind per execution. Never mutate shared `QueryCommand` state; especially never set `QueryCommand.Cache = false` on a shared command (sticky `_dontCache`).
- `IQueryCache.AnyCommand` is used for `Any` only. `Count`/`LongCount` are not shared commands; they compile their own prepared aggregate pipeline.
- `PurgeQueryCache()` clears shape plans, schema snapshot and `AnyCommand`. `QueryCacheEnabled=false` means `storeInCache:false` per call without mutating the command.
- Schema snapshot is per-context with explicit refresh; a plan carries its fingerprint and fails with a refresh error on mismatch. No guessing on server errors.

### 2.5 Capability/observability seam (DESIGN)

All rows are checked **before data execution**; where a core hook is needed it is the narrow check of §2.2.

| Construct | Detection | Timing |
|---|---|---|
| User JOIN/subquery/navigation | translator capability validation | before data |
| Synthetic `Any` `exists(subquery)` | trusted per-context `AnyCommand` provenance + exact `exists` `MethodInfo`/shape | lowered to one-index existence probe, not generic subquery execution |
| Raw SQL/procedure | translator / `IRawCommandExecutor` role | before data |
| Writes/mutations/Returning/DDL | query shape/role validation | before data |
| `LoadWith` (split/single) and navigation expansion | provider-neutral `SupportsEagerLoading=false` check before parent data query/materialization and child commands + translator | before command result, both modes; no parent/child data command |
| Locks, `WithTies`, `LimitBy`, `Sample`, table modifiers/options | prepared flags/internal state | before data |
| FTS/vector unsupported combinations | marker validation | before data |
| Unsupported `Last` lowering | translator | before data |

No row may be satisfied by a client fallback. `IgnoreColumns`/internal projection visibility is used for `EXISTS`/`NOCONTENT` so existence probes do not load fields.

## 3. Mapping

### 3.1 External index, HASH and JSON

Index schema, key prefix/filter, population and updates are external. The provider never calls `FT.CREATE`, `FT.DROPINDEX`, `FT.ALTER`, reindex/backfill or migrations. HASH values are hash fields; JSON values are JSON paths. HASH absence and JSON `null`/missing are distinct states. JSON mapping requires a compatible JSON module; its version is verified in the plan.

### 3.2 Mapping configuration (DESIGN; names PLANNING)

Explicit, typed, provider-owned configuration; never inferred solely from CLR types. Minimal model: `IndexName` (existing Search index); `StorageKind` (`Hash`/`Json`); `KeyMode` (returned key, stored field/path, or `None`); `NullSemantics` (HASH absent; JSON null or missing); `Fields` (property → field map); `Alias` (index alias/attribute used in queries); `StoragePath` (hash field or JSON path used to load); `Kind` (`Numeric`/`Tag`/`Text`/`Vector`); `Loadable`; `Sortable`; `TagOptions` (separator/case/multi-value from schema); `Vector` (dimension, metric, algorithm, element type).

`Sortable` is a requirement, not a server assertion: `FT.INFO` is the source of truth. Multiple entity configs may explicitly share one index; one query uses exactly one index. Key-prefix/filter is external and never injected silently.

### 3.3 Identity

No inference that key payload equals Id. Explicit modes: returned key mapped via converter; stored field/path loaded; or no identity. Grouped queries reject identity projection unless a verified server plan exposes it.

### 3.4 Schema validation

Readonly `FT.INFO`/equivalent validates index existence, storage kind, alias/path, kind, sortable/loadable flags, vector dimension/metric/algorithm/element type; `MODULE LIST`/equivalent checks the module/version. Missing/mismatch yields an actionable error naming index/field/property. If `FT.INFO` is unavailable or denied, fail as a schema/configuration error; no blind search and no key-scan fallback. No index mutation.

### 3.5 Null, converters, numeric

- HASH: absent field → CLR `null` when declared; empty string is not null. JSON: JSON null and missing path are separate; mapping chooses one. `== null` is translated only when that representation is expressible server-side; otherwise reject at validation.
- TAG multi-value/case/separator semantics come from the schema and are tested. Exact CLR equality is allowed only for compatible schema semantics; otherwise use an explicit native predicate.
- Reuse `ConvertFromProvider`. Supported provider-side types are fixed in the plan (`string`, `byte[]`, `long`, `double`, `bool`); others are configuration errors.
- Dates/enums have explicit representations: epoch seconds/milliseconds (NUMERIC) or ISO string (TAG/TEXT). No automatic `ToString()`/ordinal; numeric range/sort only for numeric representation.
- `decimal`/`long`: raw stored reads are exact where the stored form allows; search predicates, sorting and aggregates use double. No exact-long/decimal claims; >2^53 may lose precision. `.JsonColumn` SQL serialization is not a JSON-path mapping.
- Arbitrary methods are not accepted; only the projection allowlist of §4.1.

## 4. Query surface

### 4.1 Projection

Support what the existing core materializer supports: whole mapped entity, scalar member/constant, server numeric expression, and flat anonymous/DTO constructor/member-initializer shapes. Do not promise every nested initializer, tuple or custom constructor.

Projection may also include the explicit typed score/distance markers from §5; they are query artifacts, not entity fields. All selected members must be server-loadable and deserializable. A vector payload is readable only when an explicitly configured stored field representation can be returned/loaded losslessly by the server (HASH blob or JSON numeric array), using a typed vector decoder for that configured representation; do not confuse the indexed vector's internal representation with source storage or cast generically from double. Otherwise fail **before execution** and require a smaller projection unless an existing explicitly mapped excluded property is proven. An entity containing a vector field is not a blanket contradiction: projecting the whole entity requires the vector to be loadable; otherwise use an explicit smaller projection. No arbitrary client selector execution.

Allowed server expression: numeric `+ - * /`, unary minus and parentheses over mapped NUMERIC fields and constants, via `FT.AGGREGATE APPLY`, with double semantics. Computed numeric projection to `int`/`long`/`decimal` uses explicit checked conversion and remains documented as double semantics.

### 4.2 Where

NUMERIC: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, server boolean composition, constants/parameters. TAG: exact equality and `in`, using schema multi-value semantics. Null only for an expressible representation. TEXT/VECTOR only through typed FTS/KNN APIs.

Reject: TEXT `==`/`Contains`/`StartsWith`/`EndsWith` without an explicit FTS API; predicates on non-indexed fields; incompatible representations; arbitrary methods; SQL functions other than the recognized core scalar artifacts (`exists`, `count`, `count_big`).

### 4.3 Sorting

Repeated `OrderBy`/`OrderByDescending` appends keys (the task's `ThenBy` term; there is no separate builder method). One key uses `FT.SEARCH SORTBY` and requires `SORTABLE`; multiple or computed keys use `FT.AGGREGATE LOAD` + `SORTBY n ...` when representable. No global client sorting.

### 4.4 Paging, First, Single, Last (DESIGN; core contracts preserved)

- Existing fluent paging is `Offset`/`Limit`/`Page`, not LINQ `Skip`/`Take`. `Limit(0)` means **unbounded**, not an empty page.
- Do not invent new LINQ behavior and do not fix core paging bugs here. The translator consumes the final prepared paging.
- Core `First` overwrites limit with 1; `Single` uses limit 2 preserving offset; `Last` reverses the sort then calls `First`. Match these contracts.
- `Last` is not rejected merely because it is unobservable: accept the core-lowered reversed-sort + `First` form when the server order can represent it (ordinary fields need reversed order; KNN needs the same top-K ordering without changing candidate selection). If not representable, reject **before data**; never reverse client-side.
- `Limit(0)` with offset means unbounded-after-offset: page until the verified end.
- Unbounded enumeration must return the full logical result for **quiescent** data; see §6.2 for concurrent writes and resource/capability rejection.

### 4.5 Terminals, Any, Count, LongCount

- Use the existing enumerable/async terminal roles from `IRowReaderFactory` and the current public sync/async query pairs. Do not invent a parallel terminal API. Async naming follows `AGENTS.md` (suffix only with a sync twin). JSON/CSV export streaming APIs are explicitly **OUT of v1** and throw a provider-specific `NotSupportedException`; enumerable/async query materialization remains supported. Do not promise universal stream exports.
- `First`/`FirstOrDefault` and `Single`/`SingleOrDefault` follow the core limits above and materialize only the requested rows. `Single` detects a second row by reading it, not by trusting a total.
- `Any` is mandatory, but only for the trusted core-produced terminal artifact: per-context `IQueryCache.AnyCommand` origin/provenance plus the exact known `exists` `MethodInfo`/shape. Structural resemblance alone is insufficient; arbitrary user `exists`/subquery is rejected. The plan verifies the artifact survives preparation/cloning; if provenance is inaccessible, add a minimal neutral provenance/hook. Lower it to a one-index existence probe (`LIMIT offset 1`, `NOCONTENT`) without generic subquery execution. Include a negative test for a user-authored `exists`/subquery.
- Ordinary **unpaged scalar Count** is mandatory. `CountCore`/`LongCountCore` use `SelectParent(count/count_big)` and copy paging; compile the same semantic SQL stages (aggregate pipeline, then prepared paging), **not** a `min(total, limit)` guess. This preserves existing core behavior and does not promise LINQ `Count`-of-paged-sequence semantics.
- Count/LongCount over offset/grouped scalar shapes: verify cross-provider semantics in the plan and tests. Where existing core behavior is undefined, keep an explicit `NotSupported` for that combination until documented deliberate semantics; no guessed totals.
- Grouped scalar `Count` after `GroupBy` (count groups vs count the first group's rows) is not promised without contract proof; an ambiguous form may be rejected, but `.GroupBy(...).Select(group => group.Count ...)` and group projections are mandatory.
- KNN cardinality is different: `Count`/`LongCount` on KNN are rejected in v1 because the native total is not top-K cardinality and no faithful server count path is verified. A typed provider capability exception documents the rejection. Any/First/Single/ToList on KNN follow §5.5.

### 4.6 Grouping and aggregates

Server `GroupBy` over mapped NUMERIC/TAG keys; `Count`, `Sum`, `Min`, `Max`, `Average` through `GROUPBY`/`REDUCE`; `Having` as post-aggregation `FILTER` over reducer aliases/constants; server sort/page after grouping. `.GroupBy(...).Select(group => group.Count ...)` and group projections are mandatory. Numeric aggregates use double; no exact decimal/long promises.

Reject `GroupByRollup`/`Cube`/`GroupingSets`, `Having` without grouping, arbitrary C# expressions, reducers outside the list, KNN+group, and ambiguous grouped scalar Count (§4.5).

### 4.7 Explicitly rejected

User JOIN/`JoinInto`, correlated/uncorrelated subqueries, navigation expansion, `SelectMany`, `GroupJoin`, set operators, window functions, `Distinct`, raw SQL/`PrepareFromSql`/`FromSql`, temp tables, batches, mutations/Returning, raw commands, `LoadWith`/eager loading, table/schema/database/server modifiers (index is mapped), locks/`ForUpdate`/`ForShare`, `WithTies`, `LimitBy`, `Sample`, provider SQL modifiers. Errors name the construct and reason; unsupported shape is not silently ignored.

## 5. Full-text and vector

### 5.1 Typed marker model (ARCHITECT CALL)

Provider-native FTS/vector APIs live in `nextorm.valkey` and are represented as **marker `MethodCallExpression`s** in the copied/hashable expression trees (condition, sorting, projection). This is required because `CustomData` is not copied by `SelectCore`/`ToCommand`; markers must survive select, `ToCommand`, terminals and cloning. Marker `MethodCallExpression`s in `Condition`/`Sorting` are copied and hashed automatically by `MethodInfo`.

Marker state includes field/index reference, dimension, K, metric and vector parameters. It must survive the query shape and participate in shape-aware cache keys; vector bytes bind per execution and are never baked into a cached plan/result. Recognize markers structurally by `MethodInfo`; never arbitrarily evaluate marker methods. Marker survival is a mandatory early plan verification through the full `PrepareCommand` (normalizers/navigation/converters), not just copy tests. If markers cannot survive, a minimal neutral core extension seam is allowed as a documented plan decision; no state-bag/`CustomData` fallback. No implementation spike is authorized now. No `Dictionary<string, object>`/`dynamic`/state bag.

### 5.2 FTS

Typed term, phrase, prefix and boolean `AND`/`OR`/`NOT`; field-scoped (field required; “all TEXT fields” only if the server proves it). User text is escaped by the typed AST; it is never inserted as arbitrary vendor syntax. Pure `NOT` without a positive anchor is rejected if the grammar requires it. Fuzzy/suffix/wildcard beyond prefix are not in v1. Tokenization/stemming/case follow the index schema, not .NET string comparison. `string.Contains` does not become FTS; ordinary TEXT equality is not translated.

### 5.3 Score

FTS score comes from `WITHSCORES` as a typed `double` marker, not an entity property. Baseline 1.2.1 does not promise BM25 equivalence; scorer/scale are module-defined and may change. No client normalization or re-sorting. Score+GROUP BY and score+KNN are rejected in baseline (would need 1.3-RC `ADDSCORES`/`FT.HYBRID`).

### 5.4 KNN

Vector dimension/metric/algorithm/element type come from schema validation. Input is a typed vector (e.g. `ReadOnlyMemory<float>`), length equal to dimension, non-finite values rejected; float32 byte order is confirmed by baseline docs/tests (little-endian on target x86). K is positive and bounded; the result is top-K by distance. Distance is a typed metric-specific marker; no client recomputation.

Filter + KNN is one `FT.SEARCH`: top-K **inside the filtered set**, not global top-K then filter. This is verified live; if baseline behaves otherwise, the combination is rejected, not emulated. It is not `FT.HYBRID`: distance order only. Conflicting `OrderBy` fields are rejected.

### 5.5 KNN terminals and Last (ARCHITECT CALL)

KNN defines a **bounded top-K source** with optional server filters. All terminals, including `ToList`/`ToListAsync`, use that same bounded source: for offset `o` and explicit positive limit `L`, `remaining=max(0,K-o)` and `requested=min(L>0 ? L : remaining, remaining)` (because `Limit(0)` means unbounded, not empty). `First`/`Any` cap at 1, `Single` caps at 2. A terminal changes only the retrieval count, never K or candidate selection. If `remaining=0`, issue no data request and materialize the empty result with the existing terminal semantics; this is client resource handling, not a computation fallback.

- `Any`/`First`/`FirstOrDefault` use the native KNN page; no aggregate needed.
- `Single`/`SingleOrDefault` retrieve up to 2 rows **within the bounded K top results**, clamped to the remaining K after offset; K=1 may naturally succeed; no K≥2 requirement. Cardinality is exactly within the bounded top-K set, not across all index matches.
- `Last` is conditional: allowed only if the same bounded top-K set and server ordering are representable without changing candidate selection; otherwise reject. No client reverse.
- `Count`/`LongCount` rejected v1 (§4.5). KNN+group/aggregates rejected. Unsupported combinations are rejected unless a verified server mapping exists.

### 5.6 Excluded baseline features

`FT.HYBRID`, vector range, cursors, `BM25STD`, `FLOAT16`/`BFLOAT16`, multi-language, `ADDSCORES`, `HYBRID_POLICY`, `INKEYS` and other 1.3-RC features are outside baseline. A newer server does not auto-enable them.

## 6. Execution and paging

### 6.1 Command selection

| Shape | Command |
|---|---|
| filter + member projection + one sort + paging | `FT.SEARCH` (`SORTBY` needs `SORTABLE`) |
| FTS score / KNN distance / KNN filter | `FT.SEARCH` |
| multi-sort, computed projection | `FT.AGGREGATE` (`LOAD`+`APPLY`+`SORTBY`) |
| GROUP BY/reducers/HAVING/sort/page | `FT.AGGREGATE` |
| Any/Count/LongCount | `FT.SEARCH` or `FT.AGGREGATE` per prepared shape |
| unbounded ordinary result | sequential server windows (§6.2) |

If no baseline command represents the shape, reject; never strip a stage or replace it client-side. `FT.SEARCH` always gets an explicit `LIMIT`; its default 10 is never “all”. `RETURN` is the `FT.SEARCH` form; `LOAD` is the `FT.AGGREGATE` form. Do not load vector fields unless explicitly required.

### 6.2 Unbounded paging guarantees (ARCHITECT CALL)

- The guarantee is: **all matching logical rows for quiescent data**. Concurrent external writes may cause omissions/duplicates between offset pages; this is explicitly not snapshot-consistent and the provider does not claim to detect it. Baseline 1.2.1 uses `LIMIT` paging; no cursor is mandatory.
- Never stop merely because of the search default 10 or a server cap. Validate server max/caps and range ability via readonly config/metadata or documented configuration. If safe enumeration without truncation is impossible, reject with a clear capability/resource error; never return a partial result as complete.
- Use the actual page row count; advance offsets by actual rows. Stop on an explicit user bound or a verified end (for example an empty page under supported limits). A short page may be EOF under a verified no-hidden-cap policy, but do not assert that it detects concurrent loss.
- Materializing one bounded reply for decoding is not client counting; client filtering/sorting/grouping/deduplication is forbidden. Do not materialize all keys to replace a missing stage.

### 6.3 Group totals

`FT.SEARCH` total is an unpaged match count, not a group count. Never treat the first `FT.AGGREGATE` field as a group total without live proof. Ordinary grouping enumeration must page all rows. Grouped COUNT uses the server reduce pipeline when supported and unambiguous; otherwise reject explicitly. No client counting of all groups fallback.

### 6.4 Consistency, cluster, timeout

No transactions/snapshot/repeatable read/locks; index visibility follows Valkey Search. Cluster is not advertised until separately proved (routing, fan-out, response consistency, version); v1 correctness scope is single-node, and cluster configuration is rejected until then. Index name is not a key and must not be used for ordinary slot routing. Timeouts/cancellation propagate to the transport; no partial result is presented as complete.

## 7. Transport and errors

### 7.1 Library (PLANNING)

Exact .NET library is delegated to the implementation plan. Architect preference: official **Valkey.Glide**, if evidence confirms public arbitrary-command APIs for `FT.SEARCH`/`FT.AGGREGATE`, net10/Linux/Windows runtimes, and cluster search routing. A StackExchange.Redis-compatible alternative is only an evidence-based plan decision, not an approved dependency. Cluster routing is required only if cluster is advertised; v1 is single-node. This is an architect proposal visible for review.

### 7.2 Adapter and protocol

Internal transport adapter isolates library types. Typed RESP2/RESP3 decoding: null vs empty bulk,
null vs empty array, maps/sets/doubles/booleans, error replies; `FT.SEARCH` `[total, key, fields...]`,
`WITHSCORES` `[total, key, score, fields...]`, `NOCONTENT` `[total, key...]`; `FT.AGGREGATE`
`[total, [field, value...], ...]` (form verified by tests). Missing fields are a distinct marker, not
`""`/`DBNull`; duplicate fields are an error. Numbers/scores/distances parse invariantly; integers
checked. Cancellation, caller-owned vs context-owned connection lifetime, and idempotent disposal
follow repo conventions. No mock `DbConnection`; unit tests use a scripted transport, integration tests
use real Valkey.

### 7.3 Encoding and parameters

One grammar-aware encoder; never concatenate unescaped literals. Separate encoders for TAG, term, phrase, prefix, numeric, field/index names. Fields/indexes come from validated schema/mapping and are separate argv elements. Offsets/limits/K are range-checked without overflow. `PARAMS` is used only for roles verified on the baseline; otherwise the literal encoder is used. Vector bytes bind per execution.

### 7.4 Errors

Configuration/schema errors, unsupported query/role errors, protocol/server errors and cancellation are distinct; all name index/field/property/operator and the remedy. Raw SQL/mutations get separate `NotSupportedException` messages. No client fallback is suggested or executed. Do not log parameter values, vectors or credentials by default; `LogSensitiveData` controls diagnostics.

## 8. Baseline, capability matrix, testing, docs

### 8.1 Baseline (ARCHITECT CALL; external facts 2026-10-18)

Valkey core ≥ 9.0.1 and Valkey Search 1.2.1 (latest stable, requires core ≥ 9.0.1); JSON mapping requires a compatible JSON module, version verified in the plan. 1.3.0-rc1 adds the RC features of §5.6; they stay out. Numeric double semantics is AGREED; the 1.2.1 baseline and library choice are architect proposals for review.

### 8.2 Capability matrix

| Capability | v1 |
|---|---|
| HASH/JSON mapping, schema validation | yes |
| NUMERIC/TAG predicates, sort, aggregates | yes (double) |
| TEXT FTS term/phrase/prefix/boolean; FTS score | yes, baseline-verified |
| Vector KNN, filter+KNN, distance | yes, baseline-verified |
| Typed score/distance projection | yes (explicit markers) |
| Vector payload read (configured HASH blob / JSON numeric array) | yes, typed decoder |
| KNN ToList/ToListAsync | yes, bounded top-K rules |
| KNN Any/First/Single | yes, bounded top-K rules |
| KNN Last | conditional: same bounded set/order, else reject |
| KNN Count/LongCount/group | **no** (explicit reject) |
| JSON/CSV export streaming | **no** (provider-specific `NotSupportedException`; enumerable/async materialization stays) |
| Multi-sort, grouping, HAVING, reducers | yes, server-representable |
| Offset/Limit, quiescent unbounded paging | yes |
| Ordinary unpaged scalar Count, Any | yes |
| Count/LongCount paged/grouped edge cases | only with verified core semantics; otherwise explicit reject |
| Writes/index management/transactions | **no** |
| User JOIN/subquery/raw SQL/LoadWith | **no** (trusted core `Any` artifact recognized) |
| FT.HYBRID/vector range/cursors/BM25STD/FLOAT16 | **no** (1.3 RC) |

Unsupported-until-verified: phrase/prefix/NOT, sortable TAG/TEXT, non-indexed `LOAD`/`RETURN`, JSON path/null semantics, `PARAMS` roles, score/distance wire format, cluster routing, aggregate group totals, filter+KNN semantics, `FT.INFO` metadata. If verification fails, reject; never emulate.

### 8.3 Testing (DESIGN)

Unit: translation to argv (`FT.SEARCH`/`FT.AGGREGATE`, filter/projection/sort/group/having/limit, `NOCONTENT`, `WITHSCORES`, KNN); escaping/injection; RESP2/RESP3 decoding; capability validation with zero transport calls; marker survival through the full `PrepareCommand` (normalizers/navigation/converters) plus select/`ToCommand`/cloning/cache keys and per-execution vector binding; Any provenance and a negative user-authored `exists`/subquery; eager-load preflight asserting no parent/child data command; KNN remaining/offset/empty/ToList formulas; mapping/null/precision; no hidden client execution; sync/async parity; cancellation; limit overflow.

Integration: real Valkey + Search + JSON with pinned versions; HASH and JSON externally populated;
indexes created only in test setup, never by the provider; no InMemory. Live single-node mandatory;
cluster only if advertised. Cover accepted semantics and negative capability cases: filters,
projection, multi-sort, paging, quiescent full result >10 and >one window, no silent truncation,
ordinary Count/Any, First/Single 0/1/2, `Last` accepted lowered form, group projections/reducers,
key/null/converters, large long/decimal precision, cache/parameter isolation, rejected
join/subquery/raw SQL/mutation/LoadWith before data, FTS, KNN ToList/offset/remaining-zero,
distance/dim mismatch/filter+KNN, K=1 Single, typed score/distance projection, vector payload read
from configured HASH blob/JSON numeric array (and rejection of unsupported source representation),
negative user `exists`/subquery, sync/async/cancellation. Do not assert unstable tie order. Build net10 with warnings as
errors, FluentAssertions, CRLF; do not claim the existing integration coverage gate green or lower
thresholds. New Valkey real-DB tests are nonoptional; the full existing suite is not mandatory beyond
affected contracts.

### 8.4 Docs

EN/RU provider page and a numbered guide page (next free number; currently 33) plus mirrors; update provider overview/toc/limitations and XML docs. Cover configuring a pre-existing HASH/JSON index, supported operations/restrictions, double semantics/precision, no snapshot/transactions, module versions, FTS/vector samples, mapping/schema errors and read-only operation. Public docs must not link to internal specs.

### 8.5 Acceptance and gates

- [x] User approved this written spec on 2026-10-18; builder-check B1–B5 resolved and minor clarifications incorporated.
- [ ] Separate implementation/test plan created (**not created as of 2026-10-18**) and implementation explicitly authorized (**authorization NOT given**).
- [ ] Mapping/schema validation; HASH+JSON; null/missing; converters.
- [ ] Projection/where/order/paging/terminals aligned with existing core contracts; no new LINQ.
- [ ] Quiescent unbounded paging complete; capability/resource rejection instead of partial.
- [ ] Any trusted `AnyCommand` provenance recognized; ordinary Count/Any and group projections; edge cases verified or explicitly rejected.
- [ ] FTS/KNN accepted semantics live-tested; KNN bounded top-K terminals incl ToList; KNN Count/group rejected precisely.
- [ ] Typed score/distance projection and vector payload read rules tested.
- [ ] LoadWith/joins/subqueries/raw SQL/writes rejected before data; eager-load preflight runs no parent/child data command.
- [ ] Unit + real-Valkey tests; negative capability cases; no InMemory.
- [ ] EN/RU docs; CRLF; warnings-as-errors; coverage thresholds unchanged.

## 9. Evidence pointers (FACT; line refs are current-source snapshots; no build/tests run in this change)

- `IDataContext.cs:14-27` — role composition; connection deliberately excluded.
- `InMemoryDataContext.cs:13-156` — direct `IDataContext`, per-instance caches.
- `DataContext.cs:76-116`; `QueryPlanner.cs:575-576`; `QueryExecutor.cs:628-635` — SQL context bound to DbConnection/SqlBuilder/DbPreparedQueryCommand.
- `QueryCommand.cs:513-541`; `QueryCommand.Prepare.cs:10-17` — prepared state and public preparation.
- `EntityBuilder.cs:2249-2283` — `Limit`/`Offset`/`Page`; `Paging.cs:16-49` — `Limit(0)` unbounded.
- `EntityBuilder.cs:4116-4129` — repeated `OrderBy` appends keys (no separate `ThenBy`).
- `EntityBuilderExtensions.cs:293-325`, `:333-344`, `:941-1015` — AnyCore/AnyCommand synthetic exists; Count/LongCount scalar artifacts and paging.
- `AGENTS.md:36-37` — shared AnyCommand/sticky `_dontCache` warning.
- `IEntityMetadata.cs:10`; `SelectExpression.cs:20-55`; `Sorting.cs:33-53`; `IPropertyMetadata.cs:118-127`; `ValueConverter.cs:13-40` — entity/projection/sort/converter contracts.
- `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md` — provider API conventions only, not Valkey approval.
- External, checked 2026-10-18: https://valkey.io/topics/search/ ; https://valkey.io/topics/search-query/ ; https://valkey.io/commands/ft.search/ ; https://valkey.io/commands/ft.aggregate/ ; https://valkey.io/topics/search-data-formats/ ; https://github.com/valkey-io/valkey-search/releases ; https://glide.valkey.io/commands/supported-commands/ .
- 1.2.1 is latest stable, core ≥9.0.1; 1.3.0-rc1 (2026-10-06) adds BM25STD/FT.HYBRID/vector range/cursors/FLOAT16/BFLOAT16/multi-language. No test/build result is claimed here; core semantics caveats above are design constraints, not verified test outcomes.
