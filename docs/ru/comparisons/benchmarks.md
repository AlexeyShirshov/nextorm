# Бенчмарки: Nextorm против Dapper, linq2db и EF Core

<!-- benchmarks:conditions:start -->
Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; в ячейке — **самый
быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. Машина: AMD Ryzen 7 5800HS with Radeon
Graphics (8 логических / 4 физических ядер), .NET 10.0.12, BenchmarkDotNet 0.15.8 (`ShortRun` +
`InProcessEmitToolchain`). На **2026-10-17** для таблиц сквозных сценариев, аллокаций, категории A и категории
B (раздел tier-1 сохраняет дату своего прогона). В каждой строке лидирующая ячейка выделена **полужирным** —
быстрейшая в таблицах времени и с наименьшей аллокацией в таблице аллокаций.
<!-- benchmarks:conditions:end -->

> **Новое (2026-10-17):** отчёт по фичам только на warm-пути — без веток `Prepare()`, одна таблица на
> tier, колонки — конкуренты — в [Feature-бенчмарках — 2026-10-17](feature-benchmarks-2026-10-17.md).

## Сквозные сценарии

<!-- benchmarks:e2e:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.29 μs** | 14.89 μs | 15.76 μs | 36.64 μs |
| [Широкая выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **8.331 ms** | 8.680 ms | 9.366 ms | 9.771 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **886.7 μs** | 1,369.6 μs | 1,412.3 μs | 3,199.6 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.594 ms** | 26.432 ms | 393.986 ms | 110.812 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **889.9 μs** | 1,399.7 μs | 1,498.9 μs | 3,620.0 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **91.35 μs** | 146.41 μs | 388.41 μs | 351.03 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **90.87 μs** | 140.79 μs | 394.50 μs | 345.20 μs |
<!-- benchmarks:e2e:end -->

### Аллокации

Аллокации для того же самого быстрого метода каждой библиотеки (колонка BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Выборка данных | **792 B** | 1,904 B | 2,088 B | 10,224 B |
| Широкая выборка данных | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **92.43 KB** | 180.72 KB | 253.53 KB | 789.17 KB |
| Simulate work | **6.25 MB** | 8.90 MB | 121.57 MB | 13.68 MB |
| Any | **85.16 KB** | 139.07 KB | 233.60 KB | 807.05 KB |
| FirstOrDefault | **8.63 KB** | 16.48 KB | 187.58 KB | 80.40 KB |
| SingleOrDefault | **8.36 KB** | 16.40 KB | 187.82 KB | 80.40 KB |
<!-- benchmarks:allocations:end -->

## Сравнение по фичам (честное)

Сквозная таблица смешивает стратегии переиспользования. Чтобы сравнение было симметричным, фичи разделены
на две категории:

- **Категория A — prepared / compiled / raw:** Nextorm через `Prepare()` против ближайшей *скомпилированной*
  формы того же запроса (`EF.CompileAsyncQuery`, `LinqToDB.CompiledQuery.Compile`) и сырого Dapper (его IL
  кэшируется по тексту SQL). Это «сравнение скомпилированного со скомпилированным».
- **Категория B — warm cached против обычных:** Nextorm через неявный кэш планов (на каждый вызов — свежий
  fluent-запрос с константами, так что переиспользуется только закэшированный план) против обычных
  (не compiled) EF Core и linq2db и сырого Dapper.

Каждый метод фичи выполняет **10 запросов**; в ячейке — **на запрос**: среднее и аллокация (BenchmarkDotNet
`Mean` и `Allocated`, делённые на 10). Исходные отчёты:
[Категория A](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) ·
[Категория B](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md).

### Категория A — prepared против compiled и raw

На **2026-10-17**.

| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **9.26 µs / 744 B** | 14.60 µs / 1.34 KB | 15.08 µs / 1.59 KB | 35.61 µs / 7.90 KB |
| CTE | **9.49 µs / 793 B** | 21.43 µs / 1.57 KB | 21.52 µs / 1.77 KB | — |
| `SELECT DISTINCT` | **11.01 µs / 744 B** | 18.55 µs / 1.56 KB | 17.41 µs / 1.59 KB | 40.60 µs / 7.66 KB |
| `EXCEPT` | **11.07 µs / 760 B** | 17.72 µs / 1.44 KB | 20.80 µs / 1.68 KB | 43.47 µs / 8.81 KB |
| IN-list | **12.32 µs / 1.57 KB** | 18.65 µs / 1.27 KB | 43.53 µs / 15.23 KB | 60.70 µs / 14.77 KB |
| `INTERSECT` | **10.36 µs / 744 B** | 16.77 µs / 1.29 KB | 19.66 µs / 1.63 KB | 41.75 µs / 7.94 KB |
| Join по 4 таблицам | **12.63 µs / 1.06 KB** | 25.52 µs / 2.02 KB | 23.27 µs / 2.07 KB | 46.82 µs / 8.49 KB |
| `LEFT JOIN` | **12.09 µs / 1.20 KB** | 19.84 µs / 2.22 KB | 20.29 µs / 2.32 KB | 41.64 µs / 9.92 KB |
| Рекурсивный CTE | **12.64 µs / 752 B** | 24.24 µs / 1.53 KB | 25.73 µs / 1.78 KB | — |
| Строковые функции | **8.96 µs / 736 B** | 13.92 µs / 1.28 KB | 15.18 µs / 1.64 KB | 35.08 µs / 7.49 KB |
| UDF | **9.27 µs / 768 B** | 14.08 µs / 1.28 KB | 15.05 µs / 1.64 KB | 34.82 µs / 7.49 KB |
| Оконные функции | **14.44 µs / 848 B** | 30.04 µs / 1.54 KB | 31.84 µs / 1.74 KB | — |

Nextorm prepared быстрее **на каждой** фиче и с наименьшей аллокацией на 11 из 12 — исключение IN-list
(1.57 KB против 1.27 KB у Dapper). По времени это примерно в 3.4–4.9 раз меньше, чем у EF Core, и в 1.5–2.3
раза меньше, чем у Dapper; по аллокациям — в 1.7–2.1 раза меньше Dapper и в 8–12 раз меньше EF Core.

### Категория B — warm cached против обычных

<!-- benchmarks:category-b:start -->
| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **13.04 μs / 3.22 KB** | 14.00 μs / 1.34 KB | 25.05 μs / 3.80 KB | 56.78 μs / 11.95 KB |
| CTE | 32.72 μs / 10.89 KB | **19.83 μs / 1.57 KB** | 56.89 μs / 9.06 KB | — |
| `SELECT DISTINCT` | **15.53 μs / 3.80 KB** | 18.41 μs / 1.56 KB | 30.44 μs / 3.91 KB | 64.19 μs / 12.20 KB |
| `EXCEPT` | 17.15 μs / 6.38 KB | **16.96 μs / 1.44 KB** | 38.10 μs / 5.05 KB | 72.82 μs / 15.31 KB |
| IN-list | 31.65 μs / 7.76 KB | **18.12 μs / 1.27 KB** | 69.44 μs / 18.91 KB | 112.63 μs / 22.61 KB |
| `INTERSECT` | 16.79 μs / 6.36 KB | **15.60 μs / 1.29 KB** | 36.77 μs / 5.01 KB | 71.94 μs / 14.38 KB |
| Join по 4 таблицам | 32.96 μs / 10.67 KB | **25.03 μs / 2.02 KB** | 65.38 μs / 14.10 KB | 114.20 μs / 24.69 KB |
| `LEFT JOIN` | 23.03 μs / 6.16 KB | **19.83 μs / 2.22 KB** | 69.12 μs / 8.60 KB | 94.80 μs / 20.27 KB |
| Рекурсивный CTE | 35.33 μs / 13.31 KB | **24.44 μs / 1.53 KB** | 60.41 μs / 8.39 KB | — |
| Строковые функции | 14.97 μs / 4.20 KB | **13.93 μs / 1.28 KB** | 30.95 μs / 4.78 KB | 66.26 μs / 13.27 KB |
| UDF | 15.53 μs / 4.29 KB | **14.29 μs / 1.28 KB** | 48.46 μs / 6.50 KB | 66.25 μs / 13.27 KB |
| Оконные функции | 31.55 μs / 5.03 KB | **29.75 μs / 1.55 KB** | 62.11 μs / 5.99 KB | — |
<!-- benchmarks:category-b:end -->

В warm-пути Nextorm быстрее Dapper на `CASE WHEN` и `SELECT DISTINCT` и близок на строковых
функциях; отстаёт от Dapper примерно в 1.01–1.16 раза на `EXCEPT`, оконных функциях, строковых
функциях, `INTERSECT`, UDF и `LEFT JOIN` и в 1.32–1.75 раза на join по 4 таблицам, CTE, рекурсивном CTE
и IN-list — при этом заметно
опережая обычные linq2db и EF Core по времени. Аллокации в warm-пути выше, чем в prepared (ключ плана
перестраивается на каждый вызов); они намного ниже, чем у обычных EF Core, и ниже linq2db на большинстве
фич, но выше linq2db на CTE, рекурсивном CTE, `EXCEPT` и `INTERSECT`.

> **IN-list, категория B.** Захваченная коллекция отключает кэш планов по замыслу (SQL перестраивается на
> каждый вызов), поэтому captured-варианты платят за полную сборку, а inline-варианты попадают в кэш. Четыре
> варианта Nextorm дают 31.7–34.9 µs — это фича, где кэшированный сырой SQL Dapper впереди с наибольшим
> отрывом.

## Сценарии tier 1 по форме запроса

Добавлены в #188, чтобы покрыть формы запросов, которых нет в двух таблицах выше: проекцию в свежий
DTO, SQL-агрегаты, сортированную пагинацию, стриминг (buffered/unbuffered DTO против
zero-materialization raw reader) и кросс-библиотечный вывод JSON/CSV. Данные: `large_table`
(10 000 строк) для projection/aggregates/paging/streaming; JSON-класс сеет собственную таблицу
`json_bench`, CSV-класс берёт срез `large_table` (10 000 строк). Инварианты честного сравнения остаются
`simple_entity`=10 и `complex_entity`=3 — «10 000 строк» выше относится к `large_table`, а не к этим
фикстурам. На **2026-10-10**.

Категории переиспользования те же, что в сравнении по фичам: **категория A** — Nextorm `Prepare()` против
ближайшей *скомпилированной* формы (`EF.CompileAsyncQuery`, `LinqToDB.CompiledQuery.Compile`) и сырого
Dapper; **категория B** — неявный кэш планов Nextorm против обычных (не compiled) EF Core/linq2db и
сырого Dapper. У Dapper нет compiled-формы запроса/ридера, поэтому его армы несут обе категории.

**У стриминга три группы:** буферизованная материализация DTO, небуферизованное перечисление DTO и
**zero-materialization** подгруппа raw reader. Zero-подгруппа только синхронная (async reader-армы
отложены): Nextorm `ToDataReader`, Dapper `ExecuteReader` и linq2db `ExecuteReader` (его
`DataReaderAsync.Reader`) читают одну и ту же проекцию `id, someString` через **один общий
неудерживающий sink**. EF Core в zero-подгруппу **не** входит — у него нет LINQ raw-reader-аналога.
«Zero-materialization» означает отсутствие конструирования entity/DTO, **не** нулевые аллокации: путь
reader всё равно аллоцирует свою команду/ридер на вызов.

Для JSON/CSV конкурентные армы — **ближайший эквивалент**: ни у Dapper, ни у linq2db, ни у EF Core нет
non-materialising терминала вывода, поэтому их арм = материализация → ручной сериализатор. Это не
заявление о zero-materialization.

> **Как читать эти числа.** `ShortRun` + `InProcessEmitToolchain` — исследовательский режим: различия
> меньше ~20% считайте шумом и смотрите полный отчёт (error, стандартное отклонение, аллокации), прежде
> чем делать выводы. В таблицах приведены `Mean`/`Allocated` BenchmarkDotNet как есть; это не заявление о
> всеобщей победе.

### Проекция — `Select(x => new Dto { Id, Str })`

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToListAsync | A | 3.653 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 4.904 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 5.522 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 6.464 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 3.799 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 6.472 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 8.533 ms | 3.62 MB |

### Агрегаты — SQL-side `Count` / `Sum` / `GroupBy → Count`

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

| Операция | Арм | Категория | Mean | Allocated |
|---|---|---:|---:|---:|
| Count | A_Nextorm_Prepared_Count | A | 13.77 μs | 408 B |
| Count | A_Linq2Db_Compiled_Count | A | 18.02 μs | 1,280 B |
| Count | A_EFCore_Compiled_Count | A | 38.62 μs | 7,744 B |
| Count | Dapper_Count | A+B | 17.30 μs | 944 B |
| Count | B_Nextorm_Cached_Count | B | 19.31 μs | 3,824 B |
| Count | B_Linq2Db_Count | B | 24.56 μs | 2,864 B |
| Count | B_EFCore_Count | B | 48.88 μs | 10,080 B |
| Sum | A_Nextorm_Prepared_Sum | A | 277.57 μs | 410 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 291.81 μs | 1,290 B |
| Sum | A_EFCore_Compiled_Sum | A | 319.95 μs | 7,770 B |
| Sum | Dapper_Sum | A+B | 291.59 μs | 954 B |
| Sum | B_Nextorm_Cached_Sum | B | 286.43 μs | 4,226 B |
| Sum | B_Linq2Db_Sum | B | 304.90 μs | 3,490 B |
| Sum | B_EFCore_Sum | B | 338.05 μs | 10,978 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,281.91 μs | 4,737 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,309.32 μs | 7,025 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,343.09 μs | 33,089 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,317.46 μs | 12,329 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,314.02 μs | 10,577 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,393.44 μs | 11,553 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,407.25 μs | 42,641 B |

### Пагинация — `OrderBy(Id).Offset(5000).Limit(100)`

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_PageAsync | A | 87.54 μs | 13.99 KB |
| A_Linq2Db_Compiled_Page | A | 107.44 μs | 17.61 KB |
| A_EFCore_Compiled_PageAsync | A | 130.82 μs | 44.81 KB |
| A_Dapper_PageAsync | A+B | 101.53 μs | 18.15 KB |
| B_Nextorm_Cached_PageAsync | B | 99.68 μs | 22.48 KB |
| B_Linq2Db_PageAsync | B | 138.21 μs | 22.79 KB |
| B_EFCore_PageAsync | B | 185.63 μs | 56.9 KB |

### Стриминг — buffered / unbuffered DTO и zero-materialization reader

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Буферизованный DTO:

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToList_Dto | A | 3.743 ms | 1,328.84 KB |
| A_EFCore_Compiled_ToList_Dto | A | 8.012 ms | 3,701.54 KB |
| Linq2Db_Compiled_ToList_Dto | A | 5.637 ms | 1,508.25 KB |
| Dapper_ToList_Dto | A+B | 6.864 ms | 1,742.31 KB |
| B_Nextorm_Cached_ToList_Dto | B | 3.786 ms | 1,332.39 KB |
| B_Linq2Db_ToList_Dto | B | 8.148 ms | 1,511.3 KB |
| B_EFCore_ToList_Dto | B | 8.594 ms | 3,705.59 KB |

Небуферизованное перечисление DTO:

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_AsyncStream_Dto | A | 3.663 ms | 1,250.56 KB |
| A_EFCore_Compiled_AsyncStream_Dto | A | 5.047 ms | 3,444.77 KB |
| Dapper_AsyncStream_Dto | A+B | 4.591 ms | 1,485.82 KB |
| B_Nextorm_Cached_AsyncStream_Dto | B | 4.019 ms | 1,254.3 KB |
| B_Linq2Db_AsyncStream_Dto | B | 4.595 ms | 1,254.55 KB |
| B_EFCore_AsyncStream_Dto | B | 5.105 ms | 3,448.89 KB |

Zero-materialization raw reader (синхронные; общий неудерживающий sink; EF Core исключён):

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToDataReader | A | 4.302 ms | 944.8 KB |
| Linq2Db_ToDataReader | B | 3.762 ms | 943 KB |
| Dapper_ToDataReader | B | 3.761 ms | 942.77 KB |

### JSON / CSV кросс-библиотечно

Воспроизведение JSON: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md).
Воспроизведение CSV: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkCsv*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md).

