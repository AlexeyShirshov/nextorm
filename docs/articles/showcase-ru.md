Я уже довольно давно разрабатываю Nextorm — ORM-библиотеку для .NET. С появлением инструментов агентской разработки этот процесс заметно ускорился и упростился, но для меня это по-прежнему большой и серьёзный инженерный проект. Сейчас хочу представить важный этап этой работы: первый стабильный релиз — 1.0.9.

Идея Nextorm — совместить производительность уровня Dapper с удобством автоматической генерации SQL из конструкций C#. Вместо написания SQL вручную разработчик описывает запросы строго типизированными выражениями, получая проверку типов и обращений к свойствам на этапе компиляции, поддержку IntelliSense и привычные инструменты рефакторинга.

Например
```csharp
var row = dataContext.From<SimpleEntity>()
    .Where(x => x.Id == 1)
    .Select(x => new { x.Id })
    .Single();
```

По своей концепции и духу Nextorm ближе к LINQ to SQL. При этом библиотека использует собственный API на основе деревьев выражений C# — expression trees, а не стандартный LINQ-провайдер. Эти деревья служат для генерации SQL и построения эффективных мапперов результатов в .NET-типы. В JIT-режиме выражения маппинга компилируются в делегаты и затем переиспользуются при чтении данных из БД.

![Схема выполнения команды Nextorm](../assets/diagrams/nextorm-command-execution.svg)

Библиотека поддерживает SQLite, SQL Server, PostgreSQL, MySQL, MariaDB и ClickHouse. API охватывает широкий набор возможностей этих СУБД: соединения, группировки, CTE, оконные функции и специфичные для отдельных провайдеров конструкции. Мне важно, чтобы разработчику не приходилось переходить на ручной SQL, как только запрос становится сложнее обычной выборки.

[https://alexeyshirshov.github.io/nextorm/guide/provider-specific-sql/overview.html](https://alexeyshirshov.github.io/nextorm/guide/provider-specific-sql/overview.html)

Одним из ключевых приоритетов с самого начала была производительность, а ориентиром служил Dapper. В опубликованных бенчмарках на SQLite Nextorm во многих сценариях показывает сопоставимую скорость, а в некоторых — превосходит этот ориентир. Результаты, условия измерений и исходники тестов доступны в репозитории.

[https://alexeyshirshov.github.io/nextorm/comparisons/benchmarks.html](https://alexeyshirshov.github.io/nextorm/comparisons/benchmarks.html)

Ниже приведена таблица по замерам производительности для скомпилированных/подготовленных запросов для четырех провайдеров.

| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.75 μs** | 15.80 μs | 16.50 μs | 38.46 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **9.135 ms** | 9.883 ms | 10.594 ms | 10.998 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **915.0 μs** | 1,378.8 μs | 1,432.3 μs | 3,540.1 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **8.610 ms** | 27.766 ms | 418.616 ms | 109.613 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **923.5 μs** | 1,421.9 μs | 1,523.8 μs | 3,742.6 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **93.64 μs** | 148.96 μs | 363.31 μs | 368.44 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **92.38 μs** | 142.69 μs | 372.78 μs | 369.31 μs |
| [`CASE WHEN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.45 μs** | 14.64 μs | 15.43 μs | 36.91 μs |
| [CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.89 μs** | 22.25 μs | 23.75 μs | — |
| [`SELECT DISTINCT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.44 μs** | 18.27 μs | 18.36 μs | 42.53 μs |
| [`EXCEPT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **11.41 μs** | 18.13 μs | 21.57 μs | 46.40 μs |
| [IN-list](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.79 μs** | 18.55 μs | 47.92 μs | 66.24 μs |
| [`INTERSECT`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **10.59 μs** | 16.64 μs | 20.33 μs | 44.07 μs |
| [4-table join](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **13.27 μs** | 27.17 μs | 24.65 μs | 50.99 μs |
| [`LEFT JOIN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.65 μs** | 21.22 μs | 21.63 μs | 45.27 μs |
| [Recursive CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **12.95 μs** | 26.02 μs | 28.50 μs | — |
| [String functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.33 μs** | 14.22 μs | 15.25 μs | 36.89 μs |
| [UDF](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **9.43 μs** | 14.28 μs | 15.31 μs | 36.24 μs |
| [Window functions](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) | **14.99 μs** | 31.84 μs | 34.65 μs | — |

Есть и экспериментальная поддержка NativeAOT, пока проверенная для SQLite на .NET 11 под Linux x64. Пример приложения из репозитория удалось собрать в нативный исполняемый файл без изменений прикладного кода — включив PublishAot=true. Поддержка пока частичная: это не обещание, что любой запрос с любым провайдером уже будет работать в AOT.

[https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html](https://alexeyshirshov.github.io/nextorm/infrastructure/05-nativeaot.html)

Отдельная возможность — выполнение запросов без материализации результатов в объекты. Необязательно создавать DTO для каждой строки: можно получить DbDataReader для самостоятельного чтения или записывать результат напрямую в Stream в формате JSON или CSV. Это позволяет, например, отдавать данные в HTTP-ответ или файл без промежуточных объектов и загрузки всей выборки в память.

```csharp
app.MapGet("/orders", async (HttpContext http, DateTime from, decimal minTotal, CancellationToken ct) =>
{
    using var ctx = new DataContextBuilder()
        .UsePostgres(connectionString)
        .CreateDataContext();

    http.Response.ContentType = "application/x-ndjson; charset=utf-8";

    await ctx.From<Order>()
        .Where(o => o.CreatedAt >= from && o.Total > minTotal)   // captured -> @from, @minTotal
        .Select(o => new { o.Id, o.CreatedAt, o.Total })
        .WriteJsonAsync(http.Response.Body, new JsonStreamOptions
        {
            Mode = JsonStreamMode.NdJson,
            IgnoreNull = true,
        }, ct);                                                  // no positional list
});
```

Буду рад вашим комментариям, предложениям и критике. Особенно интересно, насколько удобным кажется API и каких возможностей вам не хватает для ваших задач.
