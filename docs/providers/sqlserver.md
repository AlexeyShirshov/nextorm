# SQL Server provider

> Use `nextorm.sqlserver` for Microsoft SQL Server and Azure SQL; it renders `@name` parameters, `top(n)` / `offset … fetch` paging (injecting an `ORDER BY` when needed), `+` concatenation, `isnull`, `datepart` and `count_big`, with bracket-quoted identifiers.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) (`src/nextorm.sqlserver/SqlServerDataContext.cs`) wraps `Microsoft.Data.SqlClient`. It
creates a `SqlConnection`, passes null parameter values as `DBNull` (SqlClient otherwise sends no value at
all), and overrides `MapColumnExpression` to read numeric columns through `Convert.ChangeType` because
SqlClient's typed getters are strict about widening.

[`SqlServerDialect`](xref:NextORM.SqlServer.SqlServerDialect) (`src/nextorm.sqlserver/SqlServerDialect.cs`) is the dialect:

- parameter placeholder `@name`;
- identifiers are bracket-quoted ([`Escape`](xref:NextORM.Core.ISqlDialect.Escape(System.String)) returns `[name]`), including column references so aliases that
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

Two overloads are available on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)
(`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.SqlServer;

var byString = new DataContextBuilder()
    .UseSqlServer("Server=localhost;Database=app;Trusted_Connection=True;TrustServerCertificate=True");

using var connection = new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Database=app;...");
var byConnection = new DataContextBuilder().UseSqlServer(connection);

using var ctx = byString.CreateDataContext();   // IDataContext
```

Directly:

```csharp
using NextORM.Core;
using NextORM.SqlServer;

using IDataContext ctx = new SqlServerDataContext("Server=localhost;Database=app;...", new DataContextBuilder());
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
injects the constant sort `(select null as anyorder)` ([`GetPagingOrderBy`](xref:NextORM.Core.ISqlDialect.GetPagingOrderBy(NextORM.Core.QueryCommand))). An offset-only query emits
`offset m rows` and no `fetch`. When the query already has an `ORDER BY`, nothing is injected.

```csharp
ctx.From<ISimpleEntity>().Offset(10).OrderBy(x => x.Id).Select(x => x.Id);
// ... order by id offset 10 rows
```

## Aggregates and scalar functions

```csharp
var count = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.count_big());        // count_big(*)
var std   = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.stdev((double)x.Id)); // stdev(...)
```

`count_big` / `count_big_distinct` render `count_big(...)`; plain `count`/`count_distinct` render
`count(...)`. `stdev`, `stdevp`, `var` and `varp` keep their names (SQL Server provides them natively).
`Math.Round`, `Math.Truncate`, `len`, `datepart`, `getdate` and `isnull` are all emitted as shown above.

SQL Server 2022+ also opts into `greatest`/`least` (standard syntax) and `SqlFunctions.Sql.date_trunc`, which
renders `datetrunc(part, value)` with the plural ANSI parts folded to the singular T-SQL spellings
(`milliseconds` → `millisecond`); `decade`/`century`/`millennium` throw. Date arithmetic is native:
`SqlFunctions.Sql.date_add(field, amount, value)` renders `dateadd(field, amount, value)` (with
`decade`/`century`/`millennium` folded onto a scaled `year` add), `SqlFunctions.Sql.date_diff(field, start, end)`
renders `datediff(field, start, end)`, `SqlFunctions.Sql.date_from_parts(year, month, day)` renders
`datefromparts(year, month, day)` and `SqlFunctions.Sql.end_of_month(value)` renders `eomonth(value)`.

Date and number formatting is intentionally **not** mapped to the cross-provider surface. T-SQL `FORMAT`
takes a .NET format string (and depends on the CLR; SQL Server 2012+), while PostgreSQL `to_char` and the
`%`-style `DATE_FORMAT`/`strftime`/`formatDateTime` of the other providers use incompatible template
languages, so there is no portable `template` argument. Declare `[SqlFunction("format")]` for SQL Server
formatting; the PostgreSQL equivalent is `SqlFunctions.Postgres.to_char`.

SQL Server 2017+ opts into `SqlFunctions.Sql.string_agg` →
`string_agg(value, delimiter)`. There is no array type, so `array_agg` still throws
([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg) is `false`). SQL Server 2016+ also opts into the JSON-as-text functions
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)): `SqlFunctions.SqlServer.json_value`, `SqlFunctions.SqlServer.json_query`, `SqlFunctions.SqlServer.json_modify` and
`SqlFunctions.SqlServer.isjson` render their T-SQL names over a text column, using a JSONPath string (`'$.name'`);
the PostgreSQL `json`/`jsonb` surface still throws. A typed `OPENJSON ... WITH (...)` rowset is declared with a
`[SqlTableFunction("openjson", WithClause = "...")]` wrapper (see the table-valued-functions guide). The full-text predicates `SqlFunctions.Sql.contains` and
`SqlFunctions.Sql.freetext` ([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText)) render as T-SQL `contains(...)`/`freetext(...)` and require a
full-text index on the column. `SqlFunctions.Sql.iif(condition, whenTrue, whenFalse)` renders `iif(...)`
([`Iif`](xref:NextORM.Core.ISqlDialect.Iif), spelled through [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String))) and
`SqlFunctions.SqlServer.choose(index, ...)` renders `choose(...)` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)); the
specialized `SqlFunctions.SqlServer.iif` spelling still works by inheritance. Locking table hints ([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)) render as `WITH (hint, ...)` after the
primary table name: `ctx.From<IComplexEntity>().WithTableHint("nolock")` emits
`from complex_entity with (nolock)`. Row locking reuses the same mechanism:
`ForUpdate`/`ForShare` ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock),
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints)) attach `with (updlock)`/
`with (holdlock)` to the primary table instead of a trailing `FOR UPDATE`/`FOR SHARE` clause. A
[`LockWaitMode`](xref:NextORM.Core.LockWaitMode) adds `nowait` or `readpast` to the same hint
(`with (updlock, nowait)` / `with (updlock, readpast)`); `readpast` approximates `SKIP LOCKED`.
`QueryCommand.ForJson(...)` ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)) is a terminal that executes
the query and returns the whole result set as one JSON document (`FOR JSON PATH`/`FOR JSON AUTO`, with
optional `ROOT('...')` and `INCLUDE_NULL_VALUES`), and `QueryCommand.ForXml(...)`
([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)) does the same for `FOR XML RAW/AUTO/EXPLICIT/PATH` (with
optional row element, `ROOT('...')` and `ELEMENTS`). `QueryCommand.WithForJson(...)`/`WithForXml(...)`
attach the clause without executing. The session/information family
([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)) renders `SqlFunctions.Sql.current_user()`/`session_user()` as the
ANSI key words and `current_schema()`/`current_database()`/`version()` as `schema_name()`/`db_name()`/`@@version`.
SQL Server renders the window percentiles `SqlFunctions.Sql.percentile_cont(fraction, property).Over()` and
`percentile_disc(...)` as `percentile_cont(fraction) within group (order by property) over (...)`
([`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow)); it has no exact ordered-set
aggregate form. The arbitrary-value aggregate `any_agg` (`ANY_VALUE`) is **not** enabled: T-SQL exposes
`ANY_VALUE` only on SQL Server 2025 / Fabric, which the version-agnostic dialect cannot assume.
The XML data-type methods `SqlFunctions.SqlServer.xml_value(xml, xpath, sqlType)`,
`xml_query(xml, xpath)` and `xml_exist(xml, xpath)`
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)) render the
postfix T-SQL form `xmlcol.value('xpath', 'type')` / `xmlcol.query('xpath')` / `xmlcol.exist('xpath')`;
the XQuery and the SQL type must be string literals. The rowset method `.nodes` is exposed as
`SqlFunctions.SqlServer.xml_nodes(xml, xpath)` and used as a correlated `CrossApply`/`OuterApply`
source: it renders `<xml>.nodes('xpath') as [alias]([value])` and the unfolded `IXmlNodesRow.Value` is
projected with the scalar methods above (the operand must be an outer row column, the XQuery a string
literal).

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

