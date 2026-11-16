namespace NextORM.Core;

/// <summary>Resolves every entity to the default, unscoped mapping.</summary>
public sealed class DefaultEntityVariantResolver : IEntityVariantResolver
{
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
        return MappingVariant.Default;
    }

    /// <inheritdoc />
    public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(keyValues);
        return MappingVariant.Default;
    }
}
