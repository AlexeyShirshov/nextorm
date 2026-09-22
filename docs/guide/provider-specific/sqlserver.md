# SQL Server-specific SQL

> SQL Server contributes the `CHOOSE` conditional function, statement/table hints and the `FOR JSON`/
> `FOR XML` result shape, the scalar XML data-type methods (`value`/`query`/`exist`), the native
> `PIVOT`/`UNPIVOT` source constructs, plus `string_split`/`openjson` table functions.
> `CONTAINSTABLE` and the rowset XML method `.nodes` are the remaining SQL Server surfaces not part
> of nextorm.

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

See [Scalar functions](../11-scalar-functions.md#conditional-helpers).

## Hints

[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1) attaches a table hint to the query's `FROM` table
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
[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1), so `.WithTableHint("rowlock").ForUpdate()`
renders `with (rowlock, updlock)`. See
[Row locking](../01-querying-and-projections.md#row-locking-for-update--for-share).

## `FOR JSON` and `FOR XML`

`ForJson`/`ForXml` shape the result as JSON or XML
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
they are mutually exclusive. See [JSON support across providers](../18-json.md).

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

The rowset method `.nodes` is not supported: it needs an outer `FROM`/`CROSS APPLY` reference.

See [Scalar functions](../11-scalar-functions.md#xml-data-type-methods).

## Table-valued functions

`string_split(...)` and `openjson(...)` are exposed through
[`FromTableFunction`](xref:NextORM.Core.EntityBuilder`1). See
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
[`Unpivot`](xref:NextORM.Core.EntityBuilder`1) stacks columns into rows with `UNPIVOT`
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

[`TableSample`](xref:NextORM.Core.EntityBuilder`1) adds a `TABLESAMPLE` modifier to the
query's primary table ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample)); an
optional seed makes the sample repeatable. SQL Server renders `tablesample (10 percent)` /
`tablesample (10 percent) repeatable (3)`
(`ITableSampleMethods.Render`).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .TableSample(10, TableSampleMethod.System, seed: 3)
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

## Not yet supported

The rowset XML method `.nodes` (which needs an outer `FROM`/`CROSS APPLY`
reference) is tracked in the backlog. See [Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [SQL Server provider](../../providers/sqlserver.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
