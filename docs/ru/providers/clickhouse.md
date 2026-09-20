# Провайдер ClickHouse

> Используйте `nextorm.clickhouse` для ClickHouse; он отрисовывает параметры `@name` (драйвер переписывает их в `{name:Type}`), идентификаторы в обратных кавычках, конкатенацию `concat(...)`, разбиение на страницы `limit`/`offset` и имена типов ClickHouse.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext) (`src/nextorm.clickhouse/ClickHouseDataContext.cs`) оборачивает официальный
ADO.NET-провайдер `ClickHouse.Driver`. Он создаёт `ClickHouseConnection` из строки подключения и
возвращает [`Instance`](xref:NextORM.ClickHouse.ClickHouseDialect.Instance) из свойства `Dialect`.

[`ClickHouseDialect`](xref:NextORM.ClickHouse.ClickHouseDialect) (`src/nextorm.clickhouse/ClickHouseDialect.cs`) отрисовывает:

- плейсхолдер параметра `@name`; драйвер переписывает их в нативный для ClickHouse вид
  `{name:Type}` и выводит тип из значения .NET;
- идентификаторы и псевдонимы в обратных кавычках;
- конкатенацию строк функцией `concat(a, b, ...)`;
- `coalesce(a, b)`, логические литералы `true`/`false` и `lengthUTF8(x)` для длины строки;
- `trimBoth`/`trimLeft`/`trimRight` для трёх видов trim;
- `now()` для локального времени и `now('UTC')` для UTC;
- `stdev`/`stdevp`/`var`/`varp` как `stddevSamp`/`stddevPop`/`varSamp`/`varPop`;
- `date_trunc(field, x)` как `dateTrunc('field', x)` (множественные ANSI-части субсекунд сворачиваются
  в единственные; `decade`/`century`/`millennium` бросают исключение), а `date_add`/`DateTime.Add*` —
  как выделенные функции `addYears`/`addQuarters`/…/`addSeconds` (`decade`/`century`/`millennium`
  сворачиваются в масштабированный `addYears`), `end_of_month` — как `toLastDayOfMonth(x)`;
- поверхность приведения/частей даты `SqlFunctions.ClickHouse.to_*` (гейт
  [`SupportsDateConversionFunctions`](xref:NextORM.Core.ISqlDialect.SupportsDateConversionFunctions)):
  `to_date`/`to_date_time`/`to_date32` — как `toDate`/`toDateTime`/`toDate32`; аксессоры `to_year`/
  `to_quarter`/`to_month`/`to_day_of_month`/`to_day_of_week`/`to_day_of_year`/`to_hour`/`to_minute`/
  `to_second` (а также проекции `DateTime.Year`/`Month`/…) — как `toYear`/`toQuarter`/…, обёрнутые в
  `toInt32(...)` (нативные `UInt8`/`UInt16` иначе не читаются построителем строк); `to_start_of_*` —
  как `toStartOfYear`/`toStartOfQuarter`/`toStartOfMonth`/`toStartOfWeek`/`toStartOfDay`/
  `toStartOfHour`/`toStartOfMinute`/`toStartOfSecond`; `to_monday` — как `toMonday` (ISO-понедельник,
  тогда как `toStartOfWeek` начинает неделю с воскресенья); `to_yyyymm`/`to_yyyymmdd` — как
  `toInt32(toYYYYMM(...))`/`toInt32(toYYYYMMDD(...))`; `to_unix_timestamp` — как
  `toInt64(toUnixTimestamp(...))`;
- `string_agg(x, delimiter)` как `arrayStringConcat(groupArray(x), delimiter)`;
- `bit_and`/`bit_or`/`bit_xor` как `groupBitAnd`/`groupBitOr`/`groupBitXor`, `covar_pop`/`covar_samp`
  как `covarPop`/`covarSamp`, `corr` как `corr`, `arg_min`/`arg_max` как `argMin`/`argMax`, а
  фильтрованные агрегаты `count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` — как комбинаторы `-If`
  `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`; агрегаты числа уникальных значений
  `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` — как `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12`,
  обёрнутые в `toInt64(...)` (нативный `UInt64` приводится, чтобы построитель строк мог
  материализовать целое CLR); агрегаты количества (`count`/`count_distinct`/`count_if` и
  `count_big`/`count_big_distinct`) приводятся так же — в `toInt32(...)` для возвращающих `int`
  вариантов и `toInt64(...)` для 64-битных; параметрические агрегаты квантилей `quantile(level)(value)`/`quantileExact`/
  `quantileTiming` и `median`, обёрнутые в `toFloat64(...)` (чтобы любой вариант материализовался как
  `double`); агрегат произвольного значения `any_agg` — как `any` (кросс-провайдерно: `ANY_VALUE(x)` в
  MySQL), а агрегат последней строки `any_last` — как `anyLast`; извлекающие функции
  строкового JSON `json_extract_string`/`json_extract_int`/`json_extract_float`/`json_extract_bool`/
  `json_extract_raw`/`json_has`/`json_type` — как `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/
  `JSONExtractBool`/`JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length` — как
  `toInt64(JSONLength(...))`, а быстрый разбор плоского JSON `visit_param_extract_string`/`_int`/`_float`/
  `_bool`/`_raw` — как `visitParamExtractString`/`visitParamExtractInt`/`visitParamExtractFloat`/
  `visitParamExtractBool`/`visitParamExtractRaw`; JSONPath-скаляры `json_value`/`json_query`/
  `json_exists` — как `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (тот же гейт [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract));
  функции словарей `dict_get`/`dict_get_or_default`/
  `dict_has` — как `dictGet`/`dictGetOrDefault`/`dictHas`;
