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

[`LimitBy`](xref:NextORM.Core.EntityBuilder`1.LimitBy``1(System.Int32,System.Int32,System.Linq.Expressions.Expression{System.Func{`0,``0}})) возвращает первые `n` строк **на каждое значение
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

[`WithTotals`](xref:NextORM.Core.EntityBuilder`1.WithTotals) добавляет к группировке модификатор ClickHouse
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
запросах. Для `SEMI`/`ANTI`/`PASTE` есть отдельные построители: `SemiJoin`/`AntiJoin` отдают только
левые колонки для левых строк, у которых есть (соответственно нет) совпадение, а `PasteJoin`
сопоставляет два источника по позиции строки без `ON`
([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/`SupportsPasteJoin`).

## `GLOBAL IN`

[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})) рендерит распределённый
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
* параметрические `quantile(level)(value)`/`quantile_exact`/`quantile_timing` и `median` (`toFloat64`), многоуровневые `quantiles(level1, level2, ...)(value)` (возвращают `Array(Float64)`, материализуются как CLR `double[]`; уровни должны быть inline-массивом);
* агрегаты наиболее частых значений `topK(k)(value)`/`topKWeighted(k)(value, weight)` (возвращают `Array(T)`, материализуются как CLR `T[]`);
* агрегат последнего произвольного значения `any_last` (`anyLast`);
* возвращающие массивы агрегаты `group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`, материализуются как CLR `T[]`; `groupArray` над array-колонкой даёт вложенный `T[][]`);
* агрегаты последовательностей/воронки `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` с `toInt32(...)`; `retention` возвращает массив, проецируется напрямую);
* общий API фильтрации агрегатов, рендерящий комбинаторы `-If`: `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
* `arg_min`/`arg_max`;
* битмап-агрегаты `group_bitmap`/`group_bitmap_and`/`group_bitmap_or`/`group_bitmap_xor`
  (`groupBitmap`/`groupBitmapAnd`/`groupBitmapOr`/`groupBitmapXor`; непрозрачное состояние `UInt64`) и
  агрегаты по ключам `sum_map`/`sum_map_filtered` (`sumMap`/`sumMapFiltered(keys)(key, value)`,
  проецируется как `Map`).

Переносимые агрегаты — в том числе `count`, произвольное значение `any_agg` (рендерится `ANY_VALUE` в
MySQL и MariaDB) и `corr`/`covar*` — документированы на тематической странице. Обычная колонка `UInt64`
(или любая проекция `ulong`/`ulong?`) материализуется напрямую через аксессор построителя строк
`DbDataReader.GetFieldValue<ulong>`, поэтому нормализующее приведение нужно только результатам функций
выше.

См. [Группировка и агрегаты](../04-grouping-and-aggregates.md).

## Нативные скалярные функции, Map и хэши

Помимо кросс-провайдерной скалярной поверхности, `ClickHouseFunctions` предоставляет нативные функции
ClickHouse, у которых нет переносимого написания. Они рендерят точное camel-case-имя и отклоняются
всеми остальными диалектами:

* UTF-8-регистр, trim и regexp/поиск по строкам: `lower_utf8`/`upper_utf8` (`lowerUTF8`/`upperUTF8`),
  `trim_left`/`trim_right`/`trim_both` (`trimLeft`/`trimRight`/`trimBoth`, с необязательным набором
  символов), `replace_regexp_one`/`replace_regexp_all`, `match`, `extract`, `extract_all` (проецируется
  как `string[]`) и `split_by_string`/`split_by_regexp`/`split_by_whitespace`;
* дата и время: `format_date_time` (`formatDateTime`, необязательный часовой пояс),
  `parse_date_time`/`parse_date_time_best_effort` (`parseDateTime`/`parseDateTimeBestEffort`) и
  текущая дата `now`/`today`/`yesterday`;
* операции над множествами массивов: `array_concat`, `array_flatten`, `array_uniq` (`arrayUniq`, в
  обёртке `toInt64`), `array_intersect`, `array_union`, `array_except`, `array_symmetric_difference`;
* семейство `Map`: конструктор `map` (inline-пары `Tuple.Create`), `map_keys`/`map_values`,
  `map_contains_key`/`map_contains_value`, `map_add`/`map_concat`, двухпараметрические лямбды
  `map_filter`/`map_apply`/`map_all`/`map_exists` и `map_sort`;
* хэши: `md5`/`sha1`/`sha256`/`sha512` (`MD5`/`SHA1`/`SHA256`/`SHA512`, fixed binary strings),
  `xx_hash32`/`xx_hash64`/`xxh3`, `city_hash64`, `sip_hash64`/`sip_hash128` и
  `murmur_hash2_32`/`murmur_hash2_64`/`murmur_hash3_32`/`murmur_hash3_64`/`murmur_hash3_128`;
* `generate_ulid` (`generateULID`).

Результат `Map` представлен как `IDictionary<TKey, TValue>`; при проецировании комбинируйте его с
`map_keys`/`map_values` или одним из предикатов `map_contains_*`, потому что построитель строк не
материализует «голую» колонку `IDictionary`.

```csharp
var rows = dataContext.From<Event>()
    .Select(e => new
    {
        HasMatch = SqlFunctions.ClickHouse.match(e.Name, "error"),
        Keys = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
        NewId = SqlFunctions.ClickHouse.generate_ulid()
    })
    .ToList();
```

См. [Скалярные функции](../11-scalar-functions.md).

## JSON, словари и функции массивов

* извлекатели строкового JSON `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/
  `JSONExtractBool`/`JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length` и плоский
  `visitParamExtract*`;
* возвращающие массивы `JSONExtractKeys`/`JSONExtractArrayRaw` (`json_extract_keys`/
  `json_extract_array_raw`, проецируются как `string[]`) и `JSONExtractKeysAndValues`
  (`json_extract_keys_and_values<T>`, проецируется как `Tuple<string, T>[]`; `T` — non-nullable
  ClickHouse-тип);
* JSONPath-скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (по строковому JSON);
* функции нативного JSON `json_all_paths`/`json_all_paths_with_types` (`JSONAllPaths`/
  `JSONAllPathsWithTypes`; первая проецируется как `string[]`, нативный `Map(String, String)` второй —
  как `Dictionary<string, string>`; обе принимают нативное значение `JSON`) и `to_json_string`
  (`toJSONString`);
* словарные функции `dict_get`/`dict_get_or_default`/`dict_has` и иерархические
  `dict_get_hierarchy`/`dict_get_children`/`dict_is_in` (нужен настроенный
  `CREATE DICTIONARY`);
* функции массивов `length`/`has`/`index_of`/`has_any`/`has_all`/`starts_with`/`ends_with`/
  `has_substr`/`array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`/
  `range`/`array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back`;
* конструктор кортежа и доступ к элементу: `System.Tuple.Create(a, b)` рендерится как `tuple(a, b)`,
  а `System.Tuple<...>.ItemN` — как `tupleElement(t, n)` (целый `Tuple(...)` проецируется как
  `System.Tuple<...>`); `untuple` не поддерживается.

См. [Скалярные функции](../11-scalar-functions.md) и [Поддержка JSON](../18-json.md)
([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)/[`SupportsDictionaries`](xref:NextORM.Core.ISqlDialect.SupportsDictionaries)).

## Табличные функции

`numbers`/`numbers_mt` и генераторы строк `zeros`/`zeros_mt` доступны через
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})). Колонка `number` типа `UInt64` приводится к
`Int64` внутри оборачивающего подзапроса, чтобы материализоваться в CLR `long`; `zeros`
материализуется сразу в `byte`:

```csharp
var rows = dataContext.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

См. [Табличные функции](../13-table-valued-functions.md).

## Пока не поддерживается

Нативный тип колонки `JSON` (его reader/type-mapping) и
распределённые табличные функции (`remote`, `cluster`, `s3`, `file`) вне области охвата. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер ClickHouse](../../providers/clickhouse.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
