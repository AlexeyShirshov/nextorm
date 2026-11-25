# Sharding and horizontal partitioning

Sharding lets one entity type use tables or schemas on several database servers. A **bucket** is the
logical place for a table or schema; a **shard** is the physical server that stores it. A bucket's number
stays fixed when you add or remove servers, so its table or schema name stays the same:

- A **shard** is one server with its own connection string. It is a deployment detail: today there may
  be three of them, tomorrow ten.
- A **bucket** is the stable unit of placement. Its number never changes and never depends on the number
  of shards; it decides the physical table or schema name.
- A **bucket belongs to a shard** through consistent hashing, so a server can be added or removed
  without renaming a single table.

The application addresses data by bucket. Shard ids identify servers only: they never appear in SQL
identifiers, and query results are not grouped by shard.

## Why buckets and shards are separate

A bucket is the stable logical address of a table or schema; a shard is the physical server that currently
stores that bucket. Keeping these separate lets the ring assign the same bucket to a different server when
the shard set changes, without changing the bucket's table or schema name. The library calculates the
bucket and selects its shard, but it does not move the bucket's rows.

The responsibilities are split as follows:

| Concern | Application | nextorm |
|---|---|---|
| Address a row | Declares the shard key and supplies its value, or supplies a known bucket variant. | Resolves the key to a stable bucket. |
| Name and place data | Creates the schema or table for each bucket. | Derives its physical name from the bucket and maps the bucket to a shard. |
| Move a bucket to another shard | Copies the affected rows to the new owner. | Recomputes bucket ownership from the ring. |

## Set up sharding

Use these steps to configure sharding and route work by key or bucket:

1. Choose a fixed `bucketCount` and a bucket placement mode. The bucket count belongs to the data and has
   no library default.