JSON-отчёт покрывает `RowCount` 1 000 / 10 000 / 100 000; показан блок на 10 000 строк (Ratio — относительно
`ToList_Dto` в том же блоке). Конкурентные армы — материализация → ручной сериализатор.

| Метод (10 000 строк) | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| WriteJson_Array_Scalar | 5,724.3 μs | 0.93 | 38.04 KB |
| ToList_Dto | 6,170.0 μs | 1.00 | 785.21 KB |
| WriteJsonAsync_Array_Scalar | 6,620.2 μs | 1.07 | 38.25 KB |
| Linq2Db_ToList_Json | 9,150.2 μs | 1.48 | 963.56 KB |
| Dapper_ToList_Json | 9,415.9 μs | 1.53 | 1,273.78 KB |
| WriteJson_Array_ScalarPayload | 10,341.2 μs | 1.68 | 11,495.22 KB |
| WriteJson_Array_Dto | 10,351.9 μs | 1.68 | 435.64 KB |
| WriteJsonAsync_Array_ScalarPayload | 11,258.5 μs | 1.83 | 11,495.38 KB |
| EFCore_ToList_Json | 11,303.0 μs | 1.83 | 3,158.67 KB |
| WriteJson_Array_WideDto | 13,801.5 μs | 2.24 | 11,527.94 KB |

