namespace NextORM.Core;

/// <summary>Resolves every entity to the default, unscoped mapping.</summary>
public sealed class DefaultEntityScopeResolver : IEntityScopeResolver
{
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
        return MappingScope.Default;
    }

    /// <inheritdoc />
    public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(keyValues);
        return MappingScope.Default;
    }
}
