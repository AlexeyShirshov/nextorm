# Внедрение зависимостей

> Регистрируйте контексты NextORM с помощью `AddNextOrmContext`, настраивайте провайдер и логирование на scoped `DbContextBuilder` и получайте один экземпляр контекста на область (scope).

**Предварительные требования:** [Установка](01-installation.md) · [Быстрый старт](02-quickstart.md).

## Обзор

Регистрация DI находится в `nextorm.core` (`ServiceCollectionExtensions`) и работает с любым контейнером `Microsoft.Extensions.DependencyInjection`. Есть два пути регистрации:

* **Путь типа** - `AddNextOrmContext<TContext>()` регистрирует конкретный тип контекста как scoped и
  перенаправляет `IDataContext` на него. Контейнер создаёт `TContext`, поэтому его конструктор должен быть
  разрешим из контейнера (например, конструктор без параметров у `InMemoryContext`).
* **Путь параметров** - `AddNextOrmContext(Action<DbContextBuilder>)` (или перегрузка
  `Action<IServiceProvider, DbContextBuilder>`) регистрирует **scoped `DbContextBuilder`**, который
  настраивает делегат параметров, плюс фабрику `IDataContext`, вызывающую
  `DbContextBuilder.CreateDbContext()`. Это путь, используемый с методами `Use…` провайдеров.

Гарантии следуют из XML-документации на `ServiceCollectionExtensions`:

* конкретный тип контекста регистрируется **один раз на область**, и получение конкретного типа и
  `IDataContext` даёт **один и тот же экземпляр**;
* делегат параметров **обязателен** для регистрации на основе фабрики - передача `null` выбрасывает
  `ArgumentNullException` во время регистрации, а не во время разрешения;
* сам `DbContextBuilder` является scoped, поэтому каждая область получает свежий построитель.

Keyed-варианты (`AddKeyedNextOrmContext`) регистрируют построитель и `IDataContext` под ключом сервиса, так что могут сосуществовать несколько по-разному настроенных контекстов.

## Регистрация контекста

Обобщённая регистрация - самая короткая форма:

```csharp
var services = new ServiceCollection();

services.AddNextOrmContext<InMemoryContext>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var viaInterface = scope.ServiceProvider.GetRequiredService<IDataContext>();
var viaConcrete = scope.ServiceProvider.GetRequiredService<InMemoryContext>();
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
services.AddKeyedNextOrmContext<InMemoryContext>("audit"); // generic keyed type path
```

```csharp
using var scope = provider.CreateScope();
var reporting = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("reporting");
```

> **Примечание:** путь параметров регистрирует только `IDataContext` (и scoped `DbContextBuilder`); он
> не регистрирует конкретный тип `SqliteDbContext`/`SqlServerDbContext`/`PostgresDbContext`. Получайте
> `IDataContext` при использовании пути параметров. Именно путь типа обеспечивает то, что конкретный тип и
> интерфейс разрешаются в один и тот же экземпляр.

## Настройка построителя напрямую

`DbContextBuilder` можно также использовать без контейнера - именно это и вызывает DI-фабрика:

```csharp
var builder = new DbContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var dataContext = builder.CreateDbContext();
// Without a Use… call (or an assigned Factory) CreateDbContext() throws
// InvalidOperationException("Context is not set").
```

| Член | Назначение |
|---|---|
| `Factory` | `Func<DbContextBuilder, IDataContext>`; задаётся расширениями `Use…` или назначьте свой, чтобы обойти провайдеры. |
| `UseLoggerFactory(ILoggerFactory)` | Создаёт логгеры контекста, команды и перечислителя из фабрики. |
| `LogSensitiveData(bool)` | Управляет тем, записываются ли значения параметров в логи. |
| `CreateDbContext()` | Вызывает `Factory`; выбрасывает `InvalidOperationException("Context is not set")`, когда ни один провайдер не настроен. |

Расширения провайдеров (`UseSqlite`/`UseSqlServer`/`UsePostgres`, в пакетах провайдеров) принимают либо строку подключения, либо `DbConnection`:

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
| `AddNextOrmContext<T>()` | `T` | scoped | один раз на область |
| `AddNextOrmContext<T>()` | `IDataContext` | scoped | перенаправляет на тот же экземпляр `T` |
| `AddNextOrmContext(Action<…>)` | `DbContextBuilder` | scoped | создаётся заново для каждой области |
| `AddNextOrmContext(Action<…>)` | `IDataContext` | scoped | `builder.CreateDbContext()` |
| `AddKeyedNextOrmContext(…, key)` | keyed `IDataContext` / `DbContextBuilder` | scoped | разрешается по ключу |

Поскольку `IDataContext` является scoped и реализует `IDisposable`/`IAsyncDisposable`, освобождение области освобождает контекст (а для контекстов, владеющих им, - подключение). Не получайте контексты из корневого провайдера - всегда создавайте область.

## Различия провайдеров

`UseLoggerFactory` и `LogSensitiveData` ведут себя одинаково во всех провайдерах; различается только расширение `Use…`, выбирающее провайдер.

| Провайдер | Расширение | Перегрузки |
|---|---|---|
| SQLite | `UseSqlite` | `string filepath`, `DbConnection` |
| SQL Server | `UseSqlServer` | `string connectionString`, `DbConnection` |
| PostgreSQL | `UsePostgres` | `string connectionString`, `DbConnection` |
| In-memory | нет | зарегистрируйте `InMemoryContext` напрямую или назначьте `Factory` |

## См. также

* [Установка](01-installation.md)
* [Быстрый старт](02-quickstart.md)
* [Сущности и метаданные](03-entities-and-metadata.md)
* [Индекс документации](../index.md)

---

Source: `test/nextorm.core.tests/DependencyInjectionTests.cs:13`,
`test/nextorm.core.tests/DependencyInjectionTests.cs:43`,
`test/nextorm.core.tests/DependencyInjectionTests.cs:58`,
`test/nextorm.core.tests/DependencyInjectionTests.cs:71`;
`src/nextorm.core/DI/ServiceCollectionExtensions.cs:73`;
`src/nextorm.core/DI/DataContextOptionsBuilder.cs:20`;
`src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.sqlserver/DI/DataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs:8`.
