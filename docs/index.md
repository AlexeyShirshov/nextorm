# Nextorm - high performance zero-sql object-relational mapping (ORM) library

## Documentation

### Getting started

- [Installation](getting-started/01-installation.md)
- [Quickstart](getting-started/02-quickstart.md)
- [Entities and metadata](getting-started/03-entities-and-metadata.md)
- [Dependency injection](getting-started/04-dependency-injection.md)

### Engine

- [How the engine works](engine/index.md)
- [Query pipeline: fluent to SQL](engine/01-query-pipeline.md)
- [Execution and rows](engine/02-execution-and-rows.md)
- [Plan cache and reuse](engine/03-plan-cache-and-reuse.md)

### Relationships

- [Relationships and single-query loading](relationships/relationships.md)
- [Implicit navigation queries](relationships/implicit-navigation.md)
- [Eager loading child collections](relationships/eager-loading.md)
- [Per-parent paging in eager loading](relationships/eager-loading-per-parent-paging.md)

### Guide

- [Query sources](guide/01-query-sources.md)
- [Projections](guide/02-projections.md)
- [Filtering (WHERE)](guide/03-filtering-where.md)
- [Joins](guide/04-joins.md)
- [Grouping and aggregates](guide/05-grouping-and-aggregates.md)
- [Sorting and paging](guide/06-sorting-and-paging.md)
- [Subqueries](guide/07-subqueries.md)
- [Set operations](guide/08-set-operations.md)
- [SELECT DISTINCT](guide/09-distinct.md)
- [Common table expressions (CTE)](guide/10-cte.md)
- [Window functions](guide/11-window-functions.md)
- [User-defined functions](guide/12-user-defined-functions.md)
- [Table-valued functions](guide/13-table-valued-functions.md)
- [Raw SQL](guide/14-raw-sql.md)
- [Query hints](guide/15-query-hints.md)
- [JSON support across providers](guide/16-json.md)
- [Data modification (INSERT)](guide/17-insert-statement.md)
- [Data modification (DELETE)](guide/18-delete-statement.md)
- [Data modification (UPDATE)](guide/19-update-statement.md)
- [Materializing a query into a table](guide/20-create-table-as.md)
- [Data merging (MERGE / upsert)](guide/21-merge-statement.md)
- [Bulk insert](guide/22-bulk-insert.md)
- [Transactions](guide/23-transactions.md)
- [Duration (TimeSpan) columns](guide/24-duration-columns.md)
- [Executing statements in one batch](guide/25-sql-batch.md)
- [Optimistic concurrency and change tracking](guide/26-optimistic-concurrency.md)
- [Range columns (pair of scalar columns)](guide/27-range-columns.md)
- [Streaming large objects (BLOB/CLOB)](guide/28-large-objects.md)
- [Dynamic columns](guide/29-dynamic-columns.md)
- [Streaming data to a Stream](guide/30-streaming-data.md)
- [Provider-specific features](guide/31-provider-specific-features.md)

### Infrastructure

- [Connections](infrastructure/01-connections.md)
- [Interceptors](infrastructure/02-interceptors.md)
- [Value converters and JSON columns](infrastructure/03-value-converters.md)
- [Logging](infrastructure/04-logging.md)

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
- [System functions](scalar-functions/09-system-functions.md)
- [Sequences and UUID generators](scalar-functions/10-sequences.md)

### Connecting providers

- [Provider overview](connecting-providers/overview.md)
- [SQLite](connecting-providers/sqlite.md)
- [SQL Server](connecting-providers/sqlserver.md)
- [PostgreSQL](connecting-providers/postgres.md)
- [In-memory](connecting-providers/in-memory.md)

### Comparisons

- [Nextorm vs Dapper, linq2db and EF Core](comparisons/index.md)

### Advanced

- [Global query filters](advanced/query-filters.md)
- [Entity Framework Core integration](advanced/integration-efcore.md)
- [Limitations and out-of-scope features](advanced/limitations.md)
- [Native extreme-row strategies](advanced/select-where-extrema-native.md)
- [Extensibility: custom SQL dialects and renderers](advanced/extensibility.md)
- [API reference](advanced/api-reference.md)

