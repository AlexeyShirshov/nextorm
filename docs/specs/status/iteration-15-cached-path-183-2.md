# Task #183 — Iteration 15 cached-path overhead (cycle 2, Stage A)

- collection: `1.0.9-b-5`, group-1
- worktree: `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1`
- branch: `collection/1.0.9-b-5/group-1`
- base commit: `01f3e92`
- milestone: `1.0.9-b`
- tracking: issue #183 — https://github.com/AlexeyShirshov/nextorm/issues/183
- design: `docs/specs/performance/iteration-15-cached-path-design.md`
- cycle: 2
- plan revision: r=2
- attempt: n=3 (CHECK attempt 3/3 FAIL on coverage branch baseline/delta absent → Escalated → decision a; accepted)
- phase: ACT (Stage A done)
- design approval provenance: commit `5137ec8`; owner approval 2026-10-04 (written-spec review passed)
- date: 2026-10-04

## Goal (Stage A of cycle 2)

Синхронизировать утверждённый дизайн и пройти Gate A, **не изменяя** production core; получить
воспроизводимую декомпозицию fresh-fluent cached-path (construction → prepare → lookup/equality →
captured-param refresh → execution), доказать репрезентативные cache hits и зафиксировать
baseline/workload/бюджеты перед B.

## PLAN (Stage A)

### Решение

Реализация #183 разрешена одобрением `5137ec8`; блокер ревью снят. Новый цикл: r=1, n=1. Старый
`-183-1.md` сохраняется.

> Revision note r1 → r2: collision-вариант разделён на Stage A guard и сохранённое обязательство проверки lookup в B1; остальные задачи и критерии неизменны; попытка n=1.

Цель Stage A: синхронизировать утверждённый дизайн и пройти Gate A, НЕ изменяя production core;
получить воспроизводимую декомпозицию fresh-fluent cached-path (construction→prepare→lookup/equality→parameter
refresh→execution), доказать репрезентативные cache hits и зафиксировать baseline/workload/бюджеты перед B.

### Приёмка Stage A (каждая с негативным случаем)

1. использован утверждённый дизайн из `5137ec8`;
2. Gate A выполнен — читаемые CPU/alloc-профили, plan/parameter counts, доказанные hits;
3. workload зафиксирован (SQL, типы/порядок параметров, строки, настройки, итерации, warm/cold,
   нормализация);
4. baseline/бюджеты заморожены до core-изменений;
5. обязательный perf acceptance — ровно 7 кейсов, без failures, с wall time/Mean/Allocated/ratios;
6. build 0/0, coverage line≥85/branch≥75;
7. filtered inner loop + один comprehensive sweep; provider-skipped ≠ pass;
8. нет sticky `Cache=false`/`_dontCache` (использовать `storeInCache:false`); нет подмены структурной
   проверки одним hash;
9. Stage A не меняет `src/nextorm.core`; изменение `CteHoister.Hoist`/nested read-CTE path → stop-guard #166.

### Стадии и gates

