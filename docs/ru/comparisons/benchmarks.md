# Бенчмарки: Nextorm против Dapper, linq2db и EF Core

<!-- benchmarks:conditions:start -->
Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; в ячейке — **самый
быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. Машина: AMD Ryzen 7 5800HS with Radeon
Graphics (8 логических / 4 физических ядер), .NET 10.0.12, BenchmarkDotNet 0.15.8 (`ShortRun` +
`InProcessEmitToolchain`). На **2026-11-07** для таблиц сквозных сценариев, аллокаций и категории B (таблица
категории A и раздел tier-1 сохраняют дату своего прогона). В каждой строке лидирующая ячейка выделена
**полужирным** — быстрейшая в таблицах времени и с наименьшей аллокацией в таблице аллокаций.
<!-- benchmarks:conditions:end -->

> **Разбивка по фичам:** сравнение по фичам, разбитое на тиры, — без веток `Prepare()`, одна таблица
> на tier, колонки — конкуренты — в [Feature-бенчмарках](feature-benchmarks.md).

## Сквозные сценарии

<!-- benchmarks:e2e:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.89 μs** | 15.85 μs | 16.66 μs | 39.88 μs |
| [Широкая выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **9.188 ms** | 10.154 ms | 10.220 ms | 10.756 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **951.1 μs** | 1,408.6 μs | 1,493.5 μs | 3,467.2 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.871 ms** | 25.427 ms | 390.157 ms | 114.500 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **1.024 ms** | 1.536 ms | 1.699 ms | 4.213 ms |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **103.8 μs** | 165.9 μs | 421.8 μs | 401.6 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **102.7 μs** | 157.2 μs | 407.9 μs | 401.2 μs |
<!-- benchmarks:e2e:end -->

### Аллокации

Аллокации для того же самого быстрого метода каждой библиотеки (колонка BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Выборка данных | **976 B** | 1,856 B | 2,472 B | 10,224 B |
| Широкая выборка данных | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **103.36 KB** | 180.71 KB | 253.53 KB | 789.16 KB |
| Simulate work | **6.25 MB** | 8.90 MB | 121.57 MB | 13.68 MB |
| Any | **85.16 KB** | 139.07 KB | 233.60 KB | 807.06 KB |
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
На **2026-11-07**.

| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **10.04 μs / 672 B** | 14.86 μs / 1.34 KB | 16.19 μs / 1.59 KB | 35.30 μs / 7.90 KB |
| CTE | **10.12 μs / 720 B** | 21.78 μs / 1.57 KB | 22.50 μs / 1.77 KB | — |
| `SELECT DISTINCT` | **11.71 μs / 672 B** | 19.43 μs / 1.56 KB | 18.29 μs / 1.59 KB | 42.23 μs / 7.66 KB |
| `EXCEPT` | **11.52 μs / 688 B** | 18.70 μs / 1.44 KB | 21.63 μs / 1.68 KB | 45.34 μs / 8.81 KB |
| IN-list | **12.96 μs / 1.56 KB** | 19.80 μs / 1.27 KB | 50.38 μs / 15.23 KB | 63.95 μs / 14.77 KB |
| `INTERSECT` | **10.80 μs / 672 B** | 17.27 μs / 1.29 KB | 21.19 μs / 1.63 KB | 43.88 μs / 7.94 KB |
| Join по 4 таблицам | **13.50 μs / 1,009 B** | 28.75 μs / 2.02 KB | 33.58 μs / 2.07 KB | 53.83 μs / 8.49 KB |
| `LEFT JOIN` | **13.63 μs / 1.13 KB** | 22.96 μs / 2.22 KB | 21.91 μs / 2.32 KB | 46.38 μs / 9.92 KB |
| Рекурсивный CTE | **12.64 μs / 680 B** | 26.34 μs / 1.53 KB | 27.71 μs / 1.78 KB | — |
| Строковые функции | **9.87 μs / 696 B** | 17.61 μs / 1.28 KB | 15.64 μs / 1.64 KB | 37.56 μs / 7.49 KB |
| UDF | **10.13 μs / 696 B** | 14.69 μs / 1.28 KB | 17.12 μs / 1.64 KB | 44.38 μs / 7.49 KB |
| Оконные функции | **15.04 μs / 776 B** | 31.93 μs / 1.54 KB | 34.40 μs / 1.74 KB | — |
<!-- benchmarks:category-a:end -->

Nextorm prepared быстрее **на каждой** фиче и с наименьшей аллокацией на 11 из 12 — исключение IN-list, где
кэшированный raw SQL Dapper экономнее. Точные соотношения — в таблице выше; они зависят от раннера, поэтому
дельты меньше ~20 % считайте шумом.

### Категория B — warm cached против обычных

<!-- benchmarks:category-b:start -->
| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **14.55 μs / 3.47 KB** | 17.79 μs / 1.34 KB | 30.39 μs / 3.80 KB | 63.81 μs / 11.95 KB |
| CTE | 38.61 μs / 11.65 KB | **24.33 μs / 1.57 KB** | 73.19 μs / 9.06 KB | — |
| `SELECT DISTINCT` | **18.14 μs / 4.16 KB** | 19.95 μs / 1.56 KB | 33.80 μs / 3.91 KB | 71.01 μs / 12.20 KB |
| `EXCEPT` | 19.91 μs / 6.40 KB | **18.43 μs / 1.44 KB** | 41.43 μs / 5.05 KB | 79.70 μs / 15.42 KB |
| IN-list | 37.46 μs / 8.09 KB | **20.56 μs / 1.27 KB** | 80.27 μs / 18.91 KB | 119.57 μs / 22.61 KB |
| `INTERSECT` | 18.09 μs / 6.38 KB | **18.03 μs / 1.29 KB** | 39.57 μs / 5.01 KB | 77.50 μs / 14.38 KB |
| Join по 4 таблицам | 43.92 μs / 11.45 KB | **28.10 μs / 2.02 KB** | 70.95 μs / 14.09 KB | 130.31 μs / 25.08 KB |
| `LEFT JOIN` | 29.02 μs / 6.66 KB | **21.10 μs / 2.22 KB** | 72.25 μs / 8.60 KB | 101.09 μs / 20.31 KB |
| Рекурсивный CTE | 41.55 μs / 13.79 KB | **26.24 μs / 1.53 KB** | 64.15 μs / 8.54 KB | — |
| Строковые функции | 16.45 μs / 4.48 KB | **15.49 μs / 1.28 KB** | 32.67 μs / 4.78 KB | 70.24 μs / 14.01 KB |
| UDF | 17.67 μs / 4.56 KB | **15.34 μs / 1.28 KB** | 50.80 μs / 6.39 KB | 71.68 μs / 13.27 KB |
| Оконные функции | 38.13 μs / 5.57 KB | **31.88 μs / 1.55 KB** | 64.67 μs / 5.82 KB | — |
<!-- benchmarks:category-b:end -->

В warm-пути Nextorm быстрее Dapper на `CASE WHEN` и `SELECT DISTINCT` и идёт наравне на `INTERSECT`;
отстаёт от Dapper примерно в 1.06–1.20 раза на строковых функциях, `EXCEPT`, UDF и оконных функциях,
в 1.38 раза на `LEFT JOIN` и в 1.56–1.82 раза на join по 4 таблицам, CTE, рекурсивном CTE и IN-list —
при этом заметно опережая обычные linq2db и EF Core по времени. Аллокации в warm-пути выше, чем в
prepared (ключ плана перестраивается на каждый вызов); они намного ниже, чем у обычных EF Core, и ниже
linq2db на большинстве фич, но выше linq2db на CTE, рекурсивном CTE, `EXCEPT` и `INTERSECT`.

> **IN-list, категория B.** Захваченная коллекция отключает кэш планов по замыслу (SQL перестраивается на
> каждый вызов), поэтому captured-варианты платят за полную сборку, а inline-варианты попадают в кэш. Четыре
> варианта Nextorm дают 37.5–41.4 µs — это фича, где кэшированный сырой SQL Dapper впереди с наибольшим
> отрывом.

## Сценарии tier 1 по форме запроса

Добавлены в #188, чтобы покрыть формы запросов, которых нет в двух таблицах выше: проекцию в свежий
DTO, SQL-агрегаты, сортированную пагинацию, стриминг (buffered/unbuffered DTO против
zero-materialization raw reader) и кросс-библиотечный вывод JSON/CSV. Данные: `large_table`
(10 000 строк) для projection/aggregates/paging/streaming; JSON-класс сеет собственную таблицу
`json_bench`, CSV-класс берёт срез `large_table` (10 000 строк). Инварианты честного сравнения остаются
`simple_entity`=10 и `complex_entity`=3 — «10 000 строк» выше относится к `large_table`, а не к этим
фикстурам. На **2026-11-07**.

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
| A_Nextorm_Prepared_ToListAsync | A | 4.284 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 5.531 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 6.327 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 7.435 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 4.370 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 6.597 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 9.956 ms | 3.62 MB |

### Агрегаты — SQL-side `Count` / `Sum` / `GroupBy → Count`

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

| Операция | Арм | Категория | Mean | Allocated |
|---|---|---:|---:|---:|
| Count | A_Nextorm_Prepared_Count | A | 14.65 μs | 408 B |
| Count | A_Linq2Db_Compiled_Count | A | 19.26 μs | 1,280 B |
| Count | A_EFCore_Compiled_Count | A | 40.92 μs | 7,744 B |
| Count | Dapper_Count | A+B | 18.23 μs | 944 B |
| Count | B_Nextorm_Cached_Count | B | 15.39 μs | 408 B |
| Count | B_Linq2Db_Count | B | 29.52 μs | 3,008 B |
| Count | B_EFCore_Count | B | 64.48 μs | 10,080 B |
| Sum | A_Nextorm_Prepared_Sum | A | 298.17 μs | 410 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 316.45 μs | 1,290 B |
| Sum | A_EFCore_Compiled_Sum | A | 334.54 μs | 7,770 B |
| Sum | Dapper_Sum | A+B | 317.79 μs | 954 B |
| Sum | B_Nextorm_Cached_Sum | B | 314.20 μs | 970 B |
| Sum | B_Linq2Db_Sum | B | 367.16 μs | 3,490 B |
| Sum | B_EFCore_Sum | B | 371.48 μs | 10,978 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,468.98 μs | 4,666 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,501.91 μs | 33,090 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,511.01 μs | 7,026 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,477.65 μs | 12,330 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,554.85 μs | 10,146 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,675.00 μs | 11,554 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,886.06 μs | 42,754 B |

### Пагинация — `OrderBy(Id).Offset(5000).Limit(100)`

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_PageAsync | A | 88.54 μs | 13.92 KB |
| A_Linq2Db_Compiled_Page | A | 112.67 μs | 17.61 KB |
| A_EFCore_Compiled_PageAsync | A | 133.15 μs | 44.81 KB |
| A_Dapper_PageAsync | A+B | 106.51 μs | 18.15 KB |
| B_Nextorm_Cached_PageAsync | B | 108.62 μs | 22.33 KB |
| B_Linq2Db_PageAsync | B | 139.58 μs | 22.65 KB |
| B_EFCore_PageAsync | B | 187.81 μs | 56.9 KB |

### Стриминг — buffered / unbuffered DTO и zero-materialization reader

Воспроизведение: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Буферизованный DTO:

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToList_Dto | A | 3,956.46 μs | 1,328.77 KB |
| Linq2Db_Compiled_ToList_Dto | A | 5,960.94 μs | 1,508.22 KB |
| A_EFCore_Compiled_ToList_Dto | A | 10,629.30 μs | 3,701.53 KB |
| Dapper_ToList_Dto | A+B | 8,223.40 μs | 1,742.27 KB |
| B_Nextorm_Cached_ToList_Dto | B | 3,954.38 μs | 1,332.18 KB |
| B_Linq2Db_ToList_Dto | B | 6,128.13 μs | 1,511.21 KB |
| B_EFCore_ToList_Dto | B | 9,005.49 μs | 3,705.72 KB |

Небуферизованное перечисление DTO:

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_AsyncStream_Dto | A | 4,585.96 μs | 1,250.84 KB |
| A_EFCore_Compiled_AsyncStream_Dto | A | 5,292.02 μs | 3,444.77 KB |
| Dapper_AsyncStream_Dto | A+B | 4,785.98 μs | 1,485.82 KB |
| B_Nextorm_Cached_AsyncStream_Dto | B | 4,255.35 μs | 1,254.42 KB |
| B_Linq2Db_AsyncStream_Dto | B | 4,982.80 μs | 1,254.55 KB |
| B_EFCore_AsyncStream_Dto | B | 5,295.63 μs | 3,448.89 KB |

Zero-materialization raw reader (синхронные; общий неудерживающий sink; EF Core исключён):

| Арм | Категория | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToDataReader | A | 4,453.12 μs | 948.45 KB |
| Dapper_ToDataReader | B | 3,954.36 μs | 943.1 KB |
| Linq2Db_ToDataReader | B | 4,006.42 μs | 943.33 KB |

### JSON / CSV кросс-библиотечно

Воспроизведение JSON: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md).
Воспроизведение CSV: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkCsv*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md).

