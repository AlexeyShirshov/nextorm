# D243 / #243 — warm-path Count/Sum allocations

- task: `T243` / issue `#243`, milestone `1.0.9-rc3`
- cycle `N=1`, plan revision `r=1`, contract revision `rv=1`, `r=1` attempt `n/3 = 1/3`
- `plan_state: ready`, phase: `PLAN done — PLAN→DO boundary`
- mode: autonomous + auto-commit (push never)
- baseline ref: `957fcfe7` (already an ancestor of HEAD `01815f3a`), measured tree binding recorded at DO
- selected_variant: `V-D-owned-immutable-scalar-artifacts`
- status file: `docs/specs/status/D243-warm-aggregates-1.md`
- evidence root: `artifacts/pdca/D243/rv1/` (manifest written at DO→CHECK)
- CHECK re-gather budget: **2** (owner CHECK; orchestrator only routes; no threshold edits, no "best run" selection)

## Goal

Снять повторное построение внутренних scalar-артефактов на fresh-fluent → warm plan-cache → aggregate execution пути так, чтобы `Count`/`Sum` в `SqliteBenchmarkAggregates` уложились в байтовые цели issue, без деградации времени, без смены benchmark-сценария и без shared mutable command.

Цели из #243: `Count` ≤ **2 864 B/query**, `Sum` ≤ **3 490 B/query**; без регрессии времени сверх полосы шума ShortRun **~20 %**; по-кейсовая таблица B/op + атрибуция («no averaging that hides one of the two cases»); тесты nullable/non-nullable и повторных вызовов.

Non-goals (явно из issue): не пять Category-B сценариев #240; не «исправить через `Prepare()`»; не заменять benchmark-сценарий caller-held prepared command; никакого benchmark-specific SQL/type special-casing; никакого shared/global cache без явного ownership + eviction.

## Acceptance criteria

| ID | Критерий и oracle | Негативный случай |
|---|---|---|
| R243-01 | В каждом из трёх итоговых четырёхсторонних запусков: Count **≤2,864 B/op**, Sum **≤3,490 B/op** по точному BDN CSV/JSON | Превышение хотя бы одного порога; усреднение Count и Sum запрещено |
| R243-02 | Для каждого терминала `qᵢ = Mean(nextorm)/Mean(linq2db)` в одном запуске; `median(q_after) ≤ 1.20` и `median(q_after) ≤ 1.20 × median(q_before)` | Замедление скрыто изменением скорости хоста или другого терминала |
| R243-03 | Отдельная таблица для Count и Sum: все повторы, Mean, Error, B/op, отношения к same-round linq2db, before/after delta и измеренная атрибуция | Только округлённые KB, общий средний результат или приписанные без измерения байты |
| R243-04 | Корректны non-nullable/nullable результаты `Count`/`LongCount`/`Sum`; для изменённых путей — пустой набор, nullable/all-null, повторные вызовы, изменённые данные | Неверный тип, null-семантика или устаревший результат |
| R243-05 | После прогрева повтор того же поддержанного scalar shape не создаёт повторно кэшируемые внутренние артефакты: delta счётчика их построений **0**; end-to-end byte regression test проходит | Новый descriptor/wrapper tree на каждом вызове либо скрытая мемоизация результата |
| R243-06 | Все `A_Nextorm_Prepared_*`: median B/op не выше baseline; median Mean ≤1.20×baseline для каждого arm | Ускорение cached-пути ценой prepared-пути |
| R243-07 | Acceptance suite — ровно **7** успешных cases, каждый запуск **≤240 s**; cached/prepared time ratio ≤`1.20×same-host before` и ≤**2.244** (от 1.87); allocation ratio ≤`1.20×same-host before` и ≤**8.904** (от 7.42) | Пропуск case, рост отношения или несопоставимый job |
| R243-08 | Core, SQLite dialect, `CachedPreparationRecipeTests`, aggregate integration suites проходят; контейнерные провайдеры действительно выполняются | Skipped providers представлены как green |
| R243-09 | Closure/parameter freshness, filters, nested sources, cache disable/clear и thread-local recipe сохраняются; нет изменения shared `QueryCommand` ради одного вызова | Sticky `_dontCache`, stale closure, cross-context/thread reuse |
| R243-10 | Новый allocation gate имеет отдельные Count/Sum rows с порогами R243-01; baseline даёт ожидаемый threshold failure, исправление — pass | Gate зелёный на исходной регрессии или падает из-за отсутствующего файла |
| R243-11 | Warnings-as-errors build; line ≥**85 %**, branch ≥**75 %** в существующем coverage scope; новые ветки покрыты либо явно guarded | Неизмеренная новая ветка или предупреждение |
| R243-12 | Evidence привязан к измеренному дереву; корректны footprint, документация, происхождение дат и единицы | Унаследованные отчёты выданы за новые измерения |

