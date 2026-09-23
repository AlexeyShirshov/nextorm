# Data modification (DELETE)

> nextorm removes rows with the same explicit-command model as `INSERT`: [`DeleteFrom<TEntity>()`](xref:NextORM.Core.DataContextExtensions.DeleteFrom``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) builds a parameterised `DELETE FROM <table> [WHERE ...]`, the context extension [`Delete<TEntity>(entity)`](xref:NextORM.Core.DataContextExtensions.Delete``1(NextORM.Core.IDataContext,``0)) deletes by the entity's declared key, and a joined query can end in a multi-table delete. There is no change tracking and no `SaveChanges`: every terminal issues exactly one command.

**Prerequisites:** [Data modification (INSERT)](19-insert-statement.md) · [Filtering (WHERE)](02-filtering-where.md) · [Joins](03-joins.md) · [Provider overview](../providers/overview.md)

## Deleting rows by predicate

[`DeleteFrom<TEntity>()`](xref:NextORM.Core.DataContextExtensions.DeleteFrom``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) removes rows and returns the number of deleted rows:

```csharp
var deleted = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Delete();

await ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .DeleteAsync(cancellationToken);
```

`Where` accepts the same predicate expressions as a query (`From<T>().Where(...)`), and repeating it combines the predicates with `and`. `DeleteFrom<T>().ToSql()` renders the statement without opening a connection.

* Values captured in the predicate become parameters (`x => x.Id == id` renders `id = @id`); inline literals are emitted verbatim, exactly as in a query `WHERE`.
* `Delete()`/`DeleteAsync()` return the affected-row count (`0` when nothing matched). ClickHouse reports no count (its mutation returns none).

## Deleting every row

Deleting every row requires the explicit `All()` marker, so an unfiltered full-table delete cannot be written by accident. Combining `Where` and `All` throws `InvalidOperationException`:

```csharp
ctx.DeleteFrom<ISimpleEntity>().All().Delete();   // delete from simple_entity
```

## Deleting by key

The key form deletes exactly the row identified by the entity's declared key (`[Key]`/`.Key()`), rendering the key equality as a parameter:

```csharp
ctx.Delete(new SimpleEntity { Id = 1 });          // delete from simple_entity where id = @p0
await ctx.DeleteAsync(new SimpleEntity { Id = 1 });
```

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

`Returning()` / `Returning(projection)` materialise the removed rows through the provider's `RETURNING`/`OUTPUT` form, read through `Single()`/`ToList()`:

```csharp
var removed = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .Returning(x => new { x.Id, x.Name })
    .ToList();

var one = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Returning()
    .Single();
```

| Provider | Form |
|---|---|
| SQLite, PostgreSQL | `DELETE ... RETURNING <cols>` |
| SQL Server | `DELETE ... OUTPUT deleted.<col> ...` |
| MySQL, MariaDB, ClickHouse, in-memory | `NotSupportedException` |

`Returning()` returns the whole mapped entity (a concrete `TEntity`); `Returning(x => ...)` returns the projected shape (a single member, an anonymous type or a member-init). `Single()` throws when the delete removed no row or more than one.

## Truncating a table

`Truncate<TEntity>()` renders the provider's native `TRUNCATE TABLE`, resetting a table faster than `DeleteFrom<T>().All()`:

```csharp
var cleared = ctx.Truncate<ISimpleEntity>().Execute();   // truncate table simple_entity
```

SQL Server, PostgreSQL, MySQL, MariaDB and ClickHouse have `TRUNCATE TABLE`; SQLite has no `TRUNCATE` and throws `NotSupportedException`, and the in-memory context is query-only and throws `NotSupportedException` too. `Execute()`/`ExecuteAsync()` return the affected-row count where the provider reports one (`0` otherwise).

## Delete based on a join

A joined query can end in `Delete()`/`DeleteAsync()`: the first table of the chain is the delete target, and the rows removed are those matching the join condition and any `Where`. Only INNER joins are supported.

```csharp
var removed = await ctx.From<ISimpleEntity>()
    .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.String == "archived")
    .DeleteAsync(cancellationToken);
```

| Provider | Rendered form |
|---|---|
| PostgreSQL | `DELETE FROM <t> AS a USING <u> AS b WHERE ...` |
| SQL Server | `DELETE a FROM <t> AS a JOIN <u> AS b ON ... WHERE ...` |
| MySQL, MariaDB | ``DELETE `a` FROM <t> AS `a` JOIN <u> AS `b` ON ... WHERE ...`` |
| SQLite, ClickHouse, in-memory | `NotSupportedException` |

* The target is the first table (`Item1`); only `Join` (INNER) is accepted. `LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin` and the `APPLY` joins throw `NotSupportedException`, because they change which rows are deleted.
* The terminals are extension methods on the joined builder for arities 2–8; `Delete()`/`DeleteAsync()` return the affected-row count. `ToSql()` renders the statement without opening a connection and throws on an in-memory context.
* `Returning` is not available on a multi-table delete; read the removed rows back with a separate query if needed.

## Notes and out-of-scope

* The in-memory context is query-only: `Delete`/`DeleteAsync`/`Truncate` (like every other write) throw `NotSupportedException`; query your own collections instead. `INSERT` on the in-memory provider is likewise out of scope by design.
* `DELETE` is not prepared or plan-cached — optimisation in nextorm targets read-only queries only (`Prepare`, the implicit plan cache, benchmarks); a mutation always renders and executes one command per call.
* There is deliberately no soft delete and no global query filter; a full `MERGE` with arbitrary branches is not part of this surface, and `UPDATE` lives in its own guide ([Data modification (UPDATE)](21-update-statement.md)).

## See also

- [Data modification (INSERT)](19-insert-statement.md)
- [Data merging (MERGE / upsert)](23-merge-statement.md)
- [Joins](03-joins.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../providers/overview.md)
- [API reference](../advanced/api-reference.md)
