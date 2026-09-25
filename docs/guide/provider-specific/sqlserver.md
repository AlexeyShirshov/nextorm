# SQL Server-specific SQL

> SQL Server contributes the `CHOOSE` conditional function, the T-SQL-only scalar library
> (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `UNICODE`, `NCHAR`, `FORMAT`, the
> trigonometric functions, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID` and the SQL/JSON
> constructors/aggregates), statement/table hints and the `FOR JSON`/`FOR XML` result shape, the XML
> data-type methods (`value`/`query`/`exist` and the `nodes` rowset), the native `PIVOT`/`UNPIVOT`
> source constructs, plus `string_split`/`openjson` table functions.

**Prerequisites:** [Querying and projections](../01-querying-and-projections.md) · [SQL Server provider](../../providers/sqlserver.md)

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
select choose(2, 'a', 'b', 'c') as [Label] from complex_entity
```

See [Scalar functions](../../scalar-functions/04-conditionals-and-conversion.md#conditional-helpers).

## Hints

[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])) attaches a table hint to the query's `FROM` table
([`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints)), and a query hint renders the
trailing `OPTION (...)` clause, for example `OPTION (RECOMPILE)`
([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints)); the CTE `maxRecursion`
option maps to `option (maxrecursion n)`. See [Query hints](../17-query-hints.md).

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
select id from simple_entity with (updlock)
```

`ForUpdate` renders `updlock` (update lock held to the end of the transaction) and `ForShare` renders
`holdlock` (shared lock). The lock hint combines with
[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])), so `.WithTableHint("rowlock").ForUpdate()`
renders `with (rowlock, updlock)`. See
[Row locking](../01-querying-and-projections.md#row-locking-for-update--for-share).

## `FOR JSON` and `FOR XML`

`ForJson`/`ForXml` are terminals that execute the query and return the whole result set as one JSON or
XML document
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
`WithForJson`/`WithForXml` attach the clause without executing; the two clause kinds are mutually
exclusive. See [JSON support across providers](../18-json.md).

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
from xml_entity as [t1] cross apply t1.payload.nodes('/root/item') as [t2](value)
```

See [Scalar functions](../../scalar-functions/07-json-and-xml.md#xml-data-type-methods-sql-server).

## Table-valued functions

`string_split(...)` and `openjson(...)` are exposed through
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})). See
[Table-valued functions](../13-table-valued-functions.md) and
[JSON support across providers](../18-json.md).

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
select id from simple_entity for system_time as of '2020-01-01 00:00:00'
```

MariaDB supports the same clause on system-versioned tables, except `CONTAINED IN`.

## `PIVOT` / `UNPIVOT`

[`EntityBuilder<TEntity>.Pivot`](xref:NextORM.Core.EntityBuilder`1) reshapes a source into columns with
the native T-SQL `PIVOT` operator and
[`Unpivot`](xref:NextORM.Core.EntityBuilder`1.Unpivot(System.String,System.String,NextORM.Core.UnpivotColumn[])) stacks columns into rows with `UNPIVOT`
([`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot) /
[`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot)). The result is an untyped source:
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
        Q1 = t.GetNullableDecimal("[1]"),
        Q2 = t.GetNullableDecimal("[2]")
    })
    .ToList();

var stacked = ctx.From<Quarterly>()
    .Unpivot("value", "quarter", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
    .Select(t => new { Quarter = t.GetString("quarter"), Value = t.GetNullableDecimal("value") })
    .ToList();
```

A derived query can be the pivoted source, which lets the `PIVOT` input carry joins and computed
aggregate/`FOR` columns (the equivalent of pivoting a CTE):

```csharp
var orderMargins = ctx.From<OrderDetail>()
    .Join(ctx.From<OrderHeader>(), (d, h) => d.OrderId == h.OrderId)
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
        Q1 = t.GetNullableDecimal("[1]"),
        Q4 = t.GetNullableDecimal("[4]")
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
select id from simple_entity tablesample (10 percent) repeatable (3)
```

SQL Server supports only the `System` method
(`TableSample`); `Bernoulli`
throws `NotSupportedException`. See
[Table sampling](../01-querying-and-projections.md#table-sampling-tablesample).

## Full-text search

The cross-provider boolean predicates `contains`/`freetext` render `CONTAINS`/`FREETEXT`. For ranking,
`SqlFunctions.SqlServer.containstable`/`freetexttable` expose the matched key and `RANK` score through
`SqlFunctions.IKeyRankRow<TKey>`; see
[Table-valued functions](../13-table-valued-functions.md#built-in-table-functions).

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
  `nchar(code)`, `format(value, format)` / `format(value, format, culture)`. `format` is the native
  T-SQL `FORMAT`, distinct from the CLR `string.Format` translation.
* **Numeric:** `acos`, `asin`, `atan`, `atn2(y, x)`, `cot`, `degrees`, `radians`, `pi()`, `square`.
* **Date/time:** `datename(datepart, date)` (the part is a constant) and
  `date_bucket(datepart, width, date[, origin])` (SQL Server 2022+).
* **Binary/system:** `hashbytes(algorithm, data)` (the algorithm is a constant such as `SHA2_256`) and
  `newsequentialid()`; the latter is valid only as a column `DEFAULT`, not in an ordinary `SELECT`.
* **JSON:** `json_array(value, ...)` and `json_object(key, value, ...)` (SQL Server 2022+),
  `json_objectagg(key, value)` and `json_arrayagg(value)` (SQL Server 2025+),
  `json_path_exists(json, path)` (SQL Server 2022+) and `json_contains(json, searchValue, path)`
  (SQL Server 2025+). The two predicates are materialised as `bit` values, mirroring `isjson`.

## Not yet supported

No SQL Server-specific surface remains open. See
[Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [SQL Server provider](../../providers/sqlserver.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
