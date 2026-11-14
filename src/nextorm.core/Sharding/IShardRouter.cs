using System.Reflection;

namespace NextORM.Core;

/// <summary>Resolves entity shard keys to mapping scopes.</summary>
public interface IShardRouter : IEntityScopeResolver
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

    /// <summary>Resolves an ordered list of shard-key values to a mapping scope.</summary>
    /// <param name="entityType">The entity type being routed.</param>
    /// <param name="keyValues">The shard-key values in declared property order.</param>
    /// <returns>The default scope for an unsharded type; otherwise the selected shard scope.</returns>
    /// <exception cref="InvalidOperationException">A sharded entity has missing or mismatched key values.</exception>
    MappingScope ResolveShard(Type entityType, IReadOnlyList<object?> keyValues);
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
    public MappingScope Resolve(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return MappingScope.Default;
    }

    /// <inheritdoc />
    public MappingScope Resolve(Type entityType, object entity)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entity);
        if (!IsSharded(entityType))
            return MappingScope.Default;

        var properties = _keyProvider.GetShardKey(entityType);
        var values = new object?[properties.Count];
        for (var index = 0; index < properties.Count; index++)
            values[index] = properties[index].GetValue(entity);
        return ResolveShard(entityType, values);
    }

    /// <inheritdoc />
    public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues)
        => ResolveShard(entityType, keyValues);

    /// <inheritdoc />
    public MappingScope ResolveShard(Type entityType, IReadOnlyList<object?> keyValues)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (!IsSharded(entityType))
            return MappingScope.Default;

        var properties = _keyProvider.GetShardKey(entityType);
        if (keyValues is null || keyValues.Count == 0 || keyValues.Count != properties.Count)
        {
            throw new InvalidOperationException(
                $"Entity '{entityType}' requires {properties.Count} shard-key value(s), but received {keyValues?.Count ?? 0}.");
        }

        var bytes = ShardKeyHasher.GetCanonicalBytes(keyValues);
        var bucket = _topology.Ring.GetBucket(bytes);
        return new MappingScope(_topology.Ring.GetShard(bucket));
    }
}