CSV (срез `large_table`, 10 000 строк; Ratio — относительно `Nextorm_ToList`):

| Метод | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Nextorm_StringProjection_ToList | 2.819 ms | 0.33 | 1,019.52 KB |
| Nextorm_StringProjection_WriteCsv | 4.029 ms | 0.47 | 949.67 KB |
| Nextorm_ToList | 8.558 ms | 1.00 | 2,267.38 KB |
| Nextorm_ToList_ManualCsv | 9.927 ms | 1.16 | 4,141.67 KB |
| Nextorm_WriteCsv | 11.076 ms | 1.29 | 1,743.61 KB |
| Dapper_ToList_ManualCsv | 13.291 ms | 1.55 | 4,865.94 KB |
| Linq2Db_ToList_ManualCsv | 13.385 ms | 1.56 | 4,322.11 KB |
| EFCore_ToList_ManualCsv | 14.364 ms | 1.68 | 6,516.97 KB |

## Как читать числа

- Все участники используют один и тот же ADO.NET-провайдер (`Microsoft.Data.Sqlite`), поэтому сравнение
  apples-to-apples; база SQLite лежит в tmpfs, чтобы дисковый ввод-вывод не маскировал ORM.
- Nextorm показывает оба пути переиспользования — неявный кэш планов и явный `Prepare()`, — а в сквозной
  ячейке указан самый быстрый из них. См. [Переиспользование запросов: кэш и Prepare](../engine/03-plan-cache-and-reuse.md).
