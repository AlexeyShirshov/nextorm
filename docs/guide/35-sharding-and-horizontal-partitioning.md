# Sharding and horizontal partitioning

nextorm can spread one entity type over several database servers. Three ideas drive the feature:

- A **shard** is one server with its own connection string. It is a deployment detail: today there may
  be three of them, tomorrow ten.
- A **bucket** is the stable unit of placement. Its number never changes and never depends on the number
  of shards; it decides the physical table or schema name.
- A **bucket belongs to a shard** through consistent hashing, so a server can be added or removed
  without renaming a single table.

The application works with buckets. Shard ids are only used to address servers: they never appear in a
SQL identifier, and no query result is grouped by them.

Sharding builds on the [runtime mapping scope](33-runtime-mapping-scope.md): the bucket travels as a
`MappingScope`, the topology decorates the metadata provider and the router resolves it. The scope
mechanism is independent of sharding and is described on its own page.

## Declare a shard key

A shard key is one or more readable instance properties. Declare it with the attribute:

```csharp
[ShardKey(nameof(Tenant))]
public sealed class Order
{
    public int Id { get; set; }
    public string Tenant { get; set; } = "";
    public decimal Total { get; set; }
}
```

The key values are hashed with a fixed, deterministic, non-cryptographic function, so the same key
resolves to the same bucket in every process and after a restart. Supported value types are `int`,
`long`, `short`, `byte`, `Guid`, `string`, `bool`, `DateTime`, `DateTimeOffset`, `decimal` and `null`;
any other type throws at route time.

## Configure the topology

`ShardTopology` owns the bucket count, the placement mode, the ring and the connection string of every
shard. The bucket count is required — there is no default, because it is a property of the data, not of
the library:

```csharp
var topology = new ShardTopology(bucketCount: 1024);
topology.AddShard("pg-1", "Host=pg-1;Database=app");
topology.AddShard("pg-2", "Host=pg-2;Database=app");

var builder = new DataContextBuilder()
    .UseSharding(topology)
    .UseDataRouting(routing => routing
        // The simplest topology: one connection string. It serves only statements that never
        // reach a bucket — unsharded entities, raw SQL and the context transaction. It is not a
        // routing target and never decides where a sharded statement runs.
        .UseDefaultTopology("main", "Host=pg-1;Database=app_master"));

using var context = new PostgresDataContext(builder);
```

The context is created from the builder alone: its own connection comes from the default topology, and
passing a connection string to the constructor as well is rejected. `UseSharding` installs a `ShardRouter`
as the scope resolver and wraps the current metadata provider in a shard-aware decorator, so an existing
`IEntityMetadataProvider` keeps working. Every statement routed to a bucket runs on that bucket's shard
connection, which the context opens lazily, one per shard, from the topology — the default host is never
part of that decision.

### Placement of a bucket

The placement mode decides how a bucket becomes a physical name. Both modes depend on the bucket number
only, never on the shard set.

With `SchemaPerBucket` — the default — the bucket becomes the schema and the table keeps its mapped name:

```text
{placeholder}_{bucket}.{table}
```

`{placeholder}` is the schema the entity is mapped to, or the literal `bucket` when it declares no
schema; `{table}` is the mapped table name, unchanged. With `TableSuffix` the table name carries the
suffix and the schema does not change:

| Entity | Bucket | `SchemaPerBucket` (default) | `TableSuffix` |
| --- | --- | --- | --- |
| `Order` — table `Orders`, no schema | 1 | `bucket_1.Orders` | `Orders_1` |
| `Invoice` — schema `sales`, table `invoices` | 1 | `sales_1.invoices` | `sales.invoices_1` |
| `Order` | 7 | `bucket_7.Orders` | `Orders_7` |

```csharp
// SchemaPerBucket is the default; ask for the suffixed table explicitly.
var byTable = new ShardTopology(bucketCount: 1024, BucketPlacementMode.TableSuffix);
```

The same answer is available to code that composes SQL itself:

```csharp
var placement = context.GetPhysicalTableName<Order>(new MappingScope("1"));
// placement.Schema == "bucket_1", placement.TableName == "Orders"
// placement.QualifiedName == "bucket_1.Orders"
```

