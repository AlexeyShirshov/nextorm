# D191 — Provider-specific extensions

```yaml
task_id: D191
issue: 191
milestone: 1.0.9-rc2
selected_variant: pdca-dotnet
base: "1.0.9-rc2 @ 18659e41"
cycle: 1
plan_revision: 1
attempt: 2
check_revision: "r1 FAIL -> DO n=2 -> CHECK PASS (snapshot-n2)"
plan_state: ready
phase: ACT
final_check: "PASS (snapshot-n2)"
snapshot: "7e9fad9f443d898fe4cde2594f555c0819d7221d"
snapshot_diff_sha256: "f652c19e8489ddf8da10e1ff4eb5c4e6dff8c4024d1aa7d46704efd48659fdb7"
artifact_root: /tmp/nextorm-D191-r1
provenance:
  - "PLAN r1: accepted direct-relocation architecture"
  - "P:D191-G1-EVIDENCE: completed; normative contract and repository facts supplied"
  - "Written specification: user-approved 2026-10-08; reviewed content git blob 77825df33c6d4e54fafdf16e1f6fc79f092236a6, sha256 8bf020f693f0dde347d99c17fad10f73a92c45a1b24ed4839a997b2f62c139ad; D191 implementation authorized by that instruction"
status_file: docs/specs/status/rc2-191-provider-extensions-1.md
```

## Goal

Перенести provider-specific fluent API из core в соответствующие provider assemblies прямой релокацией, сохранив SQL, типизированное состояние запросов, поведение клонирования, joins и plan cache.

Архитектура закреплена: **без новой иерархии, без compatibility stubs, без переноса типизированного AST из core**. Провайдерные extensions используют минимальные нейтральные механизмы core.

Текущий цикл: **r=1, N=1, evidence contract rv=1**. Завершение `P:D191-G1-EVIDENCE` подтверждает существующие обязательства и **не создаёт r2**.

## Scope

### В объёме

- Прямое перемещение provider-specific fluent methods, определённых written specification, в extensions соответствующих провайдеров.
- Сохранение typed AST, хранения query state, SQL translation и общих механизмов копирования/клонирования в core.
- Минимальное расширение доступности действительно необходимых нейтральных механизмов.
- Обновление затронутых call sites, тестов, benchmark fixtures, XML comments, EN/RU документации и внутренних регистров.
- Доказательства функциональной эквивалентности и отсутствия регрессии query-path, cache и allocations.
- Обновление issue #191 и проверка milestone `1.0.9-rc2`.

Точный перечень перемещаемых методов фиксируется из reviewed specification и Roslyn inventory в D191.1. Это уточнение файлов и существующих символов, а не разрешение расширять функциональный объём.

### Вне объёма

- Новая fluent-builder hierarchy, новая модель query state или object/string dictionaries вместо typed AST.
- Сохранение старого provider-specific API в core через forwarding/obsolete stubs.
- Изменение SQL-семантики, provider capabilities, eager loading или политики кэширования.
- Реализация #159/#160/#184/#190.
- Включение дополнительного SQL Server D168 benchmark в стандартный набор acceptance.
- Изменение проектных coverage thresholds или ослабление CI gates.
- Commit, push и merge: этим планом не разрешены.

## Acceptance criteria

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| REQ-191-API | Все методы из reviewed relocation inventory принадлежат правильным provider extensions; Roslyn audit и компиляция обновлённых consumers подтверждают это. | Метод остался provider-specific fluent API в core, появился stub/новая hierarchy либо extension доступен через неправильный provider assembly. |
| REQ-191-AST | Core сохраняет typed AST/state и нейтральные механизмы; нет public API widening ради реализации extensions. | Состояние вынесено в provider sidecar/dictionary либо neutral seam стал публичным без предусмотренного спецификацией основания. |
| REQ-191-STATE | Clone/copy сохраняют projection-independent state, `Having` и provider state во всех применимых комбинациях. | После clone/projection пропал clause, state утёк между builders либо новая ветка читает несуществующий getter `EntityBuilder.Having`. |
| REQ-191-JOIN | Оба вида join source и последующие joins сохраняют результат, SQL и options. | После join/clone потеряны options/state, неправильно выбран source либо eager-load restriction перестал действовать. |
| REQ-191-DEFAULTS | Default/null/пустые значения и существующие invalid-input guards сохраняют поведение. | Ранее допустимый default стал ошибкой или ранее запрещённая комбинация стала молча приниматься. |
| REQ-191-CACHE | Shared `QueryCommand` не мутируется ради одного вызова; cached/prepared paths и временные таблицы/TVP сохраняют изоляцию. | Устанавливается sticky `Cache=false`, последующие запросы теряют cache либо TVP auto-metadata засоряет configured cache. |
| REQ-191-TEST | Inner-loop validator проходит; целевые тесты и все 11 solution test projects проходят. Обязательные container providers реально выполняются. | Validator nonzero, нулевой выбранный набор, тестовая ошибка или provider suite представлен как PASS при skipped/not-run providers. |
| REQ-191-PERF | До/после выполнены 7 acceptance cases и moved-path category; выдержаны закреплённые ниже perf limits и allocation gate. | Пропущен case, сравниваются разные параметры/среды, ухудшен cached/prepared ratio или превышен allocation budget. |
| REQ-191-COVERAGE | Получены Cobertura и отчёт; зафиксированы line/branch coverage и применена политика текущей ветки. | Нет отчёта, изменены thresholds, branch warning выдан за main hard-pass либо warning скрыт. |
| REQ-191-DOCS | XML, EN/RU, регистры и issue #191 согласованы; DocFX проходит. | Старые примеры не компилируются, RU отстаёт, public docs ссылаются на `docs/specs/**` либо отсутствует проверяемый issue update. |
| REQ-191-EVIDENCE | CHECK сверил rv1 contract с фактическим ledger; все применимые строки закрыты. | PASS с missing/not-run evidence или N/A без доказанного закреплённого предиката. |

## Минимальное решение

1. **Цель по сути:** сменить место владения provider-specific fluent API, а не изменить построение запроса.
2. **Непреложные ограничения:** direct relocation; typed AST остаётся в core; без hierarchy/stubs; не менять shared command state; сохранить SQL/state/cache; обеспечить provider integration и perf evidence.
3. **Оптимум:** extensions в существующих provider assemblies плюс минимальные internal neutral seams и необходимые IVT; существующие core copy/clone paths остаются единственным механизмом переноса состояния.

Архитектурные альтернативы уже рассмотрены и закрыты:

| Подход | Плюсы | Минусы, цена и риск | Решение |
|---|---|---|---|
| Direct relocation + минимальные neutral seams | Малый семантический diff; typed state и clone остаются централизованы | Нужно аккуратно открыть доступ и обновить consumers | **Выбран** |
| Provider-specific builder hierarchy | Возможна отдельная поверхность API | Новая модель наследования, значительная миграция и риск clone/join | Отклонён |
| Core forwarding/obsolete stubs | Меньше немедленных изменений consumers | Provider API остаётся в core; постоянный compatibility layer | Отклонён |

Повторное архитектурное исследование не требуется.

## Prerequisites и gate 1

| Пункт | Статус | Действие/триггер |
|---|---|---|
| #183: baseline с iteration-15 cached-path | **SATISFIED** | Design/results, четыре отчёта и `iteration15-*` artifacts присутствуют в дереве. DONE с limitation; открытый issue не делает baseline заблокированным. |
| #159/#160/#184/#190 | **Нет semantic predecessor** | Не ждать завершения; запретить конкурентные изменения общего footprint. При пересечении — сериализовать работу. |
| Written-spec review | **Единственный DO-entry prerequisite; ожидается** | Пользовательский review `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md`, сейчас untracked. Триггер: **before first implementation DO of the group**. Approval записать со ссылкой на reviewed content hash. |
| Integration runtime | **In-cycle** | D191.1/D191.6: загрузить `running-integration-tests`, подготовить Podman/socket и реально выполнить providers. Не внешний предварительный блокер. |
| GitHub issue write access | **In-cycle dependency перед docs unit** | Проверить фактическую запись в #191 до D191.7; получить verified issue URL и проверить milestone. Не DO-entry gate. |
| Evidence-contract format | **CLOSED** | Все обязательные слоты закреплены ниже; версия rv1. |
| Inner-loop validator | **CLOSED** | Использовать существующую host copy; repo script отсутствует и не предполагается существующим. |
| Coverage/CI configuration | **CLOSED** | 85% line / 75% branch; hard-fail только main; инструменты и точные команды закреплены. |
| Acceptance benchmark inventory | **CLOSED** | Ровно 7 default-compiled cases, перечислены ниже; D168 исключён. |
| Roslyn/accessibility | **CLOSED** | Private/protected neutral mechanisms требуют минимального widening; options читаются через IVT, запись — через существующие mutators. |

**`plan_state=ready` означает готовность PLAN, не разрешение начать implementation DO до written-spec review.** Открытых gate-1 evidence slots нет.

## Footprint и режим единиц

### Подтверждённый shared footprint

- `src/nextorm.core/EntityBuilder.cs`
- `src/nextorm.core/Joins/JoinedEntityBuilder.cs`
- `src/nextorm.core/QueryCommand.cs`
- `src/nextorm.core/FromOptions.cs`
- `src/nextorm.core/JoinOptions.cs`
- `src/nextorm.core/nextorm.core.csproj`
- Provider extension files в `src/nextorm.<provider>/**`, согласно relocation inventory.
- Соответствующие `tests/nextorm.*.tests/**`.
- `benchmarks/nextorm.benchmark/**`.
- XML comments в изменённых исходниках.
- `docs/**`, `docs/ru/**`, затронутые public samples и внутренние registers.
- `docs/specs/status/rc2-191-provider-extensions-1.md`.

Solution boundary: 9 source projects, 11 test projects и 1 benchmark project. `tests/nextorm.alias.poc` не входит в solution: проверяется лишь при доказанном наличии затронутого consumer.

### Режим

**Один working tree, последовательно, exclusive shared footprint.** Перемещение API, изменение core seams и provider consumers связаны общим compile-time контрактом. Worktrees не нужны: изоляция не компенсирует риск расходящихся neutral seams.

Перед началом каждой единицы сверять diff с recorded baseline и отсутствие конкурентных изменений этих файлов. Пользовательские изменения не перезаписывать и не откатывать.

Точные новые extension filenames, test symbols и register paths пока не известны. Они являются implementation inventory, фиксируемым D191.1, а не открытым gate-1 slot.

## Findings: fix now / deferred

| Находка | Решение |
|---|---|
| `EntityBuilder.cs:2627,3147` — оба `JoinCore` private | **Fix now:** открыть только реально используемый neutral seam до internal; не публиковать provider API в core. |
| `EntityBuilder.cs:3125,2238,1029,2993` — `CreateJoined`, `CopyProjectionIndependentStateTo`, `EnsureNoEagerLoadState`, `GetJoinSource` private | **Fix now при фактическом использовании extensions:** минимальный internal access; не расширять весь набор автоматически. |
| `EntityBuilder.cs:2216`, `Joins/JoinedEntityBuilder.cs:115` — `CopyTo`/`CloneImp` protected | **Fix now при необходимости cross-assembly вызова:** сохранить существующую protected доступность; добавить assembly-доступность минимальным widening, не сужать контракт наследования. |
| `Joins/JoinedEntityBuilder.cs:87` — joined `JoinCore` private | **Fix now при использовании moved join path:** internal neutral seam. |
| `EntityBuilder.cs:45,3591`; `QueryCommand.cs:786` — нет builder getter `Having` | **Fix now:** перенос состояния через core copy/command path; не ссылаться на выдуманный getter и не вводить public getter. |
| Typed internal state: `SettingsList:177`, `PreWhereCondition:179`, `ArrayJoins:181`, `ArrayJoinKind:185`, `DistinctOnClause:161`, `SourceFrom:226`, `SourceEntityType:233`, `BindArrayJoinElement:235` | **Fix now:** сохранить typed state в core; extensions используют разрешённый internal доступ. |
| `FromOptions.cs:11-17`, `JoinOptions.cs:18-22,29` — private setters | **Fix now:** использовать существующие `With*`/`Sample`/`Global`/`WithStrictness` mutators. Setter widening не делать без доказанной необходимости. |
| `nextorm.core.csproj:72,76` — IVT только clickhouse/entityframeworkcore и tests | **Fix now:** добавить только реально необходимые provider friend assemblies из inventory. IVT сам по себе не открывает private/protected members. |
| Более широкая чистка API, hierarchy, unrelated sealing/рефакторинг | **Deferred:** отдельная постановка и review; не включать при обнаружении рядом с relocation. |
| Недефолтный D168 SQL Server benchmark | **Deferred:** отдельная задача или явное изменение обязательной variant matrix через PLAN; здесь не включать. |

## Ordered tasks

### D191.1 — Inventory, scope brief и подготовка evidence

После written-spec approval:

- Зафиксировать reviewed specification hash, git status/diff, dirty baseline manifest и отсутствие конкурентных shared-footprint edits.
- Roslyn-first inventory: определения, refs/callers/implementations затронутых методов; точные provider destinations, neutral seams, consumers и existing test selectors.
- Зафиксировать allocation-gate command из CI без угадывания его CLI.
- Записать scope JSON для каждой edit unit и выполнить host validator `brief` **до любого её редактирования**.
- Подготовить evidence ledger rv1, manifest, command scripts и integration runtime.
- Зафиксировать пути/hashes нормативных skills и host validator.

Результат: достаточная детализация accepted architecture для исполнения. Это не новый архитектурный spike.

### D191.2 — Baseline и moved-path benchmark fixtures

- Добавить минимальные benchmarks с `[BenchmarkCategory("provider-extensions")]`, вызывающие существующие API до релокации.
- Закрыть affected-build/inner-loop обязательства этой edit unit.
- До первого product relocation diff выполнить baseline: 7 acceptance cases и moved-path category.
- Записать runtime, CPU/OS, build configuration, BDN parameters и workload parameters.
- Включить присутствующий #183 материал в baseline provenance; не считать старые результаты заменой свежего before-run.

