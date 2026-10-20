---
# T250 — CSV terminal plan cache (#250)

> **2026-10-20 — rc5 acceptance reformulation (policy supersession).** The GitHub issue now uses the two-tier policy: **global** allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db; **local** acceptance = one-axis improvement with no regression on the other (allocations down + time not worse, or time better + allocations not up). This file's frozen thresholds and plan are the historical rc3 attempt. Before any rc5 execution, a new plan revision (`r+1`) must be planned from the reformulated issue; `N`/`r`/`n` are not reset.

- collection: `rc3-4`; milestone `1.0.9-rc3`; variant `pdca-dotnet` + `.opencode/skills/nextorm-pdca/SKILL.md`
- issue: https://github.com/AlexeyShirshov/nextorm/issues/250
- repo / worktree: `/home/alex/sources/nextorm` (current; no branch or worktree created)
- base / HEAD: `0d4b0cf6` / `0d4b0cf69489253249305e0900f9fe5e4f69c1c5`; branch `1.0.9-rc3`
- cycle `N=1`; plan revision `r=1`; attempt `n=1`; evidence contract `rv=1`
- evidence dir: `.pdca/T250/rv1/`; manifest: `.pdca/T250/rv1/manifest.json`
- collection mode: autonomous, auto-commit authorised; **lane cap 1**; BDN host lock `/tmp/opencode/nextorm-bdn-host.lock`; pinned DB sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; foreign dirty paths (88 at HEAD) must never be staged.
- current phase: **PLAN complete — collection PLAN boundary; DO not started**

## Goal (essence)
Remove the repeated CSV column-plan compilation (`Expression.Lambda...Compile()` per column) from every export by using **one bounded per-context terminal-plan cache shared with the JSON row-writer cache**, without changing public APIs, CSV bytes, guards, dialect behavior, or JSON correctness.

## Frozen acceptance criteria (#250)
- 3-column export means: 100 rows **≤230 µs**; 1,000 rows **≤1.1 ms**; 10,000 rows **within ±3 % of 10.9 ms** (10,573–11,227 µs); 10,000-rows lower bound is part of the frozen criterion.
- Eliminate per-column plan garbage on cache hits: **≥8,192 allocated bytes/column/call**.
- Cache-hit output **byte-identical** to a cold run (sync and async).
- No aliasing across options / provider / reader storage types / `SequentialAccess`; complete key separation.
- `ValueTransform` always bypasses the cache (lookup and publication).
- `DataContextCache.Clear()` drops entries; `PurgeQueryCache()` drops them too; bound respected.
- No public API change; CSV dialect contract unchanged as shipped.
- JSON and CSV must not regress each other.

## Minimal solution — three answers
1. Goal: cache the compilation product, not the I/O.
2. Constraints: public contract, exact bytes/RFC 4180/CRLF/null marker/Excel guard, guards, shared-command cache flags, JSON keys/semantics, warning-free build.
3. Optimal under constraints: extract the existing bounded engine from `JsonRowWriterCache` and reuse it for both terminal kinds; no new dependency, no public surface.

## Design decision (alternatives)
| Alternative | Benefit | Cost / risk | Decision |
|---|---|---|---|
| Extract a generic bounded engine; **one heterogeneous store per context**, tagged JSON/CSV keys with typed adapters | One mechanism, one combined bound; concrete second consumer is CSV | JSON migration + tests; footprint overlap with T247/T311 | **chosen** |
| Shared non-generic base, separate JSON/CSV stores | Less key adaptation | Two stores/bounds violate the literal one-cache constraint | reject |
| CSV-only cache with identical mechanics + migration note | Smallest CSV patch | Duplicates the mechanism, explicitly forbidden by the issue | reject |

### Cached artifact
`CsvPlan` only: ordered column delegates, headers, immutable captured policy values, `BinaryOrdinal` and binary admission. Readers, streams, buffers, cancellation state and mutable options are never cached. Write-time `CsvDialect`/policy allocation stays call-local unless a measured residual blocks acceptance.