Интерпретация шума: linq2db — same-round контроль скорости хоста, а не разрешение ухудшить собственный baseline. Для времени — отношения Mean и их медианы, **не** `Mean±Error` (ShortRun Error не есть пригодный acceptance-интервал). Пограничный результат не объявлять ни доказанной регрессией, ни улучшением; выполнить предусмотренные повторы; после их исчерпания — GAP/разбор, не выбор удобного запуска.

## Selected variant и отклонённые альтернативы

Цель — убрать повторное построение внутренних scalar-артефактов. Ограничения — неизменный публичный сценарий и отсутствие shared mutable command. Минимум в этих ограничениях — небольшой guarded fast path для `Count`/`LongCount` и простого `Sum` с существующим fallback.

| Вариант | Участок исполнения | Цена / риск | Решение |
|---|---|---|---|
| A: per-context mutable scalar command как `Any` | обход создания command через `ReplaceCommand` | конфликт с запретом call-specific mutation shared command; identity/thread/filter риски | отклонён |
| B: кэш aggregate wrapper по `(TEntity,TResult,method)` | часть `AggregateCore` (`EntityBuilderExtensions.cs:1448-1453`) | Count почти не выигрывает; ключ недостаточен для selector body; глобальный generic cache без eviction недопустим; closure retention | отклонён отдельно |
| C: только более дешёвый `SelectCore` | `EntityBuilder.cs:343-377` | не устраняет wrapper Sum; нельзя заранее доказать нужные 960/736 B | недостаточен отдельно |
| **D: immutable owned artifacts + invocation-local execution** | scalar definition/normalization artifacts + wrapper для распознанной безопасной формы | нужен внутренний planner/execution seam; обязателен bounded spike | **выбран** |

Границы D:
1. Кэш принадлежит конкретному `DataContext`, лимит **16 entries**, FIFO eviction; очищается при disposal и связанных cache purge/clear. Никаких static/global словарей, никакого удержания контекста глобальным registry.
2. Entry содержит только immutable scalar shape/normalization artifacts. Не хранит builder, mutable `QueryCommand`, closure object, текущие аргументы или результат выполнения.
3. Ключ включает terminal identity, entity/result types и нормализованный selector/поддержанный source shape; одних типов и метода недостаточно.
4. Исполнение получает текущий источник, аргументы и options через invocation-local binding. Никакого command pooling/shared scalar slot.
5. Начальный whitelist: `Count`/`LongCount` без selector; `Sum` с прямым parameter-rooted mapped member, включая nullable. Свежая inline lambda и эквивалентная cached lambda должны попадать в одну безопасную форму.
6. Неподдержанные shapes/options/providers идут существующим путём; не повторять эвристики raw recipe в независимом permissive matcher — неполная доказуемость → fallback.
7. Публичных API изменений нет. Если internal execution seam нельзя провести без публичного API или изменения settled recipe semantics — DO возвращает конкретное предусловие в PLAN.

Это не кэш полностью построенных деревьев по одним типам и не замена benchmark-сценария prepared-командой.

## Закрытые пробелы постановки

