# CCORR-201 — corrective task (raw-preparation regression)

- corrective-id: `CCORR-201`
- origin: collection `1.0.9-todo` stage **C (collection CHECK) — FAIL** (2026-10-15), branch `1.0.9-rc3` (origin recorded `df2b30ca` / actual HEAD `4a86e5e7`)
- source issue: #201 / T201 (cached-path parity), milestone `1.0.9-rc3`
- selected_variant: `pdca-dotnet`
- phase: **DONE — CCORR-201 done (CHECK PASS r=1/rv=1/n=2/3; collection C re-run pending)**
- evidence: `artifacts/pdca/collection-1.0.9-todo/C/` (C-report.md + raw logs)

## Defects to correct (do NOT fix in place)

### D01 — P1/High: global-filter policy reuse
- `src/nextorm.core/DataContext/QueryPlanner.cs:629` — raw preparation reuse (`ApplyPreparationFrom(recipePlan.QueryCommand)` + `CaptureRawGraph` + staged key) reuses a prior prepared predicate without evaluating the current context-dependent builder filter. Introduced by commit `6f7ad459` (#201 T201).
- Acceptance (draft, PLAN finalizes): permissive-context → restrictive-context regression AND reverse ordering; either safe reuse with correct filter isolation or fallback to re-preparation; `file:line` evidence.

### D02 — P1: cached-preparation liveness integration failure
- `tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs:148` — `CachedPreparationRawCandidateIsLive` returns False non-deterministically (Postgres 3/3 targeted, MySQL 1/3 targeted; full runs exit 1). Test new in `a21e5d89`.
- Acceptance (draft, PLAN finalizes): preserve result-parity checks; make raw-candidate liveness deterministic (fix the raw-plan lifecycle OR correct the thread-sensitive oracle — do not delete the assertion or retry-until-green); integration suite green, 0 failed, 0 provider-level skips.

## Explicitly out of scope

- T232 PIVOT/UNPIVOT identifier safety (`SqlServerEntityBuilderExtensions.cs:148,199`) — already accepted risk under **#237**; no duplicate corrective work.

## Required after the fix

- Re-run the FULL collection C on the integrated branch (`1.0.9-rc3`), same streams as `artifacts/pdca/collection-1.0.9-todo/C/C-report.md`.

## PLAN (r=1, rv=1) — corrective

### Corrective plan (planner, r=1 / rv=1)

### Статус и цель
- CCORR-201: r=1, evidence rv=1, ветка 1.0.9-rc3. Это завершение той же ревизии: r не увеличивать, n не сбрасывать. D01 и D02 активны (не done/superseded). Один corrective fix, две последовательные unit-единицы, общий CHECK.
- Цель: исключить перенос подготовленного (context-dependent builder) фильтра между контекстами через raw preparation reuse (D01) и исправить thread-sensitive oracle (D02), сохранив проверку streaming parity и thread-local cache.

### Приёмка (P1 по построению)
- AC01/P1: одинаковая raw-форма не переносит permissive-фильтр в restrictive-контекст и обратно; результаты равны независимой обычной подготовке. Негативный: restrictive-контекст не получает запрещённые строки.
- AC02/P1: guard учитывает все источники допустимого графа, их effective scope и живой контекст; НЕ вызывает пользовательские filter functions. Негативный: фильтр только в JOIN/CTE/UNION не обходится.
- AC03/P1: scope All / отключённые named-фильтры не считаются применимыми; unfiltered eligible query сохраняет raw publish/reuse; fail-closed expectations после DataContextCache.Clear сохранены.
- AC04/P1: streaming/non-streaming дают одинаковые результаты; raw-candidate liveness проверяется непосредственно после синхронной публикации на том же потоке. Негативный: thread hop не превращается в ложное требование cross-thread reuse.
- AC05/P1: build 0W/0E; полный integration boundary — 0 provider skips; shared QueryCommand не мутируется; temp-table/TVP-покрытие сохранено.
- AC06/P1: acceptance benchmark — ровно 7 случаев, 0 failures, wall ≤4 мин; Mean/Allocated/ratio и сравнимость с baseline зафиксированы.

### Решение (минимум + отвергнутые альтернативы)
- Исправляем eligibility raw reuse и размещение тестового oracle; НЕ нормализованный fingerprint и НЕ устройство кеша.
- D01 выбран: fail-closed metadata guard всего допустимого raw-графа (без исполнения user function). Отвергнуто: только main-source guard (не защищает JOIN/вложенные); расширение fingerprint телом фильтра (требует вычисления context body, риск некорректности/стоимости).
- D02 выбран: синхронная пара publish→assert на одном потоке; parity — отдельная обязательная проверка. Thread-local cache не менять; assertion не удалять; retries не добавлять.
- Непреложно: обычная подготовка и QueryFilterExpectations сохранены; никаких sticky cache flags, вызова filter body для probe, retries, удаления assertion.

### Пробелы (gap → закрытие)
- Полный source traversal отсутствует (доказательство QueryPreparer.cs:3863-3996) → используем уже захваченный RawShape, без нового visitor/framework.
- Как определить применимость без user code (QueryFilterResolver.cs:23-76) → GetFilters(...).Count, не новый Has* API.
- Неизвестный source mapping → guard: если нет доказательства отсутствия применимого фильтра, raw reuse исключён; обычная подготовка решает семантику.
- Гарантия потока после await отсутствует (QueryPlanStore.cs:31-32,55-56 ThreadStatic) → oracle обязан быть same-thread.
- Воспроизводимость D02 → контролируемый test-only thread-hop red-repro, без вероятностных повторов.
- Сравнимость benchmark host → фиксируем окружение; несравнимость = GAP сравнения, не выдуманный perf-verdict.
- Sealing ratio / новые abstractions не предоставлены → не заявляем измеренных значений; план не вводит public API/типов/нового слоя.

### DO задачи (footprint, порядок)
- D00 — fix now: coder записывает план/контракт; фиксирует исходный diff. Перед контейнерными тестами загружает .opencode/skills/running-integration-tests/SKILL.md.
- D01a — fix now: регрессии в tests/nextorm.core.tests/CachedPreparationRecipeTests.cs:481-500,655-672: оба порядка контекстов, applicable/disabled scopes, guarded graph sources, отсутствие дополнительного исполнения filter function.
- D01b — fix now: src/nextorm.core/DataContext/QueryPlanner.cs:609-644: базовый probe gate сохранить; ПОСЛЕ CaptureRawShape (:618), ДО TryGetRaw (:623) добавить metadata eligibility. При отказе guard — никакого staging/copy.
- D01c — fix now: локальная проверка RawShape в src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:3599-3668,3863-3996: main/JOIN/bound sources и рекурсивно UNION/CTE с effective scope каждого источника; через Roslyn сверить с injection rules :2099-2423; брать live context и GetFilters. Unsupported/неоднозначная форма → обычная подготовка. Resolver, fingerprint и ApplyPreparationFrom не расширять.
- D02 — fix now: tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs:131-150: синхронные non-streaming publish и liveness assertion рядом, до первого await; streaming parity оставить отдельным обязательным assertion; thread-hop characterization сохранить.
- D03 — fix now: red→green, полный verification boundary, coverage, acceptance benchmark, evidence manifest, внутренний отчёт.
- Deferred (вне этого fix): T232/#237, cross-thread cache, оптимизация reuse filtered graphs — триггер: согласованная задача/измеренная потребность, без переноса текущих обязательств.
- Режим: последовательно в текущем дереве (QueryPlanner/raw contract/общие тесты пересекаются). Без branch/worktree/merge; D01 и D02 не дробить на независимые сдачи.

### Матрица вариантов (variant matrix)

| Вариант | Закрытие |
|---|---|
| Scope None + applicable builder/context filter | test: обычная подготовка; raw reuse исключён |
| Scope All / IgnoreFilters | test: фильтры отключены; eligible unfiltered reuse остаётся |
| Named scope включённый/отключённый | test: guard соответствует effective metadata |
| Permissive→restrictive и reverse | test: обе последовательности, независимый normal-preparation oracle |
| Main / JOIN / bound raw / UNION / CTE | test + guard: все представимые источники; неприменимая raw-форма → normal preparation |
| Вложенный scope ≠ родительский | test: scope берётся у соответствующего источника/команды |
| Non-streaming / streaming terminal | test: parity; streaming не требует raw publish/reuse (QueryPlanner.cs:613) |
| Same thread / принудительный thread hop | test: same-thread liveness; существующая thread separation :655-672; никаких cross-thread требований |
| In-memory / SQLite / PostgreSQL / SQL Server / MySQL / ClickHouse | test: unit semantics + полный provider boundary |
| srcType absent/fallback, TableAlias/IProjection; value/reference projection | test + guard: resolver shortcuts и штатная подготовка |
| Cache.Clear + expected filters; temp-table/TVP | test: существующие fail-closed/спец-ветки в полном прогоне |
| Unfiltered graph; filter function с наблюдаемым вызовом | test: reuse сохраняется; число вызовов = normal-preparation reference; probe не исполняет функцию |

### Red→green и границы
- Unit selector: FullyQualifiedName~CachedPreparationRecipeTests (tests/nextorm.core.tests).
- D01: сначала регрессии на прежней реализации → assertion failure из-за межконтекстного переноса; затем тот же selector зелёный после guard.
- D02: временный test-only scheduling patch, переносящий СТАРУЮ liveness-проверку на другой поток после публикации → ложный oracle failure; после перестановки publish→assert тот же сценарий зелёный, parity остаётся. Selector: -method '*CachedPreparation_StreamingAndNonStreamingResultsMatch*'. Test-only red harness в production не попадает; патч и журналы сохранить.
- Ошибка сборки / недоступная БД / пропущенный provider — НЕ red-repro. Если controlled repro не подтверждён → точечный scout, D02 не объявлять завершённым.
- Полный solution run закрывает unit/SQL-generation; отдельный container-backed run — обязательный integration boundary.
- Coverage: line 85% / branch 75%, модули core/sqlite/postgres/sqlserver; ветка ниже порога — предупреждение по политике проекта (не скрытая замена порогов); локальные непокрытые ветви разобрать.

### Приоритеты / доки / измерения
- P1: E01-E05 корректность пути; E06 build; E07 реальные провайдеры/temp-table/TVP; E09 cached/prepared perf. CHECK их не понижает.
- Доки: status-файл, evidence inventory, добавление текущего замера в docs/specs/performance/acceptance-benchmarks.md:61-102 (без перезаписи исторического baseline). Public EN/RU не трогаем.
- Перф-замер обязателен: eligibility на query path (QueryPlanner.cs:609-644), не one-time. C09: 7 cases, 0 failures, wall ≤240 s. Baseline ratio cached/prepared = 1.87, allocated 7.42; сравнимый time ratio >2.244 требует расследования, НЕ автоматического perf FAIL; >240 s = gate FAIL.
- Разведка: архитектурный spike не нужен; нужен ограниченный D02 scheduling experiment (старая assertion падает после доказанного thread hop; новая same-thread assertion + parity проходят).
- Риски: conservative bypass снижает reuse filtered graphs; доп. metadata traversal; неверное соответствие source/scope; случайно зелёный D02 repro; контейнерная среда. Контрмеры — тестами и evidence.

### Evidence contract rv=1
- Корень E = artifacts/pdca/CCORR-201/rv1. Frozen manifest: artifacts/pdca/CCORR-201/rv1/manifest.json; required_rows=[E01..E11]; vocabulary met|open|na; N/A не разрешено.
- C-команды:
  - C01 `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~CachedPreparationRecipeTests"`
  - C02 `dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -method '*CachedPreparation_StreamingAndNonStreamingResultsMatch*'`
  - C03 Roslyn refs для NextORM.Core.QueryCommand.ApplyPreparationFrom и NextORM.Core.QueryFilterResolver.GetFilters + review production diff против AC01-AC03 и injection rules.
  - C04 `dotnet build nextorm.slnx -c Debug`
  - C05 `dotnet test nextorm.slnx -c Debug --no-build`
  - C06 `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor` (DOCKER_HOST обязателен)
  - C07 `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
  - C08 `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura"`
  - C09 `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
  - C10 `git diff --check` + review (CRLF, private-only scope, shared-command nonmutation, docs, 12 invariants)
  - C11 `(export GIT_INDEX_FILE=/tmp/nextorm-CCORR-201-rv1.index; git read-tree HEAD && git add -A -- . ':!artifacts' ':!TestResults' ':!tests/coverage' && git write-tree)`
  - C12 `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/CCORR-201/rv1/manifest.json`
- Строки (Req/row/rv → сценарий; invocation; артефакт; владелец; predicate=true): AC01/E01 (D01 red→green оба порядка, C01, E/e01-{red,green}.log); AC02/E02 (graph/scopes no-userfn, C03, E/e02-review.md); AC03/E03 (disabled scopes/unfiltered reuse/expectations, C01, E/e03-tests.log); AC04/E04 (D02 controlled red→green+parity, C02, E/e04-{red,green}.log); AC04/E05 (thread isolation существующий тест :655-672, C01, E/e05-thread.log); AC05/E06 (build+solution, C04/C05, E/e06-{build,solution}.log); AC05/E07 (реальные провайдеры+temp-table/TVP, C06, E/e07-integration.log); COVERAGE/E08 (C07/C08, E/e08-*.log); AC06/E09 (C09, E/e09-benchmark.log); DESIGN/E10 (C10, E/e10-review.md); GOVERNANCE/E11 (C11/C12, E/e11-*.log).

### CHECK freeze / бюджет
- До DO coder сохраняет контракт. Autonomous mode продолжает после gate-1 без ожидания.
- На DO→CHECK — единственный manifest, bound к SHA из C11 и contract_rv=1; без коммита и изменения рабочего index; после freeze исходные файлы не менять.
- Append-only во время CHECK; supersession отсутствует (тот же rv1). C12 exit 0 = структура, НЕ PASS. PASS запрещён при любом открытом обязательстве.
- Re-gather budget: владелец CHECK, максимум 2 раунда по 30 мин, адресно; исчерпан → CHECK unresolved + перечислить open IDs; не FAIL/DO из-за отсутствия отчёта.
- Классификация: доказанного внешнего блокера нет; D02 reproducibility — недостаток evidence, закрывается экспериментом; при стойкой низкой уверенности — scout, затем рекомендация escalate (trigger 5).

### Design checklist
- Инварианты 1-4: без новой абстракции; минимум концепций; perf измеряется; settled findings не переоткрываются.
- 5-8: public API не меняется; warnings-as-errors/CRLF/CPM; известная архитектура не объявляется новой находкой; milestone/scope не переносится.
- 9-12: PLAN закрепляет варианты; C# symbols — Roslyn-only; непроверенное остаётся GAP; terminal-incomplete сохраняет активные D и остаток критериев.
- Планировочный checklist пройден; реализация/evidence ещё не подтверждены.

## Progress journal

`<UTC time> | <phase> | revision r | iteration n/3 | <event> | <evidence pointer>`

2026-10-15T17:05Z | DO | r1 | n1/3 | D01 guard implemented (`QueryPlanner.cs` eligibility gate + `QueryCommand.HasApplicableFiltersInRawGraph`) and D01 regression tests added; unit selector green | artifacts/pdca/CCORR-201/rv1/E/e01-green.log
2026-10-15T17:02Z | DO | r1 | n1/3 | D01 red (real behavioural, not compile): guard reversed -> 5 failures incl. cross-context `RecipeReuses` expected 0 found 1 | artifacts/pdca/CCORR-201/rv1/E/e01-red.log
2026-10-15T17:02Z | DO | r1 | n1/3 | D01 named-disable diagnosed from raw probe: `fpEq=True, Matches=False, refEq/valEq=False, guard=False, getFilters=0` -> no wrong guard fire; test now asserts the measured no-reuse + ordinary-cache hit; temporary `Recipe_ScopeDiag` removed | artifacts/pdca/CCORR-201/rv1/E/e03-named-scope-diag.log
2026-10-15T17:04Z | DO | r1 | n1/3 | D01 variant-matrix unit outcomes all exit 0; existing `Recipe_ThreadSeparation_NoCrossThreadReuse` executed (not skipped) and passed | artifacts/pdca/CCORR-201/rv1/E/e03-tests.log ; artifacts/pdca/CCORR-201/rv1/E/e05-thread.log
2026-10-15T17:04Z | DO | r1 | n1/3 | `dotnet build nextorm.slnx -c Debug` -> 0 Warning(s) 0 Error(s), exit 0 | artifacts/pdca/CCORR-201/rv1/E/e01-build.log
2026-10-15T17:05Z | DO | r1 | n1/3 | inner-loop evidence validator exit 0 (`validate_inner_loop.py report`) | /tmp/nextorm-CCORR-201-evidence.json
2026-10-15T17:05Z | DO | r1 | n1/3 | D01 DO-unit complete pending gate 2; D02 NOT started (active) | docs/specs/status/D201-raw-preparation-regression-1.md
2026-10-15T17:10Z | DO | r1 | n1/3 | D02 oracle corrected: buffered publish->sync same-thread liveness before first await, streaming/buffered parity kept; controlled thread-hop red 4/4 FAIL exit 1 (publish/check threads differ), same-thread green 4/4 PASS exit 0 (publish==check), 0 skips | artifacts/pdca/CCORR-201/rv1/E/e04-red.log ; artifacts/pdca/CCORR-201/rv1/E/e04-green.log ; artifacts/pdca/CCORR-201/rv1/E/e04-green-threads.log
2026-10-15T17:17Z | DO | r1 | n1/3 | D01 closed (gates): fail-closed metadata guard `QueryPlanner.cs:627` + helper `QueryCommand.QueryPreparer.cs:4021` (metadata-only, no user filter fn executed); behavioural red exit 2 (5 of 21 failed) -> green exit 0 (`selected_count=21`, 21 passed, 0 skipped) | artifacts/pdca/CCORR-201/rv1/E/e01-red.log ; artifacts/pdca/CCORR-201/rv1/E/e01-green.log
2026-10-15T17:18Z | DO | r1 | n1/3 | D02 closed (gate): same-thread buffered publish->liveness `CommonTestSuite.CachedPreparation.cs:141-142`; controlled thread-hop red exit 1 (4/4 failed) -> green exit 0 (4/4 passed, 0 skipped); publish==check thread on 4/4 providers (Sqlite/Postgres/SqlServer/MySql); streaming/non-streaming parity kept | artifacts/pdca/CCORR-201/rv1/E/e04-red.log ; artifacts/pdca/CCORR-201/rv1/E/e04-green.log ; artifacts/pdca/CCORR-201/rv1/E/e04-green-threads.log
2026-10-15T17:19Z | DO | r1 | n1/3 | D03 boundary: C04 build exit 0 (0 Warning(s) 0 Error(s)); C05 solution exit 0 (total 12085 / passed 8914 / failed 0 / skipped 3171 - DB-backed tests skip without DOCKER_HOST by design); C06 integration exit 0 (total 3978 / passed 3780 / failed 0 / skipped 198; provider-level skips = 0; all 5 providers executed, 5 DB containers; 198 skips all capability/opt-in); identical summary on warm re-run (determinism) | artifacts/pdca/CCORR-201/rv1/E/e06-build.log ; artifacts/pdca/CCORR-201/rv1/E/e06-solution.log ; artifacts/pdca/CCORR-201/rv1/E/e07-integration.log ; artifacts/pdca/CCORR-201/rv1/E/e07-provider-matrix.md
2026-10-15T17:20Z | DO | r1 | n1/3 | D03 boundary: `git diff --check -- src tests` exit 0 (CRLF clean); scope = production/tests only (QueryPlanner.cs, QueryCommand.QueryPreparer.cs, CachedPreparationRecipeTests.cs, CommonTestSuite.CachedPreparation.cs), no unrelated churn; global `git diff --check` non-zero only on pre-existing unrelated tracked files (benchmarks/.opencode/other artifacts) | artifacts/pdca/CCORR-201/rv1/E/e10-diff.patch
2026-10-15T17:22Z | DO | r1 | n1/3 | D03 coverage (CI protocol reproduced): C07 `dotnet-coverage collect` exit 0 (12085 total / 8914 succeeded / 0 failed / 3171 skipped, wall 42s) -> C08 `reportgenerator` exit 0; Line 87.5% (>=85) / Branch 79.2% (>=75) both above thresholds; FIX guard branch `QueryPlanner.cs:627` 2/2 (both outcomes); uncovered arms of `HasApplicableFiltersInRawGraph` (bound raw main/join 4032/4061-4064, null-join 4055, null srcType 4098) listed in summary | artifacts/pdca/CCORR-201/rv1/E/e08-collect.log ; artifacts/pdca/CCORR-201/rv1/E/e08-report.log ; artifacts/pdca/CCORR-201/rv1/E/e08-summary.md ; tests/coverage/report/Summary.txt
2026-10-15T17:30Z | DO | r1 | n1/3 | D03 acceptance benchmark (C09) exit 0, wall 70s (BDN Global total 47.23s) <=240s gate PASS; exactly 7 cases / 0 failures; cached/prepared time ratio 1.71 vs baseline 1.87 (-8.6%, < investigation threshold 2.244), allocated ratio 5.98 vs 7.42; comparable host/config -> no cached-path regression; BDN tracked artifacts restored to HEAD (15 M -> 0 M, 3 pre-existing untracked T218 files untouched); docs append done into clean `acceptance-benchmarks.md` (+34 insertions, CRLF, historical baseline untouched) | artifacts/pdca/CCORR-201/rv1/E/e09-benchmark.log ; artifacts/pdca/CCORR-201/rv1/E/e09-comparison.md ; artifacts/pdca/CCORR-201/rv1/E/benchmarks/ ; docs/specs/performance/acceptance-benchmarks.md

2026-10-15T17:57Z | DO | r1 | n1/3 | DO→CHECK freeze: manifest artifacts/pdca/CCORR-201/rv1/manifest.json, contract rv=1 | artifacts/pdca/CCORR-201/rv1/manifest.json
2026-10-15T18:18Z | DO | r1 | n2/3 | CHECK r=1 n=1/3 FAIL (check: fail-open guard for null entity type) -> DO loop-back n=2/3; fail-closed fix applied | artifacts/pdca/CCORR-201/rv1/E/e01-guard2.patch ; artifacts/pdca/CCORR-201/rv1/E/e01-green2.log ; artifacts/pdca/CCORR-201/rv1/E/e04-green2.log
2026-10-15T18:24Z | DO | r1 | n2/3 | DO→CHECK re-freeze after loop-back n=2/3 fix (fail-closed guard); manifest re-bound to new tree | artifacts/pdca/CCORR-201/rv1/manifest.json
2026-10-15T23:30Z | ACT | r1 | n2/3 | ACT finalized: CHECK PASS r=1/rv=1/n=2/3 (AC01–AC06 met; 11/11 manifest rows met; tree 683eb7f7c13c32af67838d8c3f63be23f7883005); D01/D02 corrected; collection C re-run pending | artifacts/pdca/CCORR-201/rv1/manifest.json

## ACT — outcome

- Verdict: **CHECK PASS** — plan revision r=1, evidence contract rv=1, iteration n=2/3; AC01–AC06 all met; 11/11 manifest rows met; validator exit 0 (`artifacts/pdca/CCORR-201/rv1/manifest.json`); tree `683eb7f7c13c32af67838d8c3f63be23f7883005`. Cycle closed; no re-plan, no r+1.

### Defects corrected
- `CCORR-201-D01` — **corrected**. P1/High global-filter policy reuse (raw preparation reuse adopted a prior prepared predicate without evaluating the live context-dependent builder filter). Fix history: D01 fixed in DO; then CHECK r=1 n=1/3 **FAIL** (fail-open guard for a null/indeterminable entity type) → DO loop-back n=2/3 with a **fail-closed** fix → CHECK PASS.
- `CCORR-201-D02` — **corrected**. P1 cached-preparation liveness (thread-sensitive oracle read after an `await`). Fix: publish→assert made same-thread (synchronous publish, liveness asserted before the first await); streaming/non-streaming parity kept.

### Fixes
- D01 fail-closed metadata guard: `src/nextorm.core/DataContext/QueryPlanner.cs:627-631` + `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` `HasApplicableFiltersInRawGraph` (~:4021) / `HasApplicableFilters` (~:4111; null entity type ⇒ filter-bearing / fail-closed). Metadata-only — no user filter function is executed for the probe.
- D02 same-thread publish→assert: `tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs:141-149`.
- Tests: `tests/nextorm.core.tests/CachedPreparationRecipeTests.cs` (incl. the fail-closed case at `:1012`).

### Evidence
- `artifacts/pdca/CCORR-201/rv1/manifest.json` (11/11 met, validator exit 0); `artifacts/pdca/CCORR-201/rv1/E/**`.
- Build: 0 warnings / 0 errors. Unit: 22/22. Solution: 12086 total / 8915 passed / 0 failed / 3171 skipped. Integration: 3978 total / 3780 passed / 0 failed / 198 skipped, with **0 provider availability skips** (5 providers executed). Coverage: line 87.5 % / branch 79.3 %. Acceptance benchmark: exactly 7 cases, wall 50 s, ratio 1.73 (baseline 1.87, threshold 2.244). DocFX: not relevant to this corrective.

### Accepted (non-defect)
- P2: nested `Ctes[].Body` / `Union` recursion uses the single command-graph `dataContext`; defensive bound-raw / `joinFrom is null` branches are uncovered by the suite, but the decisive guard branches are 2/2 covered.

### Out of scope (unchanged)
- T232 PIVOT/UNPIVOT identifier safety (`SqlServerEntityBuilderExtensions.cs:148,199`) — already accepted risk under **#237**; no duplicate corrective work.

### Next step
- The parent re-runs the **FULL collection C** on `1.0.9-rc3` (same streams as `artifacts/pdca/collection-1.0.9-todo/C/C-report.md`).
