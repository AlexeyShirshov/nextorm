# Установка

> Добавьте основной пакет `nextorm` и один пакет провайдера базы данных в проект, нацеленный на `net10.0` или `net11.0`, затем используйте типы `nextorm.core` из кода.

**Предварительные требования:** проект, нацеленный на `net10.0` или `net11.0`.

## Обзор

NextORM разделён на небольшой основной пакет, не зависящий от драйвера, и по одному пакету на каждый реляционный провайдер:

* `nextorm` (сборка `nextorm.core`) содержит построитель и компилятор запросов, метаданные сущностей,
  вспомогательные методы внедрения зависимостей, кэш планов **и провайдер in-memory** ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)). Он
  ссылается на `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` и
  `Microsoft.Extensions.ObjectPool`, но не на драйвер базы данных.
* `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb` и
  `nextorm.clickhouse` добавляют каждый свой конкретный контекст
  ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext),
  [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext)) и расширение регистрации `Use…`.
  Пакеты провайдеров транзитивно зависят от `nextorm`.
* `nextorm.extensions` — необязательный продуктовый пакет, который добавляет операторы-компоненты
  запроса `SelectWhereMin` / `SelectWhereMax` и `SelectGroup` / `SelectGroupHaving` поверх публичного
  слоя компонентов запроса. Он ссылается только на `nextorm.core`, не требует собственного пакета
  провайдера и работает с любым провайдером; см. [Пакет расширений](../advanced/extensions-package.md).
* `nextorm.entityframeworkcore` — необязательный пакет-мост, который выполняет запросы и команды
  nextorm поверх существующего соединения и транзакции EF Core `DbContext`, переиспользуя маппинг
  модели EF (`db.CreateNextOrmContext()`); см. [Интеграция с EF Core](../advanced/integration-efcore.md).

1.0.9 — первый стабильный релиз, поэтому в командах установки ниже флаг `--prerelease` больше не
нужен. Для пререлизных сборок (`1.0.10-a.x` и новее) он по-прежнему обязателен.

## Установка основного пакета

```bash
dotnet add package nextorm
```

Затем добавьте ровно один пакет провайдера для целевой базы данных:

```bash
dotnet add package nextorm.sqlite
dotnet add package nextorm.sqlserver
dotnet add package nextorm.postgres
dotnet add package nextorm.mysql
dotnet add package nextorm.mariadb
dotnet add package nextorm.clickhouse
```

Пакет `nextorm.extensions` необязателен. Добавляйте его, когда нужны компонентные
`SelectWhereMin` / `SelectWhereMax` или группировка, выведенная из проекции, — `SelectGroup` /
`SelectGroupHaving`; пакет работает с любым провайдером:

```bash
dotnet add package nextorm.extensions
```

Пакет `nextorm.entityframeworkcore` тоже необязателен — он нужен, когда в приложении уже есть
`DbContext` EF Core и запросы nextorm должны идти по тому же соединению:

```bash
dotnet add package nextorm.entityframeworkcore
```

Эквивалент в консоли диспетчера пакетов:

```powershell
Install-Package nextorm
Install-Package nextorm.sqlite
```

## Ссылка на пакет

| Пакет | Идентификатор пакета | Добавляет | Драйвер |
|---|---|---|---|
| Основной | `nextorm` | Движок запросов, метаданные, DI, провайдер in-memory | — |
| SQLite | `nextorm.sqlite` | [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `Microsoft.Data.Sqlite` |
| SQL Server | `nextorm.sqlserver` | [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions.UseSqlServer(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `Microsoft.Data.SqlClient` |
| PostgreSQL | `nextorm.postgres` | [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions.UsePostgres(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `Npgsql` |
| MySQL | `nextorm.mysql` | [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`UseMySql`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions.UseMySql(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `MySqlConnector` |
| MariaDB | `nextorm.mariadb` | [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext), [`UseMariaDb`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions.UseMariaDb(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `MySqlConnector` |
| ClickHouse | `nextorm.clickhouse` | [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext), [`UseClickHouse`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions.UseClickHouse(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `ClickHouse.Driver` |
| Расширения (необязательно) | `nextorm.extensions` | [`SelectWhereMin`](xref:NextORM.Extensions.ExtremeRowExtensions) / [`SelectWhereMax`](xref:NextORM.Extensions.ExtremeRowExtensions) и [`SelectGroup`](xref:NextORM.Extensions.SelectGroupExtensions) / [`SelectGroupHaving`](xref:NextORM.Extensions.SelectGroupExtensions) поверх `nextorm.core` | — |
| EF Core (необязательно) | `nextorm.entityframeworkcore` | [`CreateNextOrmContext`](xref:NextORM.EntityFrameworkCore.EntityFrameworkCoreExtensions.CreateNextOrmContext(Microsoft.EntityFrameworkCore.DbContext,System.Action{NextORM.Core.DataContextBuilder})) — запросы nextorm поверх существующего соединения и транзакции `DbContext` | `Microsoft.EntityFrameworkCore.Relational` |

Провайдер in-memory находится в основном пакете, поэтому доступен без установки провайдера.

## Целевая платформа

Пакеты мультитаргетят `net10.0` и `net11.0`, поэтому NuGet сам подбирает ассет под целевую платформу потребляющего проекта, и включают неявные using-директивы (implicit usings) и nullable reference types. `PackageReference` выглядит так:

```xml
<ItemGroup>
  <PackageReference Include="nextorm" Version="1.0.9" />
  <PackageReference Include="nextorm.sqlite" Version="1.0.9" />
</ItemGroup>
```

Для пререлизной сборки подтяните последний пререлиз через `Version="*-*"`.

## Что читать дальше

* [Быстрый старт](02-quickstart.md) - полная минимальная программа.
* [Сущности и метаданные](03-entities-and-metadata.md) - атрибуты, отображение интерфейс/класс и
  режим [`TableAlias`](xref:NextORM.Core.TableAlias) без сущностей.
* [Внедрение зависимостей](04-dependency-injection.md) - регистрация контекста с помощью [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder}))
  и методы `Use…` провайдеров.
* [Пакет расширений](../advanced/extensions-package.md) - необязательные операторы
  `nextorm.extensions` и когда использовать их вместо встроенных.

---

Source: `docs/index.md:91`; `src/nextorm.core/nextorm.core.csproj:4`;
`src/nextorm.sqlite/nextorm.sqlite.csproj:7`; `src/nextorm.sqlserver/nextorm.sqlserver.csproj:16`;
`src/nextorm.postgres/nextorm.postgres.csproj:8`;
in-memory availability: `tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:16`.
