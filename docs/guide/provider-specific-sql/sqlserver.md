# SQL Server-specific SQL

> SQL Server contributes the `CHOOSE` conditional function, the T-SQL-only scalar library
> (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `UNICODE`, `NCHAR`, `FORMAT`, the
> trigonometric functions, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID`, the
> clock/offset/`*FROMPARTS` date family, the `CHECKSUM`/`COMPRESS`/`RAND`/`STUFF` scalars, the metadata
> functions and the SQL/JSON constructors/aggregates), statement/table hints and the `FOR JSON`/`FOR
> XML` result shape, the XML data-type methods (`value`/`query`/`exist` and the `nodes` rowset), the
> native `PIVOT`/`UNPIVOT` source constructs, plus `string_split`/`openjson` table functions.

**Prerequisites:** [Querying and projections](../../engine/index.md) · [SQL Server provider](../../connecting-providers/sqlserver.md)

## `CHOOSE`

`SqlFunctions.SqlServer.choose(index, v1, v2, ...)` renders `CHOOSE(index, v1, v2, ...)`, gated by
[`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose) (SQL Server only; every other provider
throws).

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new { Label = SqlFunctions.SqlServer.choose(2, "a", "b", "c") })
    .ToList();
```

```sql
select choose(2, 'a', 'b', 'c') as [Label]
 from complex_entity
```

See [Scalar functions](../../scalar-functions/04-conditionals-and-conversion.md#conditional-helpers).

## Hints

[`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])) attaches a table hint to the query's `FROM` table
([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)), and a query hint renders the
trailing `OPTION (...)` clause, for example `OPTION (RECOMPILE)`
([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints)); the CTE `maxRecursion`
option maps to `option (maxrecursion n)`. See [Query hints](../15-query-hints.md).

## Row locking

SQL Server has no trailing `FOR UPDATE` clause, so
[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) and
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) attach the `updlock`/`holdlock` table hint to
the query's primary source instead
([`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints), `ILockRenderer.Render`):

```csharp
var rows = dataContext.From<ISimpleEntity>().ForUpdate().Select(e => e.Id).ToList();
```

```sql
select id
 from simple_entity with (updlock)
```

`ForUpdate` renders `updlock` (update lock held to the end of the transaction) and `ForShare` renders
`holdlock` (shared lock). The lock hint combines with
[`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])), so `dataContext.From<ISimpleEntity>(o => o.WithTableHint("rowlock")).ForUpdate()`
renders `with (rowlock, updlock)`. See
[Row locking](../../guide/31-provider-specific-features.md#row-locking-for-update--for-share).

## `FOR JSON` and `FOR XML`

`ForJson`/`ForXml` are terminals that execute the query and return the whole result set as one JSON or
XML document
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
`WithForJson`/`WithForXml` attach the clause without executing; the two clause kinds are mutually
exclusive. See [JSON support across providers](../16-json.md).

## XML data-type methods

`SqlFunctions.SqlServer.xml_value<T>(xml, xpath, sqlType)`, `xml_query(xml, xpath)` and
`xml_exist(xml, xpath)` render the postfix T-SQL XML methods
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)); the XQuery
and the SQL type must be string literals, and `xml_exist` returns `bit` (compared with `1` in a
predicate context).

```csharp
var rows = dataContext.From<IXmlEntity>()
    .Select(x => new
    {
        Value = SqlFunctions.SqlServer.xml_value<string>(x.Payload, "(/root/item)[1]", "nvarchar(100)"),
        Exists = SqlFunctions.SqlServer.xml_exist(x.Payload, "/root/item[2]")
    })
    .ToList();
```

```sql
select payload.value('(/root/item)[1]', 'nvarchar(100)') as [Value],
       payload.exist('/root/item[2]') as [Exists]
 from xml_entity
```

The rowset method `.nodes` is a correlated `CrossApply`/`OuterApply` source: `xml_nodes(xml, xpath)`
unfolds the XML value into one row per node and renders `<xml>.nodes('xpath') as [alias]([value])`,
after which the unfolded `IXmlNodesRow.Value` is projected with the scalar methods above. The operand
must be a column of the outer row and the XQuery a string literal.

```csharp
var rows = dataContext.From<IXmlEntity>()
    .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
    .Select(p => new { Id = SqlFunctions.SqlServer.xml_value<int>(p.Item2.Value, "(.)[1]/@id", "int") })
    .ToList();
```

```sql
select t2.value.value('(.)[1]/@id', 'int') as [Id]
 from xml_entity as [t1]
 cross apply t1.payload.nodes('/root/item') as [t2](value)
```

