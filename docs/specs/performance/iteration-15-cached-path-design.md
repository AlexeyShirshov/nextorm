# Итерация 15: поэтапное снижение накладных расходов cached-пути

> **Тип документа:** дизайн + поэтапный roadmap уровня дизайна. НЕ является утверждением детального
> плана реализации и не даёт разрешения на код.
> **Статус:** Письменная спецификация одобрена пользователем 2026-10-04; подробный
> implementation plan и способ исполнения не согласованы; разрешения реализации нет.
> **Границы:** Коммиты, push и merge не разрешены; изменения остаются незакоммиченными до отдельного
> запроса пользователя. Согласование документа не разрешает реализацию.
> **Дата:** 2026-10-04.
> **Ревизия измерений Итерации 15 / базовая ревизия ядра:** `1ad3775` (на ней сняты числа Итерации 15).
> **HEAD рабочего дерева на момент записи спецификации:** `59adf5e` (documentation improvements,
> benchmarks). **Ветка:** `1.0.9-b`.
> **Tracking issue:** #183 — https://github.com/AlexeyShirshov/nextorm/issues/183
> **Milestone:** `1.0.9-b` (open) — https://github.com/AlexeyShirshov/nextorm/milestone/18
> **Внутренний документ** (не собирается DocFX, не публикуется; ссылки из `docs/**` запрещены).
>
> **Approval (письменная спецификация):** 2026-10-04, текущий диалог — пользователь выбрал
> «Утвердить спеку (Recommended)» на вопрос «Утверждаешь письменную спеку ...?». Это **written-spec
> review approval**: он разрешает **только** подготовку подробного плана. Он **не** является approval
> детального implementation plan, **не** выбирает execution method и **не** разрешает Stage A /
> product implementation. #166 остаётся deferred.

## 1. Цель и доказательства

Цель — снизить стоимость **каждого вызова** fresh-fluent попаданий в implicit plan cache, в первую
очередь двухтабличного Join, сохранив публичный API, корректность identity плана и привязки
параметров, а также поведение prepared/cold-путей. Обязательный паритет с Dapper (в т.ч. ≤ 1.0×)
**не ставится**; prepared-путь дефектом **не считается**.

Измерения Итерации 15 (2026-10-04), per-10 операций: Join — Nextorm cached 516.5 µs / 134.84 KB,
Dapper 277.8 µs / 21.45 KB, linq2db 106.92 KB. Это **end-to-end** замеры под тяжёлой нагрузкой хоста
(`loadavg` ≈ 8.5–8.8 на 8 логических ядрах, БД на диске, не tmpfs), а **не** allocation-stack
атрибуция. Поэтому историческое временное расхождение нельзя списывать только на код, а стоимость
конкретной стадии нельзя брать из end-to-end µs без профиля.

Текущий код подтверждает поток на каждый fresh-вызов: свежий builder/`QueryCommand` → `PrepareCommand`
→ построение ключа/хэша → полное структурное `Equals` → refresh захваченных параметров через
`ExtractParams` → обход `MakeSelect(paramMode: true)`. Warm-cache экономит рендеринг SQL и mapper, но
**не устраняет** перечисленные стадии.

Опорные точки кода:

- `src/nextorm.core/DataContext/QueryPlanner.cs:553-590` — prepare/lookup и гейты `storeInCache`;
- `src/nextorm.core/DataContext/QueryPlanner.cs:727-739` — refresh рантайм-параметров через `ExtractParams`;
- `src/nextorm.core/Query/QueryCommand.Plan.cs:30-44` — `GetOrCreatePlanKey` (мемо ключа);
- `src/nextorm.core/DataContext/SqlBuilder.cs` — обход `MakeSelect(paramMode: true)`;
- `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs:98,218` — `struct ExpressionComparer` сам
  по себе не аллоцирует в heap, но словарь области параметров (`_parameterScope`) — аллоцирует.

Вывод: стоимость стадий требует **профиля, а не догадки**.

## 2. Коррекции измерений (внести в Stage A, сейчас отчёт НЕ редактируется)

`docs/specs/performance/benchmark-report.md` в категории B смешивает prepared-арм с fresh-cached.
Коррекции подлежат реализации в Stage A; в текущей задаче отчёт не изменяется, сырые данные не
уничтожаются.

