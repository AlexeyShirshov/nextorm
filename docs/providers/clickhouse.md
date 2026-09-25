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
- native `UInt64` columns and `ulong`/`ulong?` properties/projections materialise through the row
  reader's `DbDataReader.GetFieldValue<ulong>` accessor, so no SQL cast is needed (MySQL/MariaDB
  `BIGINT UNSIGNED` benefits from the same accessor);
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
  [`DateConversion`](xref:NextORM.Core.ISqlDialect.DateConversion)):
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
  `covarPop`/`covarSamp`, `corr` as `corr`, `arg_min`/`arg_max` as `argMin`/`argMax`, and an aggregate
  filter as the `-If` combinator (`countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`, emitted for the shared
  `SqlFunctions.Sql.count`/`sum`/`avg`/`min`/`max` when they take a predicate); the distinct-count aggregates
  `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` as `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12` wrapped
  in `toInt64(...)` to normalise the native `UInt64` to the declared CLR `long`; the
  count aggregates (`count`/`count_distinct` and the filtered `countIf`, plus
  `count_big`/`count_big_distinct`) are cast
  the same way — to `toInt32(...)` for the `int`-returning variants and `toInt64(...)` for the 64-bit
  ones; the
  parameterised quantile aggregates `quantile(level)(value)`/`quantileExact`/`quantileTiming` and
  `median` wrapped in `toFloat64(...)` (so every variant materialises as a CLR `double`); the
  arbitrary-value aggregate `any_agg` as `any` (cross-provider: `ANY_VALUE(x)` on MySQL) and the
  last-row aggregate `any_last` as `anyLast`; the array-returning aggregates `group_array`/`group_uniq_array`
  as `groupArray`/`groupUniqArray` (materialised as a CLR `T[]`); the sequence/funnel aggregates
  `window_funnel`/`sequence_match`/`retention` as `windowFunnel`/`sequenceMatch`/`retention`
  (`windowFunnel`/`sequenceMatch` are wrapped in `toInt32(...)`; `retention` returns `Array(UInt8)` as `byte[]`);
- the string-JSON extractors `json_extract_string`/`json_extract_int`/`json_extract_float`/
  `json_extract_bool`/`json_extract_raw`/`json_has`/`json_type` as `JSONExtractString`/`JSONExtractInt`/
  `JSONExtractFloat`/`JSONExtractBool`/`JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length` as
  `toInt64(JSONLength(...))`, the array-returning `json_extract_keys`/`json_extract_array_raw`/
  `json_extract_keys_and_values` as `JSONExtractKeys`/`JSONExtractArrayRaw`/`JSONExtractKeysAndValues`
  (projecting as `string[]`/`Tuple<string, T>[]`; the value type of the last is a non-nullable generic),
  and the flat-JSON fast path `visit_param_extract_string`/`_int`/`_float`/
  `_bool`/`_raw` as `visitParamExtractString`/`visitParamExtractInt`/`visitParamExtractFloat`/
  `visitParamExtractBool`/`visitParamExtractRaw`; the JSONPath scalars `json_value`/`json_query`/
  `json_exists` as `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` and the native-JSON functions `json_all_paths`/
  `json_all_paths_with_types`/`to_json_string` as `JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString`
  (same [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract) gate);
  the dictionary functions `dict_get`/`dict_get_or_default`/
  `dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in` as
  `dictGet`/`dictGetOrDefault`/`dictHas`/`dictGetHierarchy`/`dictGetChildren`/`dictIsIn`;
- the super-aggregate `GROUP BY ... WITH TOTALS` modifier ([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals))
  via `EntityBuilder.WithTotals()`;
- ClickHouse type names in casts (`Int32`, `Int64`, `Float64`, `Decimal(38, 10)`, …);
- `limit n` / `limit n offset m` paging; an offset without a limit becomes
  `limit 18446744073709551615 offset m`, because ClickHouse only accepts `offset` together with `limit`;
- `LIMIT n BY expr` ([`LimitBy`](xref:NextORM.Core.ISqlDialect.LimitBy)) via
  `EntityBuilder.LimitBy(...)`: at most `n` rows per distinct key, emitted after `ORDER BY` and before the
  final `LIMIT`;
