# Внедрение зависимостей

> Регистрируйте контексты NextORM с помощью [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})), настраивайте провайдер и логирование на scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) и получайте один экземпляр контекста на область (scope).

**Предварительные требования:** [Установка](01-installation.md) · [Быстрый старт](02-quickstart.md).

## Обзор

Регистрация DI находится в [`NextORM.Core`](xref:NextORM.Core) ([`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions)) и работает с любым контейнером `Microsoft.Extensions.DependencyInjection`. Есть два пути регистрации:

* **Путь типа** - [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})) регистрирует конкретный тип контекста как scoped и
  перенаправляет [`IDataContext`](xref:NextORM.Core.IDataContext) на него. Контейнер создаёт `TContext`, поэтому его конструктор должен быть
  разрешим из контейнера (например, конструктор без параметров у [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)).
* **Путь параметров** - `AddNextOrmContext(Action<DataContextBuilder>)` (или перегрузка
  `Action<IServiceProvider, DataContextBuilder>`) регистрирует **scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)**, который
  настраивает делегат параметров, плюс фабрику [`IDataContext`](xref:NextORM.Core.IDataContext), вызывающую
  `` Это путь, используемый с методами `Use…` провайдеров.

Гарантии следуют из XML-документации на [`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions):

* конкретный тип контекста регистрируется **один раз на область**, и получение конкретного типа и
  [`IDataContext`](xref:NextORM.Core.IDataContext) даёт **один и тот же экземпляр**;
* делегат параметров **обязателен** для регистрации на основе фабрики - передача `null` выбрасывает
  `ArgumentNullException` во время регистрации, а не во время разрешения;
* сам [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) является scoped, поэтому каждая область получает свежий построитель.

Keyed-варианты ([`AddKeyedNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddKeyedNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder},System.Object))) регистрируют построитель и [`IDataContext`](xref:NextORM.Core.IDataContext) под ключом сервиса, так что могут сосуществовать несколько по-разному настроенных контекстов.

## Регистрация контекста

Обобщённая регистрация - самая короткая форма:

```csharp
var services = new ServiceCollection();

services.AddNextOrmContext<InMemoryDataContext>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var viaInterface = scope.ServiceProvider.GetRequiredService<IDataContext>();
var viaConcrete = scope.ServiceProvider.GetRequiredService<InMemoryDataContext>();
// viaInterface and viaConcrete are the same instance
```

Чтобы настроить провайдер базы данных, используйте перегрузку с параметрами:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

Перегрузка с `IServiceProvider` позволяет извлекать зависимости, такие как `ILoggerFactory`, из контейнера во время настройки:

```csharp
services.AddNextOrmContext((sp, builder) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    builder
        .UseSqlServer(connectionString)
        .UseLoggerFactory(loggerFactory);
});
```

Keyed-регистрация разделяет несколько контекстов:

```csharp
services.AddKeyedNextOrmContext(builder => builder.UsePostgres(reportingConnectionString), "reporting");
services.AddKeyedNextOrmContext<InMemoryDataContext>("audit"); // generic keyed type path
```

```csharp
using var scope = provider.CreateScope();
var reporting = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("reporting");
```

> **Примечание:** путь параметров регистрирует только [`IDataContext`](xref:NextORM.Core.IDataContext) (и scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)); он
> не регистрирует конкретный тип [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext)/[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext)/[`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext). Получайте
> [`IDataContext`](xref:NextORM.Core.IDataContext) при использовании пути параметров. Именно путь типа обеспечивает то, что конкретный тип и
> интерфейс разрешаются в один и тот же экземпляр.

## Настройка построителя напрямую

[`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) можно также использовать без контейнера - именно это и вызывает DI-фабрика:

```csharp
var builder = new DataContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var dataContext = builder.CreateDataContext();
// Without a Use… call (or an assigned Factory) CreateDataContext() throws
// InvalidOperationException("Context is not set").
```

| Член | Назначение |
|---|---|
| [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory) | `Func<DataContextBuilder, IDataContext>`; задаётся расширениями `Use…` или назначьте свой, чтобы обойти провайдеры. |
| `UseLoggerFactory(ILoggerFactory)` | Создаёт логгеры контекста, команды и перечислителя из фабрики. |
| `LogSensitiveData(bool)` | Управляет тем, записываются ли значения параметров в логи. |
| [`CreateDataContext`](xref:NextORM.Core.DataContextBuilder.CreateDataContext) | Вызывает [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory); выбрасывает `InvalidOperationException("Context is not set")`, когда ни один провайдер не настроен. |

Расширения провайдеров ([`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection))/[`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions.UseSqlServer(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection))/[`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions.UsePostgres(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)), в пакетах провайдеров) принимают либо строку подключения, либо `DbConnection`:

```csharp
builder.UseSqlite("app.db");              // context creates and owns the connection
builder.UseSqlite(connection);            // context reuses the supplied connection
builder.UseSqlServer("Server=localhost;Database=app;Trusted_Connection=True;");
builder.UsePostgres("Host=localhost;Database=app");
```

Контекст, созданный из переданного подключения, оставляет это подключение открытым при освобождении контекста; контекст, создавший собственное подключение, закрывает его. В сборках `DEBUG` `UseSqlite(string)` также проверяет, что файл существует.

## Времена жизни

| Регистрация | Сервис | Время жизни | Примечания |
|---|---|---|---|
| [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})) | `T` | scoped | один раз на область |
| [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})) | [`IDataContext`](xref:NextORM.Core.IDataContext) | scoped | перенаправляет на тот же экземпляр `T` |
| `AddNextOrmContext(Action<…>)` | [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) | scoped | создаётся заново для каждой области |
| `AddNextOrmContext(Action<…>)` | [`IDataContext`](xref:NextORM.Core.IDataContext) | scoped | `builder.CreateDataContext()` |
| `AddKeyedNextOrmContext(…, key)` | keyed [`IDataContext`](xref:NextORM.Core.IDataContext) / [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) | scoped | разрешается по ключу |

Поскольку [`IDataContext`](xref:NextORM.Core.IDataContext) является scoped и реализует `IDisposable`/`IAsyncDisposable`, освобождение области освобождает контекст (а для контекстов, владеющих им, - подключение). Не получайте контексты из корневого провайдера - всегда создавайте область.

## Различия провайдеров

[`UseLoggerFactory`](xref:NextORM.Core.DataContextBuilder.UseLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory)) и [`LogSensitiveData`](xref:NextORM.Core.DataContextBuilder.LogSensitiveData(System.Boolean)) ведут себя одинаково во всех провайдерах; различается только расширение `Use…`, выбирающее провайдер.

| Провайдер | Расширение | Перегрузки |
|---|---|---|
| SQLite | [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string filepath`, `DbConnection` |
| SQL Server | [`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions.UseSqlServer(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| PostgreSQL | [`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions.UsePostgres(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| MySQL | [`UseMySql`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions.UseMySql(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| MariaDB | [`UseMariaDb`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions.UseMariaDb(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| ClickHouse | [`UseClickHouse`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions.UseClickHouse(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| In-memory | нет | зарегистрируйте [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) напрямую или назначьте [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory) |

## См. также

* [Установка](01-installation.md)
* [Быстрый старт](02-quickstart.md)
* [Сущности и метаданные](03-entities-and-metadata.md)
* [Индекс документации](../index.md)

---

Source: `tests/nextorm.core.tests/DependencyInjectionTests.cs:13`,
`tests/nextorm.core.tests/DependencyInjectionTests.cs:43`,
`tests/nextorm.core.tests/DependencyInjectionTests.cs:58`,
`tests/nextorm.core.tests/DependencyInjectionTests.cs:71`;
`src/nextorm.core/DI/ServiceCollectionExtensions.cs:73`;
`src/nextorm.core/DI/DataContextBuilder.cs:20`;
`src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:8`.