### D191.3 — Core neutral seams и direct relocation

- Сохранить typed AST/state в core.
- Минимально расширить доступность используемых neutral mechanisms.
- Добавить необходимые IVT, не использовать public widening.
- Прямо переместить provider-specific fluent methods в правильные provider extensions.
- Не оставлять stubs и не вводить hierarchy.
- Обновить production consumers.

### D191.4 — Clone/join/options correctness

- Проверить оба `JoinCore` paths, joined-builder continuation, clone/projection и `Having`.
- Сохранить provider clauses и source binding.
- Использовать existing options mutators; сохранить null/default и guards.
- Исключить aliasing/state leakage и sticky shared-command mutation.

### D191.5 — Targeted tests и inner-loop report

- Добавить/обновить тесты по variant matrix.
- Выполнять targeted tests после каждой соответствующей единицы; selector inventory содержит реальные, разрешённые Roslyn/runner names.
- Зафиксировать actual execution records, source/artifact revisions и selected counts.
- Выполнить validator `report`; nonzero не передавать как успешное завершение.

### D191.6 — Boundary, integrations, coverage и perf

- Debug/Release builds и full solution tests.
- Container-backed provider integrations без сокрытия skipped providers.
- Coverage collect/report.
- After-run обоих benchmark categories с теми же параметрами.
- Cached/prepared ratio и allocation-gate verification.
- Сформировать сравнительный отчёт с raw artifacts и limitations.

### D191.7 — Documentation, registers и issue

- Сначала доказать issue write access.
- Обновить XML comments, EN/RU статьи/примеры и затронутые registers.
- Обновить #191: verified URL, milestone, relocation summary, breaking-source implications, evidence и limitations.
- Проверить public docs на отсутствие ссылок на internal specs.
- Выполнить DocFX, diff/CRLF audit.

### D191.8 — Evidence reconciliation и CHECK handoff

- Заполнить ledger каждой строкой rv1: actual success/failure/not-run/missing, не только успешными запусками.
- Сопоставить criteria, variants, priority matrix и evidence artifacts.
- Передать CHECK report validator, raw exit codes и полный ledger.
- Не завершать D191 самостоятельно: completion определяется CHECK/ACT.

## Test strategy и variant matrix

Unit/SQL-generation tests проверяют placement, guards, copy/clone и SQL без БД. Integration tests подтверждают execution semantics и plan/cache paths; они не заменяются SQL snapshots.

| Variant ID | Вариант | Закрытие |
|---|---|---|
| V-API | Каждый перемещаемый метод и правильный provider assembly/namespace | **Test + Roslyn audit:** compile/use через новый API; core surface без старого fluent member/stub. |
| V-NEUTRAL | Неиспользующий provider clauses core path | **Test:** результат и SQL не изменены. |
| V-DEFAULT | Default/null options, отсутствующий clause, пустой набор там, где он допустим | **Test:** прежние defaults; **guard** для прежних запрещённых случаев. |
| V-TYPES | Reference/value entity и projection types, nullable/default значения, применимые существующей сигнатуре | **Test:** clone/source binding/SQL. Непредставимые по generic constraints варианты закрываются доказанным compile-time **guard**. |
| V-COPY | Clone, projection change, repeated clone, independent builders | **Test:** сохранение состояния и отсутствие aliasing/leakage. |
| V-HAVING | `Having` присутствует/отсутствует до и после copy/clone/join | **Test:** core copy/command path сохраняет clause. |
| V-JOIN | EntityBuilder source / QueryCommand source; первый / последующий joined-builder join | **Test:** SQL/result/options и source type. |
| V-JOIN-OPTIONS | Default и каждый реально поддержанный option/flag; применимые сочетания `Sample`/`Global`/strictness | **Test:** existing mutators; **guard:** недопустимые сочетания сохраняют прежнее исключение. |
| V-CH | ClickHouse settings, prewhere, array join kind, source binding и их допустимые сочетания из inventory | **SQL tests + ClickHouse integration**. |
| V-PG | PostgreSQL `DistinctOn` и сочетание с clone/projection/order/join, где разрешено | **SQL tests + PostgreSQL integration**. |
| V-OTHER | Иные provider methods, реально перечисленные reviewed specification | **Test:** соответствующий provider SQL suite; integration для execution semantics. Это не разрешение добавить новый feature. |
| V-CACHE | Prepared / cached; повторные вызовы с разными параметрами на одном context | **Test + benchmark:** shared command не меняет sticky state. |
| V-TEMP | Temporary-table path, local `storeInCache:false`, повторный обычный запрос | **Test + integration:** cache isolation. |
| V-TVP | Auto/configured metadata precedence, Clear, repeated context/query use | **Test + SQL Server integration:** metadata-cache invariants. |
| V-EAGER | Разрешённый путь без eager state / запрещённый join-source state | **Test + guard:** `EnsureNoEagerLoadState` сохраняет запрет. |
| V-PROVIDERS | SQLite, PostgreSQL, SQL Server, MySQL, ClickHouse integration; остальные solution dialect suites | **Test:** executed provider counts и отсутствие скрытого skip. |
| V-ALIAS-POC | Consumer в `tests/nextorm.alias.poc` | **Guard:** Roslyn inventory доказывает отсутствие затронутого consumer; при его наличии — обязательный build/test этого проекта. |
| V-D168 | Недефолтный SQL Server D168 benchmark | **Deferred:** включается только отдельной постановкой или contract revision с новым row ID. |

Новый обязательный вариант, отсутствующий в этой матрице, возвращается в PLAN; он не может быть объявлен N/A ради PASS.

## Priority matrix

Приоритеты назначены PLAN; CHECK применяет их без понижения.

| Строки contract | Приоритет | Основание |
|---|---|---|
| E03–E07, E10–E14 | **P1** | Query-path / plan-cache class по `nextorm-pdca:88-92`; clone/join execution path; cache, temp-table и TVP инварианты. |
| E01–E02, E15, E18 | **P1** | Обязательные process/evidence gates; нельзя пройти CHECK с невалидным scope/report или открытым applicable evidence. |
| E08–E09 | **P1** | Compile-time/API и full boundary после изменения общего builder contract. |
| E16–E17 | **P2** | Документация и issue synchronization; остаются обязательными acceptance criteria. |

Внутри E03–E07 строки тестов всех вариантов query-path являются P1 независимо от класса/файла теста. Проектная class-priority таблица, если inventory её обнаружит, не понижает эти обязательства.

## Performance measurement

**Обязателен.** Изменяются per-query cloning/join paths: `EntityBuilder.cs:2216,2238,2627,3125,3147` и `Joins/JoinedEntityBuilder.cs:87,115`. Это не one-time-only работа.

### Baseline

Dirty baseline фиксируется до product relocation, после добавления одинаковых moved-path fixtures:

- branch/base `1.0.9-rc2 @ 18659e41`;
- existing dirty/untracked changes и отдельный diff benchmark fixtures;
- присутствующий #183 design/results/artifacts;
- исходный benchmark source revision, runtime и workload parameters.

Никаких destructive reset/stash ради baseline.

### Ровно 7 default acceptance cases

1. `SqliteBenchmarkCachedPlan.Prepared_ToList`
2. `SqliteBenchmarkCachedPlan.Cached_ToList`
3. `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param`
4. `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`
5. `SqliteBenchmarkAny.Nextorm_Cached`
6. `InMemoryBenchmarkAggregates.Nextorm_Count`
7. `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`

D168, исключённый через `Compile Remove="acceptance/**/*.cs"` в benchmark csproj:38, не включается; `-p:AcceptanceBenchmark=true` не использовать.

### Точные before/after commands

Во всех командах:

```bash
export EV=/tmp/nextorm-D191-r1
```

Before:

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories acceptance --artifacts "$EV/perf/before/acceptance"
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories provider-extensions --artifacts "$EV/perf/before/provider-extensions"
```

After:

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories acceptance --artifacts "$EV/perf/after/acceptance"
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories provider-extensions --artifacts "$EV/perf/after/provider-extensions"
```

`provider-extensions` — **планируемая** новая category, не утверждение о существующих benchmark symbols. D191.2 фиксирует фактический case inventory; для неё не допускается нулевой выбранный набор.

### Закреплённые limits

- Для каждого одинакового case/parameter set: after Mean ≤ 1.05 × before Mean.
- Allocated bytes/op не увеличиваются; нулевой baseline остаётся нулевым.
- Для одинаковых параметров:
  `R = Cached_ToList.Mean / Prepared_ToList.Mean`;
  `R_after ≤ 1.05 × R_before`.
- Existing allocation regression gate проходит по `eng/perf/iteration14-budgets.json`.
- Любой явно шумный/невалидный замер повторяется один раз **парой before/after** с тем же workload; отсутствие сопоставимого baseline не трактуется как PASS.
- Неубедительный результат фиксируется как uncertainty и направляется по правилам evidence/perf classification; thresholds молча не ослаблять.

5% — лимит этого плана, не приписываемый CI. Existing allocation gate отдельный от BDN и inner-loop validator.

## Coverage и CI

Закреплены:

- `MIN_LINE_COVERAGE=85`
- `MIN_BRANCH_COVERAGE=75`
- hard-fail только `refs/heads/main`;
- текущая `1.0.9-rc2`: ниже порога — **warning**, записанный в status/evidence, а не скрытый.

Coverage scope: `nextorm.core`, `nextorm.sqlite`, `nextorm.postgres`, `nextorm.sqlserver`; не выдавать его за coverage всех providers.

После Debug boundary build:

```bash
dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"
dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura"
```

Инструменты: `dotnet-coverage` 18.11.2, `reportgenerator` 5.5.11, `docfx` 2.78.5.

Existing allocation-gate invocation из `.github/workflows/dotnet.yml:46-52` копируется дословно в planned `$EV/commands/allocation-ci.sh`, включая используемые аргументы и условия. Не изобретать CLI `iteration14_gate.py`. Script content/hash и expanded argv становятся evidence.

## Reconnaissance

**Архитектурный spike не нужен:** direct-relocation решение принято, Roslyn/accessibility facts достаточны.

D191.1 выполняет ограниченную execution reconnaissance:

- устанавливает полный relocation/consumer inventory;
- фиксирует минимально используемые neutral seams;
- обнаруживает реальные test selectors/register paths;
- копирует точный allocation CI invocation.

Наблюдаемый результат: конечный symbol/file inventory, сопоставленный reviewed specification; каждая planned edit unit имеет валидный scope brief и исполнимый evidence command. Эта разведка не открывает gate 1 и не пересматривает архитектуру.

## Documentation plan

**Затронуты:**

- XML comments перемещённых методов и minimal neutral seams.
- EN/RU provider guides, API samples, imports/source paths и curated reference, действительно затронутые relocation.
- Internal design/API/code-smell registers и статус задачи.
- Issue #191: проверенный URL, milestone, scope, migration implications, tests/perf evidence и limitations.

**Не трогаем:**

- Unrelated guides и их numbering.
- Generated `docs/api/**` и `docs/_site/**` вручную.
- Unrelated provider roadmap.
- Документацию #159/#160/#184/#190, кроме необходимого conflict/provenance record.

Public `docs/**`, `docs/ru/**` и readme не получают hyperlinks на `docs/specs/**`.

До D191.7 выполнить no-op запись существующего body issue как фактическую проверку write access:

```bash
gh issue view 191 --json number,url,milestone,body > "$EV/issue/before.json"
gh issue view 191 --json body --jq .body > "$EV/issue/body.before.md"
gh issue edit 191 --body-file "$EV/issue/body.before.md"
```

Затем выполнить содержательный update подготовленным body и сохранить read-back:

```bash
gh issue edit 191 --body-file "$EV/issue/body.after.md"
gh issue view 191 --json number,url,milestone,body > "$EV/issue/after.json"
dotnet docfx docs/docfx.json
```

Отказ write access не отменяет документационные критерии; классифицируется отдельно при возникновении.

## Design checklist

- [ ] Relocation inventory совпадает с reviewed specification.
- [ ] Provider-specific fluent methods отсутствуют в core; stubs/hierarchy отсутствуют.
- [ ] Typed AST/state остаются в core.
- [ ] Private → internal только для используемых neutral seams.
- [ ] Existing protected contract не сужен; public widening отсутствует.
- [ ] IVT добавлен только необходимым provider assemblies.
- [ ] Options используют существующие mutators, private setters не обходятся.
- [ ] `Having` сохраняется через реальный core state path.
- [ ] Clone/projection/join не теряют и не разделяют mutable state.
- [ ] Shared `QueryCommand.Cache` не меняется ради отдельного вызова.
- [ ] Temp-table local cache preparation и TVP cache separation сохранены.
- [ ] Existing generic constraints, guards и exceptions сохранены.
- [ ] Новые async-only methods не получают `Async` suffix без sync twin.
- [ ] Central Package Management и warnings-as-errors сохранены.
- [ ] CRLF сохранены; нет unrelated churn.
- [ ] EN/RU/XML/registers/issue согласованы; public links на specs отсутствуют.

## Versioned evidence contract

### Pinned version

- Contract revision: **rv=1**.
- Normative source: `~/.config/opencode/skills/pdca-dotnet/SKILL.md:898-931`.
- Completeness: тот же skill `:1097-1116`.
- Inner-loop requirements: `:480-495,523-562`.
- Priority: `:700-704` и `nextorm-pdca/SKILL.md:88-92`.
- Escalation: `:603-626`.
- Host validator:  
  `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py`.

В D191.1 записать exact resolved paths и SHA-256 snapshots в `$EV/manifest.json`. Repo `scripts/validate_inner_loop.py` **отсутствует**; команда всегда использует host copy, а не предполагаемый repo script.

