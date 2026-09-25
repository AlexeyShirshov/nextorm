# nextorm vs linq2db: сравнение функционала

> Сравнение функционала nextorm и linq2db. Дополняет [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md)
> и [матрицу возможностей](../../comparison/capability-matrix.md) (там же рассматривается EF Core). По текущему
> дереву видно, что на аналитической поверхности запросов nextorm не уступает linq2db, а местами превосходит
> её — типы соединений, `APPLY`/`LATERAL`, full-text/JSON/массивы, кросс-провайдерные кортежи/row values,
> нативные range-типы (и range поверх пары скаляров), трансляция CLR `Regex`,
> `ROLLUP`/`CUBE`/`GROUPING SETS`, арифметика дат, временные таблицы, блокировки строк, хинты
> запросов/таблиц/индексов, настраиваемый регистр ключевых слов, TVF и корреляция in-memory на глубине
> один — и добавляет полную явную поверхность записи
> (`INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, модифицирующие CTE PostgreSQL), массовую вставку,
> `CREATE TABLE AS SELECT` и роль транзакций.

**Предварительные требования:** [Обзор провайдеров](../../../providers/overview.md) · [Ограничения](../../../advanced/limitations.md) · [Хинты запросов](../../../guide/17-query-hints.md)

## Позиционирование

* **nextorm** — сфокусированный построитель SQL и маппер без отслеживания изменений, рассчитанный на чтение
  и отчётность. Он покрывает полную аналитическую поверхность запросов, добавляет полную явную поверхность
  записи (`INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, массовая вставка, `CREATE TABLE AS SELECT`,
  а также модифицирующие CTE в PostgreSQL) и роль транзакций (`ITransactionManager`, собственные и
  привязанные) и генерирует переносимый между SQL Server, PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse SQL.
  В основе — малый объём аллокаций, параметризация и компиляция запросов (неявный кэш планов / явный
  `Prepare()`), плюс включаемые настройки вывода (квотирование идентификаторов, соглашения об именовании,
  регистр ключевых слов) и хинты индексов — и бенчмарки на уровне или выше Dapper, EF Core и linq2db на
  поставляемых сценариях.
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

