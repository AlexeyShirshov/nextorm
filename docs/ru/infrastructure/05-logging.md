# Логирование

> Подключите `ILoggerFactory` к [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder), чтобы захватывать выполняемый SQL, значения параметров и сообщения жизненного цикла соединения.

**Предварительные требования:** [Соединения](02-connections.md) · [Dependency injection](../getting-started/04-dependency-injection.md) · [Query reuse](01-query-reuse-and-caching.md).

## Обзор

Логирование настраивается для каждого контекста через [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder): `UseLoggerFactory(ILoggerFactory)` включает его, а `LogSensitiveData(bool)` решает, могут ли значения параметров попадать в вывод. Расширения провайдеров `Use…` и регистрация DI на основе options возвращают один и тот же builder, поэтому конфигурация логирования не зависит от провайдера.

## Подключение `ILoggerFactory`

Установите фабрику на [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder); контекст создаёт свои логгеры в конструкторе.

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var builder = new DataContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var ctx = builder.CreateDataContext();
```

## Три логгера

Из фабрики создаются три логгера:

| Член | Категория | Назначение |
|---|---|---|
| [`Logger`](xref:NextORM.Core.QueryCommand.Logger) | тип контекста (например, `nextorm.sqlite.SqliteDataContext`) | сообщения о соединении и командах. Доступен на [`IContextEnvironment`](xref:NextORM.Core.IContextEnvironment). |
| `CommandLogger` | тип [`QueryCommand`](xref:NextORM.Core.QueryCommand) | привязывается к командам, построенным [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) / `From(...)`. Доступен на [`IContextEnvironment`](xref:NextORM.Core.IContextEnvironment). |
| [`ResultSetEnumeratorLogger`](xref:NextORM.Core.InMemoryDataContext.ResultSetEnumeratorLogger) | `NextORM.Core.ResultSetEnumerator` | сообщения жизненного цикла потоковой передачи (`Trace` при `MoveNext`, `Debug` при открытии соединения или освобождении читателя). Внутренний. |

## [`LogSensitiveData`](xref:NextORM.Core.DataContextBuilder.LogSensitiveData(System.Boolean))

`LogSensitiveData(bool)` управляет тем, записываются ли значения параметров и строка подключения. По умолчанию — `false`:

* SQL логируется на уровне `Debug` как `Executing query: {sql}`;
* при `LogSensitiveData(false)` параметризованный запрос дополнительно логирует `Use LogSensitiveData to see param values`;
* при `LogSensitiveData(true)` логируется каждое имя и значение параметра, а создание соединения логирует `Creating connection with {connStr}` вместо просто `Creating connection`.

Включайте это только тогда, когда приёмник логов является доверенным: значения параметров могут содержать персональные данные.

## Логирование команд

Буферизованные терминалы ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`ExecuteScalar`](xref:NextORM.Core.QueryCommand`1.ExecuteScalar(System.ReadOnlySpan{System.Object})), ...) логируют команду через [`Logger`](xref:NextORM.Core.QueryCommand.Logger) перед её выполнением. Потоковые терминалы используют [`ResultSetEnumeratorLogger`](xref:NextORM.Core.InMemoryDataContext.ResultSetEnumeratorLogger), который также выдаёт `Move next` на уровне `Trace`. Оба пути работают на уровне `Debug`, поэтому уровень логирования настроенной фабрики должен допускать `Debug`, чтобы что-либо появилось:

```csharp
var builder = new DataContextBuilder()
    .UseSqlServer(connectionString)
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(true); // includes parameter values in the output
```

## Различия провайдеров

Логирование ведёт себя одинаково во всех провайдерах; различается только тип соединения. См. [Соединения: различия провайдеров](02-connections.md#различия-провайдеров).

## См. также

* [Соединения](02-connections.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Query reuse: plan cache and `Prepare`](01-query-reuse-and-caching.md)
* [Documentation index](../index.md)

---

Source: `src/nextorm.core/DI/DataContextBuilder.cs:20` ([`UseLoggerFactory`](xref:NextORM.Core.DataContextBuilder.UseLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory))).