### Evidence conventions

- Все источники ниже, кроме уже названных repository/normative facts, — **планируемые**.
- Общие обязательные artifacts каждой строки: command/tool invocation, raw stdout/stderr, actual exit code, source revision/hash и artifact revision/hash в `$EV/ledger.json`.
- Для pipelines сохранять exit code исходной команды, не `tee`; использовать `pipefail`/`PIPESTATUS`.
- Exit 0 сам по себе не доказывает успешные tests/benchmarks: нужны counts и результаты.
- `$EV/commands/*.sh` — планируемые evidence command files. Они создаются D191.1 из реальных selectors/CI invocation, сохраняются с content/hash и literal expanded argv. Их наличие не предполагается сейчас.
- Реальные selectors записываются в `$EV/selectors/*.txt`; будущие test symbols и `file:line` здесь не выдумываются.
- Scope JSON имеет nonempty `unit`, `scope.projects`, `scope.selectors`, `scope.rationale`, `scope.boundary`, список `scope.files` и `scope.rebuild` = `affected` или `none`; запрещённые broad selectors не используются.
- Report JSON содержит nonempty `executions`: literal `command`, `phase`, nonempty `source_revision`/`artifact_revision`, integer `exit_code`, integer `selected_count ≥ 1`.
- Docs-only scope не заявляет `rebuild=affected`.
- В ledger сохраняются также failed/not-run/missing записи.

### Contract rows — rv1

