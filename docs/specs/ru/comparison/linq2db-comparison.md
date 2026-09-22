# nextorm vs linq2db: сравнение функционала

> Сравнение функционала nextorm и linq2db. Дополняет [SQL capabilities gap analysis](../../roadmap/sql-capabilities-gap-analysis.md)
> и [матрицу возможностей](../../comparison/capability-matrix.md) (там же рассматривается EF Core) и основано на текущем
> дереве `1.0.3-alpha`, включая добавленные типы соединений, `APPLY`/`LATERAL`, паритет функций (full-text,
> JSON, массивы, `ROLLUP`/`CUBE`/`GROUPING SETS`, арифметика дат), временные таблицы, блокировки строк
> и хинты запросов/таблиц.

**Предварительные требования:** [Обзор провайдеров](../../../providers/overview.md) · [Ограничения](../../../advanced/limitations.md) · [Хинты запросов](../../../guide/17-query-hints.md)

## Позиционирование

* **linq2db** — зрелый полнофункциональный LINQ-to-SQL ORM: широкая матрица провайдеров, DML
  (insert/update/delete/merge), связи/eager loading, bulk copy, поддержка временных таблиц, хинты
  запросов и таблиц, расширяемость (интерсепторы, собственный SQL-маппинг) и пакет интеграции с EF Core.
  Он занимает нишу между micro-ORM и полноценным ORM.
* **nextorm** — построитель SQL и маппер только для чтения, без отслеживания изменений. Он намеренно
  исключает DML, change tracking и метаданные связей и делает упор на малый объём аллокаций,
  параметризацию, компиляцию запросов (кэш планов / `Prepare()`) и переносимую между провайдерами
  генерацию SQL.

Библиотеки пересекаются на *поверхности запросов* и расходятся в *изменении данных* и *моделировании связей*.

## Матрица возможностей

Обозначения: **yes** — полноценная поддержка; **partial** — поддержка с ограничениями; **no** — нет.
Для nextorm указан исходный файл, отвечающий за поведение.

