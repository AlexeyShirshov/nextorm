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

## Пока не поддерживается

`jsonb_to_record`/`json_populate_record` требуют динамической схемы записи и не входят в текущую
поверхность. См. [Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер PostgreSQL](../../providers/postgres.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs`.
