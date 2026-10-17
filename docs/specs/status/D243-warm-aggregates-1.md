# D243 / #243 — warm-path Count/Sum allocations

- task: `T243` / issue `#243`, milestone `1.0.9-rc3`
- cycle `N=1`, plan revision `r=1`, contract revision `rv=1`, `r=1` attempt `n/3 = 1/3`
- `plan_state: ready`, phase: `ACT done — verified`
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
- `2026-10-17T12:23Z | DO | r=1 | n=1/3 | DO started | tree binding 3b0cd737 on branch 1.0.9-rc3` — single tree (worktree не создавался), product/test/benchmark файлы не изменялись; env snapshot: `artifacts/pdca/D243/rv1/validation/env.txt`.
- `2026-10-17T12:23Z | DO | r=1 | n=1/3 | D243-01 test scope validated | validate_inner_loop.py brief exit 0` — scope `artifacts/pdca/D243/rv1/scope.json` (3 projects, 3 selectors, 5 files, rebuild=affected, integration_boundary_runs=1); stdout+exit: `artifacts/pdca/D243/rv1/validation/scope-brief.log`.
- `2026-10-17T12:23Z | DO | r=1 | n=1/3 | D243-01 bindings filled (E243-13 coverage) | receipt artifacts/pdca/D243/rv1/validation/coverage-bindings.txt` — verbatim из `.github/workflows/dotnet.yml` (HEAD 3b0cd737): `dotnet tool restore`; `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`; `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`; thresholds `MIN_LINE_COVERAGE: 85`, `MIN_BRANCH_COVERAGE: 75`; thresholds читаются из `tests/coverage/report/Summary.txt`, hard-fail только на `refs/heads/main`.
- `2026-10-17T12:23Z | DO | r=1 | n=1/3 | D243-01 bindings filled (E243-14 evidence) | receipt artifacts/pdca/D243/rv1/validation/evidence-bindings.txt (help verbatim, exit 0)` — generate: `python3 scripts/pdca_evidence.py generate --repo <repo> --contract <contract.json> --output <evidence output dir rv<k>>`; lint: `python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/D243/rv1/manifest.json` (optional `--scope-root artifacts/pdca/D243/rv1`). Обе bindings заполнены ДО соответствующего исполнения, per plan.
- `2026-10-17T12:24Z | DO | r=1 | n=1/3 | Build gate | dotnet build nextorm.slnx -c Debug exit 0, Warning(s)=0, Error(s)=0` — log `artifacts/pdca/D243/rv1/validation/build.log`, elapsed 00:00:48.78.
- `2026-10-17T12:28Z | DO | r=1 | n=1/3 | D243-02 before rounds | 3×B4 + 3×BP + 1×BA under `flock -x -w 3600 /tmp/opencode/nextorm-bdn-host.lock`, overall exit code 0` — per-round wall: four-1/2/3 = 35/29/30 s, prepared-1/2/3 = 24/25/24 s, acceptance = 48 s; каждый из 7 round-логов завершился `EXIT:0`, каждый раунд дал `*-report.csv`; acceptance — **7** executed cases (BDN `Global total time` 45.86 s), ни один arm/case не отфильтрован; env: `NEXTORM_BENCH_FULL` unset, `NEXTORM_BENCH_DB` = repo `benchmarks/nextorm.benchmark/data/test.db`; product/test/benchmark исходники не менялись. Evidence: `artifacts/pdca/D243/rv1/before/{run-all.log,numbers.tsv,rounds.md,extract_numbers.py}`, raw BDN dirs `artifacts/pdca/D243/rv1/before/{four-1,four-2,four-3,prepared-1,prepared-2,prepared-3,acceptance}/`.
- `2026-10-17T12:36Z | DO | r=1 | n=1/3 | D243-02 exact-bytes + D243-03 red | red exit 2 (selected 1, failed 1)` — **exact integer `BytesPerOperation` is NOT archived**: the `before` rounds emitted only csv/html/github exporters (no `*-report.json`/`*-report-full.json`, `// * Diagnostic Output - MemoryDiagnoser *` empty in every log), so the recoverable source is the CSV **`Allocated`** display string (`2.91 KB` / `2.8 KB` / `3.31 KB` / `3.41 KB`, identical in four-1/2/3), giving integer windows Count nextorm 2975…2984, Count linq2db 2863…2872, Sum nextorm 3385…3394, Sum linq2db 3487…3496 (derived KB×1024 = 2979.84/2867.20/3389.44/3491.84) — no integer was fabricated; evidence `artifacts/pdca/D243/rv1/before/{exact-bytes.json,exact-bytes.md}`. New fixture `tests/nextorm.integration.tests/SqliteWarmAggregateAllocationTests.cs` (sqlite, 10 000 rows, sync `Count()`/`Sum(x => x.Id)`, fresh fluent + fresh inline selector per call, warmup 64 / 256 measured × 3 series, `GC.GetAllocatedBytesForCurrentThread`, harness overhead 0 B published and not subtracted): **Count 837 632 B = 3272.00 B/query and Sum 940 032 B = 3672.00 B/query in each of 3 series (byte-identical across series)**, both over `256×2864 = 733 184` / `256×3490 = 893 440` B/series; assertion text: `Count series-1: 837632 B for 256 calls = 3272.00 B/query, over the 2864 B/query threshold (733184 B/series).` / `Sum series-1: 940032 B for 256 calls = 3672.00 B/query, over the 3490 B/query threshold (893440 B/series).` → **threshold failure reproduced** (Count exceeds by 408 B/query, Sum by 182 B/query). Calibration vs the BDN display window: fixture is **+288…+297 B/query (+9.8 %) on Count** and **+278…+287 B/query (+8.3 %) on Sum** — same direction, larger magnitude, systematic across arms including selector-free `Count`; recorded (unverified) cause: `before` ran benchmarks `-c Release` while this unit's command is `-c Debug`. Construction probe: **`NOT-OBSERVABLE-AT-THIS-TIER`** (no public construction counter; no instrumentation added, no fake zero), adjacent `DataContextCache.ExpressionsCache`/`SelectListCache` populations were 0 before/after every arm. Evidence: `artifacts/pdca/D243/rv1/red/{run.log,assertion.log,allocation-probe.json,calibration.md,test.patch,product-tree-hash.txt}`; product tree hash `3b0cd737`; `test.patch` restricted to `tests/**` (the pre-existing dirty `docs/**` edits belong to other streams).
- `2026-10-17T12:44Z | DO | r=1 | n=1/3 | D243-02 spike | Release fixture == Debug byte-for-byte: Count 3272.00 / Sum 3672.00 B/query (exit 2, selected 1, failed 1; +292.16 / +282.56 vs BDN 2979.84 / 3389.44 = arm-symmetric harness offset, not JIT config; Count violated in both oracles, Sum only in the fixture — BDN Sum 3389.44 < 3490) | stage attribution (sqlite, 10k rows, 64 warmup + 3×256, separate non-overlapping loops, harness 0 B): A builder-only Count 1784 / Sum 1624, B full terminal 3272 / 3672, C prepared control 456 / 456, B−A 1488 / 2048, residual (B−A)−C = 1032 / 1592 B/call (recipe counters: B 1662/1664 reuses, 0 rejections; A and C 0/0) | P1 pass, P2 pass, P3 pass (deficit 115.84 BDN / 408 fixture vs removable 2816 upper bound, 1032 conservative), P4 pass (Sum caveat recorded) | verdict **pass** — seam + ownership internal-only, removable band covers the Count deficit; evidence `artifacts/pdca/D243/rv1/spike/spike.json`, attribution `artifacts/pdca/D243/rv1/red/attribution.json`, Release calibration `artifacts/pdca/D243/rv1/red-release/calibration.md`; spike wall 0.4 min (≤20). Guard: attribution test is skipped without `D243_ATTRIBUTION_REPORT` (default red unchanged, `red/guard-run.log`); relative report paths resolve against the test-host CWD — must be absolute in E243-10/E243-12`
- `2026-10-17T13:05Z | DO | r=1 | n=1/3 | D243-04 axis 1 (cache + key) closed | `src/nextorm.core/DataContext/ScalarArtifactCache.cs` (ScalarTerminal/ScalarArtifactKey/ScalarArtifact/ScalarArtifactCache, capacity 16 FIFO), QueryCache find/get-or-add/clear wiring, PurgeQueryCache clears artifacts first, DisposeStaff clears; no isolated measurement recorded` — evidence pointer `artifacts/pdca/D243/rv1/impl/summary.md`, `artifacts/pdca/D243/rv1/impl/steps.md`.
- `2026-10-17T13:05Z | DO | r=1 | n=1/3 | D243-04 axis 2 (guards) closed | `EntityBuilder.IsPlainScalarScope` + `TryGetScalarArtifactKey` (empty @params, SQL DataContext, QueryCacheEnabled, plain scope, GetFilters empty, mapped single-column member) + `TryGetScalarMember`; every unproved state falls back` — evidence pointer `artifacts/pdca/D243/rv1/impl/summary.md` (guard table).
- `2026-10-17T13:05Z | DO | r=1 | n=1/3 | D243-04 axis 3 (execution binding) closed | `DataContext.TryExecuteScalarArtifact` (store-ownership re-validation + current-argument binding) and `PublishScalarArtifact` (publish only when QueryPlanStore owns the key); CountCore/LongCountCore/AggregateCore wiring` — evidence pointer `artifacts/pdca/D243/rv1/impl/summary.md`.
- `2026-10-17T13:05Z | DO | r=1 | n=1/3 | D243-04 final axis measured | fixture exit 0, `passed: true`, Count **456** B/query (baseline 3272) and Sum **1016** B/query (baseline 3672), 3/3 series byte-identical, harness 0 B; per-axis deltas not isolated (only baseline and combined final were measured)` — evidence `artifacts/pdca/D243/rv1/impl/allocation-probe.json`, `artifacts/pdca/D243/rv1/impl/steps.md`.
- `2026-10-17T13:05Z | DO | r=1 | n=1/3 | D243-05 safeguards closed | scratch probe `tests/nextorm.integration.tests/D243FilterGuardScratchProbe.cs` deleted; 24 new tests (15 sqlite + 4 core in-memory + 5 core artifact-store) using the planner's existing pre-preparation counters as the fast-path/fallback oracle; suites green: core 2617/0/0 exit 0, sqlite 1782 passed/1 pre-existing skip exit 0, allocation fixture exit 0` — evidence `artifacts/pdca/D243/rv1/impl/tests.md`, logs `artifacts/pdca/D243/rv1/impl/*.log`, `artifacts/pdca/D243/rv1/impl/git-status.txt`.
- `2026-10-17T13:07Z | DO | r=1 | n=1/3 | D243-06 dedicated allocation gate | gate eng/perf/d243_aggregate_gate.py; red exit 1 (Count four-1/2/3 = 2979.84 B/op > 2864, Sum four-1/2/3 = 3389.44 B/op <= 3490 pass; time predicates pass); self-tests 8/8 exit 0; malformed input exit 2` — new files only (`eng/perf/d243_aggregate_gate.py`, `eng/perf/tests/test_d243_aggregate_gate.py`), CRLF, `eng/perf/iteration14_gate.py`/`iteration14-budgets.json` untouched. Parses the BDN CSV `Allocated` display value (no JSON exporter, no integer fabricated) as `display x 1024` and reports the +-5.12 B (documented +-5 B) display window per row; checks every `after` round per terminal against Count <= 2864 / Sum <= 3490 with no cross-terminal or cross-round averaging; time rows carry per-round `q = Mean(nextorm)/Mean(linq2db)`, both medians and both bounds, `pass_absolute`/`pass_relative`. Red receipt: `artifacts/pdca/D243/rv1/gate-red.json`, `artifacts/pdca/D243/rv1/gate-red.md`, log `artifacts/pdca/D243/rv1/validation/gate-tests.log`. Green run is deferred to the next unit (no `after` artifacts exist yet; nothing fabricated).
- `2026-10-17T13:11Z | DO | r=1 | n=1/3 | D243-07 after rounds | 3×B4 + 3×BP + 1×BA under `flock -x -w 3600 /tmp/opencode/nextorm-bdn-host.lock` (one acquisition), overall exit code 0, every round `EXIT:0` (7/7)` — per-round wall: four-1/2/3 = 35/33/32 s, prepared-1/2/3 = 24/25/24 s, acceptance = 44 s. **B4 all three rounds (CSV `Allocated` display + ±5.12 B window)**: Count nextorm **408 B/op** (window 402.88…413.12) vs linq2db 2864 B (threshold 2864) in four-1/2/3; Sum nextorm **970 B/op** (window 964.88…975.12) vs linq2db 3490 B (threshold 3490) in four-1/2/3; no averaging across rounds. Means (µs) nextorm/linq2db: Count 14.510/24.180, 14.770/24.490, 14.540/24.700; Sum 269.590/302.820, 274.310/292.230, 271.250/293.050. Same-round `q = Mean(nextorm)/Mean(linq2db)`: Count 0.6001 / 0.6031 / 0.5887 → **median 0.6001** (before 0.7567); Sum 0.8903 / 0.9387 / 0.9256 → **median 0.9256** (before 0.9541). **BP**: Count 408/408/408 B, Sum 410/410/410 B, GroupByCount 4736/4736/4736 B (all identical to the `before` rounds; means µs Count 14.610/14.320/14.310, Sum 267.280/270.590/269.390, GroupByCount 1245.190/1251.210/1239.440). **BA**: **7** executed cases, no skip, wall **44 s** (script) / BDN `Global total time 00:00:41 (41.49 sec)`; wall ratio after/before = 44/48 = 0.9167. **Gate** `python3 eng/perf/d243_aggregate_gate.py --before …/before --after …/after --output …/gate.json --summary …/gate.md` → **exit 0, PASS (6 allocation rows, 2 time rows), no violations**. Observed and recorded without interpretation: this run's CSV prints `Allocated` in `B` for the aggregate arms (linq2db Count displays 2864, Sum 3490), whereas the `before` run printed `KB` (2.8 KB / 3.41 KB = 2867.20 / 3491.84 derived). Tree binding: HEAD `3b0cd73748bc0e7bb6b4b45a208d5aa3eb2aa08f`, branch `1.0.9-rc3`, working tree carries the uncommitted D243 implementation diff (`git_diff_sha256 19681bb3…d626`, untracked-set sha `811fa873…8961`); no product/test/benchmark edit by this unit. Evidence: `artifacts/pdca/D243/rv1/after/{run-all.log,tree-binding.txt,numbers.tsv,rounds.md,extract_numbers.py,four-*,prepared-*,acceptance/}`, gate `artifacts/pdca/D243/rv1/{gate.json,gate.md}`.
2026-10-17T13:20Z | DO | r=1 | n=1/3 | D243-07 validation E243-07 (build + core + sqlite) | `dotnet build nextorm.slnx -c Debug` exit 0, Warning(s)=0, Error(s)=0 (log `artifacts/pdca/D243/rv1/validation/build.log`); `dotnet test tests/nextorm.core.tests -c Debug --no-build` exit 0, total 2617 / succeeded 2617 / failed 0 / skipped 0 (`validation/core.log`); `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` exit 0, total 1783 / succeeded 1782 / failed 0 / skipped 1 pre-existing (`Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`, needs `NEXTORM_LOB_SQLITE_PROBE=1`) (`validation/sqlite.log`)`
2026-10-17T13:20Z | DO | r=1 | n=1/3 | D243-07 E243-08 focused recipe suite | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~CachedPreparationRecipeTests"` exit 0, total 23 / succeeded 23 / failed 0 / skipped 0; pinned cases present and green: `Recipe_ChangedClosureValuesAreFresh_NoStaleCapture` (`:631`), `Recipe_ThreadSeparation_NoCrossThreadReuse` (`:676`), `Recipe_SharedAnyCommandInstanceIsExcluded_AndLaterCacheableQueriesStillHit` (`:697`) — `validation/recipes.log``
2026-10-17T13:20Z | DO | r=1 | n=1/3 | D243-07 E243-09 container integration (mandatory) | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0: **Total 3991, Errors 0, Failed 0, Skipped 199, executed 3792**; per provider (case counts from `validation/integration-run3.trx`, skips from the same run): PostgreSQL 956 total / 26 skip / **930 executed**, SQL Server 870 / 48 / **822**, MySQL 779 / 80 / **699**, ClickHouse 293 / 0 / **293**, SQLite 832 / 42 / **790**, MariaDB 116 / 0 / **116**, shared/other classes 145 / 3 / 142 — all four containers started (`Start Docker container` lines, 4 providers + SQL Server init at 00:00:07.77) and there is **no provider-wide skip** (the 199 are per-test capability skips). Disclosed anomaly: an extra run made only to obtain machine-readable per-provider counts (`validation/integration-run2-fail.{log,trx}`) reported Total 3991 / **Failed 124** / Skipped 199 (MySQL 73, MariaDB 22, PostgreSQL 18, SQL Server 5, ClickHouse 2, other 4; e.g. `MySqlConnector.MySqlException : Table 'nextorm.fn_probe' doesn't exist`) and was **not reproduced** by the next identical-binary run (`validation/integration-run3.{log,trx}`: 3991 / Failed 0 / Skipped 199, exit 0) — recorded as a transient container-state/ordering flake, not as a D243 regression; boundary evidence stays `validation/integration.log` (defect key `suite-state-flake`, 0 D243 fixes applied)`
2026-10-17T13:20Z | DO | r=1 | n=1/3 | D243-07 E243-10 after allocation probe | `D243_ALLOCATION_REPORT=/home/alex/sources/nextorm/artifacts/pdca/D243/rv1/after/allocation-probe.json dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~SqliteWarmAggregateAllocationTests"` exit 0 (total 2: 1 passed, 1 skipped — the attribution fact stays off without its own env var); probe `passed: true`: **Count 456.00 B/query in 3/3 series** (116 736 B per 256 calls; thresholds 2 864 B/query, 733 184 B/series), **Sum 1 016.00 B/query in 3/3 series** (260 096 B; thresholds 3 490 B/query, 893 440 B/series), harness overhead 0 B and not subtracted, sink 50 005 000 — evidence `after/allocation-probe.json`, log `validation/allocation-after.log``
2026-10-17T13:20Z | DO | r=1 | n=1/3 | D243-07 E243-12 stage attribution (after only; before quoted, not re-measured) | `D243_ATTRIBUTION_REPORT=/home/alex/sources/nextorm/artifacts/pdca/D243/rv1/after/attribution.json dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~SqliteWarmAggregateAllocationTests"` exit 0, 2/2 passed. After stages (B/query, Count / Sum): A builder-only **1 784 / 1 624**, B full terminal **456 / 1 016**, C prepared control **456 / 444.19**, `B − A` **−1 328 / −608**, residual `(B − A) − C` **−1 784 / −1 052.19**. Before stages quoted from the **pre-fix tree** `red/attribution.json` (not re-measured on the fixed tree): A 1 784 / 1 624, B **3 272 / 3 672**, C 456 / 456, `B − A` 1 488 / 2 048, residual **1 032 / 1 592**. On the fixed tree the layered model no longer holds (B does not contain A), so the negative `B − A`/residual are arithmetic of a removed layering, not attribution; the interpretable after relation is `B − C` = **0.00 B (Count)** / **571.81 B (Sum, measured but unattributed)**. `stage_overlap = false` (three separate loops, each with own warmup, harness 0 B). Recipe counters B: publishes 2 → 0, reuses **1 662 → 2**. Reports `artifacts/pdca/D243/rv1/attribution/attribution.md`, `after/attribution.json`, log `validation/attribution-after.log`
2026-10-17T13:20Z | DO | r=1 | n=1/3 | D243-07 docs | created `docs/specs/performance/d243-warm-aggregates.md` (provenance 2026-10-10 issue numbers vs 2026-10-17 measurements, per-round before/after four-arm tables with Mean/B/op/same-round q, prepared control, 7-case acceptance with cached/prepared ratios, attribution table with the negative-residual caveat, exact BDN commands + host lock path + ±5.12 B KB-display window and the `after`-CSV raw-bytes note, fix prose, deferred register, limitations/non-claims); shared comparison page, shared baseline tables, collection status and the 10 pre-existing dirty `docs/**` files untouched; every touched file CRLF-normalized`
2026-10-17T13:26Z | DO | r=1 | n=1/3 | D243-07 E243-09 addendum (run-2 failure context) | the 124 failures of `validation/integration-run2-fail.{log,trx}` coincided with artifacts produced by another process in the same validation directory: `integration-truncated.log` (18:14:51), `process-check.txt` (18:15:29Z), `integration-contaminated-concurrent.log` (18:17:21, byte-identical to `integration.log`, 746 423 B) — i.e. concurrent integration activity against the shared reused MySQL/MariaDB/PostgreSQL containers, which can drop `fn_probe`/`fn_seq` under a running suite; identical binaries then ran green (`integration-run3`, 3991 / Failed 0 / 199 skipped) — concurrent-activity explanation is plausible but **not verified** as root cause; no D243 fix applied, boundary evidence unchanged (`validation/integration.log`); summary `validation/validation-summary.md`
- `2026-10-17T13:15Z | DO | r=1 | n=1/3 | E243-09 process check | ps: NONE, docker CLI: NO-DOCKER (not installed in WSL), socket ping OK` — evidence `artifacts/pdca/D243/rv1/validation/process-check.txt`; previous `integration.log` (456 lines) already carried a summary but no `EXIT:` marker, moved aside to `validation/integration-truncated.log` as instructed and not used as evidence.
- `2026-10-17T13:16Z | DO | r=1 | n=1/3 | E243-09 first re-run discarded — concurrent run, NOT evidence | Total 3991, Failed 242, EXIT:1` — 4 s after this run started, a second integration process (PID 116809, `-result-trx … integration-perprovider.trx`) began against the same reused containers; cross-test interference produced duplicate/missing seed tables (`Table 'nextorm.person' doesn't exist`, `Table 'orders' already exists`) and duplicated filter rows. Log preserved as `validation/integration-contaminated-concurrent.log`; discarded, not evidence, no product cause inferred. Wait loop until no integration process remained (`clear at 13:19:20Z`), then a clean re-run.
- `2026-10-17T13:20Z | DO | r=1 | n=1/3 | E243-09 integration boundary (clean re-run) | Total 3991, Errors 0, Failed 0, Skipped 199, Not Run 0, Time 43.962s, EXIT:0` — all five container providers started and reached `ready` (Postgres `ff3bb6dd` via `pg_isready`, MySQL `d703aedb`, MariaDB `914f06a0` via `healthcheck.sh --innodb_initialized`, plus `6bf39546` and `1b5207b30a62`); discovered per provider Postgres 943 / SqlServer 857 / MySql 767 / MariaDb 115 / ClickHouse 280 / Sqlite 818; capability `[SKIP]` lines Postgres 26 / SqlServer 48 / MySql 80 / MariaDb 0 / ClickHouse 0 / Sqlite 42 (incl. `WarmAggregateStages_ShouldAttributeThePerCallBytes` without `D243_ATTRIBUTION_REPORT`); **provider-level availability skips: 0**, so no provider suite was skipped. Evidence `artifacts/pdca/D243/rv1/validation/{integration.log,integration-providers.txt}`, discovery `validation/integration-list-tests.log`.
- `2026-10-17T13:21Z | DO | r=1 | n=1/3 | E243-10 after allocation probe | exit 0, total 2 / succeeded 1 / skipped 1, passed=true` — Count **456 B/query** (116 736 B × 3 series), Sum **1 016 B/query** (260 096 B × 3), thresholds 2 864 / 3 490, harness overhead 0 B not subtracted; before was 3 272 / 3 672 (red). Evidence `artifacts/pdca/D243/rv1/after/allocation-probe.json`, log `artifacts/pdca/D243/rv1/validation/allocation-after.log`.
- `2026-10-17T13:21Z | DO | r=1 | n=1/3 | E243-12 after attribution | exit 0, total 2 / succeeded 2 / skipped 0; stage_overlap=false` — A 1 784 / 1 624, B **456 / 1 016**, C 456 / 455.0625, `B − A` = −1 328 / −608, residual `(B−A) − C` = **−1 784 / −1 063.0625** B/call (negative because the warm terminal no longer performs stage-A construction; published numerically, explained in the report). Before stages cited from `red/attribution.json` and verified present there: A 1 784 / 1 624, B 3 272 / 3 672, C 456 / 456, residual 1 032 / 1 592. Evidence `artifacts/pdca/D243/rv1/attribution/{after-attribution.json,attribution.md}`, log `validation/attribution-after.log`.
- `2026-10-17T13:22Z | DO | r=1 | n=1/3 | D243-07 docs | docs/specs/performance/d243-warm-aggregates.md written (CRLF)` — provenance with both dates (historical 2026-10-10 3 824 / 4 226 B from the committed report; this cycle 2026-10-17 2 979.84 / 3 389.44 B before and 408 / 970 B after), per-round before/after tables (3 × 4 arms with same-round q), prepared control, acceptance 7 cases 48 s → 44 s with ratios, attribution stage table + residual, exact BDN command lines, host lock path, KB→±5.12 B quantization note, fix in prose (whitelist, fail-closed guards, 16-entry FIFO per-context ownership cleared on purge/dispose, no public API change), deferred register (all in `1.0.9-rc3`), limitations and non-claims.
- `2026-10-17T13:29Z | DO | r=1 | n=1/3 | E243-13 coverage + targeted mutations | dotnet-coverage collect (CI command, DOCKER_HOST=podman socket) exit 0: total 12204 / failed 0 / succeeded 11996 / skipped 208, all container providers executed; reportgenerator exit 0: line 88.9% >= 85, branch 80.5% >= 75 -> pass (core 88.8/80.7, postgres 90.7/80.4, sqlite 91.7/70.5, sqlserver 95.1/79.1); six probes a/b/c/d/f KILLED (exit 2; e.g. a: Sum(x => x.Id + offset) expected 36 found 9, b: purge still finds the artifact, c: Count expected 4 found 3, d: 17-shapes oldest not evicted, f: IgnoreFilters expected 3 found 2), e SURVIVED (full T243 sqlite class 15/15 green, exit 0) - defect key `mutation-e-cache-flag-guard`, fixes applied 0, cache-flag guards masked by the QueryPlanStore ownership lookup and no warm-then-disable oracle; restored: build exit 0 (0 warnings/0 errors), mutation-relevant `git diff -- src tests eng` sha dfb0c521...72b136c identical pre/post, untracked set hash identical, whole-tree hash changed only by the foreign docs edit (15-query-hints.md at 13:27:29Z) | evidence artifacts/pdca/D243/rv1/coverage/{collect.log,summary.json,report/Summary.txt}, artifacts/pdca/D243/rv1/mutations/{matrix.md,matrix.json,pre-mutation-hashes.txt,post-revert-hashes.txt}`
- `2026-10-17T13:30Z | DO | r=1 | n=1/3 | E243-14 evidence manifest | generate exit 0 (14/14 receipts, 13 met + E243-13 open with the survivor reason); `validate_inner_loop.py manifest` exit 0; `pdca_evidence.py lint` exit 0; base_ref 01815f3a, HEAD 3b0cd737, frozen tree 939e08dc4044c3184ac46bce0589be7778de9474, footprint 12 paths with 0 forbidden changes | evidence artifacts/pdca/D243/rv1/{manifest.json,handoff.md,validation/evidence-generate.log,validation/evidence-lint.log,validation/inner-loop-manifest-gate.log}`
- `2026-10-17T13:37Z | DO | r=1 | n=1/3 | mutation-e killed after adding DisablingCacheAfterWarm_ShouldBypassArtifact_AndRestoreFastPath` — new focused test in `tests/nextorm.sqlite.tests/T243WarmScalarArtifactTests.cs` (warm `Count` twice, register an `IQueryInterceptor`, assert the warm call raises **0** `CommandInitialized`, disable `QueryCacheEnabled`, assert the call raises exactly **1** — the ordinary prepared path built a command — then re-enable and assert the fast path serves the same value again with 0 preparations and `PlannerDiagnostics()==0`; the `QueryPlanner` recipe counters stay 0 on the disabled path because the terminal passes `storeInCache == false`, so `IQueryInterceptor.CommandInitialized` is the legitimate public oracle; no reflection, no new counter). Probe **e** re-applied unchanged → build exit 0 (0/0), test exit **2** with `Expected prepared.Count to be 1 because a disabled context prepares the command instead of executing the warm artifact, but found 0 (difference of -1).` → `killed: true`; mutation reverted, both mutated files byte-identical to their pre-apply copies (`cmp`) and `git diff -- src eng tests | sha256sum` = `dfb0c521753f626b2443847bb0eb23b8f8f3e7e7533c158bdb56ba97672b136c` (the recorded pre-mutation product hash; the new test is untracked and therefore outside that diff). Suites: sqlite full 1784 total / 1783 passed / 1 pre-existing skip exit 0, core `CachedPreparationRecipeTests` 23/23 exit 0, allocation fixture exit 0 with Count **456** / Sum **1016** B/query in 3/3 series (unchanged); `dotnet build nextorm.slnx -c Debug` exit 0 (0 warnings / 0 errors). Evidence regenerated: frozen tree **9c10c946…** (`E243-13` flipped `open` → `met`, 14/14 met), single generation `b50237de…`, `generate` exit 0, `validate_inner_loop.py manifest` exit 0, `pdca_evidence.py lint` exit 0 | evidence `artifacts/pdca/D243/rv1/mutations/{matrix.md,matrix.json,e.diff,e-kill-build.log,e-kill-test.log}`, `artifacts/pdca/D243/rv1/{manifest.json,handoff.md}`, logs `artifacts/pdca/D243/rv1/validation/{evidence-generate.log,inner-loop-manifest-gate.log,evidence-lint.log}`, `artifacts/pdca/D243/rv1/after/allocation-probe.json`