- модификатор супер-агрегации `GROUP BY ... WITH TOTALS` ([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals))
  через `EntityBuilder.WithTotals()`;
- `LIMIT n BY expr` ([`SupportsLimitBy`](xref:NextORM.Core.ISqlDialect.SupportsLimitBy)) через
  `EntityBuilder.LimitBy(...)`: не более `n` строк на каждое значение ключа, эмитится после `ORDER BY`
  и перед финальным `LIMIT`;
- табличные функции `numbers`/`numbers_mt` через `SqlFunctions.ClickHouse.numbers(...)` (колонка `number`
  типа `UInt64` приводится к `Int64` подзапросом-обёрткой, чтобы row reader мог её материализовать) и
  `zeros`/`zeros_mt` через `SqlFunctions.ClickHouse.zeros(...)` (`IZerosRow`, колонка `zero UInt8`
  материализуется напрямую как `byte`);
- модификаторы запроса `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS` через `Final()`, `Sample(ratio[, offset])`,
  `PreWhere(predicate)` и `Settings(("key", "value"), ...)`
  ([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings));
  `FINAL`/`PREWHERE` требуют движка таблицы, который их поддерживает (движок `Memory` отклоняет оба).
- имена типов ClickHouse в приведениях (`Int32`, `Int64`, `Float64`, `Decimal(38, 10)`, …);
- разбиение на страницы `limit n` / `limit n offset m`; offset без limit превращается в
  `limit 18446744073709551615 offset m`, потому что ClickHouse принимает `offset` только вместе с `limit`;
