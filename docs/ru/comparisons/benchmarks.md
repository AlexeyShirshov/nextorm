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
`complex_entity`=3; 10 000 строк относятся к `large_table`, а не к этим фикстурам.

<!-- benchmarks:tier1-run-info:start -->
Снимок на **2026-11-16** (замеры **18:10:43–18:51:54 UTC**, по журналу прогона). Шесть отчётов ниже содержат
98 корректных строк. Весь прогон включал 118 сценариев, в том числе 20 дополнительных диагностических случаев
CSV, которые здесь не сравниваются между библиотеками. Машина: AMD Ryzen 7 5800HS with Radeon Graphics (8
логических / 4 физических ядер), SDK .NET 11.0.100, runtime .NET 11.0.0, BenchmarkDotNet 0.15.8. 89 строк
отчётов сняты с параметрами `Job.Default` (UnrollFactor=16); 9 строк raw-reader — со специализированными
InvocationCount=1 и UnrollFactor=1. Все строки использовали `InProcessEmitToolchain`. Источник: HEAD
`ad2d0daeb6e34bcc5b094f0c9b0b1d1205fb8545`; сборка использовала 10 локальных ссылок на проекты NextORM и
локальный source-generator analyzer; все 11 хеш-строк сборки/runtime совпали с этими выходными файлами.
Рабочее дерево исходников содержало незакоммиченные изменения.
<!-- benchmarks:tier1-run-info:end -->

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

<!-- benchmarks:tier1-caveats:start -->
> **Как читать результаты tier-1.** `Job.Default` с `InProcessEmitToolchain` — это запуск внутри процесса;
> различия меньше ~20% считайте шумом и проверяйте error, стандартное отклонение и аллокации в полном
> отчёте. В 9 замерах чтения через reader сработало предупреждение о слишком коротком времени итерации
> (рекомендация — не менее 100 мс); в 6 сценариях стриминга — предупреждение о мультимодальном
> распределении. Ошибочных случаев: 0.
<!-- benchmarks:tier1-caveats:end -->

### Проекция — `Select(x => new Dto { Id, Str })`

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

<!-- benchmarks:tier1-projection:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToListAsync | A | 3.923 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 4.849 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 5.431 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 6.637 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 3.970 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 5.779 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 8.510 ms | 3.62 MB |
<!-- benchmarks:tier1-projection:end -->

### Агрегаты — SQL-side `Count` / `Sum` / `GroupBy → Count`

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

<!-- benchmarks:tier1-aggregates:start -->
| Операция | Арм | Категория | Mean | Allocated |
| --- | --- | --- | ---: | ---: |
| Count | A_Nextorm_Prepared_Count | A | 15.11 μs | 400 B |
| Count | A_Linq2Db_Compiled_Count | A | 19.05 μs | 1,272 B |
| Count | A_EFCore_Compiled_Count | A | 36.69 μs | 7,296 B |
| Sum | A_Nextorm_Prepared_Sum | A | 274.59 μs | 403 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 297.32 μs | 1,283 B |
| Sum | A_EFCore_Compiled_Sum | A | 315.38 μs | 7,323 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,297.07 μs | 4,698 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,331.58 μs | 7,018 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,419.58 μs | 32,642 B |
| Count | Dapper_Count | A+B | 19.55 μs | 936 B |
| Sum | Dapper_Sum | A+B | 290.92 μs | 947 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,336.72 μs | 12,322 B |
| Count | B_Nextorm_Cached_Count | B | 15.99 μs | 400 B |
| Count | B_Linq2Db_Count | B | 24.88 μs | 2,848 B |
| Count | B_EFCore_Count | B | 49.82 μs | 9,616 B |
| Sum | B_Nextorm_Cached_Sum | B | 282.05 μs | 963 B |
| Sum | B_Linq2Db_Sum | B | 305.87 μs | 3,475 B |
| Sum | B_EFCore_Sum | B | 334.58 μs | 10,515 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,342.22 μs | 8,650 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,377.85 μs | 11,514 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,426.02 μs | 42,154 B |
<!-- benchmarks:tier1-aggregates:end -->

### Пагинация — `OrderBy(Id).Offset(5000).Limit(100)`

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