### Key composition
Terminal-kind tag + concrete context `Type` + ordered projection descriptors sufficient to distinguish every `BuildColumn` dependency + ordered reader storage `Type`s + `SequentialAccess` + effective CSV option values (dialect, field policy, encoding/preamble/fallback semantics and remaining options). Full equality resolves hash collisions; a hash alone never establishes identity. Do not reuse the private integer `RowMapperFactory.BuildSignature`; add a CSV-specific structural key.

### Mutable options
Take one call-local value snapshot and use it consistently for validation, keying, compilation and writing. Never key by `CsvStreamOptions` object identity. Unknown mutable encoding/policy implementations conservatively bypass caching. Any non-null `ValueTransform` (including identity) bypasses lookup and publication.

### Lifecycle / invalidation
Combined capacity **64**, shared LRU, one lock covering lookup/compile/publication/eviction; no failed/cancelled builds published. Reuse the existing generation counter, retaining its legacy JSON internal name to avoid unrelated renames; poll on lookup and recheck before publication. `DataContextCache.Clear()` prevents subsequent stale hits; `PurgeQueryCache()` clears both terminal kinds. Per-context scoping is the isolation boundary; the key still records the concrete context type as required.

## Unit execution mode
**Sequential**, current worktree, one lane. **T250 cannot share a concurrent group with T247/T311**: JSON cache, `DataContext` and terminal extension contracts overlap. If either predecessor lands first, refresh its snapshots/measurements before T250 edits.

## Performance measurement (mandatory decision)
Needed: the changed work is per-export/per-column (`Query/Csv/CsvStreamWriter.cs:312-313`), not one-time initialization. Measure with the committed `SqliteBenchmarkCsvFixedCost` cases, cold-vs-hit allocation probe and a JSON before/after list. Baseline (committed BDN report, ShortRun/InProcess): `WriteCsv_3Col` 100 rows 697.86 µs / 45.12 KB; 1,000 1,637.11 µs / 199.78 KB; 10,000 10,906.99 µs / 1,746.12 KB; `ToList_3Col` 100 rows 97.54 µs / 26.91 KB. Hold `/tmp/opencode/nextorm-bdn-host.lock`; preserve the committed baseline separately; measure the current worktree baseline honestly and record the foreign-dirty fingerprint. Two predeclared repeats per phase, no cherry-picking; ambiguous timing is not a pass. A genuine >3 % speedup at 10k still violates the frozen lower bound — report failure, never pad execution.

## Reconnaissance (mandatory decision)
Needed and assigned as the first DO unit (D:1): compilation dependencies, option inventory, captures, public-surface snapshot, JSON benchmark case identities and quantitative budgets are not supplied by the issue. Observable success = recorded dependency inventory, representability rules, API snapshot and reproducible case list/hash. A missing fact goes to targeted scout, not a guessed implementation. No benchmark runs during PLAN.

