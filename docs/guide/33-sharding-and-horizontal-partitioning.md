# Sharding and horizontal partitioning

nextorm can route one entity type to several physical tables or schemas from a single connection.
The mapping layer is the one described in [Per-query source overrides](01-query-sources.md#per-query-source-overrides)
and the mapping scope: sharding adds a consistent-hash ring, a shard topology and a router on top of
`MappingScope`, `IEntityScopeResolver` and `IEntityMetadataProvider`.

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

The values of the key are hashed with a fixed, deterministic, non-cryptographic function, so the same
key resolves to the same shard in every process and after a restart. Supported value types are
`int`, `long`, `short`, `byte`, `Guid`, `string`, `bool`, `DateTime`, `DateTimeOffset`, `decimal`
and `null`; any other type throws at route time.

## Configure shards

`ShardTopology` owns the configured shard ids, the ring and the physical placement of each shard:

```csharp
var topology = new ShardTopology();          // 128 virtual nodes per shard, 4096 buckets
topology.AddShard("shard_eu", schema: "eu");
topology.AddShard("shard_us", schema: "us");

var builder = new DataContextBuilder().UseSharding(topology);
using var context = new PostgresDataContext(connectionString, builder);
```

A shard id becomes part of a SQL identifier, so it is validated on registration: letters, digits and
underscores only. When no explicit table name is configured for a shard, the table is
`{mappedTable}_{shardId}`; an explicit schema replaces the mapped schema for that shard.

`UseSharding` installs a `ShardRouter` as the scope resolver and wraps the current metadata provider
in a shard-aware decorator, so an existing `IEntityMetadataProvider` keeps working.

## Route writes

`INSERT`, `UPDATE` and `DELETE` resolve the shard from the entity instance:

```csharp
context.CreateInsertBuilder<Order>()
    .Values(new Order { Id = 1, Tenant = "acme", Total = 10m })
    .Insert();

context.Update(new Order { Id = 1, Tenant = "acme", Total = 12m });
context.Delete(new Order { Id = 1, Tenant = "acme" });
```

A multi-row insert whose rows resolve to different shards is rejected before any SQL runs; group the
rows by shard and insert each group separately. An insert that provides only columns has no shard key
and must name the target shard explicitly with `WithShardKey(...)`.

## Route reads

A point read routes from an equality on the shard key in the predicate:

```csharp
var orders = context.From<Order>()
    .Where(order => order.Tenant == tenant)
    .Select(order => order.Total)
    .ToList();
```

The route also works from an explicit key or scope:

```csharp
context.From<Order>().WithShardKey(tenant).ToList();
context.From<Order>().WithScope(new MappingScope("shard_eu")).ToList();
```

A predicate that carries no shard-key equality is **rejected before SQL** instead of silently reading
the default table or scanning every shard. Add the equality, call `WithShardKey(...)`, or collect
across shards explicitly.

## Collect across shards

`AcrossShards()` runs one command per selected shard and concatenates the rows on the client:

```csharp
var all = context.From<Order>()
    .AcrossShards()
    .Where(order => order.Total > 100m)
    .Select(order => order.Total)
    .ToList();

var some = context.From<Order>().AcrossShards("shard_eu", "shard_us").ToList();
```

Global ordering, paging, `DISTINCT` and aggregates cannot be answered by concatenating per-shard
results; `OrderBy`, `Take`, `Skip`, `Distinct`, `Count`, `Any`, `First` and `Single` on a fan-out
query throw `NotSupportedException`. Run those against a single shard, or merge the materialized rows
on the client.

## Cache, identity and rebalancing

- Plans and metadata are keyed by `(entity type, mapping scope)`, so two shards never share a prepared
  statement, a parameter prefix or a cached table name.
- A command prepared for one scope is never reused for another scope, including through the
  value-free plan-reuse shortcuts.
- New public members live in `PublicAPI.Unshipped.txt` and follow the usual API-compatibility rules.
- Consistent hashing moves roughly `1/(K+1)` of the keys when the `(K+1)`-th shard is added, but a key
  that changes shard now points at a table whose data was written under the old ring. Moving the rows
  is the application's responsibility; the library only computes the ring and the placement.

## Limitations

- Connection-per-shard, distributed transactions (2PC), data migration during rebalancing and
  online shard-count changes without a topology version bump are out of scope.
- A `params`-supplied value that only exists at execution time cannot be extracted from the predicate;
  capture the value in a closure, call `WithShardKey(...)`, or fan out.
- Bulk insert (`BulkInsertBuilder`) is not shard-aware in this version.
- Cross-shard `ORDER BY`/`LIMIT`/`OFFSET`/`DISTINCT`/aggregates are not composed globally.
