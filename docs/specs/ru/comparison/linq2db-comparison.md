# nextorm vs linq2db: сравнение функционала

> Сравнение функционала nextorm и linq2db. Дополняет [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md)
> и [матрицу возможностей](../../comparison/capability-matrix.md) (там же рассматривается EF Core). По текущему
> дереву видно, что на аналитической поверхности запросов nextorm не уступает linq2db, а местами превосходит
> её — типы соединений, `APPLY`/`LATERAL`, full-text/JSON/массивы, кросс-провайдерные кортежи/row values,
> нативные range-типы (и range поверх пары скаляров), трансляция CLR `Regex`,
> `ROLLUP`/`CUBE`/`GROUPING SETS`, арифметика дат, временные таблицы, блокировки строк, хинты
> запросов/таблиц/индексов, настраиваемый регистр ключевых слов, TVF, именованные алиасы соединений,
> доступ к немаппированным колонкам, выбор экстремальной строки, per-query переопределения источника и
> корреляция in-memory на глубине один — и добавляет полную явную поверхность записи
> (`INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, модифицирующие CTE PostgreSQL), массовую вставку,
> `CREATE TABLE AS SELECT`, `TRUNCATE`, роль транзакций, value converters, JSON-колонки, хранилище
> динамических колонок, декларативные связи с eager loading уровня 1 и неявной навигацией по объявленным
> связям, глобальные фильтры запросов, перехватчики команд, построитель SQL-батча, стриминг result-set и
> экспорт в поток, хранимые процедуры и table-valued parameters, а также пакет интеграции с EF Core.

**Предварительные требования:** [Обзор провайдеров](../../../providers/overview.md) · [Ограничения](../../../advanced/limitations.md) · [Хинты запросов](../../../guide/13-query-hints.md)

## Позиционирование

* **nextorm** — сфокусированный построитель SQL и маппер без отслеживания изменений, рассчитанный на чтение
  и отчётность. Он покрывает полную аналитическую поверхность запросов, добавляет полную явную поверхность
  записи (`INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, массовая вставка, `CREATE TABLE AS SELECT`,
  `TRUNCATE`, а также модифицирующие CTE в PostgreSQL) и роль транзакций (`ITransactionManager`,
  собственные и привязанные), и генерирует переносимый между SQL Server, PostgreSQL, MySQL/MariaDB,
  SQLite и ClickHouse SQL. В основе — малый объём аллокаций, параметризация и компиляция запросов
  (неявный кэш планов / явный `Prepare()`), плюс включаемые настройки вывода (квотирование
  идентификаторов, соглашения об именовании, регистр ключевых слов) и хинты индексов — и бенчмарки на
  уровне или выше Dapper, EF Core и linq2db на поставляемых сценариях. Слой маппинга покрывает value
  converters, JSON-колонки и хранилище динамических колонок; слой выполнения — сырые команды и хранимые
  процедуры, table-valued parameters, построитель SQL-батча, стриминг result-set и экспорт в поток. Также
  декларативно моделирует связи (`[Relationship]`/`HasMany`/`HasOne` + `JoinInto`) с eager loading
  уровня 1 (`LoadWith`) и неявной навигацией по объявленным связям, содержит перехватчики команд и
  глобальные фильтры запросов и интегрируется с EF Core (`nextorm.entityframeworkcore`).
* **linq2db** — широкий зрелый LINQ-to-SQL ORM: более широкая матрица провайдеров, полный CRUD
  (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), связи/eager loading, bulk copy, временные таблицы, кодогенерация
  под существующую БД, расширяемость (интерсепторы, собственный SQL-маппинг) и пакет интеграции с EF Core.
  Эта дополнительная поверхность идёт вместе с change tracking и более тяжёлой моделью.

Библиотеки пересекаются на *поверхности запросов* и явном изменении данных — там nextorm не уступает
linq2db или превосходит её — и расходятся в *моделировании связей*, *отслеживании изменений* и
*инструментарии*, которые nextorm осознанно оставляет за рамками.

## Матрица возможностей

Обозначения: **yes** — полноценная поддержка; **partial** — собственный пробел библиотеки, реализуемо, но
пока не реализовано; **no** — нет. nextorm — базовая линия, поэтому в его ячейке стоит чистый маркер;
ограничение самого движка БД указано кратко в скобках и не занижает оценку. Там, где linq2db поддерживает
конструкцию, но не имеет возможности nextorm, он помечен **partial** с указанием недостающего. Для nextorm
указан исходный файл, отвечающий за поведение.

