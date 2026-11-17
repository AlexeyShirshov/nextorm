namespace NextORM.Core;

/// <summary>Resolves the mapping variant for an entity type or value source.</summary>
public interface IEntityVariantResolver
{
    /// <summary>Resolves the mapping variant from the entity type alone.</summary>
    MappingVariant Resolve(Type entityType);

    /// <summary>Resolves the mapping variant from a concrete entity instance.</summary>
    MappingVariant Resolve(Type entityType, object entity);

    /// <summary>Resolves the mapping variant from explicit key values.</summary>
    MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues);
}
