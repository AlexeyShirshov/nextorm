# Provider mapping table

| Feature | SQLite | SQL Server | PostgreSQL |
|---|---|---|---|
| `ToUpper` / `ToLower` | `upper` / `lower` | `upper` / `lower` | `upper` / `lower` |
| `Length` | `length` | `len` | `length` |
| `Substring` | `substring`, 1-based | `substring`, 1-based | `substring`, 1-based |
| `Trim` / `TrimStart` / `TrimEnd` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` |
| `Replace` | `replace` | `replace` | `replace` |
| `Contains` / `StartsWith` / `EndsWith` / `like` | `like` (escape `\`) | `like` (escape `\`) | `like` (escape `\`) |
| `string.IsNullOrEmpty` | `(x is null or x = '')` | `(x is null or x = '')` | `(x is null or x = '')` |
| `string.Format` / `ToString(format)` | `printf` / `strftime` | `format` | `to_char` |
| Ordinal `Equals` / `CompareOrdinal` | `collate binary` | `collate Latin1_General_100_BIN2` | `collate "C"` |
| `collate(s, name)` | `s collate name` | `s collate name` | `s collate "name"` |
| `[Collation]` column | `s collate name` | `s collate name` | `s collate "name"` |
| Regex (`IsMatch` / `Replace`) | `s regexp ...` / `regexp_replace(...)` | `NotSupportedException` | `s ~ ...` / `regexp_replace(...)` |
| `Abs` | `abs` | `abs` | `abs` |
| `Round` | `round(x)` | `round(x, 0)` | `round(x)` |
| `Truncate` | `trunc` | `round(x, 0, 1)` | `trunc` |
| `Log` (natural) | `ln` | `log` | `ln` |
| `Now` / `UtcNow` | `datetime('now')` / `datetime('now')` | `getdate()` / `getutcdate()` | `now()` / `now() at time zone 'utc'` |
| `Year` / `Month` / `Day` / `Hour` | `cast(strftime('%Y'...`/`'%m'`/`'%d'`/`'%H'` `as integer)` | `datepart(year, ...)` etc. | `extract(year from ...)` etc. |
| `??` coalesce | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` |
| String concatenation (`+`) | `\|\|` | `+` | `\|\|` |
| Boolean predicate as a value | unchanged | `cast(case when ... then 1 else 0 end as bit)` | unchanged |
| Arrays (`any`/`all`, array functions) | `NotSupportedException` | `NotSupportedException` | `any(@array)`, `cardinality(...)`, ... |
| JSON/JSONB (`json_agg`, `->`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Text JSON (`json_value`, `json_query`, `json_modify`, `isjson`) | `NotSupportedException` | `json_value(...)`, ..., `isjson(...)` | `NotSupportedException` |
| `nullif` | supported | supported | supported |
| `greatest` / `least` | `max(...)` / `min(...)` (single argument -> `(...)`) | supported (2022+) | supported |
| `iif` | `iif(cond, a, b)` (3.32+) | `iif(cond, a, b)` | `case when cond then a else b end` |
| `multi_if` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (ClickHouse-only; `multiIf`) |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | supported |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / `strftime` difference / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | interval arithmetic / `date_trunc` / date-part difference / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (no `array_agg`) | `string_agg` (2017+); `array_agg` throws | supported |
| Aggregate `filter (where ...)` | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Extended scalar library (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Boolean/bitwise/statistical aggregates | `NotSupportedException` | `NotSupportedException` | supported |
| Ordered-set aggregates (`percentile_cont`, ...) | `NotSupportedException` | window `percentile_cont(f) within group (order by x) over (...)` | `within group (order by ...)` |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Built-in table functions | `NotSupportedException` | `string_split(...)`, `openjson(...)` | `generate_series(...)`, `unnest(...)` |

The in-memory provider does not render SQL: it compiles and evaluates the expression against in-memory
rows, so the .NET method itself runs. The SQL matrix above applies to the SQLite, SQL Server and
PostgreSQL providers.

ClickHouse renders `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (and a scaled
`addYears` for `decade`/`century`/`millennium`), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax`, the `-If` combinators and `multiIf`. It rejects the ANSI
`filter (where ...)` clause and the `regr_*`/boolean aggregates with `NotSupportedException`; see the
[ClickHouse provider](../providers/clickhouse.md).

## Explicitly unsupported

These throw `NotSupportedException` rather than emitting SQL with different semantics:

* `string.IsNullOrWhiteSpace(x)` - throws with a message mentioning `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` - the two-argument form has a provider-specific argument order, so it is left
  unsupported (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:985`). The PostgreSQL two-argument form is available
  as `SqlFunctions.Postgres.log(base, x)` in the extended scalar library instead.
* `Math.Round` overloads that take a `MidpointRounding` (more than two arguments) - not portable.
* `string.Substring(Range)` - no SQL equivalent.
* `string.Trim(c)`/`TrimStart(c)`/`TrimEnd(c)` - SQL trims whitespace only, not an arbitrary character
  set.
* `string.ToUpper(CultureInfo)`/`ToLower(CultureInfo)` with a culture other than
  `CultureInfo.InvariantCulture` - only the invariant upper/lower has a portable form.
* `string.Compare(a, b)` and `string.Compare(a, b, bool)` without a `StringComparison` - culture-sensitive;
  use `string.Compare(a, b, StringComparison.Ordinal)` or `CompareOrdinal`.
* `StringComparison.InvariantCulture`/`CurrentCulture` (with or without `IgnoreCase`) - no portable form.
* `string.Format`/`ToString(format)` specifiers outside the documented subset in
  [Formatting dates and numbers to strings](03-date-and-time.md#formatting-dates-and-numbers-to-strings) - never dropped.
* `Regex` on SQL Server - there is no regular-expression engine; use `SqlFunctions.Sql.like` for simple
  patterns (see [Regular expressions](01-string-functions.md#regular-expressions)).
* A `Regex` pattern, replacement or `RegexOptions` that is not a compile-time constant, a `RegexOptions`
  other than `IgnoreCase` (with `Compiled`/`CultureInvariant` as no-ops), and the `Regex` members other
  than `IsMatch`/`Replace` (for example `Match`, `Split`, capture groups).