- Строка отчёта `benchmark-report.md:1854` (`Where for-loop (100) = 1 080.0 µs`) — это арм
  `Nextorm_CachedForLoop_ToListAsync` (`benchmarks/nextorm.benchmark/SqliteBenchmarkWhere.cs:146-155`):
  `.Prepare()` **один раз** до 100-операционного цикла, аллокация **114.67 KB** (сырой CSV
  `BenchmarkDotNet.Artifacts/results/Nextorm.Benchmark.SqliteBenchmarkWhere-report.csv`). Его выигрыш у
  Dapper — результат prepared-vs-raw, а не fresh-cached.
- Обычный fresh-cached: `Nextorm_Cached_AsyncStream` (`SqliteBenchmarkWhere.cs:121-132`) —
  **2 087.6 µs / 734.65 KB** против `Dapper_AsyncStream` (`:213-221`) **1 693.2 µs / 203.98 KB**;
  `Nextorm_Cached_ToListAsync` (`:134-144`, помечен `[BenchmarkCategory("acceptance")]`) —
  **2 546.7 µs / 731.11 KB** против буферизованного `Dapper_Async` (`:204-211`) **1 353.1 µs / 180.7 KB**.
- LargeIteration: Nextorm cached **2.22 MB** против Dapper **2.84 MB**, т.е. утверждение отчёта
  «`Nextorm_Cached` проигрывает Dapper на всех девяти классах» по аллокациям ложно как минимум для
  LargeIteration.
- Prepared LargeIteration проецирует `LargeEntity`, cached — анонимный тип; временная дельта ≈ 2×
  **не атрибутируется** кэшу без контроля симметрии.
- Свежие артефакты лежат в корневом `BenchmarkDotNet.Artifacts/results/`; `benchmarks/BenchmarkDotNet.Artifacts`
  — устаревшие. Причина fallback: `BenchmarkArtifacts.Resolve` ищет `nextorm.sln`, а репозиторий на
  `nextorm.slnx`. Починка корневого resolution — **опциональна** и только для воспроизводимости
  harness; миграция/чистка артефактов и несвязанная уборка запрещены.

## 3. Прежние решения (что уже закрыто)

- `docs/specs/performance/performance-findings.md:624-638`: обязательный fresh-fluent паритет с Dapper
  **закрыт** как цель; при необходимости — отдельная структурная задача **M12 #3** с отдельным
  бенчмарк-гейтом.
- `docs/specs/performance/warm-path-plan.md` — не утверждён и устарел; фазу 1a (удержание Any shared
  key **по одному хэшу**) **не переносить** — небезопасно. Per-instance мемо (Итерация 13) и fast paths
  (Итерация 14) уже присутствуют.
- #165 закрыт — не переоткрывать; #166 не трогать, пока работа не коснётся `CteHoister.Hoist` (тогда
  остановиться и пересмотреть объём).
- Публичный API не меняется.

## 4. Альтернативы и выбор

- **local-only:** ниже риск, но оставляет `PrepareCommand` на hit-пути.
- **staged (принят):** изменения по одному, с гейтом после каждого; сначала измерения (Stage A), затем
  кандидаты Stage B (B1/B2) в измеренном порядке, с отдельным гейтом на каждое изменение.
- **all-at-once структурный новый кэш:** выше риск по identity и lifetime.

Выбран **staged**. Любая форма **fingerprint/hash-only identity** без структурной проверки —
исключена.

## 5. Stage A — выходы и зависимости

- Корректная отчётность **без уничтожения** сырых данных; коррекции §2 вносятся как отдельная
  версионированная ревизия.
- Гомогенная декомпозиция Join и простого Where на стадии: construction → prepare →
  lookup/equality → captured param refresh → execution.
- Plan-only бенчмарки **без БД** плюс совпадающие SQL/типы/строки/терминалы; prepared/reused — отдельная
  категория.
- Валидные allocation/CPU-профили **реального** benchmark-процесса (трейс Итерации 14 оказался
  непригодным).
- Новые/переиспользуемые correctness-тесты.
- Версионный baseline и comparison manifest: revision; job/runtime; host/config/machine load/db location;
  warmup; точные method IDs; logical operation counts; B/op в **точных байтах**; time uncertainty и
  source contract.
- Regression-бюджеты зафиксированы и заморожены **ДО** любых правок ядра.

**Gate A:** читаемые профили; representative cache hits доказаны; plan counts/params проверены; точные
workloads и нормализация; бюджеты зафиксированы и отревьюированы. Выбор profile-led порядка работ B —
легитимный deliverable фазы A; числовое улучшение сейчас **не** заявляется.