| Область | linq2db | nextorm | Обоснование в nextorm |
|---|---|---|---|
| Проекция (`SELECT`, DTO/анонимные/record/tuple/scalar) | yes | yes | `EntityBuilder.Select` |
| Предикаты (`WHERE`: сравнения, `and`/`or`/`!`, арифметика, битовые/сдвиги) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | **yes** (`FULL JOIN` нет в MySQL/MariaDB) | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes | **yes** (включая коррелированные источники; отключено в SQLite/ClickHouse) | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Строгость соединений (`ANY`/`ALL`/`ASOF`) и `GLOBAL` | no | **yes** в ClickHouse (`SEMI`/`ANTI`/`PASTE` не реализованы) | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Арность соединений | не ограничена | 2–8 (ограничение на этапе компиляции) | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Соединение с производной таблицей (подзапросом) | yes | **yes** — с любой стороны: присоединяемая (`Join(QueryCommand<T>)`) или основной источник `FROM` | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Подзапросы (`FROM`, скалярные, коррелированные `EXISTS/IN/ANY/ALL`) | yes | yes — корреляция на любой глубине у SQL-провайдеров; in-memory выбрасывает `NotSupportedException` | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference` |
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
| CTE (включая рекурсивные) | yes | yes | `Builders/CteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive` |
| Оконные функции (`OVER`, ranking, framed aggregates, `lag`/`lead`) | yes | yes — плюс именованные окна, единица кадра `GROUPS`, `EXCLUDE`, `percent_rank`/`cume_dist`, `nth_value` и `lagInFrame`/`leadInFrame` в ClickHouse | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion` |
| `CASE WHEN` / тернарный / `switch`, `COALESCE`, числовой `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| Строковые / математические / date скалярные функции, `LIKE` | yes | yes | `Visitors/ScalarFunctionTranslator.cs`, диалектные `Make*` |
| Арифметика дат (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes для всех провайдеров (допустимые поля различаются и валидируются по провайдеру) | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Полнотекстовый поиск | yes (провайдер) | **yes** на SQL Server, PostgreSQL и MySQL/MariaDB (булевы предикаты; без ранжирования) | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Нативные JSON-документы | yes | **yes на PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON скалярные функции (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | **yes на SQL Server и MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| Строковый JSON + функции словарей (ClickHouse) | no | **yes на ClickHouse** | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Массивы (`cardinality`/`array_*`/`@>`/`&&`, `Array(T)` в ClickHouse, `ARRAY JOIN`) | no | **yes на PostgreSQL и ClickHouse** (`ARRAY JOIN`; функции высшего порядка не реализованы) | `SupportsArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `IArrayJoinRenderer.Render` |
| Условные функции (`iif`/`choose`/`multi_if`) | no | **yes** (переносимый `iif`; `choose` только SQL Server; `multi_if` ClickHouse) | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes (провайдер) | **yes на SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| Методы типа XML (`.value`/`.query`/`.exist`/`.nodes`) | yes (провайдер) | **partial** — только SQL Server | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | partial | **yes** (обработка NULL зависит от провайдера) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | **yes** — `string_agg` кросс-провайдерно; `array_agg` на PostgreSQL | `SupportsStringAgg`/`SupportsArrayAgg` |
| Пользовательские скалярные функции | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Табличные функции | yes (`TableFunction`) | yes (`[SqlTableFunction]`); встроенные gated, предобъявленный набор (`generate_series`/`unnest`/…, `string_split`/`openjson`, ClickHouse `numbers`/`zeros`/`generateRandom`) меньше | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Нативный источник `PIVOT` / `UNPIVOT` | no (сырой SQL) | **yes на SQL Server** | `EntityBuilder.Pivot`/`Unpivot` |
| Сырой SQL (запрос целиком) | yes | yes | `WithSql` / `PrepareFromSql` |
| Сырой SQL как композируемый источник/подзапрос | yes | **yes** — `FromSql` рендерит фрагмент как производную таблицу, можно соединять/фильтровать дальше | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Хинты запросов | yes (зависит от провайдера) | **partial** — только SQL Server `OPTION (...)` | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Табличные хинты (например `WITH (NOLOCK)`) | yes | **partial** — только SQL Server | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Квотирование идентификаторов | yes (по провайдеру) | включается явно — `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`; по умолчанию физические имена выводятся как есть | `ISqlDialect.QuoteIdentifier` |
| Соглашения об именовании (например snake_case) | через `MappingSchema`/атрибуты (встроенной конвенции нет) | включается явно — `UseNamingConvention()`/`WithNamingConvention()`; встроенный `SnakeCaseNamingConvention`; явные имена — дословно | `INamingConvention` / `SnakeCaseNamingConvention` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **no** (только чтение по замыслу) | — |
| Bulk copy / merge / временные таблицы | yes | **no** | — |
| Навигационные свойства / связи / eager loading | yes (`[Association]`, `LoadWith`) | **no** | — |
| Отслеживание изменений / identity map | partial | **no** (по замыслу) | — |
| Расширяемость (интерсепторы, собственный SQL, фильтры) | обширная | минимальная (диалект + `[SqlFunction]`/`[SqlTableFunction]`) | `SqlDialectBase` |
| Провайдеры | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | `src/nextorm.*` |
| Интеграция с EF Core | yes (`linq2db.EntityFrameworkCore`) | no | — |
| Производительность | высокая | по бенчмаркам на уровне/выше Dapper, EF Core и linq2db на поставляемых сценариях | `docs/specs/performance/benchmark-report.md` |

## Что nextorm делает хорошо

