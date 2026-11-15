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

Sharding builds on the mapping scope described in
[Per-query source overrides](01-query-sources.md#per-query-source-overrides): the bucket travels as a
`MappingScope`, the topology decorates the metadata provider and the router resolves it.

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

var builder = new DataContextBuilder().UseSharding(topology);
using var context = new PostgresDataContext(topology.GetConnectionString("pg-1"), builder);
```

`UseSharding` installs a `ShardRouter` as the scope resolver and wraps the current metadata provider in
a shard-aware decorator, so an existing `IEntityMetadataProvider` keeps working. Every statement routed
to a bucket runs on that bucket's shard connection; the context's own connection serves unsharded
entities.

### Placement of a bucket

The placement mode decides how a bucket becomes a physical name. Both modes depend on the bucket number
only:

| Mode | Physical name for bucket `b` |
| --- | --- |
| `BucketPlacementMode.TableSuffix` (default) | `{mappedTable}_{b}` in the mapped schema |
| `BucketPlacementMode.SchemaPerBucket` | `{mappedSchema}_{b}` with the mapped table name |

```csharp
var bySchema = new ShardTopology(bucketCount: 1024, BucketPlacementMode.SchemaPerBucket);
```

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
using var context = new PostgresDataContext(connectionString, new DataContextBuilder().UseSharding(topology));
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
