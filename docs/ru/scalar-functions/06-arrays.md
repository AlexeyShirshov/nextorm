# Массивы (PostgreSQL)

В PostgreSQL есть встроенные типы-массивы. Массив всегда передаётся **одним параметром** (целиком), а
не разворачивается в список значений, поэтому текст SQL не зависит от количества элементов, и план
запроса остаётся кэшируемым. Массивом может быть runtime-параметр ([`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))),
захваченная локальная переменная/поле или встроенный `new[]`. Поверхность массивов умеет рендерить
только диалект, включивший [`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) (PostgreSQL); все остальные провайдеры бросают
`NotSupportedException`.

`SqlFunctions.Postgres.any` / `SqlFunctions.Postgres.all` принимают массив — либо как готовый предикат (`column = any(@array)`),
либо как правую часть сравнения:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.any(e.Id, ids))     // (id = any(@p0))
    .Select(e => new { e.Id })
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == SqlFunctions.Postgres.any(ids))   // id = any(@p0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (id = any(@p0))
```

Runtime-параметр-массив использует тот же механизм [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)), поэтому массив не нужно знать в момент
подготовки запроса:

```csharp
var prepared = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0)))
    .Select(e => new { e.Id })
    .Prepare();

var rows = prepared.ToList(new long[] { 1, 2, 3 });
```

```sql
select id from complex_entity where id = any(@norm_p0)
```

Функции и операторы для массивов отображаются в свои имена PostgreSQL:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.cardinality(a)` | `cardinality(a)` |
| `SqlFunctions.Postgres.array_length(a, dim)` | `array_length(a, dim)` |
| `SqlFunctions.Postgres.array_ndims(a)` | `array_ndims(a)` |
| `SqlFunctions.Postgres.array_lower(a, dim)` | `array_lower(a, dim)` |
| `SqlFunctions.Postgres.array_upper(a, dim)` | `array_upper(a, dim)` |
| `SqlFunctions.Postgres.array_position(a, element)` | `array_position(a, element)` |
| `SqlFunctions.Postgres.array_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.array_overlaps(a, b)` | `a && b` |
| `SqlFunctions.Postgres.array_contained_by(a, b)` | `a <@ b` |
| `SqlFunctions.Postgres.array_concat(a, b)` | `a \|\| b` |
| `SqlFunctions.Postgres.array_cat(a, b)` | `array_cat(a, b)` |
| `SqlFunctions.Postgres.array_append(a, element)` | `array_append(a, element)` |
| `SqlFunctions.Postgres.array_prepend(element, a)` | `array_prepend(element, a)` |
| `SqlFunctions.Postgres.array_remove(a, element)` | `array_remove(a, element)` |
| `SqlFunctions.Postgres.array_replace(a, from, to)` | `array_replace(a, from, to)` |
| `SqlFunctions.Postgres.array_fill(value, dims)` | `array_fill(value, dims)` |
| `SqlFunctions.Postgres.array_dims(a)` | `array_dims(a)` |
| `SqlFunctions.Postgres.array_positions(a, element)` | `array_positions(a, element)` |
| `SqlFunctions.Postgres.array_reverse(a)` | `array_reverse(a)` (PostgreSQL 18+) |
| `SqlFunctions.Postgres.array_sort(a)` | `array_sort(a)` (PostgreSQL 18+) |
| `SqlFunctions.Postgres.array_shuffle(a)` | `array_shuffle(a)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_sample(a, n)` | `array_sample(a, n)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_to_string(a, delimiter)` | `array_to_string(a, delimiter)` |
| `SqlFunctions.Postgres.string_to_array(s, delimiter)` | `string_to_array(s, delimiter)` |

