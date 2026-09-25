# Optimistic concurrency and change tracking

> nextorm deliberately has no change tracking and no identity map, so concurrency is explicit: put the
> expected token in `Where`, and read `0` affected rows as a conflict. This page shows that pattern, how
> to read the new token back, the token-guarded upsert, and how to build a thin change-tracking layer on
> top when you want `SaveChanges`-style ergonomics.

**Prerequisites:** [Data modification (UPDATE)](21-update-statement.md) · [Data modification (MERGE)](23-merge-statement.md) · [Transactions](25-transactions.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Why there is no built-in change tracker

A change tracker is only useful when the framework **owns** the loaded entity: it keeps the instance in an
identity map, remembers the original values, notices mutations and turns them into `UPDATE`s in
`SaveChanges`. nextorm does none of that — it is a query builder with an explicit DML surface
([Limitations and out-of-scope features](../advanced/limitations.md)):

* every terminal (`ToList`, `Update`, `Insert`, `Merge`, `Delete`, ...) issues exactly one command and
  returns its result;
* a loaded row is a plain POCO the caller owns; the context never sees it again;
* there is no identity map, no original-values snapshot and no "changed properties" concept.

For concurrency this means the framework cannot know which token value a row had when you read it, so it
cannot add the check for you. It only has to hand you the two primitives you need — a `WHERE` that
compares the token, and the affected-row count — and both already exist. Optimistic concurrency is
therefore a **pattern** on top of the explicit surface, documented here, rather than an
`IfUnchanged`/`WithRefresh` API shipped by the library.

## The token column

Optimistic concurrency needs a value that changes on every write. Map it as an ordinary property; nextorm
compares it like any other column.

| Token | Provider | Mapped as | Notes |
|---|---|---|---|
| `long` / `int` version | all | `long Version { get; set; }` | application-managed; increment in the same statement |
| `rowversion` / `timestamp` | SQL Server | `byte[] Version { get; }` | database-managed; mark computed |
| `TIMESTAMP ... ON UPDATE CURRENT_TIMESTAMP` | MySQL / MariaDB | `DateTime Version { get; set; }` | database-managed |
| `long` version (or `xmin`) | PostgreSQL | `long Version { get; set; }` | no native rowversion column; `xmin` is a system column |

```csharp
[SqlTable("orders")]
public interface IOrder
{
    [Key] long Id { get; }
    string Status { get; set; }
    long Version { get; set; }   // application-managed concurrency token
}
```

A database-managed token is never written by the application, so mark it computed
(`[DatabaseGenerated(DatabaseGeneratedOption.Computed)]` or `.Computed()`) — `Set(entity)` then skips it
automatically.

## Check on update

Load the row, remember its token, then put both the key and the original token in `Where`:

```csharp
public sealed class ConcurrencyException(string message) : Exception(message);

var originalVersion = order.Version;

var affected = ctx.Update<IOrder>()
    .Set(o => o.Status, "paid")
    .Set(o => o.Version, o => o.Version + 1)          // portable: bump in SQL
    .Where(o => o.Id == order.Id && o.Version == originalVersion)
    .Update();

if (affected == 0)
    throw new ConcurrencyException($"Order {order.Id} was modified by someone else.");
```

```sql
-- PostgreSQL
update orders set status = @p0, version = version + 1 where (id = @p1 and version = @p2)
```

* `Where` is the same predicate translator as a query `WHERE`, so `Version == originalVersion` binds the
  token as a parameter, not as inline SQL.
* `Set(o => o.Version, o => o.Version + 1)` increments atomically in the database — no
  read-modify-write race in application code.
* Drop the version predicate and the update becomes *last write wins*.
* For a database-managed token, do not `Set` it at all (it is computed); only compare it in `Where`.

`0` affected rows is deliberately ambiguous: the row may have been deleted, or its token may have
changed. If you need to tell the two apart, run one extra `FirstOrDefault` on the key.

## Read the new token back

After a successful update the token has changed. When the provider can return rows, ask for the new value
in the same statement:

```csharp
var row = ctx.Update<IOrder>()
    .Set(o => o.Status, "paid")
    .Set(o => o.Version, o => o.Version + 1)
    .Where(o => o.Id == order.Id && o.Version == order.Version)
    .Returning(o => new { o.Id, o.Version })          // UPDATE ... RETURNING / OUTPUT
    .Single();

order.Version = row.Version;                          // caller-owned write-back
```

`Returning` is available on PostgreSQL (`RETURNING`), SQLite (`RETURNING`, 3.35+) and SQL Server
(`OUTPUT`), on the predicate form only. MySQL/MariaDB and ClickHouse reject it — there, re-select the row
(inside the same transaction, if any) to obtain the new token. There is no framework-side "refresh":
nextorm does not hold your entity, so *you* assign the returned value, exactly like after any other read.

## Token-guarded upsert

When the same row may or may not exist, a full `MERGE` can insert or update atomically and refuse a stale
match, on providers that render conditional branches (SQL Server, PostgreSQL 15+):

```csharp
ctx.MergeInto<IOrder>()
    .Using(order)                                     // entity, batch or server-side query
    .OnKeys()
    .WhenMatched((t, s) => t.Version == s.Version)
    .ThenUpdate(o => new { o.Status })
    .WhenNotMatched().ThenInsert()
    .Merge();
```

* A `WHEN MATCHED AND <token>` branch that does **not** fire leaves the existing row untouched — this is
  *skip if stale*, not *fail if stale*. Read the affected-row count or a `Returning` projection if you
  need to react; for a hard "throw on conflict", use the update pattern above.
* The conditional branch (`WHEN MATCHED AND ...`) is supported on SQL Server and PostgreSQL 15+ only;
  SQLite/MySQL/MariaDB support the key upsert only and reject the general `MERGE`. See
  [MERGE](23-merge-statement.md).

## Rolling your own change tracking

There is no row-materialization hook, so a user-land tracker attaches rows as it loads them and diffs
them at `SaveChanges` time. For one entity type it is small:

```csharp
public sealed class OrderTracker
{
    private readonly IDataContext _ctx;
    private readonly Dictionary<long, (IOrder Entity, long Version, string Status)> _original = new();

    public OrderTracker(IDataContext ctx) => _ctx = ctx;

    public List<IOrder> Load()
    {
        var rows = _ctx.From<IOrder>().ToList();
        foreach (var row in rows)
            _original[row.Id] = (row, row.Version, row.Status);   // snapshot
        return rows;
    }

    public int SaveChanges()
    {
        var affected = 0;
        foreach (var (id, (entity, version, status)) in _original)
        {
            if (entity.Status == status)                          // detect change
                continue;

            var n = _ctx.Update<IOrder>()
                .Set(o => o.Status, entity.Status)
                .Set(o => o.Version, o => o.Version + 1)
                .Where(o => o.Id == id && o.Version == version)
                .Update();

            if (n == 0)
                throw new ConcurrencyException($"Order {id} was modified.");
            affected += n;
        }
        return affected;
    }
}
```

Wrap `SaveChanges` in a transaction so a conflict rolls the whole unit of work back:

```csharp
await using var tx = await ((ITransactionManager)ctx).BeginTransactionAsync();
try
{
    tracker.SaveChanges();
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}
```

To generalise, diff the mapped properties instead of one field — `DataContextCache.Metadata[typeof(T)].Properties`
exposes the mapping ([`IPropertyMetadata`](xref:NextORM.Core.IPropertyMetadata)) — or generate a typed
tracker with a source generator. nextorm deliberately stops at the primitives: an identity map, lazy
loading, `SaveChanges` ordering and a "changed property" model are out of scope
([Limitations and out-of-scope features](../advanced/limitations.md)).

## Provider support

| Provider | Token check (`WHERE` + row count) | `RETURNING` / `OUTPUT` on `UPDATE` | Conditional `MERGE` branch |
|---|---|---|---|
| PostgreSQL | yes | `RETURNING` | yes (15+) |
| SQL Server | yes | `OUTPUT` | yes |
| SQLite | yes | `RETURNING` (3.35+) | — (key upsert only) |
| MySQL / MariaDB | yes | — | — (key upsert only) |
| ClickHouse | predicate required; `UPDATE` reports **no** affected-row count | — | — |
| In-memory | — (query-only) | — | — |

ClickHouse is the notable exception: `ALTER TABLE ... UPDATE` reports no affected-row count, so the
`0 == conflict` test cannot be used there. Use a token in the `SET` expression that only matches the
expected value, or re-read the row, to detect contention.

## See also

- [Data modification (UPDATE)](21-update-statement.md)
- [Data modification (MERGE)](23-merge-statement.md)
- [Transactions](25-transactions.md)
- [Data modification (INSERT)](19-insert-statement.md)
- [Data modification (DELETE)](20-delete-statement.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