### Русская документация

- [Обзор](ru/overview.md)
- [Постановка задачи](ru/motivation.md)
- [Быстрый старт (англ.)](getting-started/02-quickstart.md)

Полное руководство на английском: [Getting started](getting-started/01-installation.md) ·
[Guide](#guide) · [Connecting providers](#connecting-providers). Русский перевод в работе.

## Overview

Nextorm perform two main functions:

- Generate SQL code
- Map relational data into language (or rather a framework) structures such as classes, primitive types, arrays, lists, etc.

Nextorm uses protocol-level libraries (for example, SqlClient for Microsoft SQL Server or SQLite for SQLite) and designed to create a high performance data access layer independent of SQL and specific RDBMS.

## Status

The current status (1.0.9-rc3) is a prof of concept.

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
its limitations are covered in the [plan cache and reuse guide](engine/03-plan-cache-and-reuse.md).

## Releases

### 1.0.9-rc3

- [SQL Server: `OPTION (MAXRECURSION n)` больше не теряется при хоистинге рекурсивного CTE в источник мутации](https://github.com/AlexeyShirshov/nextorm/issues/275)
- [SQL Server: legacy `WithRecursive` с отличным `UNION` отклоняется fail-closed вместо невалидного T-SQL](https://github.com/AlexeyShirshov/nextorm/issues/265)
- [Perf: аллокации warm-path `Count`/`Sum` снижены (Count 2979.84 → 408 B/op, Sum 3389.44 → 970 B/op)](https://github.com/AlexeyShirshov/nextorm/issues/243)
- [Evidence pipeline: канонический global `pdca-dotnet` admission и свёртка `required_rows` в покрытие](https://github.com/AlexeyShirshov/nextorm/issues/242)
- [Единообразная обработка отмены при построении сортировки/группировки](https://github.com/AlexeyShirshov/nextorm/issues/241)
- [Perf: снижена стоимость конструирования fluent-команды (отдельный скоуп/дизайн)](https://github.com/AlexeyShirshov/nextorm/issues/240)
- [D233/T233: обновлены устаревшие `file:line` в `API-NAMING-REVIEW.md`](https://github.com/AlexeyShirshov/nextorm/issues/239)
- [PDCA skill: ложные `incomplete`/`STOP` из-за дополнительных audit-provenance gates больше не возникают](https://github.com/AlexeyShirshov/nextorm/issues/238)
- [Identifier safety: экранирование скобок в SQL Server и raw-имена `UNPIVOT`/`Column<T>`](https://github.com/AlexeyShirshov/nextorm/issues/237)
- [Build blocker: устранены NU1902/NU1903 для транзитивного `System.Security.Cryptography.Pkcs`](https://github.com/AlexeyShirshov/nextorm/issues/236)
- [Evidence caveats из T218: сертификация E07, allocation-oracle E08, запись sweep-cap E10](https://github.com/AlexeyShirshov/nextorm/issues/235)
- [Evidence pipeline hardening: атомарный генератор evidence и линт устаревших хэшей](https://github.com/AlexeyShirshov/nextorm/issues/234)
- [Window functions: перегрузка `Over` с одним `partitionBy` и одним `WindowOrder`, без массивов](https://github.com/AlexeyShirshov/nextorm/issues/233)
- [SQL Server `PIVOT`/`UNPIVOT`: типизированная проекция результата вместо `TableAlias` по имени](https://github.com/AlexeyShirshov/nextorm/issues/232)
- [Join aliases: alias-цепочка, использованная в следующем запросе, больше не теряет нижестоящие цепочки](https://github.com/AlexeyShirshov/nextorm/issues/230)
- [Join aliases: anonymous-projection цепочка выдаёт диагностику вместо невалидного C#](https://github.com/AlexeyShirshov/nextorm/issues/229)
- [Flaky ClickHouse integration test `FloatNativeParity_GroupedSingleFloat64` стабилизирован](https://github.com/AlexeyShirshov/nextorm/issues/227)
- [`ProcedureResult`/`BatchResult`: раскрыто число затронутых строк (`RecordsAffected`)](https://github.com/AlexeyShirshov/nextorm/issues/226)
- [JoinInto: composite-junction M:N — документационный дельта-набор](https://github.com/AlexeyShirshov/nextorm/issues/225)
- [SQL-форматирование: `from`/`join` с новой строки](https://github.com/AlexeyShirshov/nextorm/issues/224)
- [JoinInto: composite-junction M:N — SQL link carrier](https://github.com/AlexeyShirshov/nextorm/issues/223)
- [Джойны: перегрузки без явного источника (`Join`/`LeftJoin`/`RightJoin`/`FullJoin` по типу)](https://github.com/AlexeyShirshov/nextorm/issues/222)
- [Интерцепторы: регистрация через DI (generic/factory-перегрузки)](https://github.com/AlexeyShirshov/nextorm/issues/221)
- [Документация: выделен раздел «Движок» (Engine), Querying растворён в Guide](https://github.com/AlexeyShirshov/nextorm/issues/220)
- [Session/server-identity функции: `SERVER_NAME`, `HOST_ID`/`HOST_NAME`, `APP_NAME`, `SUSER_NAME` и аналоги по провайдерам](https://github.com/AlexeyShirshov/nextorm/issues/219)
- [SQL Server последовательности (`NEXT VALUE FOR`): недостающая кросс-провайдерная поддержка](https://github.com/AlexeyShirshov/nextorm/issues/218)
- [Полноценная поддержка `DateTimeOffset`: трансляция `Now`/`UtcNow` и частей даты](https://github.com/AlexeyShirshov/nextorm/issues/217)
- [Row locking: разведены лок всего запроса (`FOR UPDATE`) и `FOR UPDATE OF` на конкретный источник](https://github.com/AlexeyShirshov/nextorm/issues/216)
- [Убрана зависимость OneOf](https://github.com/AlexeyShirshov/nextorm/issues/215)
- [TODO: JoinInto — составные ключи, составные junction-селекторы и M:N под single-query](https://github.com/AlexeyShirshov/nextorm/issues/213)
- [TODO: eager-load nested — level 2+ `LoadWith`](https://github.com/AlexeyShirshov/nextorm/issues/212)
- [TODO: eager-load per-parent `Take`/`Skip` — коррелированная детская выборка](https://github.com/AlexeyShirshov/nextorm/issues/211)
- [TODO: eager-load strategy — явный выбор стратегии загрузки и авто-диспетчер](https://github.com/AlexeyShirshov/nextorm/issues/209)
- [Единообразная обработка отмены при построении проекции](https://github.com/AlexeyShirshov/nextorm/issues/207)
- Принятые ограничения (закрыты как `not_planned`): [COW/shared-description для структурного CTE-графа](https://github.com/AlexeyShirshov/nextorm/issues/297) (аллокации снижены, но время не уложилось в ≤1.00× — откат), [остаточные аллокации warm CTE/RecursiveCte](https://github.com/AlexeyShirshov/nextorm/issues/244) (Cte 1.18–1.22×, RecursiveCte 1.59–1.61×, время 0.55–0.61×, `*_Warm_Reused` 0 B), [диаграмма пути команды](https://github.com/AlexeyShirshov/nextorm/issues/228) (правка применена вне трекинга), [гарантия порядка eager-load](https://github.com/AlexeyShirshov/nextorm/issues/210) (свёрнуто в #211), [паритет cached-пути M12 #3](https://github.com/AlexeyShirshov/nextorm/issues/201) (остаток вынесен в #240).

### 1.0.9-rc2

- [JSON streaming: вложенные проекции и `Projection<T1,T2>`](https://github.com/AlexeyShirshov/nextorm/issues/176)
- [JSON streaming: DB-side fast-path (`FOR JSON` / `json_agg` / `JSONEachRow`)](https://github.com/AlexeyShirshov/nextorm/issues/177)
- [JSON streaming: поддержка `enum` (числовая форма и STJ `[JsonConverter]`-строки)](https://github.com/AlexeyShirshov/nextorm/issues/178)
- [JSON streaming: определена политика частичного вывода/ошибок (валидный JSON при сбое)](https://github.com/AlexeyShirshov/nextorm/issues/179)
- [JSON streaming: провайдерные конвертации колонок и тип исключения валидации](https://github.com/AlexeyShirshov/nextorm/issues/180)
- [CSV streaming: чтение `byte[]`-колонок чанками (bounded memory)](https://github.com/AlexeyShirshov/nextorm/issues/167)
- [Join aliases: свободное смешивание positional/alias + алиас корня (`.WithAlias(Alias.X)`)](https://github.com/AlexeyShirshov/nextorm/issues/160)
- [CTE: прямые перегрузки join-семьи и alias-API для `Cte<T>`](https://github.com/AlexeyShirshov/nextorm/issues/159)
- [Source generator: устранён конфликт сигнатур CS0111 при двух alias-join с общим `Alias.X`](https://github.com/AlexeyShirshov/nextorm/issues/206)
- [SQL Server native JSON: проекция `SqlFunctions.Parameter<T>` снова рендерится с alias (восстановлен `FOR JSON`)](https://github.com/AlexeyShirshov/nextorm/issues/208)
- [Fix: выбор всей сущности из JOIN-проекций](https://github.com/AlexeyShirshov/nextorm/issues/190)
- [Mapping: fold проекций больше не игнорирует `Member` (`SelectExpressionPlanEqualityComparer`/`RowMapperFactory`)](https://github.com/AlexeyShirshov/nextorm/issues/173)
- [Fix: `BindEntity<T>` регистрирует маппинг сущности (типизированные проекции без предварительного `From<T>`)](https://github.com/AlexeyShirshov/nextorm/issues/185)
- [Fix: `DataContextCache.Clear()` очищает кэш `JoinIntoSpec.IdentitySelectorCache`](https://github.com/AlexeyShirshov/nextorm/issues/174)
- [Navigation: валидация уникальности FK для OneToOne](https://github.com/AlexeyShirshov/nextorm/issues/170)
- [Table hints: сохранение `TablesInScopeHints` на multi-table DELETE/UPDATE join-путях](https://github.com/AlexeyShirshov/nextorm/issues/169)
- [`LoadWith`: режим eager loading вынесен в параметр `EagerLoadMode` (`AsSingleQuery()` удалён)](https://github.com/AlexeyShirshov/nextorm/issues/184)
- [Provider-specific fluent API вынесен из `EntityBuilder` в extensions провайдерных сборок](https://github.com/AlexeyShirshov/nextorm/issues/191)
- [`ToDataReader`/`ToDataReaderAsync`: поддержка SQLite для не-LOB проекций](https://github.com/AlexeyShirshov/nextorm/issues/189)
- [V32: навигация поверх temp-table/TVP-источников отклоняется fail-closed](https://github.com/AlexeyShirshov/nextorm/issues/162)
- [Comparison benchmarks tier 1: проекции, агрегаты, пагинация, стриминг + cross-library JSON/CSV](https://github.com/AlexeyShirshov/nextorm/issues/188)
- [PG/CH диалекты `sealed`→`class`: зафиксировано решение о публичной расширяемости](https://github.com/AlexeyShirshov/nextorm/issues/153)
- [ExtremeRow capability DTO: публичная read-only конструкция вместо internal-конструкторов](https://github.com/AlexeyShirshov/nextorm/issues/154)
- [Re-run Stryker mutation testing для native extreme-row (фиксация capture)](https://github.com/AlexeyShirshov/nextorm/issues/151)
- [Согласована запись finding 25 в регистрах](https://github.com/AlexeyShirshov/nextorm/issues/157)
- [JoinInto test-quality debt: проверки hash-inequality / видимость `EagerLoadSpec.Assign`](https://github.com/AlexeyShirshov/nextorm/issues/175)
- Принятые ограничения (issue закрыты как `not_planned`, ревизия по появлении конкретного потребителя): [JoinInto поверх `As`/derived-источника](https://github.com/AlexeyShirshov/nextorm/issues/171) и [child-collection проекция в anonymous/derived-форму](https://github.com/AlexeyShirshov/nextorm/issues/172)

### 1.0.9-rc1

- [ClickHouse: нативная поддержка JSON (`[JsonColumn]`, `JsonDocument`, `JsonElement`)](https://github.com/AlexeyShirshov/nextorm/issues/198)
- [PostgreSQL: чтение `JsonNode` из нативных `json`/`jsonb`-колонок](https://github.com/AlexeyShirshov/nextorm/issues/197)
- [Raw-row materialization: поддержка PostgreSQL `ROW`/composite](https://github.com/AlexeyShirshov/nextorm/issues/194)
- [SQL Server: паритет скалярных функций с linq2db (metadata / date-part / checksum)](https://github.com/AlexeyShirshov/nextorm/issues/182)
- [SQLite: полнотекстовый поиск FTS3/FTS4/FTS5](https://github.com/AlexeyShirshov/nextorm/issues/181)
- [SQLite: поддержка JSON1](https://github.com/AlexeyShirshov/nextorm/issues/132)
- [ClickHouse: экранирование обратных слэшей в идентификаторах](https://github.com/AlexeyShirshov/nextorm/issues/161)
- [ClickHouse: нативный паритет extreme-row для float/double ключей](https://github.com/AlexeyShirshov/nextorm/issues/150)
- [ClickHouse: маппинг нативного типа колонки JSON](https://github.com/AlexeyShirshov/nextorm/issues/128)
- [Version-gates: MariaDB 13 и PostgreSQL FILTER-агрегаты для 9.2/9.3](https://github.com/AlexeyShirshov/nextorm/issues/141)
- [PostgreSQL: маппинг нативной колонки `json`/`jsonb`](https://github.com/AlexeyShirshov/nextorm/issues/131)
- [PostgreSQL: свободный (provider-specific) список колонок](https://github.com/AlexeyShirshov/nextorm/issues/140)
- [`ToDataReader` на SQLite](https://github.com/AlexeyShirshov/nextorm/issues/134)
- [Streaming LOB: MySQL/MariaDB и ClickHouse](https://github.com/AlexeyShirshov/nextorm/issues/133)
- [SQL Server: `OUTPUT INTO` в табличную переменную (`DECLARE @t TABLE`)](https://github.com/AlexeyShirshov/nextorm/issues/127)
- [Tuple-конструктор `(a, b)` на MySQL/MariaDB/SQLite](https://github.com/AlexeyShirshov/nextorm/issues/126)
- [Tuple `IN`/`Contains`: трансляция и исполнение во всех провайдерах](https://github.com/AlexeyShirshov/nextorm/issues/193)
- [FTS5 maintenance/control: поверхность команд (`AutoMerge`/`CrisisMerge`/`Merge`/`Optimize`/`Rebuild`/`IntegrityCheck`)](https://github.com/AlexeyShirshov/nextorm/issues/195)
- [FTS5 maintenance: тесты на реальном SQLite + документация EN/RU](https://github.com/AlexeyShirshov/nextorm/issues/196)
- [ClickHouse: `reference→collection` навигация — принятое ограничение провайдера (явный gate)](https://github.com/AlexeyShirshov/nextorm/issues/163)
- [SQL Server: `MapColumnExpression` больше не боксит числовые значения](https://github.com/AlexeyShirshov/nextorm/issues/168)
- [Fix: скалярный путь `InValuesTranslator` больше не отравляет plan-cache общего `QueryCommand` (sticky `Cache=false`)](https://github.com/AlexeyShirshov/nextorm/issues/199)
- [Perf: снижены аллокации свежего cached-пути CTE/RecursiveCTE](https://github.com/AlexeyShirshov/nextorm/issues/200)
- [Fix: PostgreSQL `hstore`/`ltree` raw-колонки на чистом type-каталоге больше не диагностируются как named composite](https://github.com/AlexeyShirshov/nextorm/issues/202)
- [PostgreSQL raw-row: авторитетная классификация composite на чистом type-каталоге; единый путь для single-/multi-column](https://github.com/AlexeyShirshov/nextorm/issues/203)

### 1.0.9-b

- [Неявные навигационные запросы: одиночные связи, многошаговые цепочки, presence-проверки и `AsEntityBuilder`](https://github.com/AlexeyShirshov/nextorm/issues/148)
- [Связи: `JoinInto` для many-to-many и one-to-one](https://github.com/AlexeyShirshov/nextorm/issues/135)
- [Алиасы в join: типизированные alias-проекции через source generator (`Alias.Buyer` и т.п.)](https://github.com/AlexeyShirshov/nextorm/issues/113)
- [CTE: типизированные источники с сохранением проекции `Select`, включая рекурсивные](https://github.com/AlexeyShirshov/nextorm/issues/146)
- [CTE: тела UPDATE/DELETE и вынос вложенных CTE в верхний `WITH`](https://github.com/AlexeyShirshov/nextorm/issues/136)
- [`SelectWhereMax`/`SelectWhereMin` — нативные fast paths PostgreSQL `DISTINCT ON` и ClickHouse `argMax`](https://github.com/AlexeyShirshov/nextorm/issues/144)
- [Join/`.Returning(...)`: явный терминал удаления и identity-форма для multi-table](https://github.com/AlexeyShirshov/nextorm/issues/145)
- [API: `Create…Builder` для DML/batch и альтернативные query-фабрики](https://github.com/AlexeyShirshov/nextorm/issues/147)
- [Хинты: табличные (`WITH`) на присоединённых таблицах, fluent API и параметризация](https://github.com/AlexeyShirshov/nextorm/issues/130)
- [Потоковая выдача данных в `Stream` — JSON и RFC 4180 CSV без материализации](https://github.com/AlexeyShirshov/nextorm/issues/112)
- [`ProcedureResult`: стриминг нескольких result set'ов (гетерогенный курсор)](https://github.com/AlexeyShirshov/nextorm/issues/118)
- [Raw SQL как шаг батча (`BatchBuilder.Raw`) и params-форма `ExecuteRaw`/`ExecuteProcedure`](https://github.com/AlexeyShirshov/nextorm/issues/119)
- [Глобальные фильтры: мост keyed-фильтров EF Core 10, цель INSERT/MERGE/UPSERT, `FromSql`/сырые источники](https://github.com/AlexeyShirshov/nextorm/issues/125)
- [In-memory: `PrepareFromSql` для сырого SQL и источники табличных функций](https://github.com/AlexeyShirshov/nextorm/issues/139)
- [Dynamic columns: write-side в in-memory, per-key конвертеры и JSON](https://github.com/AlexeyShirshov/nextorm/issues/137)
- [Унификация обхода result-set: `BatchResult`/`ProcedureResult` напрямую перечисляемы, `ReadSets` удалён](https://github.com/AlexeyShirshov/nextorm/issues/186)
- Новые страницы EN + RU: глава 28 «Потоковая выдача данных», глава 29 «Неявные навигационные запросы», «Нативные стратегии крайней строки», «Мост фильтров EF Core»
- [Удалены из общего API `Final()`, `PreWhere(predicate)` и `Settings(("key", "value"), ...)`](https://github.com/AlexeyShirshov/nextorm/issues/122)

### 1.0.9-a

- [Навигационные свойства и связи: декларативные O2M/M2O, `JoinInto`, дочерние коллекции](https://github.com/AlexeyShirshov/nextorm/issues/105)
- [Eager loading графа — `LoadWith`/`Include` (split- и single-query, `EagerLoadMode.SingleQuery`)](https://github.com/AlexeyShirshov/nextorm/issues/95)
- [Глобальные фильтры запросов (fluent + атрибут, keyed, selective `IgnoreFilters`, DML и INSERT/MERGE-валидация)](https://github.com/AlexeyShirshov/nextorm/issues/67)
- [Интеграция с EF Core — пакет `nextorm.entityframeworkcore` (`UseNextOrm`, DI, shared transactions)](https://github.com/AlexeyShirshov/nextorm/issues/61)
- [Динамические колонки (store): чтение и запись, INSERT/UPDATE/MERGE рендерят ключи словаря как колонки](https://github.com/AlexeyShirshov/nextorm/issues/94)
- [Потоковое чтение LOB: серверный чанкинг (MySQL/MariaDB), аксессоры именованных колонок и стрим-проекции](https://github.com/AlexeyShirshov/nextorm/issues/100)
- [`As<TResult>` — проекция join в пользовательский тип и снятие потолка арности](https://github.com/AlexeyShirshov/nextorm/issues/76)
- Новые страницы EN + RU: «Связи», «Eager loading», «Глобальные фильтры запросов», «Интеграция с EF Core», «Логирование», глава 27 «Динамические колонки»; перенумерация глав руководства
- [Глава 24 — уточнён диапазон MySQL/MariaDB `TIME`](https://github.com/AlexeyShirshov/nextorm/issues/102)

### 1.0.8-b

- [Multi-resultset support](https://github.com/AlexeyShirshov/nextorm/issues/25)
- [BLOB/CLOB support](https://github.com/AlexeyShirshov/nextorm/issues/27)
- [Хранимые процедуры и функции (вызов, output-параметры, несколько result-set)](https://github.com/AlexeyShirshov/nextorm/issues/70)
- [Table-valued parameters (TVP)](https://github.com/AlexeyShirshov/nextorm/issues/73)
- [Паритет опций bulk copy / bulk insert](https://github.com/AlexeyShirshov/nextorm/issues/92)
- [Command timeout (per-context / per-query)](https://github.com/AlexeyShirshov/nextorm/issues/93)
- [Варианты хинтов — join / subquery / tables-in-scope](https://github.com/AlexeyShirshov/nextorm/issues/96)
- [Управление кэшем планов/запросов (ClearCache, disable, sliding expiration)](https://github.com/AlexeyShirshov/nextorm/issues/97)
- [Комментарий-метка запроса (TagQuery)](https://github.com/AlexeyShirshov/nextorm/issues/98)
- [Per-query переопределение источника (table/schema/database/server, `WithTableExpression`)](https://github.com/AlexeyShirshov/nextorm/issues/99)
- Новая глава руководства EN + RU: потоковое чтение больших объектов (BLOB/CLOB)

### 1.0.7-beta

- [`OUTPUT INTO`, несколько result-set'ов, upsert-with-output](https://github.com/AlexeyShirshov/nextorm/issues/15)
- [Value converters — `EnumToStringConverter`, конвертеры в предикатах и проекциях](https://github.com/AlexeyShirshov/nextorm/issues/31)
- [Динамическая схема результата (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`)](https://github.com/AlexeyShirshov/nextorm/issues/63)
- [JSON-колонка ↔ CLR-объект (авто-сериализация свойства)](https://github.com/AlexeyShirshov/nextorm/issues/64)
- [PostgreSQL range-типы и `Overlaps` (`&&`)](https://github.com/AlexeyShirshov/nextorm/issues/66)
- [SQL Server 2025 — `regexp_like` / `regexp_replace`](https://github.com/AlexeyShirshov/nextorm/issues/85)
- [Портативный range как пара колонок — `[RangeColumns]`](https://github.com/AlexeyShirshov/nextorm/issues/86)
- [Скалярные функции: кросс-провайдерный фасад `SqlFunctions.Sql` и пробелы по провайдерам](https://github.com/AlexeyShirshov/nextorm/issues/87)
- [Value converters и JSON-колонки](https://github.com/AlexeyShirshov/nextorm/issues/88)
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
