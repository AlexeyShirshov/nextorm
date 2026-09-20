# SQL Server-specific SQL

> SQL Server contributes the `CHOOSE` conditional function, statement/table hints and the `FOR JSON`/
> `FOR XML` result shape, plus `string_split`/`openjson` table functions. Temporal-table,
> `CONTAINSTABLE`, `PIVOT`/`UNPIVOT` and XML data-type surfaces are not part of nextorm yet.

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

## `FOR JSON` and `FOR XML`

`ForJson`/`ForXml` shape the result as JSON or XML
([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)/[`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml));
they are mutually exclusive. See [JSON support across providers](../18-json.md).

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

## Not yet supported

`CONTAINSTABLE`/`FREETEXTTABLE` (full-text with a `RANK` column, which references the base table by
name), `PIVOT`/`UNPIVOT`, and the XML data-type methods (`nodes`, `value`, `query`) are tracked in the
backlog. See [Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [SQL Server provider](../../providers/sqlserver.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
