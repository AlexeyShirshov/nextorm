using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;

/// <summary>Resolves whether an entity is sharded and which properties form its shard key.</summary>
public interface IShardKeyProvider
{
    /// <summary>Returns whether the entity type declares a shard key.</summary>
    /// <param name="entityType">The entity type to inspect.</param>
    /// <returns><see langword="true"/> when the provider recognizes a shard key for the type.</returns>
    bool IsSharded(Type entityType);

    /// <summary>Gets the ordered CLR properties that compose the entity's shard key.</summary>
    /// <param name="entityType">The entity type to inspect.</param>
    /// <returns>The shard-key properties, or an empty list for an unsharded type.</returns>
    /// <exception cref="InvalidOperationException">A declared key names a missing or unreadable property.</exception>
    IReadOnlyList<PropertyInfo> GetShardKey(Type entityType);
}

/// <summary>Resolves <see cref="ShardKeyAttribute"/> declarations and caches their reflected properties.</summary>
public sealed class AttributeShardKeyProvider : IShardKeyProvider
{
    private static readonly IReadOnlyList<PropertyInfo> NoProperties = Array.Empty<PropertyInfo>();
    private readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> _cache = new();

    /// <summary>Creates an attribute-based shard-key provider with a per-type property cache.</summary>
    public AttributeShardKeyProvider()
    {
    }

    /// <inheritdoc />
    public bool IsSharded(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.IsDefined(typeof(ShardKeyAttribute), inherit: true);
    }

    /// <inheritdoc />
    public IReadOnlyList<PropertyInfo> GetShardKey(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _cache.GetOrAdd(entityType, static type => ResolveProperties(type));
    }

    private static IReadOnlyList<PropertyInfo> ResolveProperties(Type entityType)
    {
        var attribute = entityType.GetCustomAttribute<ShardKeyAttribute>(inherit: true);
        if (attribute is null)
            return NoProperties;

        var properties = new PropertyInfo[attribute.PropertyNames.Count];
        for (var index = 0; index < properties.Length; index++)
        {
            var name = attribute.PropertyNames[index];
            var property = entityType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetMethod is null || property.GetIndexParameters().Length != 0)
            {
                throw new InvalidOperationException(
                    $"Shard-key property '{name}' was not found as a readable, non-indexed instance property on '{entityType}'.");
            }

            properties[index] = property;
        }

        return Array.AsReadOnly(properties);
    }
}

internal sealed class MappedShardKeyProvider : IShardKeyProvider
{
    private readonly AttributeShardKeyProvider _attributes = new();

    public bool IsSharded(Type entityType)
        => GetMappedKey(entityType) is not null || _attributes.IsSharded(entityType);

    public IReadOnlyList<PropertyInfo> GetShardKey(Type entityType)
        => GetMappedKey(entityType) ?? _attributes.GetShardKey(entityType);

    // Do not auto-build metadata here: a lookup before From<T>(cfg) must not block that registration.
    private static IReadOnlyList<PropertyInfo>? GetMappedKey(Type entityType)
        => DataContextCache.Metadata.TryGetValue(entityType, out var metadata) && metadata is EntityMetadata mapped
            ? mapped.ShardKey
            : null;
}
