# D246 — ToDataReader: стоимость открытия и чтения raw reader

**Task:** T246 · **Issue:** #246 (https://github.com/AlexeyShirshov/nextorm/issues/246) · **Milestone:** `1.0.9-rc3`
**Plan state:** `plan_state=ready` · **Selected variant:** `V2`
**Durable state:** Current cycle `N=1`, Plan revision `r=1`, Attempt `n=1`, Contract revision `rv=1`
**Base:** branch `1.0.9-rc3` @ `01815f3a` (baseline recipe commit `957fcfe7` is an ancestor; code identical)
**Mode:** autonomous collection task; PLAN ends at the PLAN→DO boundary, DO is started by the collection parent after its applicability check.

## Goal (essence)

Сделать стоимость `ToDataReader` на вызов и на строку сопоставимой с linq2db, сохранив: значения/проекции и отсутствие материализации сущностей; отмену, ошибки и публичное поведение reader; владение per-call command/reader через `CommandReaderOwner`; поведение `ToStream`/`ToTextReader` и CSV; независимость общего `QueryCommand` от отдельного вызова терминала.

### Фрейминг причины

| Arm | Подтверждённый путь | Рабочая гипотеза и целевая задача |
|---|---|---|
| SQLite, empty | `ToDataReader` → **`OpenResultReader`**, не `OpenLobReader` (`SqliteDialect.cs:18` `LobLocatorColumn => "rowid"`; `QueryCommandExtensions.cs:334-336`) | Fixed cost: uncached preparation (`DataContext.cs:576-583`, `storeInCache:false`), получение соединения, `DbCommand`, owner, wrapper. Атрибуция — D246-01; сокращение повторной подготовки — D246-02 |
| SQLite, 10k | тот же путь + `LobDataReader` (`QueryCommandExtensions.cs:339`) | Fixed cost плюс проверки состояния/отмены и дополнительная делегация при чтении/getters (`LobDataReader.cs:84-100,163-312`). Оптимизация wrapper — D246-03 |
| PostgreSQL / SQL Server | `OpenLobReader` (null locator), sequential path | Не причина опубликованного SQLite результата, но затрагиваемый контракт и обязательная регрессия |
| MySQL / ClickHouse | typed unsupported error (`EnsureDataReaderSupported`, `QueryCommandExtensions.cs:332`) | Ошибка должна сохраниться; эти провайдеры не входят в сравнительный perf ratio |

Исходный issue ошибочно указывает на `OpenLobReader` как SQLite seam. Исправлять только этот метод недостаточно. Подготовленный arm A (4.754 ms / 944.8 KB) также не доказывает применимость cached recipe: терминальный путь передаёт `storeInCache:false`, а гейт рецепта (`QueryPlanner.cs:612,614`) требует `storeInCache` и `!streamingRowsRequested`.

**До DO производительность не измерялась.** Все утверждения о причинах сверх приведённых путей — гипотезы, не результат измерения.

## Acceptance criteria

| ID | Наблюдаемый критерий | Негативный случай |
|---|---|---|
| **R246-01** | Для 10k выполнен ratio gate времени и allocation, описанный ниже | Превышение, NA, пропущенный arm или недостаточная точность не являются PASS |
| **R246-02** | Empty arm проходит тот же gate с допуском **0 %** | Не применять самостоятельно «owner-approved tolerance»: его нет |
| **R246-03** | Значения, число строк/полей, metadata, ordinals, single/multi-column/blob проекции и параметры сохранены; материализации нет | Потеря столбца, locator в пользовательском результате, неверное значение/ordinal, появление materializer |
| **R246-04** | Close/Dispose/DisposeAsync идемпотентны; per-call reader и command освобождаются, connection не становится собственностью owner; `IsClosed`/`RecordsAffected` сохранены | Утечка, двойное освобождение, преждевременное закрытие connection, неверные terminal properties |
| **R246-05** | Sync/async и оба вида токена сохраняют отмену до открытия, после открытия и при чтении | Non-cancellable fast path проглатывает отмену либо async теряет per-call token |
| **R246-06** | Поддерживаемые провайдеры и все guards сохраняют поведение | Unsupported provider/dialect, in-memory или запрещённый temp-table source неожиданно исполняется |
| **R246-07** | `ToStream`/`ToTextReader`, LOB locator/sequential behavior и CSV не меняются | Новое raw-reader поведение попадает в LOB/CSV или меняет empty LOB semantics |
| **R246-08** | Повторная подготовка изолирована по terminal mode и всем необходимым вариантам shape/behavior; mutable command/reader/owner не кэшируются; общий `QueryCommand.Cache` не меняется | Коллизия raw/LOB/CSV/materialized preparation, stale shape/values либо sticky cache mutation |
| **R246-09** | Добавлены воспроизводимый reader gate и строки 10k/empty; parser fail-closed | Missing/duplicate/NA, неверный job, dataset или несовпавший run принимается |
| **R246-10** | Публичные benchmark-таблицы и follow-up отражают B и empty, точность измерений и фактический outcome | Скрыты Error/StdDev, невалидный запуск назван зелёным, generator `--check` падает |
| **R246-11** | Coverage ≥85 % line и ≥75 % branch; новые runtime-развилки покрыты, целевые мутации убиты | Только compilation failure вместо killed mutation; новый непокрытый P1 branch |
| **R246-12** | Manifest и frozen-tree evidence валидны, обязательные строки закрыты доказательствами | Отсутствующий отчёт трактуется как выполненная проверка |
| **R246-13** | До изменения production-кода получены воспроизводимый baseline и факты о preparation reuse | Оптимизация выбрана по старому ShortRun или reuse введён без доказательства изоляции |