See [Scalar functions](../../scalar-functions/07-json-and-xml.md#xml-data-type-methods-sql-server).

## Table-valued functions

`string_split(...)` and `openjson(...)` are exposed through
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})) with a runtime-factory call. See
[Table-valued functions](../13-table-valued-functions.md) and
[JSON support across providers](../16-json.md).

## Temporal tables

`ForSystemTime` queries system-versioned (temporal) tables
([`SupportsTemporalTable`](xref:NextORM.Core.ISqlDialect.SupportsTemporalTable)): `AS OF`,
`BETWEEN ... AND ...`, `FROM ... TO ...`, `CONTAINED IN (...)` and `ALL` are available through the
[`TemporalClause`](xref:NextORM.Core.TemporalClause) factory methods.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1)))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id
 from simple_entity for system_time as of '2020-01-01 00:00:00'
```

MariaDB supports the same clause on system-versioned tables, except `CONTAINED IN`.

## `PIVOT` / `UNPIVOT`

[`EntityBuilder.Pivot`](xref:NextORM.SqlServer.SqlServerEntityBuilderExtensions.Pivot``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PivotAggregate,System.Linq.Expressions.Expression{System.Func{``0,System.Object}},System.Linq.Expressions.Expression{System.Func{``0,System.Object}},NextORM.Core.PivotValue[])) reshapes a source into columns with
the native T-SQL `PIVOT` operator and
[`Unpivot`](xref:NextORM.SqlServer.SqlServerEntityBuilderExtensions.Unpivot``1(NextORM.Core.EntityBuilder{``0},System.String,System.String,NextORM.Core.UnpivotColumn[])) stacks columns into rows with `UNPIVOT`
([`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot) /
[`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot)). They are extension methods in the `NextORM.SqlServer`
namespace; add `using NextORM.SqlServer;` to use them. The result is an untyped source:
select the grouping columns and the pivoted columns by name through `TableAlias`. The operators apply to
a table expression, so the source may be a plain table/entity, a table-valued function or a derived
query (`ctx.From(derivedQuery)`); filters and other modifiers belong to the reshaped result (or, for a
derived source, inside the derived query).

```csharp
var rows = ctx.From<Sales>()
    .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"), PivotValue.Create("2"))
    .Select(t => new
    {
        Category = t.GetString("category"),
        Q1 = SqlFunctions.Column<decimal?>(t, "1"),
        Q2 = SqlFunctions.Column<decimal?>(t, "2")
    })
    .ToList();

var stacked = ctx.From<Quarterly>()
    .Unpivot("value", "quarter", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
    .Select(t => new { Quarter = t.GetString("quarter"), Value = t.GetNullableDecimal("value") })
    .ToList();
```

**Raw names, always safely quoted.** The identifier strings of these APIs are raw, unescaped names, not
SQL: `PivotValue.Create` carries the pivot **data value**, `UnpivotColumn.Create` a source column name,
and the pivoted/stacked result columns are read by name. Every emitted occurrence goes through exactly
one provider quoting pass, independently of the context's
[`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder.UseQuotedIdentifiers(System.Boolean)) option
— with quoting off a by-name accessor still emits `[1]` on SQL Server / `"1"` on
PostgreSQL and SQLite / `` `1` `` on MySQL and ClickHouse. A dot is a literal name character (never a
schema/alias qualifier), SQL-looking punctuation stays inside the one quoted identifier, and manual
pre-quoting is **not** supported: `Column(t, "[1]")` is the literal three-character name `[1]` (rendered
`[[1]]]` on SQL Server), so to reference the column named `1` write `Column(t, "1")` — which is why the
example above reads the pivoted columns through
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)) and not
`GetNullableDecimal("[1]")`. On SQL Server an embedded `]` is doubled, so the pivot value `a]b` renders
`[a]]b]` in both the `IN (...)` list and the projection reference.

A derived query can be the pivoted source, which lets the `PIVOT` input carry joins and computed
aggregate/`FOR` columns (the equivalent of pivoting a CTE):

```csharp
var orderMargins = ctx.From<OrderDetail>()
    .Join<OrderHeader>((d, h) => d.OrderId == h.OrderId)
    .Where(p => SqlFunctions.Sql.extract("year", p.Item2.OrderDate) == 2013)
    .Select(p => new
    {
        CategoryName = p.Item3.Name,
        QuarterNum = SqlFunctions.Sql.extract("quarter", p.Item2.OrderDate),
        Margin = p.Item1.LineTotal - p.Item3.StandardCost * p.Item1.OrderQty
    });

var byQuarter = ctx.From(orderMargins)
    .Pivot(PivotAggregate.Sum, m => m.Margin, m => m.QuarterNum,
        PivotValue.Create("1"), PivotValue.Create("2"), PivotValue.Create("3"), PivotValue.Create("4"))
    .OrderBy(t => t.GetString("CategoryName"))
    .Select(t => new
    {
        Category = t.GetString("CategoryName"),
        Q1 = SqlFunctions.Column<decimal?>(t, "1"),
        Q4 = SqlFunctions.Column<decimal?>(t, "4")
    })
    .ToList();
```

