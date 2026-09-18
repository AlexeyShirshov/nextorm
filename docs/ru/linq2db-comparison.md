# nextorm vs linq2db: сравнение функционала

> Сравнение функционала nextorm и linq2db. Дополняет [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md)
> (там же рассматривается EF Core) и основано на текущем дереве `1.0.3-alpha`, включая добавленные
> `APPLY`/`LATERAL` и хинты запросов.

**Предварительные требования:** [Обзор провайдеров](providers/overview.md) · [Ограничения](advanced/limitations.md) · [Хинты запросов](guide/17-query-hints.md)

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
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | yes | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin` |
| `APPLY` / `LATERAL` | yes | **partial** — только некоррелированные | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.MakeApply` |
| Арность соединений | не ограничена | 2–8 (ограничение на этапе компиляции) | `Projection<T1..T8>`, `EntityP2..P8` |
| Подзапросы (`FROM`, скалярные, коррелированные `EXISTS/IN/ANY/ALL`) | yes | yes (общая коррелированная скалярная — нет) | `CorrelatedQueryExpressionVisitor.cs`, `NORM.SQL` |
| `IN` по списку/массиву | yes | yes | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs` |
| `GROUP BY` / `HAVING` / агрегаты | yes | yes | `EntityBuilder.GroupBy/Having`, `BaseExpressionVisitor.cs` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, диалектные `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL`, где поддерживает движок) | yes | yes (зависит от провайдера) | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| CTE (включая рекурсивные) | yes | yes | `Builders/CteQuery.cs`, `DataContext.IDataContextExtensions.With/WithRecursive` |
| Оконные функции (`OVER`, ranking, framed aggregates, `lag`/`lead`) | yes | yes | `Visitors/WindowFunctionTranslator.cs`, `NORM.SQL` |
| `CASE WHEN` / тернарный / `switch`, `COALESCE`, числовой `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| Строковые / математические / date скалярные функции, `LIKE` | yes | yes | `Visitors/ScalarFunctionTranslator.cs`, диалектные `Make*` |
| Пользовательские скалярные функции | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Табличные функции | yes (`TableFunction`) | yes (`[SqlTableFunction]`) | `SqlTableFunctionAttribute.cs`, `DataContextExtensions.FromTableFunction` |
| Сырой SQL (запрос целиком) | yes | yes | `WithSql` / `PrepareFromSql` |
| Сырой SQL как композируемый источник/подзапрос | yes | **no** | — |
| Хинты запросов | yes (зависит от провайдера) | **partial** — только SQL Server `OPTION (...)` | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Табличные хинты (например `WITH (NOLOCK)`) | yes | **no** | — |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **no** (только чтение по замыслу) | — |
| Bulk copy / merge / временные таблицы | yes | **no** | — |
| Навигационные свойства / связи / eager loading | yes (`[Association]`, `LoadWith`) | **no** | — |
| Отслеживание изменений / identity map | partial | **no** (по замыслу) | — |
| Расширяемость (интерсепторы, собственный SQL, фильтры) | обширная | минимальная (диалект + `[SqlFunction]`/`[SqlTableFunction]`) | `SqlDialectBase` |
| Провайдеры | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | `src/nextorm.*` |
| Интеграция с EF Core | yes (`linq2db.EntityFrameworkCore`) | no | — |
| Производительность | высокая | по бенчмаркам на уровне/выше Dapper и EF Core на поставляемых сценариях | `benchmark-report.md` |

## Что nextorm делает хорошо

* Полная аналитическая поверхность запросов: все типы соединений, включая `APPLY`/`LATERAL`
  (некоррелированные), операции над множествами, `DISTINCT`, CTE (рекурсивные), оконные функции,
  `CASE`/`COALESCE`/`CAST`, строковые/математические/date функции, `IN`-списки, маппинг UDF/TVF и сырой
  SQL для запроса целиком.
* Переносимость между провайдерами: один и тот же C# рендерит `CROSS APPLY` в SQL Server и
  `CROSS JOIN LATERAL` в PostgreSQL/MySQL/MariaDB — через возможности `ISqlDialect`.
* Два пути переиспользования (неявный кэш планов и явный `Prepare()`), параметризация запросов и
  малоаллоцирующий дизайн, подтверждённый бенчмарками.
* Хинты уровня инструкции в SQL Server с участием в ключе плана (`Hint(...)`), корректно сливающиеся с
  предложением CTE `option (maxrecursion n)`.

## Где linq2db сильнее

* **Изменение данных**: `INSERT`/`UPDATE`/`DELETE`/`MERGE`, bulk copy, временные таблицы — полностью
  отсутствуют в nextorm по замыслу.
* **Связи**: `[Association]`, eager loading `LoadWith` и неявный вывод соединений.
* **Корреляция**: коррелированные скалярные проекции и коррелированные источники `APPLY`/`LATERAL`;
  nextorm выражает корреляцию только через `EXISTS`/`IN`/`ANY`/`ALL` и не позволяет применяемому
  источнику ссылаться на внешнюю строку.
* **Хинты запросов/таблиц** у разных провайдеров, а также фильтры запросов, интерсепторы и прочая
  расширяемость.
* **Широта провайдеров**: Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE и другие.
* **Интеграция с EF Core** и более крупная экосистема.

## Архитектурные различия

| Аспект | linq2db | nextorm |
|---|---|---|
| Модель | Явный CRUD-ORM со связями; без автоматического change tracking | Построитель запросов и маппер только для чтения |
| Требование к сущности | Маппинг через атрибуты/fluent/вывод | Класс сущности необязателен; `From("table")` с `TableAlias` |
| Переиспользование | Compiled queries, кэш запросов | Неявный кэш планов и `Prepare()` |
| Расширяемость | Интерсепторы, собственный SQL, расширения провайдеров | Контракт диалекта и атрибуты функций |

## Итог

Если задача — *чтение и отчётность по существующей схеме* с помощью компактного, быстрого и переносимого
между провайдерами маппера, nextorm теперь покрывает практически всю аналитическую поверхность запросов,
которую даёт linq2db. Оставшаяся функциональная дельта намеренна: DML, связи, коррелированные
lateral-источники, табличные хинты, более широкая матрица провайдеров и крупная поверхность
расширяемости/экосистемы. И наоборот, linq2db лучше подходит, когда тот же слой должен ещё и писать
данные и моделировать связи.

## См. также

- [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md) — nextorm vs EF Core и linq2db, по конструкциям.
- [Ограничения и возможности вне области охвата](advanced/limitations.md)
- [Соединения](guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Хинты запросов](guide/17-query-hints.md)
- [Обзор провайдеров](providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/sql-capabilities-gap-analysis.md`,
`benchmark-report.md`. Возможности linq2db описаны по его публичной документации.