| Область | nextorm | linq2db | Обоснование в nextorm |
|---| --- |---|---|
| Проекция (`SELECT`, DTO/анонимные/record/tuple/scalar; немаппированные колонки через `SqlFunctions.Column`) | yes | yes | `EntityBuilder.Select`, `SqlFunctions.Column` |
| Предикаты (`WHERE`: сравнения, `and`/`or`/`!`, арифметика, битовые/сдвиги) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes (`FULL JOIN` нет в MySQL/MariaDB) | yes | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes (отключено в SQLite/ClickHouse) | yes | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Строгость соединений (`ANY`/`ALL`/`ASOF`) и `GLOBAL` | **yes** в ClickHouse | yes (ClickHouse `ClickHouseHints.Join.*`, включая `Global*`) | `JoinStrictness`, `JoinOptions.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Арность соединений | yes — до 8 | yes | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Соединение с производной таблицей (подзапросом) | yes | yes | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Именованные алиасы соединений (`Alias.<Name>`) | **yes** — сгенерированные типы `AliasProjection_*`/`AliasJoin_*` адресуют каждый слот (`JoinSlotAttribute`); только SQL-провайдеры | no | `Alias`, generated `NextORM.Generated.*`, `JoinSlotAttribute` |
| Подзапросы (`FROM`, скалярные, коррелированные `EXISTS/IN/ANY/ALL`) | yes (in-memory — только глубина один) | yes | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `DataContext/InMemoryCorrelatedPlan.cs` |
| `IN` по списку/массиву | **yes** | partial — нет распределённого `GLOBAL IN` в ClickHouse | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs`, `SqlFunctions.ClickHouse.global_in` |
| `GROUP BY` / `HAVING` / агрегаты (`FILTER`/`-If`, статистические/quantile/ordered-семейства) | yes (ANSI `FILTER` в PostgreSQL/SQLite, `<fn>If` в ClickHouse; статистические/quantile/ordered-семейства провайдера) | yes (`Sql.Ext`-агрегаты провайдера) | `EntityBuilder.GroupBy/Having`, `AdvancedAggregateTranslator.cs`, `Supports*Aggregates`, `AggregateFilterStyle` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes (`WITH TOTALS` в ClickHouse) | yes | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | **yes** в ClickHouse | no | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | **yes** в ClickHouse | partial — только `FINAL` (`ClickHouseHints.Table.Final`) и `SETTINGS` (`ClickHouseHints.Query.Settings`); `SAMPLE`/`PREWHERE` нет | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, диалектные `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL`, где поддерживает движок) | yes (зависит от провайдера; `ALL` в PostgreSQL, MariaDB и ClickHouse) | yes | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | **yes** в зависимости от провайдера (`DISTINCT ON` PostgreSQL; `WITH TIES` PostgreSQL + SQL Server; `TABLESAMPLE` PostgreSQL + SQL Server) | no | `DistinctOn`/`WithTies`/`FromOptions.TableSample`, `ISqlDialect.SupportsWithTies` |
| Блокировка строк (`FOR UPDATE`/`FOR SHARE`, `NOWAIT`/`SKIP LOCKED`) | yes — `FOR UPDATE`/`FOR SHARE` по провайдерам (PostgreSQL/MySQL/MariaDB — трейлинг `FOR UPDATE`/`LOCK IN SHARE MODE`; SQL Server — через табличные хинты) **плюс режимы ожидания `NOWAIT`/`SKIP LOCKED`** (`LockWaitMode`; MySQL переключает разделяемую блокировку на `FOR SHARE`, SQL Server использует `READPAST` как приближение `SKIP LOCKED`) | yes (зависит от провайдера, `SubQueryTableHint`: PostgreSQL `FOR UPDATE`/`FOR NO KEY UPDATE`/`FOR SHARE`/`FOR KEY SHARE`, MySQL `FOR UPDATE`/`FOR SHARE`/`LOCK IN SHARE MODE`, SQL Server — табличные хинты `UPDLOCK`/`XLOCK`; плюс `NOWAIT`/`SKIP LOCKED`) | `ForUpdate`/`ForShare` + `LockWaitMode`, `ILockRenderer`/`ILockRenderer.UsesTableHints`/`Render` |
| Временные таблицы (`FOR SYSTEM_TIME`) | yes на SQL Server + MariaDB (`CONTAINED IN` — только SQL Server) | yes (SQL Server) | `ForSystemTime`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| CTE (включая рекурсивные и типизированные `AsCte`/`AsRecursiveCte`) | **yes** — типизированные поверхности несут форму anchor-проекции, авто-`UNION ALL`/хойстинг; модифицирующие CTE PostgreSQL | yes (нет модифицирующих CTE) | `Cte<T>`/`QueryCommand<T>.AsCte`/`AsRecursiveCte`, `Builders/MutationCteQuery.cs`, `ISqlDialect.SupportsDataModifyingCtes` |
| Оконные функции (`OVER`, ranking, framed aggregates, `lag`/`lead`, percentile) | **yes** (именованные окна, `GROUPS`/`EXCLUDE`, оконные percentile) | partial — нет именованных окон и кадров `GROUPS`/`EXCLUDE` | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion`/`SupportsPercentileWindow` |
| `CASE WHEN` / тернарный / `switch`, `COALESCE`, числовой `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| Строковые / математические / date скалярные функции, `LIKE`, конкатенация строк, `NULLIF`, настройки/последовательности PostgreSQL | yes — портируемые CLR-методы `string` и конкатенация `+` на всех провайдерах, `SqlFunctions.Sql.nullif`, плюс нативная библиотека строк/`regexp_*`/настроек/последовательностей на PostgreSQL (`SqlFunctions.Postgres`) | yes | `Visitors/ScalarFunctionTranslator.cs`, диалектные `Make*`, `SqlFunctions.Sql.nullif`, `SqlFunctions.Postgres` |
| CLR `Regex` (`IsMatch`/`Replace`, константный шаблон) | **yes** — PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite и SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`; 2019/2022 отклоняют) | partial — открытый `linq2db#698` (трансляции `Regex.IsMatch` нет) | `Visitors/RegexSqlTranslator.cs`, `ISqlDialect.SupportsRegex`/`MakeRegexMatch`/`MakeRegexReplace` |
| Арифметика дат (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Полнотекстовый поиск | yes на SQL Server, PostgreSQL и MySQL/MariaDB | yes (провайдер) | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Нативные JSON-документы | **yes на PostgreSQL** | partial — тип `json`/`jsonb` плюс `JsonContains` (`@>`), `JsonExtractPathText` (`#>>`) и `Json.Value`; не полная библиотека `jsonb_*` | `SupportsJson`, `JsonSqlTranslator` |
| JSON скалярные функции (`json_value`/`json_query`/`json_modify`, `isjson`) | yes на SQL Server и MySQL/MariaDB | yes | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| Строковый JSON + нативный JSON + функции словарей (ClickHouse) | **yes на ClickHouse** (`JSONExtract*`/`JSONAllPaths`/`toJSONString`/`visitParam*`, плюс словари) | нет выделенного API | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Массивы (`cardinality`/`array_*`/`@>`/`&&`, `Array(T)` в ClickHouse, `ARRAY JOIN`) | **yes** на PostgreSQL и ClickHouse | partial — операторы массивов PostgreSQL (`PostgreSQLExtensions`); API для ClickHouse `Array(T)`/higher-order/`ARRAY JOIN` нет | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Row values / кортежи (`ROW`/`(a, b)`, доступ к элементу, сравнение строк) | yes на PostgreSQL и ClickHouse | yes (`Sql.Row`; эмулируется там, где у провайдера нет нативного row) | `ISqlDialect.Tuple`/`ITupleRenderer`, `Visitors/TupleSqlTranslator.cs` |
| Нативные range-типы + range поверх пары скаляров (`Range<T>`, `Overlaps`, `range_contains`, инспекция границ) | **yes** — нативные range/multirange-типы на PostgreSQL; **пара скалярных колонок** (`[RangeColumns]`) на SQL Server/MySQL/MariaDB/SQLite/ClickHouse | partial — маппинг провайдерных `NpgsqlRange<T>`/multirange на PostgreSQL; портируемого `Range<T>`/пары скаляров нет | `Query/Range.cs`, `RangeColumnsAttribute`, `ISqlDialect.SupportsRanges`/`SupportsRangeColumns`, `SqlFunctions.Postgres` |
| Условные функции (`iif`/`choose`/`multi_if`) | **yes** | no | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes на SQL Server | yes (провайдер) | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| Методы типа XML (`.value`/`.query`/`.exist`/`.nodes`) | partial — только SQL Server | yes (провайдер) | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | **yes** (обработка NULL зависит от провайдера) | partial | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes (`array_agg` на PostgreSQL) | yes | `SupportsStringAgg`/`SupportsArrayAgg` |
| Пользовательские скалярные функции | yes (`[SqlFunction]`) | yes (`DbFunction` / `Sql.Ext`) | `SqlFunctionAttribute.cs` |
| Табличные функции | yes (`[SqlTableFunction]`) | yes (`TableFunction`; табличные функции БД можно скаффолдить) | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Нативный источник `PIVOT` / `UNPIVOT` | **yes на SQL Server** | no (сырой SQL) | `EntityBuilder.Pivot`/`Unpivot` |
| Сырой SQL (запрос целиком) | yes | yes | `WithSql` / `PrepareFromSql` |
| Сырой SQL как композируемый источник/подзапрос | yes | yes | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Per-query переопределения источника (`WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression`) | **yes** — гейт по уровням, участвует в ключе плана; SQL Server 4-part, MySQL/MariaDB/ClickHouse `db.table`, PostgreSQL/SQLite `schema.table` | partial (`Table(Name=...)`, `Sql.TableExpression`) | `EntityBuilder.WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression`, `ISqlDialect.SupportsCrossDatabase`/`SupportsLinkedServer` |
| Хинты уровня инструкции | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB встроенный `/*+ ... */`; SQLite/ClickHouse отклоняют | partial (зависит от провайдера, `QueryHint`: SQL Server `OPTION (...)`, MySQL/Oracle `/*+ ... */`, ClickHouse `SETTINGS`; в PostgreSQL API хинтов нет) | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Блокирующие табличные хинты (например `WITH (NOLOCK)`) | yes — только SQL Server | yes — только SQL Server (`SqlServerHints.TableHint`; в MySQL `TableHint` — только optimizer-хинты, в PostgreSQL/ClickHouse синтаксиса table hints нет) | `FromOptions.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Хинты индексов (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | yes — MySQL/MariaDB, SQLite и SQL Server; PostgreSQL (без `pg_hint_plan`), ClickHouse и in-memory отклоняют | yes (`IndexHint`/`TableHint`, зависит от провайдера) | `FromOptions.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| Квотирование идентификаторов | yes (включается явно) | yes (по провайдеру) | `ISqlDialect.QuoteIdentifier` |
| Соглашения об именовании (например snake_case) | **yes** (включается явно, встроенный `SnakeCaseNamingConvention`) | partial — нет встроенной конвенции | `INamingConvention` / `SnakeCaseNamingConvention` |
| Регистр ключевых слов SQL (верхний/нижний) | **yes** — включается через `KeywordCase.Upper`; по умолчанию `KeywordCase.Lower` байт-в-байт совпадает с историческим выводом | no (ключевые слова выводятся в каноническом регистре провайдера) | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | yes | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `CreateDeleteJoinBuilder`/`DeleteJoinBuilder<TProjection>`, `CreateUpdateJoinBuilder`/`UpdateJoinBuilder<TProjection>`, `DataContextExtensions.Update`/`Delete`, `CreateTruncateBuilder`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete`; in-memory применяет только key upsert |
| Bulk copy / merge / временные таблицы | частично — key upsert (`MergeInto`/`MergeBuilder<T>`), полный `MERGE` с ветками (`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource`, произвольные условия, `RETURNING`/`OUTPUT`; SQL Server, PostgreSQL 15+), массовая вставка (`BulkInsertInto<T>`: нативные `COPY`/`SqlBulkCopy` + чанковый `INSERT ... VALUES`, настройка через record `BulkInsertOptions` или fluent-`BulkInsertOptionsBuilder` — `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`, `IgnoreDuplicates`, `KeepIdentity`, `Timeout`, прогресс `NotifyAfter` с `ProgressCancellationTokenSource`; `ReturningKey`/`Returning`) и материализация запроса во (временную) таблицу ([`ToTable`/`ToTempTable`](../../../ru/guide/18-create-table-as.md) — `ToTable` на PostgreSQL/SQLite/MySQL/MariaDB/SQL Server/ClickHouse, временная форма `ToTempTable` на PostgreSQL/SQLite/MySQL/MariaDB) реализованы | yes | `MergeBuilder<TEntity>`, `BulkInsertBuilder<TEntity>`, `BulkInsertOptions`, `TempTableExtensions` |
| Транзакции (собственные и привязанные) | yes (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse и in-memory отклоняют) | yes | `DataContext/Roles/ITransactionManager.cs`, `DataContext/DbConnectionManager.cs`, `ISqlDialect.SupportsTransactions` |
| Навигационные свойства / связи / eager loading | частично — метаданные связей (O2M/M2O/O2O/M2M) + `JoinInto` (коллекции one-to-many, ссылки one-to-one, many-to-many через явный junction), уровень-1 `LoadWith` (split / single-query) и неявная навигация по объявленным связям (ссылочные скалярные цепочки/проверка присутствия/проекция целиком как `LEFT JOIN`; коллекционные `Any`/`Count`/`LongCount`/`Count`); остаётся вывод по конвенции FK (его нет и в linq2db), составные ключи, составной junction-селектор и many-to-many `JoinInto` под `AsSingleQuery` | yes (`[Association]`, `LoadWith`) | `Builders/EntityBuilder.cs`, `JoinIntoSpec.cs`/`JoinIntoStitcher.cs`, `Builders/EntityBuilderEagerLoading.cs`, `AsEntityBuilder<T>` |
| Отслеживание изменений / identity map | no (по замыслу) | partial | — |
| Расширяемость (интерсепторы, собственный SQL, фильтры) | yes — диалект + `[SqlFunction]`/`[SqlTableFunction]`, перехватчики команд/соединения, глобальные фильтры запросов и сырой SQL | обширная | `SqlDialectBase`, `IQueryInterceptor`/`IConnectionInterceptor`, `QueryFilterAttribute`/`HasQueryFilter` |
| Провайдеры | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, ClickHouse, DuckDB, Ydb | `src/nextorm.*` |
| Интеграция с EF Core | yes — `nextorm.entityframeworkcore`, делит соединение/транзакцию EF ([интеграция с EF Core](../../../advanced/integration-efcore.md)); опциональный мост DML/`SaveChanges` — вне области | yes (`linq2db.EntityFrameworkCore`) | `src/nextorm.entityframeworkcore` |
| Сырые команды и хранимые процедуры (`ExecuteRaw`/`ExecuteProcedure`, output/return-параметры, forward-only курсоры) | yes — хранимые процедуры на SQL Server/PostgreSQL/MySQL/MariaDB | yes | `IRawCommandExecutor`, `ProcedureResult`/`ProcedureParameter`, `ISqlDialect.SupportsStoredProcedures` |
| Table-valued parameters | yes — нативно SQL Server, эмуляция array/JSON/`Array(T)`+`arrayJoin` в остальных | yes (`TableParameterValue`) | `ProcedureParameter.Table<T>`, `ISqlDialect.SupportsTableValuedParameters` |
| SQL-батч (`CreateBatchBuilder`/`BatchQuery<TResult>`/`BatchResult`, типизированные result-set'ы за один round trip) | **yes** (PostgreSQL/SQL Server/MySQL/MariaDB/SQLite) | partial — батч только для remote-контекста (`BeginBatch`/`CommitBatch`); локального многошагового построителя нет | `BatchExtensions`/`BatchBuilder`/`BatchResult`, `ISqlDialect.SupportsBatch` |
| Стриминг result-set (`ToStream`/`ToTextReader`/`ToDataReader`) | **yes** — последовательное потоковое чтение LOB на PostgreSQL/SQL Server; `ToDataReader` в SQLite буферизованный и без локатора (LOB не чанками) | partial — сырой `DbDataReader` (`ExecuteReader`/`DataReaderWrapper`) и материализующий `IAsyncEnumerable`; LOB-терминала нет | `QueryCommandExtensions.ToStream`/`ToTextReader`/`ToDataReader`, `ISqlDialect.LobLocatorColumn`/`SupportsSequentialAccess` |
| Экспорт result-set в поток (`WriteJson`/`WriteCsv`) | **yes** | partial (клиентская сериализация) | `QueryCommand.WriteJson`/`WriteCsv`, `JsonStreamOptions`/`CsvStreamOptions` |
| `TRUNCATE` | yes (SQLite/in-memory отклоняют) | yes | `DataContextExtensions.CreateTruncateBuilder` |
| Кэш планов запросов и `Prepare()` | yes — неявный структурный кэш планов плюс явный `Prepare()` | yes | `EntityBuilderExtensions.Prepare`, `IPreparedQueryCommand<TResult>`, `DataContextCache` |
| `SelectMany` / `GroupJoin` | partial — только in-memory; SQL-провайдеры отклоняют | yes (трансляция в SQL) | `EntityBuilder.SelectMany`/`GroupJoin`, `InMemoryLinqSource` |
| Конфигурация контекста, логирование и DI (`DataContextBuilder`, `AddNextOrmContext`) | yes | yes | `DataContextBuilder`, `ServiceCollectionExtensions.AddNextOrmContext` |
| In-memory-провайдер (только чтение, зарегистрированные CLR-наборы) | **yes** | partial | `InMemoryDataContext`, `WithData`/`WithAsyncData` |
| Оптимистичная конкурентность (паттерн с токеном; без метаданных concurrency-token) | partial | yes | `Returning`, `DataContextExtensions.Update` |
| Жизненный цикл соединения (`IConnectionManager`: внешнее/собственное, переиспользование, dispose) | yes | yes | `IConnectionManager.GetConnection`/`EnsureConnectionOpen`, `DbConnectionManager` |
| Производительность | **наивысшая** | высокая | `docs/specs/performance/benchmark-report.md` |

## Где nextorm впереди

На общей поверхности nextorm не уступает linq2db или превосходит её; вдобавок он даёт:

* Полная аналитическая поверхность запросов: все типы соединений, включая `APPLY`/`LATERAL`
  (включая коррелированные источники), соединения с производными таблицами, строгость соединений/`GLOBAL`
  в ClickHouse, операции над множествами, `DISTINCT` (плюс `DISTINCT ON`/`WITH TIES`), CTE (рекурсивные,
  плюс **модифицирующие CTE PostgreSQL**),
  оконные функции (именованные окна, `GROUPS`, `EXCLUDE`, `nth_value`, `percent_rank`/`cume_dist`),
  `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`, `CASE`/`COALESCE`/`CAST`, строковые/математические/date
  функции, `IN`-списки, маппинг UDF/TVF, нативные `PIVOT`/`UNPIVOT`, временные таблицы, блокировки строк и
  сырой SQL для запроса целиком.
* Немаппированные колонки, именованные алиасы соединений и per-query переопределения источника: чтение
  колонки без mapped-свойства (`SqlFunctions.Column`), адресация каждого слота соединения через
  сгенерированные типы `Alias.<Name>` (`JoinSlotAttribute`) и переопределение
  таблицы/схемы/базы/сервера или сырого table-expression на запрос
  (`WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression`) — каждое входит в ключ плана.
* Выбор экстремальной строки как отдельный оператор: `SelectWhereMax`/`SelectWhereMin` с
  `ExtremeRowTies.One`/`All` — портируемый lowering с нативной стратегией в PostgreSQL (`DISTINCT ON` /
  `ORDER BY ... LIMIT 1`) и ClickHouse (`argMin`/`argMax`). Отдельного такого оператора в linq2db нет
  (он собирается из оконных функций и `OrderBy`/`Take`), а EF Core доводит только одну строку
  (`OrderBy`+`First`, `MaxBy`/`MinBy`).
* JSON-колонка первого класса (`[JsonColumn]` с хранилищем Auto/Native/Text) — поверхность маппинга,
  которой у linq2db всё ещё нет (открытый `linq2db#1661`).
* Полная явная поверхность записи: `INSERT ... VALUES`/`INSERT ... SELECT` (одна строка, сущность, батч),
  сгенерированный ключ (`ReturningIdentity`/`ReturningKey`) и возврат вставленных строк (`Returning`, в
  PostgreSQL/SQLite/SQL Server), **key upsert** (`MergeInto`), **`DELETE`** (`DeleteFrom`/`Delete<T>`/
  `Truncate<T>`), **`UPDATE`** (`Update<T>`/`UpdateBuilder<T>`) и **полный `MERGE`** с ветками
  `WHEN MATCHED`/`WHEN NOT MATCHED`/`WHEN NOT MATCHED BY SOURCE` и `RETURNING`/`OUTPUT` (SQL Server,
  PostgreSQL 15+); массовая вставка (`BulkInsertInto<T>`, нативные `COPY`/`SqlBulkCopy` или чанковый
  `VALUES`, с поверхностью `BulkInsertOptions`/`BulkInsertOptionsBuilder`) и материализация запроса в таблицу
  (`ToTempTable`/`ToTable`); в PostgreSQL — **модифицирующие CTE** (`With(имя, insert)`/
  `CteQuery.With(имя, insert)` → write-CTE, чьи `RETURNING`-строки читаются типизированно или питают
  следующий `INSERT ... SELECT`); и **транзакции** (`ITransactionManager`, собственные или привязанные из
  EF Core/Dapper/ADO.NET) — всё явные команды, без change tracking и `SaveChanges`.
* Декларативные связи и eager loading: O2M/M2O через `[Relationship]`/`HasMany`/`HasOne` и `JoinInto`
  (LEFT/INNER, `Where`, постраничная выборка родителя, несколько коллекций, паритет in-memory) плюс
  уровень-1 `LoadWith` (по умолчанию split-query, включаемый `AsSingleQuery`) с применением глобальных
  фильтров запросов к дочерним наборам ([связи](../../../advanced/relationships.md),
  [eager loading](../../../advanced/eager-loading.md)).
* Пакет интеграции с EF Core (`nextorm.entityframeworkcore`): `UseNextOrm`/`GetNextOrmContext`/
  `AddNextOrmFromDbContext` запускают nextorm поверх соединения EF с маппингом из EF-модели и делят
  транзакцию EF; `ToNextOrm` транслирует ограниченное подмножество EF `IQueryable`
  ([интеграция с EF Core](../../../advanced/integration-efcore.md)).
* Глобальные фильтры запросов (soft-delete / multi-tenancy): предикаты на сущность авто-инъектируются в
  запросы, соединения, подзапросы и eager-загружаемые дочерние наборы, с keyed-фильтрами, выборочным
  `IgnoreFilters`, фильтрами `UPDATE`/`DELETE` и валидацией `INSERT`/`MERGE`
  ([фильтры запросов](../../../advanced/query-filters.md)).
* Перехватчики команд/соединения и структурное логирование над ADO-конвейером
  ([интерсепторы](../../../infrastructure/03-interceptors.md)).
* Кросс-провайдерные row values: конструкторы `System.Tuple`/`ValueTuple`, доступ к элементу и сравнение
  строк рендерятся как `ROW(a, b)`/`(row).fN` в PostgreSQL и `tuple(a, b)`/`tupleElement` в ClickHouse,
  через `ISqlDialect.Tuple`.
* Range-типы без нативной range-колонки: в PostgreSQL `Range<T>` маппится нативно, а у остальных
  провайдеров хранится как пара скалярных границ (`[RangeColumns]`), и вся поверхность предикатов и
  инспекции (`overlaps`, `range_contains`/`range_contained_by`, позиционные/смежные предикаты,
  `lower`/`upper`/`isempty`) транслируется поверх пары — такого маппинга у linq2db нет.
* Хинты индексов (`WithIndex`/`WithoutIndex`) в MySQL/MariaDB, SQLite и SQL Server и настраиваемый
  регистр ключевых слов SQL (`KeywordCase.Upper`) — оба включаются явно и участвуют в ключе плана.
* Коррелированные подзапросы вычисляются построчно и у in-memory-провайдера (scalar/aggregate/`EXISTS`/`IN`
  на глубине один) в дополнение к произвольной глубине у SQL-провайдеров.
* Переносимость между провайдерами: один и тот же C# рендерит `CROSS APPLY` в SQL Server и
  `CROSS JOIN LATERAL` в PostgreSQL/MySQL/MariaDB — через возможности `ISqlDialect`.
* Провайдерные поверхности за тем же гейтом возможностей: нативные JSON и массивы, range/multirange-типы
  и расширенная скалярная библиотека PostgreSQL; `FOR JSON`/`FOR XML`, JSON-как-текст и
  `string_split`/`openjson` в SQL Server;
  `Array(T)`/`ARRAY JOIN`, `JSONExtract*`, словари, семейства quantile/`uniq`/`argMin`-`argMax`,
  `LIMIT BY`, `PREWHERE`/`FINAL`/`SETTINGS` и многофункциональный `multiIf` в ClickHouse.
* Полнотекстовый поиск (`contains`/`freetext`) на SQL Server, PostgreSQL и MySQL/MariaDB.
* Трансляция CLR `Regex` (`Regex.IsMatch`/`Regex.Replace`) с константным шаблоном на PostgreSQL,
  MySQL/MariaDB, ClickHouse, SQLite и SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`) — открытый
  запрос функционала в linq2db (`linq2db#698`).
* Два пути переиспользования (неявный кэш планов и явный `Prepare()`), параметризация запросов и
  малоаллоцирующий дизайн, подтверждённый бенчмарками.
* Производительность по бенчмаркам: на быстром (tmpfs) полном прогоне prepared-путь выигрывает
  **исторический** измеряемый набор классов у Dapper, EF Core и linq2db (`Any`, `First`, `Join`,
  `Single`, `Where`, `LargeIteration` `ToList`/стриминг и `Cache`) при малом объёме аллокаций. Это
  утверждение об этом историческом наборе, а не всеобщая гарантия. Сценарии tier 1 по форме запроса,
  добавленные в #188 (проекция, агрегаты, пагинация, buffered/unbuffered стриминг и синхронный
  raw-reader стриминг), измеряются в исследовательском режиме `ShortRun`/`InProcessEmitToolchain` и
  **не** входят в исторический полный набор; смотрите актуальные результаты по армам в
  `docs/comparisons/benchmarks.md`, а не предполагайте победу в каждом классе. Оставшийся разрыв -
  warm-путь (без `Prepare()`): `CTE` ~1.31×, рекурсивный `CTE` ~1.52×, `Join4` ~1.15× и captured `IN`
  ~1.61–1.70× позади Dapper; итерация 8 закрыла стоимость refresh для инлайн-`IN`
  (`docs/specs/performance/benchmark-report.md`, итерации 3–8).
