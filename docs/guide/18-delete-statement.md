# Data modification (DELETE)

> To delete rows from a query against one mapped table, specify `Where(...)`, a positive `Limit(...)`, or both, and call `Delete()` or `DeleteAsync()`. Use a delete builder when you need to inspect the SQL, return deleted rows, or explicitly delete the whole table with `All()`.

**Prerequisites:** [Data modification (INSERT)](17-insert-statement.md) · [Filtering (WHERE)](03-filtering-where.md) · [Joins](04-joins.md) · [Provider overview](../connecting-providers/overview.md)

## Choose a delete operation

- To delete rows that match a condition, use [Deleting rows by predicate](#deleting-rows-by-predicate).
- To cap how many matching rows are deleted, use [Delete a bounded set of rows](#delete-a-bounded-set-of-rows).
- To delete every row, mark the operation explicitly with [`All()`](#deleting-every-row).
- To clear a table in multiple statements, see [Clear a table in batches](#clear-a-table-in-batches).
- To delete one entity by its key, use [Deleting by key](#deleting-by-key).
- To return the removed rows, see [Returning the removed rows](#returning-the-removed-rows).
- To delete rows selected by a join or clear a table, see [Delete based on a join](#delete-based-on-a-join) or [Truncating a table](#truncating-a-table).

Each terminal issues one command. nextorm does not track changes and does not provide `SaveChanges`.

## Deleting rows by predicate

Use `Where(...).Delete()` or `Where(...).DeleteAsync()` when you already have a
query and only need the number of affected rows. Start the query with
`ctx.From<TEntity>()` or a repository such as `app.Orders`:

```csharp
var deleted = ctx.From<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Delete();

await ctx.From<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .DeleteAsync(cancellationToken);
```

```sql
-- PostgreSQL
delete from simple_entity where id = 1
delete from simple_entity where (age > 10)
```

`Where` accepts the same predicate expressions as a query (`From<T>().Where(...)`). Repeated calls
combine the predicates with `and`.

Use an explicit delete builder when you need `.ToSql()` to inspect the generated
DELETE without executing it, `.Returning(...)` to get the deleted rows on
supported providers, or `.All()` to delete every row in one statement. Start the
builder with `ctx.CreateDeleteBuilder<TEntity>()` or `app.Orders.MakeDelete()`.

* Values captured in the predicate become parameters (`x => x.Id == id` renders `id = @id`); inline literals are emitted verbatim, exactly as in a query `WHERE`.
* `Delete()`/`DeleteAsync()` return the affected-row count (`0` when nothing matched). ClickHouse reports no count (its mutation returns none).

## Delete a bounded set of rows

The examples use `app.Orders` from an application context. To set it up, see
[Repositories and application context](37-repositories.md).

Start from one mapped table and set a positive `Limit`. Add `Where` to restrict
which rows can be deleted. Global filters still apply unless you disable them
with `IgnoreFilters`. Without either `Where` or a positive limit, the query
terminal throws `InvalidOperationException`, even if global filters are active.

To delete up to 100 archived orders when any matching rows will do, call
`Delete()` directly on the query:

```csharp
var deleted = app.Orders.Where(o => o.IsArchived)
    .Limit(100)
    .Delete();
```

To delete the oldest archived orders first, break timestamp ties with the
unique order ID:

```csharp
var deleted = await app.Orders.Where(o => o.IsArchived)
    .OrderBy(o => o.ArchivedAt)
    .ThenBy(o => o.Id)
    .Limit(100)
    .DeleteAsync(cancellationToken);
```

Declare a unique, non-null key that matches the database schema; composite keys
are supported. A bounded delete selects the keys and deletes the rows in one
server statement, without fetching entities or keys to the client. It doesn't
strengthen database isolation. An unbounded predicate delete doesn't need a key.

Use a positive limit. Explicit `Limit(0)` is rejected for DELETE even though it
means no limit for SELECT. Bounded DELETE supports PostgreSQL, SQL Server,
MySQL, MariaDB, and SQLite (no optional SQLite DELETE LIMIT extension needed).
ClickHouse rejects bounded deletes; the in-memory provider doesn't support
mutations. Use the separate joined-delete API for joins; it doesn't support
bounded deletion. Short DELETE also rejects derived sources, projections, and
options such as positive offset and `WithTies` instead of ignoring them.

## Deleting every row

To delete every row, add the explicit `All()` marker. This makes a full-table delete deliberate.
Combining `Where` and `All` throws `InvalidOperationException`:

```csharp
ctx.CreateDeleteBuilder<ISimpleEntity>().All().Delete();   // delete from simple_entity
```

## Clear a table in batches

To clear an unsharded table on a [provider that supports bounded DELETE](#delete-a-bounded-set-of-rows),
repeat a limited delete until it reports zero affected rows. Omit `Where` and
use `IgnoreFilters()` to include rows hidden by global query filters, such as
tenant or soft-delete filters. `All()` cannot be combined with `Limit()`:

```csharp
const int batchSize = 1_000;

while (true)
{
    var deleted = await app.Orders.Limit(batchSize)
        .IgnoreFilters()
        .DeleteAsync(cancellationToken);

    if (deleted == 0)
        break;
}
```

Each iteration issues one DELETE. Run without an enclosing transaction if each
batch should commit separately. Pause concurrent writes if the table must be
empty when the cleanup finishes.

## Deleting by key

To delete an entity by key, pass it to the context's `Delete` extension. The operation uses the
entity's declared key (`[Key]`/`.Key()`) and binds the key equality as a parameter:

```csharp
ctx.Delete(new SimpleEntity { Id = 1 });          // delete from simple_entity where id = @p0
await ctx.DeleteAsync(new SimpleEntity { Id = 1 });
```

```sql
-- PostgreSQL
delete from simple_entity where id = @p0
```

## Sharded entities

A sharded delete must name the bucket: the statement takes its scope from an equality on the shard key in
the predicate, or from the entity in the key form. A predicate without that equality — and a full-table
`All()` delete — throws before SQL instead of naming the default table:

```csharp
ctx.CreateDeleteBuilder<Order>().Where(x => x.Tenant == tenant).Delete();   // routed by the predicate
ctx.Delete(new Order { Id = 1 });                                          // routed by the instance
ctx.CreateDeleteBuilder<Order>().All().ToSql();                            // throws: no bucket to name
```

The bucket table, its shard and the placement rules are described in
[Sharding and horizontal partitioning](35-sharding-and-horizontal-partitioning.md).

## Global query filters

A [global query filter](../advanced/query-filters.md) declared for the deleted entity is combined with
the statement's `WHERE` condition:

* predicate form — `delete from ... where <predicate> and <filter>`;
* key form — `delete from ... where <pk> = @p and <filter>`, so `ctx.Delete(entity)` removes nothing when
  the filter excludes that key.

`All()` explicitly deletes every row and is **never** filtered. A joined delete filters the target (the
first table) and every joined source.

[`DeleteBuilder<TEntity>`](xref:NextORM.Core.DeleteBuilder`1) provides four `IgnoreFilters` overloads:
ignore all filters, filters for an entity type, a filter key, or the intersection of a key and type. The
call changes builder state, and repeated calls accumulate:

```csharp
ctx.CreateDeleteBuilder<Document>()
    .IgnoreFilters(["soft-delete"])   // keep tenant scoping, drop soft-delete
    .Where(d => d.IsDeleted)
    .Delete();
```

See [UPDATE and DELETE (DML)](../advanced/query-filters.md#update-and-delete-dml) for the full matrix.

## Provider support at a glance

| Provider | `DELETE` | `RETURNING` / `OUTPUT` | `TRUNCATE` | `DELETE ... USING` / join |
|---|---|---|---|---|
| PostgreSQL | yes | `RETURNING` | yes | `USING` |
| SQL Server | yes | `OUTPUT deleted.<col>` | yes | `DELETE <alias> FROM ... JOIN` |
| SQLite | yes | `RETURNING` | `NotSupportedException` | `NotSupportedException` |
| MySQL / MariaDB | yes | `NotSupportedException` | yes | `DELETE <alias> FROM ... JOIN` |
| ClickHouse | `ALTER TABLE ... DELETE` | `NotSupportedException` | yes | `NotSupportedException` |
| In-memory | `NotSupportedException` (query-only) | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` |

## Returning the removed rows

To read the rows that the command removes, use `Returning()` or `Returning(projection)`. The provider
uses its native `RETURNING`/`OUTPUT` form; read the result with `Single()` or `ToList()`:

```csharp
var removed = ctx.CreateDeleteBuilder<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .Returning(x => new { x.Id, x.Name })
    .ToList();

var one = ctx.CreateDeleteBuilder<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Returning()
    .Single();
```

```sql
-- PostgreSQL
delete from simple_entity where (age > 10) returning id, name
delete from simple_entity where id = 1 returning id, name, age
```

| Provider | Form |
|---|---|
| SQLite, PostgreSQL | `DELETE ... RETURNING <cols>` |
| SQL Server | `DELETE ... OUTPUT deleted.<col> ...` |
| MySQL, MariaDB, ClickHouse, in-memory | `NotSupportedException` |

`Returning()` returns the whole mapped entity, which can be a class or a
[generated interface entity](../getting-started/03-entities-and-metadata.md#query-an-interface-entity).
`Returning(x => ...)` returns the projected shape (a single member, an anonymous type or a
member-init). `Single()` throws when the delete removed no row or more than one.

To capture the removed rows for a later statement of the same batch instead of returning them to the client, register a batch-scoped capture — the `deleted.*` columns **are** the captured rows — and compose a query over it with `FromOutput<TResult>`/`FromOutputTable`: see [Batch-scoped capture](17-insert-statement.md#batch-scoped-capture-batchbuilderoutputintotablevariable). SQL Server only.

## Truncating a table

To run the provider's native `TRUNCATE TABLE`, call `Truncate()` or
`TruncateAsync()` on the repository. Use `CreateTruncateBuilder<TEntity>()` if
you're working directly with the provider context or need to inspect the SQL:

```csharp
app.Orders.Truncate();
ctx.CreateTruncateBuilder<ISimpleEntity>().Execute();
```

These operations return the driver's non-query result (`int`, or `Task<int>` for
the async methods). Don't use it as a count of removed rows. SQL Server,
PostgreSQL, MySQL, MariaDB, and ClickHouse support `TRUNCATE TABLE`. SQLite and
the in-memory provider throw `NotSupportedException`.

## Delete based on a join

Use `Delete()` or `DeleteAsync()` at the end of a joined query to delete matching rows. The first table
in the chain is the target. The delete matches the join condition and any `Where` predicates. Only INNER
joins are supported.

```csharp
var removed = await ctx.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Where(p => p.Item2.String == "archived")
    .DeleteAsync(cancellationToken);
```

```sql
-- PostgreSQL
delete from simple_entity as "t1" using complex_entity as "t2" where cast(t1.id as bigint) = t2.id and t2.somestring = 'archived'
```

| Provider | Rendered form |
|---|---|
| PostgreSQL | `DELETE FROM <t> AS a USING <u> AS b WHERE ...` |
| SQL Server | `DELETE a FROM <t> AS a JOIN <u> AS b ON ... WHERE ...` |
| MySQL, MariaDB | ``DELETE `a` FROM <t> AS `a` JOIN <u> AS `b` ON ... WHERE ...`` |
| SQLite, ClickHouse, in-memory | `NotSupportedException` |

* The target is the first table (`Item1`); only `Join` (INNER) is accepted. `LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin` and the `APPLY` joins throw `NotSupportedException`, because they change which rows are deleted.
* The terminals are extension methods on the joined builder for arities 2–8; `Delete()`/`DeleteAsync()` return the affected-row count. `ToSql()` renders the statement without opening a connection and throws on an in-memory context.
* `CreateDeleteJoinBuilder()` starts the row-returning multi-table delete. Its `Returning()` and
  `Returning(projection)` terminals are available only on PostgreSQL (`DELETE ... USING ... RETURNING`),
  for INNER joins, and for arities 2–8.
  * `Returning()` is equivalent to `Returning(p => p)` and returns the whole joined projection
    (`Projection<T1, ...>`): every returnable mapped property from every item slot, in source order.
    `Item1` is the delete target. If the same CLR type appears more than once, each slot remains
    distinct, so a self-join keeps its `Item1` and `Item2` values separate.
  * `Returning(p => new { p.Item1.Id, p.Item2.Name })` returns the explicit projection; read it with
    `Single()` or `ToList()`. Returned columns receive deterministic aliases per slot, and explicit
    projections remain unchanged. `Returning()` does not add a `RETURNING` list implicitly.
  * The returned-row form works as a normal terminal and as a data-modifying CTE body through
    `With(name, delete)`. See [Common table expressions](10-cte.md).
  * nextorm rejects a returned item whose mapped property is a multi-column `Range<T>`. It also rejects
    a required member with no matching member in the joined source shape and names the slot and member.
* A joined side may be a CTE, whose declaration is hoisted before the `DELETE` (PostgreSQL `with c as (…) delete from <t> as "t1" using c as "t2" where …`); the target stays the first physical table and the CTE may sit on any join position. See [Common table expressions](10-cte.md).

```csharp
var all = await ctx.From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .Where(p => p.Item2.Tier == "gold")
    .CreateDeleteJoinBuilder()
    .Returning()          // equivalent to .Returning(p => p)
    .ToListAsync(cancellationToken);
```

### DELETE as a data-modifying CTE body (PostgreSQL)

On PostgreSQL a row-returning joined `DELETE` can be the body of a data-modifying CTE: pass it to
`With(name, delete)` and read the removed rows typed through `From(name)`:

```csharp
var doomed = dataContext
    .From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.Name });

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { r.OrderId, r.CustomerName })
    .ToList();
```

```sql
-- PostgreSQL
with del as (delete from orders as "t1" using customers as "t2" where t1.customer_id = t2.id returning t1.id as "OrderId", t2.name as "CustomerName") select "OrderId", "CustomerName"
 from del as "t1"
```

A `RETURNING` projection is required (a side-effect-only mutation is out of scope), and such a statement
is never plan-cached. See [Common table expressions](10-cte.md).

### Delete based on a CTE

```csharp
var e = ctx.From<IComplexEntity>();
var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

var removed = e
    .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
    .Delete();
```

```sql
-- PostgreSQL
with c as (select id
 from complex_entity where (id > 0)) delete from complex_entity as "t1" using c as "t2" where t1.id = cast(t2.id as bigint)
```

The `with …` is emitted before the mutation on every provider with a multi-table delete, and the CTE may sit on any join position. Two different CTEs declared with the same name on the two sides of a join are rejected with `InvalidOperationException`. See [Common table expressions](10-cte.md).

## Notes and out-of-scope

* The in-memory context is query-only: `Delete`/`DeleteAsync`/`CreateTruncateBuilder` (like every other write) throw `NotSupportedException`; query your own collections instead. `INSERT` on the in-memory provider is likewise out of scope by design.
* `DELETE` is not prepared or plan-cached — optimisation in nextorm targets read-only queries only (`PrepareBuffered`, the implicit plan cache, benchmarks); a mutation always renders and executes one command per call.
* There is deliberately no built-in soft-delete behaviour; soft delete is expressed with a [global query filter](../advanced/query-filters.md) plus an explicit `UPDATE`. A full `MERGE` with arbitrary branches is not part of this surface, and `UPDATE` lives in its own guide ([Data modification (UPDATE)](19-update-statement.md)).

## See also

- [Data modification (INSERT)](17-insert-statement.md)
- [Data merging (MERGE / upsert)](21-merge-statement.md)
- [Joins](04-joins.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../connecting-providers/overview.md)
- [API reference](../advanced/api-reference.md)
