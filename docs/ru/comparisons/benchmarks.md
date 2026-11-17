# Бенчмарки: Nextorm против Dapper, linq2db и EF Core

Используйте результаты, чтобы сравнить указанные формы запросов в описанной конфигурации SQLite. Это
данные для этих сценариев, а не прогноз для любого провайдера, нагрузки или компьютера.

<!-- benchmarks:conditions:start -->
Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; в ячейке — **самый
быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. Машина: AMD Ryzen 7 5800HS with Radeon
Graphics (8 логических / 4 физических ядер), .NET 11.0.0, BenchmarkDotNet 0.15.8 (`Job.Default` +
`InProcessEmitToolchain`). На **2026-11-16** для таблиц сквозных сценариев, аллокаций и категории B (таблица
категории A и раздел tier-1 сохраняют дату своего прогона). В каждой строке лидирующая ячейка выделена
**полужирным** — быстрейшая в таблицах времени и с наименьшей аллокацией в таблице аллокаций.
<!-- benchmarks:conditions:end -->

> **Сравнение по возможностям:** результаты сгруппированы по тирам, без веток `Prepare()`: одна
> таблица
> на tier, библиотеки расположены в столбцах. См. [Feature-бенчмарки](feature-benchmarks.md).

## Сквозные сценарии