## Oracle decision

Основной oracle — **within-run Nextorm/linq2db ratio**, отдельно для 10k и empty. Абсолютные `3.942 ms / 943.00 KB` и `109.2 µs / 5.52 KB` сохраняются как историческая справка, не как переносимый host-independent порог. Это не разрешение на tolerance: для обоих arms требуется `≤1.00×`.

### Decisive protocol

- **R=3** независимых запуска до изменений и **R=3** после изменений.
- Каждый запуск содержит A, B, linq2db и Dapper, включая empty, из одного committed класса (`benchmarks/nextorm.benchmark/SqliteBenchmarkStreaming.cs:182-235`).
- Только **`NEXTORM_BENCH_FULL=1` / `Job.Default`** (`NextormConfig.cs:23-25`).
- ShortRun допустим только для диагностики; он не закрывает acceptance.
- Извлекать unrounded Mean, Error, StdDev и allocated bytes/op из машинных артефактов. Формат, потерявший необходимую точность, непригоден для gate.
- Каждый запуск должен иметь ровно один результат каждого обязательного arm/job.
- GlobalSetup должен подтвердить dataset 10k, checksum/bytes parity и empty probe (`SqliteBenchmarkStreaming.cs:85-143`).

Для каждого запуска и каждого arm:

```text
time_ratio       = MeanNextorm / MeanLinq2Db
time_upper_ratio = (MeanNextorm + ErrorNextorm) / (MeanLinq2Db - ErrorLinq2Db)
allocation_ratio = BytesNextorm / BytesLinq2Db
```

**PASS** только если во всех трёх final runs: (1) знаменатель `time_upper_ratio` положителен; (2) `time_upper_ratio ≤ 1.00`; (3) `allocation_ratio ≤ 1.00`; (4) `Error/Mean ≤ 10 %` у обоих сравниваемых arms; (5) разброс трёх within-run time ratios, `max/min − 1`, не превышает 19 %. Дополнительно публикуются обычные ratios и медианы; усреднение не скрывает провал отдельного запуска. Применять Error именно с confidence level, записанным BDN, не выдавая ratio bound за отдельный статистический тест.

Изменения менее примерно 20 % не объявлять доказанной причинной победой над baseline этого host. Соответствие строгому within-run gate и causal speedup — разные утверждения.

### Obtainability / GAP

Протокол технически планируется, но его точность и достижимость **ещё не продемонстрированы**. Первый DO recon (D246-01) обязан проверить: воспроизводимость `Job.Default` и достаточность экспортируемых данных; допустимость шума; возможность безопасного reuse; атрибуцию основного empty overhead. Если три baseline runs недостаточно точны, parser не получает необходимые данные либо безопасный reuse не подтверждается — это **GAP для PLAN**, а не основание расширить tolerance, бесконечно перезапускать benchmark или объявлять задачу выполненной. Дополнительные decisive runs требуют явного решения PLAN.

`ready` означает готовность начать предусмотренный recon и последовательный DO, **не доказанную достижимость performance acceptance**.

## Minimal solution / alternatives

1. **Что меняем:** raw-reader preparation eligibility/isolation и существующий wrapper.
2. **Что непреложно:** lifetime, cancellation, provider/LOB/CSV contracts, cache invariants, нулевой допуск acceptance.
3. **Что минимизируем:** повторную подготовку и лишнюю работу wrapper; не число owner/command ценой нарушения владения.

| Вариант | Подход | Плюсы | Цена / риск | Решение |
|---|---|---|---|---|
| **V1** | Только облегчить существующий wrapper для raw reader | Малый footprint, адресует per-row overhead | Не объясняет и, вероятно, не устраняет большой empty fixed cost | Не выбран как полное решение |
| **V2** | Изолированно переиспользовать immutable preparation через существующую инфраструктуру planner + облегчить существующий wrapper | Адресует оба arms; сохраняет per-call ownership | Нужно доказать key/invalidation/behavior isolation; больше тестов | **Выбран** |
| **V3** | Не менять production-код, добавить только gate | Низкий implementation risk | Не выполняет цель и имеющиеся acceptance ratios | Отклонён |

### Ограничения V2

- Не переключать `storeInCache:true` вслепую.
- Не менять `queryCommand.Cache`, включая временное изменение с последующим возвратом (sticky `_dontCache`, AGENTS.md).
- Переиспользовать только подготовленные immutable данные; `DbCommand`, reader и `CommandReaderOwner` остаются per-call.
- Расширять существующую инфраструктуру подготовки, **не вводить новый cache/framework abstraction** без второго потребителя.
- Raw-reader mode обязан отличаться от materialization, CSV и LOB mode по необходимым ключам/guards.
- При недоказанной корректности reuse оставить текущий `storeInCache:false`, заблокировать D246-02 и вернуть факты в PLAN. Это не разрешение завершить задачу вариантом V1.
- Не возвращать naked provider reader: это теряет command ownership и может изменить наблюдаемое поведение.
- Не вводить pooling command/owner: lifetime/reset-риск не оправдан имеющимися данными.
- Не ослаблять observable disposed/cancellation/ordinal checks ради benchmark.

