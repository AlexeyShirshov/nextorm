# WIP: ClickHouse скалярные array-функции (`range`/`arrayEnumerate`/`arrayCumSum`/`arraySlice`/`arrayPushBack`)

- Пункт бэклога: `docs/specs/roadmap/todo_phase2.md:38`; `docs/specs/roadmap/todo_clickhouse.md:258-259`
  (подпункт массивы-воркстрима «Осталось»).
- Целевой провайдер: ClickHouse. Модель запроса не меняется.
- Критерий приёмки: `SqlFunctions.ClickHouse` предоставляет `range` (1/2/3 арг.),
  `array_enumerate`, `array_cum_sum`, `array_slice` (2/3 арг.), `array_push_back`; вызовы рендерятся в
  нативные CH-имена и **вкладываются** в скалярные array-функции (`length`, `array_string_concat`);
  прочие провайдеры отвергают вызов `NotSupportedException`. Проекция самого массива остаётся
  заблокированной отсутствующим row reader `Array(T)` — это документируется, а не обходится.

## Провайдер × форма (шаг 1)

Источники: ClickHouse — официальный array-functions reference
(clickhouse.com/docs/en/sql-reference/functions/array-functions); PostgreSQL — официальный array
reference (postgresql.org/docs/current/functions-array.html); SQL Server / MySQL / MariaDB / SQLite —
соответствующие справочники (массива как типа нет); InMemory — CLR-поверхность nextorm.

| Провайдер | `range` | `arrayEnumerate` | `arrayCumSum` | `arraySlice` | `arrayPushBack` | Источник |
|---|---|---|---|---|---|---|
| PostgreSQL | `—`: `generate_series` — set-returning (уже TVF), не массив; `int4range` — тип диапазона, не массив | `—`: `generate_subscripts` set-returning, не массив | `—`: агрегат/window `sum() over (...)` возвращает скаляры, не массив | частично срез `arr[2:3]`, отдельной функции нет | `array_append(arr, x)` (уже в `PostgresFunctions`; имя/семантика PG) | functions-array.html |
| SQL Server | `—`: нет array-типа | `—` | `—` | `—` | `—` | learn.microsoft.com/sql/t-sql/data-types |
| MySQL | `—`: нет array-типа (JSON только через `JSON_TABLE`) | `—` | `—` | `—` | `—` | dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | `—`: нет array-типа (`JSON` — псевдоним `LONGTEXT`) | `—` | `—` | `—` | `—` | mariadb.com/kb/en/json-data-type/ |
| ClickHouse | `range([start,] end[, step])` | `arrayEnumerate(arr)` (Array(UInt32)) | `arrayCumSum(arr)` (тип элементов сохраняется) | `arraySlice(arr, offset[, length])` | `arrayPushBack(arr, x)` | clickhouse.com/docs/en/sql-reference/functions/array-functions |
| SQLite | `—`: нет array-типа (JSON1 не даёт этих функций) | `—` | `—` | `—` | `—` | sqlite.org/json1.html |
| InMemory | `—`: CH-only поверхность; in-memory не участвует | — | — | — | — | nextorm (гейт `SupportsArrayFunctions`) |

**Единообразие провайдеров.** Нативные `range`/`arrayEnumerate`/`arrayCumSum`/`arraySlice`/
`arrayPushBack` есть только у ClickHouse (PG-аналоги либо имеют другое имя/семантику и уже закрыты
`PostgresFunctions`/`generate_series`, либо set-returning и не дают массив). Поэтому это
**ClickHouse-поверхность** `ClickHouseFunctions` (tier b) под уже существующим гейтом
`ISqlDialect.SupportsArrayFunctions` (default `false`; ClickHouse `true`); новый `Supports*`-флаг не
вводится, т.к. это ровно то же семейство «array-функции над нативным `Array(T)`», что и уже
реализованные `length`/`has`/`indexOf`/`arraySort`/… Отдельного `Supports*` под каждый член не
требуется: диалект без `SupportsArrayFunctions` бросает `NotSupportedException` с понятным
сообщением (`ArraySqlTranslator.RequireArrayFunctions`). SQL Server/MySQL/MariaDB/SQLite/InMemory не
выражают массивы — гейт `false`.