```sql
select CategoryName, [1], [4]
 from (select name as [CategoryName], datepart(quarter, OrderDate) as [QuarterNum],
             (LineTotal - StandardCost * OrderQty) as [Margin]
 from sales_order_detail
 join sales_order_header on sales_order_detail.OrderId = sales_order_header.OrderId
      where datepart(year, OrderDate) = 2013) as [t1]
pivot (sum([Margin]) for [QuarterNum] in ([1], [2], [3], [4])) as [t2]
order by CategoryName
```

### Typed result shape

The overloads above return an untyped [`TableAlias`](xref:NextORM.Core.TableAlias) source — every
grouping column and every pivoted column is read by name. SQL Server also exposes **typed** overloads
that declare the result members and the CLR cell type of each pivoted column once, and derive the
`IN (...)` list from that projection:

```csharp
var typed = ctx.From<Sales>()
    .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
        (s, pivot) => new
        {
            s.Category,                       // a direct member of the inner source is a grouping column
            Q1 = pivot.Value<decimal?>("1"),  // the explicit CLR cell type of the pivoted column "1"
            Q2 = pivot.Value<decimal?>("2")
        });

var rows = typed
    .OrderBy(x => x.Q1)                       // typed members are usable in Where/OrderBy/Select
    .Select(x => new { x.Category, x.Q1, x.Q2 })
    .ToList();
```

```sql
select category, [Q1], [Q2]
 from (select category, [1] as [Q1], [2] as [Q2]
 from sales pivot (sum([Margin]) for [Quarter] in ([1], [2])) as [t1]) as [t2]
 order by t2.[Q1]
```

The typed members are **aliases** over the pivoted columns (`Q1 = pivot.Value<decimal?>("1")` emits
`[1] as [Q1]`), so the shape can rename cells and order them differently from the `IN` list. A
grouping member is any other direct member of the inner source (`s.Category`); inner members that the
projection does not mention remain grouping inputs. `Value<T>` requires the CLR type explicitly —
there is no inference and no automatic nullability: use `decimal?`/`int?`/`string?` when an absent value
or an excluded `NULL` cell is possible (see below). A subset of the grouping members is allowed; the
operator still groups by every remaining inner member. Because the projection is a derived source, it
composes further: `Where`/`OrderBy`/`Select` on the result, a further `From(...)`, or `AsCte` to reuse it
as a CTE. As for every derived source, materializing it needs an explicit `Select`.

```csharp
var byQuarter = ctx.From(orderMargins)   // a derived query (join + computed Margin/FOR columns)
    .Pivot(PivotAggregate.Sum, m => m.Margin, m => m.QuarterNum,
        (m, pivot) => new { m.Category, Q4 = pivot.Value<decimal?>("4") })
    .Select(m => new { m.Category, m.Q4 })
    .ToList();
```

`Unpivot` mirrors this: the projection declares the two role columns
(`unpivot.Value<decimal?>("val")`, `unpivot.Name<string>("qtr")`) using the same identifiers the
`UNPIVOT ([val] for [qtr] in (...))` clause takes, and any other direct member is a grouping column.
SQL Server **excludes** a source cell whose value is `NULL`, so the stacked result can have fewer rows
than `columns × source rows`; declare the value cell nullable to model that exclusion.

```csharp
var stacked = ctx.From<Quarterly>()
    .Unpivot(
        (m, unpivot) => new
        {
            m.Category,
            Quarter = unpivot.Name<string>("qtr"),
            Value = unpivot.Value<decimal?>("val")
        },
        UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
    .Select(x => new { x.Category, x.Quarter, x.Value })
    .ToList();
```