<!-- benchmarks:tier1-paging:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_PageAsync | A | 89.99 μs | 13.95 KB |
| A_Linq2Db_Compiled_Page | A | 107.29 μs | 17.6 KB |
| A_EFCore_Compiled_PageAsync | A | 128.99 μs | 44.37 KB |
| A_Dapper_PageAsync | A+B | 101.65 μs | 18.14 KB |
| B_Nextorm_Cached_PageAsync | B | 100.22 μs | 19.4 KB |
| B_Linq2Db_PageAsync | B | 135.85 μs | 22.71 KB |
| B_EFCore_PageAsync | B | 183.08 μs | 56.28 KB |
<!-- benchmarks:tier1-paging:end -->

### Стриминг — buffered / unbuffered DTO и zero-materialization reader

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Буферизованный DTO:

<!-- benchmarks:tier1-streaming-buffered:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToList_Dto | A | 4,119.13 μs | 1,360,688 B |
| Linq2Db_Compiled_ToList_Dto | A | 5,720.83 μs | 1,544,457 B |
| A_EFCore_Compiled_ToList_Dto | A | 8,678.39 μs | 3,789,917 B |
| Dapper_ToList_Dto | A+B | 6,793.55 μs | 1,784,191 B |
| B_Nextorm_Cached_ToList_Dto | B | 4,165.29 μs | 1,362,858 B |
| B_Linq2Db_ToList_Dto | B | 5,937.36 μs | 1,547,617 B |
| B_EFCore_ToList_Dto | B | 8,780.73 μs | 3,794,231 B |
<!-- benchmarks:tier1-streaming-buffered:end -->

Небуферизованное перечисление DTO:

<!-- benchmarks:tier1-streaming-unbuffered:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_AsyncStream_Dto | A | 4,115.10 μs | 1,280,890 B |
| A_EFCore_Compiled_AsyncStream_Dto | A | 5,106.04 μs | 3,527,002 B |
| Dapper_AsyncStream_Dto | A+B | 4,655.43 μs | 1,521,474 B |
| B_Nextorm_Cached_AsyncStream_Dto | B | 4,542.01 μs | 1,284,226 B |
| B_Linq2Db_AsyncStream_Dto | B | 4,660.90 μs | 1,284,642 B |
| B_EFCore_AsyncStream_Dto | B | 5,159.78 μs | 3,531,154 B |
<!-- benchmarks:tier1-streaming-unbuffered:end -->

Чтение через `DbDataReader` без материализации (синхронно; общий приёмник не сохраняет строки; EF Core
исключён):

<!-- benchmarks:tier1-streaming-readers:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToDataReader | A | 4,340.06 μs | 949.37 KB |
| Linq2Db_ToDataReader | B | 3,682.80 μs | 943.65 KB |
| Provider_Direct_ToDataReader | B | 3,708.38 μs | 943.39 KB |
| Dapper_ToDataReader | B | 3,770.33 μs | 943.42 KB |
| B_Nextorm_ToDataReader | B | 4,190.48 μs | 951.91 KB |
<!-- benchmarks:tier1-streaming-readers:end -->

Пустой `DbDataReader` служит контролем времени открытия и закрытия без чтения строк:

<!-- benchmarks:tier1-streaming-empty-readers:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| Provider_Direct_ToDataReader_Empty | B | 82.39 μs | 0.00 KB |
| Dapper_ToDataReader_Empty | B | 92.60 μs | 0.00 KB |
| Linq2Db_ToDataReader_Empty | B | 92.66 μs | 0.00 KB |
| B_Nextorm_ToDataReader_Empty | B | 225.84 μs | 16.80 KB |
<!-- benchmarks:tier1-streaming-empty-readers:end -->

### JSON / CSV кросс-библиотечно

Воспроизведение JSON: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md).
Воспроизведение CSV: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkCsv*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md).

JSON-отчёт покрывает `RowCount` 1 000 / 10 000 / 100 000; показан блок на 10 000 строк (Ratio — относительно
`ToList_Dto` в том же блоке). Конкурентные армы — материализация → ручной сериализатор.