Имена CH = camelCase от CLR-имени: `range`→`range`, `array_enumerate`→`arrayEnumerate`,
`array_cum_sum`→`arrayCumSum`, `array_slice`→`arraySlice`, `array_push_back`→`arrayPushBack`.

## Ближайший аналог C# и tier

- BCL-члена с точной семантикой нет (`Enumerable.Range` возвращает ленивую последовательность и
  принимает другой набор аргументов; `Array.Copy`/`Array.IndexOf` — не SQL-массив). Tier (b): новые
  методы `ClickHouseFunctions` + ветки `ArraySqlTranslator` + существующий хук `MakeArrayFunction`.
- `array_slice` близок к `ArraySegment`, но SQL-offset 1-based/отрицательный — BCL-семантика иная.

## Диалектный план

- `ISqlDialect.SupportsArrayFunctions` (default `false`) — без изменений, гейтит новые члены.
- `ISqlDialect.MakeArrayFunction(name, call)` — без изменений (ClickHouse кастит только
  `length`/`indexOf`; новые функции возвращают массив, каст не нужен — тип определяется внешней
  функцией).
- `ArraySqlTranslator.TryTranslateClickHouseArray` — новые ветки; рендер переиспользует
  `EmitArrayFunction` (аргументы через `SqlOperandTranslator.AppendArrayOrColumn`, array-колонка —
  SQL, захваченный массив — один параметр).
- `SqlDialectBase`/другие диалекты — без изменений.

## Публичный API (точные сигнатуры)

`ClickHouseFunctions`:
- `long[] range(long end)`, `long[] range(long start, long end)`,
  `long[] range(long start, long end, long step)`;
- `long[] array_enumerate<T>(T[] array)`;
- `T[] array_cum_sum<T>(T[] array)`;
- `T[] array_slice<T>(T[] array, long offset)`, `T[] array_slice<T>(T[] array, long offset, long length)`;
- `T[] array_push_back<T>(T[] array, T element)`.

Каждый — `<summary>` с оговоркой «возвращает массив, поэтому применим только вложенно» и ссылкой на
`SupportsArrayFunctions`. Регистр — `API-NAMING-REVIEW.md`.

## План тестов

- CH SQL-gen (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`): `ArrayRange_*`,
  `ArrayEnumerate_*`, `ArrayCumSum_*`, `ArraySlice_*`, `ArrayPushBack_*` — рендер имён и вложение.
- CH хук (`ClickHouseDialectTests.cs`): расширение `ArrayCapabilities_ShouldBeEnabled` (флаг уже
  покрыт), новых хуков нет.
- Rejection (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`):
  `ClickHouseArrayScalarFunctions_UnsupportedByProvider_ShouldThrow`.
- Интеграция (`ClickHouseIntegrationTests.cs`, реальный ClickHouse):
  `ArrayScalarFunctions_ShouldReturnValues` — nested scalar-вычисления (`length(range(...))`,
  `arrayStringConcat(arrayCumSum(nums), ',')`, `arraySlice`, `arrayPushBack`, `arrayEnumerate`).
- In-memory: поверхность ClickHouse-only, in-memory не участвует.
- Покрытие: `coverage.settings.xml` не включает `nextorm.clickhouse`, поэтому число не изменится;
  SQL-gen тесты всё равно обязательны.

## Документация

`docs/guide/11-scalar-functions.md` (+RU), `docs/providers/clickhouse.md` (+RU),
`docs/guide/provider-specific/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/roadmap/todo_clickhouse.md`,
`docs/specs/roadmap/todo_phase2.md`, `docs/specs/design/API-NAMING-REVIEW.md`.

## Статус

Реализовано, протестировано; см. коммит `CH scalar array functions`.
