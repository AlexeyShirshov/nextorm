# Query sources

Use this page to choose the rows that a query starts from. Most queries start with a mapped entity:
`From<TEntity>()` selects its mapped table, `Where` filters rows, and `Select` defines the values to read.
This page covers source selection and source-specific options; use the linked guides for details about
individual query operators.

## Start with a mapped entity

For a typical query over a mapped table, start with `From<TEntity>()` and add the operators you need:

```csharp
var rows = dataContext.From<SimpleEntity>()
    .Where(it => it.Id > 8)
    .Select(it => new { it.Id })
    .ToList();
```

`Where` keeps rows with `Id` greater than 8, `Select` returns only the `Id` value, and `ToList` runs
the query and returns those values in a list.

Choose another `From` overload when your rows come from a subquery, raw SQL, a common table expression
(CTE), a temporary table, or a table-valued function.

## Expose typed sources from an application context

To expose named entity sources such as `app.Orders` and use them with `From` or `Join`,
see [Repositories and application context](37-repositories.md).

## Query sources at a glance

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) has an overload for each source type. Every overload returns an [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1), so you can compose the same query operators (`Where`, `Join`, `GroupBy`, `Having`, `OrderBy`, `Limit`, and `Select`) with any source, subject to the source and provider limitations documented for each operator:

| Source | Entry point |
|---|---|
| Mapped entity (its table from metadata) | [`From<TEntity>()`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) |
| Raw table or CTE name (via [`TableAlias`](xref:NextORM.Core.TableAlias) accessors) | [`From(string)`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) |
| Subquery / already-composed query | [`From(QueryCommand<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.QueryCommand{``0})), [`From(EntityBuilder<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.EntityBuilder{``0})) |
| Raw SQL fragment | [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) — [Raw SQL](14-raw-sql.md#compositing-raw-sql-as-a-from-source) |
| Typed CTE | [`From(Cte<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.Cte{``0})), [`From(CteReference<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.CteReference{``0})), declared with [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand)) / [`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})) — [CTEs](10-cte.md) |
| Lazy temporary table | [`From(TempTableSource<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.TempTableSource{``0})) — [`AsTempTable`](20-create-table-as.md) |
| Table-valued function (runtime factory) | [`From(() => ...)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})) — [Table-valued functions](13-table-valued-functions.md) |

Each source also accepts a `FromOptions` callback ([`FromOptions`](xref:NextORM.Core.FromOptions)) for source-specific modifiers such as `TABLESAMPLE`/`SAMPLE` and table or index hints. See [Table sampling](#table-sampling-tablesample) for an example. The `CreateQueryBuilder*` methods provide matching entry points for these overloads; see the [API reference](../advanced/api-reference.md) for their signatures. To change the table or SQL source for one query, see [Per-query source overrides](#per-query-source-overrides).

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
select id
 from (select id
 from simple_entity where (id > 8))
```

Output:

| Id |
|----|
| 9 |
| 10 |

Using a query as a source takes two steps. `From(inner)` composes the inner query as a derived table; it
does not read the rows. Add an outer `Select` to request columns exposed by the inner projection, as the
example does.

`From(inner).ToList()` without an outer `Select` is accepted only when the inner projection already
contains resolved column references: a mapped column, a renamed projected member, or a computed scalar
read through its output name. A projection such as `new { it.Id }` or `new Dto { Id = it.Id }` contains
row-member expressions and is rejected during preparation with a `QueryPreparationException`. This rule
depends on the **expression shape** of the inner projection, not the CLR type of its result.

The outer-selection form works with member-init DTOs and positional-record projections. A tested
constructor-only class projection has a known pre-existing failure (`Cannot find inner column Id`), so
use one of the supported forms instead. See
[Limitations](../advanced/limitations.md#limitations). See
[Subqueries](07-subqueries.md#subquery-as-a-from-source) for the construction/materialization split.

A derived source may also project a computed scalar instead of a named member. The enclosing query
addresses that value through the output column the derived select list exposes, so it can be ordered,
filtered and projected like any other column:

```csharp
var inner = dataContext.From<SimpleEntity>()
    .Where(it => it.Id <= 2)
    .Select(it => it.Id * 2);

var rows = dataContext.From(inner)
    .OrderBy(x => x)
    .Select(x => x * 3)
    .ToList();
```

```sql
-- SQLite
select (c0 * 3)
 from (select (id * 2) as 'c0'
 from simple_entity
 where (id <= 2)) as 't1'
 order by t1.c0
```

Output: `6`, `12`.

> The generated SQL names the computed column `c0`. This alias isn't part of the public API. Read the
> value through the builder (as above) instead of hard-coding the alias; its spelling depends on the
> provider and may change.

## Table function as source

`dataContext.From(() => ...)` uses a table-valued function (TVF)—a database function that returns rows—as
the `FROM` source. The lambda carries the function call and its arguments (see
[Table-valued functions](13-table-valued-functions.md)). The built-in helpers cover the common
set-returning functions; `SqlFunctions.Postgres.unnest` flattens a PostgreSQL array into one row per
element:

```csharp
var elements = dataContext
    .From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

```sql
select unnest as "Value"
 from unnest(@norm_p0) as "t1"
```

If the fully qualified `SqlFunctions.*` names feel too verbose, import the surface with
`using static NextORM.Core.SqlFunctions;` and drop the class prefix:

```csharp
using static NextORM.Core.SqlFunctions;

var elements = dataContext
    .From(() => Postgres.unnest(Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`SqlFunctions.Postgres.generate_series(start, stop)` generates a numeric series the same way. The
built-ins are gated by the provider ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))),
and a custom function is declared as a `TvfEntityBuilder<T>` factory; see
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
select id
 from simple_entity tablesample system (10) repeatable (42)

-- SQL Server
select id
 from simple_entity tablesample (10 percent) repeatable (42)
```

PostgreSQL supports both `System` and [`Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli);
SQL Server supports only `System`. Every other provider throws `NotSupportedException` when the SQL is
built ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample) and
[`ITableSampleMethods.Render`](xref:NextORM.Core.ITableSampleMethods.Render(NextORM.Core.TableSampleMethod,System.Double,System.Nullable{System.Double},NextORM.Core.KeywordCase))). The modifier applies to the query's
primary table only.

