# ClickHouse provider

> Use `nextorm.clickhouse` for ClickHouse; it renders `@name` parameters (rewritten to `{name:Type}` by the driver), backtick-quoted identifiers, `concat(...)` concatenation, `limit`/`offset` paging and ClickHouse type names.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

[`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext) (`src/nextorm.clickhouse/ClickHouseDataContext.cs`) wraps the official
`ClickHouse.Driver` ADO.NET provider. It creates a `ClickHouseConnection` from the connection string
and returns [`Instance`](xref:NextORM.ClickHouse.ClickHouseDialect.Instance) from its `Dialect` property.

[`ClickHouseDialect`](xref:NextORM.ClickHouse.ClickHouseDialect) (`src/nextorm.clickhouse/ClickHouseDialect.cs`) renders:

- parameter placeholder `@name`; the driver rewrites these to ClickHouse's native
  `{name:Type}` form and infers the type from the .NET value;
- backtick-quoted identifiers and aliases;
- string concatenation with the `concat(a, b, ...)` function;
- `coalesce(a, b)`, `true`/`false` boolean literals and `lengthUTF8(x)` for string length;
- `trimBoth`/`trimLeft`/`trimRight` for the three trim kinds;
- `now()` for local time and `now('UTC')` for UTC;
- `stdev`/`stdevp`/`var`/`varp` as `stddevSamp`/`stddevPop`/`varSamp`/`varPop`;
- `date_trunc(field, x)` as `dateTrunc('field', x)` (the plural ANSI sub-second parts are singularised;
  `decade`/`century`/`millennium` throw), and `date_add`/`DateTime.Add*` as the dedicated
  `addYears`/`addQuarters`/…/`addSeconds` functions (`decade`/`century`/`millennium` fold onto a scaled
  `addYears`), `end_of_month` as `toLastDayOfMonth(x)`;
- the date conversion/part surface `SqlFunctions.ClickHouse.to_*` (gated by
  [`SupportsDateConversionFunctions`](xref:NextORM.Core.ISqlDialect.SupportsDateConversionFunctions)):
  `to_date`/`to_date_time`/`to_date32` as `toDate`/`toDateTime`/`toDate32`, the `to_year`/`to_quarter`/
  `to_month`/`to_day_of_month`/`to_day_of_week`/`to_day_of_year`/`to_hour`/`to_minute`/`to_second`
  accessors (and the `DateTime.Year`/`Month`/… projections) as `toYear`/`toQuarter`/… wrapped in
  `toInt32(...)` (the native `UInt8`/`UInt16` cannot be read back otherwise), `to_start_of_*` as
  `toStartOfYear`/`toStartOfQuarter`/`toStartOfMonth`/`toStartOfWeek`/`toStartOfDay`/`toStartOfHour`/
  `toStartOfMinute`/`toStartOfSecond`, `to_monday` as `toMonday` (ISO Monday, whereas `toStartOfWeek`
  starts on Sunday), `to_yyyymm`/`to_yyyymmdd` as `toInt32(toYYYYMM(...))`/`toInt32(toYYYYMMDD(...))`
  and `to_unix_timestamp` as `toInt64(toUnixTimestamp(...))`;
- `string_agg(x, delimiter)` as `arrayStringConcat(groupArray(x), delimiter)`;
- `bit_and`/`bit_or`/`bit_xor` as `groupBitAnd`/`groupBitOr`/`groupBitXor`, `covar_pop`/`covar_samp` as
  `covarPop`/`covarSamp`, `corr` as `corr`, `arg_min`/`arg_max` as `argMin`/`argMax`, and the filtered
  aggregates `count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` as the `-If` combinators
  `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`; the distinct-count aggregates
  `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` as `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12` wrapped
  in `toInt64(...)` (the native `UInt64` is cast so the row reader can materialise a CLR integer); the
  count aggregates (`count`/`count_distinct`/`count_if`, and `count_big`/`count_big_distinct`) are cast
  the same way — to `toInt32(...)` for the `int`-returning variants and `toInt64(...)` for the 64-bit
  ones; the
  parameterised quantile aggregates `quantile(level)(value)`/`quantileExact`/`quantileTiming` and
  `median` wrapped in `toFloat64(...)` (so every variant materialises as a CLR `double`); the
  arbitrary-value aggregate `any_agg` as `any` (cross-provider: `ANY_VALUE(x)` on MySQL) and the
  last-row aggregate `any_last` as `anyLast`;
- the string-JSON extractors `json_extract_string`/`json_extract_int`/`json_extract_float`/
  `json_extract_bool`/`json_extract_raw`/`json_has`/`json_type` as `JSONExtractString`/`JSONExtractInt`/
  `JSONExtractFloat`/`JSONExtractBool`/`JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length` as
  `toInt64(JSONLength(...))`, and the flat-JSON fast path `visit_param_extract_string`/`_int`/`_float`/
  `_bool`/`_raw` as `visitParamExtractString`/`visitParamExtractInt`/`visitParamExtractFloat`/
  `visitParamExtractBool`/`visitParamExtractRaw`; the JSONPath scalars `json_value`/`json_query`/
  `json_exists` as `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (same [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract) gate);
  the dictionary functions `dict_get`/`dict_get_or_default`/
  `dict_has` as `dictGet`/`dictGetOrDefault`/`dictHas`;
- the super-aggregate `GROUP BY ... WITH TOTALS` modifier ([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals))
  via `EntityBuilder.WithTotals()`;
- ClickHouse type names in casts (`Int32`, `Int64`, `Float64`, `Decimal(38, 10)`, …);
- `limit n` / `limit n offset m` paging; an offset without a limit becomes
  `limit 18446744073709551615 offset m`, because ClickHouse only accepts `offset` together with `limit`;
- `LIMIT n BY expr` ([`SupportsLimitBy`](xref:NextORM.Core.ISqlDialect.SupportsLimitBy)) via
  `EntityBuilder.LimitBy(...)`: at most `n` rows per distinct key, emitted after `ORDER BY` and before the
  final `LIMIT`;
- the `numbers`/`numbers_mt` table functions via `SqlFunctions.ClickHouse.numbers(...)` (the `UInt64`
  `number` column is cast to `Int64` through a wrapping subquery so the row reader can materialise it)
  and the `zeros`/`zeros_mt` row-count table functions via `SqlFunctions.ClickHouse.zeros(...)`
  (`IZerosRow`, the `zero UInt8` column materialises directly as `byte`);
- the query modifiers `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS` via `Final()`, `Sample(ratio[, offset])`,
  `PreWhere(predicate)` and `Settings(("key", "value"), ...)`
  ([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings));
  `FINAL`/`PREWHERE` need a table engine that supports them (the `Memory` engine rejects both);
- the portable conditional `iif` as `if(condition, a, b)` ([`SupportsIif`](xref:NextORM.Core.ISqlDialect.SupportsIif),
  [`MakeIif`](xref:NextORM.Core.ISqlDialect.MakeIif)), and the `percent_rank()`/`cume_dist()` and
  `nth_value(expr, n)` window functions ([`SupportsPercentRankCumeDist`](xref:NextORM.Core.ISqlDialect.SupportsPercentRankCumeDist),
  [`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue));
- the distributed `GLOBAL IN` predicate via
  [`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions) (over a subquery or a
  value list, [`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates));
  negate with C# `!` for `GLOBAL NOT IN`;
- the join strictness/kind modifiers `ANY`/`ALL`/`ASOF` via
  [`EntityBuilder.WithStrictness`](xref:NextORM.Core.EntityBuilder`1) right after a join
  ([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
  [`MakeJoinKeyword`](xref:NextORM.Core.ISqlDialect.MakeJoinKeyword), enum `JoinStrictness`).
  `LEFT ANY JOIN` keeps a single right-hand row per left-hand row, `ALL` keeps every match and
  `ASOF` needs one equi-join column plus a final inequality. `SEMI`/`ANTI`/`PASTE` are not supported.
  The `GLOBAL` variant (resolved once and broadcast for distributed queries) is set with
  [`EntityBuilder.Global`](xref:NextORM.Core.EntityBuilder`1) and combines with strictness
  (`global left any join`, [`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)).

ClickHouse has no recursive CTE support, so the dialect declares every CTE with plain `with`.

## Registering the provider

Two overloads are available on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)
(`src/nextorm.clickhouse/DI/ClickHouseDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.ClickHouse;

var builder = new DataContextBuilder()
    .UseClickHouse("Host=localhost;Port=8123;Username=default;Password=secret;Database=app");

using var ctx = builder.CreateDataContext();   // IDataContext
```

You can also construct the context directly:

```csharp
using NextORM.Core;
using NextORM.ClickHouse;

using IDataContext ctx = new ClickHouseDataContext(
    "Host=localhost;Username=default;Database=app", new DataContextBuilder());
```

## String concatenation

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

```sql
select concat('id:', id) as `Label` from simple_entity
```

## Provider differences

| Aspect | ClickHouse |
|---|---|
| Parameter placeholder | `@name` (driver rewrites to `{name:Type}`) |
| Concat | `concat(a, b)` |
| Coalesce | `coalesce` |
| Boolean literal | `true` / `false` |
| Identifier quoting | backticks (`` as `t1` ``) |
| Derived table alias | required |
| TVF alias | required |
| `*ALL` | supported |
| Recursive CTE | not supported (the `recursive` modifier is omitted) |
| `date_trunc` | `dateTrunc('field', x)` |
| Date arithmetic | `addDays(x, n)` … `addYears(x, (n) * 10)`; `toLastDayOfMonth(x)` |
| Date conversion / parts | `toDate`/`toDateTime`/`toDate32`, `toYear`/… (as `toInt32(...)`), `toStartOf*`, `toMonday`, `toInt32(toYYYYMM(...))`/`toInt32(toYYYYMMDD(...))`, `toInt64(toUnixTimestamp(...))` |
| `string_agg` | `arrayStringConcat(groupArray(x), delimiter)` (no `array_agg`) |
| Bit / statistical aggregates | `groupBitAnd`/`groupBitOr`/`groupBitXor`; `corr`/`covarPop`/`covarSamp` |
| Regression aggregates | not supported (`regr_*` is PostgreSQL-only) |
| Boolean aggregates | not supported (`bool_and`/`bool_or`/`every` are PostgreSQL-only) |
| Filtered aggregate | `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf` (no ANSI `filter (where ...)`) |
| ArgMin / ArgMax | `argMin`/`argMax` |
| quantile / median | `quantile(0.5)(x)`, `quantileExact(0.9)(x)`, `quantileTiming(0.5)(x)`, `median(x)` (as `toFloat64(...)`) |
| any_agg (arbitrary value) | `any(x)` (cross-provider; `ANY_VALUE(x)` on MySQL) |
| any_last (last row) | `anyLast(x)` |
| Conditional function | `iif(cond, a, b)` → `if(cond, a, b)` |
| Window functions | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` supported |
| String JSON | `JSONExtractString`, `JSONExtractInt`, `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONHas`, `toInt64(JSONLength(...))`, `JSONType`, `visitParamExtract*`, `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (JSONPath) |
| Dictionaries | `dictGet`, `dictGetOrDefault`, `dictHas` (needs a configured `CREATE DICTIONARY`) |
| Session/info functions | `currentUser()`, `currentDatabase()`, `version()` (`session_user`/`current_schema` are not available) |
| `GROUP BY ... WITH TOTALS` | `with totals` (the extra totals row is not surfaced by `ClickHouse.Driver`) |
| `LIMIT n BY expr` | `limit [offset, ]n by col1, col2` (before the final `LIMIT`) |
| Query modifiers | `final`, `sample r [offset o]`, `prewhere`, `settings k = v` (`FINAL`/`PREWHERE` need a supporting table engine) |
| Table functions | `numbers`/`numbers_mt` (the `UInt64 number` column is cast to `Int64`), `zeros`/`zeros_mt` (`zero UInt8`), `generateRandom` (the built-in `generate_random()`/`generate_random(seed)` fix the structure `id UInt64, value Float64, name String` and cast `id` to `Int64`) |
| Array functions | over `Array(T)` columns/expressions: `length`, `has`, `indexOf`, `hasAny`, `hasAll`, `arrayStringConcat`, `splitByChar`, `arraySort`, `arrayReverse`, `arrayDistinct`, `range`, `arrayEnumerate`, `arrayCumSum`, `arraySlice`, `arrayPushBack`; the CLR `string.Split` renders as `splitByChar(separator, value)` under [`SupportsStringSplit`](xref:NextORM.Core.ISqlDialect.SupportsStringSplit) (one-character separator only); `arrayJoin(array)` expands one row per element, and `EntityBuilder.ArrayJoin`/`LeftArrayJoin` render the `[left ]array join expr, ...` clause. `EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` additionally bind the expanded element to `ArrayJoinProjection<TEntity, TElement>.Element` (with the original entity at `.Item1`); the clause expression is aliased and `p.Element` references that alias (see [`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions), [`ArrayJoinKind`](xref:NextORM.Core.ArrayJoinKind), [`ArrayJoinProjection`](xref:NextORM.Core.ArrayJoinProjection`2)) |
| Native JSON / extended scalars | not supported (PostgreSQL-only) |

## Notes and limitations

- `GROUP BY ... WITH TOTALS` renders correctly, but the official `ClickHouse.Driver` returns the totals
  row in a separate response block it does not expose through `IDataReader`, so only the group rows are
  materialised. Use it if your driver surfaces totals; nextorm itself only emits the modifier.
- ClickHouse parameters are sent as HTTP query parameters. Bulk inserts should use the driver's binary
  insert API rather than parameterized `INSERT` statements, which is outside the query builder.
- Null parameter values cannot have their ClickHouse type inferred from the CLR value alone. When a
  query binds a `null` parameter, set an explicit parameter type at the driver level (for example with
  a custom resolver) or cast the placeholder in SQL.

## See also

- [Provider overview](overview.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.clickhouse/ClickHouseDataContext.cs`,
`src/nextorm.clickhouse/DI/ClickHouseDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
