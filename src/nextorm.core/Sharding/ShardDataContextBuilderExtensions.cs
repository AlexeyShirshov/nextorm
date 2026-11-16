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
}