JSON-отчёт покрывает `RowCount` 1 000 / 10 000 / 100 000; показан блок на 10 000 строк (Ratio — относительно
`ToList_Dto` в том же блоке). Конкурентные армы — материализация → ручной сериализатор.

| Метод (10 000 строк) | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| WriteJsonAsync_Array_Scalar | 5,452.9 μs | 0.88 | 9.98 KB |
| ToList_Dto | 6,196.4 μs | 1.00 | 785.02 KB |
| WriteJson_Array_Scalar | 6,294.1 μs | 1.02 | 9.83 KB |
| Nextorm_ToList_Dto_Json | 7,261.4 μs | 1.17 | 785.33 KB |
| WriteJson_Array_Dto | 8,302.0 μs | 1.34 | 403.24 KB |
| Linq2Db_ToList_Json | 8,793.1 μs | 1.42 | 963.73 KB |
| Dapper_ToList_Json | 9,459.1 μs | 1.53 | 1,273.77 KB |
| WriteJsonAsync_Array_ScalarPayload | 9,720.8 μs | 1.57 | 11,493.34 KB |
| WriteJson_Array_ScalarPayload | 10,431.7 μs | 1.68 | 11,493.19 KB |
| EFCore_ToList_Json | 10,952.7 μs | 1.77 | 3,158.63 KB |
| WriteJson_Array_WideDto | 12,280.9 μs | 1.98 | 11,496.04 KB |

