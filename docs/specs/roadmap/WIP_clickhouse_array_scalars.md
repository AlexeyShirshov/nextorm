# WIP: ClickHouse скалярные array-функции (`startsWith`/`endsWith`/`position`/`length`)

> Рабочий план по скилу `implementing-todo-features`. Источник: `todo_clickhouse.md:85-86`.

## Пункт и цель

- Пункт бэклога отнесён к «Уровню 1. Малые правки», но по факту это часть массивы-воркстрима ClickHouse.
- Заявленный объём: явные методы-обёртки над массивами вместо `LIKE`/`lengthUTF8`.

## Факт по состоянию (шаг 1)

- Вся array-поверхность привязана к единому гейту `ISqlDialect.SupportsArrays`
  (`ISqlDialect.cs:117`, `SqlDialectBase.cs:28 => false`), включённому **только у PostgreSQL**.
- Связывание `T[]`-аргументов (`SqlOperandTranslator.cs:111`), `ArraySqlTranslator.cs:180` и
  string-array хук (`StringFunctionTranslator.cs:98`) бросают `NotSupportedException` вне PostgreSQL.
- У ClickHouse нет row reader для массивов, а binding массивов-параметров требует явного типа
  (`Array(Int64)`) — см. `todo_clickhouse.md:127-142` (Уровень 3, самый крупный workstream).

## Вывод

Заблокировано в nextorm, **хотя сам движок ClickHouse эти функции поддерживает нативно** — это не
пробел СУБД, а отсутствие в nextorm row-reader для массивов и binding массивов-параметров.
«Добавить обёртки» невозможно без:
1. разделения `SupportsArrays` на `SupportsArrayParameters` и `SupportsArrayFunctions`;
2. маппинга имён: `position` -> `indexOf` (1-based, `0` при отсутствии), `length` -> `length`
   (алиас `CARDINALITY`); префикс/суффикс массива — **не** `startsWith`/`endsWith` (они принимают
   только `String`/`FixedString`), а `hasSubstr(arr, sub)` (упорядоченный подмассив, v20.6+) либо
   `arraySlice(arr, ...)`;
3. row reader для массивов (для проекции результата).

Это Уровень 3 (`todo_clickhouse.md:122-142`), а не «мелочь». Часть строкового поведения уже покрыта
через `LIKE`/`lengthUTF8` и не требует изменений.

## Решение

Не реализуем в этом проходе. Пункт остаётся `[ ]`; при старте массивы-воркстрима ClickHouse включить
эти скаляры в общий маппинг. WIP закрыт как «заблокировано» (без изменения кода).

## Матрица «провайдер × форма» (обновлено 19.09.2026)

Шаг 1 скила: покрытие конструкцией по **всем** провайдерам (не «что уже связано в nextorm»),
заполнено по документации самих СУБД. Формы: length массива, position массива, префикс/суффикс
массива.

| Провайдер | length | position | prefix / suffix | Источник |
| --- | --- | --- | --- | --- |
| PostgreSQL | `cardinality(arr)`, `array_length(arr, 1)` | `array_position(arr, x)` (1-based, `NULL` при отсутствии) | `—`: нативной функции нет; выразимо срезом `arr[1:cardinality(sub)] = sub` | https://www.postgresql.org/docs/current/functions-array.html |
| SQL Server | `—`: нет array-типа | `—`: нет array-типа | `—`: нет array-типа | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| MySQL | `—`: нет array-типа (JSON только через `JSON_TABLE`/`JSON_EXTRACT`) | `—`: нет array-типа | `—`: нет array-типа | https://dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | `—`: нет array-типа (`JSON` — псевдоним `LONGTEXT`) | `—`: нет array-типа | `—`: нет array-типа | https://mariadb.com/kb/en/json-data-type/ |
| ClickHouse | `length(arr)` (алиас `CARDINALITY`) | `indexOf(arr, x)` (1-based, `0` при отсутствии) | `hasSubstr(arr, sub)` (упорядоченный подмассив, v20.6+) либо `arraySlice(arr, ...)`; `startsWith`/`endsWith` **не** подходят — только `String`/`FixedString` | https://clickhouse.com/docs/en/sql-reference/functions/array-functions , https://clickhouse.com/docs/en/sql-reference/functions/string-functions |
| SQLite | `json_array_length(json)` (только JSON-массивы) | `—`: для JSON-массивов функции нет | `—`: нет | https://www.sqlite.org/json1.html |
| InMemory | `Array.Length` (CLR) | `Array.IndexOf` (CLR) | `SequenceEqual`/`Take` (CLR) | — (nextorm: array-поверхность гейтится `SupportsArrays`, выделенных методов нет) |

**Решение:** движок ClickHouse поддерживает `length`/`indexOf`/`hasSubstr` нативно, поэтому блокирует
не СУБД, а отсутствие в nextorm row-reader для массивов и binding массивов-параметров с явным типом
(`Array(Int64)`); пункт остаётся `[ ]` до массивы-воркстрима ClickHouse (Уровень 3), а маппинг
`startsWith`/`endsWith` на массивы признан ошибочным.
