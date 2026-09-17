# Соединения и логирование

> Посмотрите, как NextORM получает, переиспользует и освобождает соединение с базой данных, и как подключить `ILoggerFactory`, чтобы захватывать выполняемый SQL, параметры и сообщения жизненного цикла соединения.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md) · [Query reuse](15-query-reuse.md).

## Обзор

SQL-провайдеры (`SqliteDbContext`, `SqlServerDbContext`, `PostgresDbContext`) все наследуются от `DbContext`, который реализует и `IDataContext`, и `IConnectionManager`. Контекст создаётся либо из строки подключения (контекст создаёт соединение и владеет им), либо из уже созданного `DbConnection` (им владеет вызывающая сторона). Провайдер in-memory реализует только `IDataContext` — у него вообще нет соединения.

Логирование настраивается для каждого контекста через `DbContextBuilder`: `UseLoggerFactory(ILoggerFactory)` включает его, а `LogSensitiveData(bool)` решает, могут ли значения параметров попадать в вывод.

## Строка подключения против переданного соединения

Каждый SQL-провайдер предоставляет два конструктора, и расширения builder `Use…` оборачивают их:

```csharp
// A: the context creates and owns the connection from the string.
using var owned = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());

// B: the caller owns the connection; the context reuses this exact instance.
using var supplied = new SqliteConnection("Data Source=app.db");
using var reused = new SqliteDbContext(supplied, new DbContextBuilder());

// Equivalent through the builder:
var builderA = new DbContextBuilder().UseSqlite("app.db");
var builderB = new DbContextBuilder().UseSqlite(supplied);
```

`ConnectionString` возвращает строку, с которой был создан контекст, либо собственную `ConnectionString` переданного соединения, когда контекст был создан из соединения:

```csharp
using var ctx = new SqliteDbContext("Data Source=:memory:", new DbContextBuilder());
ctx.ConnectionString; // "Data Source=:memory:"

using var supplied = new SqliteConnection("Data Source=:memory:");
using var ctx2 = new SqliteDbContext(supplied, new DbContextBuilder());
ctx2.ConnectionString; // supplied.ConnectionString
```

## Переиспользование соединения

`GetConnection()` создаёт соединение при первом обращении и возвращает **тот же** экземпляр при каждом последующем вызове. `EnsureConnectionOpen()` открывает его, если оно закрыто; `EnsureConnectionOpenAsync(CancellationToken)` — отменяемая асинхронная форма.

```csharp
using var ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());

ctx.EnsureConnectionOpen();

bool same = ReferenceEquals(ctx.GetConnection(), ctx.GetConnection()); // true
```

Поскольку соединение переиспользуется, переданное соединение — это способ сохранить базу данных, привязанную к соединению, живой. База данных SQLite `:memory:` привязана к своему соединению, поэтому таблица, созданная через переданное соединение, видна контексту только потому, что контекст работает именно на этом соединении:

```csharp
using var supplied = new SqliteConnection("Data Source=:memory:");
supplied.Open();

using (var setup = supplied.CreateCommand())
{
    setup.CommandText = "create table simple_entity (id integer primary key);" +
                        "insert into simple_entity (id) values (42);";
    setup.ExecuteNonQuery();
}

using var ctx = new SqliteDbContext(supplied, new DbContextBuilder());
var rows = ctx.Create<ISimpleEntity>().Select(x => x.Id).ToList(); // [42]
```

## Правила освобождения

* **Созданное контекстом** соединение (созданное из строки подключения) освобождается `Dispose()` / `DisposeAsync()`. Кэшированные подготовленные команды, которые были привязаны к нему, сбрасываются, чтобы перепривязаться при следующем использовании.
* **Переданное** соединение никогда не освобождается контекстом — оно остаётся в том состоянии, в котором было. Им владеет вызывающая сторона.

```csharp
var owned = new SqliteDbContext("Data Source=:memory:", new DbContextBuilder());
var ownedConn = owned.GetConnection();
owned.EnsureConnectionOpen();
owned.Dispose();
// ownedConn.State == ConnectionState.Closed

using var supplied = new SqliteConnection("Data Source=:memory:");
supplied.Open();
using (var ctx = new SqliteDbContext(supplied, new DbContextBuilder()))
{
    ctx.EnsureConnectionOpen();
}
// supplied.State == ConnectionState.Open
```

Поскольку `IDataContext` регистрируется в DI как scoped и реализует `IDisposable`/`IAsyncDisposable`, освобождение scope автоматически освобождает созданное контекстом соединение. См. [Dependency injection](../getting-started/04-dependency-injection.md).

## `IConnectionManager`

