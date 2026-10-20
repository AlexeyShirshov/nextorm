using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;
/// <summary>
/// Helpers for resolving a member's column name and for looking up its metadata in the shared entity
/// metadata cache.
/// </summary>
public static class MemberInfoExtensions
{
    // Entity metadata is registered once per type (DataContextCache.Metadata only builds on a miss),
    // so the resolved column name is stable for a given naming convention for the lifetime of the
    // process. Caching it turns the per-member metadata lookup + linear scan into a dictionary read
    // on the SQL-build path.
    private static readonly ConcurrentDictionary<(PropertyInfo, INamingConvention?, Type?), string> _columnNames = new();

    /// <summary>
    /// Drops the cached column names, all of them or only those of <paramref name="entityType"/>. Called
    /// when the process-wide metadata cache is cleared and when an already-published mapping is replaced
    /// by a configured one (the many-to-many junction auto-publish), so a column name resolved against
    /// the superseded metadata cannot be served afterwards.
    /// </summary>
    /// <param name="entityType">The owning entity type to invalidate, or <see langword="null"/> for all.</param>
    internal static void ClearColumnNames(Type? entityType = null)
    {
        if (entityType is null)
        {
            _columnNames.Clear();
            return;
        }

        foreach (var key in _columnNames.Keys)
        {
            if (key.Item1.DeclaringType == entityType
                || key.Item1.ReflectedType == entityType
                || key.Item3 == entityType)
                _columnNames.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Column name registered for the member in the shared entity metadata cache, or an empty string
    /// when the metadata does not declare it. Auto-derived names are translated through
    /// <paramref name="convention"/>; declared names are returned verbatim.
    /// </summary>
    /// <param name="mi">The CLR member to resolve.</param>
    /// <param name="convention">The naming convention applied to auto-derived names, or <see langword="null"/>.</param>
    public static string GetPropertyColumnName(this MemberInfo mi, INamingConvention? convention = null)
        => GetPropertyColumnName(mi, convention, null);

    /// <summary>
    /// Column name registered for the member in the shared entity metadata cache, or an empty string
    /// when the metadata does not declare it. Auto-derived names are translated through
    /// <paramref name="convention"/>; declared names are returned verbatim.
    /// </summary>
    /// <param name="mi">The CLR member to resolve.</param>
    /// <param name="convention">The naming convention applied to auto-derived names, or <see langword="null"/>.</param>
    /// <param name="entityType">
    /// The entity type the member is accessed through. Required for a member inherited from a base class:
    /// the member is reflected against the base type, while the mapping (and any fluent column override)
    /// belongs to the derived entity. When <see langword="null"/>, the reflected and declaring types are
    /// used as a fallback.
    /// </param>
    internal static string GetPropertyColumnName(this MemberInfo mi, INamingConvention? convention, Type? entityType)
    {
        if (mi is PropertyInfo pi)
        {
            var key = (pi, convention, entityType);

            if (_columnNames.TryGetValue(key, out var cached))
                return cached;

            // A member of a lambda over a derived entity is reflected against the type that declares
            // it (DeclaringType), not the entity type, and metadata is keyed by the entity type. When
            // the caller names the entity type, its mapping is authoritative: a property absent from it
            // is not mapped, even if the base type has a mapping of its own.
            IPropertyMetadata? prop = null;
            var resolved = false;

            if (entityType is not null && DataContextCache.Metadata.TryGetValue(entityType, out _))
            {
                resolved = DataContextCache.Metadata.TryGetValue(entityType, pi, out prop);
            }
            else
            {
                if (pi.ReflectedType is { } reflected)
                    resolved = DataContextCache.Metadata.TryGetValue(reflected, pi, out prop);

                if (!resolved && pi.DeclaringType is { } declaring && declaring != pi.ReflectedType)
                    resolved = DataContextCache.Metadata.TryGetValue(declaring, pi, out prop);
            }

            if (resolved)
            {
                var name = prop!.ColumnName;
                if (prop.IsColumnNameAuto && convention is not null)
                    name = convention.ColumnName(name);

                _columnNames.TryAdd(key, name);
                return name;
            }

            if (entityType is not null && DataContextCache.Metadata.ContainsKey(entityType))
                _columnNames.TryAdd(key, string.Empty);
        }

        return string.Empty;
    }

    /// <summary>
    /// Whether two <see cref="PropertyInfo"/> instances denote the same declared property. Reflection
    /// creates distinct instances whose equality includes <see cref="MemberInfo.ReflectedType"/>: a
    /// property inherited by a derived entity is reflected as the base declaration, while metadata built
    /// for the derived entity holds the same token reflected against the derived type. Comparing the
    /// module and metadata token treats both as the same mapping.
    /// </summary>
    internal static bool SameProperty(PropertyInfo left, PropertyInfo right)
        => ReferenceEquals(left, right)
            || (left.Module == right.Module && left.MetadataToken == right.MetadataToken);
    /// <summary>
    /// Looks up the metadata for <paramref name="pi"/> in the entity metadata registered for
    /// <paramref name="type"/>. Because the metadata is keyed by type, the property is matched by
    /// scanning that entity's properties rather than by a direct dictionary lookup.
    /// </summary>
    /// <param name="dic">Entity metadata cache keyed by entity type.</param>
    /// <param name="type">The entity type whose metadata should be searched.</param>
    /// <param name="pi">The property to find in the entity's metadata.</param>
    /// <param name="prop">When this method returns <see langword="true"/>, the matching property metadata; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the type and property are both found; otherwise <see langword="false"/>.</returns>
    public static bool TryGetValue(this IDictionary<Type, IEntityMetadata> dic, Type type, PropertyInfo pi, out IPropertyMetadata? prop)
    {
        if (dic.TryGetValue(type, out var entity))
        {
            foreach (var item in entity.Properties)
            {
                if (SameProperty(item.PropertyInfo, pi))
                {
                    prop = item;
                    return true;
                }
            }
        }
        prop = null;
        return false;
    }
}
