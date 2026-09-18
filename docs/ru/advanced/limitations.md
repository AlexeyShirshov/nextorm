# Ограничения и возможности вне области охвата

> nextorm — построитель запросов только для чтения: он генерирует инструкции `SELECT` и материализует их результаты, и намеренно оставляет отслеживание изменений, DML и вывод связей вызывающей стороне.

**Предварительные требования:** [Provider overview](../providers/overview.md) · [API reference](api-reference.md)

## Обзор

На этой странице перечислены возможности, которые **не** являются частью nextorm, с однострочной причиной для каждой. Она
выведена из [SQL capabilities gap analysis](../../sql-capabilities-gap-analysis.md); тот документ — источник истины
и также отслеживает, что *действительно* реализовано. Читайте их вместе: возможность, указанная там как
**Done**, не является ограничением, хотя более старые разделы анализа (разделы 1–4) всё ещё описывают
базовое состояние до реализации.

## Вне области охвата

| Не поддерживается | Обоснование |
|---|---|
| **DML** — `INSERT`, `UPDATE`, `DELETE`, `MERGE` | nextorm по замыслу является маппером только для чтения без отслеживания изменений; предполагается, что запись идёт через ваши собственные команды или другой инструмент. |
| **Навигационные свойства / метаданные связей** | Метаданных связей нет, и нет неявного вывода соединений; вы пишете соединения явно с помощью `Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`/`CrossApply`/`OuterApply` (см. [Соединения](../guide/03-joins.md)). |
| **Коррелированный источник `APPLY` / `LATERAL`** | `CrossApply`/`OuterApply` поддерживаются по таблице, производному запросу, необработанной таблице или табличной функции, но применяемый источник пока не может ссылаться на внешнюю строку — нет публичного API, позволяющего описать ссылку на внешнюю строку внутри подзапроса в `FROM`. Диалекты без lateral-источника (SQLite, ClickHouse) отклоняют `APPLY`; провайдер in-memory его не поддерживает. |
| **`SelectMany` / `GroupJoin` на SQL-провайдерах** | Эти LINQ-операторы реализованы только для провайдера in-memory, где корреляция — обычный делегат. На SQL-провайдере `SelectMany`/`GroupJoin` бросают `NotSupportedException`; явная поверхность соединений `CrossApply`/`OuterApply` покрывает некоррелированный эквивалент (см. [Соединения](../guide/03-joins.md)). |
| **Общая коррелированная скалярная проекция** | Коррелированные подзапросы поддерживаются только через `EXISTS` / `IN` / `ANY` / `ALL` (`NORM.SQL`); общий коррелированный скалярный подзапрос в проекции не покрывается. |
| **`INTERSECT ALL` / `EXCEPT ALL` в SQL Server и SQLite** | Ни один из этих движков не может выразить такие варианты, поэтому диалект отклоняет их с `NotSupportedException`; `INTERSECT` / `EXCEPT` (без `ALL`) и `UNION` / `UNION ALL` работают везде. PostgreSQL поддерживает оба варианта `ALL`. |
| **Хинты запросов вне SQL Server** | Хинты уровня инструкции рендерит только диалект SQL Server; SQLite, PostgreSQL, MySQL/MariaDB и ClickHouse отклоняют команду с хинтами через `NotSupportedException` (см. [Хинты запросов](../guide/17-query-hints.md)). Табличные хинты, например `WITH (NOLOCK)`, в API отсутствуют. |
| **Raw SQL как композируемый источник или подзапрос** | Raw SQL поддерживается для запроса целиком (`WithSql` / `PrepareFromSql`), но его нельзя использовать как композируемый фрагмент `FROM`/подзапроса. |
| **Функции массивов вне PostgreSQL** | Квантификаторы `any`/`all` над параметром-массивом (`column = any(@array)`) и функции массивов (`cardinality`, `array_length`, ...) требуют провайдера с нативными массивами; его включает только PostgreSQL (`SupportsArrays`). Остальные диалекты отклоняют их через `NotSupportedException` (см. [Скалярные функции](../guide/11-scalar-functions.md#массивы-postgresql)). |
| **Нативные JSON-функции вне PostgreSQL** | Функции и операторы `json`/`jsonb` (`json_agg`, `json_build_object`, `->`, `->>`, `#>`, `@>`, `?`, ...) требуют провайдера с типом JSON; его включает только PostgreSQL (`SupportsJson`). В SQL Server вместо этого есть текстовое подмножество JSON (`json_value`/`json_query`/`json_modify`, `SupportsTextJson`) над обычной текстовой колонкой. Остальные диалекты отклоняют обе поверхности через `NotSupportedException` (см. [Скалярные функции](../guide/11-scalar-functions.md#json-и-jsonb-postgresql)). |
| **`greatest`/`least` вне PostgreSQL, MySQL/MariaDB, ClickHouse и SQL Server** | Функции включаются флагом `SupportsGreatestLeast`; PostgreSQL, MySQL/MariaDB, ClickHouse и SQL Server 2022+ его включают, а SQLite (у которого скалярные `max`/`min` иначе распространяют NULL) отклоняет вызовы через `NotSupportedException`. `nullif` — ANSI и доступен всегда. |
| **`date_trunc` вне PostgreSQL, SQL Server и ClickHouse** | `date_trunc` включается флагом `SupportsDateTrunc`; его включают PostgreSQL, SQL Server 2022+ (`datetrunc`) и ClickHouse (`dateTrunc`). |
| **`array_agg` вне PostgreSQL** | `array_agg` включается флагом `SupportsArrayAgg` и требует типа-массива на стороне провайдера, поэтому его включает только PostgreSQL. `string_agg` включается отдельным флагом `SupportsStringAgg` и доступен также в SQL Server 2017+ и ClickHouse (`arrayStringConcat(groupArray(x), delimiter)`). Учтите, что результат `array_agg` — колонка-массив, которую построитель строк пока не умеет материализовать, поэтому используйте его внутри запроса (например, в `HAVING`). |
| **Логические и регрессионные агрегаты вне PostgreSQL** | `bool_and`/`bool_or`/`every` (`SupportsBooleanAggregates`) и семейство `regr_*` (`SupportsRegressionAggregates`) доступны только в PostgreSQL. `corr`/`covar_*` (`SupportsStatisticalAggregates`) доступны также в ClickHouse (`corr`/`covarPop`/`covarSamp`). |
| **`FILTER` у агрегатов вне PostgreSQL и SQLite** | Предложение `FILTER (WHERE ...)` включается флагом `SupportsFilter`; его включают PostgreSQL и SQLite, а MySQL/MariaDB и SQL Server — нет. ClickHouse тоже его отклоняет, но предоставляет аналог фильтрованного агрегата — комбинаторы `-If` (`count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`, `SupportsIfAggregates`). |
| **`GROUP BY CUBE` в MySQL/MariaDB и в провайдере in-memory** | В MySQL/MariaDB нет `CUBE`, а провайдер in-memory не поддерживает ни `ROLLUP`, ни `CUBE`; они отклоняют модификатор через `NotSupportedException`. `ROLLUP` работает на всех SQL-провайдерах (см. [Группировка и агрегаты](../guide/04-grouping-and-aggregates.md#rollup-и-cube)). |

## Не ограничения

Следующее полностью реализовано и проверено; оно появляется в столбце *готово* анализа пробелов,
а не в списке выше:

- соединения любого типа (`INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`, а также `CROSS APPLY`/`OUTER APPLY` и lateral-эквивалент там, где поддерживается) и арности 2–8;
- `CASE WHEN` / тернарный оператор / `switch`, `COALESCE`, числовой `CAST`;
- строковые, математические и date/time скалярные функции и `LIKE`;
- арифметика дат (`date_add`/`end_of_month` и `DateTime.Add*`) в PostgreSQL, SQL Server и ClickHouse (`addDays`/.../`toLastDayOfMonth`), а также `date_trunc` и `string_agg` в ClickHouse;
- битовые (`bit_and`/`bit_or`/`bit_xor`), статистические (`corr`/`covar_*`), `argMin`/`argMax` и `-If` (`count_if`/...) агрегаты в ClickHouse;
- бинарные столбцы — свойство или проекция `byte[]` отображается на `bytea` (PostgreSQL), `varbinary`/`image` (SQL Server) или `blob` (SQLite);
- `IN` по списку/массиву, логические `!` и унарные операторы;
- `SELECT DISTINCT`, `INTERSECT`/`EXCEPT`, CTE (включая рекурсивные) и оконные функции;
- пользовательские скалярные функции (`[SqlFunction]`) и табличные функции (`[SqlTableFunction]`);
- хинты уровня инструкции в SQL Server (`Hint(...)`);
- raw SQL для запроса целиком и пути переиспользования неявного кэша планов / `Prepare()`.

Обратитесь к [SQL capabilities gap analysis](../../sql-capabilities-gap-analysis.md) за таблицей статуса и
тестовыми подтверждениями по каждому пункту.

## См. также

- [SQL capabilities gap analysis](../../sql-capabilities-gap-analysis.md)
- [Хинты запросов](../guide/17-query-hints.md)
- [Provider overview](../providers/overview.md)
- [API reference](api-reference.md)

---

Source: `docs/sql-capabilities-gap-analysis.md` (sections 1–4), implementation status table at the top of
that document.