Жизненный цикл соединения — это отдельная роль, намеренно не входящая в `IDataContext`, чтобы провайдер без соединения (контекст in-memory) не был вынужден реализовывать пустую заглушку:

```csharp
public interface IConnectionManager
{
    DbConnection GetConnection();
    void EnsureConnectionOpen();
    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default);
}
```

`DbContext` реализует его, поэтому приведение контекста даёт то же соединение, что и публичный `GetConnection()`:

```csharp
using var ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());
bool same = ReferenceEquals(((IConnectionManager)ctx).GetConnection(), ctx.GetConnection()); // true
```

## Логирование

### Подключение `ILoggerFactory`

Установите фабрику на `DbContextBuilder`; контекст создаёт свои логгеры в конструкторе. Расширения провайдеров `Use…` и регистрация DI на основе options возвращают один и тот же builder, поэтому конфигурация логирования не зависит от провайдера.

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var builder = new DbContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var ctx = builder.CreateDbContext();
```

Из фабрики создаются три логгера:

| Член | Категория | Назначение |
|---|---|---|
| `Logger` | тип контекста (например, `nextorm.sqlite.SqliteDbContext`) | сообщения о соединении и командах. Доступен на `IContextEnvironment`. |
| `CommandLogger` | тип `QueryCommand` | привязывается к командам, построенным `Create<T>()` / `From(...)`. Доступен на `IContextEnvironment`. |
| `ResultSetEnumeratorLogger` | `nextorm.core.ResultSetEnumerator` | сообщения жизненного цикла потоковой передачи (`Trace` при `MoveNext`, `Debug` при открытии соединения или освобождении читателя). Внутренний. |

### `LogSensitiveData`

`LogSensitiveData(bool)` управляет тем, записываются ли значения параметров и строка подключения. По умолчанию — `false`:

* SQL логируется на уровне `Debug` как `Executing query: {sql}`;
* при `LogSensitiveData(false)` параметризованный запрос дополнительно логирует `Use LogSensitiveData to see param values`;
* при `LogSensitiveData(true)` логируется каждое имя и значение параметра, а создание соединения логирует `Creating connection with {connStr}` вместо просто `Creating connection`.

Включайте это только тогда, когда приёмник логов является доверенным: значения параметров могут содержать персональные данные.

### Логирование команд

Буферизованные терминалы (`ToList`, `First`, `ExecuteScalar`, ...) логируют команду через `Logger` перед её выполнением. Потоковые терминалы используют `ResultSetEnumeratorLogger`, который также выдаёт `Move next` на уровне `Trace`. Оба пути работают на уровне `Debug`, поэтому уровень логирования настроенной фабрики должен допускать `Debug`, чтобы что-либо появилось:

```csharp
var builder = new DbContextBuilder()
    .UseSqlServer(connectionString)
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(true); // includes parameter values in the output
```

## Различия провайдеров

| Провайдер | Тип соединения | Примечания |
|---|---|---|
| SQLite | `SqliteConnection` | `OnConnectionCreated` регистрирует пользовательские агрегатные функции (`stdev`, `stdevp`, `var`, `varp`) и для созданных, и для переданных соединений. |
| SQL Server | `SqlConnection` | Обратного вызова для соединения нет; значение параметра null отправляется как `DBNull`. |
| PostgreSQL | `NpgsqlConnection` | Обратного вызова для соединения нет; значение параметра null отправляется как `DBNull`. |
| In-memory | нет | `InMemoryContext` реализует только `IDataContext` и не предоставляет `GetConnection`/`IConnectionManager`. |

Логирование ведёт себя одинаково во всех провайдерах; различается только тип соединения.

## См. также

* [Query reuse: plan cache and `Prepare`](15-query-reuse.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Quickstart](../getting-started/02-quickstart.md)
* [Documentation index](../index.md)

---

Source: `test/nextorm.sqlite.tests/ConnectionManagementTests.cs:24` (created connection),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:32` (stable across calls),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:40` (supplied instance),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:49` (supplied stays open),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:63` (owned is disposed),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:76` / `:84` (`ConnectionString`),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:93` (`IConnectionManager`),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:101` (SQLite functions on a supplied connection),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:116` (queries use the supplied connection);
`src/nextorm.core/DataContext/Roles/IConnectionManager.cs:10`,
`src/nextorm.core/DataContext/Roles/IContextEnvironment.cs:8`,
`src/nextorm.core/DataContext/DbContext.cs:106` (`GetConnection`),
`src/nextorm.core/DataContext/DbContext.cs:176` (`ConnectionString`),
`src/nextorm.core/DI/DataContextOptionsBuilder.cs:20` (`UseLoggerFactory`).
