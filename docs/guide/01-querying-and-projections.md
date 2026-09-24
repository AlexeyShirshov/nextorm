# Querying and projections

> Shape the result of a query with [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})): one column, an anonymous type, a DTO or record, a tuple, a member initialiser, a nested entity or a calculated column.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

`dataContext.From<TEntity>()` returns an [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Every query starts by projecting that
entity with [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

The lambda is not executed - it is translated into the `SELECT` list of the generated statement.
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) returns a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); the terminal ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])),
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) executes it. See [Sorting and paging](05-sorting-and-paging.md)
for the terminals and their async forms.

Rules that apply to every projection:

* The lambda's shape decides the select list. Columns that are not projected are not read.
* A projected member whose name matches the mapped column name (case-insensitive) is emitted without
  an alias. A renamed or calculated member is aliased: `as 'Calc'` on SQLite, `as [Calc]` on SQL
  Server, `as "Calc"` on PostgreSQL.
* Constructor arguments, tuple items and member-initialiser assignments all map to select-list
  entries; the runtime materialiser builds the object from the reader.
* On the in-memory provider no SQL exists; the same expression is compiled and run over the
  in-memory data set.

The `Output:` tables below show the rows returned by each example against the integration-test seed
data: `simple_entity` holds ids `1`-`10` and `complex_entity` three rows
(`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

## Anonymous type

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`entity.Id` maps to the `id` column of `simple_entity`, so no alias is emitted.

## Modified and calculated columns

A member can be computed from other columns or constants:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Id = entity.Id + 1 })
    .ToListAsync();
```

```sql
select (id + 1) as 'Id' from simple_entity
```

Output:

| Id |
|----|
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |
| 11 |

The arithmetic expression is parenthesised and, because it is not a plain column, it is aliased with
the projected member name. Naming the member keeps the alias stable for an enclosing query:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.Id + 1 })
    .ToListAsync();
```

```sql
select id, (id + 1) as 'Calc' from complex_entity
```

String members concatenate with the provider's concat operator (`||` on SQLite and PostgreSQL, `+` on
SQL Server):

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Display = it.String + "/" + it.RequiredString })
    .ToListAsync();
```

```sql
-- SQLite
select id, ((somestring || '/') || requiredstring) as 'Display' from complex_entity
```

Output:

| Id | Display |
|----|---------|
| 1 | dadfasd/sdf |
| 2 | xxx/asdfgoi |
| 3 | null |

## Columns by name

A mapped entity only exposes the columns declared on it. A column that has no property — for example
in a wide ClickHouse table — is projected with
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)):

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Region = SqlFunctions.Column<ulong>(entity, "region_id") })
    .ToListAsync();
```

```sql
-- SQLite
select region_id as 'Region' from simple_entity
```

The first argument must be the query lambda parameter (a source), and the name is matched against the
database column verbatim, so quoting follows the provider (`` `region_id` `` on ClickHouse and MySQL,
`"region_id"` on PostgreSQL and SQLite, `[region_id]` on SQL Server). The value is materialized as
`T`, so the type must be one the row reader supports. Unlike a mapped member, the column is not
validated against entity metadata: a misspelled name fails at the database.

The same accessor works in a predicate and on a joined projection:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Join(dataContext.From<ComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => SqlFunctions.Column<long>(p.Item2, "region_id") > 0)
    .Select(p => new { p.Item1.Id, Region = SqlFunctions.Column<long>(p.Item2, "region_id") })
    .ToListAsync();
```

For a source that has no entity type at all (`From("table")`), columns are read through
[`TableAlias`](xref:NextORM.Core.TableAlias) — see [Joins](03-joins.md) and [CTE](09-cte.md). The
in-memory provider has no column-name concept and rejects `SqlFunctions.Column`.

## DTO

A non-anonymous type is projected through its constructor:

```csharp
public class SimpleEntityDto(int id)
{
    public int Id { get; } = id;
}

var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new SimpleEntityDto(entity.Id))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Record

Positional records are projected by their constructor too:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`tbl.GetInt64("id")` already has the record member's type, so no conversion is added. When the projected
type is wider or narrower than the source column, nextorm renders the conversion as `cast(...)`.

## Tuple

```csharp
var rows = await dataContext.From("simple_entity")
    .Select(tbl => new Tuple<long>(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Item1 |
|-------|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Member initialiser

Instead of a constructor, a projection can use an object initialiser:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Primitive and scalar projection

[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) can return a single value instead of a row object:

```csharp
var ids = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id < 5)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select id from simple_entity where (id < 5)
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |

A boolean member works the same way:

```csharp
var flags = await dataContext.From<ComplexEntity>()
    .Where(it => it.Boolean == true)
    .Select(it => it.Boolean)
    .ToListAsync();
```

```sql
select b from complex_entity where b = 1
```

Output:

| Boolean |
|---------|
| true |

## Nested entity and calculated columns over a projection

A [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) can itself be used as the source of another query with
`dataContext.From(query)`, so an inner projection (including calculated columns) can be read and
projected again:

```csharp
var inner = dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.String + it.String });