| Row / requirement / priority | Scenario и evidence kinds + sources | Точная команда/вызов; exit/result/log | Artifacts | Owner | Observable applicability | rv |
|---|---|---|---|---|---|---|
| E01 / REQ-191-EVIDENCE / P1 | Reviewed spec, dirty baseline, normative/tool provenance; document/hash evidence из spec, git и host files | `git status --porcelain=v1`; `git diff --binary`; `sha256sum docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py`; exit 0; записанное user approval совпадает с spec hash | `$EV/provenance/`, manifest, approval record, dirty diff | D191.1 / coder | **Всегда** | 1 |
| E02 / REQ-191-EVIDENCE / P1 | Pre-edit scope validation каждого edit unit; validator evidence | `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py brief "$EV/inner-loop/<unit>.scope.json"`; для каждого unit exit 0, нет `FAIL:`/`ERROR:`; log до первого edit | Scope JSONs, brief logs, validator path/hash | D191.1–D191.7 / coder | **Всегда для каждого edit unit** | 1 |
| E03 / REQ-191-API + REQ-191-AST / P1 | Semantic inventory и post-relocation audit; Roslyn definitions/refs/callers плюс reviewed diff | Tool `roslyn`, action `structure`, затем exact `members`/`refs`/`callers` invocations из planned `$EV/commands/roslyn.jsonl`; все tool calls без ошибок; до/после inventory удовлетворяет API/AST criteria | Roslyn invocation/results, relocation/seam inventory, API diff review | D191.1/D191.3 / scout + coder | **Всегда** | 1 |
| E04 / REQ-191-STATE + REQ-191-DEFAULTS / P1 | V-DEFAULT/V-TYPES/V-COPY/V-HAVING/V-EAGER; unit/SQL tests из actual selector inventory | `bash "$EV/commands/state-tests.sh"`; exact underlying `dotnet test … -c Debug --filter …` закреплён в script; exit 0, selected/executed >0, failed=0; каждому variant соответствует actual case/guard evidence | Script/hash, selector mapping, test logs/results | D191.4/D191.5 / coder | **Всегда**; compile-time guards только с доказанной неприменимостью runtime variant | 1 |
| E05 / REQ-191-JOIN / P1 | V-JOIN/V-JOIN-OPTIONS; оба sources и joined continuation; unit/SQL evidence | `bash "$EV/commands/join-tests.sh"`; exit 0, selected/executed >0, failed=0; logs показывают оба source paths и options/default guards | Script/hash, case mapping, test results | D191.4/D191.5 / coder | **Всегда** | 1 |
| E06 / REQ-191-API + REQ-191-DEFAULTS / P1 | V-CH/V-PG/V-OTHER/V-NEUTRAL; provider SQL and API compilation tests | `bash "$EV/commands/provider-tests.sh"`; exit 0, каждый applicable provider/relocated family выполнен, failed=0; нет пустого selector | Script/hash, provider/method matrix, logs/results | D191.3/D191.5 / coder | **Всегда**; family membership определяется recorded reviewed inventory, не наличием отчёта | 1 |
| E07 / REQ-191-CACHE / P1 | V-CACHE/V-TEMP/V-TVP; sticky state, local preparation, metadata precedence/Clear | `bash "$EV/commands/cache-tests.sh"`; exit 0, selected/executed >0, failed=0; отдельное evidence для cache/temp/TVP obligations | Script/hash, cache/temp/TVP case mapping, logs/results | D191.5 / coder | **Всегда**, по query-path priority class | 1 |
| E08 / REQ-191-API + REQ-191-TEST / P1 | Solution compilation Debug/Release; warnings-as-errors | `dotnet build nextorm.slnx -c Debug`; `dotnet build nextorm.slnx -c Release`; оба exit 0, нет новых warnings/errors | Build logs, source/artifact hashes | D191.6 / coder | **Всегда** | 1 |
| E09 / REQ-191-TEST / P1 | Full boundary всех 11 solution test projects; runner evidence | `dotnet test nextorm.slnx -c Debug --no-build --verbosity normal`; exit 0, project inventory соответствует 11 projects, failed=0; integration skips здесь не заменяют E10 | Boundary log/results, project counts | D191.6 / coder | **Всегда** | 1 |
| E10 / REQ-191-TEST + REQ-191-CACHE / P1 | Container integrations: SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse; runtime and executed-provider evidence | `DOCKER_HOST="$(cat "$EV/runtime/docker-host.txt")" dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; exit 0; каждый обязательный provider реально executed, failed=0, provider-level skipped/not-run не принимается | Skill-load record, socket/runtime probes, integration logs, provider counts | D191.1/D191.6 / coder | **Всегда** | 1 |
| E11 / REQ-191-COVERAGE / P1 | Coverage collection/report из exact CI tools/settings | Две точные coverage команды из раздела Coverage; оба exit 0; Summary содержит line/branch values; thresholds 85/75 применены как warning на этой ветке | `tests/coverage/coverage.cobertura.xml`, `tests/coverage/report/`, copied summary/logs | D191.6 / coder | **Всегда** | 1 |
| E12 / REQ-191-PERF / P1 | Before/after 7 acceptance cases; BDN raw results и environment | Две acceptance команды из Performance; обе exit 0; ровно перечисленные 7 cases, все parameter sets сопоставимы и без benchmark errors | `$EV/perf/{before,after}/acceptance/`, baseline manifest, comparison | D191.2/D191.6 / coder | **Всегда** | 1 |
| E13 / REQ-191-PERF + REQ-191-CACHE / P1 | Before/after moved-path category; mean/allocations и cached/prepared ratio | Две `provider-extensions` команды из Performance; exit 0, selected/executed >0; comparison по закреплённым limits; ratio вычислен из E12 для одинаковых параметров | `$EV/perf/{before,after}/provider-extensions/`, exact case inventory, perf/ratio comparison | D191.2/D191.6 / coder | **Всегда** | 1 |
| E14 / REQ-191-PERF / P1 | Existing allocation regression gate, distinct from BDN/validator | `python3 -m unittest discover -s eng/perf/tests -p test_iteration14_gate.py`; `bash "$EV/commands/allocation-ci.sh"`; оба exit 0; CI gate выполнен с exact workflow argv и budgets, не только unit tests | Test/gate logs, script/hash, budget/input/output artifacts | D191.1/D191.6 / coder | **Всегда** | 1 |
| E15 / REQ-191-EVIDENCE / P1 | Final inner-loop report validation; current source/artifact revisions | `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py report "$EV/inner-loop/report.json"`; exit 0; CHECK получает raw exit; executions nonempty и schema-valid | Report JSON, validator log/exit record | D191.5/D191.8 / coder | **Всегда** | 1 |
| E16 / REQ-191-DOCS / P2 | XML/EN/RU/register consistency, DocFX, links, CRLF/diff | `dotnet docfx docs/docfx.json`; `git diff --check`; `bash "$EV/commands/docs-audit.sh"`; все exit 0; audit проверяет old API samples, public-spec links и CRLF changed text files | DocFX/diff/audit logs, docs mapping, audit script/hash | D191.7 / coder | **Всегда** | 1 |
| E17 / REQ-191-DOCS / P2 | Issue #191 read/write/read-back и milestone evidence | Exact `gh issue view/edit` commands из Documentation; exit 0; read-back number=191, verified URL, milestone=`1.0.9-rc2`, сохранён intended body | `$EV/issue/{before,after}.json`, before/after body, write logs | D191.7 / coder | **Всегда**, write verification перед docs unit | 1 |
| E18 / REQ-191-EVIDENCE / P1 | Completeness reconciliation текущего contract с ledger | CHECK invocation: reconcile **E01–E19, rv1** against `$EV/ledger.json` и raw artifacts; результат COMPLETE только при закрытии всех applicable rows; решение CHECK записано с row statuses | `$EV/check/reconciliation-rv1.md`, CHECK verdict | CHECK | **Всегда** | 1 |
| E19 / REQ-191-API / P1 | Out-of-solution alias POC consumer | `bash "$EV/commands/alias-poc-check.sh"`; при applicable build/test exit 0, failed=0; иначе Roslyn refs evidence доказывает N/A | Script/hash, consumer audit; при applicable build/test logs | D191.1/D191.6 / coder | Есть affected consumer в `tests/nextorm.alias.poc`, что наблюдается в Roslyn inventory | 1 |

У всех строк есть artifacts; необоснованное artifacts=N/A не используется.

### CHECK re-gather budget

Владелец: **CHECK**, фактический сбор выполняют scout/coder по его адресному запросу.

- Не более **двух адресных re-gather rounds** на один CHECK invocation.
- Совокупно не более **четырёх дополнительных evidence invocations**.
- Перед запросом перечислить row IDs и недостающие виды evidence.
- Уже существующие raw artifacts сначала перечитать/сверить; не запускать продуктовую правку ради отсутствующего отчёта.
- Если бюджет исчерпан: completeness остаётся OPEN; PASS запрещён. Оркестратор получает точную классификацию evidence deficiency.
- Отсутствие отчёта само по себе не означает product FAIL, не запускает DO iteration и не обосновывает новую ревизию.

### Revision и escalation rules

Сейчас supersession **нет**: `r1/rv1` остаётся тем же контрактом.

Если действительно появляется новый required variant или меняются corrective tasks/dependencies при сохранении исходных критериев:

- PLAN явно выпускает revised plan и contract supersession;
- существующие requirement/row IDs и обязательства сохраняются;
- новые варианты получают новые IDs;
- только фактический revised plan увеличивает `r` и сбрасывает `N=1`.

Контракт нельзя ревизовать лишь потому, что evidence ещё не собрано.

## Assumptions / Uncertainty

### Что подтверждено

- Normative evidence slots, completeness gate, priority rules и escalation rules.
- Существование host validator и отсутствие repo copy.
- Coverage tools/settings/thresholds и branch-specific CI policy.
- Полный список 7 default acceptance cases и exclusion D168.
- Присутствие #183 baseline материалов.
- Access modifiers нейтральных механизмов, typed state и options setters.
- Accepted direct-relocation architecture и последовательный execution mode.

### Residual uncertainty — не gate-1 slots

| Пробел | Закрытие |
|---|---|
| Полный перечень методов/consumers и точные extension filenames не приложены к этому handoff | Reviewed specification + Roslyn inventory D191.1; объём не расширять. |
| Какие из private/protected neutral mechanisms фактически понадобятся каждой extension | Использовать минимальный фактический набор; сохранять protected contract. |
| Реальные новые/существующие test selectors и moved-path benchmark symbols | Планируемые источники; фиксировать literal selectors/case inventory перед выполнением соответствующей проверки. |
| Точная CLI allocation gate не приложена | Дословный CI command artifact D191.1; не угадывать аргументы. |
| Точные register и public article paths | Inventory ссылок/docs D191.1, EN/RU согласование D191.7. |
| Текущая доступность Podman и GitHub token | In-cycle probes и предписанное runtime recovery/write verification; не объявлены заранее доказанными внешними блокерами. |
| Stability локальных perf measurements | Одинаковая среда, параметры, raw artifacts и ограниченный paired rerun; uncertainty явно записывается. |
| Potential out-of-solution consumer | E19 с pinned observable predicate. |

Ни одно допущение не ослабляет acceptance criteria.

## Risks и DO → PLAN classification

- Основные риски: потеря state при clone/join, неверное source binding, изменение default options, sticky shared-cache mutation, чрезмерное access widening, benchmark noise и EN/RU drift.
- Изменение source-level API является ожидаемым следствием direct relocation; перечень и migration implications должен подтвердить written-spec review. Несогласованный дополнительный API break не считается автоматически допустимым.
- Runtime recovery и необходимые setup steps — **in-cycle prerequisites**, а не повод немедленно STOP.
- Недостаток evidence — сначала адресный scout/re-gather. При стойко низкой уверенности рекомендовать оркестратору `escalate` по trigger 5, даже без доказанной внешности.
- Истинный блокер вне полномочий/ресурсов — рекомендация оркестратору вызвать `escalate`.
- Приемлемое допущение записывается явно и не меняет criteria.
- Аддитивное предусловие сохраняет исходный D активным, blocked на новой активной зависимости; не делать `superseded→replacement`.
- Только фактическая замена объёма требует supersession mapping и переноса всех исходных критериев/остатка.
- Отчёт DO о «реализация завершена» не завершает D191.

Escalation/no-fourth:

1. Неясное требование — escalate.
2. Тот же дефект после одной попытки исправления — escalate **до второго исправления**.
3. Разные дефекты — escalate после третьего провального CHECK той же ревизии; **четвёртой попытки нет**.
4. Architectural/API/hard-security trade-off или risky-diff acceptance — escalate по соответствующему trigger.
5. Реально revised r+1 допустим лишь при фактическом изменении corrective plan и сохранении исходных acceptance criteria; переименование или сброс сессии не считается revised plan.

## Progress log

- PLAN r1 written to this file by coder; phase PLAN; DO not started; `plan_state=ready`.
- 2026-10-08T07:35:13Z | DO | revision r1 | iteration 1/3 | DO started; user approved written spec ("D191 spec user-approved (proceed)") | `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md` sha256 `8bf020f693f0dde347d99c17fad10f73a92c45a1b24ed4839a997b2f62c139ad`
- 2026-10-08T07:42:07Z | DO | revision r1 | iteration 1/3 | D191.2 benchmark fixtures added and BEFORE baselines captured (7 provider-extensions + 7 acceptance); D191.1/D191.2 `brief` gates exit 0; benchmark Debug build + both BDN Release runs exit 0 | `benchmarks/nextorm.benchmark/ProviderExtensionsBenchmark.cs`; `$EV/logs/perf-before-*.log`; `$EV/perf/before/*/wall.txt`; mirror `artifacts/pdca/rc2-191/r1/`
- 2026-10-08T07:42:07Z | DO | revision r1 | iteration 1/3 | D191.2 `report` validator exit 2: `validate_inner_loop` cannot represent a build/benchmark-only unit (`selected_count >= 1` required, zero unit tests selected); manual-gate note recorded, no count fabricated | `$EV/inner-loop/D191.2.report.log`
- 2026-10-08T07:42:07Z | DO | revision r1 | iteration 1/3 | BDN BEFORE runs dirtied 15 tracked `BenchmarkDotNet.Artifacts/results/*`; restored via `git checkout -- BenchmarkDotNet.Artifacts`; `benchmarks/BenchmarkDotNet.Artifacts` untouched; new benchmark file, csproj refs and raw provider-extensions export retained | `git status --porcelain`
- 2026-10-08T08:00:05Z | DO | revision r1 | iteration 1/3 | D191.3 direct relocation executed: provider fluent bodies moved to ClickHouse/PostgreSQL/SQL Server extensions; core typed state retained; CS0108 on joined `JoinCore` recovered; solution build exit 0 (0/0) | `$EV/logs/build-slnx.log`; `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs`; `src/nextorm.postgres/Extensions/PostgresEntityBuilderExtensions.cs`; `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`
- 2026-10-08T08:00:05Z | DO | revision r1 | iteration 1/3 | D191.3 recursion defect found and fixed: `ClickHouseJoinedEntityBuilderExtensions.SemiJoin/AntiJoin` self-bound (exit 134 stack overflow); receiver cast to base `EntityBuilder<Projection<...>>` like `ArrayJoin`; roslyn `refs` confirm the base target | `$EV/logs/inner-D191.3-chx-ext.log`; `$EV/logs/inner-D191.3-chx-sqlgen.log`; `$EV/logs/inner-D191.3-chx-ext-after.log`; `$EV/logs/inner-D191.3-chx-sqlgen-after.log`
- 2026-10-08T08:00:05Z | DO | revision r1 | iteration 1/3 | D191.3/D191.4 inner-loop reports assembled; both `validate_inner_loop.py report` exit 0 (14 and 11 executions); the two ClickHouse filters green (23/23, 403/403) | `$EV/inner-loop/D191.3.report.json`; `$EV/inner-loop/D191.4.report.json`; `$EV/inner-loop/brief-exit-codes.txt`
- 2026-10-08T08:00:05Z | DO | revision r1 | iteration 1/3 | Process deviation recorded: D191.3/D191.4 `brief` gates were (re-)run after the first relocation edits, not before them; both exit 0 and the scope JSONs are unchanged | `$EV/inner-loop/D191.3.brief.log`; `$EV/inner-loop/D191.4.brief.log`
- 2026-10-08T08:25:00Z | DO | revision r1 | iteration 1/3 | D191.5 variant-matrix targeted tests executed (15 filtered runs, all exit 0, 0 failed); new test files ClickHouseExtensionStateTests/ProviderExtensionCloneTests/ProviderExtensionEagerGuardTests | `$EV/logs/D191.5-batch-summary.txt`; `$EV/logs/D191.5-*.log`
- 2026-10-08T08:35:00Z | DO | revision r1 | iteration 1/3 | D191.6 boundary: Debug/Release solution builds exit 0 (0/0); full 12-project sweep 9111 total/0 failed/2639 skipped; container integration exit 0 total 3337/0 failed/197 skipped (all 5 providers incl. ClickHouse container 525af9d6a314); explicit ClickHouse class run 129/0/0 | `$EV/logs/build-slnx.log`; `$EV/logs/build-slnx-release.log`; `$EV/logs/D191.6-full-tests.log`; `$EV/logs/D191.6-integration-full.log`; `$EV/logs/D191.6-integration-clickhouse.log`
- 2026-10-08T08:35:00Z | DO | revision r1 | iteration 1/3 | D191.6 coverage: collect+reportgenerator exit 0; line 86.7% / branch 79.3% (thresholds 85/75 satisfied). Perf AFTER: acceptance 7/7 and provider-extensions 7/7 exit 0; all means <= before, R prepared/cached 2.070 -> 1.997 (0.965x); allocation gate exit 0 (56/56 within budget) | `$EV/coverage/`; `$EV/logs/perf-after-*.log`; `$EV/logs/allocation-ci.log`; `$EV/perf/{before,after}/`
- 2026-10-08T08:35:00Z | DO | revision r1 | iteration 1/3 | D191.6 report JSON assembled and host validator `report` run; exit 2 with one structural FAIL (`multiple boundary solution builds: 2`) because the report faithfully records both Debug and Release solution builds while the validator caps one per report; no `;`/shell-wrapper violations | `$EV/inner-loop/D191.6.report.json`; `$EV/inner-loop/D191.6.report.log`
- 2026-10-08T08:35:00Z | DO | revision r1 | iteration 1/3 | D191.6 artifact hygiene: BDN AFTER runs (incl. the noise-recheck repeat) modified/created tracked+untracked files under repo-root `BenchmarkDotNet.Artifacts`; restored via `git checkout --` + `git clean -f`; both `BenchmarkDotNet.Artifacts` and `benchmarks/BenchmarkDotNet.Artifacts` show no changes | `git status --porcelain BenchmarkDotNet.Artifacts benchmarks/BenchmarkDotNet.Artifacts`
- 2026-10-08T08:38:00Z | DO | revision r1 | iteration 1/3 | D191.7 docs/registers/issue: XML docs already complete on all moved public members; EN 5 + RU 5 article pages switched from `NextORM.Core.EntityBuilder.*` to `NextORM.Postgres.PostgresEntityBuilderExtensions`/`NextORM.SqlServer.SqlServerEntityBuilderExtensions` (xrefs resolve in generated xrefmap); registers `API-NAMING-REVIEW.md` + `code-smells-review.md` record the moved PG/SS surface; `dotnet docfx` exit 0 (2 pre-existing sourcegenerator warnings, 0 errors); `git diff --check` exit 0; issue #191 read/no-op-write/content-write/read-back all exit 0 | `$EV/logs/d191.7-docfx.log`; `$EV/logs/d191.7-docs-audit.log`; `$EV/issue/after.json`
- 2026-10-08T08:40:00Z | DO | revision r1 | iteration 1/3 | D191.8 evidence reconciliation: ledger E01–E19 rv1 written (17 closed; E13 open perf-allocation uncertainty; E18 open CHECK-owned); final E15 report assembled with the single-boundary-build convention (26 executions = 16 inner filtered + 10 boundary, exactly one boundary solution build Debug; Release carried separately) and host validator `report` exit 0 | `$EV/check/reconciliation-rv1.md`; `$EV/ledger.json`; `$EV/inner-loop/report.json`; `$EV/inner-loop/final-report.log`
- 2026-10-08T13:52:00Z | DO | revision r1 | iteration 2/3 | CHECK r1 FAIL received → DO n=2; loop-back defects recorded: `D191-ALLOC` (E13, +~70 B/op on 3 CH construct cases) and `D191-TEST-COMPILE` (restored `ClickHouseExtensionStateTests` did not compile). No replan: plan revision stays r1, criteria unchanged, attempt n=2/3 | `status#check-r1-fail--do-n2`; `$EV/check/reconciliation-rv1-n2.md`
- 2026-10-08T13:53:00Z | DO | revision r1 | iteration 2/3 | P1 fix (non-copying `SettingsListBacking`/`ArrayJoinsBacking` seams) and P2c test compile fix (mixed-kind guard reached via same-entity `ArrayJoin` form); Debug build exit 0 / 0 Warning(s) 0 Error(s); Release build exit 0 / 0/0; 8 affected filtered runs exit 0, 0 failed: CH state 8, PG clone 1, SS eager 2, CH surface 23, CH SqlGeneration 403, sqlite PlanCache 38, core InMemoryJoin 19, core JoinIntoRejection 15 | `$EV/logs/build-slnx-debug-n2.log`; `$EV/logs/build-slnx-release-n2.log`; `$EV/logs/D191n2-*.log`
- 2026-10-08T13:54:00Z | DO | revision r1 | iteration 2/3 | P1 allocation re-measure (`perf/after2/provider-extensions`, exit 0): CH construct allocations back to BEFORE — ArrayJoin 1.31 KB, Final_PreWhere_Settings 2.26 KB, Joined_ArrayJoin 3.4 KB → **E13 closed**; BDN artifacts restored to tracked state; loop-back report assembled and host validator `report` exit 0 (10 executions, single boundary build Debug); ledger E13/E18 closed and E18 row mapping supplied → **E18 closed** | `$EV/logs/perf-after2-provider-extensions.log`; `$EV/perf/after2/provider-extensions/`; `$EV/inner-loop/D191.loopback-n2.report.json`; `$EV/inner-loop/D191.loopback-n2.report.log`; `$EV/ledger.json`; `$EV/check/reconciliation-rv1-n2.md`
- 2026-10-08T14:02:00Z | CHECK | revision r1 | iteration 2/3 | CHECK r1 re-gather (snapshot-n2) boundary bound: `source_revision == artifact_revision`; Debug build exit 0 (0/0), Release build exit 0 (0/0), full sweep exit 0 (9115 total / 0 failed / 2639 skipped), container integration exit 0 (3337/0/197; 5 containers, all 5 providers executed, capability-level skips), explicit ClickHouse class exit 0 (129/0/0), coverage collect exit 0 (9115/0/2639) + report exit 0 (line 86.7% / branch 79.3%), perf acceptance exit 0 (7/7, all means ≤ 1.05× before), provider-extensions after2 exit 0 (7/7, allocations ≤ BEFORE) | `$EV/logs/snapn2-*.log`; `$EV/perf/after2/`; `$EV/coverage/`
- 2026-10-08T14:02:00Z | CHECK | revision r1 | iteration 2/3 | Snapshot-n2 final report assembled (25 executions, one boundary Debug solution build, Release separate) and host validator `report` exit 0; E01–E19 ledger excerpt + reconciliation refreshed; E13/E18 supported by bound evidence; no replan (plan r1, attempt n=2/3) | `$EV/inner-loop/report-snapn2.json`; `$EV/inner-loop/final-report-snapn2.log`; `$EV/check/ledger-n2.json`; `$EV/check/reconciliation-rv1-n2.md`
- 2026-10-08T09:17:00Z | CHECK | revision r1 | iteration 2/3 | CHECK r1 re-gather 2 (snapshot-n2): E14 allocation gate re-run on the current uncommitted tree — exit **0**, 706 s, `Ran 13 tests ... OK`, `All 56 row/job verdicts within budget`; per-row `contract_version: rv1` + own row rv1 + status + satisfying evidence path/command + exit code made explicit in `$EV/check/ledger-n2.json` and `$EV/check/reconciliation-rv1-n2.md` (E19 carries the plan-quoted observable N/A predicate); E15 report refreshed and host validator `report` exit 0; BDN artifacts restored to tracked state; snapshot hashes unchanged | `$EV/logs/allocation-ci-rg2.log`; `$EV/logs/allocation-ci-rg2.exit`; `$EV/check/ledger-n2.json`; `$EV/check/reconciliation-rv1-n2.md`; `$EV/inner-loop/final-report-rg2.log`

- 2026-10-08T09:20:00Z | ACT | revision r1 | iteration 2/3 | CHECK PASS (snapshot-n2 `7e9fad9` / diff `f652c19e`); D191 marked Done/Verified; issue #191 close instructed; authorized commit of D191 files | `$EV/check/reconciliation-rv1-n2.md`; `$EV/inner-loop/report-snapn2.json`; `artifacts/pdca/rc2-191/r1/`

## D191.1 — Inventory, scope brief and approval record

### Approval record

- User instruction **2026-10-08**: "D191 spec user-approved (proceed)" — explicit approval of the written specification; satisfies the DO-entry prerequisite (plan :90) and contract row E01 (:439).
- Reviewed content: `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md` — git blob `77825df33c6d4e54fafdf16e1f6fc79f092236a6`, SHA-256 `8bf020f693f0dde347d99c17fad10f73a92c45a1b24ed4839a997b2f62c139ad`.
- Specification header updated: `:6` set to `written spec — REVIEWED / APPROVED (2026-10-08)`; `:7` → implementation plan WRITTEN / PLAN-ready; `:8` → implementation AUTHORIZED for D191; `:9` marked superseded. Commits/push/merge remain unauthorized; functional scope is not expanded.
- Scope of authorization: reviewed specification + saved plan `docs/specs/status/rc2-191-provider-extensions-1.md` only.

### Dirty baseline (pre-edit)

- `git rev-parse HEAD` = `7e9fad9f443d898fe4cde2594f555c0819d7221d`; branch `1.0.9-rc2`.
- `git status --porcelain=v1`: 22 entries, all untracked (`??`); 0 modified tracked files. `git diff --binary` is empty (0 lines). Full manifest in `$EV/provenance/git-status.txt`.
- No shared-footprint file modified; existing #183 dirty material preserved; no reset/stash/clean.

### Roslyn inventory

Actions run: `members` on `NextORM.Core.EntityBuilder`1`, `NextORM.Core.EntityBuilder`, `NextORM.Core.Joins.JoinedEntityBuilder` (arity 2), `NextORM.Core.JoinedEntityBuilder`8`; invocations listed in `$EV/commands/roslyn.jsonl`.

ClickHouse — generic `EntityBuilder<TEntity>` (16 internal instance members; lines in `src/nextorm.core/Builders/EntityBuilder.cs`):

| Operation | Line(s) |
|---|---|
| Final | 1638 |
| Settings | 1649 |
| PreWhere | 1671 |
| ArrayJoin | 1695 |
| LeftArrayJoin | 1703 |
| ArrayJoinElement | 1737 |
| LeftArrayJoinElement | 1752 |
| LimitBy (2 overloads) | 2183, 2191 |
| WithTotals | 3508 |
| SemiJoin (2 overloads) | 2719, 2744 |
| AntiJoin (2 overloads) | 2731, 2747 |
| PasteJoin (2 overloads) | 2741, 2750 |

ClickHouse — non-generic `EntityBuilder` (named-table mode): `SemiJoin` :4166, `AntiJoin` :4180, `PasteJoin` :4200 (internal; 3 instance members observed vs the 6/2-overload split stated in reviewed spec §2.1 — recorded as an inventory discrepancy; not a scope change).

ClickHouse — `JoinedEntityBuilder<T1..Tn>` (32 members): SemiJoin/AntiJoin/PasteJoin for arities 2..7 plus ArrayJoin/LeftArrayJoin for arities 2..8; arity-2 lines 218/221/224/263/266; arity-8 lines 1577/1580.

ClickHouse — option methods (4): `FromOptions.Sample(double)` and `Sample(double,double)`; `JoinOptions.Global()`, `JoinOptions.WithStrictness(JoinStrictness)`.

PostgreSQL: `EntityBuilder<TEntity>.DistinctOn<TResult>` — public, `EntityBuilder.cs:1816`; destination `src/nextorm.postgres/Extensions/PostgresEntityBuilderExtensions.cs` (does not exist yet).

SQL Server: `EntityBuilder<TEntity>.Pivot` — public, `EntityBuilder.cs:2010`; `Unpivot` — public, `EntityBuilder.cs:2037`; destination `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs` (does not exist yet).

ClickHouse destination: existing `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs` (58 public extension methods; static hosts at :12 and :176).

### Pre-edit footprint hashes

Recorded in `$EV/provenance/footprint-sha256-before.txt`:

- `src/nextorm.core/Builders/EntityBuilder.cs` — 4fe99f499e25caa2274b21baed36caf8ba92c07f7834f98ff389142580446625
- `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs` — 770056357e26cce54106bf029f20e24acae3a5d6e202f1f4a387550310dd4cf1
- `src/nextorm.core/Query/QueryCommand.cs` — 95001b24886db53e13af054f2e24b7b6ed8dc5bcb343e012dfcc9f0ad4ac62b0
- `src/nextorm.core/DataContext/FromOptions.cs` — a34a11d4d43caea97f66b743b48a84a61793e983e10bcb0e07a11fba23553989
- `src/nextorm.core/Builders/JoinOptions.cs` — 7a112ec9212f230ac2bc7478ce2ded217f28f9f439c747fea38d020d90c8c52a
- `src/nextorm.core/nextorm.core.csproj` — d006b8f3492dade2872f21e76414e9eb6d3e70afba38118bef8bf89314de1e65
- `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs` — 0228c95a97de5ae4f0d24a22ad1b094c11eb0fc05867e3e5cd5d3cf3993d78d0
- `benchmarks/nextorm.benchmark/nextorm.benchmark.csproj` — 37f3dc9c142796790a27bfa4759d151c481f380b885531ff28a0ee45e08bf746

### Normative skills / validator provenance

- Host validator: `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` — SHA-256 `7d725b92ed00aa491117db6af1b66e952c5ff15143d90a8d85b9ffb3a08c3bd6`; repo `scripts/validate_inner_loop.py` absent (lane gate D167).
- `pdca-dotnet/SKILL.md` and `nextorm-pdca/SKILL.md` SHA-256 in `$EV/provenance/` (files `pdca-dotnet-skill-sha256.txt`, `nextorm-pdca-skill-sha256.txt`).
- D191.1 `brief` gate: `validate_inner_loop.py brief $EV/inner-loop/D191.1.scope.json` → exit 0 (log `$EV/inner-loop/D191.1.brief.log`). D191.1 is inventory/docs-only (`scope.rebuild=none`, no compiled edits, zero executions), so no `report` is produced for this unit (manual-gate note).

### Evidence root and planned commands

- Durable evidence root: `/tmp/nextorm-D191-r1` (plan :285); authoritative mirror copied to gitignored `artifacts/pdca/rc2-191/r1/` at end of DO.
- Planned command scripts in `$EV/commands/` (content hashes in `$EV/provenance/commands-sha256.txt`): `build.sh`, `allocation-ci.sh`, `state-tests.sh`, `join-tests.sh`, `provider-tests.sh`, `cache-tests.sh`, `alias-poc-check.sh`, `docs-audit.sh`, `coverage.sh`, `perf-before-acceptance.sh`, `perf-before-provider-extensions.sh`, `roslyn.jsonl`.

## D191.2 — Benchmark fixtures and BEFORE baselines

### Deliverables

- Added `benchmarks/nextorm.benchmark/ProviderExtensionsBenchmark.cs` (new, untracked): 7 `[Benchmark]` methods under `[BenchmarkCategory("provider-extensions")]` — `ClickHouse_Construct_Final_PreWhere_Settings`, `ClickHouse_Construct_ArrayJoin`, `ClickHouse_Joined_ArrayJoin`, `ClickHouse_Construct_PasteJoin`, `Postgres_Construct_DistinctOn`, `SqlServer_Construct_Pivot`, `SqlServer_Construct_Unpivot`; `[MemoryDiagnoser]` + `[Config(typeof(NextormConfig))]`. Fixtures use placeholder connection strings and never open a connection — only query construction/composition is measured.
- csproj refs added to `benchmarks/nextorm.benchmark/nextorm.benchmark.csproj`: `src/nextorm.clickhouse/nextorm.clickhouse.csproj` and `src/nextorm.postgres/nextorm.postgres.csproj` (the SQL Server reference was already present). No product code relocated in D191.2.

### Exact commands, exit codes and wall time

| # | Command (exact argv) | Exit | Wall |
|---|---|---|---|
| 1 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories provider-extensions --artifacts /tmp/nextorm-D191-r1/perf/before/provider-extensions` | 0 | 62 s (`$EV/perf/before/provider-extensions/wall.txt` → `EXIT=0 WALL_SECONDS=62`) |
| 2 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories acceptance --artifacts /tmp/nextorm-D191-r1/perf/before/acceptance` | 0 | 49 s (`$EV/perf/before/acceptance/wall.txt` → `EXIT=0 WALL_SECONDS=49`) |
| 3 | benchmark Debug build, log `$EV/logs/benchmark-build-debug.log` (`Build succeeded. 0 Warning(s) 0 Error(s)`, Time Elapsed 00:00:07.74) | 0 | 7.74 s |

- BDN-internal run time: provider-extensions `Run time 00:00:49 (49.73 s)`, global `00:00:50 (50.14 s)`; acceptance global `00:00:46 (46.65 s)`.
- Full logs: `$EV/logs/perf-before-provider-extensions.log`, `$EV/logs/perf-before-acceptance.log`.
- Command scripts with content hashes: `$EV/commands/perf-before-provider-extensions.sh` sha256 `2f858f4cf508cc830172128f9b636e9a3e2cc89439b2b768f78cbfec36ea6d36`, `$EV/commands/perf-before-acceptance.sh` sha256 `3e1c097970504a22bf701962598314542745edd08b51ec0bf455ec85286cda45`.
- BDN toolchain: BenchmarkDotNet v0.15.8; `Job=ShortRun`, `Toolchain=InProcessEmitToolchain`, `IterationCount=3`, `LaunchCount=1`, `WarmupCount=3`; Runtime .NET 10.0.12, SDK 10.0.401. Both logs carry the non-fatal `Failed to set up priority High ... Permission denied` warning; N=3 measurements were still produced.

### BEFORE — provider-extensions category (7/7 cases)

| Case | Mean | Allocated |
|---|---:|---:|
| `SqlServer_Construct_Unpivot` | 185.5 ns | 1.1 KB |
| `ClickHouse_Construct_PasteJoin` | 351.9 ns | 1.33 KB |
| `Postgres_Construct_DistinctOn` | 612.1 ns | 1.27 KB |
| `ClickHouse_Construct_ArrayJoin` | 800.1 ns | 1.31 KB |
| `ClickHouse_Construct_Final_PreWhere_Settings` | 1,081.5 ns | 2.26 KB |
| `SqlServer_Construct_Pivot` | 1,776.0 ns | 2.29 KB |
| `ClickHouse_Joined_ArrayJoin` | 2,960.1 ns | 3.4 KB |

### BEFORE — acceptance category (7/7 cases)

| Case | Mean | Allocated |
|---|---:|---:|
| `InMemoryBenchmarkAggregates.Nextorm_Count` | 2.392 ms | 374.22 KB |
| `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | 62.85 ms | 50.1 MB |
| `SqliteBenchmarkAny.Nextorm_Cached` | 2.001 ms | 609.45 KB |
| `SqliteBenchmarkCachedPlan.Prepared_ToList` | 918.3 us | 76.14 KB |
| `SqliteBenchmarkCachedPlan.Cached_ToList` | 1,901.0 us | 583.19 KB |
| `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | 584.9 us | 507.05 KB |
| `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | 2.015 ms | 607.69 KB |

### Validator gates

- `validate_inner_loop.py brief $EV/inner-loop/D191.1.scope.json` → exit **0**.
- `validate_inner_loop.py brief $EV/inner-loop/D191.2.scope.json` → exit **0**. (Host validator sha256 `7d725b92ed00aa491117db6af1b66e952c5ff15143d90a8d85b9ffb3a08c3bd6`.)
- `validate_inner_loop.py report $EV/inner-loop/D191.2.report.json` → exit **2** with 3× `FAIL: execution N selected_count must be an integer >= 1 (zero/missing selected tests)` (log `$EV/inner-loop/D191.2.report.log`; report JSON `$EV/inner-loop/D191.2.report.json`).
- **Manual-gate note (D191.2).** D191.2 is a benchmark/baseline unit: its executions are a compiled-input Debug build plus the two BDN benchmark sweeps, and it runs **zero unit tests**. `validate_inner_loop.py` structurally requires `selected_count >= 1` for every execution (including boundary/non-dotnet), so it cannot represent a build/benchmark-only unit without fabricating a test-selection count. The scope/report JSONs and the raw exit `2` are recorded as-is (no evidence fabricated or adjusted); the functional gate for D191.2 is the BDN raw evidence above. The final E15 `report.json` gate belongs to the D191.5/D191.8 test units.

### Evidence and hygiene

- Durable mirror: `artifacts/pdca/rc2-191/r1/` (gitignored), copied from `/tmp/nextorm-D191-r1`; the raw provider-extensions BDN export (`NextORM.Benchmark.ProviderExtensionsBenchmark-report.{csv,md,html}`) is additionally copied into `.../perf/before/provider-extensions/`.
- BDN tracked-artifact hygiene: the acceptance BEFORE runs regenerated 15 tracked files under repo-root `BenchmarkDotNet.Artifacts/results/`; they were restored with `git checkout -- BenchmarkDotNet.Artifacts`. `benchmarks/BenchmarkDotNet.Artifacts` showed **no** changes. The new benchmark file, the csproj refs and the raw provider-extensions report export were retained.

## D191.3 — Core neutral seams and direct relocation

### Moved-member inventory

Core `EntityBuilder.cs`/`JoinedEntityBuilder.cs` lost the provider-specific fluent bodies; the typed AST/state stays in core and the members are re-exposed as public extension methods in the provider assemblies.

- ClickHouse `EntityBuilder<TEntity>` → `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs` (public): `Final`, `Settings`, `PreWhere`, `ArrayJoin`, `LeftArrayJoin`, `ArrayJoinElement`, `LeftArrayJoinElement`, `LimitBy` (2 overloads), `WithTotals`, `SemiJoin` (2 overloads), `AntiJoin` (2 overloads), `PasteJoin` (2 overloads).
- ClickHouse named-table mode (`EntityBuilder` non-generic) → same file: `SemiJoin`, `AntiJoin`, `PasteJoin`, each in two receiver overload groups (`EntityBuilder` receiver + `EntityBuilder<TJoinEntity>` receiver).
- ClickHouse `JoinedEntityBuilder<T1..Tn>` → `ClickHouseJoinedEntityBuilderExtensions` (same file): `SemiJoin`/`AntiJoin`/`PasteJoin` for arities 2..7 and `ArrayJoin`/`LeftArrayJoin` for arities 2..8.
- ClickHouse option mutators → same file: `FromOptions.Sample(double)`, `Sample(double,double)`, `JoinOptions.Global()`, `JoinOptions.WithStrictness(JoinStrictness)`.
- PostgreSQL `EntityBuilder<TEntity>.DistinctOn<TEntity,TResult>` → new `src/nextorm.postgres/Extensions/PostgresEntityBuilderExtensions.cs`.
- SQL Server `EntityBuilder<TEntity>.Pivot` / `Unpivot` → new `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`.

### Spec-count reconciliation (no gap)

D191.1 flagged the ClickHouse non-generic named-table surface as "3 observed members vs the 6/2-overload split stated in reviewed spec §2.1". Consolidated reconciliation: the non-generic ClickHouse surface is exactly **6 public extension methods = 3 methods × 2 overload groups** — `SemiJoin`, `AntiJoin`, `PasteJoin`, each with an `EntityBuilder from` (named-table) overload and an `EntityBuilder<TJoinEntity> join` overload. The earlier "3" was only the base member count; there is no missing or extra operation and no scope change.

### Changed / new files

- Core: `src/nextorm.core/Builders/EntityBuilder.cs` (-510 net), `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs` (-108 net), `src/nextorm.core/Builders/JoinOptions.cs`, `src/nextorm.core/DataContext/FromOptions.cs`, `src/nextorm.core/nextorm.core.csproj`.
- Provider extension files: modified `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs`; new `src/nextorm.postgres/Extensions/PostgresEntityBuilderExtensions.cs`; new `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`.
- Neutral seams: `JoinOptions.Strictness`/`IsGlobal` and `FromOptions.SampleRatio`/`SampleOffset` setters widened `private set` → `set` (still internal) so the moved option mutators write through the existing typed state; `JoinedEntityBuilder.JoinCore<T..>` added as `internal new` typed seams; `nextorm.core.csproj` adds `InternalsVisibleTo nextorm.postgres` and `InternalsVisibleTo nextorm.sqlserver` (ClickHouse was already present).
- Consumers: `src/nextorm.postgres/PostgresDataContext.cs`, `src/nextorm.sqlserver/SqlServerDataContext.cs`; provider references added in `tests/nextorm.{clickhouse,core,mariadb,mysql,postgres,sqlite,sqlserver}.tests/*.csproj`; test sources updated across `tests/nextorm.*.tests/**` and `tests/nextorm.integration.tests/**`.

### CS0108 recovery

The first affected build (`build-D191.3-attempt1.log`) failed with 6× `CS0108`: `JoinedEntityBuilder<T1..Tn>.JoinCore<T..>` hid the inherited `EntityBuilder<Projection<T1..Tn>>.JoinCore<TJoinEntity>`. Recovered by declaring the typed joined seams as `internal new JoinedEntityBuilder<...> JoinCore<T..>(EntityBuilder<T..>, JoinType, LambdaExpression?, Action<JoinOptions>?)`. After fixups through attempts 2–5, attempts 6 and 7 built with 0 errors; the final `dotnet build nextorm.slnx -c Debug` is exit 0, **0 Warning(s) / 0 Error(s)** (`$EV/logs/build-slnx.log`).

### Recursion defect and fix

`ClickHouseJoinedEntityBuilderExtensions.SemiJoin`/`AntiJoin` (all 6 arity groups) bound `builder.SemiJoin(...)`/`builder.AntiJoin(...)` back to themselves, because `JoinedEntityBuilder<T1,T2>` is a more specific receiver than the base `EntityBuilder<Projection<T1,T2>>` → infinite recursion / `Stack overflow` (exit 134) in `ClickHouseExtensionSurfaceTests` and `SqlGenerationTests`. Fixed exactly like the already-fixed `ArrayJoin`/`LeftArrayJoin`: cast the receiver to the base builder before invoking the base extension, `(JoinedEntityBuilder<T1,T2>)((EntityBuilder<Projection<T1,T2>>)builder).SemiJoin(join, joinCondition, options)` (same for `AntiJoin`). Roslyn `refs` on `ClickHouseEntityBuilderExtensions.SemiJoin`/`AntiJoin` now resolve the 6 joined call sites to the base extension (`ClickHouseEntityBuilderExtensions.cs:350/362/374/386/398/410` and `:354/366/378/390/402/414`), not to themselves.

### Inner-loop executions (selected counts)

- ClickHouse `ClickHouseExtensionSurfaceTests`: fail exit 134 (9 selected) → pass exit 0 (23 selected).
- ClickHouse `SqlGenerationTests`: fail exit 134 (343 selected) → pass exit 0 (403 selected).
- core `EagerLoadingSingleQueryP1Tests` 67; `InMemoryTests` 154; `InMemoryJoinTests` 19; `JoinIntoRejectionTests` 15.
- `SqlGenerationTests` on mysql 237, postgres 712, sqlite 590, sqlserver 490; sqlite `PlanKeyUniquenessTests` 10. All final runs exit 0, 0 failed.

### Validator gates (D191.3)

- `validate_inner_loop.py brief $EV/inner-loop/D191.3.scope.json` → exit **0** (`$EV/inner-loop/D191.3.brief.log`).
- `validate_inner_loop.py report $EV/inner-loop/D191.3.report.json` → exit **0** (`$EV/inner-loop/D191.3.report.log`); 14 executions = 1 boundary solution build + 13 filtered test runs. The build execution records `selected_count=25` (number of projects compiled by the solution); the validator requires a positive integer for every execution although a build selects no tests — recorded explicitly rather than fabricating a test count.
- **Process deviation (recorded).** The D191.3/D191.4 `brief` gates were (re-)run after the first relocation edits instead of before them. Both exit 0 and the scope JSONs are unchanged, but the contract's pre-edit ordering was not observed on the first pass (`$EV/inner-loop/brief-exit-codes.txt`).

## D191.4 — Clone/join/options correctness

- No additional product edits beyond D191.3; this unit verifies the relocation's clone/join/options invariants through the filtered join/clone/options suites rather than changing code.
- Validator gates: `validate_inner_loop.py brief $EV/inner-loop/D191.4.scope.json` → exit **0**; `validate_inner_loop.py report $EV/inner-loop/D191.4.report.json` → exit **0** (`$EV/inner-loop/D191.4.report.log`); 11 executions = 1 boundary solution build + 10 filtered test runs.
- Covered suites and selected counts: `InMemoryJoinTests` 19, `JoinIntoRejectionTests` 15, `InMemoryTests` 154, sqlite `PlanKeyUniquenessTests` 10, and `SqlGenerationTests` on ClickHouse 343→403 (after the recursion fix), mysql 237, postgres 712, sqlite 590, sqlserver 490. All final runs exit 0, 0 failed.
- Container-backed provider execution semantics and the full solution sweep remain the D191.6 boundary; no integration run is claimed here.

## D191.5 — Targeted tests and inner-loop report

### Variant-matrix closure

No product edits beyond D191.3/D191.4; this unit exercises the relocated surface through the filtered variant suites. New test files added: `tests/nextorm.clickhouse.extensions.tests/ClickHouseExtensionStateTests.cs`, `tests/nextorm.postgres.tests/ProviderExtensionCloneTests.cs`, `tests/nextorm.sqlserver.tests/ProviderExtensionEagerGuardTests.cs` (plus the D191.2 `benchmarks/nextorm.benchmark/ProviderExtensionsBenchmark.cs` fixture).

| Variant(s) | Suites (selected) |
|---|---|
| V-DEFAULT / V-TYPES / V-COPY / V-HAVING / V-EAGER | core `InMemoryTests` 154, `EagerLoadingSingleQueryP1Tests` 67 (D191.3), ClickHouse `ClickHouseExtensionStateTests` 5, SqlServer `ProviderExtensionEagerGuardTests` 2, Postgres `ProviderExtensionCloneTests` 1 |
| V-JOIN / V-JOIN-OPTIONS | core `InMemoryJoinTests` 19, `JoinIntoRejectionTests` 15, sqlite `PlanKeyUniquenessTests` 10 + `SqlGenerationTests` 590 |
| V-CH / V-PG / V-OTHER / V-NEUTRAL | ClickHouse `ClickHouseExtensionSurfaceTests` 23 + `SqlGenerationTests` 403, Postgres `DistinctOn` 15, SqlServer `Pivot|Unpivot` 15 |
| V-CACHE / V-TEMP | sqlite `PlanCacheTests` 38 |
| V-TVP | core `TvpMetadataCacheTests` 4, ClickHouse `TableValuedParameterTests` 23 |
| V-PROVIDERS | all provider `SqlGenerationTests` + D191.6 container integration (below) |
| V-ALIAS-POC | `alias-poc-check.sh` inventory (D191.1); `nextorm.alias.tests` green in the D191.6 boundary sweep |

### Exact commands (argument arrays), exit codes and totals

| # | Command (exact argv) | Exit | Total / failed / skipped |
|---|---|---:|---:|
| 1 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~InMemoryTests"]` | 0 | 154 / 0 / 0 |
| 2 | `["dotnet","test","tests/nextorm.clickhouse.extensions.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~ClickHouseExtensionStateTests"]` | 0 | 5 / 0 / 0 |
| 3 | `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~ProviderExtensionEagerGuardTests"]` | 0 | 2 / 0 / 0 |
| 4 | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~ProviderExtensionCloneTests"]` | 0 | 1 / 0 / 0 |
| 5 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~InMemoryJoinTests"]` | 0 | 19 / 0 / 0 |
| 6 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinIntoRejectionTests"]` | 0 | 15 / 0 / 0 |
| 7 | `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~PlanKeyUniquenessTests"]` | 0 | 10 / 0 / 0 |
| 8 | `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~SqlGenerationTests"]` | 0 | 590 / 0 / 0 |
| 9 | `["dotnet","test","tests/nextorm.clickhouse.extensions.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~ClickHouseExtensionSurfaceTests"]` | 0 | 23 / 0 / 0 |
| 10 | `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~SqlGenerationTests"]` | 0 | 403 / 0 / 0 |
| 11 | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~DistinctOn"]` | 0 | 15 / 0 / 0 |
| 12 | `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Pivot|FullyQualifiedName~Unpivot"]` | 0 | 15 / 0 / 0 |
| 13 | `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~PlanCacheTests"]` | 0 | 38 / 0 / 0 |
| 14 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~TvpMetadataCacheTests"]` | 0 | 4 / 0 / 0 |
| 15 | `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~TableValuedParameterTests"]` | 0 | 23 / 0 / 0 |

- Raw batch transcript: `$EV/logs/D191.5-batch-summary.txt` (driver `$EV/commands/run-batch.sh`); per-run logs `$EV/logs/D191.5-*.log`. All 15 runs exit 0, failed=0, skipped=0.
- No dedicated `report` JSON was emitted for D191.5; its selectors/executions are carried into the D191.8 final report (E15).

## D191.6 — Boundary, integrations, coverage and perf

### Builds and full sweep (E08/E09)

| Command (exact argv) | Exit | Result | Log |
|---|---:|---|---|
| `["dotnet","build","nextorm.slnx","-c","Debug"]` | 0 | 0 Warning(s) / 0 Error(s), 25 projects, 2.35 s | `$EV/logs/build-slnx.log` |
| `["dotnet","build","nextorm.slnx","-c","Release"]` | 0 | 0 Warning(s) / 0 Error(s), 25 projects, 17.12 s | `$EV/logs/build-slnx-release.log` |
| `["dotnet","test","nextorm.slnx","-c","Debug","--no-build","--verbosity","normal"]` | 0 | 9111 total / 0 failed / 6472 succeeded / 2639 skipped, 18.63 s | `$EV/logs/D191.6-full-tests.log` |

- The sweep ran **12** test assemblies (core, clickhouse, clickhouse.extensions, sqlserver, publicextensibility, mysql, mariadb, alias, integration, entityframeworkcore, postgres, sqlite); the plan's E09 text says 11 — recorded as an inventory discrepancy, not a scope change.
- Source/artifact revision for every D191.6 execution: `7e9fad9f443d898fe4cde2594f555c0819d7221d` (branch `1.0.9-rc2`), `source_revision == artifact_revision`.

### Container-backed integrations (E10)

- `["dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--no-build","--","-noColor"]` (with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) → exit **0**, `Total: 3337, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0`, 60 s (`$EV/logs/D191.6-integration-full.log`).
- All five providers were **actually executed**: SQLite (no container), PostgreSQL, SQL Server, MySQL and ClickHouse. Five reusable containers were started, including the ClickHouse container `525af9d6a314`. xUnit v3's native runner prints only skipped tests, so passing ClickHouse tests produce no per-test lines — the container start is the executed-provider proof.
- `["dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--no-build","--","-class","NextORM.Integration.Tests.ClickHouseIntegrationTests","-class","NextORM.Integration.Tests.ClickHouseImplicitNavigationTests","-noColor"]` → exit **0**, `Total: 129, Errors: 0, Failed: 0, Skipped: 0`, 13 s (`$EV/logs/D191.6-integration-clickhouse.log`), explicitly proving the 129 ClickHouse cases run rather than skip.
- The 197 skips are **capability-level**, not provider-level: MySQL 80, SQL Server 48, SQLite 40, PostgreSQL 26 (unsupported constructs such as no native multi-table DELETE / no APPLY / no CTAS / no EXCEPT ALL) plus 3 harness skips (`LobPerfHarnessTests` 1 `NEXTORM_LOB_PERF=1`, `LobCapabilityProbeTests` 2 `NEXTORM_LOB_PROBE=1`). No provider was hidden as skipped.

### Coverage (E11)

- `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal"]` → exit **0** (9111 / 0 failed) (`$EV/logs/coverage-collect.log`).
- `["dotnet","tool","run","reportgenerator","-reports:tests/coverage/coverage.cobertura.xml","-targetdir:tests/coverage/report","-reporttypes:Html;TextSummary;Cobertura"]` → exit **0**.
- Scope = `nextorm.core`, `nextorm.sqlite`, `nextorm.postgres`, `nextorm.sqlserver` (4 assemblies / 564 classes / 326 files). **Line 86.7%** (47224 of 54446), **Branch 79.3%** (25533 of 32181), Method 77.1%, Full method 62.6%. Both thresholds (line 85 / branch 75) are satisfied; on `1.0.9-rc2` they are warning-only, but neither warning would fire.
- Summary + cobertura copied to `$EV/coverage/` (`Summary.txt`, `coverage.cobertura.xml`, `report-Cobertura.xml`); generated report path `tests/coverage/report/`.

### Performance AFTER (E12/E13/E14)

After commands (exact argv, all exit 0):

| Command | Exit | Wall | Log |
|---|---:|---:|---|
| `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories","acceptance","--artifacts","/tmp/nextorm-D191-r1/perf/after/acceptance"]` | 0 | 55 s | `$EV/logs/perf-after-acceptance.log` |
| `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories","provider-extensions","--artifacts","/tmp/nextorm-D191-r1/perf/after/provider-extensions"]` | 0 | 47 s | `$EV/logs/perf-after-provider-extensions.log` |
| `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories","provider-extensions","--artifacts","/tmp/nextorm-D191-r1/perf/after-repeat/provider-extensions"]` (noise re-check) | 0 | 55 s | `$EV/logs/perf-after-provider-extensions-repeat.log` |

- E12 acceptance, 7/7 cases, Mean after / before (ratio): `Nextorm_Count` 2.264/2.392 ms (0.946), `Nextorm_GroupByCount` 60.83/62.85 ms (0.968), `Nextorm_Cached` 1.902/2.001 ms (0.951), `Prepared_ToList` 921.9/918.3 us (1.004), `Cached_ToList` 1841.0/1901.0 us (0.968), `Cached_PlanOnly_Param` 549.1/584.9 us (0.939), `Nextorm_Cached_ToListAsync` 1.937/2.015 ms (0.961). All ratios **≤ 1.05**; only `Prepared_ToList` is +0.4% (within the ~19% run spread). Allocated bytes/op equal or lower (`Nextorm_Cached` 609.45→609.42 KB, `Nextorm_Cached_ToListAsync` 607.69→607.68 KB; the rest unchanged).
- E13 `R = Cached_ToList.Mean / Prepared_ToList.Mean`: **R_before = 2.070**, **R_after = 1.997**; `R_after = 0.965 × R_before` (limit `R_after ≤ 1.05 × R_before`) → pass.
- E13 provider-extensions, 7/7 cases, Mean after / before (ratio): `SqlServer_Construct_Unpivot` 143.7/185.5 ns (0.775), `ClickHouse_Construct_PasteJoin` 186.4/351.9 ns (0.530), `Postgres_Construct_DistinctOn` 476.9/612.1 ns (0.779), `ClickHouse_Construct_ArrayJoin` 638.8/800.1 ns (0.798), `ClickHouse_Construct_Final_PreWhere_Settings` 841.4/1081.5 ns (0.778), `SqlServer_Construct_Pivot` 1613.5/1776.0 ns (0.909), `ClickHouse_Joined_ArrayJoin` 2024.1/2960.1 ns (0.684). All means improved.
- **Flagged uncertainty (not a pass):** the `Allocated` column for three ClickHouse construct cases is reproducibly higher than the D191.2 baseline — `ClickHouse_Construct_ArrayJoin` 1.31→1.38 KB, `ClickHouse_Construct_Final_PreWhere_Settings` 2.26→2.33 KB, `ClickHouse_Joined_ArrayJoin` 3.4→3.46 KB (~+70 B/op). The noise re-check reproduced the same values (1.38 / 2.33 / 3.46 KB), so this is **not** run-to-run noise; the other four cases are unchanged. The plan requires allocated bytes not to increase, so this is recorded as an open perf-classification item for CHECK/ACT rather than a PASS. The pinned allocation gate (which covers the query/plan-cache path) is green.
- E14 allocation gate: `["python3","-m","unittest","discover","-s","eng/perf/tests","-p","test_iteration14_gate.py"]` → exit **0** (Ran 13 tests, OK); `["bash","/tmp/nextorm-D191-r1/commands/allocation-ci.sh"]` → exit **0**, wall 761 s, `All 56 row/job verdicts within budget` (`$EV/logs/allocation-ci.log`). The gate ran the CI argv from `.github/workflows/dotnet.yml:46-52` (`--filter *SqliteBenchmarkWarmDecompose* *SqliteBenchmarkCachedPlan* *SqliteBenchmarkFeaturePlanBuild* --job short --exporters json`).

### Report JSON and validator

- `$EV/inner-loop/D191.6.report.json`: 11 nonempty executions (Debug build, Release build, full sweep, integration sweep, explicit ClickHouse class run, coverage collect, three benchmark runs, unittest discovery, iteration14 gate), `source_revision == artifact_revision`, all `selected_count ≥ 1` (build 25 = compiled projects; benchmark 7 = cases; gate 56 = row/job verdicts; documented rather than fabricated).
- `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py report "$EV/inner-loop/D191.6.report.json"` → exit **2** with a single structural `FAIL: multiple boundary solution builds: 2` (`$EV/inner-loop/D191.6.report.log`). The validator caps one solution build per report; D191.6 legitimately builds Debug **and** Release, so the faithful report cannot satisfy this cap. No hygiene/`;`/selected_count violations. The final E15 report (D191.8) must use the single-solution-build convention.
- `reportgenerator` is not listed as an execution: its `-reporttypes:Html;TextSummary;Cobertura` token contains `;`, which the validator's shell-wrapper rule rejects; its exit 0 and output are recorded above and in `$EV/logs/coverage-report.log`.

### Artifact hygiene

- The BDN AFTER runs (and the noise re-check repeat) modified tracked files and created untracked `NextORM.Benchmark.ProviderExtensionsBenchmark-report-*` under repo-root `BenchmarkDotNet.Artifacts`. Both `BenchmarkDotNet.Artifacts` and `benchmarks/BenchmarkDotNet.Artifacts` were restored to tracked state with `git checkout --` + `git clean -f`; `git status --porcelain BenchmarkDotNet.Artifacts benchmarks/BenchmarkDotNet.Artifacts` is empty. Raw AFTER exports are preserved under `$EV/perf/after/`.

## D191.7 — Documentation, registers and issue

### XML documentation

- All new/changed public members of the relocated extension classes already carry member-complete XML docs (class `<summary>` + per-member `<summary>`/`<param>`; `CS1591` is gated by `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`): `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs`, `src/nextorm.postgres/Extensions/PostgresEntityBuilderExtensions.cs`, `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`. No member required a doc edit; the Debug/Release builds stay **0 warnings / 0 errors** (E08).

### Public docs EN/RU (old API → relocated API)

- EN (**5** files) and RU (**5** files) article pages were switched from `NextORM.Core.EntityBuilder.*` xrefs to the new provider extension classes and now name the required `using NextORM.Postgres;` / `using NextORM.SqlServer;`:
  - `docs/advanced/api-reference.md` (+RU): new `PostgresEntityBuilderExtensions` / `SqlServerEntityBuilderExtensions` rows in the namespace tables.
  - `docs/guide/03-grouping-and-aggregates.md` (+RU) and `docs/guide/07-distinct.md` (+RU): `DistinctOn` xrefs → `NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(...)`.
  - `docs/guide/provider-specific/postgresql.md` (+RU): `DistinctOn` xref + extension/`using` note.
  - `docs/guide/provider-specific/sqlserver.md` (+RU): `Pivot`/`Unpivot` xrefs → `NextORM.SqlServer.SqlServerEntityBuilderExtensions.*` + extension/`using` note.
- Verified `rg -n 'xref:NextORM\.Core\.EntityBuilder`1\.(DistinctOn|Pivot|Unpivot)'` over article docs → **none**. Generated `docs/api/**` and `docs/_site/**` untouched; no page renumbering; no public docs link to `docs/specs/**`.

### Internal registers

- `docs/specs/design/API-NAMING-REVIEW.md`: new dated audit section **issue #191** recording the moved public surface (`N191-1` BC/Шаг 5, `N191-2` naming/форма), alongside the existing CHX3/CHX4 ClickHouse rows.
- `docs/specs/design/code-smells-review.md`: new dated audit section **issue #191** recording the relocation and the D191.3 `JoinedEntityBuilder` recursion fix (`N191-C1`).
- Author≠certifier preserved: coder records the surface; CHECK certifies (E18).

### DocFX, CRLF and diff

- `dotnet docfx docs/docfx.json` → exit **0**, `Build succeeded with warning`: the 2 warnings are the pre-existing `nextorm.core.sourcegenerator` duplicate `AnalyzerReleases.*.md` files; **0 errors**; no unresolved/invalid xref warnings. All new method xrefs resolve (present in `docs/_site/xrefmap.yml`).
- `bash "$EV/commands/docs-audit.sh"` (docfx + `git diff --check`) → exit **0**.
- CRLF normalized on all 12 edited docs/register files; `git diff --check` clean.

### Issue #191 write access (E17)

- `gh issue view 191 --repo AlexeyShirshov/nextorm` → exit 0; number 191, URL `https://github.com/AlexeyShirshov/nextorm/issues/191`, milestone `1.0.9-rc2`.
- No-op write proof: `gh issue edit 191 --body-file "$EV/issue/body.before.md"` → exit **0**.
- Content update: `gh issue edit 191 --body-file "$EV/issue/body.after.md"` → exit **0**; read-back `gh issue view 191 --json number,url,milestone,body` → exit 0, body matches intended, adds `## Результат реализации (DO r1)` + evidence + limitations.
- Artifacts: `$EV/issue/{before,after}.json`, `body.before.md`, `body.after.md`, `noop-write.log`, `update-write.log`, `body.after.readback.md`.

## D191.8 — Evidence reconciliation and CHECK handoff

### Row reconciliation (E01–E19, rv1)

- Ledger: `$EV/ledger.json`; table: `$EV/check/reconciliation-rv1.md`.
- **17 closed**: E01, E02, E03, E04, E05, E06, E07, E08, E09, E10, E11, E12, E14, E15, E16, E17, E19 (each with a recorded evidence path).
- **2 open**: **E13** (P1) — mean ratios pass, but allocated bytes/op increased reproducibly on three ClickHouse construct cases (~+70 B/op; the noise re-check reproduced the same values); routed to CHECK/ACT perf classification rather than a PASS. **E18** (P1, CHECK-owned) — completeness verdict pending; coder supplied the ledger.
- No row is N/A without a proven predicate. D191.2 `report` exit 2 (build/benchmark-only unit cannot express `selected_count ≥ 1`) and D191.6 `report` exit 2 (multiple boundary solution builds) are recorded as structural validator limitations, not hidden.

### Final E15 report (single-boundary-build convention)

- `$EV/inner-loop/report.json`: **26 executions** = 16 inner filtered variant runs (D191.5) + 10 boundary executions (D191.6 sweeps/coverage/perf/gate) **minus both D191.6 solution builds**; exactly **one** boundary solution build (Debug) is retained, satisfying the validator's one-build cap. The Release solution build is carried as a separate non-validator check (`$EV/logs/build-slnx-release.log`), documented rather than dropped.
- `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py report "$EV/inner-loop/report.json"` → exit **0** (`$EV/inner-loop/final-report.log`).

### Handoff

- CHECK receives: the ledger (`ledger.json`), the reconciliation table (`check/reconciliation-rv1.md`), the final report + validator log, and the raw exit codes/artifacts referenced above.
- D191 is **not** marked done; D191.7/D191.8 are complete and pending CHECK. No commit/push.
- Evidence mirror refreshed: `artifacts/pdca/rc2-191/r1/` ← `/tmp/nextorm-D191-r1`.

## Done / Verified

- **Done: D191** (ACT, 2026-10-08). CHECK PASS on snapshot-n2 `7e9fad9f` (diff `f652c19e`); D191.1–D191.8 executed, committed in ACT.
- **Verified: yes** (CHECK snapshot-n2). Debug/Release builds exit 0 (0/0); full sweep 9115 total / 0 failed / 2639 skipped; container integration 3337/0/197 (all 5 providers executed, capability-level skips); explicit ClickHouse class 129/0/0; coverage line 86.7% / branch 79.3%; perf acceptance 7/7 all Mean ≤ 1.05× BEFORE; provider-extensions 7/7 allocations ≤ BEFORE (`R_after` 2.074 ≤ 1.05×`R_before` 2.070); allocation gate 56/56; E01–E19 rv1 all applicable rows closed (E19 N/A with the pinned Roslyn predicate). Evidence mirror `artifacts/pdca/rc2-191/r1/` (from `/tmp/nextorm-D191-r1`); `$EV/check/ledger-n2.json`; `$EV/check/reconciliation-rv1-n2.md`; `$EV/inner-loop/report-snapn2.json`; `$EV/logs/snapn2-*.log`.
- **Issue #191 close instruction (ACT):** `gh issue close 191 --repo AlexeyShirshov/nextorm --comment "D191 complete: provider-specific fluent API relocated out of core into provider packages; CHECK PASS (snapshot-n2). Evidence artifacts/pdca/rc2-191/r1/."`
- (historical) previous DO handoff open items **E13**/**E18** are closed by the ACT verification above.

