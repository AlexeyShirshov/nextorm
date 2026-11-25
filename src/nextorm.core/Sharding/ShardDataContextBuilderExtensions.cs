using System.Diagnostics.CodeAnalysis;

namespace NextORM.Core;

/// <summary>Provides data-context builder configuration for shard-aware routing and metadata.</summary>
public static class ShardDataContextBuilderExtensions
{
    /// <summary>Configures a builder to route shard keys and resolve per-shard metadata placements.</summary>
    /// <param name="builder">The data-context builder to configure.</param>
    /// <param name="topology">
    /// The topology that defines shard membership, placement and the ring; any <see cref="IShardTopology"/>
    /// implementation is accepted, so a custom <see cref="ConsistentHashRing"/> with custom hash functions
    /// can be plugged in.
    /// </param>
    /// <param name="keyProvider">The shard-key provider to use, or <see langword="null"/> for attribute-based keys.</param>
    /// <returns>The configured builder.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A non-default scope resolver is already configured.</exception>
    public static DataContextBuilder UseSharding(
        this DataContextBuilder builder,
        IShardTopology topology,
        IShardKeyProvider? keyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(topology);

        if (builder.EntityVariantResolver is not DefaultEntityVariantResolver)
        {
            throw new InvalidOperationException(
                "UseSharding cannot replace a custom entity-scope resolver; configure the resolver before enabling sharding.");
        }

        var metadataProvider = new ShardEntityMetadataProvider(builder.EntityMetadataProvider, topology);
        var shardRouter = new ShardRouter(topology, keyProvider);
        builder.UseEntityMetadataProvider(metadataProvider);
        builder.UseVariantResolver(shardRouter);
        // The sharding topology is one implementation of the general hosting contract: the router claims
        // the sharded types and the topology turns the bucket scope into the owning shard's host.
        builder.AddTopology(topology);
        builder.UseRouteResolver(new ShardRouteResolver(shardRouter, topology.Name));
        builder.ShardTopology = topology;
        return builder;
    }
}

/// <summary>
/// The rows of one bucket, together with the variant that addresses it. A bucket is the unit a statement
/// can target, so one group is exactly one command: pass <see cref="Rows"/> to an insert or a batch step
/// and <see cref="Variant"/> to <c>From</c> or <c>CreateMergeBuilder</c> when the command is built by hand.
/// </summary>
/// <typeparam name="TEntity">The sharded entity type the rows belong to.</typeparam>
public sealed class BucketGroup<TEntity>
{
    /// <summary>Creates a group.</summary>
    /// <param name="bucket">The bucket number.</param>
    /// <param name="variant">The variant that addresses that bucket.</param>
    /// <param name="rows">The rows placed in that bucket, in input order.</param>
    public BucketGroup(int bucket, MappingVariant variant, IReadOnlyList<TEntity> rows)
    {
        Bucket = bucket;
        Variant = variant;
        Rows = rows;
    }

    /// <summary>The bucket number, in the range <c>[0, BucketCount)</c>.</summary>
    public int Bucket { get; }

    /// <summary>The mapping variant that addresses <see cref="Bucket"/>.</summary>
    public MappingVariant Variant { get; }

    /// <summary>The rows whose shard key hashes to <see cref="Bucket"/>, in the order they were supplied.</summary>
    public IReadOnlyList<TEntity> Rows { get; }
}

/// <summary>
/// Provides access to the shard router a data context routes with, so a caller holding only the context can
/// resolve a bucket without keeping the topology at hand.
/// </summary>
public static class ShardDataContextExtensions
{
    /// <summary>Gets the shard router this context routes with.</summary>
    /// <param name="context">The context to inspect.</param>
    /// <returns>The router <see cref="ShardDataContextBuilderExtensions.UseSharding"/> installed on the builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Sharding is not configured for this context.</exception>
    public static IShardRouter GetShardRouter(this IDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context is DataContext { EntityVariantResolver: IShardRouter router }
            ? router
            : throw new InvalidOperationException(
                "This context has no shard router: enable sharding with UseSharding(topology) on the " +
                "DataContextBuilder the context is created from.");
    }

    /// <summary>Tries to get the shard router this context routes with.</summary>
    /// <param name="context">The context to inspect.</param>
    /// <param name="router">The configured router, or <see langword="null"/> when sharding is not configured.</param>
    /// <returns><see langword="true"/> when the context is sharded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static bool TryGetShardRouter(this IDataContext context, [NotNullWhen(true)] out IShardRouter? router)
    {
        ArgumentNullException.ThrowIfNull(context);
        router = context is DataContext dataContext ? dataContext.EntityVariantResolver as IShardRouter : null;
        return router is not null;
    }

    /// <summary>
    /// Groups rows by the bucket selected from each shard key, so each group can be written with one
    /// statement on the shard that owns that bucket. Because one <c>INSERT</c> targets one bucket table,
    /// group rows that span buckets before writing them.
    /// </summary>
    /// <typeparam name="TEntity">The sharded entity type of the rows.</typeparam>
    /// <param name="context">The sharded context; its router decides the buckets.</param>
    /// <param name="entities">The rows to group, in the order the groups keep them.</param>
    /// <returns>One group per bucket, ordered by bucket number; empty input yields no groups.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="entities"/> contains a <see langword="null"/> row.</exception>
    /// <exception cref="InvalidOperationException">
    /// The context is not sharded, or <typeparamref name="TEntity"/> declares no shard key.
    /// </exception>
    public static IReadOnlyList<BucketGroup<TEntity>> GroupByBucket<TEntity>(
        this IDataContext context,
        IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entities);

        var router = context.GetShardRouter();
        var rowsByBucket = new Dictionary<int, List<TEntity>>();
        var index = 0;
        foreach (var entity in entities)
        {
            if (entity is null)
                throw new ArgumentException($"Entity at index {index} is null.", nameof(entities));

            var bucket = router.GetBucket(typeof(TEntity), entity);
            if (!rowsByBucket.TryGetValue(bucket, out var rows))
            {
                rows = [];
                rowsByBucket.Add(bucket, rows);
            }

            rows.Add(entity);
            index++;
        }

        if (rowsByBucket.Count == 0)
            return [];

        var groups = new BucketGroup<TEntity>[rowsByBucket.Count];
        var position = 0;
        foreach (var bucket in rowsByBucket.Keys.Order())
            groups[position++] = new BucketGroup<TEntity>(bucket, router.GetScope(bucket), rowsByBucket[bucket]);

        return groups;
    }
}
