# D185 — BindEntity<T> does not register the entity mapping

- task_id: D185
- issue: #185 — https://github.com/AlexeyShirshov/nextorm/issues/185
- selected_variant: pdca-dotnet
- base: `18659e41` (branch `1.0.9-rc2`)
- cycle: N=1
- plan revision: r=2 (evidence contract `rv2`, supersedes `rv1`; issued within the initial PLAN gather→decide, no loop-back)
- plan_state: **ready**
- phase: ACT complete — EXIT
- artifact root (expected): `/tmp/nextorm-D185-r2/`
- provenance: collection `1.0.9-rc2`, task D185; source brief `/tmp/opencode/rc2/briefs.md` §#185

## Goal (in essence)
`BindEntity<TEntity>(...)` on a raw `FromSql`/`From(string)` source must be **self-sufficient**: it ensures `TEntity`'s entity mapping is registered on the context, exactly as `From<TEntity>()` would, so typed member projections and entity materialization work with NO prior `From<TEntity>` and with parity to the currently-registered path. The fix reuses the existing `From<T>` registration path; it must preserve a configured `From<T>(cfg)` mapping and must not corrupt the TVP auto-resolution path.

## Scope
In scope: the registration side-effect inside `BindEntity`; configured-mapping precedence; the two metadata caches' interaction (`DataContextCache.Metadata` vs `DataContextCache.TvpMetadata`); typed SQL generation; class entity materialization; regressions on all providers; EN/RU documentation.
Out of scope: a new caching model; fixing the TVP resolver; interface whole-entity materialization beyond existing support; raw SQL in InMemory; public API / plan-cache changes; new issue/milestone.

## Acceptance criteria (each with a negative case)
- **R01** On a genuinely cold `Metadata` (no prior `From<IComplexEntity>`), `ctx.FromSql("select id, somestring from complex_entity").BindEntity<IComplexEntity>(["id","somestring"]).Select(t => t.Id)` produces `select id from (select id, somestring from complex_entity)`. An empty identifier, a preparation exception, or hidden pre-priming = FAIL.
- **R02** SQL and results of the bound query match a separately verified variant after `From<T>`. A repeated bind does not change the mapping or the result.
- **R03** A class entity materializes without prior `From<T>`; all selected values (including a nullable value) are checked. A ctor exception, a default instead of the value, or dependence on a warm cache = FAIL.
- **R04** A pre-existing `From<T>(cfg)` keeps its configured column names, converters and filters (specifically a name that differs from the attribute/default). Falling back to auto mapping or overwriting the entry = FAIL.
- **R05** TVP-only auto-resolution leaves `Metadata` without an entry, uses `TvpMetadata`, defers to a configured mapping, and stays cleared by `Clear()`. TVP-warm → Bind registers a mapping; a TVP-only entry does not count as registration.
- **R06** Unsupported/unmappable binding and a missing required output column still produce the original explicit diagnostics. A valid convention mapping must not be declared "unmapped"; empty SQL or silent default materialization = FAIL.
- **R07** `TableAlias`, column normalization/validation and unsupported InMemory keep their prior behavior. New `TableAlias` registration or bypassing the original refusal = FAIL.

## Minimal solution / alternatives
- What is needed: register entity metadata before typed use of the returned builder.
- What is unchanged: configured mapping wins; the TVP resolver never writes to `Metadata`; existing validation runs in the prior order.
- Minimum: after creating the binding, inside the existing `TEntity != TableAlias` branch, when the type is absent from `Metadata`, invoke the existing **`From<TEntity>` registration path** via the available `source.DataProvider`, then return the original bound builder.
- Chosen: guarded call to the existing `From<TEntity>` registration path (same public registration path; no new abstraction).
- Deferred: direct generic-resolver invocation — only if the chosen path proves costly.
- Rejected: redirecting the non-generic `ResolveMetadata` into `Metadata` — violates the TVP invariant.
- Registration is **process-wide**, like `From<T>`; an already-registered type is a registration no-op. No new types/interfaces and no `QueryCommand.Cache` change.