### Raw SQL and the bucket name

Raw SQL is opaque to the planner, so it is never rewritten by accident. When the statement itself needs
the bucket's physical name, bind the entity to the raw source and use one of the tokens:

| Token | Expands to |
| --- | --- |
| `{table}` | the mapped table name, e.g. `Orders_1` |
| `{schema}` | the mapped schema, e.g. `sales_1` or `bucket_1` |
| `{qualified}` | `<schema>.<table>`, or the table alone when the entity declares no schema |

```csharp
var top = context.FromSql("select Id, Tenant, Total from {qualified} where Total > @min", new { min = 100 })
    .BindEntity<Order>(["Id", "Tenant", "Total"])
    .WithShardKey(tenant)                    // or .WithScope(...) for an explicit bucket
    .Select(order => new { order.Id, order.Total })
    .OrderByDescending(row => row.Total)
    .ToList();
```

The tokens are expanded while the plan is built, from the bound entity's metadata and the command's
mapping scope — the same point where a mapped source gets its name — so the executed SQL carries
`bucket_7.Orders` (or `Orders_7`) and the statement runs on that bucket's shard. Quoting follows the
context (`QuoteIdentifiers`). A fragment without any of the three tokens is emitted verbatim, and braces
that are not a token are left alone. `{schema}` on an entity that declares no schema is an error: use
`{table}` or `{qualified}`.

`ExecuteRaw`/`ExecuteProcedure` run on the context's own connection and carry no scope, so they cannot
address a bucket's table on its shard; use `FromSql` with a scope for that, or compose the statement for a
context already pointed at the shard.

### Custom hash functions

Both hashing stages are replaceable. `ConsistentHashRing` accepts a `BucketHash` (key bytes → bucket)
and a `ShardHash` (bucket + shard id → score) delegate, and `UseSharding` accepts any `IShardTopology`,
so a custom topology can expose that ring instead of the built-in one:

```csharp
var ring = new ConsistentHashRing(
    shardIds: ["pg-1", "pg-2"],
    bucketCount: 1024,
    bucketHash: (key, count) => MyHash.ToBucket(key, count),
    shardHash: (bucket, shardId) => MyHash.Score(bucket, shardId));

var topology = new MyTopology(ring);   // any IShardTopology implementation
using var context = new PostgresDataContext(nonRoutedDefaultConnectionString, new DataContextBuilder().UseSharding(topology));
```

A custom function must be deterministic across processes and restarts: routing is stored in the data,
so changing the function moves rows. Bucket and shard identifiers are validated; a shard id must not be
empty, and it is never rendered into SQL.

## Route a single row

`INSERT`, `UPDATE` and `DELETE` resolve the bucket from the entity instance and execute on the owning
shard connection:

```csharp
context.CreateInsertBuilder<Order>()
    .Values(new Order { Id = 1, Tenant = "acme", Total = 10m })
    .Insert();

context.Update(new Order { Id = 1, Tenant = "acme", Total = 12m });
context.Delete(new Order { Id = 1, Tenant = "acme" });
```

A point read routes from an equality on the shard key in the predicate:

```csharp
var orders = context.From<Order>()
    .Where(order => order.Tenant == tenant)
    .Select(order => order.Total)
    .ToList();
```

The bucket also works from an explicit key or scope:

```csharp
context.From<Order>().WithShardKey(tenant).ToList();
context.From<Order>().WithScope(new MappingScope("512")).ToList();   // the bucket number as text
```

A predicate that carries no shard-key equality is **rejected before SQL** instead of silently reading
the default table or scanning every shard. Add the equality, call `WithShardKey(...)`, or fan out over
buckets.

## Fan out over buckets

`AcrossBuckets()` runs the query once per bucket and returns **one result vector per bucket**. The array
is indexed by bucket number — its length is the configured bucket count — and the library never merges,
re-orders or aggregates across buckets:

```csharp
var totals = context.From<Order>()
    .AcrossBuckets()
    .Where(order => order.Total > 100m)
    .Select(order => order.Total)
    .ToList();

// totals.Length == bucketCount; totals[b] holds the rows of bucket b
var grandTotal = totals.SelectMany(vector => vector).Sum();   // the caller decides
```

