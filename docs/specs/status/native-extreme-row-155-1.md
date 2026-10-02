# Lazy extreme-row payload + single select-list build — #155 (Stage A: production)

- status: CLOSED
- check: CHECK PASS (r1/n2) → ACT
- cycle: 1, revision r1, attempt 2/3
- issue: #155 (milestone 1.0.9-b)
- collection: 1.0.9-b, task 3 (follow-up of the #144 accepted debt 3)
- goal: drop the unused payload description on PostgreSQL and remove the duplicate alias/select-list
  resolution on the native extreme-row preparation path, without changing generated SQL, results,
  cache keys or the public surface.
- acceptance:
  - (A1) `ExtremeRowDescription.Payload` is factory-backed and memoized (materialized at most once,
    thread-safe); the eager ctor delegates to it; the public surface stays
    `IReadOnlyList<ExtremeRowRenderColumn> Payload { get; }` plus the internal ctors — no public ctor,
    no new public member.
  - (A2) PostgreSQL renders with **0 payload materializations**; ClickHouse with **≤1**.
  - (A3) exactly one `EntitySelectListBuilder.Build` per native preparation (was 2–3), reused by the
    lazy payload factory, `MakeExtremeRowAliases` and `MakeExtremeRowOuter`.
  - (A4) generated SQL, results and cache keys byte-identical on PostgreSQL, ClickHouse and the
    portable path.
  - (A5) Debug + Release builds 0 warnings / 0 errors; CRLF preserved; no commit.
- DTO lazy-payload seam: `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs` —
  `ExtremeRowDescription` gains the internal
  `ExtremeRowDescription(bool isMax, IReadOnlyList<ExtremeRowRenderColumn> keys,
  IReadOnlyList<ExtremeRowRenderColumn> groups, Func<IReadOnlyList<ExtremeRowRenderColumn>>
  payloadFactory)` overload; `Payload` returns `Lazy<T>.Value`; explicit `Equals`/`GetHashCode`
  compare the exposed `IsMax`/`Keys`/`Groups`/`Payload` with the default comparers and exclude the memo
  field, so record equality/hash and `with`/copy semantics stay intact.
- single-select-list-build flow: `SqlBuilder.MakeExtremeRowSelect` builds the entity projection once
  inside the `renderer != null && Ties == One && Metadata` gate; `MakeExtremeRowDescription(cmd,
  entitySelectList)` captures it in a lazy payload factory
  (`MakeExtremeRowPayloadDescription(SelectExpression[])`); `MakeNativeExtremeRowSelect(cmd, renderer,
  entitySelectList)` forwards it to `MakeExtremeRowAliases(cmd, entitySelectList)` and
  `MakeExtremeRowOuter(..., entitySelectList)`. The portable path keeps the on-demand
  `BuildExtremeRowEntitySelectList` helper. Aliases are still resolved after `CanRender`; key/group
  alias semantics unchanged.
- variant matrix: PG global/grouped × whole-row/projection (0 payload materializations each); CH
  global/grouped × whole-row/projection (≤1 payload materialization); native accept vs reject;
  `All`/capability-absent/unmapped-source/parameter-mode portable; eager-vs-factory DTO
  equality/hash/`with`; the payload factory never materialized on PG; byte-identical SQL assertions
  reused from `tests/nextorm.{postgres,clickhouse}.tests/ExtremeRowNativeSqlGenerationTests.cs` and the
  portable SQL-generation suites.
- perf decision: correctness-only Stage A. One standard **7-case acceptance** run is required (no
  native BDN; native BDN is excluded). Wall/alloc ratios guard the preparation path against a
  regression; no speedup claim is made.
- risks: coordination with **#154** (internal DTO ctor accessibility — this change keeps the ctors
  internal and does not touch `ExtremeRowRenderColumn`/`ExtremeRowRenderRequest`) and **#150**
  (ClickHouse floating-point native eligibility — the payload seam must not widen CH eligibility; CH
  still reads `Payload` once in `CanRender`). Other risks: the memo field leaking into record equality
  (closed by explicit `Equals`/`GetHashCode`); an accidental second `Build` (closed by threading the
  single list); SQL drift (closed by the byte-level SQL-generation tests).
- progress log:
2026-10-02T05:34:40Z | DO | revision r1 | iteration 1/3 | plan recorded (goal/DTO seam/single select-list flow/acceptance/matrix/perf/risks) | docs/specs/status/native-extreme-row-155-1.md
2026-10-02T05:34:40Z | DO | revision r1 | iteration 1/3 | D1+D2 done: lazy factory-backed `Payload` (internal ctor overload, explicit `Equals`/`GetHashCode`) + one `EntitySelectListBuilder.Build` per native prep threaded into description/aliases/outer; Debug exit 0 0W/0E, Release exit 0 0W/0E; focused PG ExtremeRow 34/0/0, CH ExtremeRow 38/0/0, core ExtremeRow 32/0/0; CRLF preserved; no commit | /tmp/opencode/155/build-{debug,release}.log, /tmp/opencode/155/test-{pg,ch,core}-extremerow.log
- durable state: cycle 1, plan revision r1, attempt 1/3 (no replan, no rejected candidate; counters unchanged).
- defect history: none observed in DO.