## Done / Verified

**ACT 2026-10-17 — цикл закрыт: CHECK verdict `pass`, R243-01..R243-12 met, 0 defects.**

| Что | Итог |
|---|---|
| `Count` (B4, 3 раунда, CSV B/op) | **408 B/op** (before 2 979.84) — цель ≤ 2 864 |
| `Sum` (B4, 3 раунда, CSV B/op) | **970 B/op** (before 3 389.44) — цель ≤ 3 490 |
| Prepared controls `A_Nextorm_Prepared_*` | не изменились: Count 408 / Sum 410 / GroupByCount 4 736 B, B/op идентичны `before`-раундам, median Mean ≤ 1.20×baseline (R243-06 met) |
| Acceptance suite (BA) | **7** cases, wall **44 s** (≤ 240 s), ни одного skip; cached/prepared ratios в границах R243-07 |
| Allocation fixture (E243-10) | `passed: true`: Count **456.00 B/query**, Sum **1 016.00 B/query** в 3/3 сериях (before 3 272 / 3 672), harness overhead 0 B не вычитался |
| Coverage (E243-13) | line **88.9 %** (≥ 85 %), branch **80.5 %** (≥ 75 %) в существующем coverage scope |
| Integration (E243-09) | **3991 total / 0 failed / 199 capability skips**; все пять контейнерных провайдеров исполнены (PostgreSQL, SQL Server, MySQL, ClickHouse, SQLite; +MariaDB), provider-level skips 0 |
| Gate `eng/perf/d243_aggregate_gate.py` | **green** exit 0 на `after`; **red** exit 1 на `before` (Count 2 979.84 > 2 864, Sum 3 389.44 ≤ 3 490); self-tests 8/8; malformed/missing input exit 2 |
| Targeted mutations | **6/6 killed** (a, b, c, d, e, f) — каждое дало assertion failure, затем откатано; mutated-файлы byte-identical, tree restored |
| Manifest + lint (E243-14) | `pdca_evidence.py generate` exit 0, `validate_inner_loop.py manifest` exit 0, `pdca_evidence.py lint` exit 0, 14/14 required rows met |
| Tree binding | frozen evidence tree **9c10c946…**, base_ref `01815f3a`, HEAD `3b0cd737`, forbidden footprint changes 0 |

