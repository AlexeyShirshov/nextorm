# rc2-180 — JSON streaming: provider-specific column conversion + unified validation exceptions

- collection: `1.0.9-rc2` (task D180)
- issue: #180 (milestone 1.0.9-rc2, label enhancement; parent #39)
- task_id: D180
- selected_variant: pdca-dotnet
- cycle: N=1
- plan revision: r=2
- rv: rv=2 supersedes rv=1
- iteration: n=1 (DO not started)
- phase: PLAN complete
- plan_state: ready
- base: branch `1.0.9-rc2` @ `18659e41`
- blocking predecessor: none (#168/J1 resolved)
- collection P-phase: PLAN only; no code/branches/worktrees/DO

## Goal

Make JSON streaming's supported column conversions and rejection behavior predictable across providers, without regressing J1's typed numeric path.

## Scope

**In:** document and test the declared-CLR-type × reader-field-type × provider conversion matrix; fix demonstrated reader-binding gaps; standardize JSON preflight validation on `NotSupportedException` + a stable message convention; reject unsupported options/projections/reader-type combinations before modifying the destination; preserve both terminal surfaces, sync/async, CancellationToken, resource ownership, query/cache behavior.

**Out:** enum/nested-object support (#178/#176); runtime partial-output/recovery policy (#179); native SQL Server `FOR JSON` / ClickHouse `JSONEachRow` implementations; CSV refactoring; a general conversion framework; public terminal renaming; plan-cache changes; normalizing provider/data/cancellation/disposed-writer/programmer-argument errors into validation exceptions.

Approach decision: explicit matrix + targeted typed bindings + unified preflight failures (chosen) over a CSV-style JSON provider hook or a broad reader rewrite (both rejected — abstraction without a demonstrated second consumer; KISS).

## Acceptance criteria

| ID | Observable acceptance | Negative case |
|---|---|---|
| R180-01 | Every supported scalar/provider pair has a documented reader representation and conversion; output JSON semantically matches `JsonSerializer` for the declared projection type. | A genuinely unsupported declared type or reader/type combination is rejected; no silent coercion into a different JSON type. |
| R180-02 | JSON preflight validation consistently throws `NotSupportedException`, including in-memory; messages follow the convention. | Empty projection, invalid mode/options, duplicate names, unsupported converter/LOB/complex columns, unsupported reader bindings do not leak inconsistent validation types. |
| R180-03 | Unsupported metadata/forms are rejected before **any destination change**, including array/root framing. | A destination preloaded with sentinel bytes stays byte-for-byte unchanged after rejection. |
| R180-04 | J1's typed numeric path and ClickHouse decimal handling remain intact; supported unsigned/sbyte paths get typed bindings, not new boxed numeric fallbacks. | Unsupported numeric storage is not accepted through arbitrary `GetValue` coercion; boundary values do not silently wrap/truncate. |
| R180-05 | Both surfaces, sync/async, async CT, sibling `params` parity, fail-closed behavior, provider coverage, destination ownership are verified. | One surface cannot acquire different validation semantics; cancellation stays cancellation; destination is not disposed. |
| R180-06 | Declared nullability, `DBNull`, ignore-null, array/NDJSON framing, base64 bytes retain existing semantics. | Nullables are not converted to default numeric/bool; ignored-null object members do not change array element semantics. |
| R180-07 | SQL generation, parameter binding, shared command/cache state unchanged. | No sticky `Cache=false`, altered planning, or SQL rewrite introduced to make a conversion test pass. |
| R180-08 | EN/RU + XML docs describe the matrix, exception boundary, runtime-error distinction; required build/coverage/provider/measurement evidence recorded. | Skipped provider suite, zero-test selector, or absent measurement/report is not accepted evidence. |

**Message convention:** `JSON streaming validation [<stable-reason>]: <context>.` Fixed nonlocalized reason ids for mode/options, projection, names, unsupported column, reader binding, in-memory, unsupported execution form. Column failures identify column + declared CLR type; reader-binding failures also identify observed field type. Do not include row values, SQL, or connection strings. Applies to preflight failures in `JsonShapePlan` and binding/preparation paths. `JsonStreamWriter.cs:156/:159` lifecycle errors stay `InvalidOperationException`/`ObjectDisposedException`; runtime overflow/provider errors/cancellation keep their category.

### Confirmed r=2 decisions — `params` semantics, reason tokens, conversion boundary

**`params` semantics (confirmed).** `params` elements are positional SQL parameter values for `NormParam.GetName(i)` (`DbPreparedQueryCommand.GetDbCommandCore:196`; `@params[i] ?? DBNull.Value` :243). An empty set binds nothing; there is no arity guard.

**Chosen signatures** (options and `CancellationToken` REQUIRED, no defaults, to avoid `(stream, null)` ambiguity; the existing overloads are preserved):

- `EntityBuilderExtensions.WriteJson<TEntity>(this EntityBuilder<TEntity> builder, Stream stream, JsonStreamOptions options, CancellationToken cancellationToken, params ReadOnlySpan<object?> @params)`
- `EntityBuilderExtensions.WriteJsonAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream stream, JsonStreamOptions options, CancellationToken cancellationToken, params object?[] @params)`
- `QueryCommand<TResult>.WriteJson(Stream stream, JsonStreamOptions options, CancellationToken cancellationToken, params ReadOnlySpan<object?> @params)`
- `QueryCommand<TResult>.WriteJsonAsync(Stream stream, JsonStreamOptions options, CancellationToken cancellationToken, params object?[] @params)`

No JSON `params` overload in `QueryCommandExtensions`. The span is copied to an array once per call at most; it is NOT a boxed-read fallback.

**Fixed stable reason tokens** for the `JSON streaming validation [token]: context.` convention: `mode-options`, `projection`, `names`, `unsupported-column`, `reader-binding`, `in-memory`, `unsupported-execution-form`. Empty-select uses `[projection]`. Uniformization covers unsupported scenarios only (not cancellation/disposed/null-argument numerics/overflow).

**Conversion boundary.** Declared `sbyte/ushort/uint/ulong` stay preflight-unsupported (deferred to a separately agreed extension); reader `SByte`/unsigned are retained only for reachable allowed projections; the whitelist is narrowed by bindings, not by name; an incompatible reader schema → `[reader-binding]` after reader open but BEFORE first write; value-dependent overflow stays read-time (`ulong.MaxValue → long` is not a success and is not masked as `NotSupported`); J1 + ClickHouse decimal preserved; no new provider abstraction and no boxed fallback.

## Variant matrix

| ID | Axis / cases | Closure |
|---|---|---|
| V01 | `QueryCommand<TResult>` × `EntityBuilder<TEntity>` × sync/async | Test: positive output + identical preflight rejection on all four. |
| V02 | Async CT: default, already cancelled, cancelled during read/write | Test: CT propagation + ownership. Runtime partial-output policy = #179. |
| V03 | Numeric: byte/sbyte, signed/unsigned ints, float/double, decimal | Test: provider-representable zero, sign boundaries, min/max, fractional, widening/narrowing. Guard: unsupported field/type binding before output. |
| V04 | bool, string, Guid, DateTime, byte[] | Test: provider mappings, Unicode/escaping, empty string/bytes, base64, DateTime kind/precision as mapped. |
| V05 | SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse | Integration test: matrix with actual readers (synthetics alone cannot close). |
| V06 | SQL Server storage-typed numerics/dates; ClickHouse decimal field types; existing unsigned/sbyte boxed paths | Experiment + regression test: identify actual getter/field-type combos, then targeted typed fixes. Existing ClickHouse decimal test remains required. |
| V07 | Reference null, nullable value null/default, non-null; ignore-null on/off | Test: object and scalar/array forms; distinguish null from default. |
| V08 | Scalar/value projection, flat reference DTO, flat value-type projection, empty result | Test: framing + serializer-equivalent values. |
| V09 | Unsupported complex shape, converter, LOB streaming, empty projection, unnamed/duplicate names, naming policy returning null | Guard + test: canonical exception/message + unchanged sentinel destination. |
| V10 | Unknown mode; NDJSON+root; NDJSON+indent; valid array/root/indent combos | Guard + test: option boundaries + fail-closed. |
| V11 | In-memory; temp-table batch unsupported form | Guard + test: `NotSupportedException`, canonical reason, no output. |
| V12 | Destination ownership; success, preflight failure, cancellation, runtime failure; sync/async flushing | Test: recording destination verifies no close/dispose + existing flush contract. |
| V13 | `params` parity with sibling streaming terminals; parameterized queries | Required signature guard + test. Exact sibling signatures not supplied; resolve in D180.1. Parity gap requires explicit PLAN review. |
| V14 | Enum/nested projection | Guard now: stays fail-closed; support deferred only to #178/#176 within rc2; their support triggers this matrix's review before milestone closure. |
| V15 | Runtime malformed value, overflow, non-finite float/double where JSON rejects it | Test: distinguish runtime/data failure from preflight validation; no blanket wrapping. Recovery = #179. |

## Footprint / DO units (planned, not started; anchors not promised post-edit lines)

| Unit | Work | Likely footprint |
|---|---|---|
| D180.1 | Fix now: prerequisites + bounded reconnaissance. Capture actual field types/getters; verify preflight ordering, sibling `params` signatures, provider enablement, flush semantics; add unchanged benchmark harness + baseline before edits. | `JsonRowWriterFactory.cs:130-250`; `JsonShapePlan.cs:103-198`; `DataContext.cs:344-368`; `QueryExecutor.cs:1160-1218`; terminal surfaces; test/benchmark projects |
| D180.2 | Fix now: unified preflight validation — normalize exceptions/messages incl. empty projection; bind/validate supported reader conversions before framing; preserve lifecycle/runtime exceptions. (r=2: fixed stable reason tokens, empty-select uses `[projection]`; sentinel ordering kept — no destination change before rejection.) | `Query/Json/JsonShapePlan.cs`; `JsonRowWriterFactory.cs`; `DataContext.cs`; `QueryExecutor.cs`; `QueryCommand.TResult.cs:145-207` only where required |
| D180.3 | Fix now: demonstrated conversion gaps — extend existing typed bindings, preserve J1 + ClickHouse decimal; no numeric boxing fallback escape hatch. (r=2: reachable conversion matrix only; declared sbyte/ushort/uint/ulong stay preflight-unsupported; reader `SByte`/unsigned retained for reachable allowed projections; whitelist narrowed by bindings not name; incompatible reader schema → `[reader-binding]` after reader open but before first write; value-dependent overflow stays read-time; no new provider abstraction/boxed fallback.) | `Query/Json/JsonRowWriterFactory.cs:184-275` |
| D180.4 | Fix now: regression/boundary matrix + measurements. (r=2: forwarding/API/SQL-binding parity tests — E180-16; both surfaces × sync/async × empty/multiple/null SQL values.) | `tests/nextorm.core.tests/**`; `tests/nextorm.sqlite.tests/JsonStreamingTests.cs`; `tests/nextorm.clickhouse.tests/JsonRowWriterFactoryClickHouseDecimalTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.JsonStream.cs`; CH/MariaDB integration files; `benchmarks/nextorm.benchmark/**` |
| D180.5 | Fix now: docs + tracking — final matrix + exception contract; mark Q7 resolved only after CHECK; verified #180 link in roadmap/status. (r=2: EN/RU docs + XML for the four `params` overloads and benchmark comparison — E180-18.) | EN/RU pages; terminal XML-docs; `docs/specs/roadmap/todo_json_streaming.md:116-125/:219-226/:284-285`; cycle status |
| D180.6 | Fix now: public `params` parity — add four overloads forwarding positional SQL parameter values to the existing executor path; XML-doc + compatibility tests. | EntityBuilderExtensions.cs:158-203; QueryCommand.TResult.cs:145-190 |

Uncertainty: exact helper edits, additional test files, MariaDB fixture location, any provider-specific implementation not established by gather. Stale roadmap location `DataContext/Json/*` corrected to actual `src/nextorm.core/Query/Json/` in touched docs.

## Predecessor-result requirements

None blocking. #168/J1 resolved (preserve its implementation + regression evidence). #179 no blocking dependency (agree boundary: metadata/options rejection is preflight; row/data/IO failure may occur after output; do not implement recovery). #178/#176 no blocking dependency while enum/nested stay guarded; if their support lands first, refresh this matrix.

## Assumptions / prerequisites

- New provider abstraction: NO, unless D180.1 demonstrates an otherwise-inexpressible conversion seam (CSV is not evidence of JSON necessity).
- Validation exception type: `NotSupportedException` (preserves the explicit in-memory contract + CSV precedent; `QueryPreparationException`/new public exception rejected as unnecessary compat surface). Changing former JSON `InvalidOperationException` validation cases is deliberate + documented.
- Writer binding before framing: unproved prerequisite — prove with a sentinel destination and repair ordering in D180.2 if needed.
- Exact sibling `params` parity: evidence gap — bounded Roslyn inspection in D180.1.
- Actual provider representation/boundaries: prerequisite experiment — real-reader metadata, not inferred SQL type names.
- MariaDB/other integration: in-cycle prerequisite — load `running-integration-tests`, enable providers, provision; unavailable containers are not permission to skip; persistent failure returns to PLAN.
- Toolchain: .NET 10, local tools restored; CPM, warnings-as-errors, nullable, CRLF; no dependency additions by default.

No assumption may relax R180-01..08.

## Test strategy

Commands from repo root; capture stdout/stderr + actual exit code:
- C-BUILD: `dotnet build -c Debug`
- C-CORE: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~InMemoryTests|FullyQualifiedName~Json"`
- C-SQLITE: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~JsonStreamingTests"`
- C-CLICKHOUSE: `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter "FullyQualifiedName~JsonRowWriterFactoryClickHouseDecimalTests"`
- C-PG: `dotnet test tests/nextorm.postgres.tests -c Debug`
- C-SQLSERVER: `dotnet test tests/nextorm.sqlserver.tests -c Debug`
- C-MYSQL: `dotnet test tests/nextorm.mysql.tests -c Debug`
- C-INTEGRATION: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~JsonStream|FullyQualifiedName~ClickHouseIntegrationTests"`
- C-ALL: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test -c Debug`

Selectors are patterns over existing + planned tests; record discovered identities/counts after implementation; zero-test selector fails its evidence obligation. Unit = option/projection rejection, messages, sentinel output, synthetic metadata rejection, surface parity, CT/ownership, null/default, SQL equality, byte/base64. Integration = actual-reader matrix all six providers; shared suite covers SQLite/PG/SQLServer/MySQL; ClickHouse + MariaDB dedicated; provider manifest must list enabled providers/executed cases/skips.

Boundary sweep: numeric limits/overflow; fractional precision; DBNull; null/default; empty result/string/bytes; unknown enum-mode value; invalid option pairs; duplicate/null names; unsupported column first/middle/last; sentinel destination; cancellation before/during.

Coverage: reproduce `dotnet-coverage`→`reportgenerator` per `.github/workflows/dotnet.yml`; included assemblies core/sqlite/postgres/sqlserver; target line >=85%, branch >=75%; report actuals + all uncovered changed branches.
- `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect "dotnet test -c Debug" -s coverage.settings.xml -f cobertura -o TestResults/D180/r1/rv1/coverage.cobertura.xml`
- `reportgenerator "-reports:TestResults/D180/r1/rv1/coverage.cobertura.xml" "-targetdir:TestResults/D180/r1/rv1/coverage" "-reporttypes:Html;TextSummary"`

Branch/mutation intent: kill removal of preflight guards, null checks, checked numeric conversion, duplicate-name detection, premature framing. No numeric mutation-score promise without verified config; targeted mutation optional, branch/negative-case evidence mandatory.

## Docs plan

Update together: `docs/guide/28-streaming-data.md` + RU mirror (matrix, validation/runtime boundary, ownership/flush, cancellation); `docs/guide/14-json.md` + RU (streaming limitations, exception guidance); `docs/advanced/api-reference.md` + RU (terminal contract); `docs/guide/26-large-objects.md` + RU only affected LOB wording; XML-docs on both terminal surfaces + sync/async overloads; internal roadmap (actual `Query/Json` location, matrix, Q7/#180 status). No public docs links to `docs/specs/**`. No public rename planned.

## Performance-measurement decision

Required (per-row path: `JsonRowWriterFactory.cs:130-140/:184-250`, unsigned/sbyte `GetValue` :234; a one-time-only exemption would be wrong). Planned BenchmarkDotNet JSON workload in existing benchmark project: SQLite streaming identical harness before/after; 1 and 10,000 rows; signed numerics, unsigned/sbyte, decimal, mixed scalar, null mixture; measure throughput + allocated; distinguish one-time binding from steady-state.
- C-PERF-BASE: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*Json*' --exporters json --memory --artifacts TestResults/D180/r1/rv1/benchmarks/baseline`
- C-PERF-AFTER: same with `--artifacts TestResults/D180/r1/rv1/benchmarks/after`
Report measurements + uncertainty; no asserted speedup/regression percent. Acceptance-cached-path benchmark NOT required given no query/planner/cache change (R180-07 + SQL-equality guard); a cache/planner change triggers PLAN review and adds it.

## Reconnaissance decision

Required, bounded, in-cycle: D180.1. Outputs: (1) per-provider table declared CLR type / actual GetFieldType / typed-read outcome / representative boundary; (2) proven ordering: unsupported reader binding leaves sentinel unchanged; (3) Roslyn-resolved sibling signatures + explicit `params` parity conclusion; (4) existing flush/ownership behavior + integration-provider enablement manifest. Roslyn first; no C# symbol grep. If the experiment cannot establish a typed conversion, contradicts the matrix, or finds an API parity gap needing new overloads → return a new `P:` candidate with observations; original D stays active/blocked.

## Unit execution mode

One stream, sequential, one tree: D180.1 → D180.2 → D180.3 → D180.4 → D180.5. Shared files/contracts; no worktree. Benchmark baseline precedes implementation changes.

## Evidence contract rv2 (r=2; rv=2 supersedes rv=1)

Persistence prerequisite: this file records the contract before DO. During collection only persistence occurs.
Common: sources are planned unless a gathered baseline anchor; artifact root `TestResults/D180/r2/rv2/` (rv=1 recon/baseline retain r1 provenance at `TestResults/D180/r1/rv1/`); command evidence = full invocation + source revision/diff + exit code + stdout/stderr + discovered test identities/counts; test commands require exit 0, nonzero relevant case count, no required-provider skips; review commands require exit 0 + signed row checklist; DO producer `coder`, CHECK evidence owner `check`, D180.1 facts by `scout` (later DO); every row rv=2; P1 cannot be downgraded by CHECK.

| Row | Priority; predicate | Scenario / invocation | Evidence -> artifacts |
|---|---|---|---|
| E180-01 / R01,R04 | P1 always | C-SQLITE, C-CLICKHOUSE, C-INTEGRATION; V03-06 | Real-reader observations, test execution, typed-binding diff -> provider-matrix, logs, case manifest |
| E180-02 / R02 | P1 always | C-CORE, C-SQLITE, C-INTEGRATION; V09-11 | Exception type/message assertions + diff -> validation-checklist, logs |
| E180-03 / R03 | P1 always | C-SQLITE, C-INTEGRATION; sentinel; unsupported first/middle/last | Destination bytes before/after; binding/framing ordering -> preflight-checklist, logs |
| E180-04 / R05 | P1 always | C-CORE, C-SQLITE, C-INTEGRATION; V01,V02,V12 | Surface/async/CT/ownership/flush tests -> terminal-matrix, logs |
| E180-05 / R05 | P1 always | C-SQLITE + `git diff -- src/nextorm.core/QueryCommand.TResult.cs src/nextorm.core/EntityBuilderExtensions.cs` | Roslyn signature report; parity/parameter-binding assertions -> signature-parity, diff review, log |
| E180-06 / R06 | P1 always | C-SQLITE, C-INTEGRATION; V07,V08,V10 | Null/default, framing, serializer comparison, base64 -> case manifest, logs |
| E180-07 / R01,R05 | P1 always | C-INTEGRATION | Provider manifest: SQLite/PG/SQLServer/MySQL/MariaDB/ClickHouse executed; in-memory from C-CORE -> provider-execution, logs |
| E180-08 / R07 | P1 always | C-SQLITE, C-PG, C-SQLSERVER, C-MYSQL; `git diff -- src/nextorm.core` | SQL/parameter regression + review (no planner/cache mutation) -> query-boundary-checklist, logs |
| E180-09 / R08 | P1 always | C-BUILD; `git diff --check`; `git diff --name-only` | Build diagnostics, scope review, byte-level CRLF check, nullable/CPM review -> build.log, repository-discipline |
| E180-10 / R08 | P1 always | coverage collect/report commands | Cobertura, report, workflow comparison -> coverage.cobertura.xml, coverage/, threshold + changed-branch summary |
| E180-11 / R08 | P1 always | C-ALL | Full regression + provider manifest -> full-regression.log, test inventory |
| E180-12 / R08 | P1 always | `dotnet docfx docs/docfx.json`; `git diff -- docs docs/ru src/nextorm.core` | Build log + EN/RU/XML checklist; no public links to internal specs -> docs.log, docs-checklist |
| E180-13 / R04,R08 | P1 always | C-PERF-BASE + C-PERF-AFTER | BDN JSON/logs, baseline identity, identical harness comparison -> benchmarks/baseline/, benchmarks/after/, summary |
| E180-14 / R01-08 | P1 always | `git diff -- docs/specs/roadmap/todo_json_streaming.md`; CHECK review of persisted D180 contract | Final requirement->variant->test/evidence map + Q7 tracking -> acceptance-map, CHECK verdict |
| E180-15 / R02,R03,R05 | P1 always | C-SQLITE, C-INTEGRATION; V14,V15 | Enum/nested guards + runtime-vs-preflight tests -> boundary checklist, logs |
| E180-16 / R180-05 | P1 | forwarding matrix (both surfaces × sync/async × empty/multiple/null SQL values) matches ordinary execution; C-SQLITE | assertions + run log -> E180-16.log; owner D180.4 |
| E180-17 / R180-05 | P1 | old + new call shapes + null compatibility compile and run; C-BUILD + C-SQLITE, 0 warnings/errors; -> E180-17-build.log, E180-17-tests.log; owner D180.6 |
| E180-18 / R180-05 | P1 | XML + EN/RU docs parity for the four overloads (SQL-values semantics, required options/token, examples); scout audit + `dotnet docfx docs/docfx.json` exit 0, no new warnings; -> E180-18-audit.md, E180-18-docfx.log; owner D180.5 |

CHECK re-gather budget: at most two targeted re-gather dispatches total per CHECK, owned/tracked by `check`; each names missing rows/artifacts; reuse existing evidence first; re-gather establishes missing evidence, not weakens criteria. After budget exhaustion, missing evidence = explicit insufficient-evidence (not fabricated pass, not by itself a DO iteration/contract revision). Justified change = rv=2, supersedes rv=1, preserves ids/priorities, adds new ids. Missing reports alone do not justify revision; a genuinely changed implementation plan increments r and resets n=1; clarification/rejected candidates do not.

## Priority classes

P1 by construction (streaming-terminal invariants): both surfaces, sync/async, async CT, sibling `params` parity, fail-closed forms, ownership/flush, provider coverage (E180-01..08, E180-15 not downgradable). P1 execution-path correctness: declared/storage conversion, zero-boxing numeric bindings, null/default, validation category, preflight ordering, cache-state isolation. Mandatory verification/delivery: build, coverage, complete regression, paired docs + measurements unconditional.

## Risks

Real-reader vs synthetic behavior; normalizing former `InvalidOperationException` validation cases is externally observable; preflight cannot prove future row values convertible; SQLite unsigned representability + provider decimal/date precision may make an overbroad matrix misleading; MariaDB/provider skips could hide missing coverage; #176/#178/#179 overlap; benchmark harness/env differences; a discovered `params` parity gap may require public API additions + an actual replan.

## Confidence

High: retain `NotSupportedException`, separate preflight from runtime/lifecycle, avoid speculative provider abstraction. Medium: exact conversion fix set (depends on bounded real-reader evidence). Low only in sibling `params` parity (signatures not supplied) — D180.1 must resolve via Roslyn; if still low after bounded inspection or experiments remain inconclusive, escalate under trigger 5.

## Progress log

- PLAN(r=1) authored by `planner`; persisted by `coder` in collection P-phase; `plan_state=ready`; phase=PLAN complete; STOP at PLAN→DO. DO not started; no code/branches/worktrees.
- 2026-10-08T22:35Z | DO | r=1 | n=1 | D180.1 done (recon+baseline; no src edits, no commits). Tooling: `scripts/validate_inner_loop.py` present+executable, `--help` exit 0. Baseline C-PERF-BASE ran exit 0 in 211s: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*WriteJson*' --exporters json --memory --artifacts TestResults/D180/r1/rv1/benchmarks/baseline` → 21 cases (`SqliteBenchmarkWriteJson` 7 methods × RowCount 1k/10k/100k). Deviation: PLAN's `*Json*` selects 69 cases incl. non-row-writer D197/D198 and BDN here has no `\|` OR; after-run MUST reuse the same `*WriteJson*`. Evidence: `TestResults/D180/r1/rv1/benchmarks/baseline.run.log`, `.../benchmarks/baseline/` (BDN md/csv/html/json+log; staged because BDN `--artifacts` is ignored by the pinned config). Recon real readers captured for SQLite/PostgreSQL/ClickHouse/MySQL/MariaDB: `TestResults/D180/r1/rv1/recon/{sqlite,postgres,clickhouse,mysql,mariadb}.realreader.tsv` (+ `*.run.log`); SQL Server not run. KEY FINDINGS: (a) declared sbyte/ushort/uint/ulong rejected at PREFLIGHT by `JsonShapePlan.Classify` (`JsonShapePlan.cs:354-375`) while `JsonRowWriterFactory` numeric field-type whitelist still accepts them; (b) read-time `NotSupportedException` from `JsonRowWriterFactory.BuildNumericRead` when declared int/decimal over a non-numeric reader field type (SQLite `c_int_text`/`c_dec_text`); (c) sentinel-before-write ordering NOT yet proven; (d) `params` parity gap confirmed — Json terminals have NO `params` overloads (`EntityBuilderExtensions.cs:158,172,187,203`; `QueryCommand.TResult.cs:145,155,172,185`; `QueryCommandExtensions` has no WriteJson) vs Csv siblings with `params` (`EntityBuilderExtensions.cs:99,113`; `QueryCommandExtensions.cs:409,446`); (e) empty-select throws `InvalidOperationException` at `JsonShapePlan.cs:174` (captured shape throws `NotSupportedException` at :170-172) → candidate normalization. MySQL boundary: `BIGINT UNSIGNED`→`System.UInt64`; declared `long` over `ulong.MaxValue` throws `OverflowException`, declared `ulong` preflight-rejected. Temporary harnesses `tests/**/D180*ReconTests.cs` removed; `git status --porcelain -- src tests` clean.
- 2026-10-09 | PLAN(r=2) | DO→PLAN adjudicated by planner: params parity gap confirmed -> r=2; D180.6 added; E180-16..18 added; rv=2 supersedes rv=1. No DO code yet for r=2.
- 2026-10-08T22:51Z | DO | r=2 | n=1 | D180.6 done — four public positional-SQL-parameter overloads added (`EntityBuilderExtensions.cs:223,247`; `QueryCommand.TResult.cs:207,234`): `WriteJson(Stream, JsonStreamOptions, CancellationToken, params ReadOnlySpan<object?>)` and `WriteJsonAsync(Stream, JsonStreamOptions, CancellationToken, params object?[])`, options+token required (no defaults), `null`→`DBNull`, empty binds nothing; XML-doc on all four. E180-17 compatibility: old + new call shapes + `null` compatibility compile/run. Build exit 0, 0 warnings/0 errors across Debug+Release. Tests exit 0: `JsonStreamingTests` 113 passed/0 failed/0 skipped (8 new overload/param cases incl. `WriteJson_NullOptions_ShouldResolveExistingOptionsOverload`, `WriteJsonAsync_OptionsAndToken_ShouldResolveExistingOverload`); core `JsonShapeWriterTests|InMemoryTests` 202 passed/0 failed/0 skipped. Evidence: `TestResults/D180/r2/rv2/axis1.build.log`, `axis2.build.log`, `boundary.build.debug.log`, `boundary.build.release.log`, `axis1.core.log`, `axis2.core.log`, `axis1.sqlite.log`, `axis2.sqlite.log`, `evidence.json`.
- 2026-10-08T22:51Z | DO | r=2 | n=1 | D180.2 done — empty-select now `NotSupportedException` `[projection]` (`JsonShapePlan.cs:173`, was `InvalidOperationException`); all preflight messages normalized to `JSON streaming validation [<token>]: <context>.` (`JsonShapePlan.cs`, `JsonRowWriterFactory.cs`, `QueryCommand.TResult.cs:260`); `JsonRowWriterFactory.ValidateReaderBinding` added; incompatible reader schema now rejected before any destination write including framing — one-row look-ahead + `ValidateReaderBinding` before `new JsonStreamWriter` at `QueryExecutor.cs:1173-1177` (sync) and `:1214-1218` (async). Sentinel-before-write ordering proven by `UnsupportedShape_/UnsupportedColumn_/IncompatibleReaderSchema_(Async)_ShouldRejectBeforeOutputKeepingSentinel`. Evidence: `TestResults/D180/r2/rv2/axis2.sqlite.log`, `axis2.core.log`, `boundary.build.debug.log`.
- 2026-10-08T22:51Z | DO | r=2 | n=1 | D180.3 done — reader-binding guard in place (incompatible provider field type → `[reader-binding]` after reader open, before first write); no boxed numeric fallback added, declared `sbyte`/`ushort`/`uint`/`ulong` stay preflight-unsupported, numeric whitelist unchanged/narrowed by bindings; J1 + ClickHouse decimal preserved; value-dependent overflow stays read-time. Evidence: `TestResults/D180/r2/rv2/unit.clickhouse.log`, `unit.postgres.log`, `unit.sqlserver.log`, `unit.mysql.log`, `unit.mariadb.log`, `axis1.build.log`.
- 2026-10-08T22:54Z | DO | r=2 | n=1 | D180.4 in progress — boundary sweeps (incompatible-reader sentinel first/middle/last, empty/multiple/null positional values, sync/async, null-option resolution) green so far; full boundary matrix + measurements not yet complete. Evidence: `TestResults/D180/r2/rv2/boundary.build.debug.log`, `boundary.build.release.log`.
- 2026-10-08T22:54Z | DO | r=2 | n=1 | D180.5 done — EN/RU docs + API reference + roadmap tracking for the four `params` overloads and the unified `NotSupportedException` token contract; E180-18 evidence `TestResults/D180/r2/rv2/docfx.log` (see row). Files: `docs/guide/28-streaming-data.md`, `docs/ru/guide/28-streaming-data.md`, `docs/guide/14-json.md`, `docs/ru/guide/14-json.md`, `docs/advanced/api-reference.md`, `docs/ru/advanced/api-reference.md`, `docs/specs/roadmap/todo_json_streaming.md`. `docs/guide/26-large-objects.md` + RU left untouched (no JSON-validation wording).
- 2026-10-09T04:15Z | DO | r=2 | n=2 | CHECK r=2 n=1 returned FAIL on W3 (empty-result bypass of the `[reader-binding]` guard: `ValidateReaderBinding` ran only when the one-row look-ahead `hasRow == true`, so an empty incompatible result emitted `[]`). n=2 fix: `ValidateReaderBinding` now runs unconditionally after the reader is opened and before the framing writer/any destination write, in both sync and async paths; peeked-row emission preserved and compatible empty results still write `[]`. Closed W3 (4 empty-result tests: incompatible sync/async sentinel-unchanged + compatible sync/async `[]`), W2 (empty-select `[projection]` test reaching `JsonShapePlan.cs:173` via the reachable internal `JsonShapePlan.Build` trigger; no public query surface lowers a zero-column projection), W1 (independent null→`DBNull` oracle: observed `DbParameter.Value` plus SQL-NULL row semantics, fresh and reused/indexed indices), S5 (`<exception cref="ArgumentNullException">` on the four `params` overloads), S6 (positional-params matrix: multiple / empty / null on both `QueryCommand<TResult>` and `EntityBuilder<TEntity>`, sync and async, each independently asserted). S3 left open (optional/defensive fallback message field type). Red→green: `TestResults/D180/r2/rv2/n2.red.log` (2 failed, exit 2 without the guard); green `TestResults/D180/r2/rv2/n2.build.log` (exit 0, 0 warnings/0 errors), `n2.core.log` (exit 0, 203 pass) / split `n2.core.shapewriter.log` (41) + `n2.core.inmemory.log` (162), `n2.sqlite.log` (exit 0, 125 pass); report gate `n2.evidence.json` exit 0.

- 2026-10-09T04:37Z | CHECK re-gather #2 | r=2 | n=2 | E180-01 SQL Server reader assertions via shared integration suite (`SqlServerIntegrationTests : CommonTestSuite` → `CommonTestSuite.JsonStream.cs`); E180-05 roslyn signatures resolved (new `params` overloads + unchanged old overloads + no `where` constraints); E180-09 all touched files CRLF-only, 0 bare-LF, `git diff --check -- src tests docs` exit 0; E180-11 exit codes all 0 (`boundary.all.n2.log`, `boundary.build.debug.n2.log`, `boundary.build.release.n2.log`); artifacts persisted to `TestResults/D180/r2/rv2/` (`E180-01-sqlserver.md`, `E180-05-signatures.md`, `E180-09-crlf.md`, `crlf-fix.core.log`).
- 2026-10-09T04:40Z | ACT | r=2 | n=2 | D180 finalized — CHECK PASS r=2/n=2/rv=2; Q7 marked resolved in `docs/specs/roadmap/todo_json_streaming.md`; committed to branch `1.0.9-rc2` as `93714a8e`; #180 closed.

## Done / Verified

- Done: D180 r=2 (all units D180.1–D180.6).
- Verified: yes — CHECK PASS r=2/n=2/rv=2.
- Q7 resolved (CHECK passed); #180 closed.
- Next allowed step: ACT complete — D180 closed, no further DO.