- the `numbers`/`numbers_mt` table functions via `SqlFunctions.ClickHouse.numbers(...)` (the `UInt64`
  `number` column is cast to `Int64` through a wrapping subquery so the row reader can materialise it)
  and the `zeros`/`zeros_mt` row-count table functions via `SqlFunctions.ClickHouse.zeros(...)`
  (`IZerosRow`, the `zero UInt8` column materialises directly as `byte`); the server/cluster table
  functions `url`/`s3`/`file`/`remote`/`remote_secure`/`cluster`/`cluster_all_replicas` are also
  pre-declared (the row shape is declared by the caller's generic `TRow` interface, which must match the
  `structure` argument or the target table); the `values` table function
  (`SqlFunctions.ClickHouse.values<TRow>(tuples)`) renders its `structure` from `TRow`
  ([`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema))) and takes the
  verbatim tuple list as its argument;
- the query modifiers `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS` via `Final()`, `Sample(ratio[, offset])`,
  `PreWhere(predicate)` and `Settings(("key", "value"), ...)`
  ([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings));
  `FINAL`/`PREWHERE` need a table engine that supports them (the `Memory` engine rejects both);
- the portable conditional `iif` as `if(condition, a, b)` ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif),
  [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String))), the ClickHouse-only multi-branch `multiIf` built with
  `when(...)`/`otherwise(...)` ([`MultiIf`](xref:NextORM.Core.ISqlDialect.MultiIf),
  [`IMultiIfRenderer.Render`](xref:NextORM.Core.IMultiIfRenderer.Render(System.Collections.Generic.IReadOnlyList{System.String},System.Type))), and the `percent_rank()`/`cume_dist()`,
  `nth_value(expr, n)` and frame-respecting `lagInFrame(value[, offset[, default]])`/`leadInFrame(...)`
  window functions ([`SupportsPercentRankCumeDist`](xref:NextORM.Core.ISqlDialect.SupportsPercentRankCumeDist),
  [`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue),
  [`SupportsInFrameWindowFunctions`](xref:NextORM.Core.ISqlDialect.SupportsInFrameWindowFunctions));
- the distributed `GLOBAL IN` predicate via
  [`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})) (over a subquery or a
  value list, [`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates));
  negate with C# `!` for `GLOBAL NOT IN`;