- переносимый условный `iif` — как `if(condition, a, b)` ([`SupportsIif`](xref:NextORM.Core.ISqlDialect.SupportsIif),
  [`MakeIif`](xref:NextORM.Core.ISqlDialect.MakeIif)), а оконные функции `percent_rank()`/`cume_dist()` и
  `nth_value(expr, n)` поддерживаются ([`SupportsPercentRankCumeDist`](xref:NextORM.Core.ISqlDialect.SupportsPercentRankCumeDist),
  [`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue));
- распределённый предикат `GLOBAL IN` через
  [`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions) (по подзапросу или
  списку значений, [`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates));
  отрицание — через C# `!` (`GLOBAL NOT IN`);
- модификаторы строгости/типа join `ANY`/`ALL`/`ASOF` через
  [`EntityBuilder.WithStrictness`](xref:NextORM.Core.EntityBuilder`1) сразу после join
  ([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
  [`MakeJoinKeyword`](xref:NextORM.Core.ISqlDialect.MakeJoinKeyword), enum `JoinStrictness`).
  `LEFT ANY JOIN` оставляет одну правую строку на каждую левую, `ALL` — все совпадения, а `ASOF`
  требует хотя бы одной equi-колонки и неравенства последним. `SEMI`/`ANTI`/`PASTE` не поддерживаются.
  Вариант `GLOBAL` (правая сторона разрешается один раз и broadcast'ится для распределённых
  запросов) задаётся через [`EntityBuilder.Global`](xref:NextORM.Core.EntityBuilder`1) и
  комбинируется со strictness (`global left any join`,
  [`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)).

ClickHouse не поддерживает рекурсивные CTE, поэтому диалект объявляет каждый CTE просто через `with`.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.clickhouse/DI/ClickHouseDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.ClickHouse;

var builder = new DataContextBuilder()
    .UseClickHouse("Host=localhost;Port=8123;Username=default;Password=secret;Database=app");

using var ctx = builder.CreateDataContext();   // IDataContext
```

Также можно создать контекст напрямую:

```csharp
using NextORM.Core;
using NextORM.ClickHouse;

using IDataContext ctx = new ClickHouseDataContext(
    "Host=localhost;Username=default;Database=app", new DataContextBuilder());
```

## Конкатенация строк

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

```sql
select concat('id:', id) as `Label` from simple_entity
```

## Различия провайдера

| Аспект | ClickHouse |
|---|---|
| Плейсхолдер параметра | `@name` (драйвер переписывает в `{name:Type}`) |
| Конкатенация | `concat(a, b)` |
| Coalesce | `coalesce` |
| Логический литерал | `true` / `false` |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| Псевдоним TVF | требуется |
| `*ALL` | поддерживается |
| Рекурсивный CTE | не поддерживается (модификатор `recursive` опускается) |
| `date_trunc` | `dateTrunc('field', x)` |
| Арифметика дат | `addDays(x, n)` … `addYears(x, (n) * 10)`; `toLastDayOfMonth(x)` |
| Приведение / части даты | `toDate`/`toDateTime`/`toDate32`, `toYear`/… (как `toInt32(...)`), `toStartOf*`, `toMonday`, `toInt32(toYYYYMM(...))`/`toInt32(toYYYYMMDD(...))`, `toInt64(toUnixTimestamp(...))` |
| `string_agg` | `arrayStringConcat(groupArray(x), delimiter)` (без `array_agg`) |
| Битовые / статистические агрегаты | `groupBitAnd`/`groupBitOr`/`groupBitXor`; `corr`/`covarPop`/`covarSamp` |
| Агрегаты регрессии | не поддерживаются (`regr_*` — только PostgreSQL) |
| Логические агрегаты | не поддерживаются (`bool_and`/`bool_or`/`every` — только PostgreSQL) |
| Фильтрованный агрегат | `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf` (без ANSI `filter (where ...)`) |
| ArgMin / ArgMax | `argMin`/`argMax` |
| quantile / median | `quantile(0.5)(x)`, `quantileExact(0.9)(x)`, `quantileTiming(0.5)(x)`, `median(x)` (как `toFloat64(...)`) |
| any_agg (произвольное значение) | `any(x)` (кросс-провайдерно; `ANY_VALUE(x)` в MySQL) |
| any_last (последняя строка) | `anyLast(x)` |
| Условная функция | `iif(cond, a, b)` → `if(cond, a, b)` |
| Оконные функции | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` поддерживаются |
| Строковый JSON | `JSONExtractString`, `JSONExtractInt`, `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONHas`, `toInt64(JSONLength(...))`, `JSONType`, `visitParamExtract*`, `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (JSONPath) |
| Словари | `dictGet`, `dictGetOrDefault`, `dictHas` (нужен сконфигурированный `CREATE DICTIONARY`) |
| Session/info-функции | `currentUser()`, `currentDatabase()`, `version()` (`session_user`/`current_schema` недоступны) |
| `GROUP BY ... WITH TOTALS` | `with totals` (отдельная строка итогов не отдаётся `ClickHouse.Driver`) |
| `LIMIT n BY expr` | `limit [offset, ]n by col1, col2` (перед финальным `LIMIT`) |
| Модификаторы запроса | `final`, `sample r [offset o]`, `prewhere`, `settings k = v` (`FINAL`/`PREWHERE` требуют поддерживающего движка таблицы) |
| Табличные функции | `numbers`/`numbers_mt` (колонка `UInt64 number` приводится к `Int64`), `zeros`/`zeros_mt` (`zero UInt8`), `generateRandom` (встроенные `generate_random()`/`generate_random(seed)` фиксируют структуру `id UInt64, value Float64, name String` и приводят `id` к `Int64`) |
| Функции массивов | над колонками `Array(T)`: `length`, `has`, `indexOf`, `hasAny`, `hasAll`, `arrayStringConcat`, `splitByChar`, `arraySort`, `arrayReverse`, `arrayDistinct`; CLR-метод `string.Split` рендерится как `splitByChar(separator, value)` под [`SupportsStringSplit`](xref:NextORM.Core.ISqlDialect.SupportsStringSplit) (только одноразрядный разделитель); `arrayJoin(array)` разворачивает по строке на элемент, а `EntityBuilder.ArrayJoin`/`LeftArrayJoin` рендерят клаузу `[left ]array join expr, ...`. `EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` дополнительно привязывают вырожденный элемент к `ArrayJoinProjection<TEntity, TElement>.Element` (исходная сущность — в `.Item1`); выражение клаузы получает алиас, и `p.Element` ссылается на него (см. [`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions), [`ArrayJoinKind`](xref:NextORM.Core.ArrayJoinKind), [`ArrayJoinProjection`](xref:NextORM.Core.ArrayJoinProjection`2)) |
| Нативный JSON / расширенные скаляры | не поддерживаются (только PostgreSQL) |

## Замечания и ограничения

- `GROUP BY ... WITH TOTALS` рендерится корректно, но официальный `ClickHouse.Driver` возвращает строку
  итогов отдельным блоком ответа, который не отдаётся через `IDataReader`, поэтому материализуются
  только строки групп. Используйте, если ваш драйвер отдаёт итоги; сам nextorm только эмитит модификатор.
- Параметры ClickHouse передаются как параметры HTTP-запроса. Для массовой вставки следует
  использовать API бинарной вставки драйвера, а не параметризованные `INSERT`, что выходит за
  пределы построителя запросов.
- Для `null`-значений параметров тип ClickHouse невозможно вывести только из значения CLR. Когда
  запрос связывает параметр `null`, задайте явный тип параметра на уровне драйвера (например,
  пользовательским резолвером) или приведите плейсхолдер в SQL.

## См. также

- [Обзор провайдеров](overview.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.clickhouse/ClickHouseDataContext.cs`,
`src/nextorm.clickhouse/DI/ClickHouseDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
