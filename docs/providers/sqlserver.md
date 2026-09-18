# SQL Server provider

> Use `nextorm.sqlserver` for Microsoft SQL Server and Azure SQL; it renders `@name` parameters, `top(n)` / `offset … fetch` paging (injecting an `ORDER BY` when needed), `+` concatenation, `isnull`, `datepart` and `count_big`, with bracket-quoted identifiers.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

`SqlServerDbContext` (`src/nextorm.sqlserver/SqlServerDbContext.cs`) wraps `Microsoft.Data.SqlClient`. It
creates a `SqlConnection`, passes null parameter values as `DBNull` (SqlClient otherwise sends no value at
all), and overrides `MapColumnExpression` to read numeric columns through `Convert.ChangeType` because
SqlClient's typed getters are strict about widening.

`SqlServerDialect` (`src/nextorm.sqlserver/SqlServerDialect.cs`) is the dialect:

- parameter placeholder `@name`;
- identifiers are bracket-quoted (`Escape` returns `[name]`), including column references so aliases that
  collide with T-SQL keywords stay usable;
- type mapping: `byte`→`tinyint`, `short`→`smallint`, `int`→`int`, `long`→`bigint`, `float`→`real`,
  `double`→`float`, `decimal`→`decimal(38, 10)`;
- string length is `len(...)`, date parts are `datepart(part, value)`, `DateTime.Now`/`UtcNow` are
  `getdate()`/`getutcdate()`;
- `Math.Truncate` becomes `round(x, 0, 1)` and `Math.Round` supplies the missing length argument:
  `round(x, 0)`;
- T-SQL has no boolean type, so a boolean-valued expression used as a value is materialised as
  `cast(case when ... then 1 else 0 end as bit)`; in a condition context a boolean `CASE` is compared
  with `1`.

Finally, SQL Server has no `RECURSIVE` keyword (recursive CTEs are declared with `with` alone) and
exposes `option (maxrecursion n)` to raise the default depth.

## Registering the provider