- `ShortRun` + `InProcessEmitToolchain` — быстрый режим: сигнала он даёт достаточно, но небольшие различия
  считайте шумом и смотрите полный отчёт (mean, error, стандартное отклонение и аллокации).
- Это одна машина, один провайдер (SQLite) и фиксированный набор сценариев. Это не утверждение про любую
  нагрузку — воспроизводите на своих данных и железе.

## Воспроизведение

```bash
# Сквозные сценарии ("*SqliteBenchmarkIteration*" не захватывает SqliteBenchmarkLargeIteration:
# после префикса "SqliteBenchmark" имена типов расходятся)
for filter in "*SqliteBenchmarkIteration*" "*SqliteBenchmarkLargeIteration*" "*SqliteBenchmarkWhere*" \
              "*SqliteBenchmarkSimulateWork*" "*SqliteBenchmarkAny*" "*SqliteBenchmarkFirst*" \
              "*SqliteBenchmarkSingle*"; do
  dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "$filter"
done

# Сравнение по фичам (категории A / B)
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFair.*"
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFairCached*"
```

Каждый отчёт по ссылке — это вывод BenchmarkDotNet, закоммиченный в
`benchmarks/BenchmarkDotNet.Artifacts/results/`. Переменная `NEXTORM_BENCH_FULL=1` включает полный
out-of-process прогон вместо `ShortRun`.

Таблицы сквозных сценариев, аллокаций и категории B на этой странице **генерируются** из этих отчётов —
не правьте их вручную. После прогона:

```bash
python3 .pdca/scripts/perf/update_benchmark_tables.py          # переписать readme.md и обе языковые версии статьи
python3 .pdca/scripts/perf/update_benchmark_tables.py --check  # режим CI: упасть, если таблица устарела
```

Тот же процесс запускается из `.github/workflows/benchmarks.yml` (вручную или раз в месяц) и открывает
pull request с новыми отчётами и таблицами.

## См. также

- [Возможности](capabilities.md) — что поддерживает каждая библиотека.
- [Переиспользование запросов: кэш и Prepare](../engine/03-plan-cache-and-reuse.md).
