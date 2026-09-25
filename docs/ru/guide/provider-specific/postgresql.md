# Специфичный для PostgreSQL SQL

> У PostgreSQL самая богатая система типов, поэтому его эксклюзивная поверхность — это набор функций и
> операторов над нативными типами, а не ключевые слова запроса: массивы, `json`/`jsonb`,
> упорядоченные, регрессионные и логические агрегаты, расширенные скаляры/regexp, `DISTINCT ON` и
> табличная функция `unnest`.

**Что нужно знать:** [Запросы и проекции](../01-querying-and-projections.md) · [Провайдер PostgreSQL](../../providers/postgres.md)

## Массивы

У PostgreSQL есть нативные типы-массивы. Операнд-массив всегда передаётся **одним параметром** (весь
массив), а не разворачивается в список значений, поэтому текст SQL не зависит от числа элементов и
план остаётся кэшируемым. Поверхность гейтится
[`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) и включает `cardinality`,
`array_length`, `array_position`, `array_append`, `array_cat`, `array_to_string`, `string_to_array` и
операторы `@>`, `<@`, `&&`, `||`, а также `any`/`all` по массиву:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.any(e.Id, ids))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (id = any(@p0))
```

См. [Массивы (PostgreSQL)](../../scalar-functions/06-arrays.md#массивы-postgresql). Функции, возвращающие
массив, можно использовать внутри запроса или проецировать напрямую: row reader материализует
результат `Array(T)` как CLR `T[]`.

## Range-типы

У PostgreSQL есть нативные range-типы (`int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange`,
`daterange`). nextorm представляет их provider-agnostic value-типом
[`Range<T>`](xref:NextORM.Core.Range`1) — `Lower`/`Upper`, `LowerInclusive`/`UpperInclusive`,
`LowerInfinite`/`UpperInfinite`, `IsEmpty` и [`Range<T>.Empty`](xref:NextORM.Core.Range`1.Empty) — который
провайдер PostgreSQL биндит через Npgsql, сохраняя unbounded стороны и пустой диапазон. Поверхность
гейтится [`SupportsRanges`](xref:NextORM.Core.ISqlDialect.SupportsRanges) и живёт на
[`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres). Операторы вынесены в статические методы с именами
SQL-токенов, поэтому ни один член не конфликтует с CLR-оператором или полнотекстовым `Contains`:

На базе без нативного range-типа отобразите свойство `Range<T>` на пару скалярных колонок через [`RangeColumns`](../31-range-columns.md); те же операторы транслируются поверх пары.

| Член | SQL |
|---|---|
| `overlaps(a, b)` | `a && b` |
| `range_contains(range, value)` / `range_contains(outer, inner)` | `range @> value` / `outer @> inner` |
| `range_contained_by(inner, outer)` | `inner <@ outer` |
| `range_union(a, b)` / `range_intersection(a, b)` / `range_difference(a, b)` | `a + b` / `a * b` / `a - b` |
| `range_adjacent(a, b)` | `a -|- b` |
| `range_strictly_left_of(a, b)` / `range_strictly_right_of(a, b)` | `a << b` / `a >> b` |
| `range_not_extend_right_of(a, b)` / `range_not_extend_left_of(a, b)` | `a &< b` / `a &> b` |
| `lower(range)` / `upper(range)` / `isempty(range)` | `lower(range)` / `upper(range)` / `isempty(range)` |
| `lower_inc` / `upper_inc` / `lower_inf` / `upper_inf` | `lower_inc(range)` / … |
| `int4range` / `int8range` / `numrange` / `tsrange` / `tstzrange` / `daterange` | нативный конструктор, при необходимости с литералом границ (`'[)'`, `'[]'`, `'()'`, `'(]'`) |
| `empty_range<T>()` | `'empty'::<range type>` |

```csharp
var window = new Range<int>(15, 25);   // [15,25)

var rows = dataContext.From<IReservation>()
    .Where(e => SqlFunctions.Postgres.overlaps(e.During, window))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from reservation where (during && @p0)
```

`Range<T>` — `readonly struct` и сравнивается по своим PostgreSQL-характеристикам, поэтому
`Range<int>.Empty` и `default(Range<int>)` оба являются пустым диапазоном. In-memory провайдер
вычисляет всю поверхность выше — `overlaps`, `range_contains`/`range_contained_by`,
`range_union`/`range_intersection`/`range_difference`, позиционные и смежные операторы, функции
проверки и конструкторы — с той же семантикой, что PostgreSQL (включая ошибку
`result of range union/difference would not be contiguous`). Timestamp-диапазоны используют
`DateTime` для `tsrange` и `DateTimeOffset` для `tstzrange`; `daterange` использует `DateOnly`.

### Multirange

Multirange-типы PostgreSQL (`int4multirange`, `int8multirange`, `nummultirange`, `tsmultirange`,
`tstzmultirange`, `datemultirange`) представляются как `Range<T>[]` — биндятся и читаются как
`NpgsqlRange<T>[]`. Массив канонический: отсортирован по нижней границе, перекрывающиеся и смежные
диапазоны объединены. Сопоставьте свойство или параметр напрямую с `Range<T>[]`; имена операторов выше
перегружены для multirange (`overlaps`, `range_contains`, `range_contained_by`, `range_union`,
`range_intersection`, `range_difference`, `range_adjacent` и позиционные), плюс `multirange(range)`,
`range_merge`, функции проверки и агрегаты `range_agg` / `range_intersect_agg`:

```csharp
var window = new[] { new Range<int>(15, 25), new Range<int>(40, 50) };

var rows = dataContext.From<IReservation>()
    .Where(e => SqlFunctions.Postgres.overlaps(e.During, window))
    .Select(e => new
    {
        All = SqlFunctions.Postgres.range_agg(e.During),
        Common = SqlFunctions.Postgres.range_intersect_agg(e.During)
    })
    .ToList();
```

## `json` и `jsonb`

PostgreSQL — единственный провайдер с нативными типами `json`/`jsonb`. JSON-операнды — это
сопоставленные колонки, другие JSON-функции или параметры, чьё значение во время выполнения —
`JsonDocument`/`JsonElement`/`JsonNode` (Npgsql биндит их как `jsonb`). Семейство `jsonb_*` покрывает
построение, извлечение, вложенность и агрегацию: `jsonb_build_object`, `jsonb_agg`, `json_get_text`,
`json_cast`, операторы `->`, `->>`.

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String))
    .First();
```

```sql
select jsonb_agg(somestring) from complex_entity
```

См. [JSON и JSONB (PostgreSQL)](../../scalar-functions/index.md) и
[Поддержка JSON в разных провайдерах](../18-json.md).

## Упорядоченные, регрессионные и логические агрегаты

* `percentile_cont`/`percentile_disc` — упорядоченные агрегаты, рендерятся как
  `percentile_cont(f) within group (order by x)` ([`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates),
  [`MakeWithinGroup`](xref:NextORM.Core.ISqlDialect.MakeWithinGroup(System.String,System.String,NextORM.Core.KeywordCase))); `mode()` — упорядоченный агрегат mode;
* `bool_and`/`bool_or`/`every`, семейство регрессии `regr_*` и `bit_and`/`bit_or`/`bit_xor` есть только
  у PostgreSQL, и остальные диалекты их отклоняют;
* `array_agg` и поверхность строковых/массивных агрегатов гейтятся
  [`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates).

См. [Группировка и агрегаты](../04-grouping-and-aggregates.md).

## Полнотекстовый поиск

Кросс-провайдерные boolean-предикаты `contains`/`freetext` рендерят
`to_tsvector(col) @@ plainto_tsquery(search)` (либо `websearch_to_tsquery` для `freetext`). PostgreSQL
дополнительно предоставляет native-поверхность текстового поиска, гейтится
[`SupportsTextSearchFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTextSearchFunctions):
`to_tsvector`, `to_tsquery` (плюс `plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`),
`ts_rank`, `ts_rank_cd` (ранжирование по плотности покрытия), `ts_headline` и оператор совпадения `@@`.
`tsvector`/`tsquery` на CLR-стороне представлены
`string`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.ts_match(
        SqlFunctions.Postgres.to_tsvector(e.String!),
        SqlFunctions.Postgres.websearch_to_tsquery("cat & dog")))
    .Select(e => new
    {
        e.Id,
        Rank = SqlFunctions.Postgres.ts_rank(
            SqlFunctions.Postgres.to_tsvector(e.String!),
            SqlFunctions.Postgres.to_tsquery("cat"))
    })
    .ToList();
```

```sql
select id, ts_rank(to_tsvector(somestring), to_tsquery('cat')) from complex_entity
 where (to_tsvector(somestring) @@ websearch_to_tsquery('cat & dog'))
```

Native-поверхность реализована только в PostgreSQL; остальные провайдеры отклоняют её на этапе
построения SQL.

## Расширенные скаляры и regexp

Поверхность [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) добавляет `pg_typeof`, `num_nulls`/
`num_nonnulls`, `regexp_replace`/`regexp_like`/`regexp_split_to_array`/`regexp_count`/`regexp_instr`,
`split_part`/`strpos`/`left`/`right`/`lpad`/`rpad`/`translate`/`overlay`/`format`, математические
помощники (`pi`/`random`/`gcd`/`lcm`/`width_bucket`/...), расширенные помощники даты/времени
(`to_char`/`to_date`/`to_number`/`to_timestamp`/`timezone`/`make_interval`/`justify_*`) и
конструкторы массивов, всё гейтится `SupportsExtendedScalarFunctions`.

См. [Скалярные функции](../../scalar-functions/index.md).

## `DISTINCT ON`

[`DistinctOn`](xref:NextORM.Core.EntityBuilder`1.DistinctOn``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) рендерит `SELECT DISTINCT ON (expr, ...)`, оставляя
первую строку каждого ключа ([`DistinctOn`](xref:NextORM.Core.ISqlDialect.DistinctOn));
взаимоисключающе с `Distinct`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring from complex_entity order by somestring
```

См. [DISTINCT](../08-distinct.md).

## Функции, возвращающие наборы

Функции PostgreSQL, возвращающие набор, доступны как табличные функции через
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})):

| `SqlFunctions.Postgres.*` | Колонки SQL | Row-shape |
| --- | --- | --- |
| `generate_series(start, stop[, step])` | `generate_series` | `IGenerateSeriesRow` |
| `unnest(array)` | `unnest` | `IUnnestRow<T>` |
| `regexp_matches(source, pattern[, flags])` | `regexp_matches text[]` | `IRegexpMatchesRow` |
| `regexp_split_to_table(source, pattern[, flags])` | `regexp_split_to_table` | `IRegexpSplitToTableRow` |
| `jsonb_array_elements(json)` / `jsonb_array_elements_text(json)` | `value` | `IJsonArrayElementsRow` |
| `jsonb_each(json)` / `jsonb_each_text(json)` | `key`, `value` | `IJsonbEachRow` |
| `jsonb_object_keys(json)` | `jsonb_object_keys` | `IJsonObjectKeysRow` |
| `jsonb_path_query(json, jsonpath)` | `jsonb_path_query` | `IJsonPathQueryRow` |
| `ts_stat(query)` | `word`, `ndoc`, `nentry` | `ITsStatRow` |

JSONPath-аргумент передаётся текстом через `SqlFunctions.Postgres.jsonpath(path)`, что рендерится как
`cast(path as jsonpath)`.

PostgreSQL заменяет единственную колонку скалярной наборной функции на псевдоним, который nextorm
всегда добавляет производному источнику, поэтому диалект оборачивает такие вызовы в подзапрос с одной
колонкой (`from (select generate_series from generate_series(...)) as "t1"`); функции с явной колонкой
(`value`, `key`/`value`, `word`/`ndoc`/`nentry`) эмитятся без обёртки.

См. [Табличные функции](../13-table-valued-functions.md).

## `TABLESAMPLE`

[`FromOptions.TableSample`](xref:NextORM.Core.FromOptions.TableSample(System.Double,NextORM.Core.TableSampleMethod,System.Nullable{System.Double})) добавляет модификатор `TABLESAMPLE` к
основной таблице запроса ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample)).
PostgreSQL поддерживает оба метода сэмплирования и необязательное повторяемое зерно (seed)
(`TableSample`,
`ITableSampleMethods.Render`):

```csharp
var rows = dataContext.From<ISimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, seed: 42))
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from simple_entity tablesample system (10) repeatable (42)
```

[`TableSampleMethod.System`](xref:NextORM.Core.TableSampleMethod.System) генерирует
`tablesample system (10)`, а [`TableSampleMethod.Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli)
— `tablesample bernoulli (5)`; остальные провайдеры отклоняют модификатор при построении SQL. См.
[Сэмплирование таблицы](../01-querying-and-projections.md#сэмплирование-таблицы-tablesample).

## Блокировка строк

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) и
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) генерируют завершающее предложение блокировки
строк, которое ставится после `WHERE` и `ORDER BY`
([`Lock`](xref:NextORM.Core.ISqlDialect.Lock),
`ILockRenderer.Render`):

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > 5)
    .ForUpdate()
    .ToList();
```

```sql
select id from simple_entity where (id > 5) for update
```

`LockMode.Update` генерирует `for update`, а `LockMode.Share` — `for share`. Оба принимают
[`LockWaitMode`](xref:NextORM.Core.LockWaitMode): `ForUpdate(LockWaitMode.SkipLocked)` генерирует
`for update skip locked`, а `ForShare(LockWaitMode.NoWait)` — `for share nowait` (PostgreSQL 9.5+). См.
[Блокировку строк](../01-querying-and-projections.md#блокировка-строк-for-update--for-share).

## Модифицирующие CTE

PostgreSQL — единственный провайдер, принимающий модифицирующую инструкцию как тело CTE
(`WITH <имя> AS (INSERT ... RETURNING ...)`); гейтится
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). Начните scope с
перегрузки `With(имя, insert)`: она возвращает [`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1),
типизированный по проекции `RETURNING`, а `From(имя)` читает эти строки полным набором операторов:

```csharp
var rows = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("ins")
    .Where(r => r.Total > 0)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
with ins as (insert into orders (customer_id) values (@p0) returning id, total) select id from ins as "t1"
 where (t1.total > 0)
```

Тело может быть `VALUES`-insert или `INSERT ... SELECT`, а мутация может читать более ранний read-CTE
(объявите его первым и используйте `CteQuery.With(имя, insert)`) либо питать главный `INSERT ... SELECT`.
Полный набор форм — в разделе
[Изменение данных (INSERT): Модифицирующий CTE](../19-insert-statement.md#модифицирующий-cte-postgresql);
общие (read) CTE — в [Общих табличных выражениях](../09-cte.md). Остальные провайдеры отклоняют
`With(имя, insert)` на этапе построения SQL с `NotSupportedException`.

## Динамическая схема записи

`jsonb_to_record`/`jsonb_to_recordset` доступны как табличные функции, схема результата которых
объявляется типом строки вызывающего и рендерится списком определений колонок в псевдониме
(`AS x(a int, b text)`). См. [Динамическая схема результата](../13-table-valued-functions.md#dynamic-result-schema).
Варианты `json_populate_record(set)` (заполняющие переданную вызывающим базовую запись, а не
свободный список колонок) остаются вне области охвата. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер PostgreSQL](../../providers/postgres.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs`.
