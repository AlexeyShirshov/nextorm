# todo_phase2 — незаблокированный бэклог (Phase 2)

> Свод остатка `todo_postgres.md` / `todo_mssql.md` / `todo_clickhouse.md` после Phase 1
> (управляющий триаж — `WIP_provider_gap_triage.md`; завершённые WIP-отчёты удалены, закрытые пункты
> помечены `[x]`). Здесь только пункты, которые **не заблокированы**: их можно брать в работу скиллом
> `implementing-todo-features`. Заблокированное и вынесенное за рамки проекта — в разделах ниже и в
> бэклог не входит. Провайдерные `todo_*.md` остаются источником деталей по конкретному диалекту.

## PostgreSQL — не заблокировано

| Пункт | Объём | Что нужно | Источник |
| --- | --- | --- | --- |
| `round(double precision, int)` | простое | диалектный хук (`MakeMathFunction`): PostgreSQL определяет только `round(numeric, int)`, поэтому первый аргумент оборачивается в `(...)::numeric` | `todo_postgres.md:68` |
| Публичный `EXTRACT`/`date_part` (`quarter`, `week`, `epoch`, `dow`, `isodow`) | простое | метод-обёртка `SqlFunctions.Sql.extract(part, value)` (и/или свойства `DateTime.DayOfWeek`); `MakeDatePart`/`MakeDateDiff` уже есть | `todo_postgres.md:46–49` |
| `setseed(float)` | простое | скалярный PostgreSQL-метод (рядом с `random()`) | `todo_postgres.md:65` |
| `array_shuffle` / `array_sample` | простое | скалярные array-функции | `todo_postgres.md:78` |
| `digest` / `sha256` (pgcrypto) | простое | скалярные; нужен `Supports*`-гейт — требует расширения `pgcrypto` | `todo_postgres.md:55` |
| Наборные функции через `[SqlTableFunction]`: `regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements`(`_text`), `jsonb_each`(`_text`), `jsonb_object_keys`, `jsonb_path_query`, `ts_stat` | среднее | механизм `[SqlTableFunction]` + `FromTableFunction` уже есть; для `jsonb_each`/`ts_stat` нужен row-тип на 2–4 колонки | `todo_postgres.md:51,86,88,106–108` |
| Именованные окна (`WINDOW w AS (...)`), режим фрейма `GROUPS`, исключения фрейма (`EXCLUDE`) | сложное | расширение модели окон/фреймов | `todo_postgres.md:100` |

## SQL Server — не заблокировано

| Пункт | Объём | Что нужно | Источник |
| --- | --- | --- | --- |
| `FORMAT(value, formatString)` (дата/число) | простое | кросс-провайдерный `CommonFunctions.format_date(value, template)` + флаг/хук (PG-аналог — `to_char`) либо явная фиксация «только через `[SqlFunction]`» | `todo_mssql.md:62` |
| Общий коррелированный скалярный подзапрос в проекции | среднее/сложное | публичный API поверх уже существующего механизма `OuterRefMarker` | `todo_mssql.md:222` |
| `PIVOT` / `UNPIVOT` | сложное | новая конструкция модели запроса: агрегатная спецификация, список значений, переименование колонок; `SqlBuilder`/`EntityBuilder`/диалекты | `todo_mssql.md:229` |
| XML-тип и методы (`.value`, `.query`, `.nodes`, `.exist`) | сложное | постфиксный вызов метода на колонке, отдельный синтаксис | `todo_mssql.md:238` |
| `OPENJSON ... WITH` (типизированная схема) | низкий | пользовательский `[SqlTableFunction]`-враппер уже покрывает случай; встроенная поддержка — опционально | `todo_mssql.md:108` |
| `PATINDEX` | n/a | C#-аналога нет; корректный кейс `[SqlFunction]`, а не бэклог | `todo_mssql.md:57` |

## ClickHouse — не заблокировано

