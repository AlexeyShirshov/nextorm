# Справочник по API

> Курируемый указатель публичных типов nextorm, сгруппированных по пространству имён, с исходным файлом, которому принадлежит каждый из них.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Provider overview](../providers/overview.md)

## Обзор

Это **курируемый** указатель; справочник, сгенерированный из комментариев XML doc на исходных типах,
опубликован в разделе **API reference** этого сайта (см. верхнюю навигацию). Каждая запись ниже даёт тип,
однострочное описание и путь к исходникам для чтения полного контракта.

Типы перечислены под своим определяющим пространством имён. Весь API запросов находится в `nextorm.core`; каждый пакет провайдера
добавляет контекст, диалект и класс расширения `DbContextBuilder` в своём собственном пространстве имён.

## Пространство имён `nextorm.core`

### Контекст и роли

| Тип | Описание | Источник |
|---|---|---|
| `IDataContext` | Составной фасад над ролями контекста; точка входа, от которой обычно зависят потребители. | `src/nextorm.core/DataContext/IDataContext.cs` |
| `IQueryExecutor` | Выполняет подготовленную команду и материализует её результат (терминалы). | `src/nextorm.core/DataContext/Roles/IQueryExecutor.cs` |
| `IQueryMaterializer` | Самый узкий контракт для планирования + чтения строк, без терминалов. | `src/nextorm.core/DataContext/Roles/IQueryMaterializer.cs` |
| `IQueryPlanner` | Строит/сбрасывает планы выполнения и разрешает источники `From`. | `src/nextorm.core/DataContext/Roles/IQueryPlanner.cs` |
| `IRowReaderFactory` | Создаёт читатели строк/перечислители над подготовленной командой. | `src/nextorm.core/DataContext/Roles/IRowReaderFactory.cs` |
| `IQueryCache` | Хранит кэшированные планы и общий план `Any`. | `src/nextorm.core/DataContext/Roles/IQueryCache.cs` |
| `IContextEnvironment` | Окружающее состояние: логгеры, режим отображения, набор свойств. | `src/nextorm.core/DataContext/Roles/IContextEnvironment.cs` |
| `IConnectionManager` | Владеет жизненным циклом соединения (не является частью `IDataContext`; провайдер in-memory его не реализует). | `src/nextorm.core/DataContext/Roles/IConnectionManager.cs` |
| `InMemoryContext` | Встроенный in-memory `IDataContext` по коллекциям CLR. | `src/nextorm.core/DataContext/InMemoryDataContext.cs` |

### Построители запросов

| Тип | Описание | Источник |
|---|---|---|
| `EntityBuilder<TEntity>` | Fluent, неизменяемый построитель запросов для отображённого типа сущности. | `src/nextorm.core/Builders/EntityBuilder.cs` |
| `EntityBuilder` | Fluent-построитель для источника псевдонима/таблицы без типа сущности (режим `TableAlias`). | `src/nextorm.core/Builders/EntityBuilder.cs` |
| `EntityP2<T1,T2>` … `EntityP8<T1..T8>` | Накопительные построители соединений; арность от 2 до 8. | `src/nextorm.core/Builders/Joins/JoinCommandBuilder.cs` |
| `EntityMetadataBuilder<T>` | Fluent-конфигурация метаданных сущности, используемая `IDataContext.From<T>(...)`. | `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs` |
| `Projection<T1,T2>` … `Projection<T1..T8>` | Форма результата соединённого запроса; предоставляет `t1`…`t8`. | `src/nextorm.core/Builders/Projection.cs` |
| `IProjection` / `IExtendableProjection` | Маркеры для накопленных проекций соединений (арность 8 не расширяема). | `src/nextorm.core/Builders/Projection.cs` |
| `CteQuery` | Fluent-область, собирающая объявления `WITH`. | `src/nextorm.core/Builders/CteQuery.cs` |
| `CteDefinition` | Один CTE: имя, определяющий запрос, флаг рекурсии и необязательная максимальная рекурсия. | `src/nextorm.core/Builders/CteQuery.cs` |
| `TableAlias` / `TableColumn` | Аксессоры столбцов в режиме псевдонима и типизированная обёртка столбца (`AsInt`, `AsString`, …). | `src/nextorm.core/Builders/TableAlias.cs` |
| `Paging` | Значение `Limit` / `Offset`, используемое каждым построителем запросов. | `src/nextorm.core/Builders/Paging.cs` |

### Команды, планы и функции