## Verification evidence (Stage B: tests + verification)

- new deterministic tests (5 core + 3 PG + 3 CH = 11, all green):
  - `tests/nextorm.core.tests/ExtremeRowDescriptionLazinessTests.cs` (5): factory never invoked before `Payload` is read; memoized exactly once across repeated and 64-way concurrent reads; eager-vs-factory equality/hash; `with` carries the same lazy payload without re-invoking the factory.
  - `tests/nextorm.postgres.tests/ExtremeRowPayloadLazinessTests.cs` (3): transparent wrapper around the real PG renderer (`base.ExtremeRowRenderer`); byte-identical SQL to the stock context (global/grouped); `CanRender` called exactly once; the production-built description's payload is never materialized (`Lazy.IsValueCreated` via reflection), including a rejected candidate.
  - `tests/nextorm.clickhouse.tests/ExtremeRowPayloadLazinessTests.cs` (3): byte-identical SQL; payload materialized once on the accept path and memoized; not read on the float-key short-circuit.
- structural proof: `roslyn refs NextORM.Core.ExtremeRowDescription.Payload` -> in production only `ClickHouseExtremeRowRenderer.cs:49` (plus the record's own `Equals`/`GetHashCode`); the PostgreSQL renderer never reads it. Single native `EntitySelectListBuilder.Build` at `SqlBuilder.cs:640`, threaded into description/aliases/outer; `SqlBuilder.cs:923` is the portable-only on-demand helper.
- builds: Debug exit 0 / 0W / 0E (`/tmp/opencode/155/build-debug.log`); Release exit 0 / 0W / 0E (`/tmp/opencode/155/build-release.log`).
- full per-project `dotnet test -c Debug`, all exit 0: core 1282/0/0; sqlite 871/0/1; postgres 734/0/0; sqlserver 549/0/0; mysql 253/0/0; mariadb 153/0/0; clickhouse 481/0/0; clickhouse.extensions 23/0/0; entityframeworkcore 126/0/0; alias 34/0/0 (logs `/tmp/opencode/155/test-<project>.log`).
- live integration (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`): full run exit 0, 2995 total / 0 failed / 187 skipped, every skip capability-based (no provider-unavailable); focused CH availability 103/0/0; CH ExtremeRow 17/0/0; PG ExtremeRow 16/0/0.
- coverage (CI `dotnet-coverage collect` + `reportgenerator` over `nextorm.{core,sqlite,postgres,sqlserver}`): line **86.5 %** (threshold 85), branch **77.7 %** (threshold 75); Summary `/tmp/opencode/155/coverage-report/Summary.txt`.
- mutation (targeted, md5-verified revert after each):
  - Stryker 5.0.0 probe produced 17 mutants on `DialectCapabilities.cs` but reported all `Survived`, while the same tests kill an equivalent manual mutant -> MTP/xunit-v3 `test-case-filter` integration is unreliable on this solution; the targeted campaign is authoritative.
  - M1 force-eager payload (ctor reads `_payload.Value`): killed - core 2 failed + PG flow 3 failed.
  - M2 null/stale `Payload` (`=> null!`): killed - core 4 failed.
  - M3 double-invoke factory: killed - core 4 failed.
  - M4 payload description `isDirectMappedColumn := false`: killed - CH 18/41 failed.
  - M5 rebuild select list in `MakeExtremeRowAliases`: survivor by design (behaviour-equivalent); guarded structurally (single native `Build` at `SqlBuilder.cs:640`) and by the perf allocation ratio.
  - earlier mutant `IsMax ==` -> `!=` in `Equals`: killed - core 1 failed.
- perf acceptance: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7/0 cases, wall 46 s, BDN global total 43.41 s; `SqliteBenchmarkCachedPlan.Cached_ToList / Prepared_ToList` ratio **1.95** (baseline 1.87, threshold 2.244; #147 candidates 2.21 / 1.99), alloc ratio 7.90; log `/tmp/opencode/155/perf-acceptance.log`.
- artifacts: `BenchmarkDotNet.Artifacts` restored (`git checkout --` + `git clean`), no BDN diff; `StrykerOutput/` and `stryker-config.json` removed; no commit.
2026-10-02T11:01:00Z | DO | revision r1 | iteration 1/3 | Stage B evidence recorded (11 new tests green; Debug/Release 0/0; 10 unit projects green; live integration 2995/0/187 + focused CH/PG extreme-row green; coverage 86.5/77.7; mutation M1-M4 killed, M5 equivalent; perf 1.95 within 2.244) | docs/specs/status/native-extreme-row-155-1.md
