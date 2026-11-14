# PDCA PLAN — Campaign `issues-65-69`, Step 2 (#69), Cycle `N=7, r=1, n=0`

1. **Form, overlay & scope**: `incremental` + `coder+.NET` (by reference from campaign start: client-side per-scope command composition over `WithScope(MappingScope)` and `ShardTopology` is a justified method with bounded Phase 3 volume). Anchor: `055a4a93` on `feat/65-69-mapping-scope`.
   - **Requested / delivered result**: Phase 3 of #69 (client-side `AcrossShards` fan-out, live DB integration coverage, EN/RU documentation + PublicAPI, acceptance benchmark) and whole-step closure of #69.
   - **Prior criteria (`met` in N=6, preserved unchanged)**: `AC-69-RING`, `AC-69-META`, `AC-69-ROUTE-DML`, `AC-69-ROUTE-READ`, `AC-69-FAILFAST`, `AC-69-PARITY`, `AC-69-HYGIENE`.

2. **`AC-69-FANOUT` (Client-side cross-shard fan-out)**:
   - Observable surface: `AcrossShards()` (all topology shards in deterministic `ShardTopology.ShardIds` order) and `AcrossShards(params string[] shardIds)` / `IEnumerable<string>` (explicit subset; unknown/empty/null shard ID fails fast with `ArgumentException`/`InvalidOperationException`), composing per-shard `MappingScope` commands via `Where`, `Select`, and terminal materializers (`ToListAsync`/`ToArrayAsync`/`AsAsyncEnumerable`) that merge results across the target shards.
   - Negative condition: Global cross-shard `ORDER BY`, `LIMIT`/`OFFSET` (`OrderBy*`, `ThenBy*`, `Take`, `Skip`), `DISTINCT`, and terminal aggregates (`Count*`, `Any*`, `All*`, `Sum*`, `Min*`, `Max*`, `Average*`, `Single*`, `First*`) on a fan-out query throw `NotSupportedException` fail-fast before execution. New sharding types go in a subfolder under `src/nextorm.core/Sharding/` so direct `.cs` files in `Sharding/` remain ≤ 10.

3. **`AC-69-LIVE` (Live database integration)**:
   - Live integration tests in `tests/nextorm.integration.tests` (exercising real DBMS connections: SQLite + container-backed providers per `ProviderTestSuite`, zero `InMemoryDataContext`, respecting `AssertionStyleTests` and `IntegrationSuiteScopingTests`) verify physical per-shard table/schema creation, deterministic ring distribution across ≥ 3 shards, routed point `SELECT`/`INSERT`/`UPDATE`/`DELETE`, fail-fast unroutable queries, and `AcrossShards` collection (all-shards + subset). Container-backed suite must pass under the canonical capped run with zero unauthorized provider skips.

4. **`AC-69-DOCS` (Documentation & PublicAPI)**:
   - New guide `docs/guide/33-sharding-and-horizontal-partitioning.md` and Russian mirror `docs/ru/guide/33-sharding-and-horizontal-partitioning.md`, updated `toc.yml` in both `docs/guide/` and `docs/ru/guide/`, and v1 cross-shard limitations recorded in `docs/advanced/limitations.md` and `docs/ru/advanced/limitations.md` (no links to `docs/specs/**`, DocFX builds clean). Every new public type/member is declared in `src/nextorm.core/PublicAPI.Unshipped.txt`.

5. **`AC-69-BENCH` (Acceptance benchmark & measurement method)**:
   - Metric/unit: mean latency (ns/op) and allocated bytes/op via BenchmarkDotNet (`benchmarks/nextorm.benchmark`, Release mode) comparing non-sharded query, single-shard routed query (`WithShardKey`/predicate), and `AcrossShards` fan-out on the same host/runtime.
   - Threshold: single-shard routing and `AcrossShards` execute with zero exceptions and no regressions in non-sharded query path; results recorded in cycle evidence.

6. **Step-closure rule & handoff**:
   - Final whole-step CHECK combines N=7 criteria (`AC-69-FANOUT`, `AC-69-LIVE`, `AC-69-DOCS`, `AC-69-BENCH`) with whole-step regression (`AC-69-RING`..`AC-69-HYGIENE` still intact, `TreatWarningsAsErrors=true`, CRLF, ≤10 files/folder). On PASS of all 11 criteria for #69 (and Step 1 #65 already closed), pre-set STOP is reached and `gh issue close 69` is authorized. No criterion may be `open` at DO→CHECK handoff.

7. **Bounds, constraints & stop conditions**:
   - Max 3 DO attempts (`n ≤ 3`) before `pdca-escalate`; milestone `1.0.10`; Phase 3 scope only (no cross-shard distributed transactions or server-side merge); no `git push` or `git merge` without explicit user request.
