# nextorm vs linq2db: сравнение функционала

> Сравнение функционала nextorm и linq2db. Дополняет [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md)
> и [матрицу возможностей](../../comparison/capability-matrix.md) (там же рассматривается EF Core). По текущему
> дереву видно, что на аналитической поверхности запросов nextorm не уступает linq2db, а местами превосходит
> её — типы соединений, `APPLY`/`LATERAL`, full-text/JSON/массивы, кросс-провайдерные кортежи/row values,
> `ROLLUP`/`CUBE`/`GROUPING SETS`, арифметика дат, временные таблицы, блокировки строк, хинты
> запросов/таблиц/индексов, настраиваемый регистр ключевых слов, TVF и корреляция in-memory на глубине
> один — и добавляет явную поверхность `INSERT ... VALUES`/`INSERT ... SELECT`/возврата строк, включая
> модифицирующие CTE PostgreSQL.

**Предварительные требования:** [Обзор провайдеров](../../../providers/overview.md) · [Ограничения](../../../advanced/limitations.md) · [Хинты запросов](../../../guide/17-query-hints.md)

## Позиционирование

* **nextorm** — сфокусированный построитель SQL и маппер без отслеживания изменений, рассчитанный на чтение
  и отчётность. Он покрывает полную аналитическую поверхность запросов, добавляет явную поверхность
  `INSERT ... VALUES`/`INSERT ... SELECT`/возврата строк (одна строка, сущность, батч, сгенерированный
  ключ, а также модифицирующие CTE в PostgreSQL) и генерирует переносимый между SQL Server, PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse SQL.
  В основе — малый объём аллокаций, параметризация и компиляция запросов (неявный кэш планов / явный
  `Prepare()`), плюс включаемые настройки вывода (квотирование идентификаторов, соглашения об именовании,
  регистр ключевых слов) и хинты индексов — и бенчмарки на уровне или выше Dapper, EF Core и linq2db на
  поставляемых сценариях.
* **linq2db** — широкий зрелый LINQ-to-SQL ORM: более широкая матрица провайдеров, полный CRUD
  (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), связи/eager loading, bulk copy, временные таблицы, кодогенерация
  под существующую БД, расширяемость (интерсепторы, собственный SQL-маппинг) и пакет интеграции с EF Core.
  Эта дополнительная поверхность идёт вместе с change tracking и более тяжёлой моделью.

Библиотеки пересекаются на *поверхности запросов* и базовых вставках — там nextorm не уступает linq2db или
превосходит её — и расходятся в *полном изменении данных*, *моделировании связей* и *инструментарии*,
которые nextorm осознанно оставляет за рамками.

## Матрица возможностей

Обозначения: **yes** — полноценная поддержка; **partial** — поддержка с ограничениями; **no** — нет.
Для nextorm указан исходный файл, отвечающий за поведение.