The value and role strings are **runtime identifiers** and **raw names**: they are not checked against
the data or the inner mapping at compile time, and each is emitted as one safely quoted identifier
regardless of the context's
[`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder.UseQuotedIdentifiers(System.Boolean)) option
— a dot is a literal character, and embedded brackets are not a pre-quoted form, so `Value("[1]")` names
the column `[1]` (rendered `[[1]]]` on SQL Server), never a silent `1`. An absent PIVOT value still
renders its column but yields SQL `NULL`, so
a non-nullable cell would fail materialization — declare it nullable; an invalid name or an incompatible
cell type can fail during query preparation or materialization instead of at compile time. A truly
dynamic PIVOT whose value list is only known at runtime cannot be expressed as a typed shape and stays
with the untyped `TableAlias` overload. The typed projection is fail-closed: a duplicate value, a
projection that declares no value, a non-constant value name, a grouping member that is also the
aggregate or `FOR` column, an empty UNPIVOT column list, a missing UNPIVOT role and conflicting role
identifiers all throw `ArgumentException` (or `NotSupportedException` for reading the inner source as a
whole) instead of emitting wrong SQL.

**Variants and boundaries.** The typed operators cover the same inner sources as the untyped ones — a
plain table/entity, a **table-valued function** or a derived query, so
`ctx.From(() => Db.MyTvf()).Pivot(...)` reshapes the function call exactly like a table.
The declared value strings stay **runtime identifiers** (raw names, always safely quoted as above): a
typo or an absent value is not validated at
compile time, so its column is still rendered and a cell that no source row matches materializes as SQL
`NULL` (declare the cell nullable). A genuinely **dynamic PIVOT** — a value list known only at runtime —
has no typed form and stays on the untyped [`TableAlias`](xref:NextORM.Core.TableAlias) overload, which
is unchanged. Both operators keep the eager-load guard: calling them on a builder that carries
`LoadWith`/single-query state throws `NotSupportedException` (the composition cannot survive it), and
interleaved typed queries share no sticky command or plan-cache state.

## `TABLESAMPLE`

[`FromOptions.TableSample`](xref:NextORM.Core.FromOptions.TableSample(System.Double,NextORM.Core.TableSampleMethod,System.Nullable{System.Double})) adds a `TABLESAMPLE` modifier to the
query's primary table ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample)); an
optional seed makes the sample repeatable. SQL Server renders `tablesample (10 percent)` /
`tablesample (10 percent) repeatable (3)`
(`ITableSampleMethods.Render`).

```csharp
var rows = await dataContext.From<ISimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, seed: 3))
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id
 from simple_entity tablesample (10 percent) repeatable (3)
```

SQL Server supports only the `System` method
(`TableSample`); `Bernoulli`
throws `NotSupportedException`. See
[Table sampling](../../guide/01-query-sources.md#table-sampling-tablesample).

## Full-text search

The cross-provider boolean predicates `contains`/`freetext` render `CONTAINS`/`FREETEXT`. For ranking,
`SqlFunctions.SqlServer.containstable`/`freetexttable` expose the matched key and `RANK` score through
`SqlFunctions.IKeyRankRow<TKey>`; see
[Table-valued functions](../13-table-valued-functions.md#built-in-table-functions).

## Sequences

`SqlFunctions.SqlServer.next_value_for(sequence)` and its alias `nextval(sequence)` render the T-SQL
`NEXT VALUE FOR` expression and return CLR `long`. The name must be a constant string written as one
component (`name`) or two (`schema.name`); the components are bracket-quoted separately and an embedded
`]` is doubled, so the name text cannot escape the identifier:

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new { Next = SqlFunctions.SqlServer.next_value_for("dbo.order_seq") })
    .ToList();
```

```sql
select next value for [dbo].[order_seq] as [Next]
 from order_entity
```