<!-- benchmarks:e2e:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **12.26 μs** | 16.55 μs | 17.17 μs | 38.08 μs |
| [Широкая выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **7.921 ms** | 8.528 ms | 8.647 ms | 9.152 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **1.063 ms** | 1.503 ms | 1.553 ms | 3.386 ms |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.893 ms** | 24.616 ms | 364.543 ms | 108.639 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **1.059 ms** | 1.509 ms | 1.663 ms | 3.660 ms |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **107.1 μs** | 154.6 μs | 366.3 μs | 355.7 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **107.2 μs** | 148.6 μs | 370.8 μs | 337.2 μs |
<!-- benchmarks:e2e:end -->

### Аллокации

Аллокации для того же самого быстрого метода каждой библиотеки (колонка BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Выборка данных | **1,080 B** | 1,848 B | 2,080 B | 9,776 B |
| Широкая выборка данных | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **102.90 KB** | 179.23 KB | 252.74 KB | 745.41 KB |
| Simulate work | **6.21 MB** | 8.86 MB | 108.02 MB | 13.57 MB |
| Any | **84.38 KB** | 138.29 KB | 232.82 KB | 763.30 KB |
| FirstOrDefault | **8.56 KB** | 16.12 KB | 159.77 KB | 76.03 KB |
| SingleOrDefault | **8.28 KB** | 16.04 KB | 159.77 KB | 76.03 KB |
<!-- benchmarks:allocations:end -->

## Сравнение по фичам (честное)

Сквозная таблица показывает самый быстрый метод каждой библиотеки, но методы используют разные
стратегии переиспользования запросов. Поэтому результаты по отдельным возможностям разделены на две
категории:

- **Категория A — prepared / compiled / raw:** Nextorm через `Prepare()` сравнивается с ближайшей
  *скомпилированной* формой того же запроса (`EF.CompileAsyncQuery`,
  `LinqToDB.CompiledQuery.Compile`) и с сырым Dapper, чей IL кэшируется по тексту SQL. Это сравнение
  повторно используемых форм запроса: compiled против compiled, а для Dapper — единственный
  доступный ему raw-SQL-путь.
- **Категория B — warm cached против обычных:** Nextorm использует неявный кэш планов. Каждый вызов
  создаёт новый fluent-запрос с константами, поэтому повторно используется только закэшированный
  план. Сравнение идёт с обычными (не compiled) EF Core и linq2db, а также с сырым Dapper.

Каждый метод для отдельной возможности выполняет **10 запросов**. В таблицах указаны среднее время и
выделенная память на один запрос: `Mean` и `Allocated` BenchmarkDotNet делятся на 10. Исходные
отчёты:
[Категория A](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) ·
[Категория B](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md).

### Категория A — prepared против compiled и raw

<!-- benchmarks:category-a:start -->
На **2026-11-16**.

| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **10.79 μs / 664 B** | 15.22 μs / 1.34 KB | 16.10 μs / 1.59 KB | 35.25 μs / 7.46 KB |
| CTE | **11.35 μs / 752 B** | 23.01 μs / 1.56 KB | 23.50 μs / 1.77 KB | — |
| `SELECT DISTINCT` | **12.66 μs / 665 B** | 18.81 μs / 1.44 KB | 18.85 μs / 1.59 KB | 40.43 μs / 7.23 KB |
| `EXCEPT` | **12.56 μs / 680 B** | 18.52 μs / 1.43 KB | 21.50 μs / 1.67 KB | 42.52 μs / 8.38 KB |
| IN-list | **14.11 μs / 1.55 KB** | 19.66 μs / 1.26 KB | 45.78 μs / 13.63 KB | 62.91 μs / 14.34 KB |
| `INTERSECT` | **12.13 μs / 665 B** | 17.51 μs / 1.28 KB | 20.79 μs / 1.62 KB | 42.32 μs / 7.50 KB |
| Join по 4 таблицам | **14.58 μs / 1.03 KB** | 27.04 μs / 2.02 KB | 24.96 μs / 2.06 KB | 48.61 μs / 8.05 KB |
| `LEFT JOIN` | **14.15 μs / 1.16 KB** | 21.17 μs / 2.21 KB | 21.46 μs / 2.31 KB | 41.92 μs / 9.48 KB |
| Рекурсивный CTE | **14.06 μs / 672 B** | 25.65 μs / 1.52 KB | 26.92 μs / 1.77 KB | — |
| Строковые функции | **10.81 μs / 656 B** | 15.30 μs / 1.27 KB | 15.85 μs / 1.63 KB | 34.86 μs / 7.05 KB |
| UDF | **10.84 μs / 688 B** | 15.78 μs / 1.27 KB | 15.53 μs / 1.63 KB | 34.45 μs / 7.05 KB |
| Оконные функции | **15.75 μs / 793 B** | 30.19 μs / 1.54 KB | 32.76 μs / 1.74 KB | — |
<!-- benchmarks:category-a:end -->

В подготовленном варианте Nextorm быстрее **на каждой** проверенной возможности и создаёт меньше
аллокаций в 11 из 12 случаев — исключение IN-list, где
кэшированный raw SQL Dapper экономнее. Точные соотношения — в таблице выше; они зависят от раннера, поэтому
дельты меньше ~20 % считайте шумом.

### Категория B — warm cached против обычных

<!-- benchmarks:category-b:start -->
| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **13.63 μs / 2.06 KB** | 15.59 μs / 1.34 KB | 25.31 μs / 3.77 KB | 57.41 μs / 11.46 KB |
| CTE | 32.56 μs / 9.49 KB | **22.91 μs / 1.56 KB** | 56.53 μs / 9.37 KB | — |
| `SELECT DISTINCT` | **17.52 μs / 3.06 KB** | 18.67 μs / 1.44 KB | 30.84 μs / 3.88 KB | 63.18 μs / 11.70 KB |
| `EXCEPT` | **18.34 μs / 4.23 KB** | 18.92 μs / 1.43 KB | 38.30 μs / 5.01 KB | 71.39 μs / 14.73 KB |
| IN-list | 43.73 μs / 8.77 KB | **19.82 μs / 1.26 KB** | 68.29 μs / 17.28 KB | 102.95 μs / 22.05 KB |
| `INTERSECT` | **17.51 μs / 4.22 KB** | 18.01 μs / 1.28 KB | 39.05 μs / 4.96 KB | 70.88 μs / 13.81 KB |
| Join по 4 таблицам | 35.68 μs / 10.02 KB | **27.48 μs / 2.02 KB** | 65.00 μs / 14.14 KB | 114.91 μs / 23.90 KB |
| `LEFT JOIN` | 26.32 μs / 5.74 KB | **21.79 μs / 2.21 KB** | 69.49 μs / 8.94 KB | 98.61 μs / 19.77 KB |
| Рекурсивный CTE | 36.53 μs / 10.10 KB | **26.70 μs / 1.52 KB** | 63.08 μs / 8.43 KB | — |
| Строковые функции | **15.34 μs / 2.81 KB** | 16.34 μs / 1.27 KB | 31.89 μs / 4.74 KB | 66.92 μs / 12.74 KB |
| UDF | **15.71 μs / 2.90 KB** | 16.26 μs / 1.27 KB | 50.07 μs / 6.75 KB | 66.85 μs / 12.74 KB |
| Оконные функции | **31.01 μs / 4.09 KB** | 31.99 μs / 1.53 KB | 61.46 μs / 5.79 KB | — |
<!-- benchmarks:category-b:end -->

При повторных запросах с кэшем Nextorm быстрее Dapper на `CASE WHEN` и `SELECT DISTINCT` и идёт
наравне на `INTERSECT`. Он отстаёт от Dapper примерно в 1.06–1.20 раза на строковых функциях,
`EXCEPT`, UDF и оконных функциях; в 1.38 раза на `LEFT JOIN`; в 1.56–1.82 раза на join по четырём
таблицам, CTE, рекурсивном CTE и IN-list. По времени Nextorm опережает обычные linq2db и EF Core.

При повторных запросах с кэшем аллокаций больше, чем в подготовленном режиме, потому что ключ плана
строится заново при каждом вызове. Их значительно меньше, чем у обычного EF Core, и меньше, чем у
linq2db на большинстве проверенных возможностей, но больше на CTE, рекурсивном CTE, `EXCEPT` и
`INTERSECT`.

> **IN-list, категория B.** Захваченная коллекция намеренно отключает кэш планов, потому что SQL нужно перестраивать при
> каждом вызове; варианты с inline-списком используют кэш. Четыре варианта Nextorm дают 37.5–41.4 µs.
> В этом сценарии кэшированный сырой SQL Dapper опережает Nextorm с наибольшим отрывом.

## Сценарии tier 1 по форме запроса

В #188 добавили формы запросов, которых нет в двух предыдущих таблицах: проекцию в новый DTO (объект
передачи данных), агрегаты на стороне SQL, упорядоченную пагинацию, потоковое чтение DTO с
буферизацией и без неё, чтение через `DbDataReader` без создания объектов, а также вывод JSON/CSV в
разных библиотеках.

Для проекции, агрегатов, пагинации и потокового чтения используется `large_table` (10 000 строк).
JSON-класс заполняет собственную таблицу `json_bench`, а CSV-класс берёт срез `large_table` (10 000
строк). Инварианты фикстур честного сравнения остаются прежними: `simple_entity`=10 и
`complex_entity`=3; 10 000 строк относятся к `large_table`, а не к этим фикстурам. Снимок на
**2026-11-07**.

Категории переиспользования те же, что в сравнении возможностей: **категория A** сравнивает Nextorm
`Prepare()` с ближайшей *скомпилированной* формой (`EF.CompileAsyncQuery`,
`LinqToDB.CompiledQuery.Compile`) и сырым SQL Dapper; **категория B** — неявный кэш планов Nextorm с
обычными, не скомпилированными вызовами EF Core и linq2db, а также с сырым SQL Dapper. В этом
сравнении у Dapper нет отдельного скомпилированного варианта запроса или чтения, поэтому его ветки с
сырым SQL входят в обе категории. В сценарии чтения Dapper использует API `ExecuteReader`.

**У потокового чтения три группы:** буферизованная материализация DTO, небуферизованное перечисление
DTO и чтение через `DbDataReader` **без создания объектов**. Последняя группа только синхронная;
асинхронные варианты чтения отложены. Nextorm `ToDataReader`, Dapper `ExecuteReader` и linq2db
`ExecuteReader` (его `DataReaderAsync.Reader`) читают одну проекцию `id, someString` через **один
общий приёмник, который не сохраняет прочитанные строки**. EF Core в эту группу **не** входит: у
него нет аналога LINQ для чтения через `DbDataReader`. Здесь «без материализации» означает, что не
создаются объекты entity/DTO, а **не** отсутствие аллокаций: при каждом вызове всё равно выделяются
память под команду и объект `DbDataReader`.

Для JSON/CSV конкурентные армы — **ближайший эквивалент**: у Dapper, linq2db и EF Core нет терминала
вывода без материализации, поэтому они сначала материализуют строки, а затем сериализуют их вручную.
Это не сравнение без материализации.

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

Чтение через `DbDataReader` без материализации (синхронно; общий приёмник не сохраняет строки; EF Core
исключён):

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

- Все участники используют один ADO.NET-провайдер (`Microsoft.Data.Sqlite`), что позволяет
  сравнивать библиотеки при одинаковом провайдере. База SQLite находится в tmpfs, поэтому дисковый
  ввод-вывод не маскирует накладные расходы ORM.
- Для Nextorm показаны оба пути переиспользования — неявный кэш планов и явный `Prepare()`. Для
  Nextorm в сквозной таблице указан самый быстрый из двух путей переиспользования. См.
  [Переиспользование запросов: кэш и Prepare](../engine/03-plan-cache-and-reuse.md).
- `ShortRun` + `InProcessEmitToolchain` — исследовательский режим. Небольшие различия считайте шумом
  и перед выводами проверяйте полный отчёт: среднее, погрешность, стандартное отклонение и
  аллокации.
- Замеры охватывают одну машину, один провайдер (SQLite) и фиксированный набор сценариев. Они не
  описывают любую нагрузку — повторите их на своих данных и оборудовании.

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
