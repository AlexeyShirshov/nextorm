# Установка

> Добавьте основной пакет `nextorm` и один пакет провайдера базы данных в проект, нацеленный на `net10.0`, затем используйте типы `nextorm.core` из кода.

**Предварительные требования:** проект, нацеленный на `net10.0`.

## Обзор

NextORM разделён на небольшой основной пакет, не зависящий от драйвера, и по одному пакету на каждый реляционный провайдер:

* `nextorm` (сборка `nextorm.core`) содержит построитель и компилятор запросов, метаданные сущностей,
  вспомогательные методы внедрения зависимостей, кэш планов **и провайдер in-memory** ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)). Он
  ссылается на `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`,
  `Microsoft.Extensions.ObjectPool` и `OneOf`, но не на драйвер базы данных.
* `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb` и
  `nextorm.clickhouse` добавляют каждый свой конкретный контекст
  ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext),
  [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext)) и расширение регистрации `Use…`.
  Пакеты провайдеров транзитивно зависят от `nextorm`.

Все текущие релизы являются пререлизами (линейка `1.0.1-alpha`), поэтому в каждой команде установки
нужно явно разрешить пререлизные версии.

## Установка основного пакета

```bash
dotnet add package nextorm --prerelease
```

Затем добавьте ровно один пакет провайдера для целевой базы данных:

```bash
dotnet add package nextorm.sqlite --prerelease
dotnet add package nextorm.sqlserver --prerelease
dotnet add package nextorm.postgres --prerelease
dotnet add package nextorm.mysql --prerelease
dotnet add package nextorm.mariadb --prerelease
dotnet add package nextorm.clickhouse --prerelease
```

Эквивалент в консоли диспетчера пакетов:

```powershell
Install-Package nextorm -Prerelease
Install-Package nextorm.sqlite -Prerelease
```

## Ссылка на пакет

| Пакет | Идентификатор пакета | Добавляет | Драйвер |
|---|---|---|---|
| Основной | `nextorm` | Движок запросов, метаданные, DI, провайдер in-memory | — |
| SQLite | `nextorm.sqlite` | [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions) | `Microsoft.Data.Sqlite` |
| SQL Server | `nextorm.sqlserver` | [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions) | `Microsoft.Data.SqlClient` |
| PostgreSQL | `nextorm.postgres` | [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions) | `Npgsql` |
| MySQL | `nextorm.mysql` | [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`UseMySql`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions) | `MySqlConnector` |
| MariaDB | `nextorm.mariadb` | [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext), [`UseMariaDb`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions) | `MySqlConnector` |
| ClickHouse | `nextorm.clickhouse` | [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext), [`UseClickHouse`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions) | `ClickHouse.Driver` |

Провайдер in-memory находится в основном пакете, поэтому доступен без установки провайдера.

## Целевая платформа

Пакеты нацелены на `net10.0` и включают неявные using-директивы (implicit usings) и nullable reference types. `PackageReference` выглядит так (версии приведены для примера; используйте последнюю пререлизную версию `1.0.1-alpha`):

```xml
<ItemGroup>
  <PackageReference Include="nextorm" Version="1.0.1-alpha" />
  <PackageReference Include="nextorm.sqlite" Version="1.0.1-alpha" />
</ItemGroup>
```

## Что читать дальше

* [Быстрый старт](02-quickstart.md) - полная минимальная программа.
* [Сущности и метаданные](03-entities-and-metadata.md) - атрибуты, отображение интерфейс/класс и
  режим [`TableAlias`](xref:NextORM.Core.TableAlias) без сущностей.
* [Внедрение зависимостей](04-dependency-injection.md) - регистрация контекста с помощью [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions)
  и методы `Use…` провайдеров.

---

Source: `docs/index.md:91`; `src/nextorm.core/nextorm.core.csproj:4`;
`src/nextorm.sqlite/nextorm.sqlite.csproj:7`; `src/nextorm.sqlserver/nextorm.sqlserver.csproj:16`;
`src/nextorm.postgres/nextorm.postgres.csproj:8`;
in-memory availability: `tests/nextorm.core.tests/DependencyInjectionTests.cs:16`.
