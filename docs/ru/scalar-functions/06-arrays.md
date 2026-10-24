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
select id
 from complex_entity where (id = any(@p0))
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
select id
 from complex_entity where id = any(@norm_p0)
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
select cardinality(@norm_p1) as "N"
 from complex_entity where array_length(@norm_p0, 1) = 3
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

### Поставленные функции массивов, кортежей и Map (#286, T286b)

Члены ниже дополняют каталог массивов на закреплённом сервере 25.8.33.6. Формы результатов объявлены
явно: поэлементные предикаты возвращают `bool`, агрегаты — объявленный скалярный/массивный CLR-тип, а
семейства sort/split/fill — `T[]`.

| Функция | SQL |
|---|---|
| `SqlFunctions.ClickHouse.array_element(a, i)` | `arrayElement(a, i)` |
| `SqlFunctions.ClickHouse.array_enumerate_dense(a)` | `arrayEnumerateDense(a)` |
| `SqlFunctions.ClickHouse.array_zip(a, b)` | `arrayZip(a, b)` |
| `SqlFunctions.ClickHouse.array_difference(a)` | `arrayDifference(a)` |
| `SqlFunctions.ClickHouse.array_avg(a)` | `arrayAvg(a)` |
| `SqlFunctions.ClickHouse.array_sum(a)` | `arraySum(a)` |
| `SqlFunctions.ClickHouse.array_min(a)` | `arrayMin(a)` |
| `SqlFunctions.ClickHouse.array_max(a)` | `arrayMax(a)` |
| `SqlFunctions.ClickHouse.array_product(a)` | `arrayProduct(a)` |
| `SqlFunctions.ClickHouse.array_top_k(a, k)` | `arrayTopK(a, k)` (версия: 26.6) |
| `SqlFunctions.ClickHouse.array_bottom_k(a, k)` | `arrayBottomK(a, k)` (версия: 26.6) |
| `SqlFunctions.ClickHouse.array_random_sample(a, n)` | `arrayRandomSample(a, n)` |
| `SqlFunctions.ClickHouse.array_compact(a)` | `arrayCompact(a)` |
| `SqlFunctions.ClickHouse.array_pop_back(a)` | `arrayPopBack(a)` |
| `SqlFunctions.ClickHouse.array_pop_front(a)` | `arrayPopFront(a)` |
| `SqlFunctions.ClickHouse.array_push_front(a, element)` | `arrayPushFront(a, element)` |
| `SqlFunctions.ClickHouse.array_resize(a, size[, extender])` | `arrayResize(a, size[, extender])` |
| `SqlFunctions.ClickHouse.array_remove(a, f)` | `arrayRemove(a, f)` (версия: 25.11) |
| `SqlFunctions.ClickHouse.array_rotate_left(a, n)` / `array_rotate_right(a, n)` | `arrayRotateLeft` / `arrayRotateRight` |
| `SqlFunctions.ClickHouse.array_shift_left(a, n)` / `array_shift_right(a, n)` | `arrayShiftLeft` / `arrayShiftRight` |
| `SqlFunctions.ClickHouse.array_fold(f, a, init)` | `arrayFold(f, a, init)` |
| `SqlFunctions.ClickHouse.array_fill(f, a)` / `array_reverse_fill(f, a)` | `arrayFill` / `arrayReverseFill` |
| `SqlFunctions.ClickHouse.array_split(f, a)` / `array_reverse_split(f, a)` | `arraySplit` / `arrayReverseSplit` |
| `SqlFunctions.ClickHouse.array_reverse_sort(a)` / `array_partial_sort(a, k)` | `arrayReverseSort` / `arrayPartialSort` |
| `SqlFunctions.ClickHouse.array_sort(a)` | `arraySort(a)` |
| `SqlFunctions.ClickHouse.array_shingles(a, n)` | `arrayShingles(a, n)` |
| `SqlFunctions.ClickHouse.array_transpose(a)` | `arrayTranspose(a)` (версия: 26.4) |
| `SqlFunctions.ClickHouse.array_cum_sum_non_negative(a)` | `arrayCumSumNonNegative(a)` |
| `SqlFunctions.ClickHouse.count_equal(a, b)` | `countEqual(a, b)` |
| `SqlFunctions.ClickHouse.array_with_constant(n, value)` | `arrayWithConstant(n, value)` |
| `SqlFunctions.ClickHouse.array_jaccard_index(a, b)` / `array_similarity(a, b)` | `arrayJaccardIndex` / `arraySimilarity` |
| `SqlFunctions.ClickHouse.tuple_concat(a, b)` | `tupleConcat(a, b)` |
| `SqlFunctions.ClickHouse.tuple_names(t)` | `tupleNames(t)` |
| `SqlFunctions.ClickHouse.flatten_tuple(t)` | `flattenTuple(t)` |
| `SqlFunctions.ClickHouse.tuple_to_name_value_pairs(t)` | `tupleToNameValuePairs(t)` |
| `SqlFunctions.ClickHouse.tuple_plus` / `tuple_minus` / `tuple_multiply` / `tuple_divide` | `tuplePlus` / `tupleMinus` / `tupleMultiply` / `tupleDivide` |
| `SqlFunctions.ClickHouse.tuple_hamming_distance(a, b)` | `tupleHammingDistance(a, b)` |
| `SqlFunctions.ClickHouse.dot_product(a, b)` | `dotProduct(a, b)` |
| `SqlFunctions.ClickHouse.map_subtract(a, b)` | `mapSubtract(a, b)` |
| `SqlFunctions.ClickHouse.map_update(a, b)` | `mapUpdate(a, b)` |
| `SqlFunctions.ClickHouse.map_reverse_sort(m[, f])` / `map_partial_sort(m, k[, f])` | `mapReverseSort` / `mapPartialSort` |
| `SqlFunctions.ClickHouse.map_from_arrays(keys, values)` | `mapFromArrays(keys, values)` |
| `SqlFunctions.ClickHouse.map_populate_series(m, start, end[, step])` | `mapPopulateSeries(...)` |
| `SqlFunctions.ClickHouse.extract_key_value_pairs(s)` | `extractKeyValuePairs(s)` |