| Тип | Описание | Источник |
|---|---|---|
| `QueryCommand` | Необобщённая команда запроса, хранящая план/состояние, общие для всех результатов. | `src/nextorm.core/Query/QueryCommand.cs` |
| `QueryCommand<TResult>` | Типизированная команда запроса с терминалами (`ToList`, `First`, `Union`, `Distinct`, `Hint`, `ForJson`, `ForXml`, `WithTableHint`, `Prepare`, …). | `src/nextorm.core/Query/QueryCommand.TResult.cs` |
| `IPreparedQueryCommand<TResult>` | Подготовленная команда; её члены по умолчанию выполняют её против переданного `IDataContext`. | `src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs` |
| `NORM` | Статическая точка входа: `NORM.SQL` и `NORM.Param<T>(idx)`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM_SQL` | Поверхность SQL-функций: `exists`, `like`, `@in`, `any`/`all` (подзапрос и массив), агрегаты (включая агрегаты с `FILTER` и `string_agg`/`array_agg`), оконные функции, `nullif`/`greatest`/`least`/`date_trunc`/`date_add`/`date_diff`/`date_from_parts`/`end_of_month`, функции массивов и JSON/JSONB PostgreSQL, текстовые JSON-функции SQL Server (`json_value`/`json_query`/`json_modify`/`isjson`) и предикаты полнотекстового поиска (`contains`/`freetext`), а также табличные функции `generate_series`/`unnest`/`string_split`/`openjson`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM.WindowFunction<T>` | Незавершённый вызов окна; завершите его с помощью `Over(...)`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM.WindowOrder` | Упорядоченный ключ окна плюс `OrderDirection`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM.WindowFrame`, `WindowFrameBound`, `WindowFrameType`, `WindowFrameBoundKind` | Спецификация фрейма `ROWS`/`RANGE` и его границы. | `src/nextorm.core/Query/NORM.cs` |
| `DataContextExtensions` | Независимые от провайдера помощники: `From<T>`, `From`, `FromTableFunction`, терминалы подготовленных команд. | `src/nextorm.core/DataContext/DataContextExtensions.cs` |
| `IDataContextExtensions` | Точки входа CTE `With` / `WithRecursive`. | `src/nextorm.core/DataContext/IDataContextExtensions.cs` |

### Атрибуты отображения

| Тип | Описание | Источник |
|---|---|---|
| `SqlTableAttribute` | Отображает класс или интерфейс на имя таблицы (`[SqlTable("name")]`). | `src/nextorm.core/TableAttribute.cs` |
| `SqlFunctionAttribute` | Отображает метод CLR (или его объявляющий тип) на скалярную функцию базы данных; необязательные `Name`/`Schema`. | `src/nextorm.core/SqlFunctionAttribute.cs` |
| `SqlTableFunctionAttribute` | Отображает статический метод (или его объявляющий тип) на табличную функцию, используемую как источник `FROM`. | `src/nextorm.core/SqlTableFunctionAttribute.cs` |

### Вспомогательные типы выражений

| Тип | Описание | Источник |
|---|---|---|
| `OrderDirection` | `Asc` / `Desc`. | `src/nextorm.core/Expressions/OrderDirection.cs` |
| `JoinType` | `Inner`, `Left`, `Right`, `Full`, `Cross`, `FullCross`, `CrossApply`, `OuterApply`. | `src/nextorm.core/Expressions/JoinExpression.cs` |
| `JoinExpression` | Одно соединение: условие, тип и присоединяемый источник. | `src/nextorm.core/Expressions/JoinExpression.cs` |
| `FromExpression` / `SelectExpression` | Источник FROM и метаданные проецируемого столбца. | `src/nextorm.core/Expressions/FromExpression.cs`, `src/nextorm.core/Expressions/SelectExpression.cs` |
| `UnionType` | `None`, `Distinct`, `All`, `Intersect`, `IntersectAll`, `Except`, `ExceptAll`. | `src/nextorm.core/Expressions/UnionType.cs` |

### Внедрение зависимостей

| Тип | Описание | Источник |
|---|---|---|
| `DbContextBuilder` | Построитель параметров провайдера: `UseLoggerFactory`, `LogSensitiveData`, `Factory`, `CreateDbContext`. | `src/nextorm.core/DI/DataContextOptionsBuilder.cs` |
| `ServiceCollectionExtensions` | `AddNextOrmContext` / `AddKeyedNextOrmContext` (универсальные и управляемые options). | `src/nextorm.core/DI/ServiceCollectionExtensions.cs` |