> Функции, возвращающие массив (`array_append`, `array_cat`, `array_reverse`, `string_to_array`, ...),
> можно использовать внутри запроса (предикат, `having` или вложенное выражение) или проецировать
> напрямую: row reader материализует результат `Array(T)` как CLR `T[]`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.array_length(SqlFunctions.Parameter<long[]>(0), 1) == 3)
    .Select(e => new { N = SqlFunctions.Postgres.cardinality(SqlFunctions.Parameter<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N" from complex_entity where array_length(@norm_p0, 1) = 3
```

## Массивы (ClickHouse)

В ClickHouse есть нативный тип `Array(T)`. Функции массивов работают с array-**колонками** (или
вложенными array-выражениями) и включаются флагом `ISqlDialect.SupportsArrayFunctions`; для
`arrayJoin` дополнительно нужен `SupportsArrayJoin`. `arrayJoin(array)` разворачивает массив в одну
строку на элемент, поэтому его результат можно проецировать как скалярную колонку.

| Функция | SQL |
|---|---|
| `SqlFunctions.ClickHouse.length(a)` | `length(a)` |
| `SqlFunctions.ClickHouse.has(a, element)` | `has(a, element)` |
| `SqlFunctions.ClickHouse.index_of(a, element)` | `indexOf(a, element)` |
| `SqlFunctions.ClickHouse.has_any(a, b)` | `hasAny(a, b)` |
| `SqlFunctions.ClickHouse.has_all(a, b)` | `hasAll(a, b)` |
| `SqlFunctions.ClickHouse.starts_with(a, prefix)` | `startsWith(a, prefix)` |
| `SqlFunctions.ClickHouse.ends_with(a, suffix)` | `endsWith(a, suffix)` |
| `SqlFunctions.ClickHouse.has_substr(a, other)` | `hasSubstr(a, other)` |
| `SqlFunctions.ClickHouse.array_string_concat(a, delimiter)` | `arrayStringConcat(a, delimiter)` |
| `SqlFunctions.ClickHouse.split_by_char(separator, s)` | `splitByChar(separator, s)` |
| `SqlFunctions.ClickHouse.array_sort(a)` | `arraySort(a)` |
| `SqlFunctions.ClickHouse.array_reverse(a)` | `arrayReverse(a)` |
| `SqlFunctions.ClickHouse.array_distinct(a)` | `arrayDistinct(a)` |
| `SqlFunctions.ClickHouse.range(start, end)` | `range(start, end)` |
| `SqlFunctions.ClickHouse.array_enumerate(a)` | `arrayEnumerate(a)` |
| `SqlFunctions.ClickHouse.array_cum_sum(a)` | `arrayCumSum(a)` |
| `SqlFunctions.ClickHouse.array_slice(a, offset, length)` | `arraySlice(a, offset, length)` |
| `SqlFunctions.ClickHouse.array_push_back(a, element)` | `arrayPushBack(a, element)` |
| `SqlFunctions.ClickHouse.array_join(a)` | `arrayJoin(a)` |
| `SqlFunctions.ClickHouse.group_array(a)` | `groupArray(a)` |
| `SqlFunctions.ClickHouse.group_uniq_array(a)` | `groupUniqArray(a)` |
| `SqlFunctions.ClickHouse.array_map(f, a)` | `arrayMap(f, a)` |
| `SqlFunctions.ClickHouse.array_filter(f, a)` | `arrayFilter(f, a)` |
| `SqlFunctions.ClickHouse.array_exists(f, a)` | `arrayExists(f, a)` |
| `SqlFunctions.ClickHouse.array_all(f, a)` | `arrayAll(f, a)` |
| `SqlFunctions.ClickHouse.array_count(f, a)` | `arrayCount(f, a)` |
| `SqlFunctions.ClickHouse.array_first(f, a)` | `arrayFirst(f, a)` |
| `SqlFunctions.ClickHouse.array_first_index(f, a)` | `arrayFirstIndex(f, a)` |
| `SqlFunctions.ClickHouse.array_last(f, a)` | `arrayLast(f, a)` |
| `SqlFunctions.ClickHouse.array_last_index(f, a)` | `arrayLastIndex(f, a)` |

> `length`/`indexOf` нативно возвращают `UInt64`, поэтому диалект оборачивает их в `toInt64(...)`.
> Функции, возвращающие массив (`split_by_char`, `array_sort`, `array_reverse`, `array_distinct`,
> `range`, `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `group_array`,
> `group_uniq_array`), можно проецировать напрямую — row reader материализует результат `Array(T)`
> как CLR `T[]` — либо использовать как операнд другой array-функции (например, `length(...)` или
> `array_string_concat(...)`). Тот же reader материализует нативную колонку `Tuple(...)` (или
> выражение типа `Tuple(...)`) как `System.Tuple<...>` арности 1–7.

> Предикаты отношения массивов возвращают `bool`: `starts_with(array, prefix)`/`ends_with(array, suffix)`
> проверяют префикс/суффикс, а `has_substr(array, other)` — что `other` входит в `array` непрерывно и
> по порядку (пустой `other` содержится всегда). Требуется провайдер, поддерживающий array-функции.

> Поверхность row-значений (кортежей) кросс-провайдерная и строится на `System.Tuple.Create` /
> `new Tuple<...>` / `System.Tuple<...>.ItemN`: конструктор рендерится через row-конструктор диалекта —
> `tuple(a, b)` в ClickHouse, `ROW(a, b)` в PostgreSQL — а доступ к элементу *серверного* row рендерится
> через позиционный доступ диалекта (`tupleElement(pair, 1)` в ClickHouse, `(pair).f1` в PostgreSQL).
> Доступ к элементу *inline*-конструктора (`Tuple.Create(a, b).Item1`,
> `new ValueTuple<...>(a, b).Item2`) сворачивается в сам аргумент, поэтому работает на любом диалекте,
> умеющем выразить конструктор. `System.Tuple<,> ==` (в C# — ссылочное равенство) переинтерпретируется
> как SQL-сравнение row-значений (`Tuple.Create(x.A, x.B) == Tuple.Create(1, 'a')` →
> `ROW(a, b) = ROW(1, 'a')`); `ValueTuple` `==` в дереве выражений недостижим. Требуется провайдер с
> нативным row-типом (см. [`ITupleRenderer`](xref:NextORM.Core.ITupleRenderer) /
> [`ISqlDialect.Tuple`](xref:NextORM.Core.ISqlDialect.Tuple); PostgreSQL и ClickHouse); SQL Server,
> MySQL, MariaDB, SQLite и провайдер in-memory отклоняют эту поверхность, а tuple `IN`/`Contains` по
> списку значений пока не транслируется. `untuple` не поддерживается, так как меняет набор колонок
> результата, а не даёт скаляр.

> Функции высшего порядка (lambda) принимают inline-лямбду C#, параметр которой — элемент массива;
> например `array_map(v => -v, e.Nums)` рендерится как `arrayMap(v -> -(v), nums)`.
> `array_exists`/`array_all` возвращают `bool`; `array_count`/`array_first_index`/`array_last_index` —
> `long` (диалект оборачивает нативный `UInt32` в `toInt64(...)`); `array_first`/`array_last`
> возвращают элемент или его значение по умолчанию при отсутствии совпадения. Требуется провайдер,
> поддерживающий higher-order array-функции (см.
> [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
> ClickHouse). ClickHouse повышает тип арифметического результата независимо от C# (элемент `Int32`,
> умноженный на целочисленный литерал, даёт `Array(Int64)`), поэтому приводите тип внутри лямбды
> (`v => (long)v * 2`), если тип элемента должен совпадать с проецируемым `T[]`.

CLR-метод `string.Split` рендерится как `splitByChar(separator, value)` (гейт
[`StringSplit`](xref:NextORM.Core.ISqlDialect.StringSplit)); поддерживается только
одноразрядный разделитель (многосимвольный `splitByString` не выставлен), результат — `string[]`,
который можно проецировать напрямую или использовать внутри другой array-функции; overload с `count`, несколько разделителей и
`StringSplitOptions`, отличный от `None`, бросают `NotSupportedException`:

```csharp
var parts = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.ClickHouse.length(e.String!.Split(',')))
    .First();
```

```csharp
var tags = dataContext.From<IArrayEntity>()
    .Where(e => e.Id == 1)
    .Select(e => new { e.Id, Tag = SqlFunctions.ClickHouse.array_join(e.Tags) })
    .ToList();
```

```sql
select id, arrayJoin(tags) as `Tag` from array_entity where id = 1
```

`EntityBuilder.ArrayJoin`/`LeftArrayJoin` рендерят клаузу `[LEFT] ARRAY JOIN`, которая разворачивает
строки до `WHERE`/`GROUP BY`; `LEFT ARRAY JOIN` сохраняет строку с пустым массивом. Вырожденный элемент
не привязан к CLR-члену, поэтому для проецирования/фильтрации используйте скалярный `array_join` выше.

```csharp
var ids = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from array_entity left array join tags
```

`EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` добавляют ту же клаузу, но возвращают
`EntityBuilder<ArrayJoinProjection<TEntity, TElement>>`, поэтому доступны и исходная сущность
(`p.Item1`), и вырожденный элемент (`p.Element`). Выражение клаузы получает алиас, и `p.Element`
транслируется в этот алиас:

```csharp
var rows = dataContext.From<IArrayEntity>()
    .ArrayJoinElement(e => e.Tags)
    .Where(p => p.Element == "b")
    .Select(p => new { p.Item1.Id, Tag = p.Element })
    .ToList();
```

```sql
select id, __nextorm_aj_element as `Tag` from array_entity
array join tags as __nextorm_aj_element
where __nextorm_aj_element = 'b'
```

Привязка элемента поддерживается только для одного источника без join'ов; `Where`/`Having` нужно
применять после неё (их параметр — проекция array join). Если нужны несколько массивов или join —
используйте `ArrayJoin`/`LeftArrayJoin` со скалярным `array_join`.