Per-bucket operators (`Where`, `OrderBy`, `Distinct`, `Select`) apply to every bucket independently:
`OrderBy` sorts each vector, it does not produce a globally ordered sequence.

Scalar terminals return one value per bucket:

```csharp
int[] counts = context.From<Order>().AcrossBuckets().Count();
bool[] any = context.From<Order>().AcrossBuckets().Any();
Order[] first = context.From<Order>().AcrossBuckets().OrderBy(order => order.Id).First();
```

`AcrossBuckets(...).Buckets` lists the buckets the fan-out covers. `AsAsyncEnumerable()` streams the
result one vector at a time in bucket order, sequentially, so a large fan-out does not have to
materialize every bucket at once.

## Execution and parallelism

The fan-out groups buckets by their owning shard:

- buckets that share a shard run **sequentially** on that shard's single connection;
- different shards run **in parallel**, up to a limit that defaults to the number of shards;
- `AcrossBuckets(parallelism: 1)` serializes the whole fan-out; the value is clamped to
  `[1, shardCount]`.

```csharp
var vectors = await context.From<Order>()
    .AcrossBuckets(parallelism: 4)
    .Where(order => order.Total > 0m)
    .ToListAsync(cancellationToken);
```

## Cache, identity and rebalancing

- Plans, metadata and row materializers are keyed by `(entity type, mapping scope)`, so two buckets never
  share a prepared statement, a parameter prefix or a cached table name.
- The scope-less shortcuts (the shared `Any` command, the scalar-terminal artifacts) are bypassed for a
  bucket-scoped builder: a scoped statement is rendered and executed for that bucket only.
- A bucket's placement depends on its number alone. Adding or removing a server never renames a table or
  a schema, so no redeploy and no DDL change is needed.
- Consistent (rendezvous) hashing is used without virtual nodes: a new shard takes over only the buckets
  it wins (about `1/(K+1)` of them), and removing a shard returns exactly those buckets to their previous
  owners. With the built-in avalanche score, 4096 buckets spread over three shards as
  32.25 / 34.30 / 33.45 %.
- When a bucket changes owner, its rows still live in the old server. Copying them is the application's
  responsibility; the library only computes the ring and the placement.

## Sharding as data routing

Sharding is one rule in the general routing model. `ShardTopology` implements `IHostTopology` (see
[Routing data to topologies](34-routing-data-to-topologies.md)): the shard router claims the entity types
that declare a shard key or carry a bucket scope, and the topology turns the bucket into the owning
shard's connection string. The rest of the routing machinery — the single-topology rule for one
statement, host pinning and transactions on a routed host — applies to sharded statements unchanged:

```csharp
// Keep a session on one shard, for example one you are about to load.
context.PinHost("sharding", "pg-2");
```

The topology name sharding rules refer to is `"sharding"`.

A batch and a lazy temporary table are one session each, so they are routed as a unit: a batch whose
steps all belong to one bucket runs on that bucket's shard, and a temporary-table read of bucket `b`
must scope its source query to `b` as well — the `CREATE ... AS SELECT` and the read then share the
shard ([One batch, one topology](34-routing-data-to-topologies.md#one-batch-one-topology)). A batch
that would span two buckets is rejected; run one batch per bucket.

## Limitations

- Fan-out results are per-bucket vectors. Global `ORDER BY`, `LIMIT`/`OFFSET`, `DISTINCT` and aggregates
  across buckets are not composed: combine the vectors on the client.
- A fan-out spans several connections, so it is not covered by one transaction and the bucket statements
  are not atomic with each other.
- Data migration during rebalancing, distributed transactions (2PC) and online shard-count changes that
  keep a bucket table on the old server are out of scope.
- A `params`-supplied value that only exists at execution time cannot be extracted from the predicate;
  capture the value in a closure, call `WithShardKey(...)`, or fan out.
- Bulk insert (`BulkInsertBuilder`) is not shard-aware in this version.
- `ExecuteRaw`/`ExecuteProcedure` run on the context connection and carry no scope: address a bucket's
  table on its shard with `FromSql` under a scope, or with a context already pointed at that shard.