## CHECK r1 FAIL → DO n=2

**Durable state.** Cycle 1, plan revision **r1** (без replan: contract/criteria rv1 не менялись, новые вариантирующие требования не появлялись), CHECK revision **r1 FAIL**, DO iteration **n=2/3**. Defect history:

| Defect key | Class / row | Наблюдался | Фиксов | Итог | Evidence |
|---|---|---|---:|---|---|
| `D191-ALLOC` | P1 perf / E13 | r1/n1 — 3 ClickHouse construct-кейса +~70 B/op | 1 (r1/n2) | **closed** | `$EV/logs/perf-after2-provider-extensions.log`; `$EV/perf/{before,after2}/provider-extensions/` |
| `D191-TEST-COMPILE` | P2c test / E04 | r1/n2 — `ClickHouseExtensionStateTests.cs(137)` не компилировался (`LeftArrayJoinElement` после `ArrayJoinElement` биндится к `ArrayJoinProjection<,>`, не к `EntityBuilder<>`) | 1 (r1/n2) | **closed** | `$EV/logs/D191n2-chx-state.log` |

**CHECK defects и corrective tasks.** P1 — allocation-регрессия: релокация провела `Settings`/`ArrayJoin` через защитно-копирующие сеттеры (+~70 B/op). P2a — правки регистров (`API-NAMING-REVIEW.md` N191-1/N191-2, `code-smells-review.md` N191-C1) уже внесены в дерево. P2b — seam/IVT-примечание: non-copying `internal`-швы `EntityBuilder.SettingsListBacking`/`ArrayJoinsBacking` отдают builder'у его собственную копию после clone/join (без алиасинга/leak) и `InternalsVisibleTo nextorm.postgres`/`nextorm.sqlserver` (минимальный грант, как для ClickHouse). P2c — восстановленный/добавленный CH-guard-тест плюс `ProviderExtensionCloneTests`/`ProviderExtensionEagerGuardTests`.

