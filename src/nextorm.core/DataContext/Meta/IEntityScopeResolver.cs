namespace NextORM.Core;

/// <summary>Resolves the mapping variant for an entity type or value source.</summary>
public interface IEntityScopeResolver
{
    /// <summary>Resolves a scope from the entity type alone.</summary>
    MappingScope Resolve(Type entityType);

    /// <summary>Resolves a scope from a concrete entity instance.</summary>
    MappingScope Resolve(Type entityType, object entity);

    /// <summary>Resolves a scope from explicit key values.</summary>
    MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues);
}