2. Declare the shard key on each entity you want to route (see [Declare a shard key](#declare-a-shard-key)).
3. Create a `ShardTopology`, add its shard connection strings, register it with `UseSharding`, and configure
   the default topology for unrouted work (see [Configure the topology](#configure-the-topology)).
4. Route point operations with the shard-key equality or an explicit bucket, and group multi-row writes by
   bucket (see [Route a single row](#route-a-single-row) and [Per-bucket and per-shard work](#per-bucket-and-per-shard-work)).
5. Provision the bucket tables or schemas yourself. If a bucket changes owner, copy its rows in the
   application; nextorm does not migrate them.

Sharding builds on the [runtime mapping variant](33-runtime-mapping-variant.md): the bucket travels as a
`MappingVariant`, the topology decorates the metadata provider and the router resolves it. The variant
mechanism is independent of sharding and is described on its own page.

`IShardTopology : IHostTopology` — the sharding topology is no separate mechanism: it inherits the general
topology contract ("give me a connection for this statement") and adds the ring, the shards and the
placement mode on top, exactly like `RoundRobinTopology` is another such topology. `TryGetHost` receives
the same `TopologyContext`: the bucket arrives in `context.Variant` (the router put it there in its variant-resolver
role), the ring turns it into a shard id and the shard id into a connection string. A pinned
`context.HostId` overrules the bucket. With no bucket variant the topology does not claim the statement,
and it stays on the context connection — the same `false` any other topology returns.

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
passing a connection string to the constructor as well is rejected. Every statement routed to a bucket runs
on that bucket's shard connection, which the context opens lazily, one per shard, from the topology — the
default host is never part of that decision.

`UseSharding` adds no second mechanism — it assembles sharding from the same three layers:

- **the variant**: `ShardRouter` *becomes* the `IEntityVariantResolver`, and
  `ShardEntityMetadataProvider` wraps the current `IEntityMetadataProvider`, so your own provider keeps
  working and must still delegate the default variant;
- **the routing**: the same topology is registered in the topology table
  (`builder.AddTopology(topology)`) under the name `"sharding"`, and `ShardRouteResolver` is an ordinary
  `IEntityRouteResolver` that claims the sharded types and any statement carrying a bucket variant;
- **the sharding**: what a bucket is, where it lives physically and what the shard key hashes.

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
var placement = context.GetPhysicalTableName<Order>(new MappingVariant("1"));
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
    .WithShardKey(tenant)                    // or pass an explicit bucket variant to From
    .Select(order => new { order.Id, order.Total })
    .OrderByDescending(row => row.Total)
    .ToList();
```

The tokens are expanded while the plan is built, from the bound entity's metadata and the command's
mapping variant — the same point where a mapped source gets its name — so the executed SQL carries
`bucket_7.Orders` (or `Orders_7`) and the statement runs on that bucket's shard. Quoting follows the
context (`QuoteIdentifiers`). A fragment without any of the three tokens is emitted verbatim, and braces
that are not a token are left alone. `{schema}` on an entity that declares no schema is an error: use
`{table}` or `{qualified}`.

`ExecuteRaw`/`ExecuteProcedure` run on the context's own connection and carry no variant, so they cannot
address a bucket's table on its shard; use `FromSql` with a variant for that, or compose the statement for a
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

var builder = new DataContextBuilder()
    .UseSharding(topology)
    // Only for what never reaches a bucket: unsharded entities, raw SQL, the context transaction.
    .UseDataRouting(routing => routing.UseDefaultTopology("main", "Host=pg-1;Database=app_master"));

using var context = new PostgresDataContext(builder);
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

The bucket also works from an explicit key or variant:

```csharp
context.From<Order>().WithShardKey(tenant).ToList();
context.From<Order>(new MappingVariant("512")).ToList();   // the bucket number as text
```

`WithShardKey` takes one value per key property, in the declared order; the section
[The bucket of a row](#the-bucket-of-a-row) below covers the accepted value types and how to ask the
router for a bucket without a query.

A predicate that carries no shard-key equality is **rejected before SQL** instead of silently reading
the default table or scanning every shard. Add the equality, call `WithShardKey(...)`, or fan out over
buckets.

## The bucket of a row

`WithShardKey(...)` takes the shard-key values **in declared order** — a composite key has one value per
property — and returns a builder whose variant is the bucket that tuple hashes to:

```csharp
context.From<Order>().WithShardKey(tenant);          // Order declares [ShardKey(nameof(Tenant))]
// the same as: context.From<Order>(router.ResolveShard(typeof(Order), [tenant]))
```

A composite key takes one value per property, in the declared order:

```csharp
[ShardKey(nameof(Tenant), nameof(Region))]
public sealed class Shipment { /* ... */ }

context.From<Shipment>().WithShardKey(tenant, region);
```

* The order is the attribute's, or the one of the custom `IShardKeyProvider` passed to
  `UseSharding(topology, keyProvider)`.
* The value count must match the key exactly, and `null` is a value like any other.
* The value **types** are part of the key: `int 5` and `long 5` hash to different buckets, and only
  `null`, `int`, `long`, `short`, `byte`, `Guid`, `string`, `bool`, `DateTime`, `DateTimeOffset` and
  `decimal` are supported. An enum, `uint`, `double`, `float` or `char` key property throws
  `ArgumentException` instead of hashing.
* It returns a new builder: the one you called it on stays unscoped. When you already know the bucket,
  `From<Order>(new MappingVariant("512"))` is the same thing.

### Ask the context for a bucket

`GetShardRouter()` returns the router the context routes with, so a bucket can be resolved without
building a query and without keeping the topology at hand. `TryGetShardRouter` reports whether sharding is
configured at all:

```csharp
var router = context.GetShardRouter();             // throws when the context is not sharded

int bucket = router.GetBucket(order);              // from an instance: reads the key properties
MappingVariant variant = router.GetScope(bucket);    // that bucket as a variant

var same  = router.ResolveShard(typeof(Order), [order.Tenant]);   // from explicit values
var shard = router.GetShard(bucket);                              // "pg-2"
```

`GetShardRouter()` is the object `UseSharding` installed as the builder's variant resolver and the context
kept, so a bucket resolved here is exactly the bucket the statement would execute on. `GetBucket` throws
for an entity type that declares no shard key, and `Resolve` returns `MappingVariant.Default` for it. The
rest of the contract — `GetShardKey`, `ShardIds`, `BucketCount` — is listed on `IShardRouter`.

For a list of rows the context does the same work in bulk: `GroupByBucket(rows)` resolves every instance and
returns one group per bucket, ordered by bucket number, each carrying `Bucket`, the addressing `Variant` and
its `Rows`.

A shard's connection string is topology data, not routing data: the context opens the shard's connection
itself from the shard id, and only code that opens its own ADO.NET connection needs the topology.

### Per-bucket and per-shard work

A write is routed from the instance, so a group of rows does not need its bucket computed by hand — it
needs **one SQL statement per bucket**, because a bucket is a physical table on a shard. `GroupByBucket` resolves
every row with the context's own router and hands back one group per bucket. A multi-row insert takes the
group's rows and writes them in one round trip; rows of different buckets in one insert are rejected before
SQL, since one `INSERT` names one table:

```csharp
foreach (var entitiesPerBucket in context.GroupByBucket(orders))
{
    context.CreateInsertBuilder<Order>()
        .Values(entitiesPerBucket.Rows)  // one multi-row INSERT per bucket
        .Insert();
}
```

To run several statements on one bucket, use a batch: one session, one round trip, one variant.

```csharp
foreach (var entitiesPerBucket in context.GroupByBucket(orders))
{
    context.CreateBatchBuilder()
        .Insert(context.CreateInsertBuilder<Order>().Values(entitiesPerBucket.Rows))
        .Query(context.From<Order>(entitiesPerBucket.Variant).Select(order => order.Id))
        .ToList();                   // every step on that bucket's shard, one round trip
}
```

The same holds for a read: address the **bucket**, not the shard, and let the router pick the server.
`GetPhysicalTableName<Order>(variant)` answers where that bucket lives physically (`bucket_7.Orders`, or
`Orders_7` in the suffix mode); [One batch, one topology](34-routing-data-to-topologies.md#one-batch-one-topology)
has the batch rule in full.

### When a pin is needed

Routing picks a host **per command**, so reads, writes and batches need no pin — the bucket already
decides the server. A pin is for the opposite case: state that must outlive one command on one server.

`PinHost(topology, hostId)` keeps every command of that topology on one host until the handle is disposed:

- a server-side session you prepared — a temp table, `SET`, an advisory lock, a prepared statement — where
  the next command has to reach the same connection;
- read-your-write on a picker that rotates (`RoundRobinTopology`), where two commands would otherwise land
  on different servers;
- a shard you are about to load and want to keep warm.

```csharp
var router = context.GetShardRouter();
var shards = context.GroupByBucket(orders)
    .GroupBy(entitiesPerBucket => router.GetShard(entitiesPerBucket.Bucket));

foreach (var shard in shards)
{
    using var pin = context.PinHost("sharding", shard.Key);   // for this topology a host id is a shard id
    // every command of the topology runs on that shard until the block ends
}
```

For a shard topology a host id **is** a shard id, and a pin overrules the bucket: while it is alive every
command of that topology runs on the pinned shard, whatever bucket its variant names. Group by shard before
pinning, and never address another shard's bucket inside the block. The default topology cannot be pinned —
its host is fixed when the context is created, because unrouted commands, raw SQL and the context
transaction run there. A pin belongs to the topology, not to the context: a newer pin replaces an older
one, disposing the older handle then does nothing, and `UnpinHost` releases it without a handle.

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

- Plans, metadata and row materializers are keyed by `(entity type, mapping variant)`, so two buckets never
  share a prepared statement, a parameter prefix or a cached table name.
- The variant-less shortcuts (the shared `Any` command, the scalar-terminal artifacts) are bypassed for a
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
that declare a shard key or carry a bucket variant, and the topology turns the bucket into the owning
shard's connection string. These are exactly the two contracts sharding is built on — the generic
`IHostTopology` and [`MappingVariant`](33-runtime-mapping-variant.md). Sharding neither extends nor
replaces them: it only decides who puts the bucket into the variant and what that bucket means physically.
The rest of the routing machinery — the single-topology rule for one statement, host pinning and
transactions on a routed host — applies to sharded statements unchanged:

```csharp
// Keep a session on one shard, for example one you are about to load.
context.PinHost("sharding", "pg-2");
```

The topology name sharding rules refer to is `"sharding"`.

A batch and a lazy temporary table are one session each, so they are routed as a unit: a batch whose
steps all belong to one bucket runs on that bucket's shard, and a temporary-table read of bucket `b`
must give its source query the variant `b` as well — the `CREATE ... AS SELECT` and the read then share the
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
- For [bulk inserts](22-bulk-insert.md), pass one bucket's `MappingVariant` and supply only rows
  for that bucket. Bulk insert doesn't split rows across shards.
- `ExecuteRaw`/`ExecuteProcedure` run on the context connection and carry no variant: address a bucket's
  table on its shard with `FromSql` under a variant, or with a context already pointed at that shard.