## Reconnaissance

**Нужен spike — D246-01.** Неизвестность существенна для выбранного V2.

Что доказать: (1) получить новый Default baseline, а не интерпретировать опубликованный ShortRun с Error 40–120 % Mean; (2) измеренно отделить preparation, provider command/connection execution, owner/wrapper и row scan/getters; (3) для empty оценить вклад named allocation types/stacks в историческую разницу примерно `128.5 µs / 7.2 KB`, не приписывая всю разницу одному объекту; (4) через Roslyn установить фактические cache-key dimensions, invalidation и publication, необходимые для terminal preparation reuse.

Harness (временный, вне `src/`): `artifacts/pdca/T246/rv1/probe/T246.Probe.csproj`, `artifacts/pdca/T246/rv1/probe/**`. Исходники, command line и hash сохраняются как evidence. Он не вводит production diagnostics API и не подменяет decisive BDN.

```bash
dotnet run --project artifacts/pdca/T246/rv1/probe/T246.Probe.csproj \
  -c Release -- \
  --output artifacts/pdca/T246/rv1/recon
```

Обязательный результат: измеренные stages/allocations, overhead самого probe, число повторов, остаток unattributed и raw data. Цель — атрибутировать ≥90 % измеренных empty allocations; время может содержать явно выделенный unattributed remainder. Не суммировать sampling percentages как wall-clock delta. Инструментирование допустимо только в disposable source snapshot; production tree после probe должен совпадать с входным. Наличие подходящего allocation profiler/tool устанавливается в D246-01; отсутствие — сначала in-cycle prerequisite/scout, не выдуманный результат. После spike harness удаляется из рабочего использования; сохранённая evidence-копия остаётся. Постоянным становится только BDN scenario/gate, если результаты это обосновывают.

## Variant matrix

**T** — тест; **G** — сохраняемый guard с негативным тестом; **D** — deferred с триггером.

| Вариант | Закрытие |
|---|---|
| SQLite → `OpenResultReader` | **T:** sync/async, 10k и empty, повторные вызовы |
| PostgreSQL → `OpenLobReader` | **T:** container-backed raw reader и LOB regressions |
| SQL Server → `OpenLobReader` | **T:** container-backed raw reader и LOB regressions |
| MySQL / ClickHouse | **G:** typed unsupported error в реальных provider suites |
| MariaDB | **G:** unsupported behavior, если представлен существующей suite; иначе **D:** до появления/изменения его raw-reader support в текущем milestone |
| `CancellationToken.None` / default | **T:** optimized branch, sync/async, complete/early disposal |
| Cancellable token, не отменён | **T:** fallback/guard branch, sync/async |
| Отмена до открытия / после открытия / перед Read | **T:** existing и дополненные cancellation cases |
| `ReadAsync` с отдельным токеном | **T:** per-call cancellation и освобождение linked-token ресурсов |
| 10k / zero rows | **T:** tests + отдельные perf gate rows |
| Multi-column / scalar / blob | **T:** values, ordinals, metadata, streams/text getters |
| Null/DBNull, default value, value/reference parameters | **T:** parameter rebinding и повторная подготовка без stale values |
| Stable / changed shape; повторный terminal / другой terminal | **T:** reuse hit/miss, invalidation, mode isolation |
| In-memory context | **G:** прежняя typed ошибка |
| Temp-table source с terminal name | **G:** прежний запрет; существующая naming behavior сохранена |
| `SupportsSequentialAccess=false` | **G:** прежний unsupported error, включая locator dialect |
| Dispose до первого Read / после части / после полного чтения | **T:** sync/async owner lifetime |
| Close → DisposeAsync / Dispose → DisposeAsync | **T:** idempotence |
| `IsClosed`, `RecordsAffected`, post-dispose members | **T:** observable behavior без ослабления |
| Execute/open failure | **T:** command/reader не утекли |
| `ToStream` / `ToTextReader`, empty LOB | **T:** прежняя suite, `Stream.Null`/text behavior |
| CSV | **T:** существующие CSV regressions; raw reuse туда не попадает |
| Materialized terminals / shared Any/Count command | **T:** cache не становится sticky и подготовка не загрязняется |
| Concurrent use сверх текущих гарантий DataContext | **D:** только при изменении заявленной concurrency support; новую гарантию не вводим |

Ни один supported provider, sync/async arm или cancellation branch не deferred.

## Test strategy

### Unit / provider

Внутренний цикл — ограниченная scope; boundary — полные затронутые проекты. Планируемый brief: `artifacts/pdca/T246/rv1/inner-loop-brief.json` (точная schema подтверждается `python3 scripts/validate_inner_loop.py brief` до первой правки).