## Пространство имён `nextorm.sqlite`

| Тип | Описание | Источник |
|---|---|---|
| `SqliteDbContext` | `DbContext` поверх `Microsoft.Data.Sqlite`; регистрирует пользовательские агрегаты. | `src/nextorm.sqlite/SqliteDbContext.cs` |
| `SqliteDialect` | Синглтон `ISqlDialect` для SQLite (`SqliteDialect.Instance`). | `src/nextorm.sqlite/SqliteDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseSqlite(string filepath)` и `UseSqlite(DbConnection)`. | `src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs` |

## Пространство имён `nextorm.postgres`

| Тип | Описание | Источник |
|---|---|---|
| `PostgresDbContext` | `DbContext` поверх `Npgsql`. | `src/nextorm.postgres/PostgresDbContext.cs` |
| `PostgresDialect` | Синглтон `ISqlDialect` для PostgreSQL (`PostgresDialect.Instance`). | `src/nextorm.postgres/PostgresDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UsePostgres(string connectionString)` и `UsePostgres(DbConnection)`. | `src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs` |

## Пространство имён `nextorm.sqlserver`

| Тип | Описание | Источник |
|---|---|---|
| `SqlServerDbContext` | `DbContext` поверх `Microsoft.Data.SqlClient`, с преобразованием числовых столбцов. | `src/nextorm.sqlserver/SqlServerDbContext.cs` |
| `SqlServerDialect` | Синглтон `ISqlDialect` для SQL Server (`SqlServerDialect.Instance`). | `src/nextorm.sqlserver/SqlServerDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseSqlServer(string connectionString)` и `UseSqlServer(DbConnection)`. | `src/nextorm.sqlserver/DI/DataContextOptionsBuilderExtensions.cs` |

## Пространство имён `nextorm.mysql`

| Тип | Описание | Источник |
|---|---|---|
| `MySqlDbContext` | `DbContext` поверх `MySqlConnector`. | `src/nextorm.mysql/MySqlDbContext.cs` |
| `MySqlDialect` | Синглтон `ISqlDialect` для MySQL (`MySqlDialect.Instance`); не `sealed`, чтобы MariaDB мог наследоваться. | `src/nextorm.mysql/MySqlDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseMySql(string connectionString)` и `UseMySql(DbConnection)`. | `src/nextorm.mysql/DI/DataContextOptionsBuilderExtensions.cs` |

## Пространство имён `nextorm.mariadb`

| Тип | Описание | Источник |
|---|---|---|
| `MariaDbContext` | `DbContext` поверх `MySqlConnector`, наследуется от `MySqlDbContext`. | `src/nextorm.mariadb/MariaDbContext.cs` |
| `MariaDbDialect` | Синглтон `ISqlDialect` для MariaDB (`MariaDbDialect.Instance`); отрисовка MySQL плюс `INTERSECT ALL`/`EXCEPT ALL`. | `src/nextorm.mariadb/MariaDbDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseMariaDb(string connectionString)` и `UseMariaDb(DbConnection)`. | `src/nextorm.mariadb/DI/DataContextOptionsBuilderExtensions.cs` |

## Пространство имён `nextorm.clickhouse`

| Тип | Описание | Источник |
|---|---|---|
| `ClickHouseDbContext` | `DbContext` поверх официального ADO.NET-провайдера `ClickHouse.Driver`. | `src/nextorm.clickhouse/ClickHouseDbContext.cs` |
| `ClickHouseDialect` | Синглтон `ISqlDialect` для ClickHouse (`ClickHouseDialect.Instance`). | `src/nextorm.clickhouse/ClickHouseDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseClickHouse(string connectionString)` и `UseClickHouse(DbConnection)`. | `src/nextorm.clickhouse/DI/DataContextOptionsBuilderExtensions.cs` |

## См. также

- [Provider overview](../providers/overview.md)
- [SQLite](../providers/sqlite.md)
- [SQL Server](../providers/sqlserver.md)
- [PostgreSQL](../providers/postgres.md)
- [MySQL](../providers/mysql.md)
- [MariaDB](../providers/mariadb.md)
- [ClickHouse](../providers/clickhouse.md)
- [In-memory](../providers/in-memory.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.sqlite/**`, `src/nextorm.postgres/**`,
`src/nextorm.sqlserver/**`, `src/nextorm.mysql/**`, `src/nextorm.mariadb/**`,
`src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
