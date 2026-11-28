# Соединения

> Посмотрите, как NextORM получает, переиспользует и освобождает соединение с базой данных, и чем роль жизненного цикла соединения отличается от самого контекста.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Внедрение зависимостей](../getting-started/04-dependency-injection.md) · [Переиспользование запросов](../engine/03-plan-cache-and-reuse.md).

## Обзор

SQL-провайдеры ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) и [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext)) наследуются от [`DataContext`](xref:NextORM.Core.DataContext). `DataContext` реализует и [`IDataContext`](xref:NextORM.Core.IDataContext), и [`IConnectionManager`](xref:NextORM.Core.IConnectionManager).

Выбирайте способ передачи соединения по тому, кто им владеет: при передаче строки подключения
соединение создаёт контекст и он же им владеет; при передаче `DbConnection` им владеете вы. Провайдер
in-memory реализует только [`IDataContext`](xref:NextORM.Core.IDataContext), поскольку соединения у
него нет.

## Строка подключения против переданного соединения

Выберите строку подключения, если контекст должен создать соединение и владеть им. Передайте
`DbConnection`, если соединением должен владеть и управлять ваш код. Каждый SQL-провайдер
предоставляет оба конструктора, а расширения builder `Use…` оборачивают их:

```csharp
// A: контекст создаёт соединение из строки и владеет им.
using var owned = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

// B: соединением владеет вызывающий код; контекст повторно использует именно этот экземпляр.
using var supplied = new SqliteConnection("Data Source=app.db");
using var reused = new SqliteDataContext(supplied, new DataContextBuilder());

// Те же варианты через builder:
var builderA = new DataContextBuilder().UseSqlite("app.db");
var builderB = new DataContextBuilder().UseSqlite(supplied);
```

[`ConnectionString`](xref:NextORM.Core.DataContext.ConnectionString) возвращает строку, с которой был создан контекст, либо собственную [`ConnectionString`](xref:NextORM.Core.DataContext.ConnectionString) переданного соединения, когда контекст был создан из соединения:

```csharp
using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
ctx.ConnectionString; // "Data Source=:memory:"

using var supplied = new SqliteConnection("Data Source=:memory:");
using var ctx2 = new SqliteDataContext(supplied, new DataContextBuilder());
ctx2.ConnectionString; // supplied.ConnectionString
```

## Переиспользование соединения

[`GetConnection`](xref:NextORM.Core.DataContext.GetConnection) создаёт соединение при первом обращении и возвращает **тот же** экземпляр при каждом последующем вызове. [`EnsureConnectionOpen`](xref:NextORM.Core.DataContext.EnsureConnectionOpen) открывает его, если оно закрыто; `EnsureConnectionOpenAsync(CancellationToken)` — отменяемая асинхронная форма. Например, повторные вызовы возвращают один экземпляр:

```csharp
using var ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

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

using var ctx = new SqliteDataContext(supplied, new DataContextBuilder());
var rows = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList(); // [42]
```

## Правила освобождения

* **Созданное контекстом** соединение (созданное из строки подключения) освобождается `Dispose()` / `DisposeAsync()`. Кэшированные подготовленные команды, которые были привязаны к нему, сбрасываются, чтобы перепривязаться при следующем использовании.
* **Переданное** соединение никогда не освобождается контекстом — оно остаётся в том состоянии, в котором было. Им владеет вызывающая сторона.

Примеры показывают, как освобождается соединение при каждом варианте владения:

```csharp
var owned = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
var ownedConn = owned.GetConnection();
owned.EnsureConnectionOpen();
owned.Dispose();
// ownedConn.State == ConnectionState.Closed

using var supplied = new SqliteConnection("Data Source=:memory:");
supplied.Open();
using (var ctx = new SqliteDataContext(supplied, new DataContextBuilder()))
{
    ctx.EnsureConnectionOpen();
}
// supplied.State == ConnectionState.Open
```

Поскольку [`IDataContext`](xref:NextORM.Core.IDataContext) регистрируется в DI как scoped и реализует `IDisposable`/`IAsyncDisposable`, освобождение scope автоматически освобождает созданное контекстом соединение. См. [Dependency injection](../getting-started/04-dependency-injection.md).

## [`IConnectionManager`](xref:NextORM.Core.IConnectionManager)

Жизненный цикл соединения — это отдельная роль, намеренно не входящая в [`IDataContext`](xref:NextORM.Core.IDataContext), чтобы провайдер без соединения (контекст in-memory) не был вынужден реализовывать пустую заглушку:

```csharp
public interface IConnectionManager
{
    DbConnection GetConnection();
    void EnsureConnectionOpen();
    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default);
}
```

[`DataContext`](xref:NextORM.Core.DataContext) реализует его, поэтому приведение контекста даёт то же соединение, что и публичный [`GetConnection`](xref:NextORM.Core.DataContext.GetConnection):

```csharp
using var ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());
bool same = ReferenceEquals(((IConnectionManager)ctx).GetConnection(), ctx.GetConnection()); // true
```

## Транзакции

