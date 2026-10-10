# D190 n=3/r1 — criteria ↔ evidence (evidence-only completion pass)

Cycle `rc2-190-join-whole-entity-1`; plan revision **r1**, iteration **n=3/3**. HEAD `d67e3b3d`
(branch `1.0.9-rc2`). CHECK verdict: **BLOCKED** (product W1 closed; PASS forbidden). This pass fixes
evidence only — no `src/` or test-logic changes.

Companion ledger: `artifacts/pdca/rc2-190/provenance.md` (every run's argv/exit/selected/log).
Raw run dirs: `artifacts/pdca/rc2-190/{n3-*,EV190-*}`. `$E = artifacts/pdca/rc2-190`.

Legend: `argv` is shown as a JSON array; `→` gives exit / selected / failed. Every criterion lists a
provenance line `cmd=… | cwd=/home/alex/sources/nextorm | ts=2026-10-08T… | sha=d67e3b3d`.
Test-project argv template (`--no-build`): `["dotnet","test","<proj>","-c","Debug","--no-build","--filter","<FQN>"]`.

---

## R190-01 — supported `ItemN` is a whole entity; ordered mapped columns; typed materialization

**Anchors:** `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:958` (`TryGetProjectionItemMember`),
`:1021` (`TryGetDirectEntityItem`), `:717-728` (final guard); `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:59,77`
(`TryBuildRootEntityItem`).
**Tests:** `tests/nextorm.sqlite.tests/EntityItemProjectionTests.cs:148` (Item2 exact `select t2…` prefix),
`:192` (Item1 `select t1.id, t1.name from`), `tests/nextorm.core.tests/EntityItemProjectionInMemoryTests.cs:53,83`
(typed item materialization); provider suites incl. arity 3–8:
`tests/nextorm.sqlite.tests/JoinWholeEntitySqlGenerationTests.cs:85,102,119,137,156,176` (+ identical files
under postgres/sqlserver/mysql/mariadb/clickhouse).
**Evidence:**
- `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~EntityItemProjectionTests"]` → 0 / 15 / 0 (`n3-split-sqlite-ei/run.log`).
- `… "--filter","FullyQualifiedName~JoinWholeEntitySqlGenerationTests"` per provider → 0 / 8 / 0 each (`n3-sql-{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}/run.log`).
- `["dotnet","test","tests/nextorm.core.tests",…,"--filter","FullyQualifiedName~EntityItemProjectionInMemoryTests"]` → 0 / 7 / 0 (`n3-split-core-ei/run.log`).
- provenance: `cmd=dotnet test (6 provider + 2 project single-class runs) | cwd=…/nextorm | ts=2026-10-08T20:59–21:07 | sha=d67e3b3d`.

## R190-02 — missing outer side → null; present side → entity; in-memory/SQL parity

**Anchors:** `RowMaterializerBuilder.cs:59,77` (all-NULL guard), `QueryCommand.QueryPreparer.cs:484-532` (in-memory direct item read).
**Tests:** `EntityItemProjectionTests.cs:174` (SQL outer null), `:148,192` (present); `EntityItemProjectionInMemoryTests.cs:68` (outer null), `:83` (present), `:97` (`DirectEntityItem_PresentAllDefaultEntity_ShouldNotBeNull`); integration `CommonTestSuite.JoinWholeEntity.cs:15,29,43` + `MariaDbJoinWholeEntityIntegrationTests.cs` + `ClickHouseJoinWholeEntityIntegrationTests.cs`.
**Evidence:**
- `… "--filter","FullyQualifiedName~EntityItemProjectionTests"` → 0/15 (`n3-split-sqlite-ei/run.log`).
- `… "--filter","FullyQualifiedName~EntityItemProjectionInMemoryTests"` → 0/7 (`n3-split-core-ei/run.log`).
- `["dotnet","test","tests/nextorm.integration.tests",…,"--filter","FullyQualifiedName~JoinWholeEntity_"]` (+DOCKER_HOST) → 0 / 20 / 0 skipped (`n3-split-int-direct/run.log`).
- provenance: `cmd=dotnet test (sqlite/core/integration single-class) | cwd=…/nextorm | ts=2026-10-08T21:05–21:07 | sha=d67e3b3d`.
**SQL-side present-all-default counterpart — explicitly NOT PRODUCED (reason):** on SQL a *present* row
always has a non-null primary key (`entity_child.id` is `integer primary key`, `entity_value.id` is the
key), so a present row can never have all mapped columns NULL; the all-NULL⇒null branch is exercised
only by the genuinely missing outer-join side (`:174`). The plan (r1 §4) documents the all-nullable
mapping ambiguity as pre-existing. The in-memory counterpart exists (`:97`) because the in-memory source
carries the object itself. No fabricated run.

## R190-03 — scalar/composite/nested/bare and mapping/ctor guards unchanged; non-entity/cast fail closed

**Anchors:** `QueryCommand.QueryPreparer.cs:607` (scalar branch, untouched), `:717-722` (unmapped throw), `:724-728` (cast throw);
`RowMaterializerBuilder.cs:256-315` (ctor paths).
**Tests:** `EntityItemProjectionTests.cs:218` (scalar red→green), `:245` (cancelled prep), `:266` (unmapped
throw), `:284` (no sticky `_dontCache`), `:311` (nested member), `:329` (cast rejection), `:346` (value
guard); `RowMaterializerBuilderTests.cs:83,98,110`; `JoinReturningIdentityTests.cs` (24); postgres
`TypedCteSqlGenerationTests` / `JoinReturningIdentitySqlGenerationTests`.
**Evidence:**
- sqlite `EntityItemProjectionTests` → 0/15 (`n3-split-sqlite-ei/run.log`).
- core `RowMaterializerBuilderTests` → 0/6 (`n3-split-core-rm/run.log`); `JoinReturningIdentityTests` → 0/24 (`n3-split-core-jr/run.log`).
- integration regression: `…Cte_Typed_Heterogeneous…` → 0/4; `…Cte_Typed_SelfJoin…` → 0/4; `…JoinArities…` → 0/1 (`n3-split-int-cte*/`, `n3-split-int-joinar/run.log`).
- **Red:** before the final guard, `Select(p => (object)p.Item2)` emitted `selec from entity_parent as 't1' …` (captured probe); with the guard it throws `NotSupportedException … unrelated result type`. Scalar case red (gate disabled) = `QueryPreparationException: Cannot expand … 'Int32'` (`n2-red-scalar/run.log`, exit 2 / total 2 / failed 1), green exit 0 (`n2-green-scalar/run.log`).
- provenance: `cmd=dotnet test single-class (sqlite/core/integration) | cwd=…/nextorm | ts=2026-10-08T20:59–21:07 | sha=d67e3b3d`.

## R190-04 — plan/mapper identity; reuse; no alias/slot leak; no sticky cache disable

**Anchors:** `src/nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs:71-80,120-124` (entity+slot+member);
`src/nextorm.core/DataContext/RowMapperFactory.cs:475-478`; shared command untouched (`QueryCommand.Cache`, no diff).
**Tests:** `SelectExpressionPlanEqualityComparerTests.cs:164` (`Comparer_ShouldDistinguishDirectRootItemSourceExpressions`);
`PlanKeyUniquenessTests.cs:138-141` (direct variants), `:624` (`PlanCache_ShouldSeparateDirectEntityItemSlots`),
`:649` (`QueryPlan_ShouldReuseEquivalentDirectEntityProjection`); `EntityItemProjectionTests.cs:284`
(plan cache not disabled after a throw).
**Evidence:**
- core `SelectExpressionPlanEqualityComparerTests` → 0/12 (`n3-split-core-sc/run.log`).
- sqlite `PlanKeyUniquenessTests` → 0/13 (`n3-split-sqlite-pk/run.log`).
- sqlite `EntityItemProjectionTests` → 0/15 (`:284`).
- provenance: `cmd=dotnet test single-class | cwd=…/nextorm | ts=2026-10-08T21:05–21:07 | sha=d67e3b3d`.

## R190-05 — six providers; no skipped provider presented as pass

**Anchors:** `tests/nextorm.integration.tests/CommonTestSuite.JoinWholeEntity.cs:1` (shared, 4 `CommonTestSuite`
providers); `tests/nextorm.integration.tests/MariaDbJoinWholeEntityIntegrationTests.cs:1`;
`tests/nextorm.integration.tests/ClickHouseJoinWholeEntityIntegrationTests.cs:1`; six provider
`JoinWholeEntitySqlGenerationTests.cs`.
**Evidence (all `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`):**
- `…"--filter","FullyQualifiedName~JoinWholeEntity_"` → 0 / 20 / **0 skipped** (`n3-split-int-direct/run.log`).
- `…"--filter","FullyQualifiedName~MariaDbJoinWholeEntityIntegrationTests"` → 0 / 2 / 0 skipped (`n3-split-int-mariadb/run.log`).
- `…"--filter","FullyQualifiedName~ClickHouseJoinWholeEntityIntegrationTests"` → 0 / 2 / 0 skipped (`n3-split-int-clickhouse/run.log`).
- six provider SQL-shape `…JoinWholeEntitySqlGenerationTests` → 0 / 8 / 0 each (`n3-sql-*/run.log`).
- provenance: `cmd=dotnet test integration (DOCKER_HOST set) + 6 provider project runs | cwd=…/nextorm | ts=2026-10-08T20:59–21:07 | sha=d67e3b3d`.
- ClickHouse note: explicit outer join needs query-local `join_use_nulls=1` (dialect injects it only for
  implicit navigations); the test sets it and documents why (`ClickHouseJoinWholeEntityIntegrationTests.cs:44`).

## R190-06 — builds 0/0; coverage ≥85/75; branch delta; mutation outcome

**Anchors:** `coverage.settings.xml`; `docs/specs/performance/acceptance-benchmarks.md`.
**Evidence:**
- `["dotnet","build","nextorm.slnx","-c","Debug"]` → 0, 0 warnings / 0 errors (`n3-build/run.log`).
- `["dotnet","build","nextorm.slnx","-c","Release"]` → 0, 0 warnings / 0 errors (`n3-build-release/run.log`).
- `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal -c Debug --max-parallel-test-modules 1"]` → 0 / 9235 selected / 0 failed / 198 capability skips (`n3-coverage/run.log`).
- `reportgenerator …` → 0 (`n3-coverage-report/run.log`); **Line 88.1% (48xxx/54842), Branch 80.1% (26101/32545)** (`tests/coverage/report/Summary.txt`).
- **Branch delta (same full-sweep scope):** base `b447b7be` (`eb45e213~1`) full sweep → `QueryCommand.QueryPreparer` line 90.9% / branch 85.6%, `RowMaterializerBuilder` line 93.3% / branch 86.0% (`n3-branch-baseline-full/base-full.cobertura.xml`). Final → QueryPreparer line 91.3% / branch 85.2%, RowMaterializerBuilder line 93.3% / branch 86.6%. Δ = QueryPreparer line **+0.4pp**, branch **−0.4pp**; RowMaterializerBuilder line **0.0pp**, branch **+0.6pp** (within noise; no regression). Base sweep had 2 unrelated failures (ClickHouse native parity, timed-dictionary eviction) but produced the coverage XML (`n3-branch-baseline-full/sweep.log`).
- **Mutation:** not usable — see below and `provenance.md` M1–M6. Row open, PASS forbidden.
- provenance: `cmd=dotnet build/coverage/reportgenerator | cwd=…/nextorm | ts=2026-10-08T21:00–21:36 | sha=d67e3b3d` (baseline cwd=`/tmp/opencode/d190-base-full`, sha=`b447b7be`).

## R190-07 — acceptance benchmark exactly 7 cases, 0 failures, ≤240 s

**Anchors:** `benchmarks/nextorm.benchmark` `[BenchmarkCategory("acceptance")]`; baseline
`docs/specs/performance/acceptance-benchmarks.md:14` (command), `:22` (7 cases), `:47-62` (baseline table).
**Evidence:** `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--no-build","--","--anyCategories=acceptance"]`
→ exit **0**, **7 cases**, 0 failures, BDN `Global total time 43.67 s`, external wall 44 s (`n3-perf/run.log`).
Means/Allocated: `Nextorm_Count` 2.6 ms/374 KB; `Nextorm_GroupByCount` 67 ms/50 MB; `Nextorm_Cached`
2.18 ms/609 KB; `Prepared_ToList` 935.6 µs/76.14 KB; `Cached_ToList` 1,939.8 µs/583.19 KB;
`Cached_PlanOnly_Param` 648.6 µs/507.05 KB; `Nextorm_Cached_ToListAsync` 2.24 ms/608 KB.
**Cached/prepared ratio** = 1,939.8/935.6 = **2.07** (baseline 1.87, +10.7%, below the +20% investigation
threshold 2.244); alloc ratio 7.66.
provenance: `cmd=dotnet run benchmark | cwd=…/nextorm | ts=2026-10-08T21:03 | sha=d67e3b3d`.

## R190-08 — EN/RU docs and registers reconciled; no public→specs links

**Anchors:** `docs/advanced/limitations.md:20` + `docs/ru/advanced/limitations.md:20` (row no longer
forbids direct `Select(p => p.ItemN)`); `docs/guide/02-joins.md:104` + `docs/ru/guide/02-joins.md:106`
(direct example + outer-null note); `docs/specs/design/code-smells-review.md:1770` (now cites
`MemberTranslator.cs:573`); `docs/specs/design/API-NAMING-REVIEW.md` #190 audit entry.
**Evidence:** `dotnet tool run docfx docs/docfx.json` → exit 0, 2 pre-existing warnings (`EV190-DOCFX/run.log`);
no `specs/` links in the four public pages (rg over the four files returned none).
provenance: `cmd=dotnet docfx | cwd=…/nextorm | ts=2026-10-08T19:46 | sha=<n=1 tree>` (docs are unchanged
in n=2/n=3; anchors stable).

## R190-09 — scope, test safety, single boundary sweep, complete evidence

**Anchors:** structured scope `artifacts/pdca/rc2-190/test-scope.json`; evidence `artifacts/pdca/rc2-190/evidence.json`;
`docs/specs/status/rc2-190-join-whole-entity-1.md`.
**Evidence:**
- `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","brief","artifacts/pdca/rc2-190/test-scope.json"]` → **0** (`validate-inner-loop-brief.txt`, non-empty).
- `["python3",…,"report","artifacts/pdca/rc2-190/evidence.json"]` → **0**, report non-empty (208 bytes) (`validate-inner-loop-report-n3.txt`).
- `test-scope.json` selectors: 13 individual single-class filters, **no `|`**.
- one boundary sweep only (C1, `n3-coverage/run.log`); no broad inner-loop command.
provenance: `cmd=python3 validate_inner_loop.py | cwd=…/nextorm | ts=2026-10-08T22:01 | sha=d67e3b3d`.

---

## Mutation disclosure (R190-06 open)

Stryker.NET **5.0.0** (global). Six attempts (`provenance.md` M1–M6) are all unusable:
- default `vstest` runner + coverage capture: **rc 124** timeouts (M1 1167 mutants, M2 182 pending).
- `coverage-analysis: off` (M3/M4): **rc 0** but score **0.00%** — 182 tested, 182 survived, 0 killed;
  "22 tests found" indicates the filtered discovery did not run the tests.
- `test-runner: mtp` + `coverage-analysis: perTest` + `TreatWarningsAsErrors=false`, `mutate=QueryCommand.QueryPreparer.cs`
  (M6): **rc 124**; discovered **9204** tests (the `test-case-filter` is not honoured under MTP), 8158
  Safe-Mode compile errors (`CS0165 Use of unassigned local variable` / `CS8081` in unrelated visitor
  files), 1035 mutants pending.
Residual unverified mutants: `RowMaterializerBuilder.cs` 182, `QueryCommand.QueryPreparer.cs` 1035.
This is an environment/tooling limitation (Stryker 5.0.0 + Microsoft.Testing.Platform / xunit v3 +
mutant-control definite-assignment), not a product defect; no kills are fabricated.

## Follow-up

GitHub issue **#207** — uniform cancellation handling during projection build (W2 inherited, out of scope #190).
