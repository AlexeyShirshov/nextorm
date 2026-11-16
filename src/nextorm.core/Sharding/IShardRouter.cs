using System.Globalization;
using System.Reflection;

namespace NextORM.Core;

/// <summary>Resolves entity shard keys to mapping variants.</summary>
public interface IShardRouter : IEntityVariantResolver
{
    /// <summary>Returns whether the entity type declares a shard key.</summary>
    /// <param name="entityType">The entity type to inspect.</param>
    /// <returns><see langword="true"/> when the entity is sharded.</returns>
    bool IsSharded(Type entityType);

    /// <summary>Gets the ordered CLR properties that compose the entity's shard key.</summary>
    /// <param name="entityType">The entity type to inspect.</param>
    /// <returns>The shard-key properties, or an empty list for an unsharded type.</returns>
    IReadOnlyList<PropertyInfo> GetShardKey(Type entityType);

    /// <summary>Gets every configured shard identifier, in deterministic ordinal order.</summary>
    IReadOnlyList<string> ShardIds { get; }

    /// <summary>Gets the fixed bucket count; it never depends on the shard count.</summary>
    int BucketCount { get; }

    /// <summary>Gets the shard that currently owns the supplied bucket.</summary>
    /// <param name="bucket">A bucket in the range <c>[0, BucketCount)</c>.</param>
    /// <returns>The owning shard identifier.</returns>
    string GetShard(int bucket);

    /// <summary>Gets the mapping variant of a bucket number.</summary>
    /// <param name="bucket">A bucket in the range <c>[0, BucketCount)</c>.</param>
    /// <returns>The scope that addresses that bucket, ready for <c>WithVariant</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bucket"/> is outside the range.</exception>
    MappingVariant GetScope(int bucket);

    /// <summary>Gets the bucket an entity instance belongs to.</summary>
    /// <param name="entityType">The entity type being routed.</param>
    /// <param name="entity">The instance whose shard-key properties select the bucket.</param>
    /// <returns>The bucket number, in the range <c>[0, BucketCount)</c>.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="entityType"/> declares no shard key.</exception>
    int GetBucket(Type entityType, object entity);

    /// <summary>Gets the bucket an ordered list of shard-key values hashes to.</summary>
    /// <param name="entityType">The entity type being routed.</param>
    /// <param name="keyValues">The shard-key values in declared property order.</param>
    /// <returns>The bucket number, in the range <c>[0, BucketCount)</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="entityType"/> declares no shard key, or the value count does not match it.
    /// </exception>
    int GetBucket(Type entityType, IReadOnlyList<object?> keyValues);

    /// <summary>Resolves an ordered list of shard-key values to a mapping variant.</summary>
    /// <param name="entityType">The entity type being routed.</param>
    /// <param name="keyValues">The shard-key values in declared property order.</param>
    /// <returns>The default scope for an unsharded type; otherwise the selected bucket scope.</returns>
    /// <exception cref="InvalidOperationException">A sharded entity has missing or mismatched key values.</exception>
    MappingVariant ResolveShard(Type entityType, IReadOnlyList<object?> keyValues);
}

/// <summary>Routes entity instances and explicit shard-key values through a shard topology.</summary>
public sealed class ShardRouter : IShardRouter
{
    private readonly IShardTopology _topology;
    private readonly IShardKeyProvider _keyProvider;

    /// <summary>Creates a router for a topology and shard-key provider.</summary>
    /// <param name="topology">The shard topology used to select shard identifiers.</param>
    /// <param name="keyProvider">The provider used to resolve shard-key properties; defaults to attributes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="topology"/> is <see langword="null"/>.</exception>
    public ShardRouter(IShardTopology topology, IShardKeyProvider? keyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        _topology = topology;
        _keyProvider = keyProvider ?? new AttributeShardKeyProvider();
    }

    /// <inheritdoc />
    public bool IsSharded(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _keyProvider.IsSharded(entityType);
    }

    /// <inheritdoc />
    public IReadOnlyList<PropertyInfo> GetShardKey(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _keyProvider.GetShardKey(entityType);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ShardIds => _topology.ShardIds;

    /// <inheritdoc />
    public int BucketCount => _topology.Ring.BucketCount;

    /// <inheritdoc />
    public string GetShard(int bucket) => _topology.Ring.GetShard(bucket);

    /// <inheritdoc />
    public MappingVariant GetScope(int bucket)
    {
        if ((uint)bucket >= (uint)BucketCount)
            throw new ArgumentOutOfRangeException(nameof(bucket), bucket, $"Bucket '{bucket}' is outside [0, {BucketCount}).");

        return new MappingVariant(bucket.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public MappingVariant Resolve(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return MappingVariant.Default;
    }

    /// <inheritdoc />
    public MappingVariant Resolve(Type entityType, object entity)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entity);
        return IsSharded(entityType) ? GetScope(GetBucket(entityType, entity)) : MappingVariant.Default;
    }

    /// <inheritdoc />
    public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues)
        => ResolveShard(entityType, keyValues);

    /// <inheritdoc />
    public int GetBucket(Type entityType, object entity)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entity);
        var properties = _keyProvider.GetShardKey(entityType);
        if (properties.Count == 0)
            throw new InvalidOperationException($"Entity '{entityType}' does not declare a shard key.");

        var values = new object?[properties.Count];
        for (var index = 0; index < properties.Count; index++)
            values[index] = properties[index].GetValue(entity);
        return GetBucket(entityType, values);
    }

    /// <inheritdoc />
    public int GetBucket(Type entityType, IReadOnlyList<object?> keyValues)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        var properties = _keyProvider.GetShardKey(entityType);
        if (properties.Count == 0)
            throw new InvalidOperationException($"Entity '{entityType}' does not declare a shard key.");
        if (keyValues is null || keyValues.Count != properties.Count)
        {
            throw new InvalidOperationException(
                $"Entity '{entityType}' requires {properties.Count} shard-key value(s), but received {keyValues?.Count ?? 0}.");
        }

        return _topology.Ring.GetBucket(ShardKeyHasher.GetCanonicalBytes(keyValues));
    }

    /// <inheritdoc />
    public MappingVariant ResolveShard(Type entityType, IReadOnlyList<object?> keyValues)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return IsSharded(entityType) ? GetScope(GetBucket(entityType, keyValues)) : MappingVariant.Default;
    }
}

/// <summary>
/// Type-inferring convenience over <see cref="IShardRouter"/>: the shard key is declared on the entity
/// type, so a caller holding an instance does not need to spell the type out.
/// </summary>
public static class ShardRouterExtensions
{
    /// <summary>Gets the bucket an entity instance belongs to.</summary>
    /// <typeparam name="TEntity">The entity type, taken from the argument.</typeparam>
    /// <param name="router">The router to resolve with.</param>
    /// <param name="entity">The instance whose shard-key properties select the bucket.</param>
    /// <returns>The bucket number, in the range <c>[0, BucketCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TEntity"/> declares no shard key.</exception>
    public static int GetBucket<TEntity>(this IShardRouter router, TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(router);
        return router.GetBucket(typeof(TEntity), entity!);
    }
}