**P1 fix.** `Settings`/`ArrayJoin` переведены на non-copying seam'ы; лишняя перекопировка убрана. Re-measure (`--anyCategories provider-extensions`, exit 0): `ClickHouse_Construct_ArrayJoin` 1.31→1.31 KB, `ClickHouse_Construct_Final_PreWhere_Settings` 2.26→2.26 KB, `ClickHouse_Joined_ArrayJoin` 3.4→3.4 KB — все **≤ BEFORE** (лимиты 1.31/2.26/3.40 KB); все 7 means ≤ before. **E13 закрыт.**

**P2c fix.** `ArrayJoinElement` меняет тип ресивера, поэтому mixed-kind guard в `ToArrayJoinElement` недостижим цепочкой `ArrayJoinElement().LeftArrayJoinElement()`; тест переписан на same-entity `ArrayJoin` форму (`e.ArrayJoin(x => x.Tags).LeftArrayJoinElement(x => x.Nums)` + обратное направление) и проверяет оба entry point'а `ToArrayJoinElement`.

**Build/evidence anchors.** `dotnet build nextorm.slnx -c Debug` → exit **0**, `0 Warning(s) 0 Error(s)` (`$EV/logs/build-slnx-debug-n2.log`); `... -c Release` → exit **0**, `0/0` (`$EV/logs/build-slnx-release-n2.log`). Affected filtered runs (все exit 0 / failed 0): CH state 8, PG clone 1, SS eager 2, CH surface 23, CH SqlGeneration 403, sqlite PlanCache 38, core InMemoryJoin 19, core JoinIntoRejection 15 (`$EV/logs/D191n2-*.log`). Loop-back report `validate_inner_loop.py report` → exit **0** (10 executions, один boundary solution build Debug; Release — отдельным non-validator check) (`$EV/inner-loop/D191.loopback-n2.report.{json,log}`). `BenchmarkDotNet.Artifacts` (root + benchmarks) восстановлены к tracked-состоянию (`git status --porcelain` пуст).

