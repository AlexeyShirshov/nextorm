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

### Бенчмарки: SQLite, асинхронные запросы

| Сценарий | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/publish/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.83 μs** | 15.80 μs | 16.50 μs | 38.46 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/publish/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **9.135 ms** | 9.883 ms | 10.594 ms | 10.998 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/publish/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **915.0 μs** | 1,378.8 μs | 1,432.3 μs | 3,540.1 μs |

*По текущим отчётам в `publish`: .NET 11.0.0, AMD EPYC 7763, Linux, BenchmarkDotNet ShortRun. Для каждой библиотеки выбран её самый быстрый измеренный вариант в соответствующем отчёте; режимы выполнения различаются. Меньше — лучше.*

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