* Полная аналитическая поверхность запросов: все типы соединений, включая `APPLY`/`LATERAL`
  (включая коррелированные источники), соединения с производными таблицами, строгость соединений/`GLOBAL`
  в ClickHouse, операции над множествами, `DISTINCT` (плюс `DISTINCT ON`/`WITH TIES`), CTE (рекурсивные),
  оконные функции (именованные окна, `GROUPS`, `EXCLUDE`, `nth_value`, `percent_rank`/`cume_dist`),
  `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`, `CASE`/`COALESCE`/`CAST`, строковые/математические/date
  функции, `IN`-списки, маппинг UDF/TVF, нативные `PIVOT`/`UNPIVOT`, временные таблицы, блокировки строк и
  сырой SQL для запроса целиком.
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
* Настраиваемый вывод маппинга: квотирование идентификаторов и соглашения об именовании включаются
  явно и переопределяются для отдельной команды (`UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`,
  `UseNamingConvention()`/`WithNamingConvention()`), поэтому генерируемый SQL по умолчанию предсказуем,
  а схемы с зарезервированными словами/смешанным регистром и snake_case-каталоги не требуют рукописных имён.

## Где linq2db сильнее

* **Изменение данных**: `INSERT`/`UPDATE`/`DELETE`/`MERGE`, bulk copy, временные таблицы — полностью
  отсутствуют в nextorm по замыслу.
* **Связи**: `[Association]`, eager loading `LoadWith` и неявный вывод соединений.
* **Широта хинтов**: хинты запросов и таблиц у разных провайдеров (в nextorm оба есть только в SQL Server),
  а также фильтры запросов, интерсепторы и прочая расширяемость.
* **Покрытие за пределами ядра запросов**: более крупный предобъявленный набор TVF (хотя nextorm уже
  поставляет `CONTAINSTABLE`/`FREETEXTTABLE` с `KEY`/`RANK`, PostgreSQL `ts_rank`/`ts_rank_cd` и rowset
  XML `.nodes` SQL Server через `xml_nodes`), а также источники с динамической схемой (ClickHouse
  `values()`/серверные табличные функции, PostgreSQL `jsonb_to_record`).
* **Широта провайдеров**: Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE и другие.
* **Интеграция с EF Core** и более крупная экосистема.

Провайдер in-memory остаётся единственным местом, где корреляция ограничена: nextorm поддерживает
коррелированные скалярные подзапросы, коррелированные `EXISTS`/`IN`/`ANY`/`ALL` и коррелированные
источники `APPLY`/`LATERAL` на любой глубине вложенности у SQL-провайдеров, тогда как у провайдера
in-memory нет построчной привязки внешней строки (см.
[`sql-capabilities-gap-analysis.md`](../../roadmap/sql-capabilities-gap-analysis.md) §4).

## Архитектурные различия

| Аспект | linq2db | nextorm |
|---|---|---|
| Модель | Явный CRUD-ORM со связями; без автоматического change tracking | Построитель запросов и маппер только для чтения |
| Требование к сущности | Маппинг через атрибуты/fluent/вывод | Класс сущности необязателен; `From("table")` с `TableAlias` |
| Переиспользование | Compiled queries, кэш запросов | Неявный кэш планов и `Prepare()` |
| Квотирование идентификаторов | включено по умолчанию (по провайдеру) | выключено по умолчанию; включается через `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` |
| Маппинг имён | fluent/атрибуты (`MappingSchema`) | атрибуты/fluent/авто-вывод, плюс включаемые соглашения об именовании (`UseNamingConvention()`/`WithNamingConvention()`) |
| Расширяемость | Интерсепторы, собственный SQL, расширения провайдеров | Контракт диалекта и атрибуты функций |

## Итог

Если задача — *чтение и отчётность по существующей схеме* с помощью компактного, быстрого и переносимого
между провайдерами маппера, nextorm теперь покрывает практически всю аналитическую поверхность запросов,
которую даёт linq2db, включая провайдерные семейства функций и конструкции, специфичные для ClickHouse.
Оставшаяся функциональная дельта намеренна: DML и change tracking, связи, широта хинтов между
провайдерами, композируемый сырой SQL, более крупный предобъявленный набор TVF, более широкая матрица провайдеров и крупная поверхность расширяемости/экосистемы.
Вывод маппинга, напротив, в nextorm настраивается гибче: квотирование идентификаторов и соглашения об
именовании включаются явно и переопределяются для отдельной команды, тогда как linq2db квотирует по
умолчанию и фиксирует имена через схему отображения. И наоборот, linq2db лучше подходит, когда тот же
слой должен ещё и писать данные и моделировать связи.

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