SQL Server has neither `INTERSECT ALL` nor `EXCEPT ALL`. Calling [`IntersectAll`](xref:NextORM.Core.QueryCommand`1.IntersectAll``1(NextORM.Core.QueryCommand{``0})) or [`ExceptAll`](xref:NextORM.Core.QueryCommand`1.ExceptAll``1(NextORM.Core.QueryCommand{``0})) throws a
`NotSupportedException` from the dialect before any SQL reaches the database; [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})) and [`Except`](xref:NextORM.Core.QueryCommand`1.Except``1(NextORM.Core.QueryCommand{``0}))
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
| `greatest` / `least` | supported (SQL Server 2022+; ignores NULL arguments) |
| `date_trunc` | `datetrunc(part, value)` (SQL Server 2022+) |
| `date_add` / `date_diff` / `date_from_parts` / `end_of_month` | `dateadd(field, amount, value)` / `datediff(field, start, end)` / `datefromparts(y, m, d)` / `eomonth(value)` |
| `string_agg` / `array_agg` | `string_agg` supported (SQL Server 2017+); `array_agg` not supported (throws) |
| Text JSON | `json_value` / `json_query` / `json_modify` (SQL Server 2016+) |
| Conditional functions | `iif(...)` (portable, [`Iif`](xref:NextORM.Core.ISqlDialect.Iif)) / `choose(...)` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)) |
| Full-text predicates | `contains(...)` / `freetext(...)` (column must be full-text indexed) |
| Full-text ranking | `containstable(table, column, search)` / `freetexttable(...)` table functions return `KEY`/`RANK` ([`SqlFunctions.SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer), [`IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1)) |
| Regular expressions | `regexp_like(value, pattern, 'c'/'i')` / `regexp_replace(value, pattern, replacement, 1, 0, 'c'/'i')` (SQL Server 2025+; `regexp_like` additionally needs database compatibility level 170) |
| Locking table hints | `with (hint, ...)` after the primary table ([`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[]))) |
| Row locking | `ForUpdate`/`ForShare` render `with (updlock)`/`with (holdlock)` on the primary table ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock), [`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints)); a [`LockWaitMode`](xref:NextORM.Core.LockWaitMode) adds `nowait`/`readpast` (`with (updlock, nowait)`/`with (updlock, readpast)`) |
| Native bulk copy | `SqlBulkCopy`; [`BulkInsertOptions`](xref:NextORM.Core.BulkInsertOptions) `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` map to `SqlBulkCopyOptions` (see [Bulk insert](../guide/24-bulk-insert.md#sql-server-bulk-copy-options)) |
| Session/info functions | `current_user`, `session_user`, `schema_name()`, `db_name()`, `@@version` |
| Window percentiles | `percentile_cont`/`percentile_disc` as `... within group (order by x) over (...)` (SQL Server 2012+) |
| Arbitrary-value aggregate | not supported (`ANY_VALUE` is SQL Server 2025 / Fabric only) |
| JSON output | whole result set as one JSON document, terminal `for json path` / `for json auto` ([`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[]))) |
| XML output | whole result set as one XML document, terminal `for xml raw/auto/explicit/path` ([`ForXml`](xref:NextORM.Core.QueryCommand`1.ForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean,System.Object[]))) |
| XML data-type methods | `xml.value('xpath', 'type')` / `xml.query('xpath')` / `xml.exist('xpath')` / `xml.nodes('xpath') as [alias]([value])` ([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)) |
| `AVG` over an integer column | truncated to an integer |
| `ORDER BY … DESC` null placement | nulls sort last by default |

## See also

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs:25,37,43,52,60,69,84,93,102`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:133,146,160,185,218,231,249,268,856,1044`,
`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:24,43`,
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.sqlserver/SqlServerDataContext.cs`,
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs`.