* Хинты уровня инструкции в SQL Server с участием в ключе плана (`Hint(...)`), корректно сливающиеся с
  предложением CTE `option (maxrecursion n)`, а также табличные хинты SQL Server (`WithTableHint`).
* Настраиваемый вывод SQL: квотирование идентификаторов, соглашения об именовании и регистр ключевых слов
  включаются явно и переопределяются для отдельной команды (`UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`,
  `UseNamingConvention()`/`WithNamingConvention()`, `UseKeywordCase()`/`WithKeywordCase()`), поэтому
  генерируемый SQL по умолчанию предсказуем, а схемы с зарезервированными словами/смешанным регистром,
  snake_case-каталоги и предпочтение верхнего регистра ключевых слов не требуют рукописных имён.

## Осознанные границы: что nextorm оставляет linq2db

Это сознательные решения о scope в сфокусированной модели nextorm без change tracking — не пробелы на
поверхности запросов, где nextorm не уступает linq2db или превосходит её. linq2db их покрывает:

* **Отслеживание изменений и identity map**: nextorm даёт полную явную поверхность DML (`INSERT`/`UPDATE`/
  `DELETE`/полный `MERGE`), массовую вставку, `CREATE TABLE AS SELECT` и транзакции, но каждая запись
  остаётся явной командой — без `SaveChanges` и автоматического отслеживания изменений. linq2db сбрасывает
  отслеживаемый unit of work.