Two overloads are available on `DbContextBuilder`
(`src/nextorm.sqlserver/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.sqlserver;

var byString = new DbContextBuilder()
    .UseSqlServer("Server=localhost;Database=app;Trusted_Connection=True;TrustServerCertificate=True");

using var connection = new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Database=app;...");
var byConnection = new DbContextBuilder().UseSqlServer(connection);

using var ctx = byString.CreateDbContext();   // IDataContext
```

Directly:

```csharp
using nextorm.core;
using nextorm.sqlserver;

using IDataContext ctx = new SqlServerDbContext("Server=localhost;Database=app;...", new DbContextBuilder());
```

## Paging: `TOP` and `OFFSET … FETCH`

```csharp
ctx.From<ISimpleEntity>().Limit(5).Select(x => x.Id);
// select top(5) id from simple_entity

ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);
// select id from simple_entity order by (select null as anyorder)
// offset 10 rows
// fetch next 5 rows only
```

SQL Server rejects `OFFSET`/`FETCH` without an `ORDER BY`, so when a paged query has no sort the dialect
injects the constant sort `(select null as anyorder)` (`GetPagingOrderBy`). An offset-only query emits
`offset m rows` and no `fetch`. When the query already has an `ORDER BY`, nothing is injected.

```csharp
ctx.From<ISimpleEntity>().Offset(10).OrderBy(x => x.Id).Select(x => x.Id);
// ... order by id offset 10 rows
```

## Aggregates and scalar functions

```csharp
var count = ctx.From<IComplexEntity>().Select(x => NORM.SQL.count_big());        // count_big(*)
var std   = ctx.From<IComplexEntity>().Select(x => NORM.SQL.stdev((double)x.Id)); // stdev(...)
```

`count_big` / `count_big_distinct` render `count_big(...)`; plain `count`/`count_distinct` render
`count(...)`. `stdev`, `stdevp`, `var` and `varp` keep their names (SQL Server provides them natively).
`Math.Round`, `Math.Truncate`, `len`, `datepart`, `getdate` and `isnull` are all emitted as shown above.

SQL Server 2022+ also opts into `greatest`/`least` (standard syntax) and `NORM.SQL.date_trunc`, which
renders `datetrunc(part, value)` with the plural ANSI parts folded to the singular T-SQL spellings
(`milliseconds` → `millisecond`); `decade`/`century`/`millennium` throw. Date arithmetic is native:
`NORM.SQL.date_add(field, amount, value)` renders `dateadd(field, amount, value)` (with
`decade`/`century`/`millennium` folded onto a scaled `year` add), `NORM.SQL.date_diff(field, start, end)`
renders `datediff(field, start, end)`, `NORM.SQL.date_from_parts(year, month, day)` renders
`datefromparts(year, month, day)` and `NORM.SQL.end_of_month(value)` renders `eomonth(value)`.
SQL Server 2017+ opts into `NORM.SQL.string_agg` →
`string_agg(value, delimiter)`. There is no array type, so `array_agg` still throws
(`SupportsArrayAgg` is `false`). SQL Server 2016+ also opts into the JSON-as-text functions
(`SupportsTextJson`): `NORM.MS_SQL.json_value`, `NORM.MS_SQL.json_query`, `NORM.MS_SQL.json_modify` and
`NORM.MS_SQL.isjson` render their T-SQL names over a text column, using a JSONPath string (`'$.name'`);
the PostgreSQL `json`/`jsonb` surface still throws. The full-text predicates `NORM.SQL.contains` and
`NORM.SQL.freetext` (`SupportsFullText`) render as T-SQL `contains(...)`/`freetext(...)` and require a
full-text index on the column. Table hints (`SupportsTableHints`) render as `WITH (hint, ...)` after the
primary table name: `ctx.From<IComplexEntity>().WithTableHint("nolock")` emits
`from complex_entity with (nolock)`. `QueryCommand.ForJson(...)` (`SupportsForJson`) appends a trailing
`FOR JSON PATH`/`FOR JSON AUTO` clause (with optional `ROOT('...')` and `INCLUDE_NULL_VALUES`), and
`QueryCommand.ForXml(...)` (`SupportsForXml`) a `FOR XML RAW/AUTO/EXPLICIT/PATH` one (with optional row
element, `ROOT('...')` and `ELEMENTS`).

## Recursive CTEs and `maxRecursion`

```csharp
// Union branches must share one result type, so a named shape is used.
public sealed class CteNumberRow { public int n { get; set; } }

var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
var body = anchor.UnionAll(step);

var sql = ctx.WithRecursive("nums", body, 100).From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt });
```

```sql
with nums as (
    select ... union all select ...
)
select n from nums option (maxrecursion 100)
```

The `recursive` modifier is omitted (T-SQL has none) and `maxRecursion` is rendered as the trailing
statement option; SQL Server's default depth is 100, so only pass a value when you need a different limit.

## `*ALL` set operations

SQL Server has neither `INTERSECT ALL` nor `EXCEPT ALL`. Calling `IntersectAll` or `ExceptAll` throws a
`NotSupportedException` from the dialect before any SQL reaches the database; `Intersect` and `Except`
(without `ALL`) work.

```csharp
// Throws NotSupportedException mentioning IntersectAll.
var q = a.Select(x => x.Id).IntersectAll(b.Select(x => x.Id));
```

## Aliases

Derived tables (a subquery in `FROM`) and table-valued functions must be aliased, and identifiers use
brackets:

```sql
select t1.value, t2.somestring as [String]
from all_rows() as [t1]
join complex_entity as [t2] on t1.id = t2.id
```

## Provider differences

| Aspect | SQL Server |
|---|---|
| Parameter placeholder | `@name` |
| Limit only | `top(n)` |
| Limit + offset | `offset m rows fetch next n rows only` |
| Offset only | `offset m rows` |
| Paging without `ORDER BY` | injects `order by (select null as anyorder)` |
| Concat | `+` |
| Coalesce | `isnull` |
| Boolean literal | `1` / `0` (bit materialisation) |
| Identifier quoting | brackets (`as [t1]`) |
| Derived table / TVF alias | required |
| `*ALL` | not supported (throws) |
| Recursive CTE | `with` + `option (maxrecursion n)` |
| Aggregate names | `stdev`/`var` native; `count_big` available |
| `greatest` / `least` | supported (SQL Server 2022+) |
| `date_trunc` | `datetrunc(part, value)` (SQL Server 2022+) |
| `date_add` / `date_diff` / `date_from_parts` / `end_of_month` | `dateadd(field, amount, value)` / `datediff(field, start, end)` / `datefromparts(y, m, d)` / `eomonth(value)` |
| `string_agg` / `array_agg` | `string_agg` supported (SQL Server 2017+); `array_agg` not supported (throws) |
| Text JSON | `json_value` / `json_query` / `json_modify` (SQL Server 2016+) |
| Full-text predicates | `contains(...)` / `freetext(...)` (column must be full-text indexed) |
| Table hints | `with (hint, ...)` after the primary table (`WithTableHint`) |
| JSON output | trailing `for json path` / `for json auto` (`ForJson`) |
| XML output | trailing `for xml raw/auto/explicit/path` (`ForXml`) |
| `AVG` over an integer column | truncated to an integer |
| `ORDER BY … DESC` null placement | nulls sort last by default |

## See also

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.sqlserver.tests/SqlServerDialectTests.cs:25,37,43,52,60,69,84,93,102`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:133,146,160,185,218,231,249,268,856,1044`,
`test/nextorm.integration.tests/SqlServerSpecificTests.cs:24,43`,
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.sqlserver/SqlServerDbContext.cs`,
`src/nextorm.sqlserver/DI/DataContextOptionsBuilderExtensions.cs`.