| Пробел | Закрытие |
|---|---|
| Какой компонент даёт достаточную экономию | bounded allocation spike D243-02; байты не обещаются до измерения |
| Доступен ли внутренний scalar executor без нового command | Roslyn-разведка в D243-02; критерий — реализуемый invocation-local seam |
| Как связать context-owned entries с Clear | проверить существующие ownership/lifecycle hooks; отсутствие корректного hook — предусловие, не допущение |
| Можно ли байт-в-байт повторить исторический запуск | committed CSV воспроизводят опубликованные числа; современный before измеряется заново и не подменяется историей |
| Почему документ датирован 17 октября | источник чисел — прогон **2026-10-10**; в новом отчёте указать обе даты и точные B |
| Кто сериализует пять задач | collection owner закрепляет общий host mutex/policy до DO после ALL-barrier; локальный `flock` сам по себе не останавливает некооперативного runner |

GAP в исторических B/op нет: committed reports существуют. Доказательства эффекта реализации пока планируемые, не полученные.

## Work items

| Задача | Решение, severity, lens, место |
|---|---|
| D243-01 prerequisites | fix now 🔴 `[measurement/ownership]` collection status `:30-31`: закрепить host runner policy и владельцев shared footprint; записать PLAN/contract; сохранить baseline source binding; заполнить точные CLI bindings E243-13/14 до соответствующего исполнения |
| D243-02 bounded spike | fix now 🔴 `[allocation/path]` `EntityBuilder.cs:343-377`, `EntityBuilderExtensions.cs:1434-1460`: before runs, allocation attribution, Roslyn ownership/executor check, byte-test calibration; подтвердить seam |
| D243-03 red regression tests | fix now 🔴 `[oracle]`: воспроизводимый byte-threshold red на исходном product tree `01815f3a`, архив test patch + product-tree hash + assertion log |
| D243-04 implementation | fix now 🔴 `[lifetime/cache]` `EntityBuilderExtensions.cs:941,983,1157,1441-1453`, `EntityBuilder.cs:1335-1339`, `DataContext.cs:319/330`, `QueryPlanner.cs:585-799` |
| D243-05 safeguards | fix now 🔴 `[correctness]` `QueryCache.cs:19-21`, `QueryCommand.cs:536-540`, recipe tests `:631,:676,:697`: clear/disposal, fallback, closure/parameter freshness, отсутствие sticky/shared-state mutation |
| D243-06 dedicated gate | fix now 🔴 `[regression]` новый `eng/perf/d243_aggregate_gate.py`: отдельные Count/Sum thresholds, red/green, missing/malformed-input tests; shared iteration14 files не менять |
| D243-07 validation/docs | fix now 🟡 `[evidence]`: after/prepared/acceptance, обязательные тесты, coverage, attribution report, manifest/lint |
| deferred | см. Deferred register; всё остаётся в milestone `1.0.9-rc3` |

Новая `P:`-задача при возврате — только по фактическому результату spike/CHECK (например, невозможность invocation-local seam → `P: заменить D243-04 архитектурой, сохраняющей R243-01..12 без shared command`); это пересмотр плана, а не переименование.

## Footprint

Один поток, одно дерево, выделенное collection. Worktree не создавать. Product/tests/gate пересекаются по контракту.

