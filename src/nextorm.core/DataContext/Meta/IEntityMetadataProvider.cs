using System.Reflection;

namespace NextORM.Core;

/// <summary>Resolves entity mapping metadata and column names for a mapping scope.</summary>
public interface IEntityMetadataProvider
{
    /// <summary>Metadata for <paramref name="entityType"/> under <paramref name="scope"/>.</summary>
    IEntityMetadata GetMetadata(Type entityType, in MappingScope scope);

    /// <summary>Column name for <paramref name="property"/> under <paramref name="scope"/>.</summary>
    string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention);
}