var rows = await dataContext.From(inner)
    .Select(t => new { t.Id, t.Calc })
    .ToListAsync();
```

```sql
-- SQLite: no derived-table alias is required
select id, Calc from (select id, (somestring || somestring) as 'Calc' from complex_entity)
```

SQL Server requires the derived table to be aliased, and PostgreSQL likewise:

```sql
-- SQL Server
select id, Calc from (select id, (somestring + somestring) as [Calc] from complex_entity) as [t1]
```

## Subquery as source

`From(query)` also wraps a filtered query, which is the SQL `FROM` equivalent of a subquery:

```csharp
var inner = dataContext.From<SimpleEntity>()
    .Where(it => it.Id > 8)
    .Select(it => new { it.Id });

var rows = await dataContext.From(inner)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
-- SQLite
select id from (select id from simple_entity where (id > 8))
```

Output:

| Id |
|----|
| 9 |
| 10 |

## Table function as source

`dataContext.FromTableFunction(() => ...)` uses a table-valued function as the `FROM` source. The
built-in helpers cover the common set-returning functions; `SqlFunctions.Postgres.unnest` flattens a
PostgreSQL array into one row per element:

```csharp
var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

```sql
select unnest as "Value" from unnest(@norm_p0) as "t1"
```

If the fully qualified `SqlFunctions.*` names feel too verbose, import the surface with
`using static NextORM.Core.SqlFunctions;` and drop the class prefix:

```csharp
using static NextORM.Core.SqlFunctions;

var elements = dataContext
    .FromTableFunction(() => Postgres.unnest(Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`SqlFunctions.Postgres.generate_series(start, stop)` generates a numeric series the same way. The
built-ins are gated by the provider ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))),
and a user-defined function is declared with `[SqlTableFunction]`; see
[Table-valued functions](13-table-valued-functions.md).

## Table sampling (`TABLESAMPLE`)

[`FromOptions.TableSample`](xref:NextORM.Core.FromOptions.TableSample(System.Double,NextORM.Core.TableSampleMethod,System.Nullable{System.Double})) adds a `TABLESAMPLE` modifier to the
primary table, so the database reads only a percentage of its rows instead of scanning the whole table.
Sampling is a per-query source option, so it is configured in the `From` call. The percentage must be
in `(0, 100]`; the sampling method defaults to
[`TableSampleMethod.System`](xref:NextORM.Core.TableSampleMethod.System) and an optional seed makes the
sample repeatable:

```csharp
var rows = await dataContext.From<SimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, seed: 42))
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- PostgreSQL
select id from simple_entity tablesample system (10) repeatable (42)

-- SQL Server
select id from simple_entity tablesample (10 percent) repeatable (42)
```

PostgreSQL supports both `System` and [`Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli);
SQL Server supports only `System`. Every other provider throws `NotSupportedException` when the SQL is
built ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample) and
[`ITableSampleMethods.Render`](xref:NextORM.Core.ITableSampleMethods.Render(NextORM.Core.TableSampleMethod,System.Double,System.Nullable{System.Double},NextORM.Core.KeywordCase))). The modifier applies to the query's
primary table only.

## JSON output (SQL Server)

[`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) is a **terminal operator**: it executes the query and returns the
whole result set as one JSON document ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)). Because it is
terminal it applies no implicit `TOP 1`, so the document covers every row; the query element type is
irrelevant since the database returns a single document column:

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);
```

```sql
select id, somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) shapes the document from the projection aliases, [`Auto`](xref:NextORM.Core.ForJsonMode.Auto) from the table
structure. `ForJson` returns `null` when the query produces no rows (SQL Server returns SQL NULL for an
empty `FOR JSON` result). The clause is placed after `ORDER BY` and before a trailing `OPTION (...)`;
other providers throw `NotSupportedException`. Use
[`WithForJson`](xref:NextORM.Core.QueryCommand`1.WithForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean)) to only *attach* the clause and keep the command composable (for further
hints or SQL inspection).

## XML output (SQL Server)

[`ForXml`](xref:NextORM.Core.QueryCommand`1.ForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean,System.Object[])) is the XML counterpart and terminal ([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)); it supports
`RAW`, `AUTO`, `EXPLICIT` and `PATH`, with an optional row element name, a `ROOT('...')` wrapper and the
`ELEMENTS` flag:

```csharp
string? xml = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id })
    .ForXml(ForXmlMode.Raw, elementName: "row", root: "items", elements: true);
