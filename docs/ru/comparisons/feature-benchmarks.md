# Feature-бенчмарки

**Снимок:** 2026-10-17 · **Релиз:** 1.0.9-rc2 (ветка `1.0.9-rc3`, HEAD `881bf497`) · **Провайдер:** SQLite

Эта страница сравнивает время выполнения и выделенную память для отдельных форм запросов, а не
ранжирует библиотеки по сквозному запросу. На странице [бенчмарков](benchmarks.md) измеряются целые
сценарии и отдельно рассматривается путь с явной подготовкой запроса. Здесь результаты сгруппированы
по тирам и приведены на один запрос; Nextorm использует только неявный кэш планов.

- **Без пути с явной подготовкой запроса.** Все значения Nextorm получены через неявный кэш планов
  (`B_Nextorm_Cached_*`) или
  прямой терминал (`WriteJson`/`WriteCsv`). Ветки `Prepare()` / `A_*` Nextorm не используются.
  Отдельные
  результаты категории A на странице [бенчмарков](benchmarks.md) показывают, что Nextorm быстрее на
  каждой
  из приведённых там возможностей в подготовленном режиме; здесь рассматривается неявный кэш и
  сравнение с обычными API конкурентов.
- **Одна таблица на группу возможностей.** Колонки — библиотеки, строки — сценарии. Внутри таблицы
  нет деления на
  Category A/B.
- **На один запрос.** В ячейке указаны `среднее время / выделенная память` для одного запроса.
  Методы Category B выполняют 10 запросов за вызов, поэтому `Mean`/`Allocated` BenchmarkDotNet
  делятся на 10; методы Tier-1 выполняют один запрос за вызов. Ячейки Tier-5 включают один запрос и
  сериализацию 10 000 строк.
- **Пометки.** `**время**` отмечает лучшее время в строке, `**alloc**` — минимальную аллокацию.
  Полужирная ячейка лидирует по обоим показателям.
- **Шум.** `ShortRun` + `InProcessEmitToolchain` — исследовательский режим: различия ниже ~20 % считайте
  шумом и смотрите полные отчёты, прежде чем делать выводы.

## Методика