<!-- benchmarks:tier1-write-json:start -->
| Метод (10 000 строк) | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| WriteJson_Array_Scalar | 4,780.7 μs | 0.74 | 9.76 KB |
| WriteJsonAsync_Array_Scalar | 5,113.1 μs | 0.79 | 9.92 KB |
| ToList_Dto | 6,446.5 μs | 1.00 | 783.76 KB |
| Nextorm_ToList_Dto_Json | 7,419.7 μs | 1.15 | 784.07 KB |
| WriteJson_Array_Dto | 7,880.6 μs | 1.22 | 403.18 KB |
| Linq2Db_ToList_Json | 8,379.2 μs | 1.30 | 963.72 KB |
| WriteJson_Array_ScalarPayload | 8,707.5 μs | 1.35 | 11493.13 KB |
| WriteJsonAsync_Array_ScalarPayload | 9,174.3 μs | 1.42 | 11493.28 KB |
| Dapper_ToList_Json | 9,493.9 μs | 1.47 | 1273.76 KB |
| EFCore_ToList_Json | 10,812.4 μs | 1.68 | 3158.23 KB |
| WriteJson_Array_WideDto | 10,993.8 μs | 1.71 | 11495.97 KB |
<!-- benchmarks:tier1-write-json:end -->

CSV (срез `large_table`, 10 000 строк; Ratio — относительно `Nextorm_ToList`):

<!-- benchmarks:tier1-csv:start -->
| Метод | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Nextorm_StringProjection_ToList | 2.763 ms | 0.33 | 1018.12 KB |
| Nextorm_StringProjection_WriteCsv | 3.469 ms | 0.42 | 949.1 KB |
| Nextorm_ToList | 8.336 ms | 1.00 | 2266.74 KB |
| Nextorm_ToList_ManualCsv | 9.694 ms | 1.16 | 4375.43 KB |
| Nextorm_WriteCsv | 10.095 ms | 1.21 | 1733.92 KB |
| Linq2Db_ToList_ManualCsv | 12.945 ms | 1.55 | 4087.58 KB |
| Dapper_ToList_ManualCsv | 13.250 ms | 1.59 | 5100.61 KB |
| EFCore_ToList_ManualCsv | 14.824 ms | 1.78 | 6751.01 KB |
<!-- benchmarks:tier1-csv:end -->

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
- Опубликованные результаты используют параметры `Job.Default` с `InProcessEmitToolchain`; запуск
  внутри процесса остаётся исследовательским режимом. Небольшие различия считайте шумом
  и перед выводами проверяйте полный отчёт: среднее, погрешность, стандартное отклонение и
  аллокации.
- Замеры охватывают одну машину, один провайдер (SQLite) и фиксированный набор сценариев. Они не
  описывают любую нагрузку — повторите их на своих данных и оборудовании.

## Воспроизведение

```bash
export NEXTORM_BENCH_FULL=1

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
`benchmarks/BenchmarkDotNet.Artifacts/results/`. Переменная `NEXTORM_BENCH_FULL=1` выбирает параметры
`Job.Default` с `InProcessEmitToolchain`; этот режим работает внутри процесса, а не out-of-process.

Таблицы сквозных сценариев, аллокаций, категорий A/B и Tier-1 на этой странице **генерируются** из отчётов —
не правьте их вручную. После прогона:

```bash
python3 tools/benchmarks/update_benchmark_tables.py          # переписать readme.md и обе языковые версии статьи
python3 tools/benchmarks/update_benchmark_tables.py --check  # режим CI: упасть, если таблица устарела
```

Tier-1 использует отчёты Projection, Aggregates, Paging, Streaming, WriteJson, Csv и связанный
`tier1-run.json` в `benchmarks/BenchmarkDotNet.Artifacts/results/`. Текущий workflow обновляет сквозные
сценарии и категории A/B; Tier-1 обновляется после отдельного прогона этих классов и проверки sidecar с
датой и хешами отчётов.

Тот же процесс обновления сквозных таблиц запускается из `.github/workflows/benchmarks.yml` — автоматически
после каждого мержа в `main`, затрагивающего движок (`src/**`) или проект бенчмарков, и вручную из вкладки
Actions. Workflow открывает pull request с новыми отчётами и таблицами.

## См. также

- [Возможности](capabilities.md) — что поддерживает каждая библиотека.
- [Переиспользование запросов: кэш и Prepare](../engine/03-plan-cache-and-reuse.md).