```

```sql
select id from complex_entity for xml raw('row'), root('items'), elements
```

Like `ForJson`, `ForXml` returns `null` for an empty result set, and
[`WithForXml`](xref:NextORM.Core.QueryCommand`1.WithForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean)) attaches the clause without executing. `FOR JSON` and `FOR XML` are mutually
exclusive; combining them throws `NotSupportedException`.

## Row locking (`FOR UPDATE` / `FOR SHARE`)

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) and
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) lock the selected rows until the surrounding
transaction ends. PostgreSQL, MySQL and MariaDB emit a trailing clause placed last, after `WHERE`,
`ORDER BY` and a page request; SQL Server attaches a `WITH (updlock)`/`WITH (holdlock)` table hint to
the primary source instead:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id > 5)
    .ForUpdate()
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from simple_entity where (id > 5) for update

-- SQL Server
select id from simple_entity with (updlock) where (id > 5)
```

`ForUpdate()` locks rows exclusively; [`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) takes a
shared lock — PostgreSQL renders `for share`, MySQL/MariaDB render `lock in share mode`, and SQL Server
renders `holdlock` (shared) versus `updlock` for `ForUpdate`. The clause is implemented by PostgreSQL,
MySQL, MariaDB and SQL Server ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock); SQL Server uses
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints));
every other provider throws `NotSupportedException` when the SQL is built.

Pass a [`LockWaitMode`](xref:NextORM.Core.LockWaitMode) to control what happens when another transaction
already holds the row: [`NoWait`](xref:NextORM.Core.LockWaitMode.NoWait) fails immediately instead of
waiting, and [`SkipLocked`](xref:NextORM.Core.LockWaitMode.SkipLocked) leaves the locked rows out of the
result — the standard way to build a queue or worker pool:

```csharp
var claimed = await dataContext.From<Job>()
    .Where(x => x.State == "pending")
    .ForUpdate(LockWaitMode.SkipLocked)
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from job where (state = 'pending') for update skip locked

-- SQL Server (READPAST approximates SKIP LOCKED)
select id from job with (updlock, readpast) where (state = 'pending')
```

PostgreSQL and MySQL append the trailing `nowait`/`skip locked` (`FOR UPDATE`/`FOR SHARE [NOWAIT | SKIP
LOCKED]`); a shared lock with a wait mode switches MySQL from `lock in share mode` to `for share`, because
`LOCK IN SHARE MODE` takes no lock option. MariaDB appends the mode to both `for update` and `lock in
share mode` (`NOWAIT` on 10.3+, `SKIP LOCKED` on 10.6+). SQL Server adds `nowait` or `readpast` to the
same table hint (`with (updlock, nowait)` / `with (updlock, readpast)`); `readpast` skips any locked row,
not only a row locked by another writer, so it approximates rather than exactly matches `SKIP LOCKED`.
[`Wait`](xref:NextORM.Core.LockWaitMode.Wait) is the default and keeps the blocking behaviour.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Column aliases are single-quoted (`as 'Calc'`); derived tables do not require an alias. |
| SQL Server | Column aliases are bracket-quoted (`as [Calc]`); every derived table must be aliased (`as [t1]`). String concatenation uses `+`. |
| PostgreSQL | Column aliases are double-quoted (`as "Calc"`); derived tables must be aliased (`as "t1"`). |
| MySQL | Column aliases are backtick-quoted (`` as `Calc` ``); every derived table must be aliased (`` as `t1` ``). String concatenation uses `concat(a, b)`. |
| MariaDB | Same as MySQL: backtick-quoted aliases, aliased derived tables and `concat(a, b)` concatenation. |
| ClickHouse | Column aliases are backtick-quoted (`` as `Calc` ``); every derived table must be aliased (`` as `t1` ``). String concatenation uses `concat(a, b)`. |
| In-memory | No SQL is generated; the projection delegates are compiled and run over in-memory objects. |

## See also

* [Filtering (WHERE)](02-filtering-where.md)
* [Sorting and paging](05-sorting-and-paging.md)
* [Table-valued functions](13-table-valued-functions.md)
* [Entities and metadata](../getting-started/03-entities-and-metadata.md)
* [Provider overview](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:26`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:45`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:75`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:94`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:103`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:112`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:122`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:352`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:367`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:382`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:727`;
`tests/nextorm.integration.tests/TestModels.cs:3`;
`tests/nextorm.integration.tests/TestModels.cs:12`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:217`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:242`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:268`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:223`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1117`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1132`.