| Пункт | Объём | Что нужно | Источник |
| --- | --- | --- | --- |
| Функции приведения и частей даты: `toDate`/`toDateTime`/`toDate32`, `toYear`/`toQuarter`/`toMonth`/`toDayOfMonth`/`toDayOfWeek`/`toHour`/…, `toStartOf*`, `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp` | простое/среднее | методы `ClickHouseFunctions` + флаг + ветки `MakeDate*`; модель запроса не меняется | `todo_clickhouse.md:70` |
| `string.Split` → `splitByChar` | простое | ветка в строковом маппинге | `todo_clickhouse.md:230` |
| Скалярные array-функции: `range`, `arrayEnumerate`, `arrayCumSum`, `arraySlice`, `arrayPushBack` | среднее | скалярные формы; проекция самого массива упирается в row reader | `todo_clickhouse.md:230` |
| Продвинутые агрегаты: `windowFunnel`, `retention`, `sequenceMatch` | среднее/сложное | аккуратная типизация (DateTime/условия) | `todo_clickhouse.md:161` |
| Прочие табличные функции: `generateRandom` | среднее | через `WrapTableFunction` (остальные — см. «заблокировано») | `todo_clickhouse.md:328` |

## Заблокировано (справочно; в Phase 2 не берём)

| Блокер | Что блокирует |
| --- | --- |
| Нет row reader для `Array(T)`/`Tuple` | CH `groupArray`/`groupUniqArray`, `topK`/`topKWeighted`, `quantiles`, `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`, `tuple`/`tupleElement`/`untuple`, проекция array-колонок |
| Нет типа-состояния `AggregateFunction(...)` | CH комбинаторы `-State`/`-Merge` (`uniqState`/`uniqMerge`/…), `runningAccumulate` |
| Нет API типа колонки для нативного `JSON` | CH `JSON_VALUE`/`JSON_QUERY` нового типа `JSON`, `JSONAllPaths*`, `toJSONString` |
| Трансляция lambda/higher-order аргументов не начата | CH `arrayMap`/`arrayFilter`/`arrayExists`/`arrayAll`/`arrayCount`/`arrayFirst*` |
| Нет внешней ссылки внутри `FROM`-источника | MSSQL «сырой SQL как композируемый источник», коррелированный `APPLY`, `CONTAINSTABLE`/`FREETEXTTABLE`; PG-часть `jsonb_to_record(set)`/`json_populate_record` |
| Нет binding/типа для скаляров над `Array` | CH `startsWith`/`endsWith`/`position`/`length` над массивами |
| API-дизайн не выбран | CH join `SEMI`/`ANTI` (меняют набор колонок — несовместимо с `Projection<T1,T2>`) и `PASTE JOIN` (нет `ON`) |
| Динамическая схема результата | CH `values()`, PG `jsonb_to_record(set)` |
| Конфигурация сервера/кластера | CH `url`/`s3`/`remote`/`remoteSecure`/`file`/`format`/`merge`/`input`/`cluster`/`clusterAllReplicas` |
| Сервер не умеет | MSSQL `INTERSECT ALL`/`EXCEPT ALL` (диалект корректно бросает `NotSupportedException`) |
| Движок таблицы не поддерживает | CH `FINAL`/`PREWHERE`/`SAMPLE` на движке `Memory` (только SQL-gen тесты) — ограничение интеграционных тестов, не функционала |

## За рамками проекта на текущий момент (read-only)

- DML: `INSERT` / `UPDATE` / `DELETE` / `MERGE` (в т.ч. `OUTPUT` и `RETURNING`).
- Транзакции, `SaveChanges`, change tracking / CDC (read-only участие в чужой транзакции — отдельный
  `docs/specs/todo_transactions.md`).
- Хранимые процедуры и динамический SQL.
- Навигационные свойства и связи — только явные join.
- Внешние источники: `OPENROWSET` / `OPENQUERY` / linked servers.
- DDL/административная поверхность: `OPTIMIZE`, `ALTER`, `RENAME` и пр.

## Порядок работы

Один пункт — одно изменение по скиллу `implementing-todo-features`: WIP-отчёт → матрица «провайдер ×
форма» → диалектный хук за `Supports*`-флагом → SQL-gen + rejection + интеграционные тесты → аудит
`nextorm-code-auditor` → покрытие ≥ `MIN_LINE_COVERAGE` → docs EN/RU + обновление профильного
`todo_*.md` и `sql-capabilities-gap-analysis.md`.