**E13/E18.** E13 закрыт after2-числами; E18 закрыт row mapping'ом `E01–E19 rv1` (все applicable строки closed: E08 — Debug/Release 0/0, E13 — after2, E15 — loop-back report exit 0; N/A без доказанного предиката нет) — `$EV/ledger.json`, `$EV/check/reconciliation-rv1-n2.md`. Mirror `artifacts/pdca/rc2-191/r1/` обновлён из `$EV`. D191 **не** помечен done — завершение определяет CHECK. Commit/push не выполнялись.

**Остаток.** CHECK re-review loop-back evidence (E13/E18 закрыты coder'ом; финальный completeness-вердикт остаётся за CHECK).

## CHECK r1 re-gather (snapshot-n2)

**Snapshot `snapshot-n2`.** HEAD `7e9fad9f443d898fe4cde2594f555c0819d7221d`, branch `1.0.9-rc2`; `git diff | sha256sum` = `f652c19e8489ddf8da10e1ff4eb5c4e6dff8c4024d1aa7d46704efd48659fdb7`; `git status --porcelain | sha256sum` = `a0e9c3cdad8c9da7e3bbcd2757c7f6d802c8a6386c2592b243f736622947798b` (73 entries, 45 modified tracked). Provenance: `$EV/provenance/snapshot-n2.txt`. This re-gather performed **no product edits**, no worktree/branch, no commit/push/merge.

**Checklist status.** Head `Phase: DO` unchanged; durable state `Current cycle 1`, `Plan revision r1`, `Attempt n=2/3`; defect history unchanged (`D191-ALLOC` 1 fix closed, `D191-TEST-COMPILE` 1 fix closed); no new defect, no replan.

Boundary evidence bound to this snapshot (`source_revision == artifact_revision` for every boundary execution):

| Gate | Exact argv | Exit | Result | Log |
|---|---|---:|---|---|
| Debug build (E08) | `["dotnet","build","nextorm.slnx","-c","Debug"]` | **0** | 0 Warning(s) / 0 Error(s), 25 projects | `$EV/logs/snapn2-build-debug.log` |
| Release build (E08) | `["dotnet","build","nextorm.slnx","-c","Release"]` | **0** | 0 Warning(s) / 0 Error(s) | `$EV/logs/snapn2-build-release.log` |
| Full sweep (E09) | `["dotnet","test","nextorm.slnx","-c","Debug","--no-build","--verbosity","normal"]` | **0** | 9115 total / 0 failed / 2639 skipped (integration skips expected without DOCKER_HOST) | `$EV/logs/snapn2-full-tests.log` |
| Container integration (E10) | `DOCKER_HOST=<socket> dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor` | **0** | 3337 total / 0 failed / 197 skipped; 5 containers incl. ClickHouse `525af9d6a314`, SQL Server `4ab2d4542432`, PostgreSQL `ff3bb6dd042f`, MySQL `d703aedb9b21`; all 5 providers executed | `$EV/logs/snapn2-integration-full.log` |
| Explicit ClickHouse class (E10) | `... -class ClickHouseIntegrationTests -class ClickHouseImplicitNavigationTests -noColor` | **0** | 129 / 0 / 0 | `$EV/logs/snapn2-integration-clickhouse.log` |
| Coverage collect (E11) | `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal"]` | **0** | 9115 / 0 / 2639 | `$EV/logs/snapn2-coverage-collect.log` |
| Coverage report (E11) | `["dotnet","tool","run","reportgenerator","-reports:...","-targetdir:tests/coverage/report","-reporttypes:Html;TextSummary;Cobertura"]` | **0** | line **86.7%** (47226/54448) / branch **79.3%** (25533/32181) | `$EV/logs/snapn2-coverage-report.log`; `$EV/coverage/Summary.txt` |
| Perf acceptance (E12) | `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories","acceptance","--artifacts","$EV/perf/after2/acceptance"]` | **0** | 7/7 cases; all Mean ≤ 1.05× BEFORE; allocations non-increasing | `$EV/perf/after2/acceptance/wall.txt` (`EXIT=0 WALL_SECONDS=50`); `$EV/logs/snapn2-perf-after2-acceptance.log` |
| Perf provider-extensions (E13) | `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories","provider-extensions","--artifacts","$EV/perf/after2/provider-extensions"]` | **0** | 7/7; allocations back to BEFORE (ArrayJoin 1.31 KB, Final_PreWhere 2.26 KB, Joined_ArrayJoin 3.4 KB); R_after 2.074 ≤ 1.05×R_before 2.070 | `$EV/perf/after2/provider-extensions/wall.txt` (`EXIT=0 WALL=48s`); `$EV/logs/perf-after2-provider-extensions.log` |
| Allocation gate (E14) | `["bash","$EV/commands/allocation-ci.sh"]` | **0** | 761 s; unittest discover 13/13 OK; gate 56/56 within budget (r1/n1 baseline; filters independent of the n=2 seam) | `$EV/logs/allocation-ci.log`; `$EV/logs/allocation-ci.exit` |
| Host validator (E15) | `["python3",...,"validate_inner_loop.py","report","$EV/inner-loop/report-snapn2.json"]` | **0** | 25 executions, one boundary Debug solution build (Release separate non-validator check) | `$EV/inner-loop/final-report-snapn2.log` |

- Integration skips are capability-level (MySQL 80, SQL Server 48, SQLite 40, PostgreSQL 26, 3 harness); no provider hidden as skipped.
- `BenchmarkDotNet.Artifacts` (root + `benchmarks/`) restored to tracked state after the BDN runs; `git status --porcelain` for both is empty.
- Prior `report.json` preserved as `$EV/inner-loop/report-n1.json`; the snapshot report is promoted to both `report-snapn2.json` and `report.json`.

**Ledger and reconciliation.** E01–E19 rv1 excerpt: `$EV/check/ledger-n2.json` (obligation text, evidence, command, exit, N/A predicate). Readable mapping: `$EV/check/reconciliation-rv1-n2.md`. **18 closed; E19 N/A with the pinned Roslyn predicate; no open applicable row.**

**E13/E18.** E13 closed by the after2 provider-extensions re-measure (allocations ≤ BEFORE); E18 row mapping supplied here and in `ledger-n2.json`. The final COMPLETE verdict remains CHECK-owned (E18).

**Remaining.** None for the requested re-gather; CHECK re-review of the snapshot-bound evidence. No commit/push. D191 not marked done.


## CHECK r1 re-gather 2 (snapshot-n2)

**Snapshot unchanged.** HEAD `7e9fad9f443d898fe4cde2594f555c0819d7221d`; `git diff | sha256sum` = `f652c19e8489ddf8da10e1ff4eb5c4e6dff8c4024d1aa7d46704efd48659fdb7`; `git status --porcelain | sha256sum` = `a0e9c3cdad8c9da7e3bbcd2757c7f6d802c8a6386c2592b243f736622947798b` (73 entries). This re-gather performed **no product edits**, no worktree/branch, no commit/push/merge.

**E14 allocation gate re-run (evidence).** `bash $EV/commands/allocation-ci.sh` (literal CI copy; sha256 `ff52e60be32ca7a378ce7e6310d165ef6854c0c0d7c60b526647b7258f929308`, matches `provenance/commands-sha256.txt`) on the current tree:

- exit **0**; wall **706 s**; `python3 -m unittest discover -s eng/perf/tests` → `Ran 13 tests ... OK`; gate → `All 56 row/job verdicts within budget` (56/56 `OK` rows).
- Logs: `$EV/logs/allocation-ci-rg2.log`, `$EV/logs/allocation-ci-rg2.exit` (`EXIT=0 WALL_SECONDS=706`).
- `BenchmarkDotNet.Artifacts/results/*` (12 tracked files) restored via `git checkout --`; snapshot hashes returned to the values above.

**Versioned row mapping.** `$EV/check/ledger-n2.json` now states, for every row E01–E19, `contract_version: rv1`, the row's own `row_version: 1`, its `status`, `satisfying_evidence` path, `command` and `exit_code`; E13/E18 carry explicit closure text. The readable `$EV/check/reconciliation-rv1-n2.md` mirrors it (contract_version / row rv / status / evidence+command / exit).

**E19 N/A predicate.** Quoted verbatim from the plan contract table (`docs/specs/status/rc2-191-provider-extensions-1.md`, E19 / *Observable applicability*):

> Есть affected consumer в `tests/nextorm.alias.poc`, что наблюдается в Roslyn inventory

Observed state is the predicate's negation: no affected consumer in `tests/nextorm.alias.poc` (Roslyn inventory) and the project is not in the solution; `commands/alias-poc-check.sh` → `N/A: tests/nextorm.alias.poc absent`, exit 0.

**E15 report / validator.** `$EV/inner-loop/report.json` re-verified against the unchanged snapshot (25 executions, one boundary Debug solution build; `report-snapn2.json` is the same file); `python3 .../validate_inner_loop.py report $EV/inner-loop/report.json` → exit **0** (`$EV/inner-loop/final-report-rg2.log`).

**E13/E18 closure.** E13 closed by the after2 provider-extensions re-measure (allocations ≤ BEFORE; `R_after` 2.074 ≤ 1.05×`R_before` 2.070). E18 closed by the CHECK-owned mapping of E01–E19 rv1; all applicable rows closed, E19 N/A with the proven plan predicate. The final COMPLETE verdict remains CHECK-owned.

**Mirror.** `$EV` → `artifacts/pdca/rc2-191/r1/` refreshed. D191 **not** marked done; no commit/push.

## ACT — Done / Verified (2026-10-08)

**Durable state.** Current cycle 1; plan revision **r1** (no replan); attempt **n=2/3**; final CHECK revision **r1 PASS (snapshot-n2)**. Defect history closed: `D191-ALLOC` (1 fix), `D191-TEST-COMPILE` (1 fix).

**CHECK PASS (snapshot-n2).** Snapshot HEAD `7e9fad9f443d898fe4cde2594f555c0819d7221d`, branch `1.0.9-rc2`, `git diff` sha256 `f652c19e8489ddf8da10e1ff4eb5c4e6dff8c4024d1aa7d46704efd48659fdb7`. Final numbers as in the Verified block above; evidence mirror `artifacts/pdca/rc2-191/r1/` (from `/tmp/nextorm-D191-r1`), reconciliation `$EV/check/reconciliation-rv1-n2.md`, ledger `$EV/check/ledger-n2.json`, snapshot report `$EV/inner-loop/report-snapn2.json`.

**Commit (ACT).** Authorized single commit of D191 files only (relocation + seams + consumer/test/benchmark/docs/status; no `StrykerOutput/`, no `artifacts/`, no other tasks' `docs/specs/status/rc2-15x/16x/17x/18x…`). Message prefix: `#191 D191 provider-specific extensions relocation (core -> provider packages); CHECK PASS snapshot-n2`.

**Issue #191 (ACT).** Close with the comment quoted in the Done/Verified block; on API failure record `gh close blocked, retry pending` and continue.

**Remaining.** None for D191. Parent reconciles the collection-status D191 commit sha (recorded after commit); #160 set-aside and #184 incomplete remain preserved and untouched by this commit.