- **Машина:** AMD Ryzen 7 5800HS with Radeon Graphics (8 логических / 4 физических ядра), .NET 10.0.12,
  BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`), SQLite через `Microsoft.Data.Sqlite`;
  база лежит на tmpfs.
- **Nextorm:** неявный кэш планов для групп, производных от Category B, и кэшированные ветки Tier-1
  (`B_Nextorm_Cached_*`, `B_Nextorm_ToDataReader`). Без `Prepare()` и без compiled-веток.
- **Dapper:** сырой SQL с кэшированным IL. В этом наборе нет отдельного подготовленного или
  скомпилированного варианта запроса для Dapper, поэтому его ветка с сырым SQL используется без
  изменений.
- **linq2db и EF Core:** обычные, некомпилируемые API (`B_Linq2Db_*`, `B_EFCore_*`). Без
  `CompiledQuery`/`Compile()`.
- **Отсутствующие ветки:** у EF Core в этом наборе нет эквивалента для CTE, рекурсивного CTE, оконных
  функций и синхронного чтения через `DbDataReader`; такие ячейки — `—`.
- **Чтение через `DbDataReader`:** синхронный путь с общим приёмником, который не сохраняет строки,
  без
  материализации; EF Core исключён по дизайну, поскольку у него нет аналога LINQ для такого чтения.
- **JSON/CSV:** ветки конкурентов — ближайший эквивалент: материализация и ручная сериализация, тогда как
  Nextorm пишет напрямую. Эти две строки полезны, но это не сравнение одинаковых терминалов.

Исходные отчёты (вывод BenchmarkDotNet в `benchmarks/BenchmarkDotNet.Artifacts/results/`):

- [Features, warm cached](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md)
- [Projection](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md)
- [Aggregates](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md)
- [Paging](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md)
- [Streaming](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md)
- [JSON](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md) ·
  [CSV](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md)

## Tier-1 — сценарии формы запроса

Здесь сравниваются проекция, агрегаты на стороне SQL, упорядоченная пагинация, потоковое чтение
DTO и чтение через `DbDataReader` без создания объектов. В 6 из 8 сценариев Nextorm быстрее; в 5
сценариях он выделяет меньше памяти. Dapper быстрее в `Count`, а по времени чтение через
`DbDataReader` отстаёт от linq2db в 1.19 раза и от Dapper — в 1.17 раза.

| Сценарий | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| Проекция → DTO | **4.047 ms / 1.30 MB** | 7.286 ms / 1.70 MB | 5.958 ms / 1.48 MB | 9.881 ms / 3.62 MB |
| Count | 18.47 μs / 3.73 KB | **17.07 μs / 944 B** | 24.37 μs / 2.80 KB | 47.10 μs / 9.84 KB |
| Sum | **286.23 μs** / 4.13 KB | 297.01 μs / **954 B** | 305.35 μs / 3.41 KB | 345.24 μs / 10.72 KB |
| GroupBy → Count | **1.276 ms / 10.33 KB** | 1.290 ms / 12.04 KB | 1.416 ms / 11.27 KB | 1.397 ms / 41.94 KB |
| Пагинация — offset 5 000 / limit 100 | **101.31 μs** / 22.62 KB | 103.67 μs / **18.15 KB** | 140.91 μs / 22.65 KB | 185.84 μs / 56.90 KB |
| Стриминг — буферизованный DTO | **4.044 ms / 1.30 MB** | 7.102 ms / 1.70 MB | 6.053 ms / 1.48 MB | 8.838 ms / 3.62 MB |
| Стриминг — небуферизованный DTO | **4.059 ms / 1.22 MB** | 4.886 ms / 1.45 MB | 4.522 ms / 1.23 MB | 5.166 ms / 3.37 MB |
| Чтение через `DbDataReader` — синхронное | 4.677 ms / 947.98 KB | 3.996 ms / **942.77 KB** | **3.942 ms** / 943.00 KB | — |

**Что это значит.** В этом наборе Nextorm быстрее на широкой проекции и потоковом чтении DTO
(0.55–0.57× Dapper), `Sum` (0.96×), `GroupBy` (0.99×) и пагинации (0.98×). `Count` отстаёт от Dapper
в 1.08 раза. При чтении через `DbDataReader` все три библиотеки укладываются в диапазон 1.19× по
времени и 0.6 % по аллокациям.

## Tier-2 — реляционные формы

Dapper быстрее в 5 из 6 и меньше всех аллоцирует во всех 6; Nextorm выигрывает только `SELECT DISTINCT`
(15.53 против 18.41 μs, 0.84×). На остальных Nextorm отстаёт от Dapper в 1.16–1.75×.

| Сценарий | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| CTE | 32.72 μs / 10.89 KB | **19.83 μs / 1.57 KB** | 56.89 μs / 9.06 KB | — |
| Рекурсивный CTE | 35.33 μs / 13.31 KB | **24.44 μs / 1.53 KB** | 60.41 μs / 8.39 KB | — |
| Join по 4 таблицам | 32.96 μs / 10.67 KB | **25.03 μs / 2.02 KB** | 65.38 μs / 14.10 KB | 114.20 μs / 24.69 KB |
| LEFT JOIN | 23.03 μs / 6.16 KB | **19.83 μs / 2.22 KB** | 69.12 μs / 8.60 KB | 94.80 μs / 20.27 KB |
| SELECT DISTINCT | **15.53 μs** / 3.80 KB | 18.41 μs / **1.56 KB** | 30.44 μs / 3.91 KB | 64.19 μs / 12.20 KB |
| IN-list — inline | 31.65 μs / 7.76 KB | **18.12 μs / 1.27 KB** | 69.44 μs / 18.91 KB | 112.63 μs / 22.61 KB |

Варианты IN-list с захваченной коллекцией добавляют 24.2 μs и 12.6 KB на запрос к варианту со
встроенным списком. Это особенность кэша Nextorm, поэтому в сравнении библиотек оставлен вариант
со встроенным списком.

## Tier-3 — операции над множествами

Dapper выигрывает обе операции с небольшим отрывом по времени и в 4.4–4.9× по аллокациям.

| Сценарий | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| EXCEPT | 17.15 μs / 6.38 KB | **16.96 μs / 1.44 KB** | 38.10 μs / 5.05 KB | 72.82 μs / 15.31 KB |
| INTERSECT | 16.79 μs / 6.36 KB | **15.60 μs / 1.29 KB** | 36.77 μs / 5.01 KB | 71.94 μs / 14.38 KB |

EXCEPT — 1.01×, INTERSECT — 1.08× от Dapper; обе в 2.2–2.4× быстрее linq2db.

## Tier-4 — скалярные и оконные функции

Nextorm выигрывает `CASE WHEN` (0.93×) и `String.Contains` (0.82×); Dapper — `ToUpper` (1.10×), UDF
(1.09×) и оба оконных сценария (1.04× и 1.11×), и меньше всех аллоцирует во всех шести. Разделение
`Contains`/`ToUpper` и `ROW_NUMBER`/`SUM OVER` не смешивает разные операции в одной строке «строки» или
«окна».

| Сценарий | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| CASE WHEN | **13.04 μs** / 3.22 KB | 14.00 μs / **1.34 KB** | 25.05 μs / 3.80 KB | 56.78 μs / 11.95 KB |
| String.Contains | **14.97 μs** / 4.20 KB | 18.18 μs / **1.72 KB** | 30.95 μs / 4.78 KB | 69.55 μs / 14.01 KB |
| ToUpper | 15.29 μs / 4.20 KB | **13.93 μs / 1.28 KB** | 47.75 μs / 6.50 KB | 66.26 μs / 13.27 KB |
| UDF | 15.53 μs / 4.29 KB | **14.29 μs / 1.28 KB** | 48.46 μs / 6.50 KB | 66.25 μs / 13.27 KB |
| Окно — ROW_NUMBER | 31.55 μs / 5.03 KB | **30.46 μs / 1.54 KB** | 62.11 μs / 5.99 KB | — |
| Окно — SUM OVER | 33.03 μs / 5.46 KB | **29.75 μs / 1.55 KB** | 62.28 μs / 5.82 KB | — |

## Tier-5 — сериализация

Nextorm меньше всех аллоцирует на JSON, но отстаёт от Dapper/linq2db по времени в 1.10–1.13×; CSV —
быстрее всех и с минимальными аллокациями. Ветки конкурентов материализуют данные и сериализуют вручную,
поэтому это ближайшие эквиваленты, а не общий терминал.

| Сценарий | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| JSON-массив — DTO (10 000 строк) | 10.352 ms / **435.64 KB** | 9.416 ms / 1.24 MB | **9.150 ms** / 963.56 KB | 11.303 ms / 3.08 MB |
| CSV (10 000 строк) | **11.076 ms / 1.70 MB** | 13.291 ms / 4.75 MB | 13.385 ms / 4.22 MB | 14.364 ms / 6.36 MB |

В строке JSON используется `WriteJson_Array_Dto`, который выводит тот же объект DTO, что и варианты
конкурентов. Более узкий `WriteJson_Array_Scalar` быстрее, но выводит только ключ. Специализированный
вариант CSV для проекции строк в Nextorm ещё быстрее (4.029 ms / 949.67 KB), но покрывает более узкую
форму и не подходит для общей строки выше.

## Вердикт

В 22 сопоставимых сценариях запросов Nextorm быстрее в 9, Dapper — в 12, а linq2db — в 1.
Наименьший объём аллокаций показан у Nextorm в 5 строках, у Dapper — в 17; linq2db не лидирует ни в
одной. В этих результатах Dapper служит ориентиром для пути с кэшем при повторных запросах. Более
быстрые сценарии Nextorm сосредоточены в проекции, потоковом чтении и агрегатах, `SELECT DISTINCT`,
`CASE WHEN` и `String.Contains`; реляционные формы, операции над множествами и большинство
скалярных/оконных строк всё ещё отстают. В строке CSV Nextorm лидирует по обоим показателям; для
JSON он выделяет меньше памяти, но работает в 1.10–1.13 раза дольше. В этих строках Nextorm
записывает результат напрямую, а конкуренты сначала создают объекты, а затем сериализуют их вручную;
учитывайте это различие.

Этот отчёт не сравнивает путь с явной подготовкой запроса (`Prepare()`). В категории A на
датированной странице [бенчмарков](benchmarks.md) приведены результаты этого режима: Nextorm
показывает меньшее время на каждой из проверенных возможностей. Здесь сравнивается только путь с
неявным кэшем планов и обычными API конкурентов. Запуск приложения и NativeAOT здесь тоже не
измеряются; замер публикации NativeAOT без дополнительных настроек и показатели JIT/AOT приведены
на той же странице [бенчмарков](benchmarks.md).

## Как воспроизвести

```bash
# Этот отчёт не использует prepared-ветки.
for filter in "*SqliteBenchmarkFeaturesFairCached*" "*SqliteBenchmarkProjection*" \
              "*SqliteBenchmarkAggregates*" "*SqliteBenchmarkPaging*" "*SqliteBenchmarkStreaming*" \
              "*SqliteBenchmarkWriteJson*" "*SqliteBenchmarkCsv*"; do
  dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "$filter"
done
```

Каждый отчёт по ссылке — вывод BenchmarkDotNet в `benchmarks/BenchmarkDotNet.Artifacts/results/`.
Установите `NEXTORM_BENCH_FULL=1`, чтобы запустить полный out-of-process job вместо `ShortRun`.