Транзакция — тоже отдельная роль, поэтому контекст без соединения (in-memory) её не реализует.
[`DataContext`](xref:NextORM.Core.DataContext) реализует
[`ITransactionManager`](xref:NextORM.Core.ITransactionManager): начните транзакцию через
`BeginTransaction*` или встройтесь в существующую транзакцию ADO.NET через `UseTransaction`.
Savepoints (`CreateSavepoint*`/`RollbackToSavepoint*`/`ReleaseSavepoint*`) выполняются в активной
транзакции или во встроенной через `UseTransaction` и никогда не меняют её владение.
Её поддерживают SQLite, PostgreSQL, SQL Server и MySQL/MariaDB; ClickHouse её отклоняет, а провайдер
in-memory её не реализует. Полный гайд — [Транзакции](../guide/23-transactions.md).

## Таймаут команды

Каждая команда, которую выполняет nextorm, экспонирует ADO.NET-свойство `DbCommand.CommandTimeout` (в секундах). По умолчанию действует значение провайдера: nextorm не трогает свойство, пока вы не зададите таймаут.

Задайте общий для контекста дефолт через [`UseCommandTimeout`](xref:NextORM.Core.DataContextBuilder.UseCommandTimeout(System.Int32)); он применяется ко всем командам `SELECT`, DML и `ExecuteScalar` создаваемых builder'ом контекстов:

```csharp
using var ctx = new DataContextBuilder()
    .UseSqlite("app.db")
    .UseCommandTimeout(15)
    .CreateDataContext();
```

Переопределите его для отдельного запроса через `WithCommandTimeout(seconds)` на builder'е или на построенной команде ([`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1), [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) и [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)):

```csharp
var ids = ctx.From<ISimpleEntity>()
    .WithCommandTimeout(5)          // только этот запрос
    .Select(x => x.Id)
    .ToList();

var slowIds = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .WithCommandTimeout(120)        // и на QueryCommand<T>
    .ToList();
```

Примечания:

* Без вызова (`null`) и при значении ноль или меньше действует дефолт провайдера; свойство не меняется, поэтому при неиспользовании настройки нет никаких затрат. `WithCommandTimeout(0)` снимает per-query-переопределение и наследует контекстный дефолт.
* Итоговое значение входит в ключ план-кэша: два в остальном одинаковых запроса с разными таймаутами не делят кэшированную команду, и таймаут никогда не «протекает» из одного запроса в другой. Контексты с разными [`UseCommandTimeout`](xref:NextORM.Core.DataContextBuilder.UseCommandTimeout(System.Int32)) тоже не делят кэшированный план.
* Per-query-переопределение доступно для запросов на чтение; DML-команды и батчи всегда используют контекстный дефолт.
* У in-memory-контекста нет команды, он игнорирует настройку.
* Особенность провайдера: `ClickHouse.Driver` 1.4.0 экспонирует унаследованное свойство, но HTTP-запрос ограничивается `Timeout` соединения (по умолчанию две минуты), а не `CommandTimeout`; там значение не действует. См. [Ограничения](../advanced/limitations.md).

## Различия провайдеров

В таблице указаны тип соединения и особенности каждого провайдера:

| Провайдер | Тип соединения | Примечания |
|---|---|---|
| SQLite | `SqliteConnection` | `OnConnectionCreated` регистрирует пользовательские агрегатные функции (`stdev`, `stdevp`, `var`, `varp`) и для созданных, и для переданных соединений. |
| SQL Server | `SqlConnection` | Обратного вызова для соединения нет; значение параметра null отправляется как `DBNull`. |
| PostgreSQL | `NpgsqlConnection` | Обратного вызова для соединения нет; значение параметра null отправляется как `DBNull`. |
| MySQL | `MySqlConnection` | Обратного вызова для соединения нет; значение параметра null отправляется как `DBNull`. |
| MariaDB | `MySqlConnection` | Использует драйвер и диалект MySQL; значение параметра null отправляется как `DBNull`. |
| ClickHouse | `ClickHouseConnection` | Параметры отправляются как HTTP query-параметры; для null нельзя вывести тип ClickHouse, поэтому задайте тип параметра на уровне драйвера. |
| In-memory | нет | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) реализует только [`IDataContext`](xref:NextORM.Core.IDataContext) и не предоставляет [`GetConnection`](xref:NextORM.Core.DataContext.GetConnection)/[`IConnectionManager`](xref:NextORM.Core.IConnectionManager). |

## См. также

* [Логирование](04-logging.md)
* [Переиспользование запросов: кэш планов и `PrepareBuffered`](../engine/03-plan-cache-and-reuse.md)
* [Внедрение зависимостей](../getting-started/04-dependency-injection.md)
* [Быстрый старт](../getting-started/02-quickstart.md)
* [Указатель документации](../index.md)

---

Source: `tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:24` (created connection),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:32` (stable across calls),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:40` (supplied instance),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:49` (supplied stays open),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:63` (owned is disposed),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:76` / `:84` ([`ConnectionString`](xref:NextORM.Core.DataContext.ConnectionString)),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:93` ([`IConnectionManager`](xref:NextORM.Core.IConnectionManager)),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:101` (SQLite functions on a supplied connection),
`tests/nextorm.sqlite.tests/Raw/ConnectionManagementTests.cs:116` (queries use the supplied connection);
`src/nextorm.core/DataContext/Roles/IConnectionManager.cs:10`,
`src/nextorm.core/DataContext/Roles/IContextEnvironment.cs:8`,
`src/nextorm.core/DataContext/DataContext.cs:106` ([`GetConnection`](xref:NextORM.Core.DataContext.GetConnection)),
`src/nextorm.core/DataContext/DataContext.cs:176` ([`ConnectionString`](xref:NextORM.Core.DataContext.ConnectionString)).
