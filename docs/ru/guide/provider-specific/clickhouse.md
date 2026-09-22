# Специфичный для ClickHouse SQL

> У ClickHouse самая крупная эксклюзивная поверхность в nextorm: нативные массивы и `ARRAY JOIN`,
> модификаторы `LIMIT n BY expr` и `GROUP BY ... WITH TOTALS`, модификаторы запроса
> `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`, строгость соединений и `GLOBAL JOIN`, распределённый
> предикат `GLOBAL IN`, собственные агрегаты, функции массивов, JSON и словарей, а также табличные
> функции `numbers`/`zeros`.

**Что нужно знать:** [Запросы и проекции](../01-querying-and-projections.md) · [Провайдер ClickHouse](../../providers/clickhouse.md)

## Массивы и `ARRAY JOIN`

У ClickHouse есть нативный тип `Array(T)`. Функции массивов работают с колонками-массивами или
вложенными выражениями, а клауза array-join разворачивает по одной строке на элемент:

```csharp
var rows = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Where(e => e.Id > 0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from array_entity left array join tags where id > 0
```

Скалярный `array_join` (одна строка на элемент, проекция) и методы-клаузы
`ArrayJoin`/`LeftArrayJoin`/`ArrayJoinElement`/`LeftArrayJoinElement` описаны в разделе
[Массивы (ClickHouse)](../11-scalar-functions.md#массивы-clickhouse); функции массивов гейтятся
`SupportsArrayFunctions`, клауза — `ArrayJoinClause`.

## `LIMIT n BY expr`

[`LimitBy`](xref:NextORM.Core.EntityBuilder`1) возвращает первые `n` строк **на каждое значение
ключа**, клауза рендерится после `ORDER BY` и до финального `LIMIT`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Id)
    .LimitBy(2, x => x.Int)
    .Select(x => new { x.Id, x.Int })
    .ToList();
```

```sql
select id, nullableint from complex_entity order by id limit 2 by nullableint
```

См. [Сортировка и постраничная выборка](../05-sorting-and-paging.md#limit-by-clickhouse)
([`LimitBy`](xref:NextORM.Core.ISqlDialect.LimitBy)).

## `GROUP BY ... WITH TOTALS`

[`WithTotals`](xref:NextORM.Core.EntityBuilder`1) добавляет к группировке модификатор ClickHouse
`with totals` — строку итогов по всему набору. Он ортогонален `ROLLUP`/`CUBE` и не сочетается с
`GROUPING SETS`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(x => x.Int)
    .Select(x => new { x.Int, Count = SqlFunctions.Sql.count() })
    .WithTotals()
    .ToList();
```

```sql
select nullableint, count(*) from complex_entity group by nullableint with totals
```

См. [Группировка и агрегаты](../04-grouping-and-aggregates.md#with-totals)
([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)). Официальный
`ClickHouse.Driver` возвращает блок итогов отдельно и не отдаёт его через `IDataReader`, поэтому
материализуются только строки групп.

## Модификаторы запроса: `FINAL`, `SAMPLE`, `PREWHERE`, `SETTINGS`

Четыре метода рендерят модификаторы уровня запроса ClickHouse: `Final()`, `Sample(ratio[, offset])`,
`PreWhere(predicate)` и `Settings(("key", "value"), ...)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Final()
    .PreWhere(c => c.Int > 0)
    .Settings(("max_threads", "2"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id from complex_entity final prewhere (nullableint > 0) settings max_threads = 2
```

См. [Хинты запросов](../17-query-hints.md#модификаторы-запроса-clickhouse)
([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings)).
`FINAL`/`PREWHERE` требуют движок таблицы, который их поддерживает, — движок `Memory` отклоняет оба.

## Строгость соединения и `GLOBAL JOIN`

Модификаторы соединения ClickHouse применяются к только что добавленному соединению:
`WithStrictness(JoinStrictness.Any | All | Asof)` и `Global()`:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Global()
    .WithStrictness(JoinStrictness.Any)
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring from simple_entity as `t1` global left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

См. [Соединения](../03-joins.md#специфичные-для-провайдера-модификаторы-соединения-clickhouse)
([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness)/[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)).
`ANY` оставляет одну правую строку на каждую левую, `ALL` — все совпадения, `ASOF` требует одну
колонку равенства и завершающее неравенство; `GLOBAL` рассылает правую сторону в распределённых
запросах. Соединения `SEMI`/`ANTI`/`PASTE` не поддерживаются.

## `GLOBAL IN`

[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions) рендерит распределённый
предикат по подзапросу или списку значений; отрицание через `!` даёт `GLOBAL NOT IN`:

```csharp
var values = new int[] { 1, 3 };
var ids = dataContext.From<ISimpleEntity>()
    .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, values))
    .Select(x => new { x.Id })
    .ToList();
```

```sql
select id from simple_entity where global in (@p0, @p1)
```

См. [Фильтрация](../02-filtering-where.md#global-in-clickhouse)
([`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates)).

## Агрегаты

Эксклюзивные агрегаты ClickHouse живут на [`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions)
и отклоняются всеми остальными диалектами. Их нативные типы результата (`UInt64`, `Float32`, целые
типы комбинаторов `-If`) не всегда совпадают с объявленным CLR-типом, поэтому диалект оборачивает их в
`toInt64(...)`/`toInt32(...)`/`toFloat64(...)`, нормализуя значение к типу, который объявляет метод:

* агрегаты числа уникальных `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` (`toInt64`);
* параметрические `quantile(level)(value)`/`quantile_exact`/`quantile_timing` и `median` (`toFloat64`);
* агрегат последнего произвольного значения `any_last` (`anyLast`);
* агрегаты последовательностей/воронки `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` с `toInt32(...)`; `retention` возвращает массив, применим только вложенно);
* комбинаторы `-If`: `count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`;
* `arg_min`/`arg_max`.

Переносимые агрегаты — в том числе `count`, произвольное значение `any_agg` (рендерится `ANY_VALUE` в
MySQL и MariaDB) и `corr`/`covar*` — документированы на тематической странице. Обычная колонка `UInt64`
(или любая проекция `ulong`/`ulong?`) материализуется напрямую через аксессор построителя строк
`DbDataReader.GetFieldValue<ulong>`, поэтому нормализующее приведение нужно только результатам функций
выше.

См. [Группировка и агрегаты](../04-grouping-and-aggregates.md).

## JSON, словари и функции массивов

* извлекатели строкового JSON `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/
  `JSONExtractBool`/`JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length` и плоский
  `visitParamExtract*`;
* JSONPath-скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (по строковому JSON);
* функции нативного JSON `json_all_paths`/`json_all_paths_with_types` (`JSONAllPaths`/
  `JSONAllPathsWithTypes`; первая проецируется как `string[]`, нативный `Map(String, String)` второй —
  как `Dictionary<string, string>`; обе принимают нативное значение `JSON`) и `to_json_string`
  (`toJSONString`);
* словарные функции `dict_get`/`dict_get_or_default`/`dict_has` (нужен настроенный
  `CREATE DICTIONARY`);
* функции массивов `length`/`has`/`index_of`/`has_any`/`has_all`/`array_string_concat`/
  `split_by_char`/`array_sort`/`array_reverse`/`array_distinct`/`range`/`array_enumerate`/
  `array_cum_sum`/`array_slice`/`array_push_back`.

См. [Скалярные функции](../11-scalar-functions.md) и [Поддержка JSON](../18-json.md)
([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)/[`SupportsDictionaries`](xref:NextORM.Core.ISqlDialect.SupportsDictionaries)).

## Табличные функции

`numbers`/`numbers_mt` и генераторы строк `zeros`/`zeros_mt` доступны через
[`FromTableFunction`](xref:NextORM.Core.EntityBuilder`1). Колонка `number` типа `UInt64` приводится к
`Int64` внутри оборачивающего подзапроса, чтобы материализоваться в CLR `long`; `zeros`
материализуется сразу в `byte`:

```csharp
var rows = dataContext.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

См. [Табличные функции](../13-table-valued-functions.md).

## Пока не поддерживается

Соединения `SEMI`/`ANTI`/`PASTE`, функции высшего порядка над массивами
(`arrayMap`/`arrayFilter`), row reader для массивов/кортежей, нативный тип колонки `JSON` (его
reader/type-mapping) и распределённые табличные функции (`remote`, `cluster`, `s3`, `file`) вне
области охвата. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер ClickHouse](../../providers/clickhouse.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
