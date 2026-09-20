# todo_phase2 — незаблокированный бэклог (Phase 2)

> Свод остатка `todo_postgres.md` / `todo_mssql.md` / `todo_clickhouse.md` после Phase 1
> (управляющий триаж — `WIP_provider_gap_triage.md`; завершённые WIP-отчёты удалены, закрытые пункты
> помечены `[x]`). Здесь только пункты, которые **не заблокированы**: их можно брать в работу скиллом
> `implementing-todo-features`. Заблокированное и вынесенное за рамки проекта — в разделах ниже и в
> бэклог не входит. Провайдерные `todo_*.md` остаются источником деталей по конкретному диалекту.

## PostgreSQL — не заблокировано

| Пункт | Объём | Что нужно | Источник |
| --- | --- | --- | --- |
| `round(double precision, int)` | готово | диалектный хук `MakeMathFunction(...3 арг.)`: PostgreSQL оборачивает первый `double`/`float`-аргумент в `(...)::numeric`; см. `WIP_round.md` | `todo_postgres.md:95` |
| Публичный `EXTRACT`/`date_part` (`quarter`, `week`, `epoch`, `dow`, `isodow`) | готово | `SqlFunctions.Sql.extract(part, value)` (`int?`) + `date_part("epoch", value)` (`double?`), флаг `SupportsDatePart`; `MakeDatePart`/`MakeDateDiff` уже есть; см. `WIP_date_part.md` | `todo_postgres.md:47` |
| `setseed(float)` | готово | скалярный `PostgresFunctions.setseed` под `SupportsRandomSeed` (PG-only); см. `WIP_setseed.md` | `todo_postgres.md:87` |
| `array_shuffle` / `array_sample` | готово | скалярные `PostgresFunctions.array_shuffle`/`array_sample` (PG16+) под `SupportsArrays`; см. `WIP_array_shuffle.md` | `todo_postgres.md:110` |
| `digest` / `sha256` (pgcrypto) | готово | `PostgresFunctions.digest`/`sha256` под `SupportsCryptoFunctions`; `digest` требует расширения `pgcrypto`; см. `WIP_digest.md` | `todo_postgres.md:70` |
| Наборные функции через `[SqlTableFunction]`: `regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements`(`_text`), `jsonb_each`(`_text`), `jsonb_object_keys`, `jsonb_path_query`, `ts_stat` | готово | встроенные `PostgresFunctions`-методы + row-shape `IRegexpMatchesRow`/`IJsonbEachRow`/`ITsStatRow` и др.; `regexp_matches` отдаёт `text[]` (row reader `string[]`), `jsonb_path_query` использует `PostgresFunctions.jsonpath`; диалект оборачивает функции с колонкой-как-функция в подзапрос (`WrapTableFunction`); см. `WIP_pg_setof_functions.md` | `todo_postgres.md:51,86,88,106–108` |
| Именованные окна (`WINDOW w AS (...)`), режим фрейма `GROUPS`, исключения фрейма (`EXCLUDE`) | сложное | расширение модели окон/фреймов | `todo_postgres.md:100` |

## SQL Server — не заблокировано

| Пункт | Объём | Что нужно | Источник |
| --- | --- | --- | --- |
| `FORMAT(value, formatString)` (дата/число) | закрыто | Явная фиксация «только через `[SqlFunction]`»: языки шаблонов (.NET / PG / `%`) несовместимы. См. `todo_mssql.md` (пункт `[x]`) и `WIP_format_date.md` | `todo_mssql.md:62` |
| Общий коррелированный скалярный подзапрос в проекции | готово | механизм `OuterRefMarker` уже есть; публичный API не потребовался (терминалы `QueryCommand<T>`), закрыт остаток — ссылка на член join-проекции (`p.Item1.Id`); см. `WIP_correlated_scalar_projection.md` | `todo_mssql.md:244` |
| `PIVOT` / `UNPIVOT` | сложное | новая конструкция модели запроса: агрегатная спецификация, список значений, переименование колонок; `SqlBuilder`/`EntityBuilder`/диалекты | `todo_mssql.md:229` |
| XML-тип и методы (`.value`, `.query`, `.nodes`, `.exist`) | сложное | постфиксный вызов метода на колонке, отдельный синтаксис | `todo_mssql.md:238` |
| `OPENJSON ... WITH` (типизированная схема) | закрыто | `SqlTableFunctionAttribute.WithClause` → `openjson(...) with (...)`. См. `todo_mssql.md` (пункт `[x]`) и `WIP_openjson_with.md` | `todo_mssql.md:108` |
| `PATINDEX` | n/a | C#-аналога нет; корректный кейс `[SqlFunction]`, а не бэклог | `todo_mssql.md:57` |

## ClickHouse — не заблокировано

| Пункт | Объём | Что нужно | Источник |
| --- | --- | --- | --- |
| Функции приведения и частей даты: `toDate`/`toDateTime`/`toDate32`, `toYear`/`toQuarter`/`toMonth`/`toDayOfMonth`/`toDayOfWeek`/`toHour`/…, `toStartOf*`, `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp` | готово | методы `ClickHouseFunctions` + `SupportsDateConversionFunctions` + `MakeDateConversion`/`MakeDatePart`; модель запроса не меняется (см. `todo_clickhouse.md:72`) | `todo_clickhouse.md:70` |
| `string.Split` → `splitByChar` | готово | флаг `SupportsStringSplit` + хук `MakeStringSplit`; один символ-разделитель, многоместные/`StringSplitOptions` отклоняются | `todo_clickhouse.md:230` |
| Скалярные array-функции: `range`, `arrayEnumerate`, `arrayCumSum`, `arraySlice`, `arrayPushBack` | готово | скалярные формы через `ClickHouseFunctions` + `ArraySqlTranslator` (гейт `SupportsArrayFunctions`); проекция самого массива по-прежнему упирается в row reader, функции применимы вложенно; см. `WIP_clickhouse_array_scalar_functions.md` | `todo_clickhouse.md:230` |
| Продвинутые агрегаты: `windowFunnel`, `retention`, `sequenceMatch` | готово | `ClickHouseFunctions.window_funnel`/`sequence_match`/`retention` + `SupportsSequenceAggregates` + `MakeSequenceAggregate`; `toInt32` для скалярных, `retention` — массив (только вложенно); см. `WIP_clickhouse_sequence_aggregates.md` | `todo_clickhouse.md:161` |
| Прочие табличные функции: `generateRandom` | готово | через `WrapTableFunction` с фиксированной структурой и row-типом `IGenerateRandomRow` (см. `todo_clickhouse.md:355`); остальные — см. «заблокировано» | `todo_clickhouse.md:328` |

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
