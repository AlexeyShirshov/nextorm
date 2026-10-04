# Comparison benchmarks tier 1 — query-shape scenarios

Статус: дизайн согласован (user review пройден 2026-10-04). Реализация не начата — отложена до отдельного решения.
Tracking: [GitHub issue #188](https://github.com/AlexeyShirshov/nextorm/issues/188), milestone `1.0.9-rc2`.
Branch: `1.0.9-b`.
Зависимость: item 10 (zero-materialization) разблокируется после [#189](https://github.com/AlexeyShirshov/nextorm/issues/189).

## 1. Контекст и пробел

Кросс-библиотечное сравнение (Nextorm / Dapper / linq2db / EF Core) на SQLite покрывает:

- end-to-end классы `SqliteBenchmarkAny`, `...First`, `...Single`, `...Join`, `...Where`, `...Iteration`, `...LargeIteration`, `...Cache` — полный режим, `benchmark-report.md:14`;
- 12 SQL-конструкций в `SqliteBenchmarkFeaturesFair` / `...Cached` — `docs/comparisons/benchmarks.md:52-87`.

Не покрыты query-shapes/терминалы: проекция в DTO как отдельный сценарий, агрегаты/`GroupBy`, сортировка+пагинация, потоковый вывод без материализации; JSON/CSV сравниваются только внутри nextorm (`SqliteBenchmarkWriteJson`, `SqliteBenchmarkCsv` — nextorm-only). Нет ни одного zero-materialization арма.

## 2. Согласованный scope (tier 1)

1. `SqliteBenchmarkProjection` — `Select(x => new Dto{...})` cross-library.
2. `SqliteBenchmarkAggregates` — `Count`/`Sum`/`GroupBy→Count` на SQLite, SQL-side.
3. `SqliteBenchmarkPaging` — `OrderBy + offset/limit`.
4. `SqliteBenchmarkStreaming` — микро-материализация vs нулевая (blocked by #189).
5. JSON/CSV cross-library — расширить `SqliteBenchmarkWriteJson` / `SqliteBenchmarkCsv` армами Dapper / linq2db / EF Core через materialize→serialize.

Вне scope: LOB-стриминг `ToStream`/`ToTextReader` (не бенчмаркаем); DML и navigation/eager loading cross-library (tier 2).

## 3. Общий стенд и переиспользование

- Данные: `BenchDb.FilePath` (env `NEXTORM_BENCH_DB` → `/tmp/nextorm-bench/test.db` → `data/test.db`), `TestDataRepository` (`SimpleEntity`/`LargeEntity`/`ComplexEntity`), `EFDataContext`, `Linq2DbDataRepository`.
- Проводник: `Microsoft.Data.Sqlite` у всех участников; БД на tmpfs.
- Конструктор-паттерн: `builder.UseSqlite(BenchDb.FilePath) → CreateDataContext → EnsureConnectionOpen`; Dapper через `((SqliteDataContext)_ctx.DataContext).ConnectionString`; EF `UseSqlite + NoTracking`; linq2db `Linq2DbDataRepository`.
- Атрибуты класса: `[MemoryDiagnoser]`, `[Config(typeof(NextormConfig))]`, `[GroupBenchmarksBy(ByJob, ByCategory)]`, `[HideColumns(...)]`, class-level `[BenchmarkCategory]`.
- Job: `NextormConfig` (`ShortRun` + `InProcessEmitToolchain`, `NEXTORM_BENCH_FULL=1` → `Job.Default`); отчёты в `benchmarks/BenchmarkDotNet.Artifacts`.

## 4. Новые классы

### 4.1 `SqliteBenchmarkProjection`

- Данные: `large_table` (~10 000 строк).
- Проекция: `Select(x => new ProjectionDto { Id = x.Id, Str = x.Str })` (добавить `ProjectionDto`).
- Армы:
  - `Nextorm_Prepared_ToList` (`.Prepare()`), `Nextorm_Cached_ToList` (fresh fluent + план-кэш);
  - `Dapper_Query_Dto` (raw SQL `select id, someString`);
  - `Linq2Db_Compiled_ToList` (`CompiledQuery.Compile`), `Linq2Db_ToList`;
  - `EFCore_Compiled_ToList` (`EF.CompileAsyncQuery`), `EFCore_ToList`.
- Показывает стоимость построения `TResult` относительно implicit entity-маппинга.

### 4.2 `SqliteBenchmarkAggregates`

- Данные: `large_table` (~10 000 строк).
- Операции: `Count()`, `Sum(x => x.Id)` (long), `GroupBy(x => x.Id % 100).Select(g => new { g.Key, C = g.Count() })` (~100 групп).
- Армы: `Nextorm` (prepared/cached), `Dapper` (raw SQL), `linq2db`, `EF Core`.
- Результат обязательно «потребляется» в `_sink`, чтобы JIT не выбросил запрос.
- Точная форма group-key (`id % K` vs иная low-cardinality колонка) фиксируется при реализации; критерий — портируемость трансляции во всех четырёх библиотеках.

### 4.3 `SqliteBenchmarkPaging`

- Данные: `large_table` (~10 000 строк).
- Запрос: `OrderBy(x => x.Id).Offset(5_000).Limit(100)` (nextorm; API — `Offset`/`Limit`, `EntityBuilder.cs:2130,2141`) против `OrderBy(...).Skip(5_000).Take(100)` (Dapper raw SQL `ORDER BY id LIMIT 100 OFFSET 5000`, linq2db, EF Core).
- Армы: `Nextorm` (prepared/cached), `Dapper`, `linq2db`, `EF Core`.

### 4.4 `SqliteBenchmarkStreaming` (blocked by #189)

- Данные: `large_table` (~10 000 строк).
- Микро-материализация: `Nextorm_ToList_Dto`, `Nextorm_AsyncEnumerable_Dto`, `Dapper_Query_Dto`, `Linq2Db_ToList_Dto`, `EFCore_ToList_Dto`.
- Нулевая: `Nextorm_ToDataReader`, `Dapper_ExecuteReader`, `Linq2Db_ExecuteReader` — EF Core исключён (нет LINQ-raw-reader).
- Все армы сводят колонки в **идентичный** sink (`long`-аккумулятор, `Consumer`), иначе dead-code elimination.
- Headline-метрики: `Allocated` / `Gen0` / `Gen2` (суть — исчезновение per-row heap-объекта), время вторично.
- На SQLite `ToDataReader` доступен только после #189; до этого класс либо отсутствует, либо помечается `[Benchmark(Description=...)]`-пометкой «после #189» (решается при реализации).

### 4.5 JSON/CSV cross-library

- Расширить `SqliteBenchmarkWriteJson`: добавить `Dapper_ToList_Json` (`Query<Dto>` + `JsonSerializer.Serialize`), `Linq2Db_ToList_Json`, `EFCore_ToList_Json`; сравнение с `Nextorm_WriteJson`.
- Расширить `SqliteBenchmarkCsv`: добавить `Dapper_ToList_ManualCsv`, `Linq2Db_...`, `EFCore_...` через тот же ручной CSV-writer, что и существующий `Nextorm_ToList_ManualCsv`; сравнение с `Nextorm_WriteCsv`.
- Это **closest equivalent**, не тот же API: у конкурентов нет non-materialising терминала, поэтому их арм = materialize + serializer. Фиксируется в отчёте.

## 5. Симметрия reuse

- Category A: nextorm `.Prepare()` против `EF.CompileAsyncQuery` / `LinqToDB.CompiledQuery.Compile` и raw Dapper.
- Category B: nextorm implicit план-кэш (fresh fluent с константами) против regular (non-compiled) EF Core / linq2db и raw Dapper.
- Сохранять ту же методологию, что в `SqliteBenchmarkFeaturesFair` / `...Cached`.

## 6. Fairness constraints

- Один ADO-провайдер (`Microsoft.Data.Sqlite`) у всех; БД на tmpfs.
- Одинаковая семантика SQL на арм; параметризация согласована.
- Обязательный sink/`Consumer` для агрегатов и zero-армов.
- `large_table` (10k) для throughput-сценариев; не использовать `simple_entity`/`complex_entity` (малые + расхождение «доки ~10k / инварианты 10 и 3», зафиксировано в issue #188).
- Не трогать замороженные 7 `acceptance`-кейсов; новые классы — свои `[BenchmarkCategory]`.

## 7. Критерии приёмки

- Новые классы компилируются, помечены `[MemoryDiagnoser]`, запускаются через `--filter`; BDN рапортует 0 failures; каждый класс — исполненный прогон (ShortRun) с сохранённым отчётом.
- `dotnet build nextorm.slnx -c Debug` и `-c Release` — 0 warning / 0 error.
- `--anyCategories=acceptance` по-прежнему даёт ровно 7 кейсов.
- JSON/CSV: добавленные cross-library армы дают корректный вывод (те же байты/строки, что у существующих baselines), подтверждено сверкой.
- Zero-армы (после #189) демонстрируют измеримо меньший `Allocated` против микро-армов в сохранённом отчёте.

## 8. Доки

- `docs/comparisons/benchmarks.md` + `docs/ru/comparisons/benchmarks.md`: новые строки/таблицы (projection, aggregates, paging, streaming, JSON/CSV cross-library).
- `docs/specs/comparison/linq2db-comparison.md` + `docs/specs/ru/comparison/linq2db-comparison.md`: обновить утверждение о бенчмаркнутых сценариях (`:197-199`).
- `docs/comparisons/capabilities.md` / `capability-matrix.md` — только если формулировка о потоковых терминалах меняется после #189.

## 9. Зависимости

- Item 10 (zero-arm на SQLite) заблокирован #189.
- Items 7–9 и JSON/CSV cross-library — без зависимостей, могут идти параллельно.

## 10. Риски

- `GroupBy`-трансляция `id % K` может отличаться по поддержке/плану между библиотеками — при реализации выбрать портируемую форму.
- Zero-arm без идентичного sink даёт недостоверный результат (dead-code elimination) — sink обязателен.
- Смешение reuse-стратегий искажает сравнение — строго держать Category A/B.
- `ToDataReader` на SQLite после #189 может дать неожиданный allocation-профиль из-за non-sequential чтения BLOB — не использовать LOB в streaming-сценарии.

## 11. Ссылки

- `benchmarks/nextorm.benchmark/SqliteBenchmarkLargeIteration.cs`, `...Where.cs`, `...FeaturesFair.cs`, `...FeaturesFairCached.cs`, `...WriteJson.cs`, `...Csv.cs`
- `benchmarks/nextorm.benchmark/Linq2DbDataContext.cs`, `EFDataContext.cs`, `TestDataContext.cs`, `BenchDb.cs`, `NextormConfig.cs`
- `docs/comparisons/benchmarks.md`, `docs/specs/performance/benchmark-report.md`, `docs/specs/performance/acceptance-benchmarks.md`
- `docs/specs/comparison/linq2db-comparison.md:197-199`