```json
{
  "unit": "T246",
  "scope": {
    "projects": ["tests/nextorm.core.tests", "tests/nextorm.sqlite.tests"],
    "selectors": [
      "FullyQualifiedName~LobDataReaderTests|FullyQualifiedName~LobStreamingTests|FullyQualifiedName~LobWrappersTests|FullyQualifiedName~RecordsAffectedTests",
      "FullyQualifiedName~ToDataReaderSqliteTests|FullyQualifiedName~ResultReaderTests|FullyQualifiedName~D199ScalarInPlanCacheTests|FullyQualifiedName~TypedCteTests"
    ],
    "files": [
      "tests/nextorm.core.tests/LobDataReaderTests.cs",
      "tests/nextorm.core.tests/LobStreamingTests.cs",
      "tests/nextorm.core.tests/LobWrappersTests.cs",
      "tests/nextorm.core.tests/RecordsAffectedTests.cs",
      "tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs",
      "tests/nextorm.sqlite.tests/ResultReaderTests.cs",
      "tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs",
      "tests/nextorm.sqlite.tests/TypedCteTests.cs"
    ],
    "rationale": "raw preparation, reader delegation, ownership, LOB isolation and plan-cache regression",
    "rebuild": "affected",
    "boundary": "DO→CHECK boundary: clean Release solution build + full nextorm.core.tests, full nextorm.sqlite.tests, full nextorm.postgres.tests, full nextorm.sqlserver.tests, then one full container-backed integration run",
    "integration_projects": ["tests/nextorm.integration.tests"],
    "integration_boundary_runs": 1
  }
}
```

Новые тесты добавлять в существующие классы; будущие test symbols не объявляются существующими.

### Команды inner loop

```bash
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~LobDataReaderTests|FullyQualifiedName~LobStreamingTests|FullyQualifiedName~LobWrappersTests|FullyQualifiedName~RecordsAffectedTests"

dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReaderSqliteTests|FullyQualifiedName~ResultReaderTests|FullyQualifiedName~D199ScalarInPlanCacheTests|FullyQualifiedName~TypedCteTests"
```

Каждый выполненный selector должен выбрать ≥1 тест. Для новых runtime-развилок фиксировать branch delta: обе стороны ветки, соответствующий тест/guard и raw result.

### Boundary