| Область | linq2db | nextorm | Обоснование в nextorm |
|---|---|---|---|
| Проекция (`SELECT`, DTO/анонимные/record/tuple/scalar) | yes | yes | `EntityBuilder.Select` |
| Предикаты (`WHERE`: сравнения, `and`/`or`/`!`, арифметика, битовые/сдвиги) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | **yes** (`FULL JOIN` нет в MySQL/MariaDB) | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes | **yes** (отключено в SQLite/ClickHouse) | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Строгость соединений (`ANY`/`ALL`/`ASOF`) и `GLOBAL` | no | **yes** в ClickHouse | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Арность соединений | yes | **yes** — до 8 | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Соединение с производной таблицей (подзапросом) | yes | **yes** | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Подзапросы (`FROM`, скалярные, коррелированные `EXISTS/IN/ANY/ALL`) | yes | **yes** (in-memory — только глубина один) | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `DataContext/InMemoryCorrelatedPlan.cs` |
| `IN` по списку/массиву | partial — нет распределённого `GLOBAL IN` в ClickHouse | **yes** | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs`, `SqlFunctions.ClickHouse.global_in` |
| `GROUP BY` / `HAVING` / агрегаты | yes | **yes** | `EntityBuilder.GroupBy/Having`, `AdvancedAggregateTranslator.cs`, `Supports*Aggregates` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes | **yes** (`WITH TOTALS` в ClickHouse) | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | no | **yes** в ClickHouse | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | no | **yes** в ClickHouse | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, диалектные `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL`, где поддерживает движок) | yes | yes (зависит от провайдера; `ALL` в PostgreSQL, MariaDB и ClickHouse) | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | no | **yes** в зависимости от провайдера (`DISTINCT ON` PostgreSQL; `WITH TIES` PostgreSQL + SQL Server; `TABLESAMPLE` PostgreSQL + SQL Server) | `DistinctOn`/`WithTies`/`FromOptions.TableSample`, `ISqlDialect.SupportsWithTies` |
| Блокировка строк (`FOR UPDATE`/`FOR SHARE`, `NOWAIT`/`SKIP LOCKED`) | yes (зависит от провайдера, `SubQueryTableHint`: PostgreSQL `FOR UPDATE`/`FOR NO KEY UPDATE`/`FOR SHARE`/`FOR KEY SHARE`, MySQL `FOR UPDATE`/`FOR SHARE`/`LOCK IN SHARE MODE`, SQL Server — табличные хинты `UPDLOCK`/`XLOCK`; плюс `NOWAIT`/`SKIP LOCKED`) | **yes** — `FOR UPDATE`/`FOR SHARE` по провайдерам (PostgreSQL/MySQL/MariaDB — трейлинг `FOR UPDATE`/`LOCK IN SHARE MODE`; SQL Server — через табличные хинты) **плюс режимы ожидания `NOWAIT`/`SKIP LOCKED`** (`LockWaitMode`; MySQL переключает разделяемую блокировку на `FOR SHARE`, SQL Server использует `READPAST` как приближение `SKIP LOCKED`) | `ForUpdate`/`ForShare` + `LockWaitMode`, `ILockRenderer`/`ILockRenderer.UsesTableHints`/`Render` |
| Временные таблицы (`FOR SYSTEM_TIME`) | yes (SQL Server) | **yes** на SQL Server + MariaDB (`CONTAINED IN` — только SQL Server) | `ForSystemTime`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| CTE (включая рекурсивные) | yes | **yes** (модифицирующие CTE PostgreSQL) | `Builders/CteQuery.cs`, `Builders/MutationCteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive`, `ISqlDialect.SupportsDataModifyingCtes` |
| Оконные функции (`OVER`, ranking, framed aggregates, `lag`/`lead`) | partial — нет именованных окон и кадров `GROUPS`/`EXCLUDE` | **yes** | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion` |
| `CASE WHEN` / тернарный / `switch`, `COALESCE`, числовой `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| Строковые / математические / date скалярные функции, `LIKE` | yes | **yes** — портируемые CLR-методы `string` на всех провайдерах плюс нативная библиотека строк/`regexp_*` на PostgreSQL (`SqlFunctions.Postgres`) | `Visitors/ScalarFunctionTranslator.cs`, диалектные `Make*`, `SqlFunctions.Postgres` |
| CLR `Regex` (`IsMatch`/`Replace`, константный шаблон) | partial — открытый `linq2db#698` (трансляции `Regex.IsMatch` нет) | **yes** — PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite и SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`; 2019/2022 отклоняют) | `Visitors/RegexSqlTranslator.cs`, `ISqlDialect.SupportsRegex`/`MakeRegexMatch`/`MakeRegexReplace` |
| Арифметика дат (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | **yes** | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Полнотекстовый поиск | yes (провайдер) | **yes** на SQL Server, PostgreSQL и MySQL/MariaDB | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Нативные JSON-документы | yes | **yes на PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON скалярные функции (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | **yes на SQL Server и MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| Строковый JSON + функции словарей (ClickHouse) | no | **yes на ClickHouse** | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Массивы (`cardinality`/`array_*`/`@>`/`&&`, `Array(T)` в ClickHouse, `ARRAY JOIN`) | no | **yes** на PostgreSQL и ClickHouse | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Row values / кортежи (`ROW`/`(a, b)`, доступ к элементу, сравнение строк) | yes (`Sql.Row`; эмулируется там, где у провайдера нет нативного row) | **yes** на PostgreSQL и ClickHouse | `ISqlDialect.Tuple`/`ITupleRenderer`, `Visitors/TupleSqlTranslator.cs` |
| Нативные range-типы + range поверх пары скаляров (`Range<T>`, `Overlaps`, `range_contains`, инспекция границ) | partial — только `Sql.Row.Overlaps` (нет полноценного маппинга `Range<T>`) | **yes** — нативные range/multirange-типы на PostgreSQL; **пара скалярных колонок** (`[RangeColumns]`) на SQL Server/MySQL/MariaDB/SQLite/ClickHouse | `Query/Range.cs`, `RangeColumnsAttribute`, `ISqlDialect.SupportsRanges`/`SupportsRangeColumns`, `SqlFunctions.Postgres` |
| Условные функции (`iif`/`choose`/`multi_if`) | no | **yes** | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes (провайдер) | **yes на SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| Методы типа XML (`.value`/`.query`/`.exist`/`.nodes`) | yes (провайдер) | **partial** — только SQL Server | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | partial | **yes** (обработка NULL зависит от провайдера) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | **yes** (`array_agg` на PostgreSQL) | `SupportsStringAgg`/`SupportsArrayAgg` |
| Пользовательские скалярные функции | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Табличные функции | yes (`TableFunction`; табличные функции БД можно скаффолдить) | **yes** (`[SqlTableFunction]`) | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Нативный источник `PIVOT` / `UNPIVOT` | no (сырой SQL) | **yes на SQL Server** | `EntityBuilder.Pivot`/`Unpivot` |
| Сырой SQL (запрос целиком) | yes | yes | `WithSql` / `PrepareFromSql` |
| Сырой SQL как композируемый источник/подзапрос | yes | **yes** | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Хинты уровня инструкции | partial (зависит от провайдера, `QueryHint`: SQL Server `OPTION (...)`, MySQL/Oracle `/*+ ... */`, ClickHouse `SETTINGS`; в PostgreSQL API хинтов нет) | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB встроенный `/*+ ... */`; SQLite/ClickHouse отклоняют | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Блокирующие табличные хинты (например `WITH (NOLOCK)`) | yes — только SQL Server (`SqlServerHints.TableHint`; в MySQL `TableHint` — только optimizer-хинты, в PostgreSQL/ClickHouse синтаксиса table hints нет) | **yes** — только SQL Server | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Хинты индексов (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | yes (`IndexHint`/`TableHint`, зависит от провайдера) | **yes** — MySQL/MariaDB, SQLite и SQL Server; PostgreSQL (без `pg_hint_plan`), ClickHouse и in-memory отклоняют | `EntityBuilder.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| Квотирование идентификаторов | yes (по провайдеру) | **yes** (включается явно) | `ISqlDialect.QuoteIdentifier` |
| Соглашения об именовании (например snake_case) | partial — нет встроенной конвенции | **yes** (включается явно, встроенный `SnakeCaseNamingConvention`) | `INamingConvention` / `SnakeCaseNamingConvention` |
| Регистр ключевых слов SQL (верхний/нижний) | no (ключевые слова выводятся в каноническом регистре провайдера) | **yes** — включается через `KeywordCase.Upper`; по умолчанию `KeywordCase.Lower` байт-в-байт совпадает с историческим выводом | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **yes** | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete`; in-memory применяет только key upsert |
| Bulk copy / merge / временные таблицы | yes | **частично** — key upsert (`MergeInto`/`MergeBuilder<T>`), полный `MERGE` с ветками (`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource`, произвольные условия, `RETURNING`/`OUTPUT`; SQL Server, PostgreSQL 15+), массовая вставка (`BulkInsertInto<T>`: нативные `COPY`/`SqlBulkCopy` + чанковый `INSERT ... VALUES`, настройка через record `BulkInsertOptions` или fluent-`BulkInsertOptionsBuilder` — `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`, `IgnoreDuplicates`, `KeepIdentity`, `Timeout`, прогресс `NotifyAfter` с `ProgressCancellationTokenSource`; `ReturningKey`/`Returning`) и материализация запроса во (временную) таблицу ([`ToTempTable`/`ToTable`](../../../ru/guide/22-create-table-as.md), `CREATE [TEMPORARY] TABLE ... AS SELECT`, PostgreSQL/SQLite/MySQL/MariaDB) реализованы | `MergeBuilder<TEntity>`, `BulkInsertBuilder<TEntity>`, `BulkInsertOptions`, `TempTableExtensions` |
| Транзакции (собственные и привязанные) | yes | **yes** (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse и in-memory отклоняют) | `DataContext/Roles/ITransactionManager.cs`, `DataContext/DbConnectionManager.cs`, `ISqlDialect.SupportsTransactions` |
| Навигационные свойства / связи / eager loading | yes (`[Association]`, `LoadWith`) | **no** | — |
| Отслеживание изменений / identity map | partial | **no** (по замыслу) | — |
| Расширяемость (интерсепторы, собственный SQL, фильтры) | обширная | минимальная (диалект + `[SqlFunction]`/`[SqlTableFunction]`) | `SqlDialectBase` |
| Провайдеры | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | `src/nextorm.*` |
| Интеграция с EF Core | yes (`linq2db.EntityFrameworkCore`) | **no** (запланировано: [интеграция с EF Core](../../roadmap/todo_efcore_integration.md)) | — |
| Производительность | высокая | по бенчмаркам на уровне/выше Dapper, EF Core и linq2db на поставляемых сценариях | `docs/specs/performance/benchmark-report.md` |

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
* Производительность по бенчмаркам: на быстром (tmpfs) полном прогоне prepared-путь выигрывает все
  измеряемые классы у Dapper, EF Core и linq2db (`Any`, `First`, `Join`, `Single`, `Where`,
  `LargeIteration` `ToList`/стриминг и `Cache`) при малом объёме аллокаций. Оставшийся разрыв -
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
* **Связи**: у nextorm нет метаданных связей — соединения всегда явные; linq2db добавляет `[Association]`,
  eager loading `LoadWith` и неявный вывод соединений.
* **Плагинная расширяемость и более широкие табличные хинты**: linq2db предлагает интерсепторы, фильтры
  запросов и собственный SQL-маппинг, плюс табличные хинты на большем числе провайдеров (например Oracle);
  nextorm осознанно держит фиксированный контракт диалекта с хинтами уровня инструкции, блокирующими
  хинтами SQL Server и хинтами индексов в MySQL/MariaDB, SQLite и SQL Server.
* **Кодогенерация под существующую БД**: linq2db поставляет CLI/T4-цепочку кодогенерации, которая
  скаффолдит маппинги сущностей и табличных функций из живой базы; nextorm объявляет маппинги в коде.
  Источники с динамической схемой, которые nextorm поддерживает через объявляемую вызывающим схему
  `TRow` (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`,
  [динамическая схема результата](../../../guide/13-table-valued-functions.md#dynamic-result-schema)),
  не поддерживает и linq2db.
* **Широта провайдеров**: linq2db добавляет Oracle, Firebird, DB2, SAP HANA, Informix, Sybase и SQL CE;
  nextorm сосредоточен на SQL Server, PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse.
* **Пакет интеграции с EF Core** и более крупная экосистема (у nextorm интеграция
  [запланирована](../../roadmap/todo_efcore_integration.md)).

Корреляция единообразна у SQL-провайдеров (произвольная глубина для скалярных подзапросов, агрегатных
терминалов, `EXISTS`/`IN`/`ANY`/`ALL` и коррелированных источников `APPLY`/`LATERAL`). Провайдер in-memory
теперь вычисляет коррелированные scalar/aggregate/`EXISTS`/`IN` на глубине один построчно и отклоняет
только более глубокие формы — корреляцию глубже одного уровня, внешнюю ссылку во внутренней проекции или
`ORDER BY`, коррелированный `GROUP BY`/`HAVING` и асинхронный внутренний источник (см.
[`sql-capabilities-gap-analysis.md`](../../roadmap/sql-capabilities-gap-analysis.md) §4).

## Архитектурные различия

| Аспект | linq2db | nextorm |
|---|---|---|
| Модель | Явный CRUD-ORM со связями; без автоматического change tracking | Построитель запросов и маппер без отслеживания изменений, с полной явной поверхностью записи (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), массовой вставкой и транзакциями |
| Требование к сущности | Нужен класс с маппингом (атрибуты, fluent или конвенции) | Класс сущности необязателен — маппинг через атрибуты/fluent/конвенции, либо вообще без класса через `From("table")` + `TableAlias` |
| Переиспользование | Compiled queries, кэш запросов | Неявный кэш планов и `Prepare()` |
| Квотирование идентификаторов | включено по умолчанию (по провайдеру) | выключено по умолчанию; включается через `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` |
| Маппинг имён | fluent/атрибуты (`MappingSchema`) | атрибуты/fluent/авто-вывод, плюс включаемые соглашения об именовании (`UseNamingConvention()`/`WithNamingConvention()`) |
| Регистр ключевых слов SQL | фиксированный (канонический для провайдера, верхний) | настраиваемый, по умолчанию нижний (`KeywordCase`) |
| Расширяемость | Интерсепторы, собственный SQL, расширения провайдеров | Контракт диалекта и атрибуты функций |

## Итог

Для чтения, отчётности и явного изменения данных по существующей схеме nextorm — более сильный выбор: он
покрывает практически всю аналитическую поверхность запросов, которую даёт linq2db — провайдерные
семейства функций, специфичные для ClickHouse конструкции, кросс-провайдерные row values, TVF и многое
другое — при меньшем объёме аллокаций, результатах бенчмарков на уровне или выше Dapper, EF Core и linq2db
на поставляемых сценариях и более настраиваемом выводе SQL (квотирование идентификаторов, соглашения об
именовании и регистр ключевых слов включаются явно и переопределяются для отдельной команды, тогда как
linq2db квотирует по умолчанию и фиксирует имена через схему отображения). linq2db остаётся лучшим выбором
только тогда, когда тот же слой должен ещё и моделировать связи, отслеживать изменения или генерировать
слой доступа к данным из живой схемы — то, что nextorm осознанно
оставляет за рамками.

## См. также

- [Матрица возможностей: nextorm vs EF Core и linq2db](../../comparison/capability-matrix.md) — исчерпывающая матрица по конструкциям.
- [Gap-анализ открытого backlog linq2db](../../comparison/linq2db-backlog-gap-analysis.md) — что linq2db *планирует добавить* и чего из этого не хватает nextorm.
- [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core и linq2db, по конструкциям.
- [Ограничения и возможности вне области охвата](../../../advanced/limitations.md)
- [Соединения](../../../guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Range-колонки](../../../guide/31-range-columns.md) — `Range<T>`, хранимый как пара скалярных колонок.
- [Хинты запросов](../../../guide/17-query-hints.md)
- [Обзор провайдеров](../../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
Возможности linq2db описаны по его публичной документации.