CSV (срез `large_table`, 10 000 строк; Ratio — относительно `Nextorm_ToList`):

| Метод | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Nextorm_StringProjection_ToList | 2.799 ms | 0.34 | 1,019.05 KB |
| Nextorm_StringProjection_WriteCsv | 3.423 ms | 0.41 | 948.99 KB |
| Nextorm_ToList | 8.278 ms | 1.00 | 2,267.52 KB |
| Nextorm_ToList_ManualCsv | 9.806 ms | 1.18 | 4,141.82 KB |
| Nextorm_WriteCsv | 10.241 ms | 1.24 | 1,733.64 KB |
| Dapper_ToList_ManualCsv | 14.115 ms | 1.71 | 4,866.08 KB |
| Linq2Db_ToList_ManualCsv | 14.635 ms | 1.77 | 4,321.92 KB |
| EFCore_ToList_ManualCsv | 16.190 ms | 1.96 | 6,516.91 KB |

### Startup с NativeAOT (справочно, не бенчмарк запросов)

AOT переносит стоимость со старта на публикацию. На кейсе `oc-report` (SQLite 13 ГБ, окно 3 дня) один и
тот же отчёт идёт 0.42 s под JIT и 0.12 s нативным бинарником; EF Core 10 — 1.03 s → 0.23 s, Dapper —
0.26 s → 0.12 s. SQL и число строк одинаковы у всех трёх, а замер упирается в один scan
`session_message` — читайте его как стоимость старта, а не пропускную способность запросов. Только
nextorm публикуется с одним `PublishAot=true`; эталонное приложение —
[`examples/nextorm.examples.oc-report`](https://github.com/AlexeyShirshov/nextorm/tree/main/examples/nextorm.examples.oc-report),
см. [руководство по NativeAOT](../infrastructure/05-nativeaot.md).

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
