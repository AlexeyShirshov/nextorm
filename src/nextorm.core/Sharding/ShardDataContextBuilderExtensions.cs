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

        if (builder.EntityScopeResolver is not DefaultEntityScopeResolver)
        {
            throw new InvalidOperationException(
                "UseSharding cannot replace a custom entity-scope resolver; configure the resolver before enabling sharding.");
        }

        var metadataProvider = new ShardEntityMetadataProvider(builder.EntityMetadataProvider, topology);
        var shardRouter = new ShardRouter(topology, keyProvider);
        builder.UseEntityMetadataProvider(metadataProvider);
        builder.UseScopeResolver(shardRouter);
        builder.ShardTopology = topology;
        return builder;
    }
}