| Область | Разрешённый footprint | Неопределённость / collision |
|---|---|---|
| Product | `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `Builders/EntityBuilder.cs`, `DataContext/DataContext.cs`, `DataContext/QueryPlanner.cs`, `DataContext/Cache/QueryCache.cs`; при необходимости один internal scalar-artifact файл в `DataContext/` | конкретный seam устанавливает spike; `Roles/IQueryCache.cs` — только если изменение непубличное, иначе PLAN; общий recipe algorithm без отдельного обоснования не менять |
| Tests | `tests/nextorm.core.tests/` (новый focused файл + существующие recipe tests), `tests/nextorm.sqlite.tests/` (focused regression file), `tests/nextorm.integration.tests/CommonTestSuite.Aggregates.cs` + новый SQLite allocation fixture | новые test symbols пока не определены; контракт не выдаёт их за существующие |
| Benchmarks | исходные `SqliteBenchmarkAggregates.cs`, config и acceptance scenarios **не менять** | `benchmarks/nextorm.benchmark/**` shared с T244–T247; diagnostic-only instrumentation требует явного footprint approval и не входит в shipping diff |
| Gate | **новый** `eng/perf/d243_aggregate_gate.py` + focused tests/fixtures | `iteration14_gate.py` и `iteration14-budgets.json` **не трогать** (shared с T244–T247) |
| Docs | task status; новый `docs/specs/performance/d243-warm-aggregates.md` | `docs/specs/performance/**` shared как каталог, новый task-specific файл изолирован |
| Evidence | `artifacts/pdca/D243/rv1/**`, task-labelled `TestResults/**` | BDN output только в task-specific directories |
| Parent-owned | collection status, общий followups table, `docs/comparisons/feature-benchmarks-2026-10-17.md`, shared acceptance baseline | **не редактировать**; передать collection owner готовый фрагмент |

Любое расширение product footprint фиксируется и проверяется у collection owner.

## Prerequisites и зависимости

- Числа и реализация T244–T247 не нужны.
- Нужны parent-owned scheduling/footprint решения после ALL-barrier и общий host mutex.
- Baseline WIP `957fcfe7` принимается как исходный код, **не** как доказанно правильная оптимизация; зависимость ограничена обязательными recipe tests.
- До baseline: committed DB `benchmarks/nextorm.benchmark/data/test.db` существует; `GlobalSetup` подтверждает 10 000 rows и ожидаемые aggregates.
- До container tests: загрузить `.opencode/skills/running-integration-tests/SKILL.md`, проверить/start Podman по её troubleshooting.
- Если инфраструктура не готова — активное предусловие; исходная implementation-задача остаётся **active/blocked**, не done и не superseded.

## Measurement protocol

### Изоляция

Collection-wide policy: **один BDN runner на хосте**, включая attribution, acceptance и другие задачи. Все runner’ы используют `/tmp/opencode/nextorm-bdn-host.lock`. Каждая BDN-команда выполняется под:

```text
flock -x -w 600 /tmp/opencode/nextorm-bdn-host.lock env -u NEXTORM_BENCH_FULL NEXTORM_BENCH_DB="$PWD/benchmarks/nextorm.benchmark/data/test.db" <command>
```

Отдельные `--artifacts` для каждого запуска. Рядом не запускать другие performance workloads; во время BDN не выполнять build/tests других collection units. `NEXTORM_BENCH_FULL=1` запрещён. Использовать существующий `NextormConfig` (ShortRun, InProcessEmit, 3 warmup / 3 measurement / 1 launch); не добавлять `--job short`. Tracked `benchmarks/BenchmarkDotNet.Artifacts/results/*` сохранить неизменными; shared iteration14 runner в этой задаче не запускать (он удаляет target JSON).

### Команды

`P=artifacts/pdca/D243/rv1`; `<phase>` ∈ {`before`,`after`}; `<i>` ∈ {1,2,3}.

B4 — четыре same-round arms:
```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkAggregates.B_Nextorm_Cached_Count" "*SqliteBenchmarkAggregates.B_Linq2Db_Count" "*SqliteBenchmarkAggregates.B_Nextorm_Cached_Sum" "*SqliteBenchmarkAggregates.B_Linq2Db_Sum" --artifacts artifacts/pdca/D243/rv1/<phase>/four-<i>
```

BP — prepared controls:
```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkAggregates.A_Nextorm_Prepared_*" --artifacts artifacts/pdca/D243/rv1/<phase>/prepared-<i>
```

BA — acceptance:
```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --artifacts artifacts/pdca/D243/rv1/<phase>/acceptance
```

Каждый invocation — с lock/env prefix. Сохранять console log, wall time, runtime/job metadata и все BDN reports.

### Повторы и бюджет

- before: **3×B4 + 3×BP + 1×BA**; after: **3×B4 + 3×BP + 1×BA**.
- время — median парных отношений; allocations — отдельные значения каждого запуска; R243-01 проверяется во всех трёх after rounds.
- acceptance рост >20 % сначала расследовать (job, host contention, cases); разрешён **один** дополнительный сопоставимый before/after acceptance pair без удаления исходных данных; устойчивый рост не принимается.
- бюджеты: BDN **30 мин** активного wall time (без ожидания mutex); acceptance invocation ≤4 мин; spike ≤20 мин; validation ≤40 мин. Исчерпание — отчёт/PLAN, не сокращение обязательных проверок.

### Oracle

Решающий before-oracle — **современный same-round 4-arm BDN прогон** на committed product sources; исторический committed report (`results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md`, run 2026-10-10 → 18.47 µs/3,824 B vs 24.37 µs/2,864 B; Sum 286.23 µs/4,226 B vs 305.35 µs/3,490 B) используется как provenance/control, не как замена before. Абсолютные B-цели берутся из issue (точные B, не округлённые KB).

## Variant matrix

| Ось / вариант | Закрытие |
|---|---|
| Count, LongCount | **test**: parameterless, sync, корректный int/long, повторные вызовы |
| Sum | **test**: inline/cached member lambda, nullable/non-nullable, fresh fluent, correctness + byte oracle |
| Avg/Min/Max | **guard + test**: существующий путь, smoke/aggregate-suite при общем refactor |
| Stdev/Stdevp/Var/Varp | **guard + test**: не включать в whitelist; существующие provider suites и routing checks |
| Sync / async | **test**: sync fast path; async — прежний путь, correctness/repetition/cancellation |
| Parameterless selector | **test**: Count/LongCount; selector overload не выдумывать |
| Inline literal / static cached lambda | **test**: одна поддержанная Sum shape, одинаковый результат, reuse immutable artifacts |
| Captured closure / составной selector | **guard + test**: fallback; изменение closure между вызовами не устаревает |
| Non-nullable / nullable | **test**: int/long/decimal и nullable counterparts, пустой/all-null набор; perf target только benchmark `long` |
| Reference result / unsupported type | **guard + test**: старое поведение/ошибка не меняются; никаких SQL/type special case |
| 1st / 2nd+ | **test**: cold construction; warmed construction delta = 0; результат вычисляется заново |
| Cold / warm plan-cache | **test**: miss→hit, эквивалентный новый builder, clear→miss |
| Prepared | **guard + test + BP**: старый путь и performance controls |
| `_dontCache`, explicit args/options | **guard + test**: fallback без sticky mutation; следующие cacheable calls по-прежнему hit |
| JoinInto absent / present | **test**: обычный fast path; **guard + test**: clone branch `EntityBuilder.cs:1294`, существующая семантика |
| Filters, disabled/ignored filters | **guard + test**: fallback; не обходить filter options |
| Context-dependent filters | **guard + test**: оба порядка контекстов, user filter не вызывается probe’ом |
| Temp-table source | **guard + test**: старый local `storeInCache:false` путь; без scalar artifact reuse |
| Subquery / foreign nested source | **guard + test**: fail-closed fallback, recipe regression suite |
| InMemoryDataContext | **guard + test**: старый executor, Count/Sum correctness; SQLite byte budgets не распространять |
| Thread-local recipes / overlapping calls | **test**: существующий `:676`; immutable entries не разделяются между контекстами, invocation state не разделяется |
| Unsupported provider-specific execution | **guard + integration test**: fallback; никакого предположения о поддержке всеми провайдерами |
| Capacity/eviction/disposal/clear | **test**: 17 shapes, eviction/rebuild, release context/closure references, Clear invalidates |
| #240 Category-B shapes / GroupByCount perf | **deferred**: только по новому измеренному aggregate-related regression (correctness recipe-suite обязателен сейчас) |

## Test strategy

DO `test scope`:
```text
projects: tests/nextorm.core.tests, tests/nextorm.sqlite.tests, tests/nextorm.integration.tests
selectors: core full suite; sqlite full suite; CachedPreparationRecipeTests filter отдельно; integration full suite (all enabled providers)
files: existing aggregate/recipe/cache tests + planned focused scalar-artifact и SQLite allocation-fixture files
rebuild: dotnet build nextorm.slnx -c Debug; clean rebuild affected test outputs after product changes
boundary: полный scope перед CHECK; focused runs после каждого логического edit
rationale: builder → planner → cache → scalar execution и provider fallback
integration_projects: tests/nextorm.integration.tests
integration_boundary_runs: 1 полный container-backed run на финальном boundary; focused runs в DO его не заменяют
```

Byte regression и red↔green: планируемая SQLite fixture воспроизводит benchmark essentials (10 000 rows, тот же provider, sync Count/Sum, fresh fluent + fresh inline selector; setup вне измерения). Прогрев **64** вызова, измерение **256** вызовов, три последовательные серии на одном потоке. Technique: `GC.GetAllocatedBytesForCurrentThread()` до/после цикла; overhead пустого harness измеряется отдельно и публикуется; query/selector allocations не вычитаются. Порог: Count ≤ `256×2864`, Sum ≤ `256×3490` bytes на серию; tolerance **0** дополнительных B/query; assertions/форматирование вне измеряемого цикла. Отдельный probe считает построение перечисленных кэшируемых artifacts: после прогрева construction delta = 0; входная lambda и необходимые invocation-local allocations не объявляются «нулевыми». Red выполняется **после добавления только тестов, до исправления**, с архивом test patch, product-tree hash и assertion log. Если fixture не воспроизводит threshold failure на committed product sources — это **GAP именно unit-byte oracle**: не подгонять fixture/порог молча, вернуть PLAN; BDN gate остаётся независимо воспроизводимым red oracle.

Атрибуция: test-only stage probe измеряет непересекающиеся construction / preparation-lookup / execution интервалы, отдельно Count и Sum; diagnostic instrumentation patch сохраняется и не входит в shipping diff; сумма стадий и неатрибутированный остаток публикуются; BDN total остаётся главным oracle.

Coverage/mutation: воспроизвести `dotnet-coverage → reportgenerator` команды из `.github/workflows/dotnet.yml`, точные вызовы — в receipt; 85/75 не понижать. Для изменённых веток — targeted manual mutations (убрать shape guard; игнорировать Clear; вернуть прошлый binding; заменить current args предыдущими; отключить eviction; включить `_dontCache` reuse), каждая должна дать assertion failure, затем удалена; процент mutation coverage без реального отчёта не заявлять.

## Docs plan

Обновить task status и новый task-specific `docs/specs/performance/d243-warm-aggregates.md`: exact B/op, before/after, все rounds, attribution, ограничения, provenance (run 2026-10-10 vs comparison page 2026-10-17), deferred register, handoff collection owner. **Не трогать**: общий comparison page, shared baseline tables, collection status/followups, чужие reports — передать parent готовый фрагмент. Публичный API не меняется → публичные `docs/**`/`docs/ru/**` правок не требуют; если это условие нарушается — PLAN, а не незадокументированное изменение.

## Решения PLAN

- Perf-measurement: **нужен** — это per-query путь `SelectCore:343-377`, `AggregateCore:1448-1453`, `QueryPlanner:609-799`; baseline — same-host committed-product before; исторический отчёт — provenance/control.
- Reconnaissance: **нужен** bounded spike D243-02 (≤20 мин), выход `spike.json` (stage bytes, ownership/lifecycle map, guard list, число устраняемых bytes, pass/fail по 4 пунктам). Если путь не покрывает оба дефицита — вернуть PLAN до большой реализации; не переключаться автоматически на shared command.
- Unit execution mode: **single-tree, single stream** (collection уже владеет изоляцией); worktree не создавать.
- Design checklist: абстракция ради одного потребителя — нет; новый концепт — один bounded immutable scalar-artifact owner; новой публичной поверхности — нет; нового global/shared mutable cache — нет; ownership/eviction/invalidation заданы и проверяются; unsupported cases fail-closed; CRLF, nullable, warnings-as-errors, CPM обязательны; settled findings без нового измерения не переоткрывать.
- Приоритеты: P1 по построению — R243-01,02,04–10,12; P2 — R243-03 attribution completeness и R243-11 coverage presentation; CHECK не понижает P1.
- Риски: усложнение scalar executor; неполный shape key; closure retention; stale lifecycle; вмешательство соседнего BDN; fixture/BDN discrepancy; увеличенный fallback allocation — у каждого тест/guard или явный replan trigger.
- Уверенность: высокая в исходных измерениях и ограничениях; средняя в выбранном seam и достаточности экономии до spike.

## Deferred register (всё в milestone `1.0.9-rc3`)

| Что | Почему не сейчас | Триггер | Где |
|---|---|---|---|
| Avg/Min/Max/statistical и async fast path | не требуются для двух measured targets | новый per-case allocation regression либо доказанное бесплатное расширение без новых концептов | task status + performance report |
| Captured/composite selectors fast path | нужны дополнительные binding guarantees | измеренный allocation hotspot в таком selector + approved evidence contract | там же |
| JoinInto/filter/temp/nested fast paths | высокая стоимость key/invalidation; корректный fallback достаточен | новый измеренный провал соответствующего scenario | там же |
| #240 Category-B performance | явный non-goal #243 | отдельное измерение показывает регрессию от данного diff | collection handoff, milestone сохранён |
| Shared iteration14 integration | избегаем collision; dedicated gate достаточен | collection owner интегрирует gate family после ALL-barrier | parent-owned followup |
| Общий comparison/followups update | shared docs owner — parent | parent получил все task results | handoff |

## Evidence contract (rv=1, supersedes: none)

Общие поля каждой строки: **tree binding** (`git_head`, `base_ref=01815f3a`, product/test footprint digest, dirty diff hash, runtime/job/environment, command, exit code, start/end/wall time); **sources** — все перечисленные будущие логи/отчёты планируемые, исторические committed reports маркируются отдельно; **applicability = true** для всех строк (отсутствующий provider/report не делает строку N/A); **result** — receipt содержит числа и итоговый boolean predicate; **storage** — `artifacts/pdca/D243/rv1/manifest.json`, receipts в `generations/<digest>/results/<E-ID>.json`, raw sources по путям таблицы; **owner** — DO producer, CHECK независимо проверяет receipt/tree binding. Все строки — `required_rows`.

| ID / требования | Проверка, точный вызов или probe | Evidence → артефакты | Владелец / predicate |
|---|---|---|---|
| E243-01 R01–03,12 | B4 `before` i=1..3 с lock/env | BDN CSV/JSON + logs → `before/four-{1,2,3}/**`, `before/four-summary.json` | Perf; exit=0, arm_count=4 для каждого, valid_setup=true |
| E243-02 R01–03,12 | B4 `after` i=1..3 | аналогично → `after/four-{1,2,3}/**`, `after/four-summary.json` | Perf; exit=0; все allocation inequalities R01=true; оба time predicates R02=true |
| E243-03 R06 | BP `before` i=1..3 | prepared reports → `before/prepared-{1,2,3}/**` | Perf; exit=0; matched_expected_prepared_arms=true |
| E243-04 R06 | BP `after` i=1..3 | reports + per-arm comparison → `after/prepared-{1,2,3}/**`, `prepared-comparison.json` | Perf; exit=0; каждый alloc_after≤alloc_before и time_ratio≤1.20 |
| E243-05 R07 | BA `before` | BDN + wall time → `before/acceptance/**` | Perf; exit=0; count=7; wall_s≤240 |
| E243-06 R07 | BA `after` (+ условная доп. пара по §Повторы) | BDN + ratio table → `after/acceptance/**`, `acceptance-comparison.json`; повторы хранятся отдельно | Perf; exit=0; count=7; wall_s≤240; все R07 inequalities=true |
| E243-07 R04,09,11 | `dotnet build nextorm.slnx -c Debug`; `dotnet test tests/nextorm.core.tests -c Debug`; `dotnet test tests/nextorm.sqlite.tests -c Debug` | build/test logs, results → `validation/{build,core,sqlite}.log`, task-labelled `TestResults/**` | Tests; все exit=0; failed=0; warnings=0 |
| E243-08 R08–09 | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~CachedPreparationRecipeTests"` | focused log/results → `validation/recipes.log`, `TestResults/**` | Tests; exit=0; executed>0; failed=0; перечисленные pinned cases присутствуют |
| E243-09 R04,08–09 | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | provider execution/results/log → `validation/integration.log`, `TestResults/**` | Integration; exit=0; aggregate failures=0; PostgreSQL/SQLServer/MySQL/ClickHouse executed=true, skipped-provider=false |
| E243-10 R04–05,10 | planned fixture: `D243_ALLOCATION_REPORT=artifacts/pdca/D243/rv1/<phase>/allocation-probe.json dotnet test tests/nextorm.integration.tests -c Debug`; SQLite fixture обязана выполнить probe | byte series, construction counters, test patch, product binding → `<phase>/allocation-probe.json`, `red/**`, `green/**` | Tests; baseline exit≠0 **по threshold assertion**, after exit=0; after thresholds=true, warmed_artifact_build_delta=0 |
| E243-11 R01–02,06–07,10 | planned `python3 eng/perf/d243_aggregate_gate.py --before artifacts/pdca/D243/rv1/before --after artifacts/pdca/D243/rv1/after --output artifacts/pdca/D243/rv1/gate.json`; red mode `--after …/before --output …/gate-red.json` | gate output, parser/self-test log → `gate.json`, `gate-red.json`, `validation/gate-tests.log` | Gate; red exit=1 с allocation violations; green exit=0; malformed/missing inputs rejected=true |
| E243-12 R03,05,12 | planned stage probe: `D243_ATTRIBUTION_REPORT=artifacts/pdca/D243/rv1/<phase>/attribution.json dotnet test tests/nextorm.integration.tests -c Debug`, diagnostic-only patch привязан к source | GC stage data + instrumentation patch + residual table → `<phase>/attribution.json`, `diagnostic/**`, `attribution.md` | Tests/Perf; exit=0; Count и Sum present=true; stage_overlap=false; unattributed_bytes указан численно |
| E243-13 R09,11 | coverage invocations из `.github/workflows/dotnet.yml` (точные команды фиксируются до запуска) + mutation probes тем же project test commands | coverage reports + executable command receipt + mutation matrix → `coverage/**`, `mutations/**` | Tests; line≥85; branch≥75; обязательные mutations killed=true; restored_tree=true |
| E243-14 R12 | `python3 scripts/pdca_evidence.py generate --help`, затем schema-correct generate invocation, закреплённый в status до исполнения; `python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/D243/rv1/manifest.json` | manifest/receipts, footprint/docs/CRLF checks, lint log → `manifest.json`, `validation/evidence-lint.log`, `handoff.md` | Evidence; lint exit=0; required_receipts_complete=true; tree_bindings_match=true; forbidden_footprint_changes=0 |

Уточнение E243-13/14: точные coverage/generate CLI аргументы в брифе отсутствуют. Их discovery — обязательное in-cycle предусловие D243-01 (без чтения product-кода planner’ом); coder закрепляет найденные дословные команды в статусе **до** соответствующего исполнения. Это заполнение command binding, не ослабление обязательств. Если локальный контракт запрещает позднее заполнение, оркестратор сначала заказывает точечный scout и возвращает PLAN; до этого DO gate не считается пройденным.

## Классификация возвратов DO → PLAN

- Нужен lifecycle hook/internal seam/инфраструктурная подготовка → предусловие отдельной активной задачей; исходный D остаётся blocked с прежними критериями.
- Безопасный fallback для неподдержанной формы → приемлемое допущение-риск, только если численные targets выполнены.
- Нет источника/непонятен allocation → недостаток evidence → точечный scout, не автоматический DO retry.
- Внешнее ограничение полномочий/ресурсов → рекомендация оркестратору вызвать `escalate`.
- Фактическая смена архитектуры → новый `P:`, `r` увеличивается, `n=1`; contract revision — с явным supersession и сохранением обязательств.
- Отчёт «реализация завершена» сам по себе D не завершает.

## Gate 1 (PLAN→DO)

Goal — есть; acceptance criteria — R243-01..12; task list — D243-01..07 без TBD; risks — есть; test strategy — есть (projects/selectors/byte oracle/red↔green/coverage/mutation); variant matrix — есть, каждая строка закрыта test/guard/deferred+trigger; docs plan — есть; perf-measurement decision — есть с аргументом; reconnaissance decision — есть (bounded spike); unit execution mode — single-tree single stream; design checklist — пройден; evidence contract rv=1 — 14 строк, все required_rows; N=1, r=1, rv=1; CHECK re-gather budget 2.

К исполнению DO переходить после записи этого файла (CRLF). Апрув пользователя не требуется (autonomous).

## Progress log

- `PLAN written` — planner issued r=1, variant `V-D-owned-immutable-scalar-artifacts`; status persisted by coder; no code, tests, benchmarks or artifacts touched; manifest deferred to DO→CHECK.
