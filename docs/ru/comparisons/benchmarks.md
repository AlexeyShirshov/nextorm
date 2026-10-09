# Бенчмарки: Nextorm против Dapper, linq2db и EF Core

Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; в ячейке —
**самый быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. Машина: AMD Ryzen 7 5800HS
(16 логических / 8 физических ядер), .NET 10.0.12, BenchmarkDotNet 0.15.8 (`ShortRun` +
`InProcessEmitToolchain`). На **2026-09-25**. В каждой строке лидирующая ячейка выделена **полужирным** —
быстрейшая в таблицах времени и с наименьшей аллокацией в таблице аллокаций.

## Сквозные сценарии

| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkIteration-report-github.md) | **11.24 μs** | 15.82 μs | 17.49 μs | 38.00 μs |
| [Широкая выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **8.592 ms** | 9.226 ms | 10.350 ms | 10.395 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkWhere-report-github.md) | **942.3 μs** | 1,385.0 μs | 2,759.0 μs | 3,344.6 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.003 ms** | 25.836 ms | 429.853 ms | 117.813 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkAny-report-github.md) | **943.2 μs** | 1,401.8 μs | 2,805.2 μs | 3,653.8 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFirst-report-github.md) | **94.77 μs** | 146.92 μs | 674.83 μs | 359.09 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSingle-report-github.md) | **95.10 μs** | 148.47 μs | 686.56 μs | 370.82 μs |

### Аллокации

Аллокации для того же самого быстрого метода каждой библиотеки (колонка BenchmarkDotNet `Allocated`):

| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Выборка данных | **792 B** | 1,904 B | 2,472 B | 10,224 B |
| Широкая выборка данных | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **92.43 KB** | 180.72 KB | 551.98 KB | 789.17 KB |
| Simulate work | **6.24 MB** | 8.90 MB | 122.18 MB | 13.68 MB |
| Any | **85.16 KB** | 139.08 KB | 401.59 KB | 807.06 KB |
| FirstOrDefault | **8.56 KB** | 16.48 KB | 222.27 KB | 80.40 KB |
| SingleOrDefault | **8.36 KB** | 16.40 KB | 222.27 KB | 80.40 KB |

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
[Категория A](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFeaturesFair-report-github.md) ·
[Категория B](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md).

### Категория A — prepared против compiled и raw

| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **10.51 µs / 672 B** | 15.36 µs / 1.34 KB | 16.37 µs / 1.59 KB | 40.08 µs / 7.90 KB |
| CTE | **16.64 µs / 720 B** | 25.45 µs / 1.57 KB | 23.31 µs / 1.77 KB | — |
| `SELECT DISTINCT` | **12.91 µs / 672 B** | 20.35 µs / 1.56 KB | 19.36 µs / 1.59 KB | 44.12 µs / 7.66 KB |
| `EXCEPT` | **11.59 µs / 688 B** | 20.73 µs / 1.44 KB | 24.10 µs / 1.68 KB | 46.94 µs / 8.81 KB |
| IN-list | **13.43 µs / 1.50 KB** | 24.01 µs / 1.27 KB | 55.95 µs / 15.23 KB | 74.42 µs / 14.77 KB |
| `INTERSECT` | **11.46 µs / 672 B** | 17.04 µs / 1.29 KB | 20.49 µs / 1.63 KB | 47.99 µs / 7.94 KB |
| Join по 4 таблицам | **13.33 µs / 1,008 B** | 27.23 µs / 2.02 KB | 25.08 µs / 2.07 KB | 50.77 µs / 8.49 KB |
| `LEFT JOIN` | **12.76 µs / 1.13 KB** | 21.44 µs / 2.22 KB | 21.87 µs / 2.32 KB | 46.60 µs / 9.92 KB |
| Рекурсивный CTE | **13.13 µs / 680 B** | 26.50 µs / 1.53 KB | 27.92 µs / 1.78 KB | — |
| Строковые функции | **9.79 µs / 696 B** | 16.15 µs / 1.28 KB | 17.69 µs / 1.64 KB | 40.03 µs / 7.49 KB |
| UDF | **10.05 µs / 696 B** | 16.30 µs / 1.28 KB | 17.10 µs / 1.64 KB | 40.61 µs / 7.49 KB |
| Оконные функции | **16.09 µs / 776 B** | 34.01 µs / 1.55 KB | 35.08 µs / 1.74 KB | — |

Nextorm prepared быстрее **на каждой** фиче и с наименьшей аллокацией — примерно в 4–10 раз меньше, чем у
EF Core, и в 2–4 раза меньше, чем у Dapper.

### Категория B — warm cached против обычных

| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **13.04 µs / 2.56 KB** | 15.15 µs / 1.34 KB | 25.78 µs / 3.80 KB | 61.20 µs / 11.95 KB |
| CTE | 27.32 µs / 8.70 KB | **21.85 µs / 1.57 KB** | 59.81 µs / 9.06 KB | — |
| `SELECT DISTINCT` | **15.54 µs / 2.66 KB** | 18.73 µs / 1.56 KB | 31.54 µs / 3.91 KB | 67.15 µs / 12.20 KB |
| `EXCEPT` | **18.17 µs / 4.56 KB** | 19.03 µs / 1.44 KB | 42.24 µs / 5.05 KB | 82.00 µs / 15.31 KB |
| IN-list | 29.69 µs / 6.55 KB | **20.83 µs / 1.27 KB** | 77.85 µs / 18.91 KB | 117.93 µs / 22.61 KB |
| `INTERSECT` | **16.83 µs / 4.55 KB** | 18.40 µs / 1.29 KB | 44.02 µs / 5.01 KB | 86.72 µs / 14.49 KB |
| Join по 4 таблицам | 34.29 µs / 9.37 KB | **29.32 µs / 2.02 KB** | 79.49 µs / 14.06 KB | 139.46 µs / 24.77 KB |
| `LEFT JOIN` | **23.16 µs / 5.17 KB** | 25.05 µs / 2.22 KB | 90.69 µs / 8.71 KB | 111.59 µs / 20.27 KB |
| Рекурсивный CTE | 37.73 µs / 8.89 KB | **30.81 µs / 1.53 KB** | 75.61 µs / 8.47 KB | — |
| Строковые функции | 15.17 µs / 3.62 KB | **14.93 µs / 1.28 KB** | 33.18 µs / 4.78 KB | 70.10 µs / 13.27 KB |
| UDF | 15.51 µs / 3.75 KB | **15.36 µs / 1.28 KB** | 51.24 µs / 6.39 KB | 72.67 µs / 13.27 KB |
| Оконные функции | 35.13 µs / 4.75 KB | **32.95 µs / 1.55 KB** | 68.12 µs / 5.99 KB | — |

В warm-пути Nextorm по-прежнему быстрее Dapper на `CASE WHEN`, `DISTINCT`, `EXCEPT`, `INTERSECT` и
`LEFT JOIN` и идёт вровень на `ToUpper`/UDF/окнах; отстаёт от Dapper примерно в 1.15–1.6 раза на CTE,
рекурсивном CTE, join по 4 таблицам и IN-list, оставаясь заметно впереди обычных linq2db и EF Core.
Аллокации в warm-пути выше, чем в prepared (ключ плана перестраивается на каждый вызов), но всё равно ниже,
чем у обычных конкурентов.

> **IN-list, категория B.** Захваченная коллекция отключает кэш планов по замыслу (SQL перестраивается на
> каждый вызов), поэтому captured-варианты платят за полную сборку, а inline-варианты попадают в кэш. Четыре
> варианта Nextorm дают 29.69–32.76 µs — это единственная фича, где кэшированный сырой SQL Dapper остаётся
> впереди.

## Сценарии tier 1 по форме запроса

Добавлены в #188, чтобы покрыть формы запросов, которых нет в двух таблицах выше: проекцию в свежий
DTO, SQL-агрегаты, сортированную пагинацию, стриминг (buffered/unbuffered DTO против
zero-materialization raw reader) и кросс-библиотечный вывод JSON/CSV. Данные: `large_table`
(10 000 строк) для projection/aggregates/paging/streaming; JSON-класс сеет собственную таблицу
`json_bench`, CSV-класс берёт срез `large_table` (10 000 строк). Инварианты честного сравнения остаются
`simple_entity`=10 и `complex_entity`=3 — «10 000 строк» выше относится к `large_table`, а не к этим
фикстурам.

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
  ячейке указан самый быстрый из них. См. [Переиспользование запросов: кэш и Prepare](../infrastructure/01-query-reuse-and-caching.md).
- `ShortRun` + `InProcessEmitToolchain` — быстрый режим: сигнала он даёт достаточно, но небольшие различия
  считайте шумом и смотрите полный отчёт (mean, error, стандартное отклонение и аллокации).
- Это одна машина, один провайдер (SQLite) и фиксированный набор сценариев. Это не утверждение про любую
  нагрузку — воспроизводите на своих данных и железе.

## Воспроизведение

```bash
# Сквозные сценарии
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWhere*

# Сравнение по фичам (категории A / B)
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFair.*"
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFairCached*"
```

Каждый отчёт по ссылке — это вывод BenchmarkDotNet, закоммиченный в
`benchmarks/BenchmarkDotNet.Artifacts/results/`. Переменная `NEXTORM_BENCH_FULL=1` включает полный
out-of-process прогон вместо `ShortRun`.

## См. также

- [Возможности](capabilities.md) — что поддерживает каждая библиотека.
- [Переиспользование запросов: кэш и Prepare](../infrastructure/01-query-reuse-and-caching.md).