## Ordered tasks (fix now)
1. **D185.1 — normative input:** record the supplied `pdca-dotnet`/`nextorm-pdca` normative sections and map obligations to this contract (coder persists the plan + contract before DO).
2. **D185.2 — cold-state regression:** `RawSourceBindingMetadataTests.cs`, sqlite `RawSourceBindingSqlGenerationTests.cs`; add a cold/warm comparison, prove a cold `Metadata`, obtain red against the original production version. Cache isolation is test-only, with state restoration.
3. **D185.3 — registration:** `EntityBuilderExtensions.cs:54–69`; implement the guarded registration, preserving the Normalize/binding order and the `TableAlias` guard. Do NOT modify `DataContextExtensions.cs:315–348`.
4. **D185.4 — cache precedence:** core metadata/filter tests and existing `TvpMetadataCacheTests`; check configured, TVP-only, TVP→Bind, configured→TVP→Bind, repeated bind, and late explicit configuration against current `From<T>` semantics.
5. **D185.5 — SQL parity:** existing `RawSourceBindingSqlGenerationTests.cs` of all six providers; cold typed projection, auto/configured mapping, both raw entry points.
6. **D185.6 — runtime:** `CommonTestSuite.RawSourceBinding.cs`, existing MariaDB/ClickHouse raw-binding tests; cold class materialization and values, then container-backed integration.
7. **D185.7 — fail-closed:** core contract/filter tests, sqlite diagnostics and EF fail-closed regressions; preserve the negative cases and unsupported InMemory.
8. **D185.8 — docs & evidence:** update EN/RU, run build/full-suite/coverage, branch delta, directed mutations and separate code/test/doc/perf stream evidence + mutation (Stryker.NET); hand to CHECK without declaring D185 complete.

## Variant matrix (derived from the execution path)
- Cold interface + typed projection, both `FromSql` and `From(string)` → **test** R01/R02.
- Cold class + typed projection / whole entity → **test** R02/R03.
- Registered auto mapping + interface/class → **test** cold/warm parity.
- Registered configured mapping + projection/materialization → **test** R04 (converter/filter).
- Interface + whole entity → **test** baseline parity; new support **deferred** on a separate requirement.
- TVP-only cold; TVP-warm→Bind; configured→TVP→Bind → **test** R05.
- Bind-auto→explicit `From<T>(cfg)`→re-bind → **test** current `From<T>` semantics, R04.
- `TEntity == TableAlias`; repeated bind → **guard + test** R07/R02, no extra registration.
- Null/empty/invalid columns; missing mapped column; unmappable type → **guard + test** existing refusals R06/R07.
- Nullable/default values and converter provider type → **test** R03/R04; value-type entity **deferred** until a confirmed supported scenario.
- InMemory raw source → **guard + test** the original unsupported refusal.
- Shared-command/cache flags; concurrent reconfiguration → **guard**, no change; a new concurrency guarantee **deferred** until a separate requirement/reproducible defect.

## Footprint (expected + uncertainty)
- Production: `src/nextorm.core/Builders/EntityBuilderExtensions.cs`.
- Consumer/cache/TVP files (`DataContextExtensions.cs`, `DataContextCache.cs`, `QueryPreparer.cs`, `MemberTranslator.cs`, `RowMaterializerBuilder.cs`, `RawMapperFactory.cs`) — read-only control; any additional production edit requires a justified replan.
- Tests: `tests/nextorm.core.tests/RawSourceBinding{Tests,MetadataTests,FilterTests}.cs`; `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/RawSourceBindingSqlGenerationTests.cs`; acceptable additional existing targets `tests/**/TvpMetadataCacheTests.cs`, `tests/**/CommonTestSuite.RawSourceBinding.cs`, `tests/**/MariaDbRawSourceBindingTests.cs`, `tests/**/ClickHouseQueryFilterTests.cs`; diagnostics/EF tests run first, edited only for a necessary regression.
- Docs: `docs/guide/12-raw-sql.md`, `docs/ru/guide/12-raw-sql.md`.
- **Uncertainty:** whether registering in `Metadata` alone is sufficient for interface whole-entity materialization, or whether parity requires more, is NOT yet confirmed; it is closed by parity tests vs the registered path, not by expanding the footprint. Exact new line numbers after the diff are not invented now.

## Predecessor-result requirements
None identified: issue #185 is standalone. At subsequent grouping, any overlap with metadata/TVP tasks requires sequential execution or an agreed shared contract.

