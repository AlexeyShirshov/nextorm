# Ограничения и возможности вне области охвата

> nextorm — построитель запросов только для чтения: он генерирует инструкции `SELECT` и материализует их результаты, и намеренно оставляет отслеживание изменений, DML и вывод связей вызывающей стороне.

**Предварительные требования:** [Provider overview](../providers/overview.md) · [API reference](api-reference.md)

## Обзор

На этой странице перечислены возможности, которые **не** являются частью nextorm, с однострочной причиной для каждой. Она
выведена из [SQL capabilities gap analysis](../../specs/roadmap/sql-capabilities-gap-analysis.md); тот документ — источник истины
и также отслеживает, что *действительно* реализовано. Читайте их вместе: возможность, указанная там как
**Done**, не является ограничением, хотя более старые разделы анализа (разделы 1–4) всё ещё описывают
базовое состояние до реализации.

## Вне области охвата

| Не поддерживается | Обоснование |
|---|---|
| **DML** — `INSERT`, `UPDATE`, `DELETE`, `MERGE` | nextorm по замыслу является маппером только для чтения без отслеживания изменений; предполагается, что запись идёт через ваши собственные команды или другой инструмент. |
| **Навигационные свойства / метаданные связей** | Метаданных связей нет, и нет неявного вывода соединений; вы пишете соединения явно с помощью [`Join`](xref:NextORM.Core.EntityBuilder`1)/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1)/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1)/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1)/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1)/[`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) (см. [Соединения](../guide/03-joins.md)). |
| **Коррелированный источник `APPLY` / `LATERAL`** | [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) поддерживаются по таблице, производному запросу, необработанной таблице или табличной функции, но применяемый источник пока не может ссылаться на внешнюю строку — нет публичного API, позволяющего описать ссылку на внешнюю строку внутри подзапроса в `FROM`. Диалекты без lateral-источника (SQLite, ClickHouse) отклоняют `APPLY`; провайдер in-memory его не поддерживает. |
| **[`SelectMany`](xref:NextORM.Core.EntityBuilder`1) / [`GroupJoin`](xref:NextORM.Core.EntityBuilder`1) на SQL-провайдерах** | Эти LINQ-операторы реализованы только для провайдера in-memory, где корреляция — обычный делегат. На SQL-провайдере [`SelectMany`](xref:NextORM.Core.EntityBuilder`1)/[`GroupJoin`](xref:NextORM.Core.EntityBuilder`1) бросают `NotSupportedException`; явная поверхность соединений [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) покрывает некоррелированный эквивалент (см. [Соединения](../guide/03-joins.md)). |
| **Коррелированные подзапросы глубиной больше одного уровня и коррелированные подзапросы в провайдере in-memory** | Один уровень корреляции работает для скалярных подзапросов и `EXISTS`/`IN`/`ANY`/`ALL` в `SELECT`, `WHERE` и `ORDER BY` у каждого SQL-провайдера (см. [Подзапросы](../guide/06-subqueries.md#коррелированный-скалярный-подзапрос)). Подзапрос, вложенный в другой коррелированный подзапрос, бросает `NotSupportedException`, чтобы не привязать внешний маркер к чужому запросу; у провайдера in-memory нет построчной привязки внешней строки, и он тоже бросает `NotSupportedException`. |
| **Коррелированный скаляр: терминалы `First`/`Single` и их `*OrDefault`-формы** | При nullable-проекции отсутствие строки даёт `null`, при non-nullable value-проекции терминалы `FirstOrDefault`/`SingleOrDefault` дают `default` (например `0`). `First`/`Single` на non-nullable value-проекции бросают исключение, если строки нет. `Single`/`SingleOrDefault` при более чем одной строке бросают у всех провайдеров, чьи скалярные подзапросы проверяют кардинальность (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse); SQLite её не проверяет, поэтому там эти терминалы отклоняются с `NotSupportedException`, а не молча возвращают первую строку. |
| **Агрегатные терминалы как проекция подзапроса** | `EntityBuilder<T>.Count()`/`Sum(...)`/... выполняются немедленно, поэтому их нельзя встроить как проекцию SQL-подзапроса; используйте соответствующий агрегат `` Встраивание такого терминала бросает `NotSupportedException`. |
| **`INTERSECT ALL` / `EXCEPT ALL` в SQL Server и SQLite** | Ни один из этих движков не может выразить такие варианты, поэтому диалект отклоняет их с `NotSupportedException`; `INTERSECT` / `EXCEPT` (без `ALL`) и `UNION` / `UNION ALL` работают везде. PostgreSQL поддерживает оба варианта `ALL`. |
| **Хинты запросов вне SQL Server** | Хинты уровня инструкции рендерит только диалект SQL Server; SQLite, PostgreSQL, MySQL/MariaDB и ClickHouse отклоняют команду с хинтами через `NotSupportedException` (см. [Хинты запросов](../guide/17-query-hints.md)). Табличные хинты (`WithTableHint("nolock")` → `WITH (NOLOCK)`) поддерживаются только в SQL Server. |
| **Raw SQL как композируемый источник или подзапрос** | Raw SQL поддерживается для запроса целиком ([`WithSql`](xref:NextORM.Core.EntityBuilder`1) / [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1)), но его нельзя использовать как композируемый фрагмент `FROM`/подзапроса. |
| **Функции массивов вне PostgreSQL** | Квантификаторы `any`/`all` над параметром-массивом (`column = any(@array)`) и функции массивов (`cardinality`, `array_length`, ...) требуют провайдера с нативными массивами; его включает только PostgreSQL ([`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays)). Остальные диалекты отклоняют их через `NotSupportedException` (см. [Скалярные функции](../guide/11-scalar-functions.md#массивы-postgresql)). |
| **Нативные JSON-функции вне PostgreSQL** | Функции и операторы `json`/`jsonb` (`json_agg`, `json_build_object`, `->`, `->>`, `#>`, `@>`, `?`, ...) требуют провайдера с типом JSON; его включает только PostgreSQL ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson)). В SQL Server, MySQL и MariaDB вместо этого есть текстовое подмножество JSON (`json_value`/`json_query`/`json_modify`/`isjson`, [`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) над обычной текстовой колонкой (MySQL/MariaDB рендерят его через `JSON_EXTRACT`/`JSON_SET`). Остальные диалекты отклоняют обе поверхности через `NotSupportedException` (см. [Скалярные функции](../guide/11-scalar-functions.md#json-и-jsonb-postgresql)). |
| **`greatest`/`least` у провайдеров без поддержки** | Функции включаются флагом [`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast); PostgreSQL, MySQL/MariaDB, ClickHouse, SQL Server 2022+ и SQLite его включают. SQLite рендерит многоаргументные скалярные `max`/`min` (один аргумент возвращается как есть, так как `max`/`min` с одним аргументом — агрегат). Семантика NULL различается: PostgreSQL, SQL Server 2022+ и ClickHouse 24.12+ игнорируют NULL-аргументы, а MySQL/MariaDB и SQLite возвращают NULL, если любой аргумент NULL. Провайдер, не включивший флаг, отклоняет вызовы через `NotSupportedException`. `nullif` — ANSI и доступен всегда. |
| **`date_trunc` вне PostgreSQL, SQL Server и ClickHouse** | `date_trunc` включается флагом [`SupportsDateTrunc`](xref:NextORM.Core.ISqlDialect.SupportsDateTrunc); его включают PostgreSQL, SQL Server 2022+ (`datetrunc`) и ClickHouse (`dateTrunc`). |
| **`array_agg` вне PostgreSQL** | `array_agg` включается флагом [`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg) и требует типа-массива на стороне провайдера, поэтому его включает только PostgreSQL. `string_agg` включается отдельным флагом [`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg) и доступен также в SQL Server 2017+ и ClickHouse (`arrayStringConcat(groupArray(x), delimiter)`). Учтите, что результат `array_agg` — колонка-массив, которую построитель строк пока не умеет материализовать, поэтому используйте его внутри запроса (например, в `HAVING`). |
| **Логические и регрессионные агрегаты вне PostgreSQL** | `bool_and`/`bool_or`/`every` ([`SupportsBooleanAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBooleanAggregates)) и семейство `regr_*` ([`SupportsRegressionAggregates`](xref:NextORM.Core.ISqlDialect.SupportsRegressionAggregates)) доступны только в PostgreSQL. `corr`/`covar_*` ([`SupportsStatisticalAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStatisticalAggregates)) доступны также в ClickHouse (`corr`/`covarPop`/`covarSamp`). |
| **`FILTER` у агрегатов вне PostgreSQL и SQLite** | Предложение `FILTER (WHERE ...)` включается флагом [`SupportsFilter`](xref:NextORM.Core.ISqlDialect.SupportsFilter); его включают PostgreSQL и SQLite, а MySQL/MariaDB и SQL Server — нет. ClickHouse тоже его отклоняет, но предоставляет аналог фильтрованного агрегата — комбинаторы `-If` (`count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`, [`SupportsIfAggregates`](xref:NextORM.Core.ISqlDialect.SupportsIfAggregates)). |
| **`GROUP BY CUBE`/`GROUPING SETS` в MySQL/MariaDB и в провайдере in-memory** | В MySQL/MariaDB нет `CUBE` и `GROUPING SETS`, а провайдер in-memory не поддерживает ни `ROLLUP`, ни `CUBE`, ни `GROUPING SETS`; они отклоняют модификатор через `NotSupportedException`. `ROLLUP` работает на всех SQL-провайдерах (см. [Группировка и агрегаты](../guide/04-grouping-and-aggregates.md#rollup-и-cube)). |

## Не ограничения

Следующее полностью реализовано и проверено; оно появляется в столбце *готово* анализа пробелов,
а не в списке выше:

- соединения любого типа (`INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`, а также `CROSS APPLY`/`OUTER APPLY` и lateral-эквивалент там, где поддерживается) и арности 2–8;
- коррелированные скалярные подзапросы и коррелированные `EXISTS`/`IN`/`ANY`/`ALL` в `SELECT`, `WHERE` и `ORDER BY` у каждого SQL-провайдера (один уровень; см. [Подзапросы](../guide/06-subqueries.md));
- `CASE WHEN` / тернарный оператор / `switch`, `COALESCE`, числовой `CAST`;
- строковые, математические и date/time скалярные функции и `LIKE`;
- арифметика дат (`date_add`/`end_of_month`/`date_diff`/`date_from_parts` и `DateTime.Add*`) в PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB и SQLite, а также `date_trunc` в PostgreSQL/SQL Server/ClickHouse и `string_agg` в PostgreSQL/SQL Server/ClickHouse/MySQL/MariaDB/SQLite;
- битовые (`bit_and`/`bit_or`/`bit_xor`), статистические (`corr`/`covar_*`), `argMin`/`argMax` и `-If` (`count_if`/...) агрегаты в ClickHouse;
- бинарные столбцы — свойство или проекция `byte[]` отображается на `bytea` (PostgreSQL), `varbinary`/`image` (SQL Server) или `blob` (SQLite);
- `IN` по списку/массиву, логические `!` и унарные операторы;
- `SELECT DISTINCT`, `INTERSECT`/`EXCEPT`, CTE (включая рекурсивные) и оконные функции;
- пользовательские скалярные функции (`[SqlFunction]`) и табличные функции (`[SqlTableFunction]`);
- предикаты полнотекстового поиска (`SqlFunctions.Sql.contains`/`freetext`) в SQL Server, PostgreSQL и MySQL/MariaDB, текстовые JSON-функции (`json_value`/`json_query`/`json_modify`/`isjson`) в SQL Server и MySQL/MariaDB и табличные функции SQL Server `openjson`/`string_split`;
- хинты уровня инструкции и табличные хинты в SQL Server (`Hint(...)`, `WithTableHint(...)`) и JSON-вывод (`ForJson(...)` → `FOR JSON PATH`/`AUTO`) и XML-вывод (`ForXml(...)` → `FOR XML`);
- raw SQL для запроса целиком и пути переиспользования неявного кэша планов / [`Prepare`](xref:NextORM.Core.EntityBuilder`1).

Обратитесь к [SQL capabilities gap analysis](../../specs/roadmap/sql-capabilities-gap-analysis.md) за таблицей статуса и
тестовыми подтверждениями по каждому пункту.

## См. также

- [SQL capabilities gap analysis](../../specs/roadmap/sql-capabilities-gap-analysis.md)
- [Хинты запросов](../guide/17-query-hints.md)
- [Provider overview](../providers/overview.md)
- [API reference](api-reference.md)

---

Source: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` (sections 1–4), implementation status table at the top of
that document.
