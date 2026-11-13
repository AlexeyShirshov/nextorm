using System.Reflection;

namespace NextORM.Core;

/// <summary>Resolves metadata by using the standard reflection-based entity mapping.</summary>
public sealed class DefaultEntityMetadataProvider : IEntityMetadataProvider
{
    /// <inheritdoc />
    public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (scope == MappingScope.Default)
        {
            lock (DataContextCache.MetadataRegistrationGate)
            {
                if (DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
                    return metadata;
            }

            var built = BuildMetadata(entityType);
            lock (DataContextCache.MetadataRegistrationGate)
            {
                if (DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
                    return metadata;
                DataContextCache.Metadata[entityType] = built;
                return built;
            }
        }

        return DataContextCache.ScopedMetadata.GetOrAdd(
            (entityType, scope), key => BuildMetadata(key.Type));
    }

    /// <inheritdoc />
    public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention)
    {
        ArgumentNullException.ThrowIfNull(property);
        var metadata = GetMetadata(property.DeclaringType ?? throw new ArgumentException("Property must have a declaring type.", nameof(property)), scope);
        var mapped = metadata.Properties.FirstOrDefault(p => p.PropertyInfo == property);
        if (mapped is null)
            return convention?.ColumnName(property.Name) ?? property.Name;
        return mapped.IsColumnNameAuto && convention is not null
            ? convention.ColumnName(mapped.ColumnName)
            : mapped.ColumnName;
    }

    private static IEntityMetadata BuildMetadata(Type entityType)
    {
        var builderType = typeof(EntityMetadataBuilder<>).MakeGenericType(entityType);
        var builder = Activator.CreateInstance(builderType)
            ?? throw new InvalidOperationException($"Could not create metadata builder for {entityType}.");
        return (IEntityMetadata)(builderType.GetMethod(nameof(EntityMetadataBuilder<object>.AutoBuild))!.Invoke(builder, null)
            ?? throw new InvalidOperationException($"Could not build metadata for {entityType}."));
    }
}
