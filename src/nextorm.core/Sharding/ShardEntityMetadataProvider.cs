using System.Reflection;

namespace NextORM.Core;

/// <summary>Decorates entity metadata with the physical placement selected by a shard scope.</summary>
public sealed class ShardEntityMetadataProvider : IEntityMetadataProvider
{
    private readonly IEntityMetadataProvider _inner;
    private readonly IShardTopology _topology;

    /// <summary>Creates a shard-aware metadata provider that preserves an existing provider.</summary>
    /// <param name="inner">The provider that supplies base entity metadata and column names.</param>
    /// <param name="topology">The topology that supplies per-shard placements.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ShardEntityMetadataProvider(IEntityMetadataProvider inner, IShardTopology topology)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(topology);
        _inner = inner;
        _topology = topology;
    }

    /// <inheritdoc />
    public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (scope == MappingScope.Default)
            return _inner.GetMetadata(entityType, scope);

        var defaultScope = MappingScope.Default;
        var metadata = _inner.GetMetadata(entityType, defaultScope);
        var shardId = scope.Key;
        if (string.IsNullOrEmpty(shardId))
            throw new InvalidOperationException("A non-default shard scope must contain a shard id.");

        var placement = _topology.GetPlacement(entityType, shardId);
        var tableName = placement.TableName
            ?? (metadata.TableName is null ? null : $"{metadata.TableName}_{shardId}");
        return new ShardMetadata(metadata, placement.Schema ?? metadata.Schema, tableName);
    }

    /// <inheritdoc />
    public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention)
        => _inner.GetColumnName(property, scope, convention);

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
