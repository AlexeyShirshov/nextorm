# Nextorm - high performance zero-sql object-relational mapping (ORM) library

## Documentation

### Getting started

- [Installation](getting-started/01-installation.md)
- [Quickstart](getting-started/02-quickstart.md)
- [Entities and metadata](getting-started/03-entities-and-metadata.md)
- [Dependency injection](getting-started/04-dependency-injection.md)

### Guide

- [Querying and projections](guide/01-querying-and-projections.md)
- [Filtering (WHERE)](guide/02-filtering-where.md)
- [Joins](guide/03-joins.md)
- [Grouping and aggregates](guide/04-grouping-and-aggregates.md)
- [Sorting and paging](guide/05-sorting-and-paging.md)
- [Subqueries](guide/06-subqueries.md)
- [Set operations](guide/07-set-operations.md)
- [SELECT DISTINCT](guide/08-distinct.md)
- [Common table expressions (CTE)](guide/09-cte.md)
- [Window functions](guide/10-window-functions.md)
- [User-defined functions](guide/12-user-defined-functions.md)
- [Table-valued functions](guide/13-table-valued-functions.md)
- [Raw SQL](guide/14-raw-sql.md)
- [Query reuse: cache vs Prepare](guide/15-query-reuse.md)
- [Connections and logging](guide/16-connections-and-logging.md)
- [Query hints](guide/17-query-hints.md)
- [JSON support across providers](guide/18-json.md)
- [Data modification (INSERT)](guide/19-insert-statement.md)
- [Data modification (DELETE)](guide/20-delete-statement.md)
- [Data modification (UPDATE)](guide/21-update-statement.md)
- [Materializing a query into a table](guide/22-create-table-as.md)
- [Data merging (MERGE / upsert)](guide/23-merge-statement.md)
- [Bulk insert](guide/24-bulk-insert.md)
- [Transactions](guide/25-transactions.md)
- [Duration (TimeSpan) columns](guide/26-duration-columns.md)
- [Interceptors](guide/27-interceptors.md)
- [Executing statements in one batch](guide/28-sql-batch.md)
- [Optimistic concurrency and change tracking](guide/29-optimistic-concurrency.md)
- [Value converters and JSON columns](guide/30-value-converters.md)
- [Range columns (pair of scalar columns)](guide/31-range-columns.md)

### Scalar functions

- [Overview](scalar-functions/index.md)
- [String functions](scalar-functions/01-string-functions.md)
- [Math functions](scalar-functions/02-math-functions.md)
- [Date and time](scalar-functions/03-date-and-time.md)
- [Conditional and conversion helpers](scalar-functions/04-conditionals-and-conversion.md)
- [Aggregates](scalar-functions/05-aggregates.md)
- [Arrays](scalar-functions/06-arrays.md)
- [JSON and XML](scalar-functions/07-json-and-xml.md)
- [Provider reference](scalar-functions/08-reference.md)

### Providers

- [Provider overview](providers/overview.md)
- [SQLite](providers/sqlite.md)
- [SQL Server](providers/sqlserver.md)
- [PostgreSQL](providers/postgres.md)
- [In-memory](providers/in-memory.md)

### Comparisons

- [Nextorm vs Dapper, linq2db and EF Core](comparisons/index.md)

### Advanced

- [Limitations and out-of-scope features](advanced/limitations.md)
- [API reference](advanced/api-reference.md)

### Русская документация

- [Обзор](ru/overview.md)
- [Постановка задачи](ru/motivation.md)
- [Быстрый старт (англ.)](getting-started/02-quickstart.md)

