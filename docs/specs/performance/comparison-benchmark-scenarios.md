# Comparison benchmarks tier 1 — query-shape scenarios

Статус: дизайн согласован (user review пройден 2026-10-04). Non-zero части реализованы в D188 DO (2026-10-09): SQLite projection/aggregates/paging/streaming (buffered+unbuffered DTO) и JSON/CSV cross-library. Zero/raw-reader подгруппа — `blocked + active`, ждёт CHECK-прохождения #189.
Tracking: [GitHub issue #188](https://github.com/AlexeyShirshov/nextorm/issues/188), milestone `1.0.9-rc2`.
Branch: `1.0.9-rc2`.
Зависимость: zero/raw-reader подгруппа item 10 (#188) заблокирована незавершённым [#189](https://github.com/AlexeyShirshov/nextorm/issues/189). Цикл #189 остановлен на собственном perf-предикате приёмки; zero-арм остаётся активным (blocked), не снят, не подменён материализацией, не подменён другим провайдером и не «вылечен» падением на materialize→serialize. Текущий статус #189 не приписывается реализации `ToDataReader` (D134) как причине пост-D134 регрессии.

## 1. Контекст и пробел

Кросс-библиотечное сравнение (Nextorm / Dapper / linq2db / EF Core) на SQLite покрывало:

- end-to-end классы `SqliteBenchmarkAny`, `...First`, `...Single`, `...Join`, `...Where`, `...Iteration`, `...LargeIteration`, `...Cache` — полный режим, `benchmark-report.md:14`;
- 12 SQL-конструкций в `SqliteBenchmarkFeaturesFair` / `...Cached` — `docs/comparisons/benchmarks.md:52-87`.

Не были покрыты query-shapes/терминалы: проекция в DTO как отдельный сценарий, агрегаты/`GroupBy`, сортировка+пагинация, потоковый вывод; JSON/CSV сравнивались только внутри nextorm (`SqliteBenchmarkWriteJson`, `SqliteBenchmarkCsv` — nextorm-only). Не было ни одного zero-materialization арма.

Все перечисленные non-zero пробелы закрыты D188: добавлены `SqliteBenchmarkProjection`, `SqliteBenchmarkAggregates`, `SqliteBenchmarkPaging`, `SqliteBenchmarkStreaming` (buffered/unbuffered DTO) и расширены `SqliteBenchmarkWriteJson` / `SqliteBenchmarkCsv` (competitor materialize→serialize). Единственный незакрытый пробел — zero-materialization (raw reader) подгруппа streaming, она остаётся `blocked + active`.

## 2. Согласованный scope (tier 1)

1. `SqliteBenchmarkProjection` — `Select(x => new Dto{...})` cross-library.
2. `SqliteBenchmarkAggregates` — `Count`/`Sum`/`GroupBy→Count` на SQLite, SQL-side.
3. `SqliteBenchmarkPaging` — `OrderBy + offset/limit`.
4. `SqliteBenchmarkStreaming` — buffered DTO vs unbuffered DTO enumeration; zero-materialization (raw reader) — blocked by #189.
5. JSON/CSV cross-library — расширить `SqliteBenchmarkWriteJson` / `SqliteBenchmarkCsv` армами Dapper / linq2db / EF Core через materialize→serialize.

Вне scope: LOB-стриминг `ToStream`/`ToTextReader` (не бенчмаркаем); DML и navigation/eager loading cross-library (tier 2); synchronous-кампания; дополнительные провайдеры.

## 3. Общий стенд и переиспользование

- Данные: `BenchDb.FilePath` (env `NEXTORM_BENCH_DB` → `/tmp/nextorm-bench/test.db` → `data/test.db`), `TestDataRepository` (`SimpleEntity`/`LargeEntity`/`ComplexEntity`), `EFDataContext`, `Linq2DbDataRepository`.
- Наблюдённые counts (D01/D188 DO, read-only): `simple_entity`=10, `complex_entity`=3, `large_table`=10000. `large_table` используется для projection/aggregates/paging/streaming; JSON-класс сеет собственную временную `json_bench`-таблицу, CSV-класс — срез `large_table` (10 000 строк).
- Проводник: `Microsoft.Data.Sqlite` у всех участников; БД на tmpfs.
- Конструктор-паттерн: `builder.UseSqlite(BenchDb.FilePath) → CreateDataContext → EnsureConnectionOpen`; Dapper через `((SqliteDataContext)_ctx.DataContext).ConnectionString`; EF `UseSqlite + NoTracking`; linq2db `Linq2DbDataRepository`.
- Атрибуты класса: `[MemoryDiagnoser]`, `[Config(typeof(NextormConfig))]`, `[GroupBenchmarksBy(ByJob, ByCategory)]`, `[HideColumns(...)]`, class-level `[BenchmarkCategory]` где применимо.
- Job: `NextormConfig` (`ShortRun` + `InProcessEmitToolchain`, `NEXTORM_BENCH_FULL=1` → `Job.Default`); отчёты в `benchmarks/BenchmarkDotNet.Artifacts`. `NEXTORM_BENCH_FULL=0` для D188-свипов.
- Sinks/валидация (benchmark-only, `benchmarks/nextorm.benchmark/`): `BenchmarkRowSink` (count+checksum+bytes, без удержания строк), `BenchmarkSerializationSink` (write-only non-retaining `Stream`), `BenchmarkComparisonValidation` (`Measure`/`Checksum`/`Ensure*`/`Report`).

### 3.1 Граница тайминга

- Семантическая сверка армов, подготовка prepared/compiled-команд, проверка counts/checksums/pages/сериализованного вывода выполняются в конструкторе/`[GlobalSetup]` вне измеряемой области.
- `Prepare()` / `CompiledQuery.Compile` / `CompileAsyncQuery` создаются вне timed-body; Category A измеряет только повторное исполнение reusable-команды.
- Sink сбрасывается вне timed-body; `BenchmarkSerializationSink` не удерживает сериализованный payload.
- Никакие production-пути/API, фикстуры или пакеты не меняются.

## 4. Классы, армы и эквивалентность

### 4.1 `SqliteBenchmarkProjection`

- Данные: `large_table` (10 000 строк).
- Общий DTO: `ProjectionDto { Id, Str }` из `id, someString` (non-LOB).
- Category A (prepared/compiled): `A_Nextorm_Prepared_ToListAsync`, `A_EFCore_Compiled_ToListAsync` (`EF.CompileAsyncQuery`), `A_Linq2Db_Compiled_ToList` (`CompiledQuery.Compile`), `A_Dapper_ToListAsync` (raw SQL; тот же арм несёт и Category B).
- Category B (обычный путь): `B_Nextorm_Cached_ToListAsync` (неявный план-кэш), `B_EFCore_ToListAsync`, `B_Linq2Db_ToListAsync`.
- Setup-валидация: count и order-sensitive checksum совпадают у nextorm/linq2db/EF Core/Dapper.

### 4.2 `SqliteBenchmarkAggregates`

- Данные: `large_table` (10 000 строк); grouping key `id % 100` (`GroupModulus=100`, ровно 100 бакетов, SQL-side).
- Операции: `Count()`, `Sum(x => x.Id)` (long, ожидаемо 50 005 000), `GroupBy(x => x.Id % 100) → GroupCountRow { Key, Count }`.
- Category A: `A_Nextorm_Prepared_{Count,Sum,GroupByCount}`, `A_EFCore_Compiled_{...}`, `A_Linq2Db_Compiled_{...}`, `Dapper_{Count,Sum,GroupByCount}` (последние несут и B).
- Category B: `B_Nextorm_Cached_{...}`, `B_EFCore_{...}`, `B_Linq2Db_{...}`.
- Setup-валидация: rows=10000, sum=ExpectedRows*(ExpectedRows+1)/2, group count=100; агрегация строго на стороне SQLite (нет client-side).

### 4.3 `SqliteBenchmarkPaging`

- Данные: `large_table` (10 000 строк).
- Запрос: `ORDER BY id OFFSET 5000 LIMIT 100` (nextorm `OrderBy/Offset/Limit`; конкуренты `ORDER BY ... LIMIT 100 OFFSET 5000`). Ключ сортировки — уникальный primary key, tie-breaker не требуется.
- Category A: `A_Nextorm_Prepared_PageAsync`, `A_EFCore_Compiled_PageAsync`, `A_Linq2Db_Compiled_Page`, `A_Dapper_PageAsync` (Dapper raw-арм несёт и Category B).
- Category B: `B_Nextorm_Cached_PageAsync`, `B_EFCore_PageAsync`, `B_Linq2Db_PageAsync`.
- Setup-валидация: page count=100 (непустая interior-страница), первый `Id`=5001, checksums совпадают у всех библиотек.

### 4.4 `SqliteBenchmarkStreaming`

- Данные: `large_table` (10 000 строк); не-LOB колонки `id, someString`.
- Buffered DTO: `A_Nextorm_Prepared_ToList_Dto`, `A_EFCore_Compiled_ToList_Dto`, `Dapper_ToList_Dto` (A+B), `Linq2Db_Compiled_ToList_Dto`, `B_Nextorm_Cached_ToList_Dto`, `B_EFCore_ToList_Dto`, `B_Linq2Db_ToList_Dto`.
- Unbuffered DTO enumeration: `A_Nextorm_Prepared_AsyncStream_Dto`, `A_EFCore_Compiled_AsyncStream_Dto`, `Dapper_AsyncStream_Dto` (A+B), `B_Nextorm_Cached_AsyncStream_Dto`, `B_EFCore_AsyncStream_Dto`, `B_Linq2Db_AsyncStream_Dto`.
- Все армы сводят колонки в один `BenchmarkRowSink`; setup-валидация сверяет count+checksum buffered/unbuffered DTO.
- **Zero/raw-reader подгруппа — `blocked + active` (ждёт #189).** Не реализована: нет per-row entity/DTO construction и collection accumulation. EF Core исключён из zero-подгруппы (нет LINQ raw-reader). Нет `ToList`-fallback, нет relabel материализованного арма как zero, нет «SQLite unavailable» как pass. После CHECK-прохождения #189 добавляются `Nextorm_ToDataReader`/`ToDataReaderAsync`, `Dapper_ExecuteReader`, `Linq2Db_ExecuteReader` с идентичным sink; только тогда zero-утверждения попадают в отчёт.
- Headline-метрики zero-подгруппы (когда разблокируется): `Allocated` / `Gen0` / `Gen2`; время вторично.

### 4.5 JSON/CSV cross-library

Расширены **closest equivalent** армами: у конкурентов нет non-materialising терминала, поэтому их арм = materialize + serializer. Это фиксируется в отчёте.

- `SqliteBenchmarkWriteJson` (временная `json_bench`, `[Params(1_000, 10_000, 100_000)]`):
  - native (сохранены и исполняются): `WriteJson_Array_Scalar`, `WriteJson_Array_ScalarPayload`, `WriteJson_Array_Dto`, `WriteJson_Array_WideDto`, `WriteJsonAsync_Array_Scalar`, `WriteJsonAsync_Array_ScalarPayload`, `ToList_Dto` (baseline).
  - competitor materialize→serialize: `Dapper_ToList_Json`, `Linq2Db_ToList_Json`, `EFCore_ToList_Json` (`Query`/linq2db/EF `ToList` + `JsonSerializer.Serialize` в non-retaining `BenchmarkSerializationSink`).
  - Setup-валидация: сериализованный вывод десериализуется и сверяется по count+checksum с native baseline (логическая, не байтовая эквивалентность, т.к. native streaming и `JsonSerializer` форматируют по-разному).
- `SqliteBenchmarkCsv` (срез `large_table` 10 000 строк; колонки `id, someString, dt`):
  - native (сохранены): `Nextorm_ToList` (baseline), `Nextorm_ToList_ManualCsv`, `Nextorm_WriteCsv`, `Nextorm_StringProjection_ToList`, `Nextorm_StringProjection_WriteCsv`.
  - competitor: `Dapper_ToList_ManualCsv`, `Linq2Db_ToList_ManualCsv`, `EFCore_ToList_ManualCsv` — тот же ручной writer.
  - CSV-dialect: header `Id,Str,Dt\r\n`, строка `{Id},{Str},{Dt:O}\r\n`, UTF-8 без BOM, `\r\n`. Setup-валидация: побайтовое совпадение с native baseline.
  - `[BenchmarkCategory("csv")]`; замороженные 7 `acceptance`-кейсов не затронуты.

## 5. Симметрия reuse

- Category A: nextorm `.Prepare()` против `EF.CompileAsyncQuery` / `LinqToDB.CompiledQuery.Compile` и raw Dapper.
- Category B: nextorm implicit план-кэш (fresh fluent с константами) против regular (non-compiled) EF Core / linq2db и raw Dapper.
- Dapper raw-армы присутствуют в A и B (у него нет compiled-режима); это явно отражено в `[BenchmarkCategory]`.
- Сохраняется та же методология, что в `SqliteBenchmarkFeaturesFair` / `...Cached`.
- Category A не заявляется, если не представлены все применимые reusable-курьеры (nextorm/EF/linq2db); Dapper — общий raw-арм.

## 6. Fairness constraints

- Один ADO-провайдер (`Microsoft.Data.Sqlite`) у всех; БД на tmpfs.
- Одинаковая семантика SQL на арм; параметризация согласована.
- Обязательный sink/`Consumer` для всех сравнимых армов (`BenchmarkRowSink`, `BenchmarkSerializationSink`).
- `large_table` (10 000) для throughput-сценариев; не использовать `simple_entity`/`complex_entity` (10 и 3 строки). Расхождение «доки ~10k / инварианты 10 и 3» из issue #188 фиксируется как наблюдённое: `large_table`=10000.
- Не трогать замороженные 7 `acceptance`-кейсов; новые классы — свои `[BenchmarkCategory]`.
- Setup-проверки вне timed-области; результаты не подменяются клиентской агрегацией/материализацией.
- Zero-materialization означает отсутствие entity/DTO-материализации, **не** нулевые аллокации.

## 7. Критерии приёмки

- Новые классы компилируются, помечены `[MemoryDiagnoser]`, запускаются через `--filter`; BDN рапортует 0 failures; каждый класс — исполненный прогон (ShortRun) с сохранённым отчётом.
- `dotnet build nextorm.slnx -c Debug` и `-c Release` — 0 warning / 0 error.
- `--anyCategories=acceptance` по-прежнему даёт ровно 7 кейсов.
- JSON/CSV: добавленные cross-library армы дают корректный вывод (логически эквивалентный native JSON; побайтово совпадающий manual CSV), подтверждено setup-сверкой.
- Zero-армы (после CHECK-прохождения #189) демонстрируют корректное чтение и, в сохранённом отчёте, измеримо меньший `Allocated` против микро-армов. До этого zero-подгруппа остаётся `blocked + active` и в отчёт не попадает.

## 8. Доки

- `docs/comparisons/benchmarks.md` + `docs/ru/comparisons/benchmarks.md`: новые строки/таблицы (projection, aggregates, paging, streaming, JSON/CSV cross-library).
- `docs/specs/comparison/linq2db-comparison.md` + `docs/specs/ru/comparison/linq2db-comparison.md`: обновить утверждение о бенчмаркнутых сценариях (`:197-199`).
- `docs/comparisons/capabilities.md` / `capability-matrix.md` — только если формулировка о потоковых терминалах меняется после #189.
- Публичные страницы не ссылаются на внутренние `docs/specs/**`; отчёты — на retained BDN artifacts / публичные страницы.

## 9. Зависимости

- Item 10 (zero-arm на SQLite) заблокирован #189; остаётся `blocked + active`, исходный объём и критерии не изменены.
- Items 7–9 и JSON/CSV cross-library — без зависимостей, реализованы non-zero частью D188.
- #189 не CHECK-прошёл; D188 не заменяет и не снимает zero-arm.

## 10. Риски

- `GroupBy`-трансляция `id % K` может отличаться по поддержке/плану между библиотеками — форма зафиксирована `id % 100`.
- Zero-arm без идентичного sink даёт недостоверный результат (dead-code elimination) — sink обязателен.
- Смешение reuse-стратегий искажает сравнение — строго держать Category A/B.
- Native JSON streaming и `JsonSerializer` форматируют по-разному — сравнение логическое (count/checksum), не байтовое; CSV сравнивается побайтово.
- ShortRun/InProcess дают runnable coverage и индикативные измерения, а не статистические рейтинги.

## 11. Ссылки

- `benchmarks/nextorm.benchmark/SqliteBenchmarkProjection.cs`, `...Aggregates.cs`, `...Paging.cs`, `...Streaming.cs`, `...WriteJson.cs`, `...Csv.cs`
- `benchmarks/nextorm.benchmark/BenchmarkRowSink.cs`, `BenchmarkSerializationSink.cs`, `BenchmarkComparisonValidation.cs`
- `benchmarks/nextorm.benchmark/Linq2DbDataContext.cs`, `EFDataContext.cs`, `TestDataContext.cs`, `BenchDb.cs`, `NextormConfig.cs`
- `docs/comparisons/benchmarks.md`, `docs/specs/performance/benchmark-report.md`, `docs/specs/performance/acceptance-benchmarks.md`
- `docs/specs/comparison/linq2db-comparison.md:197-199`
