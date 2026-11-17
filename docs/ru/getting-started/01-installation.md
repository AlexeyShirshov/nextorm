# Установка

Установите NextORM и пакеты провайдеров баз данных, которые нужны вашему приложению, а затем подготовьтесь к [быстрому старту](02-quickstart.md). В быстром старте используется SQLite. На этой странице описана установка пакетов; подключение к реальной базе данных и синтаксис запросов здесь не рассматриваются.

## Перед началом

- Проект, нацеленный на `net10.0` или `net11.0`.
- Терминал, открытый в каталоге с файлом проекта.

## Установка основного пакета

Из каталога проекта добавьте основной пакет:

```bash
dotnet add package nextorm
```

Добавьте пакет провайдера для нужной базы данных. В быстром старте используется SQLite:

```bash
dotnet add package nextorm.sqlite
```

Чтобы выполнить быстрый старт, в файле проекта должны быть ссылки на `nextorm` и `nextorm.sqlite`.
Если приложение работает с другими базами данных, добавьте соответствующие пакеты провайдеров. Их
идентификаторы приведены в [справочнике пакетов](#ссылка-на-пакет).

Если вы используете консоль диспетчера пакетов Visual Studio, выберите проект приложения и
выполните команды:

```powershell
Install-Package nextorm
Install-Package nextorm.sqlite
```

## Необязательные пакеты

Для быстрого старта достаточно основного пакета и пакета провайдера. Добавьте `nextorm.extensions`,
только если вам нужны его операторы-компоненты запроса:

```bash
dotnet add package nextorm.extensions
```

Пакет добавляет `SelectWhereMin` / `SelectWhereMax` и `SelectGroup` / `SelectGroupHaving`. Он работает
со всеми провайдерами; см. [Пакет расширений](../advanced/extensions-package.md).

Если приложение уже использует `DbContext` Entity Framework Core и запросам NextORM нужно то же
соединение и транзакция, добавьте необязательный пакет-мост:

```bash
dotnet add package nextorm.entityframeworkcore
```

Подробности приведены в разделе [Интеграция с EF Core](../advanced/integration-efcore.md).

## Обзор

NextORM отделяет движок запросов от драйверов баз данных:

* `nextorm` (сборка `nextorm.core`) содержит построитель и компилятор запросов, метаданные сущностей,
  средства внедрения зависимостей, кэш планов и провайдер in-memory
  ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)). Пакет ссылается на
  `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` и
  `Microsoft.Extensions.ObjectPool`, но не на драйвер базы данных.
* Каждый пакет провайдера добавляет контекст и расширение регистрации `Use…`. Пакет провайдера
  транзитивно зависит от `nextorm`.

Версия `1.0.9` — первый стабильный выпуск, поэтому для стабильных пакетов не нужен флаг
`--prerelease`. Для пререлизных сборок (`1.0.10-a.x` и новее) он по-прежнему нужен.

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

Пакеты поддерживают `net10.0` и `net11.0`; NuGet выбирает сборку для целевой платформы проекта.
Примеры предполагают, что включены неявные using-директивы и nullable reference types. Если эти
свойства ещё не заданы, добавьте в `.csproj` следующий блок:

```xml
<PropertyGroup>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

Ссылка на пакет через `PackageReference` выглядит так:

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