Полное руководство на английском: [Getting started](getting-started/01-installation.md) ·
[Guide](#guide) · [Providers](#providers). Русский перевод в работе.

## Overview

Nextorm perform two main functions:

- Generate SQL code
- Map relational data into language (or rather a framework) structures such as classes, primitive types, arrays, lists, etc.

Nextorm uses protocol-level libraries (for example, SqlClient for Microsoft SQL Server or SQLite for SQLite) and designed to create a high performance data access layer independent of SQL and specific RDBMS.

## Status

The current status (1.0.7-beta) is a prof of concept.

## Roadmap

- [1.0.2-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.2)
- [1.0.3-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.3)
- [1.0.4-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.4)
- [1.0.5-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.5)
- [1.0.6-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.6)
- [1.0.7-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-b.1)
- [1.0-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-rc.1)
- [1.0](https://github.com/AlexeyShirshov/nextorm/milestones/1.0)
- [1.1.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.1-a.1)
- [1.1-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.1-b.1)
- [1.1-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.1-rc.1)
- [1.1](https://github.com/AlexeyShirshov/nextorm/milestones/1.1)
- [1.2.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.2-a.1)
- [1.2-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.2-b.1)
- [1.2-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.2-rc.1)
- [1.2](https://github.com/AlexeyShirshov/nextorm/milestones/1.2)
- [1.3.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.3-a.1)
- [1.3-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.3-b.1)
- [1.3-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.3-rc.1)
- [1.3](https://github.com/AlexeyShirshov/nextorm/milestones/1.3)
- [1.4.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.4-a.1)
- [1.4-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.4-b.1)
- [1.4-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.4-rc.1)
- [1.4](https://github.com/AlexeyShirshov/nextorm/milestones/1.4)

## Installation

- from cli `dotnet add package nextorm`
- from package manager `Install-Package nextorm`

Don't forget to add `--prerelease` flag since all current versions is not stable.
To add specific database provider use the following:

- `dotnet add package nextorm.sqlserver`
- `dotnet add package nextorm.sqlite`
- `dotnet add package nextorm.postgres`
- `dotnet add package nextorm.mysql`
- `dotnet add package nextorm.mariadb`
- `dotnet add package nextorm.clickhouse`

In-memory provider is built-in in core library.

## Query reuse

There are two independent ways to avoid re-building a query plan on every execution: the implicit plan
cache (used automatically by [`EntityBuilder`](xref:NextORM.Core.EntityBuilder)/[`QueryCommand`](xref:NextORM.Core.QueryCommand) terminals) and explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) returning an
[`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1).

They differ in cost, lifetime and thread-safety rules. Which one to use, what each one costs per call and
its limitations are covered in the [Query reuse guide](guide/15-query-reuse.md).

## Releases

### 1.0.7-beta

- [`OUTPUT INTO`, несколько result-set'ов, upsert-with-output](https://github.com/AlexeyShirshov/nextorm/issues/15)
- [Value converters — `EnumToStringConverter`, конвертеры в предикатах и проекциях](https://github.com/AlexeyShirshov/nextorm/issues/31)
- [Динамическая схема результата (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`)](https://github.com/AlexeyShirshov/nextorm/issues/63)
- [JSON-колонка ↔ CLR-объект (авто-сериализация свойства)](https://github.com/AlexeyShirshov/nextorm/issues/64)
- [PostgreSQL range-типы и `Overlaps` (`&&`)](https://github.com/AlexeyShirshov/nextorm/issues/66)
- [SQL Server 2025 — `regexp_like` / `regexp_replace`](https://github.com/AlexeyShirshov/nextorm/issues/85)
- [Портативный range как пара колонок — `[RangeColumns]`](https://github.com/AlexeyShirshov/nextorm/issues/86)
- [Скалярные функции: кросс-провайдерный фасад `SqlFunctions.Sql` и пробелы по провайдерам](https://github.com/AlexeyShirshov/nextorm/issues/87)
- [Value converters — фаза 2 и JSON-колонки](https://github.com/AlexeyShirshov/nextorm/issues/88)
- [In-memory функции, структурный ключ плана запроса, dictionary lookup](https://github.com/AlexeyShirshov/nextorm/issues/89)
- Новые главы руководства EN + RU: конвертеры значений и JSON-колонки, range-колонки
- [Спеки, регистры аудита, документация EN + RU](https://github.com/AlexeyShirshov/nextorm/issues/90)

### 1.0.6-alpha

- [column collation — `Collate(...)` в маппинге и запросах](https://github.com/AlexeyShirshov/nextorm/issues/28)
- [Interceptors — `IQueryInterceptor` / `IConnectionInterceptor` (наблюдение за командами и соединениями)](https://github.com/AlexeyShirshov/nextorm/issues/30)
- [Трансляция Regex в запросах — `Regex.IsMatch` / `Regex.Replace`](https://github.com/AlexeyShirshov/nextorm/issues/68)
- [C# string-семантика — ordinal-сравнения, format-спецификаторы, culture](https://github.com/AlexeyShirshov/nextorm/issues/71)
- [TimeSpan/interval-колонки и точность дат — атрибут `[Duration]`](https://github.com/AlexeyShirshov/nextorm/issues/72)
- [DDL/DML + читающий запрос в одном SQL-батче (pgbouncer-safe CTAS)](https://github.com/AlexeyShirshov/nextorm/issues/75)
- [Кросс-провайдерные строковые/числовые обёртки `SqlFunctions.Sql`](https://github.com/AlexeyShirshov/nextorm/issues/77)
- [ClickHouse: пробелы функций (`lowerUTF8`/`upperUTF8`, `trim*`, `replaceRegexp*`, `map*`, массивы, `groupBitmap`/`sumMap`, хэши, `generateULID`)](https://github.com/AlexeyShirshov/nextorm/issues/78)
- [MariaDB: пробелы функций (`NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`, `TO_CHAR`/`TO_DATE`/`TO_NUMBER`, `KDF`, `XXH3`, `JSON_DETAILED`/`JSON_COMPACT`, последовательности)](https://github.com/AlexeyShirshov/nextorm/issues/79)
- [MySQL: пробелы функций (`FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `STR_TO_DATE`, `DATE_FORMAT`, `FROM_UNIXTIME`, JSON-mutation, `UUID_TO_BIN`)](https://github.com/AlexeyShirshov/nextorm/issues/80)
- [PostgreSQL: пробелы функций (`sha224/384/512`, `regexp_substr`, `make_*`, `age`, `date_bin`, `current_setting`, последовательности, SQL/JSON)](https://github.com/AlexeyShirshov/nextorm/issues/81)
- [SQLite: пробелы функций (JSON1, `printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`, `typeof`, `glob`, `unicode`/`char`, `timediff`, `Math.*`)](https://github.com/AlexeyShirshov/nextorm/issues/82)
- [SQL Server: пробелы функций (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `TRANSLATE`, `FORMAT`, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, JSON-агрегаты)](https://github.com/AlexeyShirshov/nextorm/issues/83)
- Новые главы руководства EN + RU: длительности (`[Duration]`), interceptors, SQL-батчи, оптимистичный параллелизм

### 1.0.5-alpha

- [INSERT: одиночная и многострочная вставка значений и сущностей, возврат сгенерированного ключа](https://github.com/AlexeyShirshov/nextorm/issues/3)
- [UPDATE: присваивания по колонкам, фильтр WHERE и обновление сущности по ключу](https://github.com/AlexeyShirshov/nextorm/issues/4)
- [DELETE: удаление по предикату и по объявленному ключу](https://github.com/AlexeyShirshov/nextorm/issues/5)
- [MERGE: key upsert (ON CONFLICT / ON DUPLICATE KEY) и полный MERGE с WHEN MATCHED / WHEN NOT MATCHED](https://github.com/AlexeyShirshov/nextorm/issues/6)
- [Транзакции: созданные nextorm и переданные извне (ADO.NET / EF Core)](https://github.com/AlexeyShirshov/nextorm/issues/32)
- [CREATE TABLE ... AS SELECT (CTAS) и временные таблицы](https://github.com/AlexeyShirshov/nextorm/issues/60)
- Массовая вставка (bulk): values / сущности / DataTable, пакетами, с RETURNING / OUTPUT
- Новые главы руководства EN + RU: INSERT/DELETE/UPDATE/MERGE, CTAS, bulk insert, транзакции

### 1.0.4-alpha

- [ClickHouse: закрыт остаток backlog — UInt64 row reader, серверные/кластерные TVF, нативный JSON](https://github.com/AlexeyShirshov/nextorm/issues/55)
- [ClickHouse: массивы Array(T)/Tuple — row reader, array-агрегаты и higher-order (lambda) функции](https://github.com/AlexeyShirshov/nextorm/issues/56)
- [ClickHouse: join kinds SEMI/ANTI/PASTE — JoinType.Semi/Anti/Paste, SemiJoin/AntiJoin/PasteJoin](https://github.com/AlexeyShirshov/nextorm/issues/57)
- [Типизированный доступ к колонке по имени — SqlFunctions.Column<T>](https://github.com/AlexeyShirshov/nextorm/issues/58)
- Полная XML-документация публичного API во всех пакетах

### 1.0.3.1-alpha

- Hotfix for a regression introduced in [1.0.3-alpha](#103-alpha): a derived query whose projection references a
  source from a nested command rendered an incomplete column list (invalid SQL, or `Operation is not valid due to
  the current state of the object`). Affected PostgreSQL, SQL Server, MySQL/MariaDB and ClickHouse.

### 1.0.3-alpha

- [Table-valued functions](https://github.com/AlexeyShirshov/nextorm/issues/8)
- [Scalar-valued functions](https://github.com/AlexeyShirshov/nextorm/issues/9)
- [Table hints](https://github.com/AlexeyShirshov/nextorm/issues/14)
- [Benchmark with Dapper and EF](https://github.com/AlexeyShirshov/nextorm/issues/17)
- [Новые возможности SQL-генерации](https://github.com/AlexeyShirshov/nextorm/issues/19)
- [PostgreSQL support](https://github.com/AlexeyShirshov/nextorm/issues/21)
- [MySQL support](https://github.com/AlexeyShirshov/nextorm/issues/22)
- [SQL functions](https://github.com/AlexeyShirshov/nextorm/issues/33)
- [ClickHouse support](https://github.com/AlexeyShirshov/nextorm/issues/49)

### 1.0.2-alpha

- [Aggregates (count, min, max, avg, sum, stdev, var)](https://github.com/AlexeyShirshov/nextorm/issues/12)
- [Grouping (GROUP BY / HAVING)](https://github.com/AlexeyShirshov/nextorm/issues/13)
- [Union](https://github.com/AlexeyShirshov/nextorm/issues/18)
- [Correlated subqueries](https://github.com/AlexeyShirshov/nextorm/issues/35)
- [Custom ExpressionVisitor](https://github.com/AlexeyShirshov/nextorm/issues/45)

### 1.0.1-alpha

- [Where clause](https://github.com/AlexeyShirshov/nextorm/issues/1)
- [Joins](https://github.com/AlexeyShirshov/nextorm/issues/2)
- [Paging](https://github.com/AlexeyShirshov/nextorm/issues/7)
- [Subqueries](https://github.com/AlexeyShirshov/nextorm/issues/10)
- [Sorting](https://github.com/AlexeyShirshov/nextorm/issues/11)
- [Microsoft SQL Server support](https://github.com/AlexeyShirshov/nextorm/issues/23)
- [In-memory support](https://github.com/AlexeyShirshov/nextorm/issues/24)