Non-constant or captured names (a column, a captured local, `null`/`default`) throw
`NotSupportedException` before any SQL is emitted, and the name is never bound as a parameter. The
sequence itself is an ordinary database object created with DDL; only its next value is exposed (no
session `currval`/`lastval`, no value-returning `setval`). See
[Sequences](../../scalar-functions/10-sequences.md#sql-server).

## Version-gated functions (2022 and 2025)

The [#282](https://github.com/AlexeyShirshov/nextorm/issues/282) names below are gated by the **declared
server major version** on this provider. The gate is version-only: unlike the native JSON storage type
(`SupportsJson`, [#345](https://github.com/AlexeyShirshov/nextorm/issues/345)), which also consults an
engine-edition allowlist because a few Azure/Fabric products have the type while reporting a lower major
version, function availability follows the engine major version itself. An edition declaration never
substitutes for a version here, and an unversioned context refuses them before SQL.

| Member | SQL | Prerequisite |
|---|---|---|
| `SqlFunctions.SqlServer.left_shift` / `right_shift` / `bit_count` / `get_bit` / `set_bit` (one overload set per operand type `byte`/`short`/`int`/`long`) | `CASE WHEN … THEN CAST(NULL AS <operand type>) ELSE left_shift(cast(value as tinyint\|smallint\|int\|bigint), cast(shift as bigint)) END`; `bit_count` → `bigint`, `get_bit` → `bit` | major 16 (SQL Server 2022) |
| `SqlFunctions.SqlServer.regexp_count(input, pattern)` | `regexp_count(input, pattern)` | major 17 (SQL Server 2025); no compatibility-level gate |
| `SqlFunctions.SqlServer.regexp_substr(input, pattern[, start[, occurrence[, match_type]]])` / `regexp_instr(input, pattern[, start[, occurrence[, return_option[, match_type]]]])` | the native call; `0` for a no-match `regexp_instr`; the match type is a `varchar` argument and renders as `cast(… as varchar(4000))` (an `nvarchar` argument is Msg 8116) | major 17; no compatibility-level gate |
| `SqlFunctions.SqlServer.regexp_matches(input, pattern[, match_type])` | a `FROM` source; rows are `match_id bigint`, `start_position`/`end_position int`, `match_value`, `substring_matches` (full JSON capture text) | major 17 **and** database compatibility level 170 |
| `SqlFunctions.SqlServer.edit_distance(input, pattern[, maximum_distance])` / `edit_distance_similarity` / `jaro_winkler_distance` / `jaro_winkler_similarity` | the native call; the capped `edit_distance` returns `maximum_distance + 1` when the distance exceeds the cap | major 17 **and** compatibility level 170 **and** `PREVIEW_FEATURES = ON` **and** a Windows collation |
| `SqlFunctions.SqlServer.product(value)` | `product(value)` (aggregate context only — no `OVER`) | major 17 |
| `SqlFunctions.SqlServer.regexp_split_to_table(input, pattern)` | a `FROM` source; rows are `value` plus the one-based `ordinal bigint` | major 17 **and** database compatibility level 170 |
| `SqlFunctions.SqlServer.approx_count_distinct(value)` | `approx_count_distinct(value)` (aggregate context; `0` for an empty or all-NULL group) | major 15 (SQL Server 2019) |
| `SqlFunctions.SqlServer.checksum_agg(value)` / `grouping_id(expressions...)` | `checksum_agg(value)`; `grouping_id(a, b, …)` in the `SELECT` list, `HAVING` or `ORDER BY` of a grouped command | no version floor; `GROUPING_ID` needs a `GROUP BY` context and every argument must be a grouping expression (both enforced before SQL) |
| `SqlFunctions.SqlServer.approx_percentile_cont(fraction, value)` / `approx_percentile_disc(fraction, value)` | `approx_percentile_cont(f) within group (order by value)`; the `_disc` form returns the sort input type | major 16 (SQL Server 2022); no `OVER` form |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Count = SqlFunctions.SqlServer.regexp_count(e.String, "[0-9]"),
        Shifted = SqlFunctions.SqlServer.left_shift(e.Int, 1),
        Bits = SqlFunctions.SqlServer.bit_count(e.Int),
        Bit = SqlFunctions.SqlServer.get_bit(e.Int, 1),
        Set = SqlFunctions.SqlServer.set_bit(e.Int, 1, false)
    })
    .ToList();

var fragments = dataContext
    .From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b,c", ","))
    .Select(r => new { r.Value, r.Ordinal }) // value + ordinal bigint
    .ToList();
```

A context declared for 15, 16 or 17 permits exactly the rows its major version lists; anything lower or
unversioned is refused **before SQL**, for example:

```text
The LEFT_SHIFT function requires SQL Server major version 16 or later; declared major version: 15.
The REGEXP_SPLIT_TO_TABLE function requires SQL Server major version 17 or later; declared major version: 16.
```

The bit family types every argument inside the call and guards a NULL operand, shift, offset or bit value
with a typed NULL, because a bare `left_shift(NULL, …)` is Msg 8116; the operand's own type selects
`tinyint`/`smallint`/`int`/`bigint`, `bit_count` is `bigint` (`long?`) and `get_bit` is `bit` (`bool?`).
`right_shift` is the engine's logical (zero-fill) shift — `right_shift(-8, 1)` is `2147483644`. Because the
family is one overload set per operand type, an untyped constant binds to the smallest overload
(`left_shift(1, 2)` → `byte?`/`tinyint`, `left_shift(-8, 1)` → `short?`): cast the operand
(`left_shift((int?)1, 2)`) when a specific SQL type is required. `regexp_split_to_table` is a real gate on
two axes: at compatibility level 160 a major-17 engine reports the function as missing (Msg 208), so the 170
prerequisite is asserted live rather than assumed. `product` is offered in the aggregate context only.

The U3 regex scalars and the fuzzy family were executed on the tested engine baseline
`17.0.5005.3`: `regexp_substr`/`regexp_instr` work at compatibility levels **160 and 170**, while
`regexp_matches` disappears at 160 (Msg 208) and the four fuzzy names need compatibility 170,
`PREVIEW_FEATURES = ON` (off by default) and a Windows collation (a `SQL_*` collation is Msg 9847, and a
missing preview flag is Msg 195). Those are runtime prerequisites the dialect cannot inspect, so they are
documented and asserted live; the version flag promises the major-version gate only, and no promise is
made for future builds.

The U4 aggregate family is aggregate-context only. `approx_count_distinct` is `long` (`bigint`) and
counts `0` — not `NULL` — for an empty or all-NULL group; `checksum_agg` is `int?` and returns `NULL`
there; the approximate percentiles are `double?` and the sort input type respectively and also return
`NULL`. A constant NULL operand renders as a typed `CAST(NULL AS <type>)` (a bare `NULL` is Msg 8117);
the percentile fraction must be a query-independent literal or captured scalar in `[0, 1]`, a
row-dependent fraction is refused before SQL, and a literal or NULL `WITHIN GROUP` sort key is refused
before SQL because the engine answers Msg 5309. `GROUPING_ID`'s grouping-context and grouping-list
obligations are enforced before execution with zero statements dispatched, and `Over(...)` on either
approximate percentile is refused — the engine has the `WITHIN GROUP` form only (Msg 4113 for `OVER`;
`checksum_agg` is the one name whose native `OVER` the engine accepts, but this surface offers the
aggregate form only). Both percentiles and the distinct count are approximate: the vendor documents a
≈1.33 % rank error at 99 % confidence for the percentiles, which is a statistical bound, not a
deterministic tolerance.

## T-SQL scalar functions

`SqlFunctions.SqlServer` exposes the T-SQL-only scalar functions through
[`ISqlDialect.SqlServerFunctions`](xref:NextORM.Core.ISqlDialect.SqlServerFunctions)
([`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions)); SQL Server is the only provider that
implements it, so every other provider throws `NotSupportedException` for these members. The members
have no portable equivalent: the cross-provider `ascii`/`char`/`translate` live on
[`SqlFunctions.Sql`](../../scalar-functions/01-string-functions.md#cross-provider-scalar-functions) instead, and
`Math.Log10` already renders T-SQL `LOG10`.

The functions are usable as ordinary values (and as predicates where the T-SQL form is a condition):

```csharp
var data = System.Text.Encoding.UTF8.GetBytes("abc");

var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Pos = SqlFunctions.SqlServer.patindex("%a%", e.String),
        Bracketed = SqlFunctions.SqlServer.quotename(e.String),
        Code = SqlFunctions.SqlServer.soundex(e.String),
        Similar = SqlFunctions.SqlServer.difference(e.String, "abc"),
        Escaped = SqlFunctions.SqlServer.string_escape(e.String, "json"),
        CodePoint = SqlFunctions.SqlServer.unicode(e.String),
        Letter = SqlFunctions.SqlServer.nchar(65),
        Number = SqlFunctions.SqlServer.format(e.Id, "D6"),
        Angle = SqlFunctions.SqlServer.acos(0.5),
        Bucket = SqlFunctions.SqlServer.date_bucket("day", 1, e.Datetime),
        Month = SqlFunctions.SqlServer.datename("month", e.Datetime),
        Digest = SqlFunctions.SqlServer.hashbytes("SHA2_256", data),
        Arr = SqlFunctions.SqlServer.json_array("a", e.Id, "b"),
        Obj = SqlFunctions.SqlServer.json_object("id", e.Id),
        HasId = SqlFunctions.SqlServer.json_path_exists(e.String, "$.id")
    })
    .ToList();
```

```sql
select patindex('%a%', somestring) as [Pos],
       quotename(somestring) as [Bracketed],
       soundex(somestring) as [Code],
       difference(somestring, 'abc') as [Similar],
       string_escape(somestring, 'json') as [Escaped],
       unicode(somestring) as [CodePoint],
       nchar(65) as [Letter],
       format(id, 'D6') as [Number],
       acos(0.5) as [Angle],
       date_bucket(day, 1, dt) as [Bucket],
       datename(month, dt) as [Month],
       hashbytes('SHA2_256', @p0) as [Digest],
       json_array('a', id, 'b') as [Arr],
       json_object('id' : id) as [Obj],
       cast(case when json_path_exists(somestring, '$.id') = 1 then 1 else 0 end as bit) as [HasId]
 from complex_entity
```

* **String:** `patindex(pattern, expression)`, `quotename(value)` / `quotename(value, quote)`,
  `soundex(value)`, `difference(first, second)`, `string_escape(value, type)`, `unicode(value)`,
  `nchar(code)`, `format(value, format)` / `format(value, format, culture)` and
  `stuff(value, start, length, newValue)` (string only — SQL Server's `STUFF` implicitly converts binary arguments to character data, so no `byte[]` overload is exposed). `format` is the native T-SQL
  `FORMAT`, distinct from the CLR `string.Format` translation; `str(value[, length[, decimals]])`
  is the fixed-width floating-point conversion. An apostrophe delimiter is an ordinary string argument:
  `quotename("abc", "'")` renders `quotename(N'abc', N'''')` and the server returns `'abc'`. Text
  literals are Unicode `N'…'` literals with inline apostrophes doubled (`O'Brien` becomes `N'O''Brien'`),
  and a value containing a NUL character is rendered as a constructed Unicode expression over `NCHAR(0)`
  rather than as one literal, so the value is always passed as-is and is never pre-escaped by the caller.
  This paragraph describes SQL Server only and is not a cross-provider guarantee.
* **Numeric:** `acos`, `asin`, `atan`, `atn2(y, x)`, `square` (`cot`/`degrees`/`radians`/`pi` are portable — see [`SqlFunctions.Sql`](../../scalar-functions/02-math-functions.md)) and `rand()` / `rand(seed)` (non-deterministic without a seed).
* **Date/time:** `datename(datepart, date)` (the part is a constant), `date_bucket(datepart, width, date[, origin])` (SQL Server 2022+), the server clocks `sysdatetime`/`sysdatetimeoffset`/`sysutcdatetime`, the offset functions `switchoffset(value, offset)`/`todatetimeoffset(value, offset)` (a string such as `"+02:00"` or signed minutes) and the construction family `timefromparts`/`smalldatetimefromparts`/`datetimefromparts`/`datetime2fromparts`/`datetimeoffsetfromparts`. The trailing `*FROMPARTS` precision must be a constant `0..7` (a non-constant, `null` or out-of-range value throws before any SQL is emitted).
* **Binary/system:** `hashbytes(algorithm, data)` (the algorithm is a constant such as `SHA2_256`), `newsequentialid()` (valid only as a column `DEFAULT`, not in an ordinary `SELECT`), `checksum(values...)`/`binary_checksum(values...)` (at least one value; the `checksum(*)` wildcard is not exposed) and `compress(value)`/`decompress(value)` (GZIP over `varbinary(max)`).
* **Metadata:** `col_length`, `col_name`, `ident_incr`/`ident_seed`, `index_col`, `object_definition`/`object_id`/`object_name`/`object_schema_name`, `stats_date`, `db_id`/`db_name`/`original_db_name`, `schema_id`/`schema_name`, `type_id`/`type_name`, `filegroup_id`/`filegroup_name`/`file_id`/`file_idex`/`file_name`, `current_timezone`/`current_timezone_id`, `getansinull`, `parsename` and `publishingservername`. A missing object/column/index/statistic yields `null`, like the native function.
* **Live state:** `current_request_id()` (`short?`, NULL outside a request), `current_transaction_id()`
  (`long?`, NULL outside a transaction), `xact_state()` (native `smallint` 0/1/-1), `ident_current(table)`
  (`decimal?`, the last identity of the table in **any** session — racy, not a last-insert API),
  `min_active_rowversion()` (native `binary(8)`, not a counter), `rowcount_big()` (the previous command on
  the same connection) and `scope_identity()` (`decimal?`, only in the same batch as the `INSERT`).
* **Conversion/logical:** `isdate(value)`/`isnumeric(value)` return the native T-SQL `int` (1/0), not a `bit`, so they stay integer values rather than materialised predicates, and `formatmessage(messageOrId, args...)` accepts at most 20 formatting arguments. `try_cast<T>(value)`/`try_convert<T>(value[, style])` (plus the `string`-target overloads over `nvarchar(4000)`) render the T-SQL `TRY_CAST`/`TRY_CONVERT` through a **closed** CLR→SQL destination map and are not version-gated: an invalid value is SQL NULL, a NULL style is SQL NULL even for a convertible value, and a target outside the map is refused before SQL. See [TRY_CAST and TRY_CONVERT](../../scalar-functions/04-conditionals-and-conversion.md#sql-server-try_cast-and-try_convert).

The portable BASE64 and crypto members are SQL Server 2025-only on this provider: `SqlFunctions.Sql.base64_encode` renders `BASE64_ENCODE(value)` over `varbinary` and `base64_decode` renders `BASE64_DECODE(value)` over `varchar` (cast an `nvarchar` column), while `crypt_gen_random` renders the typed `CASE WHEN length IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(length) END` wrapper so a NULL length stays a typed NULL. No compatibility level or preview flag is required.
* **JSON:** `json_array(value, ...)` and `json_object(key, value, ...)` (SQL Server 2022+),
  `json_objectagg(key, value)` and `json_arrayagg(value)` (SQL Server 2025+),
  `json_path_exists(json, path)` (SQL Server 2022+) and `json_contains(json, searchValue, path)`
  (SQL Server 2025+). The two predicates are materialised as `bit` values, mirroring `isjson`.

Live mutable state is **not** an exclusion criterion: the seven names that report live connection-,
transaction-, session- or statement-scope state are exposed as-is, with the native SQL Server semantics
and the caveats above. nextorm promises no per-row stability, no connection continuity between queries
and no identity-retrieval guarantee — a separate `SCOPE_IDENTITY`/`IDENT_CURRENT` call is not a
last-insert API, so use `ReturningIdentity<TKey>()` for the key. The stable configuration/identity
constants of the same scope (`CURRENT_TIMEZONE`/`CURRENT_TIMEZONE_ID`, `SERVER_NAME`, `HOST_ID`,
`HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) stay on the same surface.

## Null-safe comparison

SQL Server has no `IS [NOT] DISTINCT FROM` on the supported version floor, so both helpers are emulated
over ordinary equality as an always-two-valued guarded predicate:
`((a is not null and b is not null and a = b) or (a is null and b is null))`, with `is_distinct_from` as
its `not (...)` complement. A boolean projection is legalised as
`cast(case when ... then 1 else 0 end as bit)`. Because the operands are compared twice, a captured null
operand needs a SQL type derived from metadata; an unmapped one is rejected before execution with
`Null-safe comparison requires a SQL Server type mapping for null operands.` See
[Null-safe comparison](../../scalar-functions/04-conditionals-and-conversion.md#null-safe-comparison-is-not-distinct-from).

## Refused functions (deterministic, before SQL)

[#282](https://github.com/AlexeyShirshov/nextorm/issues/282) is closed for this slice:
**22 delivered / 0 delivery names remaining / 11 tested refusals**. The eleven refusals are part of the
surface — callable `SqlFunctions.SqlServer` members that throw `NotSupportedException` with one stable
message per member (`SqlFunctions.SqlServer.<member> (<TOKEN>) is refused: <reason>`) at the SQL-builder
command boundary, before SQL is rendered and before any command is created. They are not deferred names:
they are deterministic, tested refusals, and each keeps the supported siblings of the same surface
(`rowcount_big`, `xact_state`, `scope_identity`, `db_name`, `current_user`, …) rendering.

| Member | SQL token | Kind and reason |
|---|---|---|
| `session_context(key)` | `SESSION_CONTEXT` | Scope/CLR: returns `sql_variant`, for which nextorm has no faithful CLR result contract; no lossy cast substitution is performed. |
| `session_id()` | `SESSION_ID` | Engine rejection: not a SQL Server engine function (Msg 195); the documented function belongs to Azure Synapse. Session identity is `@@SPID` or `sys.dm_exec_sessions.session_id`; no substitution is performed. |
| `rowcount()` | `@@ROWCOUNT` | Scope: describes the previous statement and is statement-scoped; a per-row query projection cannot expose that value faithfully. |
| `error()` | `@@ERROR` | Scope: the same previous-statement scope as `@@ROWCOUNT`. |
| `error_number()` | `ERROR_NUMBER` | Scope: requires a `CATCH`-block scope; outside `CATCH` it returns NULL, and nextorm does not establish that scope. |
| `error_severity()` | `ERROR_SEVERITY` | Scope: the same `CATCH`-only scope. |
| `error_state()` | `ERROR_STATE` | Scope: the same `CATCH`-only scope. |
| `error_line()` | `ERROR_LINE` | Scope: the same `CATCH`-only scope. |
| `error_message()` | `ERROR_MESSAGE` | Scope: the same `CATCH`-only scope. |
| `error_procedure()` | `ERROR_PROCEDURE` | Scope: the same `CATCH`-only scope. |
| `any_value(value)` | `ANY_VALUE` | Engine rejection: SQL Server build `17.0.5005.3` rejected it with `Msg 195` in the aggregate and `OVER ()` forms, at compatibility 160/170 and with `PREVIEW_FEATURES = ON`; no supported SQL Server execution contract is established. |

The two kinds stay distinct: the ten system names are **scope/CLR limitations** (a statement or `CATCH`
scope, or `sql_variant` without a faithful CLR carrier) except `SESSION_ID`, which — like `ANY_VALUE` — is
an **observed engine rejection**. `ANY_VALUE` is not a version gap a later nextorm release can close, and
the portable `CommonFunctions.any_agg` is unaffected on the providers that enable it. See
[Limitations and out-of-scope features](../../advanced/limitations.md) and
[system functions](../../scalar-functions/09-system-functions.md#refused-statement-and-error-scope-names-sql-server).

## See also

* [SQL Server provider](../../connecting-providers/sqlserver.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs`.
