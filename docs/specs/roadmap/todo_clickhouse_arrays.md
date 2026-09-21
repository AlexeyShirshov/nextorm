# TODO: Массивы ClickHouse — остаток (row reader, higher-order, скаляры над массивами)

> Actionable-остаток массивы-воркстрима ClickHouse. Первый срез (функции первого порядка,
> `[LEFT] ARRAY JOIN`, привязка вырожденного элемента) реализован.
> **Статус: заблокировано.** Движок ClickHouse умеет всё перечисленное нативно; не хватает
> инфраструктуры nextorm — row reader для массивов, binding `T[]`-параметров с явным типом и
> трансляции lambda/higher-order аргументов.

## Что уже сделано (не переделывать)

- Функции первого порядка над array-колонками: `ClickHouseFunctions.length`/`has`/`index_of`/
  `has_any`/`has_all`/`array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/
  `array_distinct`; флаг `SupportsArrayFunctions`, хук `MakeArrayFunction` (`length`/`indexOf` →
  `toInt64(...)`, т.к. нативно `UInt64`); array-колонка рендерится как SQL, захваченный массив —
  одним параметром.
- Скалярный `array_join` (разворачивает строки) — `SupportsArrayJoin`.
- Клауза `[LEFT] ARRAY JOIN`: `EntityBuilder.ArrayJoin`/`LeftArrayJoin`, `ArrayJoinKind`,
  `ArrayJoinClause`/`IArrayJoinRenderer.Render`.
- Привязка вырожденного элемента: `ArrayJoinElement`/`LeftArrayJoinElement` →
  `EntityBuilder<ArrayJoinProjection<TEntity, TElement>>` (маркер `IArrayJoinProjection`),
  `MemberTranslator` транслирует `.Element` в алиас `__nextorm_aj_element`.
- `string.Split` → `splitByChar`: `StringSplit` + `IStringSplitRenderer.Render` (CH-only) — **готово**.

## Блокер (общий)

- **Нет row reader для `Array(T)`/`Tuple`** — массив/Tuple-колонку нельзя материализовать или
  спроецировать как значение; это же ограничивает `groupArray`/`topK`/`quantiles`/`JSONExtractArrayRaw`
  и проекцию array-колонок.
- **Binding `T[]`-параметров требует явного типа** (`Array(Int64)`): вся array-поверхность привязана к
  единому гейту `SupportsArrays`, включённому **только у PostgreSQL**; `SqlOperandTranslator`,
  `ArraySqlTranslator`, `StringFunctionTranslator` бросают вне PG. Нужно разделить гейт на
  `SupportsArrayParameters` и `SupportsArrayFunctions`.
- **Трансляция lambda/higher-order аргументов не начата.**

## Остаток (что нужно, чтобы разблокировать)

### 1. Row reader `Array(T)` / `Tuple`

- Материализация array/Tuple-колонок и агрегатов `groupArray`/`groupUniqArray`/`topK`/`topKWeighted`/
  `quantiles`/`JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`.
- Скаляры `tuple`/`tupleElement`/`untuple`.
- Функции словарей, возвращающие массивы: `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`.

### 2. Higher-order / lambda

- `arrayMap`/`arrayFilter`/`arrayExists`/`arrayAll`/`arrayCount`/`arrayFirst*` — лямбда-аргумент.

### 3. Скаляры над массивами (пункт `[ ]` прежнего бэклога)

Корректные соответствия: `position` → `indexOf` (1-based, `0` при отсутствии), `length` → `length`
(алиас `CARDINALITY`); префикс/суффикс массива — **не** `startsWith`/`endsWith` (они принимают только
`String`/`FixedString`), а `hasSubstr(arr, sub)` (упорядоченный подмассив, v20.6+) либо
`arraySlice(arr, ...)`. Требует пунктов 1 и разделения гейта.

### 4. Несколько массивов / join'ы

- Привязка элемента для нескольких массивов/join'ов (сейчас один источник без `join`).

## Матрица «провайдер × форма»

Формы: length массива, position массива, префикс/суффикс массива (по документации СУБД).

| Провайдер | length | position | prefix / suffix | Источник |
| --- | --- | --- | --- | --- |
| PostgreSQL | `cardinality(arr)`, `array_length(arr, 1)` | `array_position(arr, x)` (1-based, `NULL` при отсутствии) | нативной функции нет; выразимо срезом | https://www.postgresql.org/docs/current/functions-array.html |
| SQL Server | — нет array-типа | — | — | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| MySQL | — нет array-типа | — | — | https://dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | — нет array-типа | — | — | https://mariadb.com/kb/en/json-data-type/ |
| ClickHouse | `length(arr)` (алиас `CARDINALITY`) | `indexOf(arr, x)` (1-based, `0` при отсутствии) | `hasSubstr(arr, sub)` либо `arraySlice(arr, ...)` | https://clickhouse.com/docs/en/sql-reference/functions/array-functions |
| SQLite | `json_array_length(json)` (только JSON-массивы) | — | — | https://www.sqlite.org/json1.html |
| InMemory | `Array.Length` (CLR) | `Array.IndexOf` (CLR) | `SequenceEqual`/`Take` (CLR) | — |

## Критерий приёмки

- Array/Tuple-колонка и агрегаты из п.1 материализуются и проецируются; запрос строится, выполняется
  на реальном ClickHouse 25.8.
- Higher-order функции из п.2 рендерятся и выполняются.
- Скаляры из п.3 корректно маппятся; прочие провайдеры отклоняют (`NotSupportedException`).
- SQL-gen + rejection + интеграционные тесты зелёные; покрытие ≥ `MIN_LINE_COVERAGE`; аудит
  `nextorm-code-auditor`; docs EN+RU.

## Источники и файлы

- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.4/5/9.
- `sql-capabilities-gap-analysis.md` — строки по массивам/JSON.
- Код: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`,
  `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Visitors/ArraySqlTranslator.cs`,
  `SqlOperandTranslator.cs`, `MemberTranslator.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
  `src/nextorm.core/Expressions/ArrayJoinKind.cs`, `ArrayJoinProjection.cs`.