## Test strategy
Commands (from repo root; `E=.pdca/T250/rv1`; retain stdout/stderr and exit status under `$E`):
- **B:** `dotnet build nextorm.slnx -c Debug` — exit 0, zero warnings/errors.
- **C:** `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~CsvStreamWriterTests|FullyQualifiedName~CsvChunkedReadTests|FullyQualifiedName~JsonRowWriterCacheTests"`.
- **S:** `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~CsvStreamTests"`.
- **M/P:** `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~CsvTypedColumnMappingTests"`; `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~CsvBinaryGuardTests"`.
- **W (single DO→CHECK boundary sweep):** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o "$E/whole.cobertura.xml" "dotnet test nextorm.slnx -c Debug --no-build --verbosity normal"`.
- **I (dedicated integration-only measurement):** `DOCKER_HOST=… dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o "$E/integration.cobertura.xml" "dotnet test tests/nextorm.integration.tests -c Debug --no-build --verbosity normal"`; integration runs **twice total** (once in W, once here); no new integration tests planned.
- **G:** `python3 .pdca/scripts/perf/t250_terminal_gate.py coverage --root "$E"` — reportgenerator on both reports, literal summaries, configured filters and `-NextORM.Core.InMemory*` exclusion; whole ≥85 % line/75 % branch; integration ≥65 %/75 %.
All test selections must discover >0 tests; zero failures and no unapproved skips. Provision live providers per `.opencode/skills/running-integration-tests/SKILL.md`.

### Variant matrix (every row closed: test / guard / deferred+trigger)
- **Options:** defaults and explicit equivalents; independent mutation of every option; equivalent/different instances; headers/delimiter/dialect; encoding/preamble/fallback; null marker; Excel guard; combined policies → C/S tests for intended reuse/separation + exact bytes. Unrepresentable custom objects → tested bypass guard.
- **Data/readers:** empty/one/many rows; null/`DBNull`/default; nullable/non-nullable value types and reference types; differing storage types for one projection; ordered projection/header differences; binary/null binary; sync/async; `SequentialAccess` both states → C/S/M/P tests; live-provider CSV cases → W/I.
- **Guards:** unsupported/boxed projection, temp table, in-memory, invalid options, pre-/mid-cancel, stream ownership, sticky query-command cache state → existing tests plus hit-path repetitions in C/S; transforms absent/identity/stateful/throwing → repeated bypass tests, never cached.
- **Cache:** per-context and concrete-context-type isolation; CSV/JSON key-collision attempts; concurrent same/different keys; option mutation between calls; eviction/LRU at 64 combined entries; global `Clear`, context purge and `Clear` racing compilation → C/S tests. Concurrent mutation of the caller's mutable options is an explicit unsupported-use assumption, not permission to retain its reference.
- **Priority matrix:** P1 = every frozen-acceptance row and every executed hit/miss/key/snapshot/guard/binary/publication/invalidation branch; CHECK cannot downgrade. P2 = incidental maintenance. New branches require positive and negative assertions; FluentAssertions only; CRLF; warnings-as-errors; no invented mutation-score threshold.

### Coverage/branch/mutation
Project thresholds are the lower bound: whole 85/75, integration 65/75; report branch delta for touched code. Mutation testing deferred unless an existing configured tool is discovered or a surviving key/invalidation defect warrants it; untested branches must be listed explicitly.

## Docs plan
- Update `docs/specs/roadmap/todo_csv_streaming.md:58` (internal roadmap currently records "Компиляция колонкового плана CSV не кэшируется") to describe the implemented shared cache and measured evidence.
- Public CSV docs (`docs/guide/30-streaming-data.md`, `docs/ru/guide/30-streaming-data.md`, `docs/ru/advanced/api-reference.md`, `docs/ru/advanced/limitations.md`, `docs/ru/engine/02-execution-and-rows.md`) are unchanged because bytes/API/dialect behavior do not change; audit both language trees and forbid new public links to internal specs.
- XML-doc: no public member added/changed; CS1591 remains enforced.

## DO units
| unit | state | criteria covered | footprint |
|---|---|---|---|
| D:1 prerequisite/recon | pending | dependency inventory, option inventory/captures, public-surface snapshot, JSON case list/budgets, pinned DB path/hash, foreign-dirty fingerprint, baseline measurement before edits | `.pdca/T250/rv1/**` |
| D:2 shared engine | pending | extract engine from `JsonRowWriterCache.cs:251-352`; JSON key semantics unchanged; one combined store/bound/generation; ownership/purge wiring; existing JSON cache tests green | `src/nextorm.core/Query/TerminalPlanCache.cs` [new], `Query/Json/JsonRowWriterCache.cs`, `DataContext/DataContext.cs` |
| D:3 CSV wiring | pending | snapshot/key, lookup after reader schema is available, guard order and shared-command flags retained; compilation only on misses; `ValueTransform` bypass | `Query/Csv/CsvPlanCacheKey.cs` [new], `Query/Csv/CsvStreamWriter.cs`, `Query/QueryCommandExtensions.cs` |
| D:4 tests + perf | pending | cold/hit, key, lifecycle, concurrency, allocation assertions; CSV gate; candidate measurements; JSON non-regression | `tests/nextorm.core.tests/Csv/CsvStreamWriterTests.cs`, `tests/nextorm.sqlite.tests/Csv/CsvStreamTests.cs`, `tests/nextorm.sqlserver.tests/Csv/CsvTypedColumnMappingTests.cs`, `tests/nextorm.postgres.tests/Csv/CsvBinaryGuardTests.cs`, `tests/nextorm.core.tests/Json/JsonRowWriterCacheTests.cs`, `.pdca/scripts/perf/t250_terminal_gate.py` [new] |
| D:5 evidence/boundary | pending | API snapshot/diff, docs, CRLF/hygiene, W/I boundary runs, coverage, manifest finalization (CHECK decides completion) | `.pdca/T250/rv1/**`, `docs/specs/roadmap/todo_csv_streaming.md`, status file |

Benchmark harness and public docs are read-only; additional edit paths require PLAN review.

## Planned footprint
Production: `src/nextorm.core/Query/TerminalPlanCache.cs` [new], `src/nextorm.core/Query/Csv/CsvPlanCacheKey.cs` [new], `src/nextorm.core/Query/Csv/CsvStreamWriter.cs`, `src/nextorm.core/Query/Json/JsonRowWriterCache.cs`, `src/nextorm.core/Query/QueryCommandExtensions.cs`, `src/nextorm.core/DataContext/DataContext.cs`; `src/nextorm.core/DataContext/DataContextCache.cs:120-142` is a read-only dependency unless D:1 proves otherwise.
Tests: `tests/nextorm.core.tests/Csv/CsvStreamWriterTests.cs`, `tests/nextorm.sqlite.tests/Csv/CsvStreamTests.cs`, `tests/nextorm.sqlserver.tests/Csv/CsvTypedColumnMappingTests.cs`, `tests/nextorm.postgres.tests/Csv/CsvBinaryGuardTests.cs`, `tests/nextorm.core.tests/Json/JsonRowWriterCacheTests.cs`.
Gate/docs/status: `.pdca/scripts/perf/t250_terminal_gate.py` [new], `docs/specs/roadmap/todo_csv_streaming.md`, `.pdca/status/T250-csv-terminal-plan-cache-1.md`, `.pdca/T250/rv1/**`.
Read-only: `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkCsvFixedCost.cs` (may need a new arm only via PLAN review), public docs, collection status file, all foreign dirty paths. Benchmark artifacts go to phase-specific dirs under `$E/perf/**`.

## Versioned evidence contract rv1 (manifest `.pdca/T250/rv1/manifest.json`)
Every row: stable ID, requirement, applicability `A=always`, evidence kinds/sources, exact command/result, artifacts, owner. Common commands B/C/S/M/P/W/I/G expand as above. Missing evidence is not success. No prior revision to supersede.
| row ID | requirement | evidence / command | owner |
|---|---|---|---|
| R250-PERF / E01 | three row-count limits + out-of-budget rejection | BDN raw measurements + `t250_terminal_gate.py gate`; `perf/{baseline,candidate}/**`, `perf/gate.json`, `perf/gate.log` | D:4 |
| R250-ALLOC / E02 | 1/3/8-column cold/hit allocation, ≥8192 B/column saved, zero hit recompiles | executed S assertions/counters + BDN allocation reports | D:4 |
| R250-BYTES / E03 | cold/hit exact bytes, sync/async, null/binary/escaping/header; unequal-byte control detected | C/S/M/P/W/I assertions + provider results | D:3 |
| R250-KEY / E04 | options/context/projection/storage/sequential separation, collision, option mutation | structural-key review + C/S/M/P tests; `key-review.md` | D:3 |
| R250-TRANSFORM / E05 | null transform caches; every non-null transform repeatedly bypasses, exceptions preserved | C/S counters/assertions + bypass counters | D:3 |
| R250-LIFECYCLE / E06 | combined LRU ≤64, both clear paths, isolation, concurrency, generation race | C/S tests + engine review | D:2 |
| R250-ONECACHE / E07 | both consumers use one owned store; no duplicate LRU/generation machinery | Roslyn references + `t250_terminal_gate.py audit` + owned diff | D:2 |
| R250-API / E08 | unchanged public surface incl. negative detection | before/after Roslyn snapshots + audit | D:5 |
| R250-DIALECT / E09 | existing dialect/guard/stream/cancellation behavior on cold and hits | C/S/M/P/W/I assertions, expected failures asserted not swallowed | D:3 |
| R250-JSON / E10 | JSON correctness/cache suite + frozen standalone/mixed benchmark list not regressed | C/W/I + paired BDN/gate outputs; conservative non-regression | D:4 |
| R250-BUILD / E11 | full solution build | command B, 0 warnings/errors, `build.log` | D:5 |
| R250-TESTS / E12 | scoped tests, one boundary sweep, dedicated live integration measurement, discovered counts/skip reconciliation | C/S/M/P/W/I outputs + `test-case-map.json` | D:5 |
| R250-COVERAGE / E13 | whole/integration line+branch gates unchanged | W/I Cobertura + literal reportgenerator summaries + G | D:5 |
| R250-DOCS / E14 | roadmap correction, public-doc/API non-change, CRLF and owned-path hygiene | owned diff + `t250_terminal_gate.py audit` + `git diff --check` | D:5 |
CHECK owns at most **two targeted re-gathers**, one artifact family/request; no extra benchmark runs beyond the predeclared DO budget. Re-gather existing logs first; exhausted/low-confidence evidence → `escalate` trigger 5. Missing reports alone neither justify DO nor revise rv/r; real obligation changes require explicit supersession preserving IDs.

## Risks / known issues
- Medium uncertainty: correctness design follows existing mechanics; performance is unmeasured until D:1/D:4. The 100-row target requires removing ~470 µs of fixed cost; if reader-open/Bdn overhead keeps it above 230 µs, report failure and return to PLAN (do not relax the criterion).
- JSON regression risk from the shared engine; existing JSON tests plus frozen benchmark list are mandatory gates.
- Foreign dirty paths must remain untouched/unstaged; unexpected footprint expansion or persistently uncertain prerequisites → PLAN/scout, then escalation trigger 5.
- `dotnet test nextorm.slnx` is the planned boundary command; if the host runner rejects solution-level selection, record it and use the planned unit projects + integration project without widening scope (no silent substitution).

## Deferred + trigger
Optimize residual `CsvDialect`/policy allocations (`CsvStreamWriter.cs:230,251,558-559`) only if measured residual blocks acceptance. No unrelated row-buffer, mapper-signature, JSON-key or shared-query-cache redesign. Mutation tooling requires an existing configured tool or a surviving key/invalidation defect.

## Predecessor requirements / assumptions
- T247/#247 terminal state: HEAD already contains `JsonRowWriterCache` (commits `c9aa8690`, `9247ccd3`, `0e5890e9`, `547d8360`, `b2c5765c`); its measured GAP remains separate. Do not assume T247 completed.
- T311/#311: no status/evidence at HEAD. Any T247/T311 predecessor change requires refreshing affected snapshots/measurements before T250 edits.
- Assumptions: clean task-relevant paths at PLAN (33 tracked paths); no public API change needed; per-context cache scoping is the isolation boundary; concurrent mutation of caller-owned mutable options is unsupported; the committed baseline was measured on the same host/config.
- Prerequisites: host lock `/tmp/opencode/nextorm-bdn-host.lock`; pinned DB sha256 `32bf10a0…d72295` (full digest to be recorded by D:1); `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; fresh Debug/Release build provenance; provider images as per integration skill; `.pdca/scripts/validate_inner_loop.py` and `.pdca/scripts/pdca_evidence.py` available.

## Refs
- https://github.com/AlexeyShirshov/nextorm/issues/250
- `.pdca/status/collection-rc3-4.md`
- `src/nextorm.core/Query/Csv/CsvStreamWriter.cs`, `Query/Csv/CsvPlanCacheKey.cs` [new], `Query/QueryCommandExtensions.cs`, `Query/Json/JsonRowWriterCache.cs`, `DataContext/DataContext.cs`, `DataContext/DataContextCache.cs`
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkCsvFixedCost.cs`; `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsvFixedCost-report-github.md`
- `tests/nextorm.core.tests/Csv/CsvStreamWriterTests.cs`, `tests/nextorm.core.tests/Json/JsonRowWriterCacheTests.cs`, `tests/nextorm.sqlite.tests/Csv/CsvStreamTests.cs`
- `docs/specs/roadmap/todo_csv_streaming.md`; `docs/specs/performance/acceptance-benchmarks.md`; `.opencode/skills/nextorm-pdca/SKILL.md`

## Durable state
- Current cycle N=1; Plan revision r=1; Attempt n=1; contract rv=1.
- Defect history: — (none).

## Progress log
2026-10-19T13:32:08Z | PLAN | r=1 | n=1/3 | PLAN ready — collection PLAN boundary (no DO, no branches, no benchmark runs) | this file
---