## 6. Stage B — архитектура (кандидаты упорядочены по измеренному вкладу)

Оба кандидата **не гарантированы**; внедряется тот, чей вклад подтверждён Stage A.

**B1 — аллокации временных equality-скоупов.** Уменьшить аллокации области параметров при сохранении
полного структурного равенства, shadowing/nested lambda, reentrancy и потокобезопасности. Запрещено:
слепой threadstatic comparer, утечка pooled state между запросами, удержание shared Any-ключа по
одному хэшу. Каждое изменение — собственные correctness/perf evidence и откат.

**B2 — immutable guarded parameter-refresh recipe.** На промахе формировать неизменяемую recipe,
способную привязать **текущие** fresh-захваты, сохраняя pN-имена/порядок, число параметров,
dedup-семантику, provider conversion, null, разделение stable/runtime. Чистая recipe **не** удерживает
mutable `DbCommand`/enumerator и **не** захватывает свежее замыкание в reusable recipe; удержание уже
существующих старых кэшей — вне объёма, но новый retention не добавляется. Совместимость/число/форма
валидируются **до** вычисления значений; неподдержанные случаи используют оригинальный `ExtractParams`
**до** evaluation; двойного evaluation замыканий быть не должно. Для динамического IN/lookup (контент и
размер), фильтров/prewhere/nested shape refresh остаётся **до** выбора плана. Сначала поддерживаются
только скалярные формы, доказуемо эквивалентные; converter/rawSQL/nested/IN идут в fallback, пока не
доказано отдельно. Mismatch-safe fallback обязателен (не только `Debug.Assert`). General visitor
replacement не обещается; возможное появление expression-walk в binding свежих захватов нужно
квантифицировать (recipe не гарантирует O(1)).

**B1 и B2 — разные изменения** с отдельными гейтами и откатами; порядок определяется Stage A.

## 7. Stage C — условный гейт / следующий проект

После B повторить декомпозицию и зафиксировать остаток. Если `PrepareCommand` — крупнейшая оставшаяся
**nonDB**-стоимость выше неопределённости измерения, оформить отдельный **письменный дизайн M12 #3**,
ревью и новый план **до** реализации.

Возможный кандидат: share immutable normalized preparation recipe по verified shape; без
переиспользования mutable builder, `QueryCommand`, старых closure-значений, `DbCommand` или result
enumerator. Обязательно спроектировать: точную identity (settings/filter/mapping/provider/root/nested
зависимости); refresh-гарды; структурную проверку коллизий; bounded lifecycle/invalidation/
`DataContextCache.Clear`/`PurgeQueryCache`; thread/reentrancy ownership и fallback. Существующий
`QueryPlanStore` (`[ThreadStatic]`, generation clear, без eviction) — **не** разрешение добавлять
второй неограниченный кэш.

Stage C сейчас — **только** решение/handoff, а не лицензия на реализацию. Если prepare не доминирует
или B достигает цели — остановиться и задокументировать остаток, не форсировать fingerprint/hash-only
shortcut.

## 8. Инварианты корректности

- Полное структурное равенство даже при **форсированной хэш-коллизии**.
- Никакого `queryCommand.Cache = false` (sticky shared Any); локальный `storeInCache: false` уже
  существует для DML/CTE/temp table.
- Stable/runtime/captured значения не пересекают границу запроса (нет утечки).
- Именование, конвертеры, null и наблюдаемый порядок exception/evaluation не меняются.
- IN/prewhere форма, mapping/filter-настройки — часть identity плана.
- Prepared-путь сохранён; warm CTE 0B сохранён.
- Никакого нового process-wide seeding метаданных (например, несвязанный TVP).
- Fastpath-vs-fallback не меняет ошибки и соблюдает evaluation once.
- Никакого несвязанного рефакторинга.

## 9. Тестирование

Существующие сьюты: `PlanCacheTests`, `InListCacheTests`, `PlanKeyUniquenessTests` (sqlite.tests);
`JoinIntoPlanKeyTests`, `ExpressionPlanEqualityComparerNodeTests`, `PlanKeyStructureTests`,
`QueryCacheControlsTests`, `Iteration14CteLookupTests` (core.tests); `QueryFilterSqlGenerationTests`
(все провайдеры); `DataModifyingCtePlanCacheTests` (postgres.tests).

Добавить:

- два инстанса замыкания с одинаковой формой и разными значениями;
- captures в WHERE/JOIN/projection: повтор, dedup, имена, конвертеры, null;
- IN и lookup: смена формы и значений; фильтры/настройки;
- чередование Any/Count/All с DML/CTE;
- форсированную коллизию **на самом lookup**, а не просто тест разных хэшей;
- вложенные lambda со shadowing и очистку reentrant scope;
- cache clear/lifetime; recipe не использует **первое** замыкание;
- fastpath-vs-fallback: ошибки и evaluation once.

Provider-специфичный неподдержанный DML/CTE обрабатывается честно (без фиктивного pass).

## 10. Performance-приёмка

- Точный baseline из Stage A задаёт **числовые** байт-бюджеты **до** правок ядра; запрещены округлённые
  KB отчёта и reverse-fit после результатов. Никаких числовых «TBD».
- Первичные сценарии: fresh Join и `Cached_PlanOnly_Param`/`Cached_ToList`; контроли:
  Any/First/Single/Where; симметрия Iteration/LargeIteration и prepared guard; cold Cache — **отдельно**
  (сейчас без `MemoryDiagnoser` и с purge на каждый invocation).
- B принимается только при **воспроизводимо меньшем** B/op в целевых fresh Join/param-cached сценариях
  и атрибутируемом ускорении целевой стадии, **без** статистически надёжной регрессии end-to-end
  времени.
- Нет роста памяти `Prepared_ToList` (тот же job/method/нормализация); warm CTE ≤ 0B.
- Существующие тесты/бюджеты `eng/perf/iteration14_gate.py` обязаны продолжать проходить; текущий
  слэк 25% **не** является приёмкой оптимизации. Неизменные контроли не растут по B/op без отдельно
  утверждённого обоснованного бюджета.
- Время — два paired/interleaved прогона baseline↔candidate на том же хосте/job/конфиге; приводить CI
  и шум; избегать кросс-хостных сравнений под разной нагрузкой. Регрессия **> 20%** на 2 сопоставимых
  прогонах — автоматическое расследование/блок; меньшая, но статистически надёжная регрессия тоже
  блокирует. Dapper-ratio — контекст, не обязательное ≤ 1.0.
- Финальные полные 9 сравнений — исправленными методами, сырые данные сохраняются.
- Существующий гейт покрывает только 3 класса → будущий новый гейт обязан покрывать Join и корректные
  Where-методы, точные logical counts и быть **fail-closed** при отсутствии данных/jobs/methods.

## 11. Верификация и порядок

Тесты (существующие примеры команд):

```bash
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
```

затем SQL-тесты всех провайдеров. `eng/perf/iteration14_gate.py` — использовать его существующий usage,
не выдумывая аргументы.

Полные бенчмарки (будущие команды; сейчас **не** запускать):

```bash
NEXTORM_BENCH_FULL=1 NEXTORM_BENCH_DB=<same frozen DB> \
  dotnet run -c Release --no-build --project benchmarks/nextorm.benchmark -- --filter '*SqliteBenchmarkJoin*'
```

Перед `--no-build` обязательна сборка benchmark в Release. `NEXTORM_BENCH_DB` — тот же замороженный
файл БД, что и в baseline Stage A (зафиксированный в comparison manifest путь, а не незаполненное
значение).

Контейнерные интеграционные тесты обязательны для **всех** провайдеров; при исполнении читать
`.opencode/skills/running-integration-tests/SKILL.md`:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

Если сокета нет — поднять Podman по инструкции skill; skipped-провайдеры **не** считаются pass. .NET 10,
warnings-as-errors, покрытие 85 line / 75 branch (где применимо).

### Гейты и следующий шаг

1. письменный ревью спецификации пользователем — **пройден 2026-10-04** (письменная спецификация одобрена);
2. детальный implementation plan и выбор способа исполнения — **не пройден**;
3. реализация — **не пройдена** (разрешения нет).

Код на текущем шаге **не** пишется. Кандидатные области реализации — core
`QueryPlanner`/`SqlBuilder`/`ExpressionPlanEqualityComparer`/`QueryCommand.Plan` плюс выделенные
benchmark/gate-тесты; точные имена файлов и алгоритмы фиксируются на этапе детального плана/Stage A.
Письменный ревью спецификации **пройден 2026-10-04**; следующий шаг — детальный implementation plan
и явный выбор способа исполнения до любой реализации. Issue при этом **не** закрывается.