A (текущий цикл) → B1 (equality-scope allocations, `ExpressionPlanEqualityComparer.cs:98,218`) → B2
(guarded immutable refresh recipe, `QueryPlanner.cs:727-739`) → C (decision/handoff; если `PrepareCommand` —
крупнейший non-DB cost выше шума, отдельный дизайн M12 #3). Порядок B: после A первой идёт стадия с
большей доказанной долей устранимых allocations/CPU; при неразличимом в шуме вкладе — B1→B2.
Промежуточный ACT не закрывает #183.

### D: задачи Stage A (fix now, последовательно)

- A0 preflight gather (scout); **CLOSED 2026-10-04** — seams/commands established, design+plan synced (`docs/specs/performance/iteration-15-cached-path-design.md`).
- A1 approved-design sync + статус (coder); **CLOSED 2026-10-04** — approved design `5137ec8` synced; this cycle status written (plan r=2).
- A2 benchmark/diagnostic harness без core edits (coder; primary fresh Join, `Cached_PlanOnly_Param`,
  `Cached_ToList`; controls Any/First/Single/Where; prepared control; Iteration/LargeIteration symmetry;
  warm/cold; matched SQL/types/rows; отдельно timed runs и diagnostic batches; CPU/alloc профили;
  inclusive CPU не суммировать; если seams не хватает — вернуть PLAN, не внедрять незапланированную
  instrumentation); **CLOSED 2026-10-04** — `benchmarks/nextorm.benchmark/SqliteBenchmarkStageA.cs`, `StageADiagnostics.cs`; mandatory acceptance 7/7 exit 0 (wall 45.788s).
- A3 correctness characterization (coder; closures same shape/diff values; повторные captures/dedup/pN;
  WHERE/JOIN/projection, null, converter; IN/lookup изменение; Any/Count/All ↔ DML/CTE; structural
  mismatch при hash collision на lookup; nested shadowing/reentrancy; cache clear/lifetime; fallback
  errors/eval-once; recipe-специфичные проверки — Deferred до B2); **CLOSED 2026-10-04** — `tests/nextorm.sqlite.tests/CachedPathCharacterizationTests.cs`, `DataContextCacheClearTests.cs`, `tests/nextorm.core.tests/PlanKeyStructureTests.cs` (collision guard); full suites 0 failures.
- A4 измерения/manifest/runner (coder; новые `docs/specs/performance/iteration-15-stage-a-manifest.json`,
  `...-report.md`, `scripts/iteration15_evidence.py`; runner сохраняет фактические команды/версии/exit
  codes/вывод/длительность/provenance, не превращает skipped в success; большие traces не коммитить); **CLOSED 2026-10-04** — manifest/report/runner present; runner records commands/versions/exit codes/durations, skipped ≠ success.
- A5 boundary CHECK+ACT (coder→check). **CLOSED 2026-10-04** — boundary sweep green (see `## Stage A boundary evidence`); build 0W/0E, 0 failures, coverage 87.1/78.7, integration exit 0, gate exit 0, core diff empty.

### Матрица вариантов (наследуется B1/B2)

- fresh closures same shape/diff values — test + hit proof (B2: не использует первый closure);
- WHERE/JOIN/projection captures — test (fastpath/fallback equivalence);
- value/reference/null/default — test + guard (B2 recipe matrix);
- converter/rawSQL — baseline test + guard fallback; rawSQL: `CachedPathCharacterizationTests.RawSqlOverride_FormAndValueChange_ShouldKeyThePlanBySqlTextOnly` (form/value change + cache behavior; `SqlStmt` null → ExtractParams bypass); B2 fallback preservation — mandatory;
- IN/lookup форма/значения — test (shape refresh до plan selection, первоначально fallback);
- filters/prewhere/settings/nested shape — test/guard;
- Any/Count/All ↔ DML/CTE — interleaving test + sticky guard;
- Hash collision — **guard**: `PlanKeyStructureTests.EqualHashes_ShouldNotImplyEquality_ForQueryableConstant` проверяет equal-hash/structural-mismatch; это НЕ тест реального lookup. Реальный forced-collision lookup — **deferred**, триггер: B1 допускает добавление test-only seam.
- nested/shadowing/reentrant — test baseline (B1 cleanup + concurrency);
- cache clear/lifetime — test (B2 no retention);
- evaluation error/once — baseline test (B2 mismatch до evaluation);
- cold/warm, Iteration/LargeIteration, prepared — отдельные measured cases;
- providers — unit SQL matrix + реальные integration (skip запрещён);
- recipe-only fastpath / unsupported mismatch — **Deferred to B2**, trigger: appearance of the recipe; mandatory tests before B2 CHECK;
- #166 read-CTE — guard/deferred, триггер — изменение Hoist.

### Inner loop (exact, non-broad; включить реальные добавленные selectors после создания)

```bash
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JoinIntoPlanKeyTests|FullyQualifiedName~ExpressionPlanEqualityComparerNodeTests|FullyQualifiedName~PlanKeyStructureTests|FullyQualifiedName~QueryCacheControlsTests|FullyQualifiedName~Iteration14CteLookupTests"
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~PlanCacheTests|FullyQualifiedName~InListCacheTests|FullyQualifiedName~PlanKeyUniquenessTests"
dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~DataModifyingCtePlanCacheTests"
```

Boundary sweep: полные core/sqlite suites, all-provider SQL-generation, postgres CTE, integration с
DOCKER_HOST, coverage core/sqlite/postgres/sqlserver line≥85/branch≥75.

### Perf

Канонический acceptance `dotnet run --project benchmarks/nextorm.benchmark -c Release --
--anyCategories=acceptance`; ровно 7 кейсов, ≤4 мин, без сокращения workload; baseline
`acceptance-benchmarks.md:61-69` (Nextorm_Count 2.915ms/335.17KB; Nextorm_GroupByCount 60.95ms/50MB;
Nextorm_Cached 1.772ms/534.42KB; Prepared_ToList 923.8µs/76.14KB; Cached_ToList 1727.4µs/565.22KB;
Cached_PlanOnly_Param 518.3µs/489.08KB; Nextorm_Cached_ToListAsync 2.137ms/692.07KB); ratios к
Prepared_ToList; рост comparable ratio >20% → повторить/расследовать (не auto-fail); B — две
paired/interleaved run, reliable >20% regression на двух сопоставимых прогонах блокирует;
iteration14_gate.py продолжает проходить; 25% slack не становится acceptance.

### Разведка

Ограниченная, A0 устанавливает seams/команды, A2 — measurement spike (читаемые stacks/attribution,
проверяемые hit/count, совпавшие SQL/types/rows; наличие .nettrace — не успех).

### Evidence contract rv=1 (сводка плана)

G1-PERF (всегда, MEASURED: 7-case, профили, hits, paired baseline, frozen budgets, iteration14 guard;
coder собирает, check принимает); G1-CACHE (всегда: structural/cache/parameter characterization, no
sticky mutation, scope, build, coverage, provider runs; coder исполняет, scout подтверждает scope, check
принимает); G1-CTE (только при изменении Hoist/nested read-CTE; predicate=false → N/A с доказательством;
true → активный #166 prerequisite; scout semantic owner, orchestrator stop/re-scope, check проверяет).
CHECK re-gather budget: 2 точечных gather, владелец check; простое отсутствие отчёта → re-gather, не
ревизия/повтор DO.

### Docs

Public API не меняется; менять в Stage A только внутренние файлы (approved design sync, новый cycle
status, Stage A manifest/report, benchmark/test harness, evidence runner). Не трогать public EN/RU docs,
`benchmark-report.md`, старый cycle status, production core.

### Пробелы/риски

Profiler perturbation/inlining; нерепрезентативные hits; историческая variance; недоказанный collision
test; provider skips; случайное core/scope расширение. Отсутствие измеримости в DO → сначала точечный
scout; доступный инструмент/fixture можно добавить как аддитивное предусловие (исходный D active/blocked,
приёмка не меняется); стойкая низкая уверенность → рекомендация escalate (триггер 5).

### Remaining units

B1, B2, C — активны в текущем milestone; #166 deferred с trigger (при trigger relevant D блокируется на
активной зависимости); M12 #3 не включается скрыто в C.

## Evidence contract rv=2 (authoritative, final)

> rv=1 → rv=2 at ACT (CHECK attempt 3/3 FAIL only on coverage branch baseline/delta absent → Escalated → decision a); predicates/scope unchanged.

| row | applies | predicate | status |
|---|---|---|---|
| G1-PERF | #183 Stage A, всегда | **MEASURED** — ровно 7 acceptance-кейсов без failures; читаемые CPU/alloc-профили реального benchmark-процесса; plan/parameter counts; доказанные representative hits; paired baseline; frozen budgets; `eng/perf/iteration14_gate.py` проходит | **met** |
| G1-CACHE | #183 Stage A, всегда | structural/cache/parameter characterization; полное структурное равенство (в т.ч. hash collision); отсутствие sticky `queryCommand.Cache=false`/`_dontCache`; scope (нет `src/nextorm.core` изменений); build 0/0; coverage line≥85/branch≥75; реальные provider runs (skip ≠ pass) | **met** |
| G1-CTE | только если изменён `CteHoister.Hoist`/nested read-CTE path | predicate=false → N/A с доказательством (пустой diff); predicate=true → активный prerequisite #166, stop/re-scope | **N/A** — predicate=false; proof: `git diff 01f3e92 -- src` пуст (production `src` byte-identical) |

CHECK re-gather budget: **2** точечных gather, владелец — `check`; простое отсутствие отчёта →
re-gather, не ревизия плана и не повтор DO.

## Inner-loop discipline

- `scripts/validate_inner_loop.py` — **CONFIRMED ABSENT** в этом worktree (`ls` → No such file or
  directory); записано; цикл идёт под ручной дисциплиной, inner-loop evidence — реальные команды +
  exit code + числа + путь к логу.
- Внутренний цикл — только затронутый отфильтрованный поднабор (см. PLAN → Inner loop): затронутый
  проект собирается один раз, затем `dotnet test <project> --no-build --filter <selector>`.
- Один comprehensive boundary sweep на границе DO→CHECK (полные core/sqlite suites, all-provider
  SQL-generation, postgres CTE, integration с DOCKER_HOST, coverage line≥85/branch≥75).
- Provider-skipped ≠ pass.

## Stage A boundary evidence

- Build `dotnet build nextorm.slnx -c Debug`: **exit 0**, 0 Warning(s) / 0 Error(s) — log `/tmp/nextorm-a5/build.log`.
- Unit + provider suites all pass, **0 failures**: core 1482P; sqlite 992P / 1S (env-gated LobStream probe); postgres 742P; sqlserver 556P; mysql 257P; mariadb 157P; clickhouse 491P — logs `/tmp/nextorm-a5/test-*.log`.
- Coverage aggregate: **line 87.1% / branch 78.7%** — artifacts `tests/coverage/` (`report/Summary.txt`, `coverage.cobertura.xml`).
- Integration with `DOCKER_HOST`: **exit 0**; 4 DB providers executed, 0 failed (total 3136, errors 0, skipped 193 capability-based) — log `/tmp/nextorm-a5/integration.log`.
- `eng/perf/iteration14_gate.py`: **exit 0** (all 56 row/job verdicts within budget) — log `/tmp/nextorm-a5/iteration14_gate.log`.
- Mandatory acceptance: **7/7 cases exit 0**, wall 45.788s — manifest `docs/specs/performance/iteration-15-stage-a-manifest.json`, report `docs/specs/performance/iteration-15-stage-a-report.md`.
- `git diff 01f3e92 -- src/nextorm.core`: **empty** → G1-CTE predicate=false.
- rawSQL boundary: `CachedPathCharacterizationTests.RawSqlOverride_FormAndValueChange_ShouldKeyThePlanBySqlTextOnly` (`tests/nextorm.sqlite.tests/CachedPathCharacterizationTests.cs:272`) — SQL-text-only plan identity, form/value change; prepared `SqlStmt` null → `ExtractParams` bypass; B2 fallback preservation is a mandatory obligation (report §8).
- param-refresh attribution (`manifest.paramRefreshAttribution`): refresh-needed **6528.104 B/op** vs no-refresh control **5184.188 B/op**; delta **1343.916 B/op exact across 3 runs** (time indicative only, 48.3–57.6 µs/op). Exact isolation via immutable refresh-recipe instrumentation is **deferred to B2**, trigger: the recipe exists.
- Sticky-state guard: `StageADiagnostics.cs:355-358` asserts the shared `QueryCommand` stays cacheable (`stickyCommand.Cache == true` after `GetPreparedQueryCommand`); runner `cmd_correctness` fails closed unless `sticky_cache_false_absent == true` (`scripts/iteration15_evidence.py:403-404,459-460`) — no `queryCommand.Cache=false` / `_dontCache` mutation.
- Suppression/slop audit: see `## Suppression/slop audit (Stage A changed files)` below.

## Suppression/slop audit (Stage A changed files)

Full Stage A changed-file set reviewed: 6 `.cs` files (**1391** total lines) plus `scripts/iteration15_evidence.py` (632 lines; scanned, 0 matches for every C#-specific pattern). Command: `rg -c -- '<pattern>' <files>`.

| pattern | count | denominator (lines) | ratio | locations |
|---|---:|---:|---:|---|
| `#pragma warning` | 0 | 1391 | 0.000 | — |
| `SuppressMessage` | 0 | 1391 | 0.000 | — |
| `NoWarn` | 0 | 1391 | 0.000 | — |
| `TODO\|FIXME\|HACK\|XXX` | 0 | 1391 | 0.000 | — |
| `Debug.Assert` | 0 | 1391 | 0.000 | — |
| `async void` | 0 | 1391 | 0.000 | — |
| empty catch (`catch { }`) | 0 | 1391 | 0.000 | — |
| `Thread.Sleep` | 0 | 1391 | 0.000 | — |
| `GC.Collect` | 0 | 1391 | 0.000 | — |
| `Console.Write` | 4 | 1391 | 0.00288 | `StageADiagnostics.cs:89,90,91` (JSON emit on the diagnostics stdout channel), `Program.cs:36` (commented-out dev line) |

`// TODO`-style markers, suppressions, debug asserts, `Thread.Sleep`, explicit `GC.Collect` and empty catches are all **zero**; the four `Console.Write` hits are the intended benchmark/diagnostics output channel (`Emit` writes the STAGE-A JSON markers) plus one commented line — no production slop.

## Approved-design alignment (plan D:A0–A5)

Design `docs/specs/performance/iteration-15-cached-path-design.md` approval header: line **17** (`> **Approval (письменная спецификация):** 2026-10-04 …`).

- §5 Stage A outputs (design `:99-117`) → **D:A0** seams/preflight (`:102-104` decomposition contract), **D:A2** benchmark/diagnostic harness + valid real-process profiles (`:105-108`), **D:A4** versioned baseline/manifest + frozen budgets before core edits (`:109-113`).
- §9 Testing (design `:174-192`) → **D:A3** correctness characterization: existing suites (`:176-179`) plus closure-shape/IN/Any-DML collision/nested/cache-clear/fastpath rows (`:183-190`); forced-lookup-collision stays **deferred to B1** (plan collision row).
- §10 Performance acceptance (design `:194-214`) → **D:A2** exact Byte-alloc budgets + normalized workload (`:196-198`), **D:A4** manifest/runner, **D:A5** `iteration14_gate` + provider/integration verification (`:205-214`).

## Register delta (Stage A)

- `git diff --exit-code 01f3e92 -- docs/specs/design` → **exit 0** (empty): no design-register edits.
- No public API/surface change (Stage A touches only internal benchmark/test/evidence files); no claims added to `docs/specs/design/API-NAMING-REVIEW.md` or `docs/specs/design/code-smells-review.md`.

## CHECK / ACT history (cycle 2, Stage A)

- CHECK attempt 1/3: **FAIL** — evidence-gap items W1–W5, S1, S5–S7; closed in DO loop-back.
- CHECK attempt 2/3: **FAIL** — evidence-gap close (rawSQL boundary test, refresh attribution isolation, recipe-fastpath deferral); close budget 2/2.
- CHECK attempt 3/3: **FAIL only on "coverage branch baseline/delta absent"** — all other criteria met.
- `Escalated` → **decision (a): accept Stage A**.

## ACT finalization (escalation decision a)

- Verdict: **Stage A done** (via escalation decision).
- Evidence contract **rv=2**: **G1-PERF met**, **G1-CACHE met**, **G1-CTE N/A** (predicate=false with proof: `git diff 01f3e92 -- src` empty).
- Branch delta: N/A — production `src` byte-identical to `01f3e92` (`git diff --stat/--exit-code/--porcelain -- src` empty); baseline coverage not collected; absolute gates 87.1 line / 78.7 branch met unchanged.
- No replan, no STOP, no fourth CHECK.

## Progress log

- 2026-10-04T11:40Z | PLAN | revision r1 | iteration 1/3 | PLAN written (cycle -183-2, Stage A) | docs/specs/status/iteration-15-cached-path-183-2.md
- 2026-10-04T11:40Z | DO | revision r1 | iteration 1/3 | DO started (Stage A; A0 preflight gather → A1 design sync + status) | docs/specs/status/iteration-15-cached-path-183-2.md
- 2026-10-04T11:55Z | DO | revision r1 | iteration 1/3 | D:A2/A4 harness+acceptance+profiles+manifest done; runner added; build 0/0; acceptance 7/7 exit 0 (wall 45.788s); G1-CTE predicate=false | scripts/iteration15_evidence.py
- 2026-10-04T12:02Z | PLAN | revision r2 | iteration 1/3 | collision row reclassified (guard + deferred→B1); DO A5 started | docs/specs/status/iteration-15-cached-path-183-2.md
- 2026-10-04T12:16Z | DO | revision r2 | iteration 1/3 | A5 boundary sweep green: build 0W/0E; 7/7 provider suites exit 0 (1482/992/742/556/257/157/491 passed; 1 env-gated sqlite probe skip); core diff vs 01f3e92 empty (+CteHoister empty); iteration14_gate exit 0 (56/56 within budget) | /tmp/nextorm-a5/
- 2026-10-04T17:19Z | DO | revision r2 | iteration 1/3 | A5 integration sweep with DOCKER_HOST exit 0; 5 containers started/ready/stopped; global 3136 total, 0 failed, 0 errors, 193 skipped; skips are capability-based, per-provider skips PG 25 / MSSQL 43 / MySQL 79 / SQLite 43 / ClickHouse 0 — no provider skipped wholesale | /tmp/nextorm-a5/integration.log
- 2026-10-04T12:20Z | DO closed | revision r2 | iteration 1/3 | Stage A all D tasks closed; boundary evidence recorded; entering CHECK | docs/specs/status/iteration-15-cached-path-183-2.md
- 2026-10-04T12:27Z | DO loop-back (CHECK FAIL 1/3) | revision r2 | iteration 1/3 | W1-W5,S1,S5-S7 fixed; W4/S8 re-gathered | scripts/iteration15_evidence.py; docs/specs/performance/iteration-15-stage-a-manifest.json
- 2026-10-04T12:36Z | DO (evidence-gap close 2/2 budget) | revision r2 | iteration 1/3 | rawSQL test added (RawSqlOverride_FormAndValueChange_ShouldKeyThePlanBySqlTextOnly); refresh attribution separated (1343.92 B/op diagnostic delta) + limitation/trigger→B2 for exact isolation; recipe-fastpath deferral recorded | tests/nextorm.sqlite.tests/CachedPathCharacterizationTests.cs; docs/specs/performance/iteration-15-stage-a-manifest.json; /tmp/opencode/test-rawsql.log
- 2026-10-04T12:37Z | DO | revision r2 | iteration 1/3 | DO evidence-gap close finalized (budget 2/2) | audit counters + register delta + alignment + sticky guard recorded; entering CHECK 3/3
- 2026-10-04T12:42Z | ACT | revision r2 | iteration 3/3 | CHECK attempt 3/3 FAIL only on coverage branch baseline/delta absent; Escalated → decision (a) accept Stage A; Stage A done (evidence contract rv=2: G1-PERF/G1-CACHE met, G1-CTE N/A); no replan/STOP/fourth CHECK | docs/specs/status/iteration-15-cached-path-183-2.md

## Done / Verified / Remaining

- Done: **Stage A** (D:A0, A1, A2, A3, A4, A5 closed 2026-10-04; boundary green — see `## Stage A boundary evidence`).
- Verified: **Stage A (escalation decision a)** — build 0W/0E; suites 0 failures; coverage 87.1 line / 78.7 branch absolute; integration exit 0; gate exit 0; `src/nextorm.core` diff empty.
- Remaining: **B1, B2, C (active, same milestone)**; **#166 deferred with trigger (Hoist/nested read-CTE)**.