- the join strictness/kind modifiers `ANY`/`ALL`/`ASOF` via
  [`EntityBuilder.WithStrictness`](xref:NextORM.Core.EntityBuilder`1.WithStrictness(NextORM.Core.JoinStrictness)) right after a join
  ([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
  [`MakeJoinKeyword`](xref:NextORM.Core.ISqlDialect.MakeJoinKeyword(NextORM.Core.JoinType,NextORM.Core.JoinStrictness,System.Boolean,NextORM.Core.KeywordCase)), enum `JoinStrictness`).
  `LEFT ANY JOIN` keeps a single right-hand row per left-hand row, `ALL` keeps every match and
  `ASOF` needs one equi-join column plus a final inequality. The `SEMI`/`ANTI`/`PASTE` kinds have
  dedicated builder methods: [`SemiJoin`](xref:NextORM.Core.EntityBuilder`1.SemiJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}}))/`AntiJoin` return only the
  left-hand columns for left rows that do (respectively do not) have a match, and `PasteJoin` pairs the
  two sources by row position with no `ON` (yielding as many rows as the shorter side). They are gated
  by [`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/`SupportsPasteJoin`.
  The `GLOBAL` variant (resolved once and broadcast for distributed queries) is set with
  [`EntityBuilder.Global`](xref:NextORM.Core.EntityBuilder`1.Global) and combines with strictness
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
| Filtered aggregate | the `-If` combinator `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`, emitted from the shared filter API (no ANSI `filter (where ...)`) |
| ArgMin / ArgMax | `argMin`/`argMax` |
| quantile / median | `quantile(0.5)(x)`, `quantileExact(0.9)(x)`, `quantileTiming(0.5)(x)`, `median(x)` (as `toFloat64(...)`) |
| any_agg (arbitrary value) | `any(x)` (cross-provider; `ANY_VALUE(x)` on MySQL) |
| any_last (last row) | `anyLast(x)` |
| Conditional function | `iif(cond, a, b)` → `if(cond, a, b)`; `multi_if(when(c1, v1), ..., otherwise(v))` → `multiIf(c1, v1, ..., v)` |
| Window functions | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` supported; `lag_in_frame`/`lead_in_frame` → `lagInFrame`/`leadInFrame` (frame-respecting; the plain `lag`/`lead` reject an explicit frame on ClickHouse) |
| String JSON | `JSONExtractString`, `JSONExtractInt`, `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONHas`, `toInt64(JSONLength(...))`, `JSONType`, `JSONExtractKeys`/`JSONExtractArrayRaw` (`string[]`), `JSONExtractKeysAndValues` (`Tuple<string, T>[]`), `visitParamExtract*`, `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (JSONPath) |
| Native JSON functions | `json_all_paths` → `JSONAllPaths` (projects as `string[]`), `json_all_paths_with_types` → `JSONAllPathsWithTypes` (native `Map(String, String)` surfaced as `Dictionary<string, string>`; `mapKeys`/`mapValues` bridge to a collection), `to_json_string` → `toJSONString`; all take a native `JSON` value (`CAST(col AS JSON)` for a `String` column), while the native `JSON` *column type* itself is not mapped |
| Dictionaries | `dictGet`, `dictGetOrDefault`, `dictHas`, `dictGetHierarchy`, `dictGetChildren`, `dictIsIn` (needs a configured `CREATE DICTIONARY`) |
| Session/info functions | `currentUser()`, `currentDatabase()`, `version()` (`session_user`/`current_schema` are not available) |
| `GROUP BY ... WITH TOTALS` | `with totals` (the extra totals row is not surfaced by `ClickHouse.Driver`) |
| `LIMIT n BY expr` | `limit [offset, ]n by col1, col2` (before the final `LIMIT`) |
| Query modifiers | `final`, `sample r [offset o]`, `prewhere`, `settings k = v` (`FINAL`/`PREWHERE` need a supporting table engine) |
| Table functions | `numbers`/`numbers_mt` (the `UInt64 number` column is cast to `Int64`), `zeros`/`zeros_mt` (`zero UInt8`), `generateRandom` (the built-in `generate_random()`/`generate_random(seed)` fix the structure `id UInt64, value Float64, name String` and cast `id` to `Int64`), and the server/cluster functions `url(url, format, structure)`, `s3(url, format, structure)`, `file(path, format, structure)`, `remote(addresses, db, table)`, `remote_secure(...)`, `cluster(cluster, db, table)`, `cluster_all_replicas(...)` (the row shape is the caller's `TRow` interface) |
| Array functions | over `Array(T)` columns/expressions: `length`, `has`, `indexOf`, `hasAny`, `hasAll`, `startsWith`, `endsWith`, `hasSubstr`, `arrayStringConcat`, `splitByChar`, `arraySort`, `arrayReverse`, `arrayDistinct`, `range`, `arrayEnumerate`, `arrayCumSum`, `arraySlice`, `arrayPushBack`; the CLR `string.Split` renders as `splitByChar(separator, value)` under [`StringSplit`](xref:NextORM.Core.ISqlDialect.StringSplit) (one-character separator only); `arrayJoin(array)` expands one row per element, and `EntityBuilder.ArrayJoin`/`LeftArrayJoin` render the `[left ]array join expr, ...` clause. `EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` additionally bind the expanded element to `ArrayJoinProjection<TEntity, TElement>.Element` (with the original entity at `.Item1`); the clause expression is aliased and `p.Element` references that alias (see [`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions), [`ArrayJoinKind`](xref:NextORM.Core.ArrayJoinKind), [`ArrayJoinProjection`](xref:NextORM.Core.ArrayJoinProjection`2)) |
| Tuple surface | a native `Tuple(...)` column/expression projects as `System.Tuple<...>` (arity 1–7); `Tuple.Create(a, b, ...)` renders `tuple(a, b, ...)` and `System.Tuple<...>.ItemN` renders `tupleElement(t, n)`, both under [`SupportsTupleFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTupleFunctions); `untuple` is not supported (it changes the result column set) |
| Native JSON column type | not mapped: `ClickHouse.Driver` reads a native `JSON` column as `System.Text.Json.Nodes.JsonObject`, which the row reader cannot materialise. The native-JSON *functions* are available over any JSON-valued expression |

## Notes and limitations

- `GROUP BY ... WITH TOTALS` renders correctly, but the official `ClickHouse.Driver` returns the totals
  row in a separate response block it does not expose through `IDataReader`, so only the group rows are
  materialised. Use it if your driver surfaces totals; nextorm itself only emits the modifier.
- ClickHouse parameters are sent as HTTP query parameters. Bulk inserts should use the driver's binary
  insert API rather than parameterized `INSERT` statements, which is outside the query builder.
- Null parameter values cannot have their ClickHouse type inferred from the CLR value alone. When a
  query binds a `null` parameter, set an explicit parameter type at the driver level (for example with
  a custom resolver) or cast the placeholder in SQL.
- ClickHouse rejects an explicit window frame on the standard `lag`/`lead` (and other non-frame-aware
  window functions) with `BAD_ARGUMENTS`. Use `SqlFunctions.ClickHouse.lag_in_frame`/`lead_in_frame`
  when the calculation must respect the frame; their `lag`/`lead` spelling is frame-agnostic.
- `multi_if` returns the common supertype ClickHouse infers for its branches. When `TResult` is a
  numeric CLR type the whole call is cast to it (`cast(multiIf(...) as Int64)`, `Float64`, ...), because
  ClickHouse would otherwise materialise the common type (for example `UInt8` for small integer
  literals), which would not match the declared CLR `TResult`; non-numeric results (string, date) are
  not cast, so choose a `TResult` matching the branches for those.
- The native-JSON functions (`json_value`/`json_query`/`json_exists`, `json_all_paths`/
  `json_all_paths_with_types`/`to_json_string`) are gated by `SupportsJsonExtract`. The `JSONAllPaths`
  pair takes a native `JSON` value (cast a `String` column with `CAST(col AS JSON)`); `json_all_paths`
  projects as `string[]`, and `json_all_paths_with_types` surfaces the native `Map(String, String)` as
  `Dictionary<string, string>` (`mapKeys`/`mapValues` bridge it to a collection). A native `JSON`
  *column*, however, is not mapped: `ClickHouse.Driver` surfaces it as
  `System.Text.Json.Nodes.JsonObject`, which nextorm's row reader has no mapping for. Store JSON in
  a `String` column (or cast the column in SQL) when you need to materialise the column itself.
- `hits_v1` and similar wide tables have far more columns than an entity interface declares. Rather
  than mapping every column, project the extra ones by name with
  [`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)) (see
  [Querying and projections](../guide/01-querying-and-projections.md#columns-by-name)); the name is
  matched verbatim, so quoting follows the dialect.

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