```bash
dotnet build nextorm.slnx -c Release
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
dotnet test tests/nextorm.postgres.tests -c Debug
dotnet test tests/nextorm.sqlserver.tests -c Debug
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

Перед integration обязательно загрузить repo skill `running-integration-tests`; при отсутствии socket выполнить описанный там startup/retry. Skipped PostgreSQL/SQL Server/MySQL/ClickHouse — **не PASS** этой строки.

### Coverage

Пороги **85 % line / 75 % branch** (`coverage.settings.xml` + CI). Планируемый вызов (точная форма сверяется с `.github/workflows/dotnet.yml` в D246-01 до первого запуска):

```bash
dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" --settings coverage.settings.xml -f xml -o artifacts/pdca/T246/rv1/coverage/coverage.xml
reportgenerator -reports:artifacts/pdca/T246/rv1/coverage/coverage.xml -targetdir:artifacts/pdca/T246/rv1/coverage/report '-reporttypes:Cobertura;TextSummary'
```

Aggregate scope остаётся определённой `coverage.settings.xml`. Отдельно проверить новые branches в изменённых runtime-методах; blanket suppression недопустим.

### Mutation targets

Последовательно, с обязательным восстановлением дерева: (1) убрать raw-vs-LOB/CSV preparation discriminator; (2) отключить cancellation на оптимизированной ветке; (3) исключить освобождение command либо reader; (4) допустить reuse после значимого изменения shape/parameter binding. Планируемый runner: `python3 artifacts/pdca/T246/rv1/mutation/run.py --repo /home/alex/sources/nextorm --cases artifacts/pdca/T246/rv1/mutation/cases.json --output artifacts/pdca/T246/rv1/mutation/results.json`. Валидная мутация компилируется, выбирает тесты и даёт ожидаемый test failure; compilation failure не считается killed. Затем исходное дерево проходит тот же selector.

## Priority matrix

Консервативно **все E246-01…E246-15 — P1**: performance acceptance, ownership, cancellation, provider errors, cache isolation, LOB/CSV preservation и обязательный evidence не понижаются CHECK. Отсутствие отчёта не переводит P1 в deferred.

## Docs plan

Затрагиваем: `docs/specs/performance/feature-benchmark-followups-2026-10-17.md:17,27`; `docs/comparisons/feature-benchmarks-2026-10-17.md:63,66-67,147`; `docs/comparisons/benchmarks.md:232-238` и соответствующую RU generated table; `eng/perf/update_benchmark_tables.py` (включение B и обеих empty строк, сохранение остальных разделов).

```bash
python3 eng/perf/update_benchmark_tables.py
python3 eng/perf/update_benchmark_tables.py --check
```

Публиковать Mean, allocation, Error/StdDev, job, run count, ratios и ограничения. Не переписывать исторический baseline как будто это новая величина.

Не трогаем: public API, guides, curated API reference, LOB documentation и XML-doc без семантической причины. Обязательные comparison pages EN/RU — исключение из «public docs не трогаем». Новых ссылок из public docs на `docs/specs/**` не добавлять. Точная карта generated outputs (incl. README/RU) не подтверждена — read-only scout обязан её установить до изменения generator; не угадывать пути.

## Perf measurement decision

**Нужен.** Это per-call/per-row путь: `LobDataReader.cs:84-100,163-312`; one-time exemption неприменим.

### Decisive run (для `PHASE=baseline` и `PHASE=final`, `I=1,2,3`)

```bash
A=/home/alex/sources/nextorm/artifacts/pdca/T246/rv1
OUT="$A/perf/$PHASE/run-$I"; DB="/tmp/opencode/T246/$PHASE/run-$I/test.db"
mkdir -p "$OUT" "$(dirname "$DB")"; cp /tmp/nextorm-bench/test.db "$DB"
NEXTORM_BENCH_FULL=1 NEXTORM_BENCH_DB="$DB" \
dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter "*SqliteBenchmarkStreaming*" \
  --anyCategories A_RawReader B_RawReader B_RawReader_Empty \
  --exporters json csv --artifacts "$OUT"
```

### Изоляция

- Один BDN process **на всём host** (не только в этом дереве) — эксклюзивное measurement window; без него запуск недействителен. Никаких параллельных BDN-прогонов sibling-задач collection.
- У каждого run отдельные DB и artifacts; исходную `/tmp/nextorm-bench/test.db` не модифицировать.
- Исходная DB должна существовать и содержать корректную benchmark dataset; при отсутствии — in-cycle scout/bootstrap prerequisite, не произвольная замена данных.
- Перед measurement — setup/parity и DB warm-up существующего класса.
- Exporter должен сохранить raw statistics (Mean/Error/StdDev/allocated). Неподдержанный CLI сначала приводит к правке зафиксированного invocation, не к подмене evidence.
- Снять pre/post diff `benchmarks/BenchmarkDotNet.Artifacts`; `--artifacts` не освобождает от проверки pinned config (`NextormConfig.cs:15`). При попадании outputs в tracked dir — перенести evidence в task dir, затем восстановить точный pre-run snapshot; не выполнять общий `git restore`.
- После последней runtime/build-affecting правки decisive final results недействительны и должны быть получены заново.

### Gate

Планируются `eng/perf/t246_evidence.py` и `eng/perf/t246-budgets.json`; две budget rows: raw-reader **10k** и **empty**, каждая с time/allocation ratio `1.00`.

```bash
python3 eng/perf/t246_evidence.py gate --budgets eng/perf/t246-budgets.json --runs artifacts/pdca/T246/rv1/perf/final --output artifacts/pdca/T246/rv1/perf/gate.json
python3 eng/perf/t246_evidence.py self-test --output artifacts/pdca/T246/rv1/perf/gate-negative.json
```

Это **новый планируемый CLI**, не существующий инструмент. Self-test должен отвергать missing/duplicate/NA, неверный job, потерю empty arm, округлённые недостаточные данные и превышения ratio. Не добавлять reader в текущий `iteration14_gate.py` с `logical_ops_per_invocation=100`: reader invocation имеет другую семантику; existing gate/budgets не меняются (их меняют sibling-задачи #243/#245).

### `acceptance` category

Cached-path `--anyCategories=acceptance` **не является oracle исходного reader path** (он `storeInCache:false`). Поскольку V2 меняет eligibility/reuse планировщика, cached acceptance suite становится **обязательной регрессией**, но не заменяет reader gate:

```bash
NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --anyCategories acceptance --artifacts artifacts/pdca/T246/rv1/perf/cached-acceptance
```

Отдельная DB и эксклюзивное host window; результаты оцениваются существующим проектным acceptance contract без нового допуска.

## Unit execution mode

**Последовательно в одном дереве.** Причины: preparation, wrapper, tests и gate образуют общий контракт; `DataContext.cs` пересекается с #247, benchmark/docs directories — с остальной collection. Worktree не требуется; disposable probe snapshot не является интеграционной веткой.

## Requirements on sibling/predecessor outputs

- Семантический результат #243/#244/#245/#247 не требуется как предусловие.
- Обязателен сериализованный BDN window на host (одна BDN-сессия за раз, включая cached-acceptance runs).
- Writer lease на `src/nextorm.core/DataContext/DataContext.cs`: регионы #247 `353-420` и T246 `469-583` могут сосуществовать, но одновременные файловые правки запрещены.
- `DataContext.cs` не обязан быть свободен от уже интегрированных непересекающихся изменений #247; их revision/diff фиксируется.
- #243/#245 могут менять `eng/perf/iteration14_gate.py`/`iteration14-budgets.json`: T246 их не меняет и не требует конкретного порядка до/после.
- Generator и общие report pages (`docs/comparisons/benchmarks.md`, EN+RU) обновлять под отдельным writer lease, не затирая sibling rows.
- Acceptance привязан к фактически frozen candidate tree: если sibling integration меняет runtime, benchmark configuration или build inputs после measurement — результаты инвалидируются.
- Commits/merge/push этим планом не разрешаются; действуют разрешения collection.

## Write footprint

Production (планируемый): `src/nextorm.core/Query/QueryCommandExtensions.cs:308-397`; `src/nextorm.core/DataContext/DataContext.cs:469-583`; `src/nextorm.core/DataContext/QueryPlanner.cs:603-656,820-829`; `src/nextorm.core/DataContext/LobDataReader.cs:20-398`. `CommandReaderOwner.cs` — read-only контракт, production-правки не планируются.

Tests/tooling/reports: `tests/nextorm.core.tests/LobDataReaderTests.cs`; `tests/nextorm.core.tests/RecordsAffectedTests.cs`; `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`; `tests/nextorm.sqlite.tests/ResultReaderTests.cs`; `tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs`; `tests/nextorm.integration.tests/SqliteSpecificTests.cs`; `benchmarks/nextorm.benchmark/SqliteBenchmarkStreaming.cs`; `eng/perf/t246_evidence.py` (новый); `eng/perf/t246-budgets.json` (новый); `eng/perf/update_benchmark_tables.py`; `docs/comparisons/benchmarks.md`; `docs/comparisons/feature-benchmarks-2026-10-17.md`; `docs/specs/performance/feature-benchmark-followups-2026-10-17.md`; `docs/specs/status/D246-raw-datareader-1.md`; `artifacts/pdca/T246/**`.

**Неопределённость footprint:** точные пути RU/README generated outputs не подтверждены (scout до редактирования generator); полный output map `update_benchmark_tables.py` — prerequisite D246-00/06. Не расширять footprint самостоятельно.

## DO task list

| Задача | Действие и target | Решение |
|---|---|---|
| **D246-00** | Записать статус, контракт `contract.json`, brief; проверить issue URL, toolchain, output map, measurement lease | **fix now** |
| **D246-01** | Recon baseline, probe и Roslyn key/invalidation audit: `QueryPlanner.cs:603-656,820-829`, `DataContext.cs:469-583` | **fix now**, prerequisite для D246-02/03 |
| **D246-02** | Изолированный reuse immutable raw preparation; ни mutable runtime resources, ни shared cache flag не переиспользуются/не меняются | **fix now**, только после положительного D246-01 |
| **D246-03** | Уменьшить доказанный wrapper overhead в существующем `LobDataReader`; сохранить observable guards и ownership | **fix now**, после D246-01 |
| **D246-04** | Дополнить variant/branch/lifetime tests, выполнить inner loop и targeted mutations | **fix now**, вместе с D246-02/03 |
| **D246-05** | Reader gate + negative fixtures, полный boundary, integration, coverage, final R=3 и cached acceptance regression | **fix now** |
| **D246-06** | Отчёты EN/RU, generator `--check`, follow-up outcome, manifest и frozen-tree lint | **fix now** |
| **D246-07** | CHECK по rv1; только CHECK может дать итоговый вердикт | **fix now** |

### Fix now / deferred

Fix now: неверное указание SQLite seam в status/follow-up; недостоверность старого ShortRun oracle (новый baseline); отсутствие B/empty в generated table; отсутствие reader gate; запрет sticky shared-command cache mutation (негативная проверка).

Deferred + триггер (всё в milestone `1.0.9-rc3`): command/owner pooling — до доказанного остаточного bottleneck и отдельного PLAN; perf benchmarking неподдерживаемых провайдеров — до появления raw-reader support (typed-error tests сейчас); дополнительные absolute host-specific budgets — до согласования canonical host; owner-approved empty tolerance — только явное будущее решение owner с отдельным PLAN, не способ закрыть текущий провал.

## Versioned evidence contract — rv1

Контракт: `artifacts/pdca/T246/rv1/contract.json`; manifest: `artifacts/pdca/T246/rv1/manifest.json`.

```yaml
task: T246
issue: 246
status_file: docs/specs/status/D246-raw-datareader-1.md
path: artifacts/pdca/T246/rv1/contract.json
base_ref: 01815f3a
collection_baseline_ref: 01815f3a
contract_rv: 1
footprint: [явный утверждённый список файлов из Write footprint, без wildcard]
exclusions: [artifacts/**, docs/specs/status/**]
required_rows: [E246-01, E246-02, E246-03, E246-04, E246-05, E246-06, E246-07, E246-08, E246-09, E246-10, E246-11, E246-12, E246-13, E246-14, E246-15]
```

`footprint` в JSON — не ссылка на раздел и не wildcard. Все источники/артефакты ниже **планируемые**; evidence считается полученным только после фактического исполнения. Каждый execution сохраняет command array, source/artifact revisions, exit code и selected count, где применимо. Обозначения `C-*` ниже означают точные вызовы из разделов этого плана и сохраняются в contract целиком (название без argv недостаточно).

| Row / rv | Requirements; сценарий | Evidence kind и источник | Вызов; требуемый результат | Планируемые артефакты | Владелец | Applicability predicate |
|---|---|---|---|---|---|---|
| **E246-01 / 1** | R13: preflight, reuse feasibility, allocation attribution | semantic scout report через Roslyn; probe raw output | `C-RECON`, exit 0; положительный key/invalidation audit; измерения и unattributed remainder | `recon/**`, `recon/semantic-audit.md`, probe source/hash | scout + coder | Всегда (V2 выбран) |
| **E246-02 / 1** | R13: новый baseline | BDN machine reports, logs, dataset parity | `C-BDN`, baseline I=1..3; все exit 0; Default, все arms, accuracy protocol | `perf/baseline/run-{1,2,3}/**` | perf-coder | Всегда |
| **E246-03 / 1** | R3, R5, R6: wrapper/guards/cancellation | Unit test logs и discovery counts | `C-CORE`; exit 0, selected ≥1, новые branches закрыты | `tests/core-inner.log`, report JSON | test-coder | Всегда |
| **E246-04 / 1** | R3, R4: SQLite values, empty и lifetime | SQLite test results | `C-SQLITE`; exit 0, selected ≥1, owner/resource cases проходят | `tests/sqlite-inner.log`, branch map | test-coder | Всегда |
| **E246-05 / 1** | R8: cache/mode/shape/parameter isolation | Tests + reviewed diff + Roslyn impact | `C-CORE`, `C-SQLITE`; exit 0; нет shared flag mutation/кэширования mutable owner resources | `tests/reuse-isolation.md`, logs | coder + scout | Всегда (reuse — часть V2) |
| **E246-06 / 1** | R3–R8: полный boundary и warnings | Build/test output | `C-BOUNDARY`; каждый exit 0, tests selected >0, warnings-as-errors соблюдены | `boundary/**` | test-coder | Всегда |
| **E246-07 / 1** | R4–R7: cross-provider behavior/report | Реальные provider results и capability inventory | `C-INTEGRATION`; exit 0; SQLite/PG/SQL Server и unsupported checks MySQL/ClickHouse реально исполнены | `integration/**`, `cross-provider.md` | integration-coder | Всегда (planner/wrapper seam меняется) |
| **E246-08 / 1** | R1, R2: final 10k/empty | BDN raw reports + ratio calculation | `C-BDN`, final I=1..3, затем `C-GATE`; все exit 0 и все условия oracle | `perf/final/**`, `perf/gate.json` | perf-coder | Всегда |
| **E246-09 / 1** | R9: fail-closed gate | Negative fixture results | `C-GATE-NEGATIVE`; exit 0 = все плохие fixtures отвергнуты; перечислить каждый случай | `perf/gate-negative.json` | tooling-coder | Всегда |
| **E246-10 / 1** | R8: existing cached acceptance | Полные BDN outputs, existing acceptance result | `C-CACHED`; exit 0 плюс PASS существующего acceptance contract, не только process exit | `perf/cached-acceptance/**` | perf-coder | Всегда (V2) |
| **E246-11 / 1** | R11: coverage и branch delta | Coverage XML/Cobertura, branch map | `C-COVERAGE`; exit 0; line ≥85 %, branch ≥75 %; новые branches подтверждены | `coverage/**` | test-coder | Всегда |
| **E246-12 / 1** | R4, R5, R8, R11: mutations | Mutation patches, executions, restoration hash | `C-MUTATION`; exit 0; все валидные targeted mutations killed; clean rerun PASS | `mutation/**` | test-coder | Всегда |
| **E246-13 / 1** | R10: docs и generated table | Generated diff, precision/report review | `C-DOCS`; оба exit 0; B и оба empty представлены; результат не преувеличен | `docs/generator.log`, `docs/review.md` | docs-coder | Всегда |
| **E246-14 / 1** | R7, R12: LOB/CSV/footprint unchanged outside allowance | Full test outputs, semantic/diff audit | `git diff --check` exit 0; `C-BOUNDARY`/integration; review подтверждает scope, CRLF и LOB/CSV isolation | `review/footprint.md`, `review/lob-csv.md`, diff | coder + scout | Всегда |
| **E246-15 / 1** | R12: validated evidence bound to tree | Validator reports + frozen-tree manifest | команды ниже; каждая exit 0; required_rows точно совпадают, evidence не пуст | `inner-loop-*.json`, `manifest.json`, lint logs | coder; CHECK проверяет | Всегда |

```bash
python3 scripts/validate_inner_loop.py brief artifacts/pdca/T246/rv1/inner-loop-brief.json
python3 scripts/validate_inner_loop.py report artifacts/pdca/T246/rv1/inner-loop-report.json
python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T246/rv1/manifest.json
python3 scripts/pdca_evidence.py generate --repo /home/alex/sources/nextorm --contract artifacts/pdca/T246/rv1/contract.json --output artifacts/pdca/T246/rv1
python3 scripts/pdca_evidence.py lint --repo /home/alex/sources/nextorm --manifest artifacts/pdca/T246/rv1/manifest.json
```

`manifest.json` должен содержать `task`, `tree`, `status_file`, `contract_rv=1`, все `required_rows` и уникальные rows. `met` требует непустое evidence; `open` — reason; `na` — доказанный predicate. Для безусловных строк выше `na` не разрешён.

**CHECK re-gather budget: 2** точечных re-gather, владелец — CHECK (запрос недостающего уже существующего evidence либо повторное чтение узкой области; не два дополнительных BDN цикла и не разрешение менять критерии). После исчерпания — verdict с явным GAP; при стойкой низкой уверенности маршрут в PLAN/escalate.

**Versioning:** `rv1` — начальный контракт, supersession отсутствует. При обоснованном изменении контракта `rv2` явно указывает `supersedes rv1`, сохраняет существующие row/requirement IDs и обязательства, добавляет IDs новых вариантов. Отсутствующий отчёт сам по себе не обосновывает ревизию контракта или новую DO iteration.

## Risks

- Preparation reuse может потребовать key/invalidation изменений шире минимального footprint.
- Wrapper overhead может быть мал относительно provider execution; микровыигрыш не гарантирует empty parity.
- Строгий zero-tolerance gate может остаться недоказуемым при host noise; это GAP, не повод расширить допуск.
- Новый cache mode может загрязнить LOB/CSV/materialized preparation.
- Оптимизация non-cancellable path может изменить cancellation/exception precedence.
- Соседние edits и concurrent BDN могут инвалидировать измерения.
- Generator output map и точный overlay priority table не переданы.
- Installed profiler/coverage/exporter capabilities ещё не проверены.

## Prerequisites / «Чего не сказала постановка»

| Пробел | Закрытие |
|---|---|
| Существующий cache key и invalidation полностью не описаны | Roslyn scout + spike до production edits |
| Нет доказанной cost attribution | Измеренный probe; не заключать по object count |
| Нет owner-approved empty tolerance | Допуск 0 %, критерий не ослабляется |
| Нет точного issue URL | Verify текущий repo/issue до записи ссылки (D246-00) |
| Нет source DB bootstrap invocation | Проверить готовую DB; при отсутствии — in-cycle scout/bootstrap prerequisite |
| Нет полной output map generator | Read-only scout; запрет неизвестных writes |
| Нет полного текста overlay priority table | Консервативно все evidence rows P1 |
| Нет полного CLI/schema глобального evidence skill | Оркестратор сверяет skill до DO; обязательства контракта не ослаблять |
| Не подтверждена точность Default на host | Первый baseline R=3; при провале — GAP |
| Нет разрешения на новые concurrency guarantees | Сохранять существующие, не расширять |

## DO → PLAN classification

Текущий вход — initial PLAN, не DO blocker. При возврате: отсутствующий tool/socket/DB либо необходимый bootstrap — **предусловие**, новая активная in-cycle задача; допустимое свойство окружения — явное допущение только без ослабления acceptance; доказанный внешний ресурсный/полномочный блокер — рекомендация оркестратору вызвать `escalate`; непонятный key/invalidation или неполная атрибуция — сначала точечный `scout`; стойкая низкая уверенность — `escalate`, триггер 5.

Если D246-01 опровергает V2, следующая задача:

> **P246-02:** пересмотреть способ сокращения fixed cost по измеренной атрибуции и найденным ограничениям cache isolation, сохранив R246-01…13.

D246-02/03 остаются активными и `blocked`, не `done` и не `superseded` только из-за prerequisite. Фактическая замена scope требует явного `superseded → replacement` с переносом всех критериев и остатка. Только реальное изменение действий/зависимостей повышает `r` и сбрасывает `n=1`.

## Feasibility / confidence

- **Высокая:** установленные execution paths, ownership boundaries, необходимость разделять fixed/per-row cost.
- **Средняя:** выбранная архитектурная возможность reuse существующей preparation infrastructure.
- **Не установлена:** величина выигрыша, достижимость zero-tolerance parity и точность Default runs.

## Gate-1 self-check

- [x] Цель и наблюдаемые acceptance, включая негативные случаи.
- [x] Минимальное решение, альтернативы и выбранный V2.
- [x] Конкретные DO-задачи и prerequisite gate.
- [x] Риски, допущения и обработка неизвестных.
- [x] Unit/integration strategy, scope, branch delta, mutation и coverage.
- [x] Полная variant matrix; deferred имеет триггер.
- [x] Docs plan с generated EN/RU exception.
- [x] Perf protocol, baseline, ratio oracle, isolation и конечный R.
- [x] Recon decision с наблюдаемым результатом.
- [x] Последовательный unit mode и sibling requirements.
- [x] Design checklist: без нового public API, materializer, naked reader, pooling, sticky cache mutation и ненужной абстракции.
- [x] rv1 evidence contract, stable IDs, sources, commands, outcomes, artifacts, owners, predicates.
- [x] CHECK re-gather budget 2; manifest path закреплён.
- [x] План и contract записываются до DO; collection parent владеет PLAN→DO boundary.

## DO units (state)

| Unit | State | Criteria covered | superseded→replacement |
|---|---|---|---|
| D246-00 | pending | R246-12, rv1 setup | — |
| D246-01 | pending | R246-13 | — |
| D246-02 | pending (blocked on D246-01) | R246-01, R246-08 | — |
| D246-03 | pending (blocked on D246-01) | R246-01, R246-03 | — |
| D246-04 | pending | R246-03..R246-06, R246-11 | — |
| D246-05 | pending | R246-01, R246-02, R246-09, R246-10 | — |
| D246-06 | pending | R246-10, R246-12 | — |
| D246-07 | pending | verdict | — |

## Defect history (durable)

| Defect key | Observed r/n | Fixes applied | Last recurrence | Escalation outcome |
|---|---|---|---|---|
| — | — | — | — | — |

## Current state

Phase **P (PLAN complete)**, `plan_state=ready`, revision `r=1`, iteration `1/3` (not yet started). DO not started — collection parent owns the PLAN→DO boundary and resumes this cycle at DO after its applicability check.

## Next plan / next todo

Next: **D246-00** (status/contract/brief, toolchain + output-map + measurement-lease preflight), then **D246-01** (baseline + probe + Roslyn isolation audit). Todo is not carried between sessions; it lives here.

## Changed files

| Path | What |
|---|---|
| `docs/specs/status/D246-raw-datareader-1.md` | this plan + contract + progress log (created at PLAN) |

## Pointers

Issue #246; `docs/specs/performance/feature-benchmark-followups-2026-10-17.md:17,27`; `docs/comparisons/feature-benchmarks-2026-10-17.md:63,66-67,147`; baseline CSV `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report.csv:15-21`; code anchors `src/nextorm.core/Query/QueryCommandExtensions.cs:308-397`, `src/nextorm.core/DataContext/DataContext.cs:469-583`, `src/nextorm.core/DataContext/LobDataReader.cs:20-398`, `src/nextorm.sqlite/SqliteDialect.cs:18`, `src/nextorm.core/DataContext/QueryPlanner.cs:603-656,820-829`.

## Progress log

`2026-10-17T12:08:30Z | P | revision r=1 | iteration 1/3 | PLAN ready — handoff to collection (plan_state=ready) | docs/specs/status/D246-raw-datareader-1.md`