* **Связи**: nextorm декларативно моделирует связи O2M/M2O/O2O/M2M (`[Relationship]`/`HasMany`/`HasOne`/
  `HasOneToOne`/`HasManyThrough` + `JoinInto`) с eager loading уровня 1 (`LoadWith`) и неявной навигацией
  по объявленным связям ([связи](../../../advanced/relationships.md),
  [eager loading](../../../advanced/eager-loading.md),
  [неявная навигация](../../../guide/29-implicit-navigation.md)); linq2db дополнительно даёт только
  ordering/strategy для eager loading — вывод по конвенции FK отсутствует у обоих, так как связи в обеих
  библиотеках объявляются явно.
* **Кодогенерация под существующую БД**: linq2db поставляет CLI/T4-цепочку кодогенерации, которая
  скаффолдит маппинги сущностей и табличных функций из живой базы; nextorm объявляет маппинги в коде.
  Источники с динамической схемой, которые nextorm поддерживает через объявляемую вызывающим схему
  `TRow` (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`,
  [динамическая схема результата](../../../guide/11-table-valued-functions.md#dynamic-result-schema)),
  не поддерживает и linq2db.
* **Широта провайдеров**: пересечение — SQL Server, PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse;
  linq2db дополнительно поставляет Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE,
  DuckDB и Ydb, а nextorm добавляет in-memory-контекст и отдельные провайдеры MySQL/MariaDB (linq2db
  моделирует MariaDB версией MySQL).
* **DDL / управление схемой**: linq2db поставляет `CreateTable`/`DropTable` и schema API; nextorm меняет
  схему только через `CREATE TABLE AS SELECT` — ad-hoc DDL идёт через `ExecuteRaw` (вне области).
* **Внешние / linked-server источники**: `WithServer`/`WithDatabase` лишь переименовывают квалификатор
  `FROM`; `OPENROWSET`/`OPENQUERY` — вне области (у linq2db тоже нет first-class-поверхности).
Корреляция единообразна у SQL-провайдеров (произвольная глубина для скалярных подзапросов, агрегатных
терминалов, `EXISTS`/`IN`/`ANY`/`ALL` и коррелированных источников `APPLY`/`LATERAL`). Провайдер in-memory
теперь вычисляет коррелированные scalar/aggregate/`EXISTS`/`IN` на глубине один построчно и отклоняет
только более глубокие формы — корреляцию глубже одного уровня, внешнюю ссылку во внутренней проекции или
`ORDER BY`, коррелированный `GROUP BY`/`HAVING` и асинхронный внутренний источник (см.
[`sql-capabilities-gap-analysis.md`](../../roadmap/sql-capabilities-gap-analysis.md) §4).

## Архитектурные различия

| Аспект | nextorm | linq2db |
|---|---|---|
| Модель | Построитель запросов и маппер без отслеживания изменений, с полной явной поверхностью записи (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), массовой вставкой и транзакциями | Явный CRUD-ORM со связями; без автоматического change tracking |
| Требование к сущности | Класс сущности необязателен — маппинг через атрибуты/fluent/конвенции, либо вообще без класса через `From("table")` + `TableAlias` | Нужен класс с маппингом (атрибуты, fluent или конвенции) |
| Переиспользование | Неявный кэш планов и `Prepare()` | Compiled queries, кэш запросов |
| Потребление результата | Non-materialising-терминалы первого класса на запросе: потоковая одна LOB-колонка (`ToStream`/`ToTextReader`), принадлежащий вызывающему `DbDataReader` (`ToDataReader`) и экспорт JSON/CSV (`WriteJson`/`WriteCsv`), отдающие строки без создания `TResult` | Всегда материализует mapped-тип на строку; сырой доступ — только через `DataConnection.ExecuteReader`/`DataReaderWrapper` (`DbDataReader`), без терминалов LOB или JSON/CSV |
| Квотирование идентификаторов | выключено по умолчанию; включается через `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` | включено по умолчанию (по провайдеру) |
| Маппинг имён | атрибуты/fluent/авто-вывод, плюс включаемые соглашения об именовании (`UseNamingConvention()`/`WithNamingConvention()`) | fluent/атрибуты (`MappingSchema`) |
| Регистр ключевых слов SQL | настраиваемый, по умолчанию нижний (`KeywordCase`) | фиксированный (канонический для провайдера, верхний) |
| Расширяемость | Контракт диалекта (`ISqlDialect`), атрибуты `[SqlFunction]`/`[SqlTableFunction]`, интерсепторы (`IQueryInterceptor`), глобальные фильтры запросов и сырой SQL | Интерсепторы, собственный SQL, runtime `MappingSchema` и пакеты-расширения провайдеров |

## Итог

Для чтения, отчётности и явного изменения данных по существующей схеме nextorm — более сильный выбор: он
покрывает практически всю аналитическую поверхность запросов, которую даёт linq2db — провайдерные
семейства функций, специфичные для ClickHouse конструкции, кросс-провайдерные row values, TVF и многое
другое — при меньшем объёме аллокаций, результатах бенчмарков на уровне или выше Dapper, EF Core и linq2db
на поставляемых сценариях и более настраиваемом выводе SQL (квотирование идентификаторов, соглашения об
именовании и регистр ключевых слов включаются явно и переопределяются для отдельной команды, тогда как
linq2db квотирует по умолчанию и фиксирует имена через схему отображения). linq2db остаётся лучшим выбором только когда тот же слой должен дополнительно отслеживать изменения,
генерировать слой доступа к данным из живой схемы или давать ordering/strategy для eager loading — то,
что nextorm осознанно оставляет за рамками (связи O2M/M2O/O2O, many-to-many через junction, неявная
навигация по объявленным связям и eager loading уровня 1 уже покрыты).

## См. также

- [Матрица возможностей: nextorm vs EF Core и linq2db](../../comparison/capability-matrix.md) — исчерпывающая матрица по конструкциям.
- [Gap-анализ открытого backlog linq2db](../../comparison/linq2db-backlog-gap-analysis.md) — что linq2db *планирует добавить* и чего из этого не хватает nextorm.
- [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core и linq2db, по конструкциям.
- [Ограничения и возможности вне области охвата](../../../advanced/limitations.md)
- [Соединения](../../../guide/02-joins.md) — `CrossApply`/`OuterApply`.
- [Range-колонки](../../../guide/25-range-columns.md) — `Range<T>`, хранимый как пара скалярных колонок.
- [Хинты запросов](../../../guide/13-query-hints.md)
- [Обзор провайдеров](../../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
Возможности linq2db описаны по его публичной документации.