Non-claims: `after`-CSV печатает `Allocated` в байтах, `before` — в KB (±5.12 B display-окно опубликовано, не усреднялось); attribution `B − A` / residual на исправленном дереве отрицательны (снятый слой построения), интерпретируемая after-связь — `B − C` = 0.00 B (Count) / 571.81 B (Sum, измерено, не атрибутировано); run-2 integration failure (124) не воспроизвёлся и записан как transient container-state flake, не как дефект D243.

## Handoff to collection owner

- **Report fragment** для общей comparison-страницы: `docs/specs/performance/d243-warm-aggregates.md`.
- **Deferred register** — все пункты остаются в milestone `1.0.9-rc3`: Avg/Min/Max/statistical и async fast path; captured/composite selectors; JoinInto/filter/temp/nested fast paths; #240 Category-B performance; shared `iteration14` integration; общий comparison/followups update (owner — parent).
- **Gate ownership:** `eng/perf/d243_aggregate_gate.py` + `eng/perf/tests/test_d243_aggregate_gate.py` — task-owned gate; shared `eng/perf/iteration14_gate.py` / `iteration14-budgets.json` **не менялись** (их владельцы — T244/T245/T247).
- Публичный API не менялся → правок публичных `docs/**` / `docs/ru/**` не требуется.
