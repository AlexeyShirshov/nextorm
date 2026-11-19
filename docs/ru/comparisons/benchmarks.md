# Бенчмарки: Nextorm против Dapper, linq2db и EF Core

Используйте результаты, чтобы сравнить указанные формы запросов в описанной конфигурации SQLite. Это
данные для этих сценариев, а не прогноз для любого провайдера, нагрузки или компьютера.

<!-- benchmarks:conditions:start -->
Условия: провайдер SQLite (`Microsoft.Data.Sqlite`) у всех участников; асинхронные запросы; в ячейке — **самый
быстрый метод** этой библиотеки в связанном отчёте BenchmarkDotNet. Машина: AMD EPYC 9V45 (4 логических / 2
физических ядер), .NET 11.0.0, BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`). На
**2026-11-18** для таблиц сквозных сценариев и аллокаций; категория B запущена **2026-11-18**. Категория A и
раздел tier-1 сохраняют даты своих прогонов. В каждой строке лидирующая ячейка выделена **полужирным** —
быстрейшая в таблицах времени и с наименьшей аллокацией в таблице аллокаций.
<!-- benchmarks:conditions:end -->

> **Сравнение по возможностям:** результаты сгруппированы по тирам, без веток `Prepare()`: одна
> таблица
> на tier, библиотеки расположены в столбцах. См. [Feature-бенчмарки](feature-benchmarks.md).

## Сквозные сценарии

<!-- benchmarks:e2e:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **8.248 μs** | 10.785 μs | 11.084 μs | 21.973 μs |
| [Широкая выборка данных](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **6.632 ms** | 7.467 ms | 7.585 ms | 7.659 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **705.0 μs** | 896.1 μs | 926.8 μs | 1,924.2 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **5.654 ms** | 14.806 ms | 107.557 ms | 59.253 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **728.6 μs** | 921.6 μs | 968.5 μs | 1,950.4 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **74.11 μs** | 94.52 μs | 150.02 μs | 200.65 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **72.72 μs** | 90.03 μs | 150.76 μs | 186.52 μs |
<!-- benchmarks:e2e:end -->

### Аллокации

Аллокации для того же самого быстрого метода каждой библиотеки (колонка BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Метод | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Выборка данных | **968 B** | 1,896 B | 2,080 B | 9,776 B |
| Широкая выборка данных | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **102.58 KB** | 179.22 KB | 252.74 KB | 745.39 KB |
| Simulate work | **6.21 MB** | 8.86 MB | 107.44 MB | 13.56 MB |
| Any | **84.38 KB** | 138.29 KB | 232.81 KB | 763.28 KB |
| FirstOrDefault | **8.55 KB** | 16.12 KB | 159.77 KB | 76.02 KB |
| SingleOrDefault | **8.28 KB** | 16.04 KB | 159.77 KB | 76.02 KB |
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
На **2026-11-18**.

| Фича | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| CASE WHEN | **7.48 μs / 664 B** | 9.49 μs / 1.34 KB | 9.76 μs / 1.59 KB | 19.71 μs / 7.46 KB |
| CTE | **7.79 μs / 752 B** | 12.33 μs / 1.56 KB | 12.39 μs / 1.77 KB | — |
| SELECT DISTINCT | **8.32 μs / 664 B** | 10.24 μs / 1.44 KB | 10.58 μs / 1.59 KB | 20.71 μs / 7.23 KB |
| EXCEPT | **8.80 μs / 680 B** | 11.71 μs / 1.43 KB | 12.65 μs / 1.67 KB | 23.45 μs / 8.38 KB |
| IN-list | **9.09 μs / 1.55 KB** | 10.77 μs / 1.26 KB | 16.12 μs / 13.63 KB | 24.12 μs / 14.34 KB |
| INTERSECT | **7.94 μs / 665 B** | 10.45 μs / 1.28 KB | 11.71 μs / 1.62 KB | 21.08 μs / 7.50 KB |
| Join по 4 таблицам | **10.01 μs / 1.03 KB** | 16.03 μs / 2.02 KB | 14.64 μs / 2.06 KB | 25.06 μs / 8.05 KB |
| LEFT JOIN | **9.98 μs / 1.16 KB** | 13.03 μs / 2.21 KB | 13.02 μs / 2.31 KB | 22.16 μs / 9.48 KB |
| Рекурсивный CTE | **9.08 μs / 672 B** | 13.44 μs / 1.52 KB | 13.98 μs / 1.77 KB | — |
| Строковые функции | **7.13 μs / 656 B** | 9.23 μs / 1.27 KB | 9.27 μs / 1.63 KB | 18.23 μs / 7.05 KB |
| UDF | **7.22 μs / 688 B** | 9.34 μs / 1.27 KB | 9.55 μs / 1.63 KB | 18.57 μs / 7.05 KB |
| Оконные функции | **10.23 μs / 793 B** | 14.65 μs / 1.54 KB | 14.60 μs / 1.73 KB | — |
<!-- benchmarks:category-a:end -->

В подготовленном варианте Nextorm быстрее **на каждой** проверенной возможности и создаёт меньше
аллокаций в 11 из 12 случаев — исключение IN-list, где
кэшированный raw SQL Dapper экономнее. Точные соотношения — в таблице выше; они зависят от раннера, поэтому
дельты меньше ~20 % считайте шумом.

### Категория B — warm cached против обычных

<!-- benchmarks:category-b:start -->
| Фича | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| CASE WHEN | **8.82 μs / 2.06 KB** | 9.65 μs / 1.34 KB | 11.59 μs / 3.77 KB | 21.98 μs / 11.46 KB |
| CTE | 14.28 μs / 9.26 KB | **12.34 μs / 1.56 KB** | 19.76 μs / 9.31 KB | — |
| SELECT DISTINCT | **9.69 μs / 3.03 KB** | 10.30 μs / 1.44 KB | 12.23 μs / 3.88 KB | 22.80 μs / 11.70 KB |
| EXCEPT | **10.72 μs / 4.30 KB** | 11.22 μs / 1.43 KB | 15.59 μs / 5.01 KB | 26.55 μs / 14.92 KB |
| IN-list | 13.43 μs / 7.04 KB | **10.39 μs / 1.26 KB** | 18.55 μs / 17.28 KB | 29.03 μs / 22.20 KB |
| INTERSECT | **10.07 μs / 4.28 KB** | 10.36 μs / 1.28 KB | 14.61 μs / 4.96 KB | 25.82 μs / 13.99 KB |
| Join по 4 таблицам | 16.44 μs / 10.05 KB | **15.73 μs / 2.02 KB** | 24.59 μs / 14.06 KB | 40.00 μs / 23.94 KB |
| LEFT JOIN | 13.27 μs / 5.64 KB | **12.63 μs / 2.21 KB** | 22.28 μs / 8.94 KB | 31.47 μs / 19.88 KB |
| Рекурсивный CTE | 14.49 μs / 10.09 KB | **13.30 μs / 1.52 KB** | 19.94 μs / 8.45 KB | — |
| Строковые функции | **8.58 μs / 2.81 KB** | 8.72 μs / 1.27 KB | 12.23 μs / 4.89 KB | 21.87 μs / 12.74 KB |
| UDF | **9.03 μs / 2.90 KB** | 9.09 μs / 1.27 KB | 15.92 μs / 6.63 KB | 23.31 μs / 12.74 KB |
| Оконные функции | **13.91 μs / 4.19 KB** | 14.43 μs / 1.54 KB | 18.98 μs / 5.79 KB | — |
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

В этой таблице сравниваем другие формы запросов: проекцию в новый DTO (объект
передачи данных), агрегаты на стороне SQL, упорядоченную пагинацию, потоковое чтение DTO с
буферизацией и без неё, чтение через `DbDataReader` без создания объектов, а также вывод JSON/CSV в
разных библиотеках.

Для проекции, агрегатов, пагинации и потокового чтения используется `large_table` (10 000 строк).
JSON-класс заполняет собственную таблицу `json_bench`, а CSV-класс берёт срез `large_table` (10 000
строк). Инварианты фикстур честного сравнения остаются прежними: `simple_entity`=10 и
`complex_entity`=3; 10 000 строк относятся к `large_table`, а не к этим фикстурам.

<!-- benchmarks:tier1-run-info:start -->
Снимок на **2026-11-17–2026-11-18** (замеры **2026-11-17 18:38:46–2026-11-18 02:56:28 UTC**, по журналу
прогона). Шесть отчётов ниже содержат 98 корректных строк. Весь прогон включал 118 сценариев, в том числе 20
дополнительных диагностических случаев CSV, которые здесь не сравниваются между библиотеками. Машина: AMD
Ryzen 7 5800HS with Radeon Graphics (8 логических / 4 физических ядер), SDK .NET 11.0.100, runtime .NET
11.0.0, BenchmarkDotNet 0.15.8. 89 строк отчётов сняты с параметрами `Job.Default` (UnrollFactor=16); 9 строк
raw-reader — со специализированными InvocationCount=1 и UnrollFactor=1. Все строки использовали
`InProcessEmitToolchain`. Источник: HEAD `9226f0415d96eb1c4364c81ac3c4acaa8f9db6a8`; сборка использовала 10
локальных ссылок на проекты NextORM и локальный source-generator analyzer; все 11 хеш-строк сборки/runtime
совпали с этими выходными файлами. Рабочее дерево исходников было чистым.
<!-- benchmarks:tier1-run-info:end -->

Категории переиспользования те же, что в сравнении возможностей: **категория A** сравнивает Nextorm
`Prepare()` с ближайшей *скомпилированной* формой (`EF.CompileAsyncQuery`,
`LinqToDB.CompiledQuery.Compile`) и сырым SQL Dapper; **категория B** — неявный кэш планов Nextorm с
обычными, не скомпилированными вызовами EF Core и linq2db, а также с сырым SQL Dapper. В этом
сравнении у Dapper нет отдельного скомпилированного варианта запроса или чтения, поэтому его ветки с
сырым SQL входят в обе категории. В сценарии чтения Dapper использует API `ExecuteReader`.

**У потокового чтения три группы:** буферизованная материализация DTO, небуферизованное перечисление
DTO и чтение через `DbDataReader` **без создания объектов**. Последняя группа только синхронная;
асинхронное чтение не измеряется. Nextorm `ToDataReader`, Dapper `ExecuteReader` и linq2db
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
> (рекомендация — не менее 100 мс); в 5 сценариях стриминга — предупреждение о мультимодальном
> распределении. Ошибочных случаев: 0.
<!-- benchmarks:tier1-caveats:end -->

### Проекция — `Select(x => new Dto { Id, Str })`

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

<!-- benchmarks:tier1-projection:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToListAsync | A | 4.433 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 4.979 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 6.528 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 7.363 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 4.401 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 6.069 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 9.262 ms | 3.62 MB |
<!-- benchmarks:tier1-projection:end -->

### Агрегаты — SQL-side `Count` / `Sum` / `GroupBy → Count`

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

<!-- benchmarks:tier1-aggregates:start -->
| Операция | Арм | Категория | Mean | Allocated |
| --- | --- | --- | ---: | ---: |
| Count | A_Nextorm_Prepared_Count | A | 15.27 μs | 400 B |
| Count | A_Linq2Db_Compiled_Count | A | 19.70 μs | 1,272 B |
| Count | A_EFCore_Compiled_Count | A | 39.61 μs | 7,296 B |
| Sum | A_Nextorm_Prepared_Sum | A | 282.65 μs | 403 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 294.11 μs | 1,283 B |
| Sum | A_EFCore_Compiled_Sum | A | 314.81 μs | 7,323 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,338.19 μs | 4,698 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,369.71 μs | 7,018 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,401.89 μs | 32,642 B |
| Count | Dapper_Count | A+B | 18.55 μs | 936 B |
| Sum | Dapper_Sum | A+B | 295.30 μs | 947 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,486.63 μs | 12,322 B |
| Count | B_Nextorm_Cached_Count | B | 15.41 μs | 400 B |
| Count | B_Linq2Db_Count | B | 26.00 μs | 2,848 B |
| Count | B_EFCore_Count | B | 47.36 μs | 9,616 B |
| Sum | B_Nextorm_Cached_Sum | B | 279.90 μs | 963 B |
| Sum | B_Linq2Db_Sum | B | 306.10 μs | 3,475 B |
| Sum | B_EFCore_Sum | B | 325.55 μs | 10,515 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,340.93 μs | 8,730 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,391.61 μs | 11,738 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,454.86 μs | 42,155 B |
<!-- benchmarks:tier1-aggregates:end -->

### Пагинация — `OrderBy(Id).Offset(5000).Limit(100)`

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

<!-- benchmarks:tier1-paging:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_PageAsync | A | 90.91 μs | 13.95 KB |
| A_Linq2Db_Compiled_Page | A | 124.58 μs | 17.6 KB |
| A_EFCore_Compiled_PageAsync | A | 131.03 μs | 44.37 KB |
| A_Dapper_PageAsync | A+B | 112.87 μs | 18.14 KB |
| B_Nextorm_Cached_PageAsync | B | 118.32 μs | 19.43 KB |
| B_Linq2Db_PageAsync | B | 149.24 μs | 22.6 KB |
| B_EFCore_PageAsync | B | 215.39 μs | 56.39 KB |
<!-- benchmarks:tier1-paging:end -->

### Стриминг — buffered / unbuffered DTO и zero-materialization reader

Воспроизведение: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Отчёт](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Буферизованный DTO:

<!-- benchmarks:tier1-streaming-buffered:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToList_Dto | A | 5,217.99 μs | 1,360,688 B |
| Linq2Db_Compiled_ToList_Dto | A | 6,867.59 μs | 1,544,580 B |
| A_EFCore_Compiled_ToList_Dto | A | 10,303.59 μs | 3,790,111 B |
| Dapper_ToList_Dto | A+B | 7,831.00 μs | 1,784,116 B |
| B_Nextorm_Cached_ToList_Dto | B | 5,110.60 μs | 1,362,858 B |
| B_Linq2Db_ToList_Dto | B | 7,003.57 μs | 1,547,645 B |
| B_EFCore_ToList_Dto | B | 10,053.18 μs | 3,794,208 B |
<!-- benchmarks:tier1-streaming-buffered:end -->

Небуферизованное перечисление DTO:

<!-- benchmarks:tier1-streaming-unbuffered:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_AsyncStream_Dto | A | 5,102.05 μs | 1,280,890 B |
| A_EFCore_Compiled_AsyncStream_Dto | A | 6,390.56 μs | 3,527,002 B |
| Dapper_AsyncStream_Dto | A+B | 5,615.85 μs | 1,521,474 B |
| B_Nextorm_Cached_AsyncStream_Dto | B | 5,489.86 μs | 1,284,226 B |
| B_Linq2Db_AsyncStream_Dto | B | 5,632.32 μs | 1,284,642 B |
| B_EFCore_AsyncStream_Dto | B | 6,309.38 μs | 3,531,154 B |
<!-- benchmarks:tier1-streaming-unbuffered:end -->

Чтение через `DbDataReader` без материализации (синхронно; общий приёмник не сохраняет строки; EF Core
исключён):

<!-- benchmarks:tier1-streaming-readers:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToDataReader | A | 5,302.54 μs | 949.29 KB |
| Linq2Db_ToDataReader | B | 4,204.47 μs | 943.65 KB |
| Provider_Direct_ToDataReader | B | 4,258.22 μs | 943.39 KB |
| Dapper_ToDataReader | B | 4,597.43 μs | 943.42 KB |
| B_Nextorm_ToDataReader | B | 5,008.46 μs | 951.91 KB |
<!-- benchmarks:tier1-streaming-readers:end -->

Пустой `DbDataReader` служит контролем времени открытия и закрытия без чтения строк:

<!-- benchmarks:tier1-streaming-empty-readers:start -->
| Арм | Категория | Mean | Allocated |
| --- | --- | ---: | ---: |
| Provider_Direct_ToDataReader_Empty | B | 79.50 μs | 0.00 KB |
| Dapper_ToDataReader_Empty | B | 92.57 μs | 0.00 KB |
| Linq2Db_ToDataReader_Empty | B | 92.69 μs | 0.00 KB |
| B_Nextorm_ToDataReader_Empty | B | 220.89 μs | 16.80 KB |
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
| WriteJson_Array_Scalar | 5,721.1 μs | 0.92 | 10.31 KB |
| WriteJsonAsync_Array_Scalar | 4,948.7 μs | 0.80 | 9.97 KB |
| ToList_Dto | 6,215.8 μs | 1.00 | 783.76 KB |
| Nextorm_ToList_Dto_Json | 7,040.5 μs | 1.13 | 784.07 KB |
| WriteJson_Array_Dto | 7,424.2 μs | 1.20 | 403.13 KB |
| Linq2Db_ToList_Json | 8,423.1 μs | 1.36 | 963.75 KB |
| WriteJson_Array_ScalarPayload | 8,201.0 μs | 1.32 | 11493.13 KB |
| WriteJsonAsync_Array_ScalarPayload | 8,612.0 μs | 1.39 | 11493.34 KB |
| Dapper_ToList_Json | 8,835.7 μs | 1.42 | 1273.72 KB |
| EFCore_ToList_Json | 10,377.1 μs | 1.67 | 3158.27 KB |
| WriteJson_Array_WideDto | 10,384.0 μs | 1.67 | 11495.97 KB |
<!-- benchmarks:tier1-write-json:end -->

CSV (срез `large_table`, 10 000 строк; Ratio — относительно `Nextorm_ToList`):

<!-- benchmarks:tier1-csv:start -->
| Метод | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Nextorm_StringProjection_ToList | 2.617 ms | 0.34 | 1018.15 KB |
| Nextorm_StringProjection_WriteCsv | 3.302 ms | 0.42 | 949.13 KB |
| Nextorm_ToList | 7.805 ms | 1.00 | 2266.74 KB |
| Nextorm_ToList_ManualCsv | 9.237 ms | 1.18 | 4375.42 KB |
| Nextorm_WriteCsv | 9.480 ms | 1.21 | 1733.92 KB |
| Linq2Db_ToList_ManualCsv | 11.925 ms | 1.53 | 4087.51 KB |
| Dapper_ToList_ManualCsv | 12.323 ms | 1.58 | 5100.61 KB |
| EFCore_ToList_ManualCsv | 13.288 ms | 1.70 | 6750.96 KB |
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
датой и хешами отчётов. Хеш SHA-256 каждого отчёта вычисляется после замены окончаний строк CRLF на LF,
поэтому настройки окончаний строк при checkout не влияют на проверку; остальные байты отчёта по-прежнему
проверяются хешем.

Генератор читает дату начала каждого набора в UTC, версию Nextorm и хеши отчётов из
`suite-run-dates.json`. README показывает версии, а подробные статьи сохраняют даты.
После локального прогона передайте `--record-run-date CLASS=YYYY-MM-DD --run-version VERSION
--run-evidence SOURCE`. Повторите `--record-run-date` для каждого набора, замеренного на той же
ревизии. Укажите дату UTC начала набора, замеренную версию или Git-ревизию вместо `VERSION` и
путь к журналу или его URL вместо `SOURCE`. CI определяет версию по тегу замеренного коммита.
Если коммит не совпадает с тегом релиза, CI включает в подпись идентификатор коммита.

Тот же процесс обновления сквозных таблиц запускается из `.github/workflows/benchmarks.yml` — автоматически
после каждого мержа в `main`, затрагивающего движок (`src/**`) или проект бенчмарков, и вручную из вкладки
Actions. Workflow открывает pull request с новыми отчётами и таблицами.

## См. также

- [Возможности](capabilities.md) — что поддерживает каждая библиотека.
- [Переиспользование запросов: кэш и Prepare](../engine/03-plan-cache-and-reuse.md).
