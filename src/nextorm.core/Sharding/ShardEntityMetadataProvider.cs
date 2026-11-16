using System.Globalization;
using System.Reflection;

namespace NextORM.Core;

/// <summary>Decorates entity metadata with the physical placement selected by a bucket scope.</summary>
public sealed class ShardEntityMetadataProvider : IEntityMetadataProvider
{
    private readonly IEntityMetadataProvider _inner;
    private readonly IShardTopology _topology;

    /// <summary>Creates a shard-aware metadata provider that preserves an existing provider.</summary>
    /// <param name="inner">The provider that supplies base entity metadata and column names.</param>
    /// <param name="topology">The topology that supplies the bucket count and the placement mode.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ShardEntityMetadataProvider(IEntityMetadataProvider inner, IShardTopology topology)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(topology);
        _inner = inner;
        _topology = topology;
    }

    /// <inheritdoc />
    public IEntityMetadata GetMetadata(Type entityType, in MappingVariant scope)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (scope == MappingVariant.Default)
            return _inner.GetMetadata(entityType, scope);

        var bucket = ParseBucket(scope.Key, _topology.Ring.BucketCount);
        var metadata = _inner.GetMetadata(entityType, MappingVariant.Default);
        return _topology.PlacementMode switch
        {
            BucketPlacementMode.SchemaPerBucket => new ShardMetadata(
                metadata,
                metadata.Schema is null or "" ? $"bucket_{bucket}" : $"{metadata.Schema}_{bucket}",
                metadata.TableName),
            _ => new ShardMetadata(
                metadata,
                metadata.Schema,
                metadata.TableName is null ? null : $"{metadata.TableName}_{bucket}"),
        };
    }

    /// <inheritdoc />
    public string GetColumnName(PropertyInfo property, in MappingVariant scope, INamingConvention? convention)
        => _inner.GetColumnName(property, scope, convention);

    private static int ParseBucket(string? scopeKey, int bucketCount)
    {
        if (string.IsNullOrEmpty(scopeKey))
            throw new InvalidOperationException("A non-default bucket scope must contain a bucket number.");

        if (!int.TryParse(scopeKey, NumberStyles.None, CultureInfo.InvariantCulture, out var bucket))
            throw new InvalidOperationException($"The mapping variant key '{scopeKey}' is not a bucket number.");

        if ((uint)bucket >= (uint)bucketCount)
        {
            throw new InvalidOperationException(
                $"The mapping variant key '{scopeKey}' is outside [0, {bucketCount}).");
        }

        return bucket;
    }

    private sealed class ShardMetadata(IEntityMetadata inner, string? schema, string? tableName) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
        public string? TableName { get; } = tableName;
        public string? Schema { get; } = schema;
        public bool IsTableNameAuto => inner.IsTableNameAuto;
        public IPropertyMetadata? DynamicColumnsStore => inner.DynamicColumnsStore;
        public IReadOnlyList<IQueryFilterMetadata> Filters => inner.Filters;
        public IReadOnlyList<IRelationshipMetadata> Relationships => inner.Relationships;
    }
}