## Assumptions / prerequisites
.NET 10; `TreatWarningsAsErrors=true`; CRLF; CPM; static-cache test isolation (both caches are process-wide statics — tests must prove the initial state and exclude parallel interference); project test/coverage/Stryker tooling; `running-integration-tests` skill and a working `DOCKER_HOST`/container providers for runtime coverage; available CHECK lens agents; no API expansion and no shared-command mutation. A new context does NOT imply a cold cache (statics). Interface whole-entity materialization is not promised — verify parity with `From<interface>`, do not extend ctor support. The concrete "genuinely unmapped" fixture must be pinned from an existing explicit-refusal scenario before the fix.

## Test strategy
Unit: cache precedence, guards, fail-closed. SQL generation: all six providers (SQLite needs no DB). Integration: real class materialization — **SQL-generation-only is insufficient for R03**. Red↔green: the same added regressions against the original and fixed production versions; negative tests stay green; a warm-cache test is not a cold regression. Branch delta: map changed executable lines/branches to cases. Directed mutations (do not replace Stryker.NET): remove registration; accept a TVP entry as configured; force-overwrite a configured mapping; remove the `TableAlias` guard — each non-equivalent mutant must yield the expected failing test; restore afterwards. Coverage thresholds: line 85%, branch 75% (hard-fail on `main`; warning behavior on this branch, not a reason to hide the result). A P1 delta cannot remain unproven.
Focused selectors:
- `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~RawSourceBinding"`
- `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~RawSourceBindingSqlGenerationTests"` (analogous per provider)
- runtime: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test nextorm.slnx -c Debug`

## Docs plan
Both guides explain self-sufficient binding, process-wide registration, preservation of the configured mapping, and the need to set a *custom* configuration beforehand. Keep the "first call after a raw source" rule; add no references to `docs/specs/**`.

## Performance measurement decision
Registration lives in `BindEntity` (`EntityBuilderExtensions.cs:54–69`) and runs at builder time, not on the per-row materialization path (`Project`/`BuildRows`/`WriteJson`/`ToTypedArray` are untouched). The non-generic TVP resolver `DataContextExtensions.cs:315–348` is unchanged, so no per-row TVP benchmark is needed. However, because the project class-priority table treats this as a query-path change, `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` **IS required**: exactly 7 cases, no failures; compare the cached/prepared ratio with `docs/specs/performance/acceptance-benchmarks.md`; a comparable growth >20% requires a documented investigation, not an automatic hard fail.

## Reconnaissance decision
No prototype needed: the existing `From<T>` path (`DataContextExtensions.cs:1019,278`) is the normative baseline. Verifying the chosen call and the red regression is implementation verification, not an architectural spike.

## Unit execution mode
Sequential, one tree: shared process-wide static caches and a single production contract make worktrees unhelpful. The interface-materialization risk is closed by parity tests, not by widening the footprint.

## Design checklist
- [DRY/SOLID, P1] fix now: close the gap between Bind and the existing registration by reuse of `From<T>`, without a second metadata-builder.
- [Cache isolation, P1] fix now: do not change the TVP resolver and do not clobber a configured mapping.
- [Type design, P2] guard: no new production types/API; test isolation is acceptable as an observable seam for cache scenarios.
- [Perf anti-patterns, P1] guard: no per-row reflection, repeated auto-build, shared-command mutation or cache-disabling.
- [Structural sealedness, P2] deferred: no sealing inventory in the pack; no new production types. Revisit only when a type is added.

## Evidence contract `rv2` (`rv2 supersedes rv1`; all R01–R07 and E01–E06 IDs retained; artifact root `/tmp/nextorm-D185-r2/`)
All rows are `planned` evidence except E00 (normative input received). Every row carries: stable row/requirement ID, scenario, evidence kind/source, exact invocation + required exit/result/log, expected artifacts, owner, observable applicability, `rv=2`, priority. The DO ledger records actual evidence per row; failed/not-run/missing/blocked are recorded explicitly. N/A requires a demonstrated predicate and never waives an unconditional obligation.

| E-ID / req | Check; evidence | Invocation → required result | Artifacts (root) | Owner; applicability |
|---|---|---|---|---|
| E00 / GATE | Normative contract supplied by user excerpts | accept and record source → received | `normative.md`, this §E00 | orchestrator; always; P1 |
| E01 / R01–R07 | Build diagnostics | `dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings/0 errors | `build.log` | coder; always; P1 |
| E02 / R02,R04–R07 | Core tests, cold-state assertions | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~RawSourceBinding"` → exit 0, selected>0 | `core.log` | coder; always; P1 |
| E03-SQ / R01,R02,R04 | SQLite SQL assertions | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~RawSourceBindingSqlGenerationTests"` → exit 0, selected>0 | `sqlite.log` | coder; always; P1 |
| E03-PG / R02,R04 | PostgreSQL SQL assertions | same filter in `tests/nextorm.postgres.tests` → exit 0, selected>0 | `postgres.log` | coder; always; P1 |
| E03-SS / R02,R04 | SQL Server SQL assertions | same filter in `tests/nextorm.sqlserver.tests` → exit 0, selected>0 | `sqlserver.log` | coder; always; P1 |
| E03-MY / R02,R04 | MySQL SQL assertions | same filter in `tests/nextorm.mysql.tests` → exit 0, selected>0 | `mysql.log` | coder; always; P1 |
| E03-MA / R02,R04 | MariaDB SQL assertions | same filter in `tests/nextorm.mariadb.tests` → exit 0, selected>0 | `mariadb.log` | coder; always; P1 |
| E03-CH / R02,R04 | ClickHouse SQL assertions | same filter in `tests/nextorm.clickhouse.tests` → exit 0, selected>0 | `clickhouse.log` | coder; always; P1 |
| E04 / R03,R05–R07 | Full suite, existing TVP/EF/diagnostics, runtime providers | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test nextorm.slnx -c Debug` → exit 0; required provider scenarios executed, not skipped | `suite.log`, provider matrix | coder; always; P1 |
| E05 / R01–R07 | Red↔green + directed mutations | E02/E03/E04 invocations, unchanged between baseline/fix/mutants → baseline/mutant nonzero for the expected assertion, fixed/restored zero | `red-green.md`, `mutations.md`, logs | coder; changed registration/guards exist; P1 |
| E06 / COVER | Coverage + branch delta | `DOCKER_HOST=... dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" --settings coverage.settings.xml -f cobertura -o /tmp/nextorm-D185-r2/coverage.xml` and `reportgenerator ...` → both exit 0, thresholds reported | `coverage.xml`, report, `delta.md` | coder; always; P1 |
| E07 / DOC,DESIGN | Completeness of the four unconditional streams (code audit, test, doc, perf; security via E13) | `check`: reconcile contract with the DO ledger and E08–E13 → explicit green of each mandatory stream and actual evidence for each applicable row | `check-completeness.md`, `review.md` | check; always; P1 |
| E08 / LENS-TEST | Test lens: run/coverage/branch delta/CRAP + inner-loop report | `dotnet-testing-specialist` raw findings on the D185 diff/test evidence; `python3 scripts/validate_inner_loop.py report` → exit 0, no fail-closed errors | `test-lens.md`, `inner-loop-report.log`, `branch-delta.md`, `crap.md` | testing specialist + coder; always; P1 |
| E09 / MUTATION | Stryker.NET on touched core type | from `tests/nextorm.core.tests`: `dotnet stryker --project nextorm.core --mutate "**/Builders/EntityBuilderExtensions.cs" --reporter html --reporter json` → exit 0, scope confirmed; survivors on new code killed or individually justified | `mutation.log`, reports copied to `mutation/`, `survivors.md` | coder; registration/guards changed — true; P1 |
| E10 / LENS-CODE | Raw code-audit candidates (build, suppression/slop, smells, public API/CS1591) | `dotnet-code-review-agent` audits the D185 diff → raw candidates with locations; E01 build 0/0; `check` classifies | `code-audit.md`, `build.log`, `code-judgment.md` | code-review-agent + coder; always; P1 |
| E11 / LENS-DOC | EN/RU narrative + API-doc consistency | `dotnet-docs-generator` + `dotnet-api-docs` on both guides/diff → findings recorded and resolved | `docs-narrative.md`, `docs-api.md`, `docs-judgment.md` | both doc streams; always; P1 |
| E12 / LENS-PERF | Raw benchmark numbers, baseline and ratio | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` → exit 0, exactly 7 cases, no failures; ratio vs baseline, no verdict | `perf/run.log`, BDN artifacts, `perf/baseline.md`, `perf/comparison.md`, optional `perf/investigation.md` | coder + perf gather; always lens; measurement applicable — true; P1 |
| E13 / LENS-SECURITY | Raw-source/input-boundary audit: fail-closed, identifier/output-column handling, no validation bypass | `security-auditor` audits only the D185 diff and the directly affected raw-binding boundary → raw findings/exclusions with locations; `check` resolves | `security-audit.md`, `security-judgment.md` | security gather + check; diff touches public raw-source binding/input boundary — true |
| E14 / CLASS-STREAMING | Project row: streaming terminal | `check`: verify the diff for a new/changed streaming terminal → N/A only with a demonstrated absence | `class-priorities.md` §streaming | check; expected false; P1 |
| E15 / CLASS-SQL | Project row: SQL function/operator | `check`: verify the diff for a new/changed SQL function/operator → N/A only with a demonstrated absence | `class-priorities.md` §sql | check; expected false; P1 |
| E16 / CLASS-QUERY | Query-path floor: cached/prepared ratio, no shared-command mutation, temp-table/TVP branch evidence | execute E04/E06/E12; `check` maps the branch report to temp-table/TVP paths and verifies no shared `QueryCommand` change → all obligations explicitly closed, uncovered branches listed | `class-priorities.md` §query, `temp-tvp-branches.md`, links to E04/E06/E12 | coder + check; query-path registration change — true; P1 |

If mutation/metric tooling is unavailable, the ledger explicitly records the problem and the untested branches; that is NOT an automatic waiver and does not permit declaring a stream green.

## CHECK re-gather budget and completeness
- Owner: `check`; budget: at most **2 targeted re-gather requests** per CHECK, each for one missing/conflicting evidence bundle.
- Missing reporting is re-gathered inside CHECK: no product FAIL, no new DO, no `rv` change solely for missing reporting.
- If the budget is exhausted with applicable rows open → **CHECK unresolved**, list the E-IDs/reasons and route via blocker/escalation; PASS is forbidden while any applicable mandatory row is open.
- A new mandatory variant → justified CHECK→PLAN, explicit supersession, existing IDs retained and new IDs added.

## Risks / confidence
- Risks: false cold-state from static caches; configured/TVP precedence; interface expectations; provider skips. Each is closed by a dedicated scenario, not a green assumption.
- Confidence: high in the solution; the normative gap is closed, so gate 1 passes. Future test/tool results remain planned evidence, not unresolved requirements.

## Plan self-check
gate1 items present = **yes** (goal; acceptance R01–R07 incl. negative cases; concrete ordered task list; test strategy + variant matrix; docs plan; perf-measurement decision; reconnaissance decision; unit execution mode; design checklist; evidence contract `rv2` with all mandatory slots; risks). Phase: PLAN complete — STOP at PLAN→DO.

## Progress log

- 2026-10-09 | DO | plan r=2 | N=1 | DO started | —
- 2026-10-09 | DO | plan r=2 | N=1 | D185.1–D185.8 closed: production fix in `src/nextorm.core/Builders/EntityBuilderExtensions.cs` (guarded `ResolveMetadata<TEntity>`/`IsRegisteredInMetadata` self-registration; table-alias guard preserved; TVP resolver untouched) | files below
- 2026-10-09 | DO | plan r=2 | N=1 | tests added: `tests/nextorm.core.tests/RawSourceBindingMetadataTests.cs`, `tests/nextorm.core.tests/TvpMetadataCacheTests.cs` (RawSourceBindingTvpRegistrationTests), `tests/nextorm.sqlite.tests/RawSourceBindingSqlGenerationTests.cs`, 5 provider `RawSourceBindingSqlGenerationTests.cs` (postgres/sqlserver/mysql/mariadb/clickhouse), `tests/nextorm.integration.tests/CommonTestSuite.RawSourceBinding.cs` (RawBindColdEntity, materialized via `.ToList()` — `.Select(x=>x)` is a documented pre-existing engine limitation, code-smells-review.md:6847) | —
- 2026-10-09 | DO | plan r=2 | N=1 | builds Debug/Release 0 Warning(s) 0 Error(s) | `/tmp/nextorm-D185-r2/build*.log`
- 2026-10-09 | DO | plan r=2 | N=1 | red↔green: production reverted → core 3 fail (`red-core.log`), sqlite 3 fail (`red-sqlite.log`); fixed → 86/0, 26/0 | `/tmp/nextorm-D185-r2/`
- 2026-10-09 | DO | plan r=2 | N=1 | boundary sweep: core 1896/0/0, sqlite 1298/0/1skip, postgres 818/0/0, sqlserver 748/0/0, mysql 318/0/0, mariadb 242/0/0, clickhouse 606/0/0; one comprehensive `dotnet test nextorm.slnx -c Debug --no-build` = 9695 total / 0 failed | `/tmp/nextorm-D185-r2/`
- 2026-10-09 | DO | plan r=2 | N=1 | container integration: `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~RawSourceBinding"` = 29/29 passed, 0 skipped (all providers) | `integration-rawsourcebinding-rerun.log`
- 2026-10-09 | DO | plan r=2 | N=1 | coverage: changed lines `EntityBuilderExtensions.cs:78–91` 100%; core assembly line 86.9% / branch 79.3% (≥85/75) | `/tmp/nextorm-D185-r2/`
- 2026-10-09 | DO | plan r=2 | N=1 | perf acceptance: 7/7, no failures, wall 48s; cached/prepared 2.10 vs baseline 1.87 = +12.3% (<20%) | `perf/comparison.md`
- 2026-10-09 | DO | plan r=2 | N=1 | directed mutations M1–M4 mapped to tests (`mutations.md`); Stryker.NET unobtainable under MTP/xunit-v3 → honest no-tool disclosure, mutation row not claimed green | `mutations.md`
- 2026-10-09 | DO | plan r=2 | N=1 | evidence: `evidence.json`; `validate_inner_loop.py report` exit 0 | `/tmp/nextorm-D185-r2/evidence.json`
- 2026-10-09 | DO | plan r=2 | N=1 | DO complete → entering CHECK | —
- 2026-10-09 | DO | plan r=2 | N=1 | iteration n=2 (CHECK→DO loop-back, evidence gate; no product defect) — R03 isolated cold class-materialization proof added in `RawSourceBindingColdRegistrationTests.ColdClassEntity_RawSql_ShouldMaterializeNullAndNonNullValues` (sqlite, cache-clear collection); integration R03 test renamed to `RawSourceBinding_BoundClassEntity_ShouldMaterialize` (no unestablishable cold claim) | `/tmp/nextorm-D185-r2/r03-isolated.log`
- 2026-10-09 | DO | plan r=2 | N=1 | W2 observation: a composed-receiver `BindEntity` throws `InvalidOperationException` after already registering `Metadata` (registration `EntityBuilderExtensions.cs:78-79` precedes the guard `EntityBuilder.cs:2032-2033`); no fix — frozen rv2 plan requires none | `/tmp/nextorm-D185-r2/w2-observation.log`
- 2026-10-09 | DO | plan r=2 | N=1 | r2n2 builds Debug/Release 0 Warning(s) 0 Error(s) | `/tmp/nextorm-D185-r2/build-r2n2.log`, `build-release-r2n2.log`
- 2026-10-09 | DO | plan r=2 | N=1 | r2n2 filtered: core 87/0/0, sqlite 27/0/0, isolated R03 1/1, W2 1/1; integration RawSourceBinding 29/29, 0 skipped (DOCKER_HOST) | `/tmp/nextorm-D185-r2/`
- 2026-10-09 | DO | plan r=2 | N=1 | evidence.json extended; `validate_inner_loop.py report` exit 0 | `/tmp/nextorm-D185-r2/evidence.json`, `validator-r2n2.log`
- 2026-10-09 | ACT | plan r=2 | N=1 | CHECK PASS r=2/n=2; evidence root `/tmp/nextorm-D185-r2/`; coverage changed-lines 100%, core 86.9/79.3; perf 7/7 ratio +12.3%; directed M1–M4 detected; E09 honest no-tool disclosure; E05/E10/E07 closed; W1/W2 accepted observations; S1/S3 accepted ℹ️ P3 | `/tmp/nextorm-D185-r2/`
