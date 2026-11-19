# Внедрение зависимостей

> Регистрируйте контексты NextORM с помощью [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext``1(Microsoft.Extensions.DependencyInjection.IServiceCollection)), настраивайте провайдер и логирование на scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) и получайте один экземпляр контекста на область (scope).

**Предварительные требования:** [Установка](01-installation.md) · [Быстрый старт](02-quickstart.md).

## Обзор

Выберите регистрацию через делегат настройки, если нужно настроить SQL-провайдер вызовами `Use…`.
Регистрация по типу подходит, когда контейнер может сам создать контекст, например
[`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext). Оба варианта доступны через
[`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions) из
[`NextORM.Core`](xref:NextORM.Core) и работают с любым контейнером
`Microsoft.Extensions.DependencyInjection`.

Регистрация через делегат настройки добавляет scoped
[`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) и фабрику
[`IDataContext`](xref:NextORM.Core.IDataContext). Фабрика вызывает
[`CreateDataContext`](xref:NextORM.Core.DataContextBuilder.CreateDataContext). Регистрация по типу
добавляет конкретный контекст со сроком жизни scoped и направляет `IDataContext` на тот же экземпляр.
Контейнер создаёт `TContext`, поэтому должен уметь разрешить параметры его конструктора.

Эти регистрации гарантируют:

* конкретный тип контекста регистрируется **один раз на область**, и получение конкретного типа и
  [`IDataContext`](xref:NextORM.Core.IDataContext) даёт **один и тот же экземпляр**;
* делегат настройки **обязателен** для регистрации на основе фабрики - передача `null` выбрасывает
  `ArgumentNullException` во время регистрации, а не во время разрешения;
* сам [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) является scoped, поэтому каждая область получает свежий построитель.

Keyed-варианты ([`AddKeyedNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddKeyedNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder},System.Object))) регистрируют построитель и [`IDataContext`](xref:NextORM.Core.IDataContext) под ключом сервиса, так что могут сосуществовать несколько по-разному настроенных контекстов.

## Регистрация контекста

Для SQL-провайдера настройте построитель в делегате параметров:

```csharp
var services = new ServiceCollection();

services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

Если для настройки провайдера нужна другая зависимость, например `ILoggerFactory`, используйте
перегрузку с `IServiceProvider`:

```csharp
services.AddNextOrmContext((sp, builder) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    builder
        .UseSqlServer(connectionString)
        .UseLoggerFactory(loggerFactory);
});
```

Если приложению нужны несколько по-разному настроенных контекстов, задайте им ключи:

```csharp
services.AddKeyedNextOrmContext(builder => builder.UsePostgres(reportingConnectionString), "reporting");
services.AddKeyedNextOrmContext<InMemoryDataContext>("audit"); // generic keyed type path
```

Создайте провайдер после регистрации обоих контекстов и получайте контекст с ключом из области:

```csharp
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var reporting = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("reporting");
```

Путь параметров регистрирует только [`IDataContext`](xref:NextORM.Core.IDataContext) и scoped
[`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder). Он не регистрирует конкретный тип
провайдера, например [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext),
[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) или
[`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext). При такой регистрации получайте
`IDataContext`.

Для контекста, который контейнер может создать напрямую, используйте путь типа. В пределах одной
области конкретный тип и `IDataContext` указывают на один экземпляр:

```csharp
var typeServices = new ServiceCollection();
typeServices.AddNextOrmContext<InMemoryDataContext>();

using var typeProvider = typeServices.BuildServiceProvider();
using var typeScope = typeProvider.CreateScope();

var viaInterface = typeScope.ServiceProvider.GetRequiredService<IDataContext>();
var viaConcrete = typeScope.ServiceProvider.GetRequiredService<InMemoryDataContext>();
// viaInterface and viaConcrete are the same instance
```

## Интерцепторы через DI

Интерцептор можно зарегистрировать по типу сервиса, а не готовым экземпляром; builder разрешает его из области, создавшей builder:

```csharp
services.AddScoped<TimingInterceptor>();

services.AddNextOrmContext(builder => builder
    .UseSqlite("app.db")
    .AddInterceptor<TimingInterceptor>()); // или .AddInterceptor(typeof(TimingInterceptor))
```

Разрешение происходит один раз на создание контекста, поэтому scoped-, singleton- и transient-времена жизни соблюдаются, а владение освобождением остаётся за контейнером. Type-регистрация требует builder с DI — на отдельном builder она падает при `CreateDataContext()`, а незарегистрированный сервис — ошибкой `GetRequiredService` самого контейнера до вызова фабрики контекста. Порядок, дедупликацию и контракт ошибок см. в [«Интерцепторы»](../infrastructure/02-interceptors.md).

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
* [Логирование](../infrastructure/04-logging.md)
* [Индекс документации](../index.md)

---

Source: `tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:13`,
`tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:43`,
`tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:58`,
`tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:71`;
`src/nextorm.core/DI/ServiceCollectionExtensions.cs:73`;
`src/nextorm.core/DI/DataContextBuilder.cs:20`;
`src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:8`.