## Per-query source overrides

By default, a query reads the table mapped with `EntityMetadataBuilder<T>.Table(...)` (or the name passed
to `From("table")`). The `With*` methods on [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1)
change which table or SQL expression this query reads from; the metadata and every other query stay
unchanged:

```csharp
public EntityBuilder<TEntity> WithTableName(string name);
public EntityBuilder<TEntity> WithSchema(string schema);
public EntityBuilder<TEntity> WithDatabase(string database);
public EntityBuilder<TEntity> WithServer(string server);
public EntityBuilder<TEntity> WithTableExpression(string sql);
```

```csharp
var rows = await dataContext.From<IOrder>()
    .WithSchema("sales")
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id
 from sales.orders
```

The provider determines which schema, database, and server name prefixes it supports. SQL generation
throws `NotSupportedException` for a prefix the provider cannot express:

| Provider | `WithSchema` | `WithDatabase` | `WithServer` |
|---|---|---|---|
| PostgreSQL | `schema.table` | rejected | rejected |
| SQL Server | `schema.table` | `database.schema.table` | `server.database.schema.table` |
| MySQL / MariaDB | `db.table` (schema = database) | `db.table` | rejected |
| SQLite | `db.table` (attached database) | `db.table` | rejected |
| ClickHouse | `db.table` | `db.table` | rejected |
| In-memory | rejected | rejected | rejected |

On MySQL, MariaDB, ClickHouse, and SQLite, schema and database names use the same single qualifier, so
you can set only one of them. When identifier quoting is enabled, the provider quotes each part
separately (see [`WithQuotedIdentifiers`](xref:NextORM.Core.EntityBuilder`1.WithQuotedIdentifiers(System.Boolean))):
`[srv].[db].[sales].[orders]`, `` `db`.`orders` ``, `"sales"."orders"`.

[`WithTableExpression`](xref:NextORM.Core.EntityBuilder`1.WithTableExpression(System.String)) wraps a raw SQL
fragment as a derived source (`(sql) AS alias`) and cannot be combined with the name qualifiers. The
fragment is emitted verbatim, so pass only trusted SQL. As with `WithSql`, you are responsible for its
validity and injection safety:

```csharp
var rows = await dataContext.From<IOrder>()
    .WithTableExpression("select id from orders_2025 union all select id from orders_2026")
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- PostgreSQL
select id
 from (select id from orders_2025 union all select id from orders_2026) as "t1"
```

The override is part of the query-plan cache key, so two queries that differ only in the override never
share a cached plan. Without an override, the generated SQL is unchanged. The in-memory provider has no
SQL source to rewrite and rejects every override.

