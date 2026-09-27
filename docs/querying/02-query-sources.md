# Query sources

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
[Table-valued functions](../guide/11-table-valued-functions.md).

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

## Per-query source overrides

By default a query reads the table mapped with `EntityMetadataBuilder<T>.Table(...)` (or the name passed
to `From("table")`). The `With*` methods on [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1)
change the rendered source for **this query only**; the metadata and every other query are untouched:

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
select id from sales.orders
```

The schema/database/server qualifiers are provider-specific; the levels a provider cannot express are
rejected with `NotSupportedException` when the SQL is built:

| Provider | `WithSchema` | `WithDatabase` | `WithServer` |
|---|---|---|---|
| PostgreSQL | `schema.table` | rejected | rejected |
| SQL Server | `schema.table` | `database.schema.table` | `server.database.schema.table` |
| MySQL / MariaDB | `db.table` (schema = database) | `db.table` | rejected |
| SQLite | `db.table` (attached database) | `db.table` | rejected |
| ClickHouse | `db.table` | `db.table` | rejected |
| In-memory | rejected | rejected | rejected |

On MySQL, MariaDB, ClickHouse and SQLite the schema and the database name the same single qualifier, so
only one of the two may be set. The parts are quoted with the provider's delimiter when identifier
quoting is enabled (see [`WithQuotedIdentifiers`](xref:NextORM.Core.EntityBuilder`1.WithQuotedIdentifiers(System.Boolean))),
each part separately: `[srv].[db].[sales].[orders]`, `` `db`.`orders` ``, `"sales"."orders"`.

[`WithTableExpression`](xref:NextORM.Core.EntityBuilder`1.WithTableExpression(System.String)) replaces the table access with a raw SQL
expression rendered as a derived source (`(sql) AS alias`) and cannot be combined with the name
qualifiers. The fragment is emitted verbatim, so pass only trusted SQL (the caller owns its validity and
injection safety, as with `WithSql`):

```csharp
var rows = await dataContext.From<IOrder>()
    .WithTableExpression("select id from orders_2025 union all select id from orders_2026")
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- PostgreSQL
select id from (select id from orders_2025 union all select id from orders_2026) as "t1"
```

The override is part of the plan-cache key, so two queries that differ only in the override never share a
cached plan; without an override the generated SQL is exactly as before. The in-memory provider has no SQL
source to rewrite and rejects every override.