| Область | linq2db | nextorm | Обоснование в nextorm |
|---|---|---|---|
| Проекция (`SELECT`, DTO/анонимные/record/tuple/scalar) | yes | yes | `EntityBuilder.Select` |
| Предикаты (`WHERE`: сравнения, `and`/`or`/`!`, арифметика, битовые/сдвиги) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | **yes** (`FULL JOIN` нет в MySQL/MariaDB) | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes | **yes** (включая коррелированные источники; отключено в SQLite/ClickHouse) | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Строгость соединений (`ANY`/`ALL`/`ASOF`) и `GLOBAL` | no | **yes** в ClickHouse (`SEMI`/`ANTI`/`PASTE` через `SemiJoin`/`AntiJoin`/`PasteJoin`) | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Арность соединений | не ограничена | 2–8 (ограничение на этапе компиляции) | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Соединение с производной таблицей (подзапросом) | yes | **yes** — с любой стороны: присоединяемая (`Join(QueryCommand<T>)`) или основной источник `FROM` | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Подзапросы (`FROM`, скалярные, коррелированные `EXISTS/IN/ANY/ALL`) | yes | yes — корреляция на любой глубине у SQL-провайдеров и на глубине один (scalar/aggregate/`EXISTS`/`IN`) у in-memory; более глубокие формы in-memory выбрасывают `NotSupportedException` | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `DataContext/InMemoryCorrelatedPlan.cs` |
| `IN` по списку/массиву | yes | yes (+ распределённый `GLOBAL IN` в ClickHouse) | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs`, `SqlFunctions.ClickHouse.global_in` |
| `GROUP BY` / `HAVING` / агрегаты | yes | yes — плюс `FILTER (WHERE ...)`, булевы/битовые/статистические/регрессионные агрегаты и семейства `arg_min`/`arg_max`, `uniq*`, `quantile*`/`median`, `-If` | `EntityBuilder.GroupBy/Having`, `AdvancedAggregateTranslator.cs`, `Supports*Aggregates` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes | yes (зависит от провайдера; `WITH TOTALS` в ClickHouse) | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | no | **только ClickHouse** | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | no | **только ClickHouse** (кросс-провайдерный аналог `TABLESAMPLE` — отдельно, ниже) | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, диалектные `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL`, где поддерживает движок) | yes | yes (зависит от провайдера; `ALL` в PostgreSQL, MariaDB и ClickHouse) | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | no | yes (зависит от провайдера: `DISTINCT ON` PostgreSQL; `WITH TIES` PostgreSQL + SQL Server; `TABLESAMPLE` PostgreSQL + SQL Server) | `DistinctOn`/`WithTies`/`TableSample`, `ISqlDialect.SupportsWithTies` |
| Блокировка строк (`FOR UPDATE`/`FOR SHARE`) | partial — SQL Server через табличные хинты `UPDLOCK`/`XLOCK`, иначе зависит от провайдера | yes (зависит от провайдера: PostgreSQL/MySQL/MariaDB — трейлинг `FOR UPDATE`/`LOCK IN SHARE MODE`; SQL Server — через табличные хинты) | `ForUpdate`/`ForShare`, `ILockRenderer`/`ILockRenderer.UsesTableHints` |
| Временные таблицы (`FOR SYSTEM_TIME`) | yes (SQL Server) | **SQL Server + MariaDB** (`CONTAINED IN` — только SQL Server) | `ForSystemTime`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| CTE (включая рекурсивные) | yes | yes — плюс **модифицирующие CTE PostgreSQL** (`With(имя, insert)`, типизированное чтение `RETURNING`, тело `INSERT ... VALUES`/`INSERT ... SELECT`, главный `INSERT ... SELECT` поверх mutation-CTE) | `Builders/CteQuery.cs`, `Builders/MutationCteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive`, `ISqlDialect.SupportsDataModifyingCtes` |
| Оконные функции (`OVER`, ranking, framed aggregates, `lag`/`lead`) | yes | yes — плюс именованные окна, единица кадра `GROUPS`, `EXCLUDE`, `percent_rank`/`cume_dist`, `nth_value` и `lagInFrame`/`leadInFrame` в ClickHouse | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion` |
| `CASE WHEN` / тернарный / `switch`, `COALESCE`, числовой `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| Строковые / математические / date скалярные функции, `LIKE` | yes | yes | `Visitors/ScalarFunctionTranslator.cs`, диалектные `Make*` |
| Арифметика дат (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes для всех провайдеров (допустимые поля различаются и валидируются по провайдеру) | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Полнотекстовый поиск | yes (провайдер) | **yes** на SQL Server, PostgreSQL и MySQL/MariaDB (булевы предикаты; без ранжирования) | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Нативные JSON-документы | yes | **yes на PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON скалярные функции (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | **yes на SQL Server и MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| Строковый JSON + функции словарей (ClickHouse) | no | **yes на ClickHouse** | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Массивы (`cardinality`/`array_*`/`@>`/`&&`, `Array(T)` в ClickHouse, `ARRAY JOIN`) | no | **yes на PostgreSQL и ClickHouse** (`ARRAY JOIN`; функции высшего порядка и row reader `Array(T)`/`Tuple` реализованы) | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Row values / кортежи (`ROW`/`(a, b)`, доступ к элементу, сравнение строк) | yes (`Sql.Row`; эмулируется там, где у провайдера нет нативного row) | **yes на PostgreSQL и ClickHouse** (`ROW(a, b)`/`(row).fN` и `tuple(a, b)`/`tupleElement`); SQL Server, MySQL/MariaDB, SQLite и in-memory отклоняют | `ISqlDialect.Tuple`/`ITupleRenderer`, `Visitors/TupleSqlTranslator.cs` |
| Условные функции (`iif`/`choose`/`multi_if`) | no | **yes** (переносимый `iif`; `choose` только SQL Server; `multi_if` ClickHouse) | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes (провайдер) | **yes на SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| Методы типа XML (`.value`/`.query`/`.exist`/`.nodes`) | yes (провайдер) | **partial** — только SQL Server | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | partial | **yes** (обработка NULL зависит от провайдера) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | **yes** — `string_agg` кросс-провайдерно; `array_agg` на PostgreSQL | `SupportsStringAgg`/`SupportsArrayAgg` |
| Пользовательские скалярные функции | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Табличные функции | yes (`TableFunction`; табличные функции БД можно скаффолдить) | yes (`[SqlTableFunction]`); небольшой встроенный набор, gated по провайдеру (`generate_series`/`unnest`, `string_split`/`openjson`/`containstable`/`freetexttable`, ClickHouse `numbers`/`zeros`/`generateRandom` + серверные/кластерные); остальное — пользовательские обёртки | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Нативный источник `PIVOT` / `UNPIVOT` | no (сырой SQL) | **yes на SQL Server** | `EntityBuilder.Pivot`/`Unpivot` |
| Сырой SQL (запрос целиком) | yes | yes | `WithSql` / `PrepareFromSql` |
| Сырой SQL как композируемый источник/подзапрос | yes | **yes** — `FromSql` рендерит фрагмент как производную таблицу, можно соединять/фильтровать дальше | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Хинты уровня инструкции | yes (зависит от провайдера) | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB встроенный `/*+ ... */`; SQLite/ClickHouse отклоняют | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Блокирующие табличные хинты (например `WITH (NOLOCK)`) | yes | **partial** — только SQL Server | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Хинты индексов (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | yes (`IndexHint`/`TableHint`, зависит от провайдера) | **yes** — MySQL/MariaDB, SQLite и SQL Server; PostgreSQL (без `pg_hint_plan`), ClickHouse и in-memory отклоняют | `EntityBuilder.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| Квотирование идентификаторов | yes (по провайдеру) | включается явно — `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`; по умолчанию физические имена выводятся как есть | `ISqlDialect.QuoteIdentifier` |
| Соглашения об именовании (например snake_case) | через `MappingSchema`/атрибуты (встроенной конвенции нет) | включается явно — `UseNamingConvention()`/`WithNamingConvention()`; встроенный `SnakeCaseNamingConvention`; явные имена — дословно | `INamingConvention` / `SnakeCaseNamingConvention` |
| Регистр ключевых слов SQL (верхний/нижний) | no (ключевые слова выводятся в каноническом регистре провайдера) | **yes** — включается через `KeywordCase.Upper`; по умолчанию `KeywordCase.Lower` байт-в-байт совпадает с историческим выводом | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **частично** — `INSERT ... VALUES` (одна строка, сущность, батч), `INSERT ... SELECT` (`Values(source, mapping)` поверх `EntityBuilder`), сгенерированный ключ (`ReturningIdentity`/`ReturningKey`) и возврат строк (`Returning`, только PostgreSQL/SQLite/SQL Server) через `InsertInto`; **модифицирующие CTE PostgreSQL** (`With(имя, insert)`/`CteQuery.With(имя, insert)` → `MutationCteQuery<T>`: write-CTE, чьи `RETURNING`-строки читаются типизированно через `From`/`FromTable` и могут питать главный `INSERT ... SELECT`); **key upsert** (`MergeInto` → `MergeBuilder<T>`, родные `ON CONFLICT ... DO UPDATE`/`ON DUPLICATE KEY UPDATE`/`MERGE ... USING (VALUES ...)`, ClickHouse/in-memory отклоняют); **`DELETE`** (`DeleteFrom` → `DeleteBuilder<T>` и `Delete<T>(entity)` по объявленному ключу, явный `All()` для удаления всей таблицы, родной `DELETE FROM <table> [WHERE ...]`, `Returning()` для удалённых строк, `Truncate<T>()` для `TRUNCATE TABLE`, ClickHouse-мутация `ALTER TABLE ... DELETE`); **`UPDATE`** (`Update<T>(entity)`/`UpdateBuilder<T>`, predicate/key, `Returning`, multi-table join, ClickHouse-мутация) и **полный `MERGE`** с ветками `WHEN MATCHED`/`WHEN NOT MATCHED`/`WHEN NOT MATCHED BY SOURCE`, условиями и `RETURNING`/`OUTPUT` (SQL Server, PostgreSQL 15+) реализованы | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete` |
| Bulk copy / merge / временные таблицы | yes | **частично** — key upsert (`MergeInto`/`MergeBuilder<T>`), полный `MERGE` с ветками (`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource`, произвольные условия, `RETURNING`/`OUTPUT`; SQL Server, PostgreSQL 15+) и материализация запроса во (временную) таблицу ([`ToTempTable`/`ToTable`](../../../ru/guide/22-create-table-as.md), `CREATE [TEMPORARY] TABLE ... AS SELECT`, PostgreSQL/SQLite/MySQL/MariaDB) реализованы; bulk insert запланирован: [bulk insert](../../roadmap/todo_bulk_insert.md) | `MergeBuilder<TEntity>`, `TempTableExtensions` |
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
* Компактная явная поверхность записи: `INSERT ... VALUES` (одна строка, сущность, батч, `DEFAULT`/
  all-defaults) и `INSERT ... SELECT`, сгенерированный
  ключ (`ReturningIdentity`/`ReturningKey`), возврат вставленных строк (`Returning`, в
  PostgreSQL/SQLite/SQL Server) и, в PostgreSQL, **модифицирующие CTE** (`With(имя, insert)`/
  `CteQuery.With(имя, insert)` → write-CTE, чьи `RETURNING`-строки читаются типизированно или питают
  следующий `INSERT ... SELECT`) — явные команды, без change tracking и `SaveChanges`.
* Кросс-провайдерные row values: конструкторы `System.Tuple`/`ValueTuple`, доступ к элементу и сравнение
  строк рендерятся как `ROW(a, b)`/`(row).fN` в PostgreSQL и `tuple(a, b)`/`tupleElement` в ClickHouse,
  через `ISqlDialect.Tuple`.
* Хинты индексов (`WithIndex`/`WithoutIndex`) в MySQL/MariaDB, SQLite и SQL Server и настраиваемый
  регистр ключевых слов SQL (`KeywordCase.Upper`) — оба включаются явно и участвуют в ключе плана.
* Коррелированные подзапросы вычисляются построчно и у in-memory-провайдера (scalar/aggregate/`EXISTS`/`IN`
  на глубине один) в дополнение к произвольной глубине у SQL-провайдеров.
* Переносимость между провайдерами: один и тот же C# рендерит `CROSS APPLY` в SQL Server и
  `CROSS JOIN LATERAL` в PostgreSQL/MySQL/MariaDB — через возможности `ISqlDialect`.
* Провайдерные поверхности за тем же гейтом возможностей: нативные JSON и массивы, расширенная скалярная
  библиотека PostgreSQL; `FOR JSON`/`FOR XML`, JSON-как-текст и `string_split`/`openjson` в SQL Server;
  `Array(T)`/`ARRAY JOIN`, `JSONExtract*`, словари, семейства quantile/`uniq`/`argMin`-`argMax`,
  `LIMIT BY`, `PREWHERE`/`FINAL`/`SETTINGS` и многофункциональный `multiIf` в ClickHouse.
* Полнотекстовый поиск (`contains`/`freetext`) на SQL Server, PostgreSQL и MySQL/MariaDB.
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

* **Полное изменение данных**: nextorm даёт только явную поверхность записи (`INSERT ... VALUES`/
  `INSERT ... SELECT`, возврат строк, key upsert, `DELETE` и модифицирующие CTE в PostgreSQL);
  полные `UPDATE` и `MERGE` с ветками, bulk copy, временные таблицы и change tracking — вне области по
  замыслу (каждая запись — явная команда, без identity map). linq2db покрывает полный CRUD.
* **Связи**: у nextorm нет метаданных связей — соединения всегда явные; linq2db добавляет `[Association]`,
  eager loading `LoadWith` и неявный вывод соединений.
* **Плагинная расширяемость и более широкие табличные хинты**: linq2db предлагает интерсепторы, фильтры
  запросов и собственный SQL-маппинг, плюс табличные хинты на большем числе провайдеров (например Oracle);
  nextorm осознанно держит фиксированный контракт диалекта с хинтами уровня инструкции, блокирующими
  хинтами SQL Server и хинтами индексов в MySQL/MariaDB, SQLite и SQL Server.
* **Кодогенерация под существующую БД**: linq2db поставляет CLI/T4-цепочку кодогенерации, которая
  скаффолдит маппинги сущностей и табличных функций из живой базы; nextorm объявляет маппинги в коде.
  Источники с динамической схемой, которых у nextorm всё ещё нет (ClickHouse `values()`, PostgreSQL
  `jsonb_to_record(set)`), не поддерживает и linq2db — это общий пробел, отслеживаемый в
  [`todo_dynamic_result_schema.md`](../../roadmap/todo_dynamic_result_schema.md).
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
| Модель | Явный CRUD-ORM со связями; без автоматического change tracking | Построитель запросов и маппер без отслеживания изменений (реализован `INSERT`) |
| Требование к сущности | Нужен класс с маппингом (атрибуты, fluent или конвенции) | Класс сущности необязателен — маппинг через атрибуты/fluent/конвенции, либо вообще без класса через `From("table")` + `TableAlias` |
| Переиспользование | Compiled queries, кэш запросов | Неявный кэш планов и `Prepare()` |
| Квотирование идентификаторов | включено по умолчанию (по провайдеру) | выключено по умолчанию; включается через `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` |
| Маппинг имён | fluent/атрибуты (`MappingSchema`) | атрибуты/fluent/авто-вывод, плюс включаемые соглашения об именовании (`UseNamingConvention()`/`WithNamingConvention()`) |
| Регистр ключевых слов SQL | фиксированный (канонический для провайдера, верхний) | настраиваемый, по умолчанию нижний (`KeywordCase`) |
| Расширяемость | Интерсепторы, собственный SQL, расширения провайдеров | Контракт диалекта и атрибуты функций |

## Итог

Для чтения, отчётности и редких явных вставок по существующей схеме nextorm — более сильный выбор: он
покрывает практически всю аналитическую поверхность запросов, которую даёт linq2db — провайдерные
семейства функций, специфичные для ClickHouse конструкции, кросс-провайдерные row values, TVF и многое
другое — при меньшем объёме аллокаций, результатах бенчмарков на уровне или выше Dapper, EF Core и linq2db
на поставляемых сценариях и более настраиваемом выводе SQL (квотирование идентификаторов, соглашения об
именовании и регистр ключевых слов включаются явно и переопределяются для отдельной команды, тогда как
linq2db квотирует по умолчанию и фиксирует имена через схему отображения). linq2db остаётся лучшим выбором
только тогда, когда тот же слой должен ещё и выполнять полный CRUD (`UPDATE` и полный `MERGE`
с ветками), моделировать связи или генерировать слой доступа к данным из живой схемы — то, что nextorm осознанно
оставляет за рамками.

## См. также

- [Матрица возможностей: nextorm vs EF Core и linq2db](../../comparison/capability-matrix.md) — исчерпывающая матрица по конструкциям.
- [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core и linq2db, по конструкциям.
- [Ограничения и возможности вне области охвата](../../../advanced/limitations.md)
- [Соединения](../../../guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Хинты запросов](../../../guide/17-query-hints.md)
- [Обзор провайдеров](../../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
Возможности linq2db описаны по его публичной документации.