`arrayReduce`/`arrayReduceInRanges` остаются отклонёнными: агрегат задаётся строкой SQL во время
выполнения, поэтому типизированный контракт невозможен (см.
[ClickHouse-specific SQL › Пока не поддерживается](../guide/provider-specific-sql/clickhouse.md#пока-не-поддерживается)).
`untuple` отклонён по той же структурной причине: одно выражение проекции отображается ровно в одну
колонку результата.

> `length`/`indexOf` нативно возвращают `UInt64`, поэтому диалект оборачивает их в `toInt64(...)`.
> Функции, возвращающие массив (`split_by_char`, `array_sort`, `array_reverse`, `array_distinct`,
> `range`, `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `group_array`,
> `group_uniq_array`), можно проецировать напрямую — row reader материализует результат `Array(T)`
> как CLR `T[]` — либо использовать как операнд другой array-функции (например, `length(...)` или
> `array_string_concat(...)`). Функции, у которых объявленный CLR-тип массива может отличаться от типа
> элемента, выводимого ClickHouse (`top_k`, `top_k_weighted`, `group_array`, `group_uniq_array`,
> `retention`, `array_map` и три перегрузки `range`), нормализуются **на сервере**: диалект оборачивает
> готовый вызов ровно в один `cast(... as Array(...))`, построенный из закрытого объявленного CLR-типа
> (`int[]` → `Array(Int32)`, `int?[]` → `Array(Nullable(Int32))`, `long[]` → `Array(Int64)`,
> `string[]`/`string?[]` → `Array(Nullable(String))`). Элемент ссылочного типа объявляется `Nullable(...)`
> всегда, потому что его nullable-аннотация CLR стирается в рантайме, поэтому приведение на стороне
> вызывающего кода не требуется, а
> `retention` материализуется как `int[]`, а не `byte[]`. Тот же reader материализует нативную колонку `Tuple(...)` (или
> выражение типа `Tuple(...)`) как `System.Tuple<...>` арности 1–7.

> `SqlFunctions.ClickHouse.array_except` остаётся для совместимости API, но **не поддерживается**
> на закреплённой базовой версии 25.8: нативная `arrayExcept` существует только с ClickHouse 25.9,
> поэтому вызов отклоняется с `NotSupportedException` на этапе трансляции и SQL не генерируется.

> Предикаты отношения массивов возвращают `bool`: `starts_with(array, prefix)`/`ends_with(array, suffix)`
> проверяют префикс/суффикс, а `has_substr(array, other)` — что `other` входит в `array` непрерывно и
> по порядку (пустой `other` содержится всегда). Требуется провайдер, поддерживающий array-функции.

> Поверхность row-значений (кортежей) кросс-провайдерная и строится на `System.Tuple.Create` /
> `new Tuple<...>` / `System.Tuple<...>.ItemN`: конструктор рендерится через row-конструктор диалекта —
> `tuple(a, b)` в ClickHouse, `ROW(a, b)` в PostgreSQL и плоский ANSI `(a, b)` в MySQL/MariaDB/SQLite — а
> доступ к элементу *серверного* row рендерится через позиционный доступ диалекта
> (`tupleElement(pair, 1)` в ClickHouse, `(pair).f1` в PostgreSQL). В MySQL/MariaDB/SQLite серверного
> доступа к элементу нет, поэтому `System.Tuple<...>.ItemN` на серверном row бросает
> `NotSupportedException`. Доступ к элементу *inline*-конструктора (`Tuple.Create(a, b).Item1`,
> `new ValueTuple<...>(a, b).Item2`) сворачивается в сам аргумент и работает на любом диалекте,
> умеющем выразить конструктор. `System.Tuple<,> ==` (в C# — ссылочное равенство) переинтерпретируется
> как SQL-сравнение row-значений (`Tuple.Create(x.A, x.B) == Tuple.Create(1, 'a')` →
> `ROW(a, b) = ROW(1, 'a')`); `ValueTuple` `==` в дереве выражений недостижим. В PostgreSQL и ClickHouse
> поверхность — это нативный row-тип ([`ITupleRenderer`](xref:NextORM.Core.ITupleRenderer) /
> [`ISqlDialect.Tuple`](xref:NextORM.Core.ISqlDialect.Tuple)); в MySQL/MariaDB/SQLite плоский конструктор
> `(a, b)` принимается только как **прямой операнд сравнения** в `WHERE`/`HAVING`/`JOIN ON` — в
> `Select`/`ORDER BY`/`GROUP BY` или как аргумент функции он бросает `NotSupportedException` при
> подготовке. SQL Server и провайдер in-memory отклоняют эту поверхность row-конструктора/сравнения
> строк; при этом in-memory вычисляет список значений tuple `IN`/`Contains` in-process (не отклоняет).
> Tuple `IN`/`Contains` по
> списку значений плоских `System.Tuple`/`System.ValueTuple` арности 1..7 — реализован
> ([#193](https://github.com/AlexeyShirshov/nextorm/issues/193)); точные формы провайдеров и семантика
> описаны в блоке tuple `IN` ниже.
> Материализация «сырого» `ROW(...)` — реализована в PostgreSQL
> ([#194](https://github.com/AlexeyShirshov/nextorm/issues/194)): одна колонка-запись `ROW(...)`
> (арность 1..7) или зарегистрированный вызывающим именованный composite материализуется в
> соответствующий `System.Tuple<...>`/именованный тип (см.
> [Сырой SQL](../guide/14-raw-sql.md#сырые-postgresql-строки-и-composite-типы)); `ValueTuple`,
> арность ≥8, вложенный/пустой `ROW`, несколько или смешанные колонки-записи, незарегистрированный
> именованный composite и composite, объявленный как `System.Tuple`, остаются под гардом
> `NotSupportedException`. `untuple` не поддерживается, так как меняет набор колонок результата,
> а не даёт скаляр.

> Tuple `IN`/`Contains` транслирует список значений плоских `System.Tuple<...>`/`System.ValueTuple<...>`
> арности **1..7** (`SqlFunctions.Sql.@in`, `SqlFunctions.ClickHouse.global_in` или
> `Enumerable.Contains`) в проверку принадлежности строки, построенную на row-конструкторе диалекта, а
> компоненты идут отдельными параметрами по порядку (никогда не один непрозрачный параметр-кортеж).
> Точные формы провайдеров:
>
> | Провайдер | SQL tuple `IN` |
> |---|---|
> | SQLite | `(a, b) IN (VALUES (@p0, @p1), (@p2, @p3))` |
> | MySQL / MariaDB | `(a, b) IN ((@p0, @p1), (@p2, @p3))` |
> | PostgreSQL | `ROW(a, b) IN (ROW(@p0, @p1), ROW(@p2, @p3))` |
> | ClickHouse | `tuple(a, b) IN (tuple(@p0, @p1), tuple(@p2, @p3))` (`global_in` рендерит `GLOBAL IN`) |
> | SQL Server | не поддерживается — **любая** форма кортежа бросает `NotSupportedException` с `SQL Server does not support tuple IN/Contains translation.`, до вычисления коллекции (null-коллекция, null-элемент ссылочного кортежа, арность ≥8 и вложенный кортеж дают именно это сообщение, а не ошибку вычисления) |
> | In-memory | вычисляется in-process (SQL не рендерится; `Enumerable.Contains` выполняется по строкам in-memory) |
>
> Пустая (не-null) коллекция рендерится как `1 = 0`. На провайдерах, поддерживающих эту поверхность,
> null-коллекция бросает `ArgumentNullException`, `default`-кортеж значения — обычная строка, а
> **nullable-элемент кортежа** (`List<(int, int)?>`) бросает `NotSupportedException`. Принадлежность
> двузначна, поэтому null-компонент совпадает с другим null-компонентом: строки, у которых все ячейки
> не-null, используют нативный список строк провайдера с гардом `IS NOT NULL` на каждом
> nullable-компоненте слева, а строка, содержащая null, рендерится явной OR-of-AND-ветвью
> (`c1 IS NULL` / `c1 IS NOT NULL AND c1 = @p`), потому что обычный row `IN` никогда не совпадает с SQL
> `NULL`. Null-элемент ссылочного кортежа (`null` внутри коллекции) бросает `NotSupportedException`, а не
> вырождается в строку из одних null; арность ≥8, форма `Rest` и вложенные компоненты-кортежи бросают
> `NotSupportedException`; на SQL Server всё это вместо этого даёт отклонение провайдера выше. Отложено
> в текущем milestone: tuple-типизированный `QueryCommand` (подзапрос) в правой части и
> `Rest`/вложенные кортежи — **пока не поддерживаются**; обещания будущего milestone нет.

> Функции высшего порядка (lambda) принимают inline-лямбду C#, параметр которой — элемент массива;
> например `array_map(v => -v, e.Nums)` рендерится как `arrayMap(v -> -(v), nums)`.
> `array_exists`/`array_all` возвращают `bool`; `array_count`/`array_first_index`/`array_last_index` —
> `long` (диалект оборачивает нативный `UInt32` в `toInt64(...)`); `array_first`/`array_last`
> возвращают элемент или его значение по умолчанию при отсутствии совпадения. Требуется провайдер,
> поддерживающий higher-order array-функции (см.
> [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
> ClickHouse). ClickHouse повышает тип арифметического результата независимо от C# (элемент `Int32`,
> умноженный на целочисленный литерал, даёт `Array(Int64)`); диалект нормализует готовый результат
> `arrayMap` к закрытому объявленному CLR-типу элемента, поэтому операнд лямбды остаётся ровно таким,
> как написан, и приведение вида `(long)v` внутри лямбды не требуется (`arrayFilter` и так возвращает
> тип элемента входного массива).

> **Nullable-элементы в связанном параметре-массиве:** CLR-массив ранга 1, чей **объявленный** тип элемента
> nullable (`Nullable<T>` или `string`), связывается как `Array(Nullable(T))`, выведенный из этого
> объявленного CLR-типа (`int?[]` → `Array(Nullable(Int32))`, `double?[]` → `Array(Nullable(Float64))`,
> `DateTime?[]` → `Array(Nullable(DateTime))`, `decimal?[]` → `Array(Nullable(Decimal(38,10)))`, `string?[]` →
> `Array(Nullable(String))`), поэтому `null`-элементы и их позиции доходят до сервера —
> `array_map(v => v, new int?[] { 1, null, 3 })` даёт `{ 1, null, 3 }`. Пустые массивы и массивы из одних
> `null` несут тот же типизированный элемент, а массив вызывающего не мутируется. Массивы с
> non-nullable-элементом (`int[]`, `double[]`, `DateTime[]`, `decimal[]`, `byte[]`) не меняются;
> nested/jagged и многомерные массивы, а также `null`-объект массива **не поддерживаются** и сохраняют
> прежнее (выведенное драйвером) поведение. Массивы-**результаты** подчиняются тому же правилу
> элемента: результат с элементом **ссылочного типа** объявляется `Nullable(...)` безусловно
> (результат `string[]`/`string?[]` рендерится как
> `Array(Nullable(String))`, jagged — как `Array(Array(Nullable(String)))`) и по-прежнему материализуется
> как CLR `string[]` — меняется только текст генерируемого SQL, но не данные, не CLR-тип и не публичный
> API, а элементы значимых типов (`int[]`, `int?[]`, `byte[]`) не затронуты. Decimal-массивы
> **результата** материализуются в обеих формах с [#337](https://github.com/AlexeyShirshov/nextorm/issues/337):
> `Array(Nullable(Decimal(38,10)))` → `decimal?[]` и `Array(Decimal(38,10))` → `decimal[]` — через
> прямые, member-bound, многоколоночные и raw-маршруты чтения, причём каждое представимое значение
> сохраняется точно (без округления, усечения и culture-преобразования). Элемент `Decimal(38,10)`,
> мантисса которого при масштабе 10 превышает 96 бит (например `1e19`: мантисса `1e29` при пределе
> CLR `decimal` `2^96 − 1`), в CLR `decimal` **не** представим: материализация падает fail-closed
> с ошибкой драйвера `OverflowException` (`Value cannot be represented as System.Decimal`), а поскольку
> закреплённый драйвер не отбрасывает хвостовые нули, математически меньшая запись не подставляется.
> Оставшиеся ограничения массивов перечислены в
> [Ограничениях](../advanced/limitations.md#ограничения).

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
select id, arrayJoin(tags) as `Tag`
 from array_entity where id = 1
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
select id
 from array_entity
 left array join tags
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
select id, __nextorm_aj_element as `Tag`
 from array_entity
 array join tags as __nextorm_aj_element
where __nextorm_aj_element = 'b'
```

Привязка элемента поддерживается только для одного источника без join'ов; `Where`/`Having` нужно
применять после неё (их параметр — проекция array join). Если нужны несколько массивов или join —
используйте `ArrayJoin`/`LeftArrayJoin` со скалярным `array_join`.
