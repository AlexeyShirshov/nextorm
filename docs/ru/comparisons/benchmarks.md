# Бенчмарки: Nextorm против Dapper, linq2db и EF Core

<!-- benchmarks:conditions:start -->
Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; в ячейке — **самый
быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. Машина: AMD EPYC 9V74 (4 логических / 2
физических ядер), .NET 10.0.14, BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`). На
**2026-10-30** для таблиц сквозных сценариев, аллокаций и категории B (таблица категории A и раздел tier-1
сохраняют дату своего прогона). В каждой строке лидирующая ячейка выделена **полужирным** — быстрейшая в
таблицах времени и с наименьшей аллокацией в таблице аллокаций.
<!-- benchmarks:conditions:end -->

> **Новое (2026-10-17):** отчёт по фичам только на warm-пути — без веток `Prepare()`, одна таблица на
> tier, колонки — конкуренты — в [Feature-бенчмарках — 2026-10-17](feature-benchmarks-2026-10-17.md).

## Сквозные сценарии

<!-- benchmarks:e2e:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **9.926 μs** | 13.107 μs | 13.492 μs | 26.482 μs |
| [Широкая выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **7.589 ms** | 8.176 ms | 8.537 ms | 8.950 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **855.5 μs** | 1,153.7 μs | 1,198.2 μs | 2,468.9 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.024 ms** | 20.144 ms | 235.186 ms | 77.044 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **869.7 μs** | 1,180.8 μs | 1,248.4 μs | 2,613.2 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **87.72 μs** | 118.99 μs | 223.82 μs | 246.84 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **86.57 μs** | 118.70 μs | 228.30 μs | 246.69 μs |
<!-- benchmarks:e2e:end -->

### Аллокации

Аллокации для того же самого быстрого метода каждой библиотеки (колонка BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Выборка данных | **1.02 KB** | 1.86 KB | 2.04 KB | 9.98 KB |
| Широкая выборка данных | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **118.98 KB** | 180.71 KB | 253.52 KB | 789.16 KB |
| Simulate work | **6.25 MB** | 8.90 MB | 122.26 MB | 13.68 MB |
| Any | **85.16 KB** | 139.07 KB | 233.60 KB | 807.05 KB |
| FirstOrDefault | **8.63 KB** | 16.48 KB | 187.58 KB | 80.40 KB |
| SingleOrDefault | **8.36 KB** | 16.40 KB | 187.58 KB | 80.40 KB |
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

<!-- benchmarks:category-a:start -->
На **2026-10-30**.

| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **8.79 μs / 744 B** | 12.21 μs / 1.34 KB | 12.48 μs / 1.59 KB | 24.58 μs / 7.90 KB |
| CTE | **9.13 μs / 793 B** | 16.60 μs / 1.57 KB | 16.63 μs / 1.77 KB | — |
| `SELECT DISTINCT` | **10.17 μs / 744 B** | 13.84 μs / 1.56 KB | 13.53 μs / 1.59 KB | 26.69 μs / 7.66 KB |
| `EXCEPT` | **10.25 μs / 760 B** | 14.56 μs / 1.44 KB | 15.99 μs / 1.68 KB | 28.99 μs / 8.81 KB |
| IN-list | **11.15 μs / 1.63 KB** | 14.12 μs / 1.27 KB | 25.13 μs / 15.23 KB | 36.71 μs / 14.77 KB |
| `INTERSECT` | **9.60 μs / 744 B** | 13.66 μs / 1.29 KB | 15.05 μs / 1.63 KB | 27.41 μs / 7.94 KB |
| Join по 4 таблицам | **11.87 μs / 1.06 KB** | 21.29 μs / 2.02 KB | 18.79 μs / 2.07 KB | 33.04 μs / 8.49 KB |
| `LEFT JOIN` | **11.50 μs / 1.20 KB** | 16.74 μs / 2.22 KB | 16.89 μs / 2.32 KB | 29.62 μs / 9.92 KB |
| Рекурсивный CTE | **11.11 μs / 752 B** | 18.05 μs / 1.53 KB | 18.86 μs / 1.78 KB | — |
| Строковые функции | **8.73 μs / 736 B** | 11.92 μs / 1.28 KB | 12.45 μs / 1.64 KB | 24.56 μs / 7.49 KB |
| UDF | **8.73 μs / 768 B** | 11.98 μs / 1.28 KB | 12.40 μs / 1.64 KB | 23.93 μs / 7.49 KB |
| Оконные функции | **12.50 μs / 848 B** | 20.51 μs / 1.55 KB | 21.25 μs / 1.74 KB | — |
<!-- benchmarks:category-a:end -->

Nextorm prepared быстрее **на каждой** фиче и с наименьшей аллокацией на 11 из 12 — исключение IN-list, где
кэшированный raw SQL Dapper экономнее. Точные соотношения — в таблице выше; они зависят от раннера, поэтому
дельты меньше ~20 % считайте шумом.

### Категория B — warm cached против обычных

<!-- benchmarks:category-b:start -->
| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **11.33 μs / 3.52 KB** | 12.19 μs / 1.34 KB | 16.03 μs / 3.80 KB | 33.69 μs / 11.95 KB |
| CTE | 21.69 μs / 11.83 KB | **16.54 μs / 1.57 KB** | 33.10 μs / 9.20 KB | — |
| `SELECT DISTINCT` | **12.67 μs / 4.15 KB** | 14.19 μs / 1.56 KB | 17.70 μs / 3.91 KB | 37.53 μs / 12.20 KB |
| `EXCEPT` | **14.46 μs / 6.95 KB** | **14.46 μs / 1.44 KB** | 21.93 μs / 5.05 KB | 45.65 μs / 15.31 KB |
| IN-list | 20.34 μs / 7.99 KB | **14.13 μs / 1.27 KB** | 42.62 μs / 18.91 KB | 80.37 μs / 22.61 KB |
| `INTERSECT` | 13.85 μs / 6.93 KB | **13.55 μs / 1.29 KB** | 21.37 μs / 5.01 KB | 44.31 μs / 14.38 KB |
| Join по 4 таблицам | 24.38 μs / 11.58 KB | **21.02 μs / 2.02 KB** | 39.49 μs / 13.95 KB | 81.53 μs / 24.59 KB |
| `LEFT JOIN` | 17.43 μs / 6.58 KB | **16.68 μs / 2.22 KB** | 39.70 μs / 8.60 KB | 63.96 μs / 20.55 KB |
| Рекурсивный CTE | 24.91 μs / 14.28 KB | **17.98 μs / 1.53 KB** | 34.80 μs / 8.54 KB | — |
| Строковые функции | 12.18 μs / 4.52 KB | **11.84 μs / 1.28 KB** | 18.12 μs / 4.78 KB | 39.65 μs / 13.39 KB |
| UDF | 12.38 μs / 4.60 KB | **11.87 μs / 1.28 KB** | 24.47 μs / 6.39 KB | 38.90 μs / 13.39 KB |
| Оконные функции | 20.56 μs / 5.42 KB | **20.43 μs / 1.55 KB** | 37.44 μs / 5.99 KB | — |
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

Таблицы сквозных сценариев, аллокаций, категории A и категории B на этой странице **генерируются** из этих
отчётов — не правьте их вручную. После прогона:

```bash
python3 tools/benchmarks/update_benchmark_tables.py          # переписать readme.md и обе языковые версии статьи
python3 tools/benchmarks/update_benchmark_tables.py --check  # режим CI: упасть, если таблица устарела
```

Тот же процесс запускается из `.github/workflows/benchmarks.yml` — автоматически после каждого мержа в `main`,
затрагивающего движок (`src/**`) или проект бенчмарков, и вручную из вкладки Actions. Workflow открывает pull
request с новыми отчётами и таблицами.

## См. также

- [Возможности](capabilities.md) — что поддерживает каждая библиотека.
- [Переиспользование запросов: кэш и Prepare](../engine/03-plan-cache-and-reuse.md).
